using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.HttpHost;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json.Linq;
using Xunit;
using HttpScenario = MyDmsVn.Server.Infrastructure.IntegrationTests.WebAuthHttpSqlTests.Scenario;
using Reply = MyDmsVn.Server.Infrastructure.IntegrationTests.WebAuthHttpSqlTests.Reply;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed partial class WebOperationHttpSqlTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Web5_resolution_protocol_reconciles_six_original_results_and_negative_keys()
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? failure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().MigrateWeb5(database.ConnectionString).Successful);
            using var http = await HttpScenario.Create(database.ConnectionString, web5Enabled: true);
            var data = await Data.Seed(http);
            // Also exercises all six unchanged v1 writes/status/replays on the exact13 host
            // and their independent ledger/valuation/line/version oracle after restart.
            await Lifecycle(data, false, async (_, completed, access) =>
            {
                async Task<Reply> Resolution(Intent intent, string token, string? fingerprint = null)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, WebFrontendPortBridge.Origin + "/api/v2/operations/" + intent.Key.ToString("D") + "/resolution");
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                    request.Headers.TryAddWithoutValidation("X-MyDmsVn-Deployment-Epoch", data.Epoch);
                    request.Headers.TryAddWithoutValidation("X-MyDmsVn-Operation-Fingerprint", fingerprint ?? intent.Fingerprint);
                    return await http.Send(request);
                }
                using (var request = new HttpRequestMessage(HttpMethod.Get, WebFrontendPortBridge.Origin + "/api/v2/operations/context"))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + access);
                    var context = Success(await http.Send(request));
                    Assert.Equal(new[] { "actorUserId", "contractVersion", "deploymentEpoch", "sameKeyResendEnabled" }, Names(context));
                    Assert.Equal("web5-resolution-v1", (string?)context["contractVersion"]);
                    Assert.Equal(data.Epoch, (string?)context["deploymentEpoch"]);
                    Assert.Equal(http.Alice, (int)context["actorUserId"]!);
                    Assert.False((bool)context["sameKeyResendEnabled"]!);
                }
                var other = (await http.Login("bob")).Access;
                var session = await http.Admin.QuerySingleAsync<Guid>("SELECT SessionId FROM dbo.WebAuthSessions WHERE UserId=@actor AND RevokedAtUtc IS NULL;", new { actor = http.Alice });
                await http.Admin.ExecuteAsync("CREATE USER WebResolutionOperator WITHOUT LOGIN; ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER WebResolutionOperator;");
                foreach (var original in completed)
                {
                    var intent = original.Intent;
                    var kind = (WebOperationKind)Enum.Parse(typeof(WebOperationKind), intent.Kind.Replace(".", ""));
                    var permission = WebOperationContext.Permission(kind);
                    // Deny Read and the other mutation grant; only the immutable kind's
                    // grant may authorize original-result recovery.
                    await http.Admin.ExecuteAsync("UPDATE dbo.UserPermissions SET Granted=CASE WHEN PermissionKey=@permission THEN 1 ELSE 0 END WHERE UserId=@actor;", new { actor = http.Alice, permission });
                    var positive = Success(await Resolution(intent, access));
                    Assert.Equal("COMMITTED", (string?)positive["state"]);
                    Assert.Equal("web5-resolution-v1", (string?)positive["contractVersion"]);
                    Assert.Equal(http.Alice, (int)positive["actorUserId"]!);
                    Assert.True(JToken.DeepEquals(original.Reply.Json, positive["outcome"]));
                    Assert.Equal(JTokenType.Null, positive["terminal"]!.Type);
                    Assert.Equal(intent.Fingerprint, (string?)positive["requestFingerprint"]);
                    Assert.Equal(intent.Kind, (string?)positive["operationKind"]);
                    Error(await Resolution(intent, other), 404, "Operation.NotObserved");
                    Error(await Resolution(intent, access, new string('0', 64)), 409, "Idempotency.FingerprintMismatch");

                    var negative = new Intent(intent.Kind, intent.Body, intent.Target);
                    Error(await Resolution(negative, access), 404, "Operation.NotObserved");
                    var operation = new WebOperationContext(negative.Key, Guid.Parse(data.Epoch), session, kind,
                        negative.Target == null ? (long?)null : long.Parse(negative.Target, CultureInfo.InvariantCulture), HashBytes(negative.Fingerprint), new WebOperationResultCodec());
                    var decision = Guid.NewGuid();
                    using (var owned = new WebOperationJournalTests.Context(new SqlConnection(database.ConnectionString)))
                    {
                        await owned.Sql.OpenAsync();
                        await owned.Sql.ExecuteAsync("EXECUTE AS USER=N'WebResolutionOperator';");
                        owned.Tx = owned.Sql.BeginTransaction();
                        await new SqlWebOperationResolutionMaintenance(owned).RecordNotExecutedAsync(operation, http.Alice, decision,
                            "WEB5-HTTP-" + ((byte)kind).ToString(CultureInfo.InvariantCulture), "OPERATOR_VERIFIED", "owned-http-sql-fixture", CancellationToken.None);
                        owned.Tx.Commit();
                    }
                    var terminal = Success(await Resolution(negative, access));
                    Assert.Equal("NOT_EXECUTED", (string?)terminal["state"]);
                    Assert.Equal(negative.Key.ToString("D"), (string?)terminal["operationId"]);
                    Assert.Equal(negative.Fingerprint, (string?)terminal["requestFingerprint"]);
                    Assert.Equal(negative.Method.Method, (string?)terminal["method"]);
                    Assert.Equal(negative.Scope, (string?)terminal["routeScope"]);
                    Assert.Equal(negative.Target, (string?)terminal["requestTargetId"]);
                    Assert.Equal(decision.ToString("D"), (string?)terminal["terminal"]?["decisionId"]);
                    Assert.Equal(JTokenType.Null, terminal["outcome"]!.Type);
                    var beforeLate = await data.State();
                    Error(await data.Send(negative, access), 503, "Operation.ResultUnavailable");
                    AssertStatus(await data.Status(negative, access), negative, data.Epoch, "RESULT_UNAVAILABLE", null);
                    Assert.Equal(beforeLate, await data.State());
                    await http.Admin.ExecuteAsync("UPDATE dbo.UserPermissions SET Granted=0 WHERE UserId=@actor;", new { actor = http.Alice });
                    Error(await Resolution(intent, access), 403, "Auth.Forbidden");
                    Error(await Resolution(negative, access), 403, "Auth.Forbidden");
                }
                Assert.Equal(6, await http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit WHERE OperatorDatabaseUser=N'WebResolutionOperator';"));
                Assert.Equal(6, await http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE Disposition='CANCELLED';"));
                await data.Oracle();
            });
        }
        catch (Exception error) { failure = error; throw; }
        finally { try { await database.DisposeAsync(); } catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); } }
    }
}
