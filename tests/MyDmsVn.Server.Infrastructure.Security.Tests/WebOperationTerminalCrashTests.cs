using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using static MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationResolutionScenario;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationTerminalCrashTests
{
    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData("before-commit")]
    [InlineData("after-commit")]
    public Task Operator_OS_process_death_has_no_partial_audit_and_requires_independent_read(string phase) => Owned(async s =>
    {
        await s.Observer.ExecuteAsync("CREATE USER WebOperationOperatorProbe WITHOUT LOGIN; ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER WebOperationOperatorProbe;");
        var operation = Operation(s); var decision = Guid.NewGuid(); WebOperationCrashProcess? child = null; Exception? failure = null;
        try
        {
            child = await WebOperationCrashProcess.StartAsync(Configuration(s.Database.ConnectionString, operation, s.Actor, decision, phase));
            await child.DispatchAsync(); var barrier = await child.EventAsync(phase);
            Assert.True((int)barrier["sqlSessionId"]! > 0);
            await child.KillAsync();
            await Quiescent(s.Database.ConnectionString, child.ProcessId, s.Observer.ServerProcessId);
            // This observer is opened only after verified owned OS-process exit and SQL drain.
            using var oracle = new SqlConnection(s.Database.ConnectionString); await oracle.OpenAsync();
            var expected = phase == "after-commit" ? 1 : 0;
            Assert.Equal(expected, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE OperationId=@id;", new { id = operation.OperationId }));
            Assert.Equal(expected, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit WHERE OperationId=@id;", new { id = operation.OperationId }));
            Assert.Equal(0, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Products;"));
            var resolution = await Read(s, operation);
            if (phase == "before-commit") Assert.Null(resolution);
            else
            {
                Assert.Equal("NOT_EXECUTED", resolution!.Status.State); Assert.Equal(decision, resolution.Terminal!.DecisionId);
                Assert.Equal("WebOperationOperatorProbe", await oracle.QuerySingleAsync<string>("SELECT OperatorDatabaseUser FROM dbo.WebOperationTerminalAudit;"));
                var retry = await s.Transaction();
                Assert.Equal("Operation.ResolutionConflict", (await Assert.ThrowsAsync<WebOperationException>(() => Record(s, retry, operation))).Code);
                if (retry.Tx.Connection != null) retry.Tx.Rollback();
            }
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try
            {
                if (child != null) { await child.CloseAsync(); await Quiescent(s.Database.ConnectionString, child.ProcessId, s.Observer.ServerProcessId); }
            }
            catch (Exception cleanup)
            {
                GuiTestSafety.Poison(new InvalidOperationException("Terminal child or SQL drain uncertain; retain the owned database."));
                if (failure != null) throw new AggregateException(failure, cleanup);
                throw;
            }
        }
    });

    internal static JObject Configuration(string connection, WebOperationContext operation, int actor, Guid decision, string phase) => new JObject
    {
        ["connectionString"] = connection, ["phase"] = phase, ["method"] = "OPERATOR", ["path"] = "operations/terminal", ["access"] = JValue.CreateNull(), ["headers"] = new JObject(),
        ["body"] = new JObject { ["operationId"] = operation.OperationId.ToString("D"), ["epoch"] = operation.DeploymentEpoch.ToString("D"), ["session"] = operation.SessionId.ToString("D"),
            ["kind"] = (int)operation.Kind, ["target"] = operation.RequestTargetId.HasValue ? new JValue(operation.RequestTargetId.Value) : JValue.CreateNull(),
            ["fingerprint"] = Convert.ToBase64String(operation.Fingerprint), ["actor"] = actor, ["decisionId"] = decision.ToString("D"),
            ["caseId"] = "case-crash", ["reason"] = "ORIGINAL_NOT_EXECUTED", ["evidence"] = "evidence-crash" }.ToString(Formatting.None)
    };

    private static async Task Quiescent(string connection, int process, int retained)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var observer = new SqlConnection(connection); await observer.OpenAsync(stop.Token);
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(8))
        {
            var owners = await observer.QuerySingleAsync<int>(new SqlTestCommand(@"SELECT
 (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process=1 AND host_process_id=@process)
 +(SELECT COUNT(*) FROM sys.dm_tran_database_transactions d WHERE d.database_id=DB_ID() AND d.database_transaction_type IN(1,2)
   AND NOT EXISTS(SELECT 1 FROM sys.dm_tran_session_transactions s WHERE s.transaction_id=d.transaction_id AND s.session_id IN(@@SPID,@retained)));",
                new { process, retained }, commandTimeout: 2, cancellationToken: stop.Token));
            if (owners == 0) return;
            await Task.Delay(25, stop.Token);
        }
        throw new TimeoutException("Terminal child SQL ownership did not drain.");
    }
}
