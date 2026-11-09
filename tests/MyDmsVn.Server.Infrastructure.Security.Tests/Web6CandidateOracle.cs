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
using Newtonsoft.Json;
using MyDmsVn.Server.HttpHost;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class Web6CandidateExpected
{
    internal Web6CandidateExpected(string candidateId, string frontendSourceSha, string backendSourceSha,
        string frontendDistManifestSha256, Web6StockExpectation? stock = null)
    {
        if (!Regex.IsMatch(candidateId ?? "", @"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid candidate identity.", nameof(candidateId));
        CandidateId = candidateId!;
        FrontendSourceSha = Web6CandidateInputs.RequireHash(frontendSourceSha, 40);
        BackendSourceSha = Web6CandidateInputs.RequireHash(backendSourceSha, 40);
        FrontendDistManifestSha256 = Web6CandidateInputs.RequireHash(frontendDistManifestSha256);
        Stock = stock;
    }

    internal string CandidateId { get; }
    internal string FrontendSourceSha { get; }
    internal string BackendSourceSha { get; }
    internal string FrontendDistManifestSha256 { get; }
    internal Web6StockExpectation? Stock { get; }
}

internal sealed class Web6StockExpectation
{
    internal Web6StockExpectation(int warehouseId, int salesProductId, int receiptProductId)
    {
        if (warehouseId <= 0 || salesProductId <= 0 || receiptProductId <= 0 || salesProductId == receiptProductId) throw new ArgumentException("WEB6 stock keys are invalid.");
        WarehouseId = warehouseId; SalesProductId = salesProductId; ReceiptProductId = receiptProductId;
    }
    internal int WarehouseId { get; }
    internal int SalesProductId { get; }
    internal int ReceiptProductId { get; }
    internal (decimal Quantity, decimal Value) Initial(int warehouse, int product)
    {
        if (warehouse != WarehouseId) throw new InvalidDataException("Invalid WEB6 candidate evidence.");
        if (product == SalesProductId) return (1000m, 15000m);
        if (product == ReceiptProductId) return (0m, 0m);
        throw new InvalidDataException("Invalid WEB6 candidate evidence.");
    }
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

    internal static JObject ValidateCanonicalResultBytes(int operationKind, byte[] bytes)
    {
        if (operationKind is < 1 or > 6 || bytes == null || bytes.Length == 0) throw Invalid();
        JObject envelope;
        try { envelope = new WebOperationResultCodec().DecodeEnvelope((MyDmsVn.Server.Application.Operations.WebOperationKind)operationKind, bytes); }
        catch { throw Invalid(); }
        var canonical = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope, Formatting.None));
        if (!canonical.SequenceEqual(bytes)) throw Invalid();
        return envelope;
    }

    internal static void ValidateDraftProjection(int operationKind, JObject envelope, JObject header, JArray lines)
    {
        if (operationKind is not (1 or 2 or 4 or 5) || envelope?["data"] is not JObject data || header == null || lines == null)
            throw Invalid();
        var sales = operationKind <= 2;
        var headerMap = sales
            ? new[] { ("saleId", "saleId"), ("saleNumber", "saleNumber"), ("saleDate", "saleDate"), ("customerId", "customerId"), ("warehouseId", "warehouseId"), ("employeeId", "employeeId"), ("status", "status"), ("note", "note"), ("version", "version") }
            : new[] { ("receiptId", "receiptId"), ("receiptNumber", "receiptNumber"), ("receiptDate", "receiptDate"), ("warehouseId", "warehouseId"), ("employeeId", "employeeId"), ("status", "status"), ("note", "note"), ("version", "version") };
        foreach (var pair in headerMap)
        {
            var expected = data[pair.Item1]; var actual = header[pair.Item2];
            if (!((expected == null || expected.Type == JTokenType.Null) && (actual == null || actual.Type == JTokenType.Null)) &&
                !JToken.DeepEquals(expected, actual)) throw Invalid();
        }
        if (header.Property("postedAtUtc", StringComparison.Ordinal) == null || header.Property("postedByUserId", StringComparison.Ordinal) == null ||
            header["postedAtUtc"]!.Type != JTokenType.Null || header["postedByUserId"]!.Type != JTokenType.Null) throw Invalid();
        if (data["lines"] is not JArray expectedLines || expectedLines.Count != lines.Count || lines.Count == 0) throw Invalid();
        var names = sales
            ? new[] { "saleLineId", "lineNo", "productId", "qty", "unitPrice", "isGift", "lineAmount" }
            : new[] { "receiptLineId", "lineNumber", "productId", "quantity", "unitCost" };
        for (var index = 0; index < lines.Count; index++)
            foreach (var name in names)
                if (!JToken.DeepEquals(expectedLines[index]![name], lines[index]![name])) throw Invalid();
    }

    internal static void ValidateStockBalance(string initialQuantity, string initialValue, string quantityChange,
        string valueChange, string actualQuantity, string actualValue)
    {
        static decimal ExactDecimal(string value)
        {
            if (!Regex.IsMatch(value ?? "", @"\A-?(?:0|[1-9][0-9]*)\.[0-9]{4}\z", RegexOptions.CultureInvariant) ||
                !decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)) throw Invalid();
            return parsed;
        }
        if (ExactDecimal(initialQuantity) + ExactDecimal(quantityChange) != ExactDecimal(actualQuantity) ||
            ExactDecimal(initialValue) + ExactDecimal(valueChange) != ExactDecimal(actualValue)) throw Invalid();
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
SELECT u.Username AS Actor,j.OperationId,j.OperationKind,j.RequestTargetId,j.ResultDocumentId,j.RequestFingerprint,j.DeploymentEpoch,j.Disposition,j.ResultBytes
FROM dbo.WebOperationJournal j JOIN dbo.Users u ON u.UserId=j.ActorUserId
WHERE j.OperationId=@operation;", new { operation = operationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var expectedKind = Array.IndexOf(Kinds, Text(claimed, "operationKind")) + 1;
            if (row.Actor != "integratedEditor" || row.OperationId != operationId || row.OperationKind != expectedKind ||
                row.Disposition != "COMMITTED" || ToHex(row.RequestFingerprint) != Text(claimed, "requestFingerprint") ||
                row.ResultDocumentId <= 0 || (expectedKind is 1 or 4 ? row.RequestTargetId.HasValue : row.RequestTargetId != row.ResultDocumentId) ||
                row.DeploymentEpoch == Guid.Empty)
                throw Invalid();
            var envelope = ValidateCanonicalResultBytes(row.OperationKind, row.ResultBytes);
            await VerifyPhysicalEffect(sql, row, expected.Stock, cancellationToken).ConfigureAwait(false);
            await VerifyResultProjection(sql, row, envelope, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task VerifyOneEffectAsync(SqlConnection sql, Guid operationId, CancellationToken cancellationToken, byte[]? retainedCanonicalResult = null)
    {
        var count = await sql.QuerySingleAsync<int>(new SqlTestCommand(
            "SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE OperationId=@operation AND Disposition='COMMITTED';",
            new { operation = operationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (count != 1) throw Invalid();
        var row = await sql.QuerySingleAsync<JournalRow>(new SqlTestCommand(@"
SELECT u.Username AS Actor,j.OperationId,j.OperationKind,j.RequestTargetId,j.ResultDocumentId,j.RequestFingerprint,j.DeploymentEpoch,j.Disposition,j.ResultBytes
FROM dbo.WebOperationJournal j JOIN dbo.Users u ON u.UserId=j.ActorUserId WHERE j.OperationId=@operation;",
            new { operation = operationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await VerifyPhysicalEffect(sql, row, null, cancellationToken).ConfigureAwait(false);
        var envelope = ValidateCanonicalResultBytes(row.OperationKind, retainedCanonicalResult ?? row.ResultBytes);
        await VerifyResultProjection(sql, row, envelope, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<long> VerifyCommittedSetupAsync(SqlConnection sql, Web6CommittedSetup proof, Guid epoch)
    {
        var rows = (await sql.QueryAsync<JournalRow>(@"
SELECT u.Username AS Actor,j.OperationId,j.OperationKind,j.RequestTargetId,j.ResultDocumentId,j.RequestFingerprint,j.DeploymentEpoch,j.Disposition,j.ResultBytes
FROM dbo.WebOperationJournal j JOIN dbo.Users u ON u.UserId=j.ActorUserId WHERE j.OperationId=@operation;",
            new { operation = proof.Operation })).ToArray();
        if (rows.Length != 1) throw Invalid();
        var row = rows[0];
        proof.VerifyJournal(row.OperationId, row.OperationKind, ToHex(row.RequestFingerprint), row.DeploymentEpoch, epoch,
            row.Disposition, row.Actor, row.RequestTargetId, row.ResultDocumentId);
        var envelope = ValidateCanonicalResultBytes(row.OperationKind, row.ResultBytes);
        await VerifyPhysicalEffect(sql, row, null, CancellationToken.None);
        await VerifyResultProjection(sql, row, envelope, CancellationToken.None);
        return row.ResultDocumentId;
    }

    private static async Task VerifyResultProjection(SqlConnection sql, JournalRow row, JObject envelope, CancellationToken cancellationToken)
    {
        if (row.OperationKind is 1 or 2)
        {
            var header = await sql.QuerySingleAsync<SalesHeader>(new SqlTestCommand(
                "SELECT SaleId,SaleNumber,SaleDate,CustomerId,WarehouseId,EmployeeId,Status,Note,RowVersion,PostedAtUtc,PostedByUserId FROM dbo.SalesReceipts WHERE SaleId=@id;",
                new { id = row.ResultDocumentId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var lines = (await sql.QueryAsync<SalesLine>(new SqlTestCommand(
                "SELECT SaleLineId,LineNo,ProductId,Qty,UnitPrice,IsGift,LineAmount FROM dbo.SalesReceiptLines WHERE SaleId=@id ORDER BY LineNo;",
                new { id = row.ResultDocumentId }, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
            ValidateDraftProjection(row.OperationKind, envelope, header.Json(), new JArray(lines.Select(line => line.Json())));
        }
        else if (row.OperationKind is 4 or 5)
        {
            var header = await sql.QuerySingleAsync<ReceiptHeader>(new SqlTestCommand(
                "SELECT ReceiptId,ReceiptNo ReceiptNumber,ReceiptDate,WarehouseId,EmployeeId,Status,Note,RowVersion,PostedAtUtc,PostedByUserId FROM dbo.GoodsReceipts WHERE ReceiptId=@id;",
                new { id = row.ResultDocumentId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var lines = (await sql.QueryAsync<ReceiptLine>(new SqlTestCommand(
                "SELECT ReceiptLineId,LineNo LineNumber,ProductId,Quantity,UnitCost FROM dbo.GoodsReceiptLines WHERE ReceiptId=@id ORDER BY LineNo;",
                new { id = row.ResultDocumentId }, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
            ValidateDraftProjection(row.OperationKind, envelope, header.Json(), new JArray(lines.Select(line => line.Json())));
        }
        else
        {
            var data = (JObject)envelope["data"]!;
            var idName = row.OperationKind == 3 ? "saleId" : "receiptId";
            var posted = await sql.QuerySingleAsync<PostHeader>(new SqlTestCommand(row.OperationKind == 3
                ? "SELECT Status,PostedAtUtc,RowVersion FROM dbo.SalesReceipts WHERE SaleId=@id;"
                : "SELECT Status,PostedAtUtc,CAST(NULL AS binary(8)) RowVersion FROM dbo.GoodsReceipts WHERE ReceiptId=@id;",
                new { id = row.ResultDocumentId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if ((string?)data[idName] != row.ResultDocumentId.ToString(CultureInfo.InvariantCulture) || (string?)data["status"] != posted.Status ||
                !DateTime.TryParse((string?)data["postedAtUtc"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expectedPosted) ||
                expectedPosted != DateTime.SpecifyKind(posted.PostedAtUtc, DateTimeKind.Utc) ||
                (row.OperationKind == 3 && (string?)data["version"] != Convert.ToBase64String(posted.RowVersion!))) throw Invalid();
        }
    }

    private static async Task VerifyPhysicalEffect(SqlConnection sql, JournalRow row, Web6StockExpectation? stock, CancellationToken cancellationToken)
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
        if (stock != null)
        {
            var keys = (await sql.QueryAsync<StockKey>(new SqlTestCommand(
                "SELECT DISTINCT WarehouseId,ProductId FROM dbo.StockLedger WHERE DocumentType=@type AND DocumentId=@id;",
                new { type = sales ? "SalesReceipt" : "GoodsReceipt", id = row.ResultDocumentId }, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
            if (keys.Length == 0) throw Invalid();
            foreach (var key in keys)
            {
                var initial = stock.Initial(key.WarehouseId, key.ProductId);
                var movement = await sql.QuerySingleAsync<StockMovement>(new SqlTestCommand(@"
SELECT COALESCE(SUM(l.QuantityChange),0) QuantityChange,COALESCE(SUM(v.ValueChange),0) ValueChange
FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId
WHERE l.WarehouseId=@warehouse AND l.ProductId=@product;", new { warehouse = key.WarehouseId, product = key.ProductId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
                var balance = await sql.QuerySingleAsync<StockBalance>(new SqlTestCommand(
                    "SELECT Quantity,InventoryValue FROM dbo.StockBalances WHERE WarehouseId=@warehouse AND ProductId=@product;",
                    new { warehouse = key.WarehouseId, product = key.ProductId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
                ValidateStockBalance(Decimal(initial.Quantity), Decimal(initial.Value), Decimal(movement.QuantityChange), Decimal(movement.ValueChange), Decimal(balance.Quantity), Decimal(balance.InventoryValue));
            }
        }
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
        public byte[] ResultBytes { get; set; } = Array.Empty<byte>();
    }

    private static string Decimal(decimal value) => value.ToString("F4", CultureInfo.InvariantCulture);
    private sealed class SalesHeader { public long SaleId { get; set; } public string SaleNumber { get; set; } = ""; public DateTime SaleDate { get; set; } public int CustomerId { get; set; } public int WarehouseId { get; set; } public int EmployeeId { get; set; } public string Status { get; set; } = ""; public string? Note { get; set; } public byte[] RowVersion { get; set; } = Array.Empty<byte>(); public DateTime? PostedAtUtc { get; set; } public int? PostedByUserId { get; set; } internal JObject Json() => new JObject { ["saleId"] = SaleId.ToString(CultureInfo.InvariantCulture), ["saleNumber"] = SaleNumber, ["saleDate"] = SaleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ["customerId"] = CustomerId, ["warehouseId"] = WarehouseId, ["employeeId"] = EmployeeId, ["status"] = Status, ["note"] = Note == null ? JValue.CreateNull() : Note, ["version"] = Convert.ToBase64String(RowVersion), ["postedAtUtc"] = PostedAtUtc.HasValue ? new JValue(PostedAtUtc.Value) : JValue.CreateNull(), ["postedByUserId"] = PostedByUserId.HasValue ? new JValue(PostedByUserId.Value) : JValue.CreateNull() }; }
    private sealed class SalesLine { public long SaleLineId { get; set; } public int LineNo { get; set; } public int ProductId { get; set; } public decimal Qty { get; set; } public decimal UnitPrice { get; set; } public bool IsGift { get; set; } public decimal LineAmount { get; set; } internal JObject Json() => new JObject { ["saleLineId"] = SaleLineId.ToString(CultureInfo.InvariantCulture), ["lineNo"] = LineNo, ["productId"] = ProductId, ["qty"] = Qty.ToString("F4", CultureInfo.InvariantCulture), ["unitPrice"] = UnitPrice.ToString("F4", CultureInfo.InvariantCulture), ["isGift"] = IsGift, ["lineAmount"] = LineAmount.ToString("F4", CultureInfo.InvariantCulture) }; }
    private sealed class ReceiptHeader { public long ReceiptId { get; set; } public string ReceiptNumber { get; set; } = ""; public DateTime ReceiptDate { get; set; } public int WarehouseId { get; set; } public int EmployeeId { get; set; } public string Status { get; set; } = ""; public string? Note { get; set; } public byte[] RowVersion { get; set; } = Array.Empty<byte>(); public DateTime? PostedAtUtc { get; set; } public int? PostedByUserId { get; set; } internal JObject Json() => new JObject { ["receiptId"] = ReceiptId.ToString(CultureInfo.InvariantCulture), ["receiptNumber"] = ReceiptNumber, ["receiptDate"] = ReceiptDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ["warehouseId"] = WarehouseId, ["employeeId"] = EmployeeId, ["status"] = Status, ["note"] = Note == null ? JValue.CreateNull() : Note, ["version"] = Convert.ToBase64String(RowVersion), ["postedAtUtc"] = PostedAtUtc.HasValue ? new JValue(PostedAtUtc.Value) : JValue.CreateNull(), ["postedByUserId"] = PostedByUserId.HasValue ? new JValue(PostedByUserId.Value) : JValue.CreateNull() }; }
    private sealed class ReceiptLine { public long ReceiptLineId { get; set; } public int LineNumber { get; set; } public int ProductId { get; set; } public decimal Quantity { get; set; } public decimal UnitCost { get; set; } internal JObject Json() => new JObject { ["receiptLineId"] = ReceiptLineId.ToString(CultureInfo.InvariantCulture), ["lineNumber"] = LineNumber, ["productId"] = ProductId, ["quantity"] = Quantity.ToString("F4", CultureInfo.InvariantCulture), ["unitCost"] = UnitCost.ToString("F4", CultureInfo.InvariantCulture) }; }
    private sealed class PostHeader { public string Status { get; set; } = ""; public DateTime PostedAtUtc { get; set; } public byte[]? RowVersion { get; set; } }
    private sealed class StockKey { public int WarehouseId { get; set; } public int ProductId { get; set; } }
    private sealed class StockMovement { public decimal QuantityChange { get; set; } public decimal ValueChange { get; set; } }
    private sealed class StockBalance { public decimal Quantity { get; set; } public decimal InventoryValue { get; set; } }
}

// The cutover pack deliberately emits lifecycle checks and no successful mutation rows.
// Keep this contract separate from Web6CandidateOracle, whose six-kind SQL proof is owned by
// the ordinary resend candidate pack.
internal static class Web6CutoverOracle
{
    private static readonly string[] Checks =
    {
        "web6-cutover-old-client-new-host", "web6-cutover-new-client-old-host",
        "web6-cutover-revoked-grant", "web6-cutover-committed-unavailable-protected",
        "web6-cutover-same-epoch-restart", "web6-cutover-restored-epoch-rejected",
    };

    internal static void Validate(JObject count, JObject result, Web6CandidateCutoverPlan plan,
        Web6CandidateExpected current, JObject protectedHandles)
    {
        if (count == null || result == null || plan == null || current == null ||
            current.CandidateId != plan.CurrentCandidateId || !JToken.DeepEquals(protectedHandles, plan.FrontendHandles())) throw Invalid();
        Exact(count, "version", "discovered", "passed", "failed", "skipped", "interrupted", "retries");
        foreach (var name in new[] { "version", "discovered", "passed", "failed", "skipped", "interrupted", "retries" })
            if (count[name]?.Type != JTokenType.Integer) throw Invalid();
        if ((int)count["version"]! != 2 || (int)count["discovered"]! != 3 || (int)count["passed"]! != 3 ||
            new[] { "failed", "skipped", "interrupted", "retries" }.Any(name => (int)count[name]! != 0))
            throw Invalid();

        Exact(result, "version", "mode", "candidateId", "frontendSourceSha", "backendSourceSha",
            "frontendDistManifestSha256", "operations", "checks");
        if (result["version"]?.Type != JTokenType.Integer || (int)result["version"]! != 2 ||
            (string?)result["mode"] != "web6-candidate" ||
            (string?)result["candidateId"] != plan.CurrentCandidateId ||
            (string?)result["frontendSourceSha"] != current.FrontendSourceSha ||
            (string?)result["backendSourceSha"] != current.BackendSourceSha ||
            (string?)result["frontendDistManifestSha256"] != current.FrontendDistManifestSha256 ||
            result["operations"] is not JArray operations || operations.Count != 0 ||
            result["checks"] is not JArray checks)
            throw Invalid();
        var markers = checks.Select(token => token.Type == JTokenType.String ? (string?)token : null).ToArray();
        if (markers.Length != Checks.Length || markers.Any(marker => marker == null) || markers.Distinct(StringComparer.Ordinal).Count() != Checks.Length ||
            !new HashSet<string>(markers!, StringComparer.Ordinal).SetEquals(Checks))
            throw Invalid();
    }

    internal static void ValidateSetupEvidence(Web6CommittedSetup? browserProof, Web6CommittedSetup? sqlProof, bool restoredAdmitted)
    {
        if (browserProof == null || sqlProof == null || browserProof.Operation != sqlProof.Operation ||
            browserProof.Fingerprint != sqlProof.Fingerprint || !restoredAdmitted) throw Invalid();
    }

    internal static void ValidateAllocationDisposition(Web6CandidateCutoverPlan plan,
        IReadOnlyCollection<Web6CommittedSetup> verifiedSetups, int restoredAdmissions, int currentReadmissions)
    {
        if (verifiedSetups.Count != 2 || verifiedSetups.Select(p => p.Operation).Distinct().Count() != 2 ||
            restoredAdmissions != 2 || currentReadmissions != 1 ||
            new[] { plan.CurrentDatabaseReceipt, plan.OldDatabaseReceipt, plan.RestoredDatabaseReceipt }.Distinct(StringComparer.Ordinal).Count() != 3)
            throw Invalid();
    }

    internal static void ValidateDisposition(bool lifecycleMutationAbsent, bool operationIdentityPreserved,
        bool receiptIdentityPreserved, bool browserDrained, bool hostDrained, bool cleanupVerified)
    {
        if (!lifecycleMutationAbsent || !operationIdentityPreserved || !receiptIdentityPreserved ||
            !browserDrained || !hostDrained || !cleanupVerified)
            throw Invalid();
    }

    private static void Exact(JObject value, params string[] names)
    {
        if (!value.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal)
            .SequenceEqual(names.OrderBy(name => name, StringComparer.Ordinal))) throw Invalid();
    }

    private static InvalidDataException Invalid() => new InvalidDataException("Invalid WEB6 cutover evidence.");
}

internal static class Web6CandidateAcceptanceGate
{
    private static readonly string[] Required = { "MYDMSVN_WEB6_CANDIDATE_ROOT", "MYDMSVN_WEB6_CANDIDATE_ID", "MYDMSVN_WEB_TEST_BROWSER_DIRECTORY", "MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY", "MYDMSVN_WEB_TEST_TLS_THUMBPRINT", "MYDMSVN_WEB_PILOT_PROJECT", "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING", "MYDMSVN_SQL_TEST_APPROVAL_FILE" };
    internal static string? OrdinarySkipReason(IReadOnlyDictionary<string, string?> values) =>
        Value(values, "MYDMSVN_WEB6_CANDIDATE_ENABLE") == "1" ? null : "WEB6 paired candidate requires exact explicit nonproduction opt-in.";
    internal static void RequireLive(IReadOnlyDictionary<string, string?> values)
    {
        if (Value(values, "MYDMSVN_WEB6_CANDIDATE_ENABLE") != "1") throw new InvalidOperationException("MYDMSVN_WEB6_CANDIDATE_ENABLE must equal 1.");
        var missing = Required.Where(name => string.IsNullOrWhiteSpace(Value(values, name))).ToArray();
        if (missing.Length != 0) throw new InvalidOperationException("WEB6 paired candidate BLOCKED: missing " + string.Join(",", missing));
    }
    internal static IReadOnlyDictionary<string, string?> EnvironmentValues() => Environment.GetEnvironmentVariables().Keys.Cast<object>()
        .Select(key => key.ToString()!).Where(key => key.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(key => key, Environment.GetEnvironmentVariable, StringComparer.OrdinalIgnoreCase);
    private static string? Value(IReadOnlyDictionary<string, string?> values, string name) => values.TryGetValue(name, out var value) ? value : null;
}
