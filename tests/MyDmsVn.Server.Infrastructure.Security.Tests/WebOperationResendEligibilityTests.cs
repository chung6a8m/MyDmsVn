using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Xunit;
using static MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationResolutionScenario;
using ActorAccessor = MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationJournalTests.ActorAccessor;
using Context = MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationJournalTests.Context;
using Scenario = MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationJournalTests.Scenario;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationResendEligibilityTests
{
    private static readonly WebOperationKind[] Kinds =
    {
        WebOperationKind.SalesCreate, WebOperationKind.SalesUpdate, WebOperationKind.SalesPost,
        WebOperationKind.ReceiptCreate, WebOperationKind.ReceiptUpdate, WebOperationKind.ReceiptPost
    };

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Absent_identity_requires_each_original_grant_and_both_reads_are_nonmutating() => Owned(async s =>
    {
        foreach (var kind in Kinds)
        {
            await s.Observer.ExecuteAsync("DELETE dbo.RolePermissions; INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@role,@permission);",
                new { role = s.Role, permission = WebOperationContext.Permission(kind) });
            var operation = Operation(s, kind);
            var transaction = await s.Transaction();
            var repository = Repository(s, transaction);
            await repository.AuthorizeReceiptAsync(s.Actor, s.Session, s.Epoch, kind, s.Stop.Token);
            Assert.Null(await repository.ReadEligibilityAsync(s.Actor, s.Session, s.Epoch, operation.OperationId,
                kind, operation.RequestTargetId, operation.Fingerprint, s.Stop.Token));
            transaction.Tx.Commit();
        }
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Products;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Read_permission_session_epoch_and_immutable_mismatches_fail_closed() => Owned(async s =>
    {
        var operation = Operation(s, WebOperationKind.SalesUpdate);
        await s.Observer.ExecuteAsync("DELETE dbo.RolePermissions; INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@role,N'Sales.Receipts.Read');", new { role = s.Role });
        var denied = await s.Transaction();
        Assert.Equal("Auth.Forbidden", (await Assert.ThrowsAsync<WebOperationException>(() => Repository(s, denied)
            .AuthorizeReceiptAsync(s.Actor, s.Session, s.Epoch, operation.Kind, s.Stop.Token))).Code);
        denied.Tx.Rollback();

        await s.Observer.ExecuteAsync("DELETE dbo.RolePermissions; INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@role,@permission);",
            new { role = s.Role, permission = WebOperationContext.Permission(operation.Kind) });
        await s.Observer.ExecuteAsync("INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@role,@permission);",
            new { role = s.Role, permission = WebOperationContext.Permission(WebOperationKind.ReceiptUpdate) });
        await InsertCommitted(s, operation, expired: false, bytes: Encoding.UTF8.GetBytes("{\"data\":\"original\"}"));

        foreach (var defect in new[] { "epoch", "fingerprint", "kind", "target" })
        {
            var transaction = await s.Transaction();
            var error = await Assert.ThrowsAsync<WebOperationException>(() => Repository(s, transaction).ReadEligibilityAsync(
                s.Actor, s.Session, defect == "epoch" ? Guid.NewGuid() : s.Epoch, operation.OperationId,
                defect == "kind" ? WebOperationKind.ReceiptUpdate : operation.Kind,
                defect == "target" ? operation.RequestTargetId + 1 : operation.RequestTargetId,
                defect == "fingerprint" ? new byte[32] : operation.Fingerprint, s.Stop.Token));
            Assert.Equal(defect == "epoch" ? "Deployment.EpochMismatch" : defect == "fingerprint" ? "Idempotency.FingerprintMismatch" : "Idempotency.KeyConflict", error.Code);
            transaction.Tx.Rollback();
        }

        await s.Observer.ExecuteAsync("UPDATE dbo.WebAuthSessions SET RevokedAtUtc=SYSUTCDATETIME();");
        var revoked = await s.Transaction();
        Assert.Equal("Auth.Unauthorized", (await Assert.ThrowsAsync<WebOperationException>(() => Repository(s, revoked)
            .ReadEligibilityAsync(s.Actor, s.Session, s.Epoch, operation.OperationId, operation.Kind,
                operation.RequestTargetId, operation.Fingerprint, s.Stop.Token))).Code);
        revoked.Tx.Rollback();
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Existing_positive_negative_expired_and_incomplete_terminal_states_are_preserved() => Owned(async s =>
    {
        var committed = Operation(s);
        var unavailable = Operation(s);
        var expired = Operation(s);
        var terminal = Operation(s);
        var incomplete = Operation(s);
        await InsertCommitted(s, committed, false, Encoding.UTF8.GetBytes("{\"data\":\"retained\"}"));
        await InsertCommitted(s, unavailable, false, Encoding.UTF8.GetBytes("{\"data\":\"will-be-corrupted\"}"));
        await s.Observer.ExecuteAsync(@"DISABLE TRIGGER dbo.TR_WebOperationJournal_Immutable ON dbo.WebOperationJournal;
ALTER TABLE dbo.WebOperationJournal NOCHECK CONSTRAINT CK_WebOperationJournal_Result;
UPDATE dbo.WebOperationJournal SET ResultBytes=NULL WHERE OperationId=@id;
ALTER TABLE dbo.WebOperationJournal CHECK CONSTRAINT CK_WebOperationJournal_Result;
ENABLE TRIGGER dbo.TR_WebOperationJournal_Immutable ON dbo.WebOperationJournal;", new { id = unavailable.OperationId });
        await InsertCommitted(s, expired, true, Encoding.UTF8.GetBytes("{\"data\":\"expired\"}"));
        var writer = await s.Transaction(); await Record(s, writer, terminal); writer.Tx.Commit();
        var corrupt = await s.Transaction(); await Record(s, corrupt, incomplete); corrupt.Tx.Commit();
        await s.Observer.ExecuteAsync("DISABLE TRIGGER dbo.TR_WebOperationTerminalAudit_Immutable ON dbo.WebOperationTerminalAudit; DELETE dbo.WebOperationTerminalAudit WHERE OperationId=@id; ENABLE TRIGGER dbo.TR_WebOperationTerminalAudit_Immutable ON dbo.WebOperationTerminalAudit;", new { id = incomplete.OperationId });

        Assert.Equal("COMMITTED", (await Read(s, committed))!.Status.State);
        Assert.Equal("RESULT_UNAVAILABLE", (await Read(s, unavailable))!.Status.State);
        Assert.Equal("RESULT_EXPIRED", (await Read(s, expired))!.Status.State);
        Assert.Equal("NOT_EXECUTED", (await Read(s, terminal))!.Status.State);
        Assert.Equal("INDETERMINATE", (await Read(s, incomplete))!.Status.State);
        Assert.Equal(5, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Other_actor_row_is_indistinguishable_from_absence_after_current_authority() => Owned(async s =>
    {
        var operation = Operation(s);
        await InsertCommitted(s, operation, false, Encoding.UTF8.GetBytes("{\"data\":\"private\"}"));
        var other = await s.Observer.QuerySingleAsync<int>("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'other-resend',N'OTHER-RESEND',N'Other',N'Local',N'hash',N'',N'BCrypt');");
        var session = Guid.NewGuid();
        await s.Observer.ExecuteAsync("DECLARE @n datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebAuthSessions VALUES(@session,@actor,@n,DATEADD(day,30,@n),DATEADD(day,7,@n),NULL); INSERT dbo.UserRoles(UserId,RoleId) VALUES(@actor,@role);", new { session, actor = other, role = s.Role });
        var transaction = await s.Transaction();
        var repository = new SqlWebOperationResendEligibilityRepository(transaction, new ActorAccessor(other), Registry());
        Assert.Null(await repository.ReadEligibilityAsync(other, session, s.Epoch, operation.OperationId, operation.Kind,
            operation.RequestTargetId, operation.Fingerprint, s.Stop.Token));
        transaction.Tx.Commit();
    });

    private static SqlWebOperationResendEligibilityRepository Repository(Scenario s, Context transaction) =>
        new SqlWebOperationResendEligibilityRepository(transaction, new ActorAccessor(s.Actor), Registry());

    private static async Task InsertCommitted(Scenario s, WebOperationContext operation, bool expired, byte[]? bytes)
    {
        await s.Observer.ExecuteAsync(@"DECLARE @n datetime2(7)=DATEADD(day,@days,SYSUTCDATETIME());
INSERT dbo.WebOperationJournal(ActorUserId,OperationId,DeploymentEpoch,ContractVersion,CodecVersion,OperationKind,RequestTargetId,RequestFingerprint,ResultDocumentId,Disposition,ResultBytes,CreatedAtUtc,CompletedAtUtc,ResultExpiresAtUtc)
VALUES(@actor,@id,@epoch,1,1,@kind,@target,@hash,41,'COMMITTED',@bytes,@n,@n,DATEADD(day,7,@n));",
            new { actor = s.Actor, id = operation.OperationId, epoch = s.Epoch, kind = (byte)operation.Kind,
                target = operation.RequestTargetId, hash = operation.Fingerprint, bytes, days = expired ? -8 : 0 });
    }
}
