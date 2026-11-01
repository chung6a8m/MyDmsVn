using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Xunit;
using static MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationJournalTests;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationReadTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Context_uses_current_database_epoch_without_command_grants_and_requires_owned_transaction() => Owned(async s =>
    {
        await s.Observer.ExecuteAsync("DELETE dbo.RolePermissions;");
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserAccessor>(new ActorAccessor(s.Actor));
        services.AddSingleton<IPermissionRegistry>(Registry());
        services.AddSqlPersistence(s.Database.ConnectionString);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var uow = await scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().CreateAsync(s.Stop.Token);
        var reads = uow.Repository<IWebOperationReadRepository>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reads.ReadContextAsync(s.Actor, s.Session, s.Stop.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reads.ReadStatusAsync(s.Actor, s.Session, s.Epoch, Guid.NewGuid(), Hash(), s.Stop.Token));
        uow.BeginTransaction();
        Assert.Equal(s.Epoch, await reads.ReadContextAsync(s.Actor, s.Session, s.Stop.Token));
        uow.Rollback();
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData("session")]
    [InlineData("account")]
    public Task Context_rejects_current_revoked_authority(string change) => Owned(async s =>
    {
        await s.Observer.ExecuteAsync(s.Change(change));
        var read = await s.Transaction();
        Assert.Equal("Auth.Unauthorized", (await Assert.ThrowsAsync<WebOperationException>(() => Reads(s, read).ReadContextAsync(s.Actor, s.Session, s.Stop.Token))).Code);
        read.Tx.Rollback();
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Context_holds_epoch_and_session_until_caller_transaction_finishes() => Owned(async s =>
    {
        var read = await s.Transaction();
        Assert.Equal(s.Epoch, await Reads(s, read).ReadContextAsync(s.Actor, s.Session, s.Stop.Token));
        var writer = await s.Transaction();
        var rotation = s.Track(writer.Sql.ExecuteAsync(new SqlTestCommand("UPDATE dbo.WebDeploymentState SET DeploymentEpoch=NEWID(),RotatedAtUtc=SYSUTCDATETIME(); UPDATE dbo.WebAuthSessions SET RevokedAtUtc=SYSUTCDATETIME();", transaction: writer.Tx, cancellationToken: s.Stop.Token)));
        await s.Blocked(writer.Sql.ServerProcessId, read.Sql.ServerProcessId);
        read.Tx.Commit();
        await rotation;
        writer.Tx.Commit();
        var again = await s.Transaction();
        Assert.Equal("Auth.Unauthorized", (await Assert.ThrowsAsync<WebOperationException>(() => Reads(s, again).ReadContextAsync(s.Actor, s.Session, s.Stop.Token))).Code);
        again.Tx.Rollback();
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData(WebOperationKind.SalesCreate, null, "Sales.Receipts.Write")]
    [InlineData(WebOperationKind.SalesUpdate, 41L, "Sales.Receipts.Write")]
    [InlineData(WebOperationKind.SalesPost, 41L, "Sales.Receipts.Post")]
    [InlineData(WebOperationKind.ReceiptCreate, null, "Inventory.GoodsReceipts.Write")]
    [InlineData(WebOperationKind.ReceiptUpdate, 41L, "Inventory.GoodsReceipts.Write")]
    [InlineData(WebOperationKind.ReceiptPost, 41L, "Inventory.GoodsReceipts.Post")]
    public Task Status_returns_original_owned_command_bytes_and_identity_with_only_required_grant(WebOperationKind kind, long? target, string permission) => Owned(async s =>
    {
        await s.Observer.ExecuteAsync("DELETE dbo.RolePermissions; INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@role,@permission);", new { role = s.Role, permission });
        var id = Guid.NewGuid();
        await Seed(s, id, kind, target);
        var completedAtUtc = await s.Observer.QuerySingleAsync<DateTime>("SELECT CompletedAtUtc FROM dbo.WebOperationJournal WHERE OperationId=@id;", new { id });
        var read = await s.Transaction();
        var result = await Reads(s, read).ReadStatusAsync(s.Actor, s.Session, s.Epoch, id, Hash(), s.Stop.Token);
        Assert.NotNull(result);
        Assert.Equal(id, result!.OperationId);
        Assert.Equal(s.Epoch, result.DeploymentEpoch);
        Assert.Equal(kind, result.Kind);
        Assert.Equal(target, result.RequestTargetId);
        Assert.Equal(Hash(), result.RequestFingerprint);
        Assert.Equal(41L, result.ResultDocumentId);
        Assert.Equal(completedAtUtc, result.CompletedAtUtc);
        Assert.Equal("COMMITTED", result.State);
        Assert.Equal("{\"data\":\"original\"}", Encoding.UTF8.GetString(result.ResultBytes!));
        read.Tx.Commit();
        Assert.Equal(1, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Products;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Status_missing_and_other_actor_are_identically_absent_even_with_wrong_fingerprint() => Owned(async s =>
    {
        var id = Guid.NewGuid();
        await Seed(s, id, WebOperationKind.SalesCreate, null, actor: s.Actor + 1);
        var read = await s.Transaction();
        Assert.Null(await Reads(s, read).ReadStatusAsync(s.Actor, s.Session, s.Epoch, id, new byte[32], s.Stop.Token));
        Assert.Null(await Reads(s, read).ReadStatusAsync(s.Actor, s.Session, s.Epoch, Guid.NewGuid(), new byte[32], s.Stop.Token));
        read.Tx.Rollback();
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Status_checks_required_permission_before_fingerprint_and_epoch_before_disclosure() => Owned(async s =>
    {
        var id = Guid.NewGuid();
        await Seed(s, id, WebOperationKind.SalesCreate, null);
        var wrongHash = await s.Transaction();
        Assert.Equal("Idempotency.FingerprintMismatch", (await Assert.ThrowsAsync<WebOperationException>(() => Reads(s, wrongHash).ReadStatusAsync(s.Actor, s.Session, s.Epoch, id, new byte[32], s.Stop.Token))).Code);
        wrongHash.Tx.Rollback();
        await s.Observer.ExecuteAsync(s.Change("deny"));
        var denied = await s.Transaction();
        Assert.Equal("Auth.Forbidden", (await Assert.ThrowsAsync<WebOperationException>(() => Reads(s, denied).ReadStatusAsync(s.Actor, s.Session, s.Epoch, id, new byte[32], s.Stop.Token))).Code);
        denied.Tx.Rollback();
        var stale = await s.Transaction();
        Assert.Equal("Deployment.EpochMismatch", (await Assert.ThrowsAsync<WebOperationException>(() => Reads(s, stale).ReadStatusAsync(s.Actor, s.Session, Guid.NewGuid(), id, Hash(), s.Stop.Token))).Code);
        stale.Tx.Rollback();
        await s.Observer.ExecuteAsync("DELETE dbo.UserPermissions; UPDATE dbo.WebDeploymentState SET DeploymentEpoch=NEWID(),RotatedAtUtc=SYSUTCDATETIME();");
        var currentEpoch = await s.Observer.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;");
        var oldStoredEpoch = await s.Transaction();
        Assert.Equal("Deployment.EpochMismatch", (await Assert.ThrowsAsync<WebOperationException>(() => Reads(s, oldStoredEpoch).ReadStatusAsync(s.Actor, s.Session, currentEpoch, id, Hash(), s.Stop.Token))).Code);
        oldStoredEpoch.Tx.Rollback();
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData("COMMITTED", true, "RESULT_EXPIRED")]
    [InlineData("EXPIRED", true, "RESULT_EXPIRED")]
    [InlineData("CANCELLED", false, "RESULT_UNAVAILABLE")]
    public Task Status_expired_or_unavailable_keeps_identity_but_discloses_no_result(string disposition, bool expired, string state) => Owned(async s =>
    {
        var id = Guid.NewGuid();
        await Seed(s, id, WebOperationKind.SalesCreate, null, disposition, expired);
        var read = await s.Transaction();
        var result = await Reads(s, read).ReadStatusAsync(s.Actor, s.Session, s.Epoch, id, Hash(), s.Stop.Token);
        Assert.NotNull(result);
        Assert.Equal(state, result!.State);
        Assert.Null(result.ResultBytes);
        Assert.Null(result.RequestTargetId);
        Assert.Equal(id, result.OperationId);
        Assert.Equal(Hash(), result.RequestFingerprint);
        read.Tx.Commit();
        Assert.Equal(disposition, await s.Observer.QuerySingleAsync<string>("SELECT Disposition FROM dbo.WebOperationJournal;"));
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData(true)]
    [InlineData(false)]
    public Task Status_waits_for_original_transaction_and_observes_commit_or_absence_without_writing(bool commit) => Owned(async s =>
    {
        var operation = s.Operation(hash: Hash());
        var original = await s.Transaction();
        var journal = s.Journal(original);
        Assert.Null(await journal.AdmitAsync(operation, s.Actor, s.Stop.Token));
        await journal.CompleteAsync(operation, s.Actor, 41, Encoding.UTF8.GetBytes("original"), s.Stop.Token);
        var read = await s.Transaction();
        var waiting = s.Track(Reads(s, read).ReadStatusAsync(s.Actor, s.Session, s.Epoch, operation.OperationId, Hash(), s.Stop.Token));
        await s.Blocked(read.Sql.ServerProcessId, original.Sql.ServerProcessId);
        if (commit) original.Tx.Commit(); else original.Tx.Rollback();
        var result = await waiting;
        if (commit)
        {
            Assert.Equal("COMMITTED", result!.State);
            Assert.Equal("original", Encoding.UTF8.GetString(result.ResultBytes!));
        }
        else Assert.Null(result);
        read.Tx.Commit();
        Assert.Equal(commit ? 1 : 0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData("session", "Auth.Unauthorized")]
    [InlineData("account", "Auth.Unauthorized")]
    [InlineData("deny", "Auth.Forbidden")]
    [InlineData("membership", "Auth.Forbidden")]
    [InlineData("role", "Auth.Forbidden")]
    [InlineData("grant", "Auth.Forbidden")]
    [InlineData("binding", "Auth.Forbidden")]
    public Task Status_rechecks_current_authority_after_waiting_for_original_key(string change, string error) => Owned(async s =>
    {
        var id = Guid.NewGuid();
        await Seed(s, id, WebOperationKind.SalesCreate, null);
        var holder = await s.Transaction();
        await holder.Sql.ExecuteAsync("EXEC sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0;", new { key = "MyDmsVn.WebOperation:" + s.Actor + ":" + id.ToString("N") }, holder.Tx);
        var read = await s.Transaction();
        var waiting = s.Track(Reads(s, read).ReadStatusAsync(s.Actor, s.Session, s.Epoch, id, Hash(), s.Stop.Token));
        await s.Blocked(read.Sql.ServerProcessId, holder.Sql.ServerProcessId);
        await s.Observer.ExecuteAsync(s.Change(change));
        holder.Tx.Commit();
        Assert.Equal(error, (await Assert.ThrowsAsync<WebOperationException>(async () => await waiting)).Code);
        read.Tx.Rollback();
    });

    private static SqlWebOperationReadRepository Reads(Scenario s, Context context) => new SqlWebOperationReadRepository(context, new ActorAccessor(s.Actor), Registry());
    private static IPermissionRegistry Registry() => PermissionRegistry.Create(new[]
    {
        new PermissionDefinition(PermissionKeys.SalesReceiptsWrite, "Sales", "Write"),
        new PermissionDefinition(PermissionKeys.SalesReceiptsPost, "Sales", "Post"),
        new PermissionDefinition(PermissionKeys.InventoryGoodsReceiptsWrite, "Inventory", "Write"),
        new PermissionDefinition(PermissionKeys.InventoryGoodsReceiptsPost, "Inventory", "Post")
    });
    private static byte[] Hash() => Enumerable.Repeat((byte)0x31, 32).ToArray();
    private static Task Seed(Scenario s, Guid id, WebOperationKind kind, long? target, string disposition = "COMMITTED", bool expired = false, int? actor = null) => s.Observer.ExecuteAsync(@"
INSERT dbo.WebOperationJournal(ActorUserId,OperationId,DeploymentEpoch,ContractVersion,CodecVersion,OperationKind,RequestTargetId,RequestFingerprint,ResultDocumentId,Disposition,ResultBytes,CreatedAtUtc,CompletedAtUtc,ResultExpiresAtUtc)
VALUES(@actor,@id,@epoch,1,1,@kind,@target,@hash,CASE WHEN @disposition='CANCELLED' THEN NULL ELSE 41 END,@disposition,CASE WHEN @disposition='COMMITTED' THEN @bytes ELSE NULL END,@completed,@completed,CASE WHEN @disposition='CANCELLED' THEN NULL ELSE DATEADD(day,7,@completed) END);", new
    {
        actor = actor ?? s.Actor,
        id,
        epoch = s.Epoch,
        kind = (byte)kind,
        target,
        hash = Hash(),
        disposition,
        completed = DateTime.UtcNow.AddDays(expired ? -8 : 0),
        bytes = Encoding.UTF8.GetBytes("{\"data\":\"original\"}")
    });
}
