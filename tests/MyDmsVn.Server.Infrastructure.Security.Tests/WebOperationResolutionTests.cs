using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Xunit;
using static MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationResolutionScenario;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationResolutionTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Separate_operator_inspects_absence_and_terminal_without_original_user_grants_or_session() => Owned(async s =>
    {
        await s.Observer.ExecuteAsync("CREATE USER WebInspectProbe WITHOUT LOGIN; ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER WebInspectProbe;");
        var operation = Operation(s); var writer = await s.Transaction(); await Record(s, writer, operation); writer.Tx.Commit();
        await s.Observer.ExecuteAsync("DELETE dbo.RolePermissions; UPDATE dbo.WebAuthSessions SET RevokedAtUtc=SYSUTCDATETIME();");
        var before = await s.Observer.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.WebOperationTerminalAudit FOR JSON PATH,INCLUDE_NULL_VALUES);");
        var inspector = await s.Transaction(); await inspector.Sql.ExecuteAsync("EXECUTE AS USER=N'WebInspectProbe';", transaction: inspector.Tx);
        var maintenance = new SqlWebOperationResolutionMaintenance(inspector);
        var terminal = await maintenance.InspectAsync(s.Epoch, s.Actor, operation.OperationId, operation.Fingerprint, s.Stop.Token);
        Assert.Equal("CANCELLED", terminal.Disposition); Assert.Equal(operation.OperationId, terminal.OperationId); Assert.NotNull(terminal.Terminal);
        Assert.False(terminal.OriginalResultRetained); Assert.False(terminal.OriginalVersionRetained); Assert.False(terminal.OriginalLineIdsRetained);
        var absent = await maintenance.InspectAsync(s.Epoch, s.Actor, Guid.NewGuid(), operation.Fingerprint, s.Stop.Token);
        Assert.Equal("INDETERMINATE", absent.Disposition); Assert.Null(absent.Terminal); Assert.Null(absent.Kind);
        var foreign = await maintenance.InspectAsync(s.Epoch, s.Actor + 100, operation.OperationId, operation.Fingerprint, s.Stop.Token);
        Assert.Equal("INDETERMINATE", foreign.Disposition); Assert.Null(foreign.Terminal);
        inspector.Tx.Commit();
        Assert.Equal(before, await s.Observer.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.WebOperationTerminalAudit FOR JSON PATH,INCLUDE_NULL_VALUES);"));
        Assert.Equal(1, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData(false)]
    [InlineData(true)]
    public Task Operator_inspection_reports_retention_without_returning_business_bytes_and_never_writes(bool expired) => Owned(async s =>
    {
        await s.Observer.ExecuteAsync("CREATE USER WebInspectProbe WITHOUT LOGIN; ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER WebInspectProbe;");
        var operation = Operation(s);
        var payload = Encoding.UTF8.GetBytes("{\"data\":{\"version\":\"AAAAAAAAB9E=\",\"lines\":[{\"saleLineId\":\"41\"}],\"note\":\"private-original-payload\"}}");
        await s.Observer.ExecuteAsync("DECLARE @n datetime2(7)=DATEADD(day,@days,SYSUTCDATETIME()); INSERT dbo.WebOperationJournal VALUES(@actor,@id,@epoch,1,1,1,NULL,@hash,41,'COMMITTED',@bytes,@n,@n,DATEADD(day,7,@n));",
            new { actor = s.Actor, id = operation.OperationId, epoch = s.Epoch, hash = operation.Fingerprint, bytes = payload, days = expired ? -8 : 0 });
        var before = await s.Observer.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.WebOperationJournal FOR JSON PATH,INCLUDE_NULL_VALUES);");
        var inspector = await s.Transaction(); await inspector.Sql.ExecuteAsync("EXECUTE AS USER=N'WebInspectProbe';", transaction: inspector.Tx);
        var result = await new SqlWebOperationResolutionMaintenance(inspector).InspectAsync(s.Epoch, s.Actor, operation.OperationId, operation.Fingerprint, s.Stop.Token);
        Assert.Equal(expired ? "EXPIRED" : "COMMITTED", result.Disposition); Assert.Null(result.Terminal);
        Assert.Equal(!expired, result.OriginalResultRetained); Assert.Equal(!expired, result.OriginalVersionRetained); Assert.Equal(!expired, result.OriginalLineIdsRetained);
        var summary = Newtonsoft.Json.JsonConvert.SerializeObject(result);
        Assert.DoesNotContain("private-original-payload", summary); Assert.DoesNotContain("AAAAAAAAB9E=", summary);
        inspector.Tx.Commit();
        Assert.Equal(before, await s.Observer.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.WebOperationJournal FOR JSON PATH,INCLUDE_NULL_VALUES);"));
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Operator_inspection_rejects_runtime_stale_epoch_and_wrong_fingerprint() => Owned(async s =>
    {
        var operation = Operation(s); var writer = await s.Transaction(); await Record(s, writer, operation); writer.Tx.Commit();
        await s.Observer.ExecuteAsync("CREATE USER WebInspectRuntimeProbe WITHOUT LOGIN;");
        var runtime = await s.Transaction(); await runtime.Sql.ExecuteAsync("EXECUTE AS USER=N'WebInspectRuntimeProbe';", transaction: runtime.Tx);
        Assert.Equal("Auth.Forbidden", (await Assert.ThrowsAsync<WebOperationException>(() => new SqlWebOperationResolutionMaintenance(runtime).InspectAsync(s.Epoch, s.Actor, operation.OperationId, operation.Fingerprint, s.Stop.Token))).Code);
        if (runtime.Tx.Connection != null) runtime.Tx.Rollback();
        var stale = await s.Transaction();
        Assert.Equal("Deployment.EpochMismatch", (await Assert.ThrowsAsync<WebOperationException>(() => new SqlWebOperationResolutionMaintenance(stale).InspectAsync(Guid.NewGuid(), s.Actor, operation.OperationId, operation.Fingerprint, s.Stop.Token))).Code);
        if (stale.Tx.Connection != null) stale.Tx.Rollback();
        var mismatch = await s.Transaction();
        Assert.Equal("Idempotency.FingerprintMismatch", (await Assert.ThrowsAsync<WebOperationException>(() => new SqlWebOperationResolutionMaintenance(mismatch).InspectAsync(s.Epoch, s.Actor, operation.OperationId, new byte[32], s.Stop.Token))).Code);
        if (mismatch.Tx.Connection != null) mismatch.Tx.Rollback();
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Limited_runtime_cannot_execute_operator_or_write_audit_but_separate_operator_can() => Owned(async s =>
    {
        await s.Observer.ExecuteAsync(@"CREATE USER WebResolutionRuntimeProbe WITHOUT LOGIN;
GRANT SELECT,INSERT ON OBJECT::dbo.WebOperationJournal TO WebResolutionRuntimeProbe;
GRANT SELECT ON OBJECT::dbo.WebOperationTerminalAudit TO WebResolutionRuntimeProbe;
CREATE USER WebResolutionOperatorProbe WITHOUT LOGIN;
ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER WebResolutionOperatorProbe;");
        var operation = Operation(s);
        var runtime = await s.Transaction();
        await runtime.Sql.ExecuteAsync("EXECUTE AS USER=N'WebResolutionRuntimeProbe';", transaction: runtime.Tx);
        Assert.Equal(0, await runtime.Sql.QuerySingleAsync<int>("SELECT HAS_PERMS_BY_NAME(N'dbo.WebOperationTerminalAudit',N'OBJECT',N'INSERT');", transaction: runtime.Tx));
        Assert.Equal(0, await runtime.Sql.QuerySingleAsync<int>("SELECT HAS_PERMS_BY_NAME(N'dbo.RecordWebOperationNotExecuted',N'OBJECT',N'EXECUTE');", transaction: runtime.Tx));
        Assert.NotNull(await Xunit.Record.ExceptionAsync(() => Record(s, runtime, operation)));
        if (runtime.Tx.Connection != null) runtime.Tx.Rollback();
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
        var separate = await s.Transaction();
        await separate.Sql.ExecuteAsync("EXECUTE AS USER=N'WebResolutionOperatorProbe';", transaction: separate.Tx);
        Assert.Equal(0, await separate.Sql.QuerySingleAsync<int>("SELECT HAS_PERMS_BY_NAME(N'dbo.WebOperationTerminalAudit',N'OBJECT',N'INSERT');", transaction: separate.Tx));
        await Record(s, separate, operation); separate.Tx.Commit();
        Assert.Equal("NOT_EXECUTED", (await Read(s, operation))!.Status.State);
        Assert.Equal("WebResolutionOperatorProbe", await s.Observer.QuerySingleAsync<string>("SELECT OperatorDatabaseUser FROM dbo.WebOperationTerminalAudit;"));
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData("DELETE dbo.WebOperationTerminalAudit;")]
    [InlineData("UPDATE dbo.WebOperationTerminalAudit SET RequestFingerprint=CONVERT(binary(32),0x01);")]
    [InlineData("UPDATE dbo.WebOperationTerminalAudit SET DeploymentEpoch=NEWID();")]
    [InlineData("UPDATE dbo.WebOperationTerminalAudit SET DecidedAtUtc=DATEADD(second,-1,DecidedAtUtc);")]
    public Task Missing_or_mismatched_audit_never_certifies_cancelled(string corruption) => Owned(async s =>
    {
        var operation = Operation(s); var writer = await s.Transaction(); await Record(s, writer, operation); writer.Tx.Commit();
        // An administrator deliberately corrupts this owned fixture, then restores the guard.
        await s.Observer.ExecuteAsync("DISABLE TRIGGER dbo.TR_WebOperationTerminalAudit_Immutable ON dbo.WebOperationTerminalAudit; " + corruption + " ENABLE TRIGGER dbo.TR_WebOperationTerminalAudit_Immutable ON dbo.WebOperationTerminalAudit;");
        var result = await Read(s, operation);
        Assert.Equal("INDETERMINATE", result!.Status.State); Assert.Null(result.Terminal); Assert.Null(result.Status.ResultBytes);
        Assert.Equal("CANCELLED", await s.Observer.QuerySingleAsync<string>("SELECT Disposition FROM dbo.WebOperationJournal;"));
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData(WebOperationKind.SalesCreate)]
    [InlineData(WebOperationKind.SalesUpdate)]
    [InlineData(WebOperationKind.SalesPost)]
    [InlineData(WebOperationKind.ReceiptCreate)]
    [InlineData(WebOperationKind.ReceiptUpdate)]
    [InlineData(WebOperationKind.ReceiptPost)]
    public Task Durable_negative_requires_original_grant_even_when_Read_denied_and_never_changes_v1(WebOperationKind kind) => Owned(async s =>
    {
        await s.Observer.ExecuteAsync("DELETE dbo.RolePermissions; INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@role,@permission);", new { role = s.Role, permission = WebOperationContext.Permission(kind) });
        var operation = Operation(s, kind); var decision = Guid.NewGuid();
        var writer = await s.Transaction(); await Record(s, writer, operation, decision); writer.Tx.Commit();
        var resolution = await Read(s, operation);
        Assert.NotNull(resolution); Assert.Equal("NOT_EXECUTED", resolution!.Status.State);
        Assert.Equal(operation.OperationId, resolution.Status.OperationId); Assert.Equal(operation.Fingerprint, resolution.Status.RequestFingerprint);
        Assert.Equal(kind, resolution.Status.Kind); Assert.Equal(operation.RequestTargetId, resolution.Status.RequestTargetId);
        Assert.Null(resolution.Status.ResultBytes); Assert.Null(resolution.Status.ResultDocumentId); Assert.NotNull(resolution.Terminal);
        Assert.Equal(decision, resolution.Terminal!.DecisionId); Assert.Equal("case-123", resolution.Terminal.CaseId);
        var legacy = await s.Transaction();
        var v1 = await new SqlWebOperationReadRepository(legacy, new WebOperationJournalTests.ActorAccessor(s.Actor), Registry())
            .ReadStatusAsync(s.Actor, s.Session, s.Epoch, operation.OperationId, operation.Fingerprint, s.Stop.Token);
        Assert.Equal("RESULT_UNAVAILABLE", v1!.State); legacy.Tx.Commit();
        await s.Observer.ExecuteAsync("DELETE dbo.RolePermissions; INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@role,@read);", new { role = s.Role, read = (byte)kind <= (byte)WebOperationKind.SalesPost ? "Sales.Receipts.Read" : "Inventory.GoodsReceipts.Read" });
        var denied = await s.Transaction();
        Assert.Equal("Auth.Forbidden", (await Assert.ThrowsAsync<WebOperationException>(() => Reads(s, denied).ReadResolutionAsync(s.Actor, s.Session, s.Epoch, operation.OperationId, operation.Fingerprint, s.Stop.Token))).Code);
        denied.Tx.Rollback();
        Assert.Equal(1, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
        Assert.Equal(1, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Products;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Missing_row_and_other_actor_are_absent_not_terminal_certificates() => Owned(async s =>
    {
        var operation = Operation(s); Assert.Null(await Read(s, operation));
        var transaction = await s.Transaction(); await Record(s, transaction, operation); transaction.Tx.Commit();
        var other = await s.Observer.QuerySingleAsync<int>("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'other',N'OTHER',N'Other',N'Local',N'hash',N'',N'BCrypt');");
        var session = Guid.NewGuid();
        await s.Observer.ExecuteAsync("DECLARE @n datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebAuthSessions VALUES(@session,@actor,@n,DATEADD(day,30,@n),DATEADD(day,7,@n),NULL);", new { session, actor = other });
        var read = await s.Transaction();
        var repository = new SqlWebOperationResolutionRepository(read, new WebOperationJournalTests.ActorAccessor(other), Registry());
        Assert.Null(await repository.ReadResolutionAsync(other, session, s.Epoch, operation.OperationId, new byte[32], s.Stop.Token));
        read.Tx.Rollback();
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData("session", "Auth.Unauthorized")]
    [InlineData("account", "Auth.Unauthorized")]
    [InlineData("epoch", "Deployment.EpochMismatch")]
    [InlineData("fingerprint", "Idempotency.FingerprintMismatch")]
    public Task Current_identity_epoch_and_original_fingerprint_are_rechecked(string change, string error) => Owned(async s =>
    {
        var operation = Operation(s); var writer = await s.Transaction(); await Record(s, writer, operation); writer.Tx.Commit();
        if (change == "session" || change == "account") await s.Observer.ExecuteAsync(s.Change(change));
        var read = await s.Transaction();
        Assert.Equal(error, (await Assert.ThrowsAsync<WebOperationException>(() => Reads(s, read).ReadResolutionAsync(s.Actor, s.Session,
            change == "epoch" ? Guid.NewGuid() : s.Epoch, operation.OperationId, change == "fingerprint" ? new byte[32] : operation.Fingerprint, s.Stop.Token))).Code);
        read.Tx.Rollback();
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Audit_failure_and_caller_rollback_leave_no_negative_then_lost_ack_requires_independent_read() => Owned(async s =>
    {
        var operation = Operation(s);
        await s.Observer.ExecuteAsync("CREATE TRIGGER dbo.TestRejectTerminalAudit ON dbo.WebOperationTerminalAudit AFTER INSERT AS BEGIN THROW 51997, 'Injected audit failure.',1; END;");
        var failed = await s.Transaction();
        Assert.NotNull(await Xunit.Record.ExceptionAsync(() => Record(s, failed, operation)));
        if (failed.Tx.Connection != null) failed.Tx.Rollback();
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
        await s.Observer.ExecuteAsync("DROP TRIGGER dbo.TestRejectTerminalAudit;");
        var rolledBack = await s.Transaction(); await Record(s, rolledBack, operation); rolledBack.Tx.Rollback();
        Assert.Null(await Read(s, operation));
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
        var committed = await s.Transaction(); await Record(s, committed, operation); committed.Tx.Commit();
        // No success receipt is used: only a new authenticated read establishes the decision.
        Assert.Equal("NOT_EXECUTED", (await Read(s, operation))!.Status.State);
        var duplicate = await s.Transaction();
        Assert.Equal("Operation.ResolutionConflict", (await Assert.ThrowsAsync<WebOperationException>(() => Record(s, duplicate, operation))).Code);
        duplicate.Tx.Rollback();
        Assert.Equal(1, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData(false)]
    [InlineData(true)]
    public Task Committed_and_expired_original_result_never_become_negative(bool expired) => Owned(async s =>
    {
        var operation = Operation(s);
        await s.Observer.ExecuteAsync("DECLARE @n datetime2(7)=DATEADD(day,@days,SYSUTCDATETIME()); INSERT dbo.WebOperationJournal VALUES(@actor,@id,@epoch,1,1,1,NULL,@hash,41,'COMMITTED',@bytes,@n,@n,DATEADD(day,7,@n));",
            new { actor = s.Actor, id = operation.OperationId, epoch = s.Epoch, hash = operation.Fingerprint, bytes = Encoding.UTF8.GetBytes("{\"data\":\"original\"}"), days = expired ? -8 : 0 });
        var attempted = await s.Transaction();
        Assert.Equal("Operation.ResolutionConflict", (await Assert.ThrowsAsync<WebOperationException>(() => Record(s, attempted, operation))).Code); attempted.Tx.Rollback();
        var result = await Read(s, operation);
        Assert.Equal(expired ? "RESULT_EXPIRED" : "COMMITTED", result!.Status.State); Assert.Null(result.Terminal);
        if (!expired) Assert.Equal("{\"data\":\"original\"}", Encoding.UTF8.GetString(result.Status.ResultBytes!));
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Runtime_insert_cannot_mint_cancelled_without_operator_audit_and_audit_is_immutable() => Owned(async s =>
    {
        var operation = Operation(s);
        await Assert.ThrowsAsync<SqlException>(() => s.Observer.ExecuteAsync("DECLARE @n datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebOperationJournal VALUES(@actor,@id,@epoch,1,1,1,NULL,@hash,NULL,'CANCELLED',NULL,@n,@n,NULL);", new { actor = s.Actor, id = operation.OperationId, epoch = s.Epoch, hash = operation.Fingerprint }));
        var transaction = await s.Transaction(); await Record(s, transaction, operation); transaction.Tx.Commit();
        await Assert.ThrowsAsync<SqlException>(() => s.Observer.ExecuteAsync("UPDATE dbo.WebOperationTerminalAudit SET CaseId=N'changed';"));
        await Assert.ThrowsAsync<SqlException>(() => s.Observer.ExecuteAsync("DELETE dbo.WebOperationTerminalAudit;"));
        Assert.Equal("case-123", (await Read(s, operation))!.Terminal!.CaseId);
    });
}
