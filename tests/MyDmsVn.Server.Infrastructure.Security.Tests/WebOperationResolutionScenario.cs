using System;
using System.Linq;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Xunit;
using static MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationJournalTests;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal static class WebOperationResolutionScenario
{
    internal static IPermissionRegistry Registry() => PermissionRegistry.Create(new[]
    {
        new PermissionDefinition(PermissionKeys.SalesReceiptsWrite, "Sales", "Write"),
        new PermissionDefinition(PermissionKeys.SalesReceiptsPost, "Sales", "Post"),
        new PermissionDefinition(PermissionKeys.SalesReceiptsRead, "Sales", "Read"),
        new PermissionDefinition(PermissionKeys.InventoryGoodsReceiptsWrite, "Receipt", "Write"),
        new PermissionDefinition(PermissionKeys.InventoryGoodsReceiptsPost, "Receipt", "Post"),
        new PermissionDefinition(PermissionKeys.InventoryGoodsReceiptsRead, "Receipt", "Read")
    });

    internal static WebOperationContext Operation(Scenario s, WebOperationKind kind = WebOperationKind.SalesCreate) =>
        new WebOperationContext(Guid.NewGuid(), s.Epoch, s.Session, kind,
            kind == WebOperationKind.SalesCreate || kind == WebOperationKind.ReceiptCreate ? (long?)null : 41L,
            Enumerable.Repeat((byte)7, 32).ToArray(), new UnusedCodec());

    internal static Task Record(Scenario s, Context transaction, WebOperationContext operation, Guid? decision = null) =>
        new SqlWebOperationResolutionMaintenance(transaction).RecordNotExecutedAsync(operation, s.Actor,
            decision ?? Guid.NewGuid(), "case-123", "ORIGINAL_NOT_EXECUTED", "evidence-123", s.Stop.Token);

    internal static SqlWebOperationResolutionRepository Reads(Scenario s, Context transaction) =>
        new SqlWebOperationResolutionRepository(transaction, new ActorAccessor(s.Actor), Registry());

    internal static async Task<WebOperationResolution?> Read(Scenario s, WebOperationContext operation)
    {
        var transaction = await s.Transaction();
        var result = await Reads(s, transaction).ReadResolutionAsync(s.Actor, s.Session, s.Epoch, operation.OperationId, operation.Fingerprint, s.Stop.Token);
        transaction.Tx.Commit();
        return result;
    }

    internal static async Task Owned(Func<Scenario, Task> body)
    {
        Assert.False(WebOperationCrashProcess.IsPoisoned, "Prior process cleanup was uncertain; no new SQL allocation is admitted.");
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Scenario? scenario = null; Exception? failure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().MigrateWeb5(db.ConnectionString).Successful);
            scenario = await Scenario.Create(db);
            await body(scenario);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try
            {
                if (WebOperationCrashProcess.IsPoisoned) throw new InvalidOperationException("Process cleanup is uncertain; owned SQL allocation quarantined without drop.");
                if (scenario == null) await db.DisposeAsync(); else await scenario.Cleanup();
            }
            catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); }
        }
    }

    private sealed class UnusedCodec : IWebOperationResultCodec
    {
        public byte[] Encode<T>(WebOperationKind kind, T result) => throw new NotSupportedException();
        public T Decode<T>(WebOperationKind kind, byte[] bytes) => throw new NotSupportedException();
    }
}
