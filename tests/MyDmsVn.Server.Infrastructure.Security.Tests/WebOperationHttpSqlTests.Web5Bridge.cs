using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.HttpHost;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json.Linq;
using Xunit;
using HttpScenario = MyDmsVn.Server.Infrastructure.IntegrationTests.WebAuthHttpSqlTests.Scenario;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed partial class WebOperationHttpSqlTests
{
    // Separate opt-in wrapper: production TypeScript resolution + original ports,
    // actual OWIN/auth/restricted SQL and independent physical oracle. No browser,
    // IIS, production operator UI or user resend authority is inferred here.
    [Web5FrontendPortSqlFact]
    [Trait("Category", "SecurityProvisioning")]
    public async Task Frontend_ports_resolve_six_commands_and_terminal_decisions_on_exact13()
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? failure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().MigrateWeb5(database.ConnectionString).Successful);
            using var http = await HttpScenario.Create(database.ConnectionString, web5Enabled: true);
            var data = await Data.Seed(http);
            await data.GrantMutations(http.Alice);
            async Task<string> PhysicalState()
            {
                var rows = new List<string>();
                foreach (var source in new[] { "SalesReceipts ORDER BY SaleId", "SalesReceiptLines ORDER BY SaleLineId",
                    "GoodsReceipts ORDER BY ReceiptId", "GoodsReceiptLines ORDER BY ReceiptLineId", "StockBalances ORDER BY WarehouseId,ProductId",
                    "StockLedger ORDER BY LedgerId", "StockValuationEntries ORDER BY LedgerId" })
                    rows.Add(await http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo." + source + " FOR JSON PATH);"));
                rows.Add(await http.Admin.QuerySingleAsync<string>("SELECT (SELECT name,CONVERT(decimal(38,0),current_value) AS value FROM sys.sequences WHERE object_id IN (OBJECT_ID(N'dbo.SalesReceiptNumberSequence'),OBJECT_ID(N'dbo.GoodsReceiptNumberSequence')) ORDER BY name FOR JSON PATH);"));
                return string.Join("|", rows);
            }
            var observed = await WebFrontendPortBridge.RunAsync(http, new JObject
            {
                ["actor"] = new JObject { ["username"] = "alice", ["password"] = HttpScenario.Password, ["userId"] = http.Alice },
                ["catalog"] = new JObject { ["customerId"] = data.Customer, ["warehouseId"] = data.Warehouse,
                    ["employeeId"] = data.Employee, ["productId"] = data.Product, ["otherProductId"] = data.OtherProduct }
            }, async (frozen, token) =>
            {
                Assert.Equal(new[] { "actorKey", "deploymentEpoch", "operationId", "request", "requestFingerprint", "schemaVersion" }, Names(frozen));
                Assert.Equal(1, (int)frozen["schemaVersion"]!);
                Assert.Equal(http.Alice.ToString(CultureInfo.InvariantCulture), (string?)frozen["actorKey"]);
                var epoch = await http.Admin.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;");
                Assert.Equal(epoch.ToString("D"), (string?)frozen["deploymentEpoch"]);
                var request = Assert.IsType<JObject>(frozen["request"]);
                Assert.Equal(new[] { "body", "method", "operationKind", "requestTargetId", "routeScope" }, Names(request));
                var name = (string)request["operationKind"]!;
                Assert.Contains(name, new[] { "Sales.Create", "Sales.Update", "Sales.Post", "Receipt.Create", "Receipt.Update", "Receipt.Post" });
                var intent = new Intent(name, Assert.IsType<JObject>(request["body"]), (string?)request["requestTargetId"], Guid.ParseExact((string)frozen["operationId"]!, "D"));
                Assert.Equal(intent.Method.Method, (string?)request["method"]);
                Assert.Equal(intent.Scope, (string?)request["routeScope"]);
                Assert.Equal(intent.Fingerprint, (string?)frozen["requestFingerprint"]); // Independently rederived from the full frozen request.
                var session = await http.Admin.QuerySingleAsync<Guid>("SELECT TOP(1) SessionId FROM dbo.WebAuthSessions WHERE UserId=@actor AND RevokedAtUtc IS NULL ORDER BY CreatedAtUtc DESC;", new { actor = http.Alice });
                var kind = (WebOperationKind)Enum.Parse(typeof(WebOperationKind), name.Replace(".", ""));
                var operation = new WebOperationContext(intent.Key, epoch, session, kind,
                    intent.Target == null ? (long?)null : long.Parse(intent.Target, CultureInfo.InvariantCulture), HashBytes(intent.Fingerprint), new WebOperationResultCodec());
                var before = await PhysicalState();
                using (var context = new WebOperationJournalTests.Context(new Microsoft.Data.SqlClient.SqlConnection(database.ConnectionString)))
                {
                    await context.Sql.OpenAsync(token);
                    context.Tx = context.Sql.BeginTransaction();
                    await new SqlWebOperationResolutionMaintenance(context).RecordNotExecutedAsync(operation, http.Alice, Guid.NewGuid(),
                        "WEB5-BRIDGE-" + ((byte)kind).ToString(CultureInfo.InvariantCulture), "OPERATOR_VERIFIED", "owned-frontend-sql-fixture", token);
                    context.Tx.Commit();
                }
                // The committed decision is read on a different SQL connection, never
                // manufactured from the maintenance return value or lost acknowledgement.
                Assert.Equal(1, await http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal j JOIN dbo.WebOperationTerminalAudit a ON a.ActorUserId=j.ActorUserId AND a.OperationId=j.OperationId WHERE j.ActorUserId=@actor AND j.OperationId=@operation AND j.Disposition='CANCELLED';", new { actor = http.Alice, operation = intent.Key }));
                Assert.Equal(before, await PhysicalState());
            });
            Assert.Equal(1, observed.CompletedTests);
            Assert.Equal(12, observed.MutationDispatches.Count);
            Assert.All(observed.MutationDispatches, mutation => Assert.Equal(1, mutation.Value));
            Assert.Equal(6, observed.TerminalDecisions);
            Assert.True(observed.StatusReads >= 12);
            Assert.Equal(2, observed.DeniedDocumentReads);
            Assert.Equal(1, observed.Restarts);
            Assert.Equal(6, await http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE Disposition='COMMITTED';"));
            Assert.Equal(6, await http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE Disposition='CANCELLED';"));
            Assert.Equal(6, await http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
            await data.Oracle();
        }
        catch (Exception error) { failure = error; throw; }
        finally { try { await database.DisposeAsync(); } catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); } }
    }
}
