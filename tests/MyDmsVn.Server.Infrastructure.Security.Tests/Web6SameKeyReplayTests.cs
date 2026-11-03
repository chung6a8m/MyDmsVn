using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.HttpHost;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed partial class WebOperationHttpSqlTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Resend_first_and_two_delayed_same_key_workers_converge_for_all_six_commands() =>
        ResendFirstMultiContenderMatrix();

    private async Task ResendFirstMultiContenderMatrix()
    {
        foreach (var kind in CrashKinds)
            await OwnedCrash(async scope =>
            {
                var arranged = await ArrangeCrash(scope.Data, kind);
                scope.Operation = arranged.Intent;

                var resendFirst = await scope.Start(CrashConfiguration(scope, arranged, "before-commit"));
                await resendFirst.DispatchAsync();
                var barrier = await resendFirst.EventAsync("before-commit");
                var holderSession = (int)barrier["sqlSessionId"]!;
                Assert.True(holderSession > 0);

                var delayedOriginal = await scope.Start(CrashConfiguration(scope, arranged, "none"));
                var secondResend = await scope.Start(CrashConfiguration(scope, arranged, "none"));
                await delayedOriginal.DispatchAsync();
                await secondResend.DispatchAsync();
                using (var observer = await CrashOracle(scope.ConnectionString))
                {
                    await WaitForCrashFence(observer, holderSession, delayedOriginal.ProcessId);
                    await WaitForCrashFence(observer, holderSession, secondResend.ProcessId);
                }

                await resendFirst.ReleaseAsync();
                var winner = await resendFirst.ReplyAsync();
                Success(winner);
                await resendFirst.CloseAsync();
                var originalReply = await delayedOriginal.ReplyAsync();
                var resendReply = await secondResend.ReplyAsync();
                Success(originalReply);
                Success(resendReply);
                await delayedOriginal.CloseAsync();
                await secondResend.CloseAsync();
                Assert.True(JToken.DeepEquals(winner.Json, originalReply.Json));
                Assert.True(JToken.DeepEquals(winner.Json, resendReply.Json));

                await AwaitCrashQuiescence(scope, scope.Data.Http.Admin.ServerProcessId);
                using var oracle = await CrashOracle(scope.ConnectionString);
                await VerifyCrashCommitted(oracle, scope.Data, arranged);
                await RecoverAndReplayCrash(scope, arranged);
                _crashOutput.WriteLine("WEB6 resend-first: {0}; three independent workers converged; one committed effect; children drained", kind);
            }, web5: true);
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Operator_first_cancelled_blocks_delayed_original_and_resend_for_all_six_commands() =>
        OperatorFirstMatrix();

    private async Task OperatorFirstMatrix()
    {
        foreach (var kind in CrashKinds)
            await OwnedCrash(async scope =>
            {
                var arranged = await ArrangeCrash(scope.Data, kind);
                scope.Operation = arranged.Intent;
                var businessBefore = await Web6BusinessSnapshot(scope.ConnectionString);
                var operation = await Web6Operation(scope, arranged);
                using (var context = new WebOperationJournalTests.Context(new SqlConnection(scope.ConnectionString)))
                {
                    await context.Sql.OpenAsync();
                    context.Tx = context.Sql.BeginTransaction();
                    await new SqlWebOperationResolutionMaintenance(context).RecordNotExecutedAsync(operation,
                        scope.Data.Http.Alice, Guid.NewGuid(), "WEB6-OPERATOR-FIRST", "OPERATOR_VERIFIED",
                        "owned-web6-sql-proof", default);
                    context.Tx.Commit();
                }
                var terminalBefore = await Web6TerminalSnapshot(scope.ConnectionString, scope.Data.Http.Alice, arranged.Intent.Key);

                var delayedOriginal = await scope.Start(CrashConfiguration(scope, arranged, "none"));
                var delayedResend = await scope.Start(CrashConfiguration(scope, arranged, "none"));
                await delayedOriginal.DispatchAsync();
                await delayedResend.DispatchAsync();
                Error(await delayedOriginal.ReplyAsync(), 503, "Operation.ResultUnavailable");
                Error(await delayedResend.ReplyAsync(), 503, "Operation.ResultUnavailable");
                await delayedOriginal.CloseAsync();
                await delayedResend.CloseAsync();

                await AwaitCrashQuiescence(scope, scope.Data.Http.Admin.ServerProcessId);
                Assert.Equal(businessBefore, await Web6BusinessSnapshot(scope.ConnectionString));
                Assert.Equal(terminalBefore, await Web6TerminalSnapshot(scope.ConnectionString, scope.Data.Http.Alice, arranged.Intent.Key));
                _crashOutput.WriteLine("WEB6 operator-first: {0}; CANCELLED blocked delayed original and resend; zero business effect; children drained", kind);
            }, web5: true);
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Original_first_commit_remains_authoritative_over_waiting_operator_for_all_six_commands() =>
        OriginalFirstOperatorMatrix();

    private async Task OriginalFirstOperatorMatrix()
    {
        foreach (var kind in CrashKinds)
            await OwnedCrash(async scope =>
            {
                var arranged = await ArrangeCrash(scope.Data, kind);
                scope.Operation = arranged.Intent;
                var original = await scope.Start(CrashConfiguration(scope, arranged, "before-commit"));
                await original.DispatchAsync();
                var barrier = await original.EventAsync("before-commit");
                var holderSession = (int)barrier["sqlSessionId"]!;
                Assert.True(holderSession > 0);

                var operation = await Web6Operation(scope, arranged);
                var operatorConnection = new SqlConnectionStringBuilder(scope.ConnectionString)
                {
                    MultipleActiveResultSets = true
                };
                using (var context = new WebOperationJournalTests.Context(new SqlConnection(operatorConnection.ConnectionString)))
                {
                    await context.Sql.OpenAsync();
                    context.Tx = context.Sql.BeginTransaction();
                    var terminal = new SqlWebOperationResolutionMaintenance(context).RecordNotExecutedAsync(operation,
                        scope.Data.Http.Alice, Guid.NewGuid(), "WEB6-ORIGINAL-FIRST", "OPERATOR_VERIFIED",
                        "owned-web6-sql-proof", default);
                    using (var observer = await CrashOracle(scope.ConnectionString))
                        await WaitForCrashFenceSession(observer, holderSession, context.Sql.ServerProcessId);

                    await original.ReleaseAsync();
                    Success(await original.ReplyAsync());
                    await original.CloseAsync();
                    var conflict = await Assert.ThrowsAsync<WebOperationException>(() => terminal);
                    Assert.Equal("Operation.ResolutionConflict", conflict.Code);
                    if (context.Tx.Connection != null) context.Tx.Rollback();
                }

                await AwaitCrashQuiescence(scope, scope.Data.Http.Admin.ServerProcessId);
                using var oracle = await CrashOracle(scope.ConnectionString);
                await VerifyCrashCommitted(oracle, scope.Data, arranged);
                Assert.Equal(0, await oracle.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit WHERE ActorUserId=@actor AND OperationId=@operation;",
                    new { actor = scope.Data.Http.Alice, operation = arranged.Intent.Key }));
                await RecoverAndReplayCrash(scope, arranged);
                _crashOutput.WriteLine("WEB6 original-first: {0}; operator waited then conflict; COMMITTED remained authoritative; children drained", kind);
            }, web5: true);
    }

    private static async Task WaitForCrashFenceSession(SqlConnection observer, int holder, int waiter)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(7))
        {
            var waiting = await observer.QuerySingleAsync<int>(@"SELECT COUNT(*) FROM sys.dm_tran_locks waiter
JOIN sys.dm_tran_locks holder ON holder.resource_database_id=waiter.resource_database_id
 AND holder.resource_type=waiter.resource_type AND holder.resource_description=waiter.resource_description
WHERE waiter.resource_database_id=DB_ID() AND waiter.resource_type='APPLICATION'
 AND waiter.request_status='WAIT' AND holder.request_status='GRANT'
 AND holder.request_session_id=@holder AND waiter.request_session_id=@waiter;", new { holder, waiter });
            if (waiting > 0) return;
            await Task.Delay(25);
        }
        Assert.Fail("The operator SQL session was not observed waiting on the original transaction application lock within 7 seconds.");
    }

    private static async Task<WebOperationContext> Web6Operation(CrashScope scope, CrashArrangement arranged)
    {
        var session = await scope.Data.Http.Admin.QuerySingleAsync<Guid>(
            "SELECT TOP(1) SessionId FROM dbo.WebAuthSessions WHERE UserId=@actor AND RevokedAtUtc IS NULL ORDER BY CreatedAtUtc DESC;",
            new { actor = scope.Data.Http.Alice });
        var kind = (WebOperationKind)Enum.Parse(typeof(WebOperationKind), arranged.Intent.Kind.Replace(".", ""));
        var target = arranged.Intent.Target == null ? (long?)null : long.Parse(arranged.Intent.Target, CultureInfo.InvariantCulture);
        return new WebOperationContext(arranged.Intent.Key, Guid.Parse(scope.Data.Epoch), session, kind, target,
            HashBytes(arranged.Intent.Fingerprint), new WebOperationResultCodec());
    }

    private static async Task<string> Web6BusinessSnapshot(string connectionString)
    {
        using var oracle = await CrashOracle(connectionString);
        var values = new System.Collections.Generic.List<string>();
        foreach (var source in new[]
        {
            "SalesReceipts ORDER BY SaleId", "SalesReceiptLines ORDER BY SaleLineId",
            "GoodsReceipts ORDER BY ReceiptId", "GoodsReceiptLines ORDER BY ReceiptLineId",
            "StockBalances ORDER BY WarehouseId,ProductId", "StockLedger ORDER BY LedgerId",
            "StockValuationEntries ORDER BY LedgerId"
        }) values.Add((await CrashRows(oracle, source)).ToString(Newtonsoft.Json.Formatting.None));
        return string.Join("|", values);
    }

    private static async Task<string> Web6TerminalSnapshot(string connectionString, int actor, Guid operation)
    {
        using var oracle = await CrashOracle(connectionString);
        var journal = await oracle.QuerySingleAsync<string>(
            "SELECT COALESCE((SELECT * FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]');",
            new { actor, operation });
        var audit = await oracle.QuerySingleAsync<string>(
            "SELECT COALESCE((SELECT * FROM dbo.WebOperationTerminalAudit WHERE ActorUserId=@actor AND OperationId=@operation FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]');",
            new { actor, operation });
        return journal + "|" + audit;
    }
}
