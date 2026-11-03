using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;
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
            frozen["actorKey"]?.Type != JTokenType.String || (string?)frozen["actorKey"] != actor.ToString(CultureInfo.InvariantCulture) ||
            frozen["deploymentEpoch"]?.Type != JTokenType.String || (string?)frozen["deploymentEpoch"] != epoch.ToString("D") || frozen["request"] is not JObject request ||
            !names(request).SequenceEqual(new[] { "body", "method", "operationKind", "requestTargetId", "routeScope" }) ||
            request["operationKind"]?.Type != JTokenType.String || request["method"]?.Type != JTokenType.String || request["routeScope"]?.Type != JTokenType.String ||
            request["body"] is not JObject body || frozen["operationId"]?.Type != JTokenType.String ||
            !Guid.TryParseExact((string)frozen["operationId"]!, "D", out var key) ||
            !Regex.IsMatch(key.ToString("D"), @"\A[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\z") ||
            key.ToString("D") != (string?)frozen["operationId"] || frozen["requestFingerprint"]?.Type != JTokenType.String)
            throw new InvalidDataException("Invalid negative fixture identity.");
        var kind = (string)request["operationKind"]!;
        if (!new[] { "Sales.Create", "Sales.Update", "Sales.Post", "Receipt.Create", "Receipt.Update", "Receipt.Post" }.Contains(kind)) throw new InvalidDataException("Invalid negative operation kind.");
        var create = kind.EndsWith(".Create", StringComparison.Ordinal);
        string? target = null;
        if (create)
        {
            if (request["requestTargetId"]?.Type != JTokenType.Null) throw new InvalidDataException("Invalid create target.");
        }
        else
        {
            if (request["requestTargetId"]?.Type != JTokenType.String) throw new InvalidDataException("Invalid original target.");
            target = (string)request["requestTargetId"]!;
            if (!long.TryParse(target, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0 || id.ToString(CultureInfo.InvariantCulture) != target)
                throw new InvalidDataException("Invalid original target.");
        }
        var intent = new WebOperationHttpSqlTests.Intent(kind, body, target, key);
        if ((string?)request["method"] != intent.Method.Method || (string?)request["routeScope"] != intent.Scope || (string?)frozen["requestFingerprint"] != intent.Fingerprint)
            throw new InvalidDataException("Negative fixture fingerprint or route mismatch.");
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

    internal sealed class Identity
    {
        internal int Actor; internal Guid Epoch, Key, Decision; internal WebOperationKind Kind; internal long? Target; internal byte[] Fingerprint = Array.Empty<byte>();
    }
    internal static async Task<Identity> Record(string connectionString, JObject frozen, CancellationToken token)
    {
        using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync(token);
        var actor = await sql.QuerySingleAsync<int>("SELECT UserId FROM dbo.Users WHERE Username=N'integratedEditor';");
        var epoch = await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;");
        var intent = Validate(frozen, actor, epoch);
        var target = intent.Target == null ? (long?)null : long.Parse(intent.Target, CultureInfo.InvariantCulture);
        if (target.HasValue)
        {
            var versionText = (string?)intent.Body["expectedVersion"];
            byte[] version;
            try { version = Convert.FromBase64String(versionText ?? ""); }
            catch (FormatException) { throw new InvalidDataException("Invalid negative baseline version."); }
            if (version.Length != 8 || Convert.ToBase64String(version) != versionText) throw new InvalidDataException("Invalid negative baseline version.");
            var sales = intent.Kind.StartsWith("Sales.", StringComparison.Ordinal);
            var baselineSql = sales
                ? "SELECT COUNT(*) FROM dbo.SalesReceipts d JOIN dbo.WebOperationJournal j ON j.ActorUserId=@actor AND j.OperationKind=1 AND j.ResultDocumentId=d.SaleId AND j.Disposition='COMMITTED' WHERE d.SaleId=@target AND d.CreatedByUserId=@actor AND d.Status='Draft' AND d.RowVersion=@version;"
                : "SELECT COUNT(*) FROM dbo.GoodsReceipts d JOIN dbo.WebOperationJournal j ON j.ActorUserId=@actor AND j.OperationKind=4 AND j.ResultDocumentId=d.ReceiptId AND j.Disposition='COMMITTED' WHERE d.ReceiptId=@target AND d.CreatedByUserId=@actor AND d.Status='Draft' AND d.RowVersion=@version;";
            if (await sql.QuerySingleAsync<int>(new SqlTestCommand(baselineSql, new { actor, target, version }, cancellationToken: token)) != 1)
                throw new InvalidDataException("Negative fixture baseline is not the actor's current committed draft.");
        }
        var kind = (WebOperationKind)Enum.Parse(typeof(WebOperationKind), intent.Kind.Replace(".", ""));
        var decision = Guid.NewGuid();
        var session = await sql.QuerySingleAsync<Guid>("SELECT TOP(1) SessionId FROM dbo.WebAuthSessions WHERE UserId=@actor AND RevokedAtUtc IS NULL ORDER BY CreatedAtUtc DESC;", new { actor });
        var hash = Enumerable.Range(0, 32).Select(i => byte.Parse(intent.Fingerprint.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
        using (var context = new WebOperationJournalTests.Context(new SqlConnection(connectionString)))
        {
            await context.Sql.OpenAsync(token);
            context.Tx = context.Sql.BeginTransaction();
            var operation = new WebOperationContext(intent.Key, epoch, session, kind, target, hash, new WebOperationResultCodec());
            await new SqlWebOperationResolutionMaintenance(context).RecordNotExecutedAsync(operation, actor, decision, "WEB5-IIS-NEGATIVE", "OPERATOR_VERIFIED", "owned-iis-fixture", token);
            context.Tx.Commit();
        }
        var identity = new Identity { Actor = actor, Epoch = epoch, Key = intent.Key, Kind = kind, Target = target, Fingerprint = hash, Decision = decision };
        await VerifyDurable(sql, identity, token);
        return identity;
    }
    internal static async Task VerifyDurable(SqlConnection sql, Identity identity, CancellationToken token)
    {
        var count = await sql.QuerySingleAsync<int>(new SqlTestCommand("SELECT COUNT(*) FROM dbo.WebOperationJournal j JOIN dbo.WebOperationTerminalAudit a ON a.ActorUserId=j.ActorUserId AND a.OperationId=j.OperationId WHERE j.ActorUserId=@actor AND j.OperationId=@key AND j.Disposition='CANCELLED' AND j.OperationKind=@kind AND j.ResultDocumentId IS NULL AND (j.RequestTargetId=@target OR (j.RequestTargetId IS NULL AND @target IS NULL)) AND j.RequestFingerprint=@hash AND j.DeploymentEpoch=@epoch AND a.OperationKind=@kind AND (a.RequestTargetId=@target OR (a.RequestTargetId IS NULL AND @target IS NULL)) AND a.RequestFingerprint=@hash AND a.DeploymentEpoch=@epoch AND a.DecisionId=@decision;", new { actor = identity.Actor, key = identity.Key, hash = identity.Fingerprint, epoch = identity.Epoch, kind = (byte)identity.Kind, target = identity.Target, decision = identity.Decision }, cancellationToken: token));
        if (count != 1) throw new InvalidDataException("Negative fixture durable oracle failed.");
    }
}
