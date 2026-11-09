using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class Web6CandidateExpected
{
    internal Web6CandidateExpected(string candidateId, string frontendSourceSha, string backendSourceSha,
        string frontendDistManifestSha256)
    {
        if (!Regex.IsMatch(candidateId ?? "", @"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid candidate identity.", nameof(candidateId));
        CandidateId = candidateId!;
        FrontendSourceSha = Web6CandidateInputs.RequireHash(frontendSourceSha, 40);
        BackendSourceSha = Web6CandidateInputs.RequireHash(backendSourceSha, 40);
        FrontendDistManifestSha256 = Web6CandidateInputs.RequireHash(frontendDistManifestSha256);
    }

    internal string CandidateId { get; }
    internal string FrontendSourceSha { get; }
    internal string BackendSourceSha { get; }
    internal string FrontendDistManifestSha256 { get; }
}

internal static class Web6CandidateOracle
{
    private static readonly string[] Kinds =
    {
        "Sales.Create", "Sales.Update", "Sales.Post", "Receipt.Create", "Receipt.Update", "Receipt.Post"
    };
    private static readonly string[] Checks =
    {
        "web6-six-kinds-one-consent", "web6-committed-loss-no-resend", "web6-two-tab-one-claim",
        "web6-fail-closed-boundaries", "web6-ime-double-click"
    };
    private static readonly Regex Marker = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9._:-]{0,127}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Candidate = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Hash40 = new Regex(@"\A[0-9a-f]{40}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Hash64 = new Regex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> NetworkMethods = new HashSet<string>(new[] { "GET", "POST", "PUT" }, StringComparer.Ordinal);
    private static readonly HashSet<string> NetworkRoutes = new HashSet<string>(new[] { "resend-context", "resend-receipt", "resend-eligibility", "sales-write", "receipt-write", "operation-resolution" }, StringComparer.Ordinal);

    internal static void ValidateResult(JObject result, Web6CandidateExpected expected)
    {
        if (result == null || expected == null) throw Invalid();
        Exact(result, "version", "mode", "candidateId", "frontendSourceSha", "backendSourceSha",
            "frontendDistManifestSha256", "operations", "checks");
        if (result["version"]?.Type != JTokenType.Integer || (int)result["version"]! != 2 ||
            Text(result, "mode") != "web6-candidate" || Text(result, "candidateId") != expected.CandidateId ||
            Text(result, "frontendSourceSha") != expected.FrontendSourceSha ||
            Text(result, "backendSourceSha") != expected.BackendSourceSha ||
            Text(result, "frontendDistManifestSha256") != expected.FrontendDistManifestSha256 ||
            result["operations"] is not JArray operations || result["checks"] is not JArray checks)
            throw Invalid();

        if (!Candidate.IsMatch(expected.CandidateId) || !Hash40.IsMatch(expected.FrontendSourceSha) ||
            !Hash40.IsMatch(expected.BackendSourceSha) || !Hash64.IsMatch(expected.FrontendDistManifestSha256) ||
            operations.Count != Kinds.Length)
            throw Invalid();

        var seenKinds = new HashSet<string>(StringComparer.Ordinal);
        var seenOperations = new HashSet<Guid>();
        foreach (var token in operations)
        {
            if (token is not JObject row) throw Invalid();
            Exact(row, "operationKind", "operationId", "requestFingerprint", "consentDispatches", "candidateId", "checkId");
            var kind = Text(row, "operationKind");
            var operationText = Text(row, "operationId");
            if (!Kinds.Contains(kind, StringComparer.Ordinal) || !seenKinds.Add(kind) ||
                !Guid.TryParseExact(operationText, "D", out var operation) || operation == Guid.Empty ||
                operation.ToString("D") != operationText || operationText[14] != '4' || "89ab".IndexOf(operationText[19]) < 0 ||
                !seenOperations.Add(operation) || !Hash64.IsMatch(Text(row, "requestFingerprint")) ||
                row["consentDispatches"]?.Type != JTokenType.Integer || (int)row["consentDispatches"]! != 1 ||
                Text(row, "candidateId") != expected.CandidateId || !Marker.IsMatch(Text(row, "checkId")))
                throw Invalid();
        }
        if (!seenKinds.SetEquals(Kinds)) throw Invalid();

        var markers = checks.Select(value => value.Type == JTokenType.String ? (string?)value : null).ToArray();
        if (markers.Any(value => value == null || !Marker.IsMatch(value)) ||
            markers.Distinct(StringComparer.Ordinal).Count() != markers.Length ||
            !new HashSet<string>(markers!, StringComparer.Ordinal).SetEquals(Checks))
            throw Invalid();
    }

    internal static void ValidateBrowserCount(JObject summary)
    {
        Exact(summary, "version", "discovered", "passed", "failed", "skipped", "interrupted", "retries");
        foreach (var name in new[] { "version", "discovered", "passed", "failed", "skipped", "interrupted", "retries" })
            if (summary[name]?.Type != JTokenType.Integer) throw Invalid();
        if ((int)summary["version"]! != 2 || (int)summary["discovered"]! != 3 || (int)summary["passed"]! != 3 ||
            new[] { "failed", "skipped", "interrupted", "retries" }.Any(name => (int)summary[name]! != 0))
            throw Invalid();
    }

    internal static void ValidateNetwork(JArray summary)
    {
        if (summary == null || summary.Count == 0 || summary.Count > 128) throw Invalid();
        foreach (var token in summary)
        {
            if (token is not JObject row) throw Invalid();
            Exact(row, "method", "routeClass", "status", "count");
            if (!NetworkMethods.Contains(Text(row, "method")) || !NetworkRoutes.Contains(Text(row, "routeClass")) ||
                row["status"]?.Type != JTokenType.Integer || (int)row["status"]! is < 0 or > 599 ||
                row["count"]?.Type != JTokenType.Integer || (int)row["count"]! <= 0)
                throw Invalid();
        }
    }

    internal static void ValidateDisposition(bool auditHealthy, bool browserDrained, bool hostDrained, bool cleanupVerified)
    {
        if (!auditHealthy || !browserDrained || !hostDrained || !cleanupVerified)
            throw new InvalidDataException("WEB6 candidate disposition is not clean and healthy.");
    }

    internal static async Task VerifySqlAsync(string connectionString, JObject result, Web6CandidateExpected expected,
        CancellationToken cancellationToken)
    {
        ValidateResult(result, expected);
        using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (JObject claimed in (JArray)result["operations"]!)
        {
            var operationId = Guid.ParseExact(Text(claimed, "operationId"), "D");
            var row = await sql.QuerySingleAsync<JournalRow>(new SqlTestCommand(@"
SELECT u.Username AS Actor,j.OperationId,j.OperationKind,j.RequestTargetId,j.ResultDocumentId,j.RequestFingerprint,j.DeploymentEpoch,j.Disposition
FROM dbo.WebOperationJournal j JOIN dbo.Users u ON u.UserId=j.ActorUserId
WHERE j.OperationId=@operation;", new { operation = operationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var expectedKind = Array.IndexOf(Kinds, Text(claimed, "operationKind")) + 1;
            if (row.Actor != "integratedEditor" || row.OperationId != operationId || row.OperationKind != expectedKind ||
                row.Disposition != "COMMITTED" || ToHex(row.RequestFingerprint) != Text(claimed, "requestFingerprint") ||
                row.ResultDocumentId <= 0 || (expectedKind is 1 or 4 ? row.RequestTargetId.HasValue : row.RequestTargetId != row.ResultDocumentId) ||
                row.DeploymentEpoch == Guid.Empty)
                throw Invalid();
            await VerifyPhysicalEffect(sql, row, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task VerifyOneEffectAsync(SqlConnection sql, Guid operationId, CancellationToken cancellationToken)
    {
        var count = await sql.QuerySingleAsync<int>(new SqlTestCommand(
            "SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE OperationId=@operation AND Disposition='COMMITTED';",
            new { operation = operationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (count != 1) throw Invalid();
        var row = await sql.QuerySingleAsync<JournalRow>(new SqlTestCommand(@"
SELECT u.Username AS Actor,j.OperationId,j.OperationKind,j.RequestTargetId,j.ResultDocumentId,j.RequestFingerprint,j.DeploymentEpoch,j.Disposition
FROM dbo.WebOperationJournal j JOIN dbo.Users u ON u.UserId=j.ActorUserId WHERE j.OperationId=@operation;",
            new { operation = operationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await VerifyPhysicalEffect(sql, row, cancellationToken).ConfigureAwait(false);
    }

    private static async Task VerifyPhysicalEffect(SqlConnection sql, JournalRow row, CancellationToken cancellationToken)
    {
        var sales = row.OperationKind <= 3;
        var exists = await sql.QuerySingleAsync<int>(new SqlTestCommand(sales
            ? "SELECT COUNT(*) FROM dbo.SalesReceipts WHERE SaleId=@id;"
            : "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE ReceiptId=@id;", new { id = row.ResultDocumentId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (exists != 1) throw Invalid();
        if (row.OperationKind is not (3 or 6)) return;
        var posted = await sql.QuerySingleAsync<int>(new SqlTestCommand(sales
            ? "SELECT COUNT(*) FROM dbo.SalesReceipts WHERE SaleId=@id AND Status='Posted' AND PostedAtUtc IS NOT NULL;"
            : "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE ReceiptId=@id AND Status='Posted' AND PostedAtUtc IS NOT NULL;",
            new { id = row.ResultDocumentId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var sourceLines = await sql.QuerySingleAsync<int>(new SqlTestCommand(sales
            ? "SELECT COUNT(*) FROM dbo.SalesReceiptLines WHERE SaleId=@id;"
            : "SELECT COUNT(*) FROM dbo.GoodsReceiptLines WHERE ReceiptId=@id;", new { id = row.ResultDocumentId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var ledger = await sql.QuerySingleAsync<int>(new SqlTestCommand(
            "SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId WHERE l.DocumentType=@type AND l.DocumentId=@id;",
            new { type = sales ? "SalesReceipt" : "GoodsReceipt", id = row.ResultDocumentId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var invalid = await sql.QuerySingleAsync<int>(new SqlTestCommand(sales ? @"
SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId
JOIN dbo.SalesReceiptLines s ON s.SaleId=l.DocumentId AND s.SaleLineId=l.DocumentLineId
WHERE l.DocumentType='SalesReceipt' AND l.DocumentId=@id AND
(l.QuantityChange<>-s.Qty OR v.ValueChange<>-s.CostAmount OR s.PostedUnitCost IS NULL OR s.CostAmount IS NULL);" : @"
SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId
JOIN dbo.GoodsReceiptLines r ON r.ReceiptId=l.DocumentId AND r.ReceiptLineId=l.DocumentLineId
WHERE l.DocumentType='GoodsReceipt' AND l.DocumentId=@id AND
(l.QuantityChange<>r.Quantity OR v.ValueChange<>ROUND(r.Quantity*r.UnitCost,4));",
            new { id = row.ResultDocumentId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (posted != 1 || sourceLines <= 0 || ledger != sourceLines || invalid != 0) throw Invalid();
    }

    private static void Exact(JObject value, params string[] keys)
    {
        if (value == null || !value.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal)
            .SequenceEqual(keys.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal)) throw Invalid();
    }
    private static string Text(JObject value, string key) => value[key]?.Type == JTokenType.String ? (string)value[key]! : throw Invalid();
    private static string ToHex(byte[] value) => BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
    private static InvalidDataException Invalid() => new InvalidDataException("Invalid WEB6 candidate evidence.");

    private sealed class JournalRow
    {
        public string Actor { get; set; } = "";
        public Guid OperationId { get; set; }
        public int OperationKind { get; set; }
        public long? RequestTargetId { get; set; }
        public long ResultDocumentId { get; set; }
        public byte[] RequestFingerprint { get; set; } = Array.Empty<byte>();
        public Guid DeploymentEpoch { get; set; }
        public string Disposition { get; set; } = "";
    }
}
