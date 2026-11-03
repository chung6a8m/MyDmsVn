using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.HttpHost;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Bounded fixture-only maintenance; never available to the IIS process.
internal static class Web5NegativePilot
{
    internal static WebOperationHttpSqlTests.Intent Validate(JObject frozen, int actor, Guid epoch)
    {
        string[] names(JObject o) => o.Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        if (!names(frozen).SequenceEqual(new[] { "actorKey", "deploymentEpoch", "operationId", "request", "requestFingerprint", "schemaVersion" }) ||
            frozen["schemaVersion"]?.Type != JTokenType.Integer || (int)frozen["schemaVersion"]! != 1 ||
            (string?)frozen["actorKey"] != actor.ToString(CultureInfo.InvariantCulture) ||
            (string?)frozen["deploymentEpoch"] != epoch.ToString("D") || frozen["request"] is not JObject request ||
            !names(request).SequenceEqual(new[] { "body", "method", "operationKind", "requestTargetId", "routeScope" }) ||
            (string?)request["operationKind"] != "Sales.Create" || (string?)request["method"] != "POST" ||
            (string?)request["routeScope"] != "/api/v1/sales-receipts" || request["requestTargetId"]?.Type != JTokenType.Null ||
            request["body"] is not JObject body || !Guid.TryParseExact((string?)frozen["operationId"], "D", out var key) ||
            key.ToString("D") != (string?)frozen["operationId"]) throw new InvalidDataException("Invalid negative fixture identity.");
        var intent = new WebOperationHttpSqlTests.Intent("Sales.Create", body, null, key);
        if ((string?)frozen["requestFingerprint"] != intent.Fingerprint) throw new InvalidDataException("Negative fixture fingerprint mismatch.");
        return intent;
    }

    internal static async Task<string> Snapshot(string connectionString)
    {
        using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        var result = new System.Collections.Generic.List<string>();
        foreach (var source in new[] { "SalesReceipts ORDER BY SaleId", "SalesReceiptLines ORDER BY SaleLineId", "GoodsReceipts ORDER BY ReceiptId", "GoodsReceiptLines ORDER BY ReceiptLineId", "StockBalances ORDER BY WarehouseId,ProductId", "StockLedger ORDER BY LedgerId", "StockValuationEntries ORDER BY LedgerId" })
            result.Add(await sql.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo." + source + " FOR JSON PATH);"));
        result.Add(await sql.QuerySingleAsync<string>("SELECT (SELECT name,CONVERT(decimal(38,0),current_value) AS value FROM sys.sequences WHERE object_id IN (OBJECT_ID(N'dbo.SalesReceiptNumberSequence'),OBJECT_ID(N'dbo.GoodsReceiptNumberSequence')) ORDER BY name FOR JSON PATH);"));
        return string.Join("|", result);
    }

    internal static async Task Record(string connectionString, JObject frozen, CancellationToken token)
    {
        using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync(token);
        var actor = await sql.QuerySingleAsync<int>("SELECT UserId FROM dbo.Users WHERE Username=N'integratedEditor';");
        var epoch = await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;");
        var intent = Validate(frozen, actor, epoch);
        var session = await sql.QuerySingleAsync<Guid>("SELECT TOP(1) SessionId FROM dbo.WebAuthSessions WHERE UserId=@actor AND RevokedAtUtc IS NULL ORDER BY CreatedAtUtc DESC;", new { actor });
        var hash = Enumerable.Range(0, 32).Select(i => byte.Parse(intent.Fingerprint.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
        using (var context = new WebOperationJournalTests.Context(new SqlConnection(connectionString)))
        {
            await context.Sql.OpenAsync(token);
            context.Tx = context.Sql.BeginTransaction();
            var operation = new WebOperationContext(intent.Key, epoch, session, WebOperationKind.SalesCreate, null, hash, new WebOperationResultCodec());
            await new SqlWebOperationResolutionMaintenance(context).RecordNotExecutedAsync(operation, actor, Guid.NewGuid(), "WEB5-IIS-NEGATIVE", "OPERATOR_VERIFIED", "owned-iis-fixture", token);
            context.Tx.Commit();
        }
        var count = await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal j JOIN dbo.WebOperationTerminalAudit a ON a.ActorUserId=j.ActorUserId AND a.OperationId=j.OperationId WHERE j.ActorUserId=@actor AND j.OperationId=@key AND j.Disposition='CANCELLED' AND j.RequestFingerprint=@hash AND j.DeploymentEpoch=@epoch;", new { actor, key = intent.Key, hash, epoch });
        if (count != 1) throw new InvalidDataException("Negative fixture durable oracle failed.");
    }
}
