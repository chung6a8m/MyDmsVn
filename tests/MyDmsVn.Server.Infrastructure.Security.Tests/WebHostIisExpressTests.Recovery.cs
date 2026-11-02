using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed partial class WebHostIisExpressTests
{
    private static readonly string[] RecoveryKinds = { "Sales.Create", "Sales.Update", "Sales.Post", "Receipt.Create", "Receipt.Update", "Receipt.Post" };
    private static readonly string[] RecoveryActors = { "alice", "poster", "receiptEditor", "receiptPoster", "integratedEditor" };
    internal static readonly string[] RecoveryChecks = { "six-command-recovery-sales", "six-command-recovery-receipt", "valuation-and-inventory",
        "epoch-headers", "absent-and-denied", "corrupt-and-mismatch", "storage-failure", "actor-isolation", "security", "epoch-switch", "same-key-concurrency", "live-cross-tab" };

    // A private driver result is an assertion input, not an SQL oracle. Validate every
    // identity before querying the physical source/ledger and recomputing valuation.
    internal static void VerifyRecoveryResult(JObject result, JArray journal, long[] receipts, long[] sales, Guid[] epochs)
    {
        RecoveryProperties(result, "version", "mode", "posted", "operations", "checks");
        if (result["version"]?.Type != JTokenType.Integer || (int)result["version"]! != 1 ||
            (string?)result["mode"] != "web4-recovery" || result["posted"] is not JArray posted ||
            result["operations"] is not JArray operations || operations.Count == 0 || result["checks"] is not JArray checks || checks.Count == 0)
            throw RecoveryInvalid();
        var checkIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var check in checks)
            if (check.Type != JTokenType.String || !checkIds.Add((string)check!)) throw RecoveryInvalid();
        if (!checkIds.SetEquals(RecoveryChecks)) throw new InvalidDataException("Recovery browser completion set is incomplete or unknown.");
        VerifyPilotResultIdentities(posted, receipts, sales, integrated: true);
        var actual = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (var item in operations)
        {
            if (item is not JObject row) throw RecoveryInvalid();
            RecoveryProperties(row, "actor", "operationId", "operationKind", "requestTargetId", "resultDocumentId", "requestFingerprint", "deploymentEpoch");
            var actor = RecoveryText(row, "actor");
            var kind = RecoveryText(row, "operationKind");
            var key = RecoveryGuid(row, "operationId");
            if (!Regex.IsMatch(key.ToString("D"), @"\A[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\z") ||
                !RecoveryActors.Contains(actor) || !RecoveryKinds.Contains(kind) || !epochs.Contains(RecoveryGuid(row, "deploymentEpoch")) ||
                !Regex.IsMatch(RecoveryText(row, "requestFingerprint"), @"\A[0-9a-f]{64}\z")) throw RecoveryInvalid();
            PilotResultId(row, "resultDocumentId");
            if (kind.EndsWith(".Create", StringComparison.Ordinal))
            {
                if (row["requestTargetId"]?.Type != JTokenType.Null) throw RecoveryInvalid();
            }
            else if (PilotResultId(row, "requestTargetId") != PilotResultId(row, "resultDocumentId")) throw RecoveryInvalid();
            var identity = actor + ":" + key.ToString("D");
            if (actual.ContainsKey(identity)) throw RecoveryInvalid();
            actual.Add(identity, row);
        }
        if (journal.Count != actual.Count) throw new InvalidDataException("Recovery operation union does not match physical journal.");
        foreach (JObject row in journal)
        {
            var key = RecoveryText(row, "actor") + ":" + RecoveryText(row, "operationId");
            if (!actual.TryGetValue(key, out var claimed) || !JToken.DeepEquals(row, claimed))
                throw new InvalidDataException("Recovery operation identity differs from physical journal.");
        }
    }

    private static async Task VerifyRecoveryStock(string connectionString, string runDirectory, JObject result, Guid[] epochs,
        int warehouse, int product, int receiptProduct, IntegratedCatalog integrated)
    {
        using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        var environment = await sql.QuerySingleAsync<(string Version, string Level, string Edition)>(
            "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')),CONVERT(nvarchar(128),SERVERPROPERTY('ProductLevel')),CONVERT(nvarchar(128),SERVERPROPERTY('Edition')); ");
        File.WriteAllText(Path.Combine(runDirectory, "sql-environment.json"), new JObject
        {
            ["productVersion"] = environment.Version, ["productLevel"] = environment.Level, ["edition"] = environment.Edition,
        }.ToString(), new System.Text.UTF8Encoding(false));
        var rows = (await sql.QueryAsync<RecoveryJournal>(@"
SELECT u.Username AS Actor,j.OperationId,j.OperationKind,j.RequestTargetId,j.ResultDocumentId,j.RequestFingerprint,j.DeploymentEpoch,j.Disposition
FROM dbo.WebOperationJournal j JOIN dbo.Users u ON u.UserId=j.ActorUserId;")).ToArray();
        Assert.NotEmpty(rows);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, rows.Select(row => row.OperationKind).Distinct().OrderBy(kind => kind));
        var journal = new JArray(rows.Select(RecoveryJournalJson));
        VerifyRecoveryResult(result, journal,
            (await sql.QueryAsync<long>("SELECT ReceiptId FROM dbo.GoodsReceipts WHERE Status='Posted';")).ToArray(),
            (await sql.QueryAsync<long>("SELECT SaleId FROM dbo.SalesReceipts WHERE Status='Posted';")).ToArray(), epochs);
        // Every source, including an unposted draft, must have exactly one Create.
        foreach (var receipt in new[] { false, true })
        {
            var kind = receipt ? 4 : 1;
            var created = rows.Where(row => row.OperationKind == kind).Select(row => row.ResultDocumentId).ToArray();
            Assert.Equal(created.Length, created.Distinct().Count());
            var physical = await sql.QueryAsync<long>(receipt ? "SELECT ReceiptId FROM dbo.GoodsReceipts;" : "SELECT SaleId FROM dbo.SalesReceipts;");
            Assert.Equal(created.OrderBy(id => id), physical.OrderBy(id => id));
        }
        Assert.Equal(0, await sql.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM dbo.WebOperationJournal j
LEFT JOIN dbo.SalesReceipts s ON j.OperationKind BETWEEN 1 AND 3 AND s.SaleId=j.ResultDocumentId
LEFT JOIN dbo.GoodsReceipts r ON j.OperationKind BETWEEN 4 AND 6 AND r.ReceiptId=j.ResultDocumentId
WHERE (j.OperationKind BETWEEN 1 AND 3 AND s.SaleId IS NULL) OR (j.OperationKind BETWEEN 4 AND 6 AND r.ReceiptId IS NULL)
OR (j.OperationKind=1 AND s.CreatedByUserId<>j.ActorUserId) OR (j.OperationKind=4 AND r.CreatedByUserId<>j.ActorUserId)
OR (j.OperationKind=3 AND (s.Status<>'Posted' OR s.PostedByUserId<>j.ActorUserId))
OR (j.OperationKind=6 AND (r.Status<>'Posted' OR r.PostedByUserId<>j.ActorUserId));"));
        // Each Posted document has exactly one committed Post operation.
        foreach (var kind in new[] { 3, 6 })
        {
            var ids = rows.Where(row => row.OperationKind == kind).Select(row => row.ResultDocumentId).ToArray();
            Assert.Equal(ids.Length, ids.Distinct().Count());
            var physical = await sql.QueryAsync<long>(kind == 3 ? "SELECT SaleId FROM dbo.SalesReceipts WHERE Status='Posted';" : "SELECT ReceiptId FROM dbo.GoodsReceipts WHERE Status='Posted';");
            Assert.Equal(ids.OrderBy(id => id), physical.OrderBy(id => id));
        }
        foreach (var item in (JArray)result["posted"]!)
        {
            var receipt = (string?)item["documentType"] == "GoodsReceipt";
            var id = PilotResultId(item, receipt ? "receiptId" : "saleId");
            if (item["actor"] != null)
                Assert.Equal(rows.Single(row => row.OperationKind == (receipt ? 6 : 3) && row.ResultDocumentId == id).Actor, RecoveryText(item, "actor"));
            var expectedQuantity = RecoveryDecimal(item, "quantity");
            var expectedValue = RecoveryDecimal(item, receipt ? "value" : "cost");
            var measured = await sql.QuerySingleAsync<(decimal Quantity, decimal Value)>(receipt
                ? "SELECT SUM(Quantity),SUM(ROUND(Quantity*UnitCost,4)) FROM dbo.GoodsReceiptLines WHERE ReceiptId=@id;"
                : "SELECT SUM(Qty),SUM(CostAmount) FROM dbo.SalesReceiptLines WHERE SaleId=@id;", new { id });
            Assert.Equal((expectedQuantity, expectedValue), measured);
        }
        // Reuse physical source/ledger correspondence checks, never application projections.
        // The recovery driver is allowed to leave an unused seeded stock key absent.
        await VerifyRecoveryMovements(sql, warehouse, product, receiptProduct, integrated);
        await VerifyRecoveryValuation(sql, warehouse, product);
    }

    private static async Task VerifyRecoveryMovements(SqlConnection sql, int warehouse, int product, int receiptProduct, IntegratedCatalog integrated)
    {
        var permitted = new[] { (warehouse, product), (warehouse, receiptProduct), (integrated.Warehouse, integrated.Product), (integrated.OtherWarehouse, integrated.Product) };
        var actual = (await sql.QueryAsync<(int Warehouse, int Product)>("SELECT WarehouseId,ProductId FROM dbo.StockBalances;")).ToArray();
        Assert.All(actual, key => Assert.Contains(key, permitted));
        foreach (var key in actual)
        {
            var movement = await sql.QuerySingleAsync<(decimal Q, decimal V)>("SELECT COALESCE(SUM(l.QuantityChange),0),COALESCE(SUM(v.ValueChange),0) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId WHERE l.WarehouseId=@Warehouse AND l.ProductId=@Product;", new { key.Warehouse, key.Product });
            var opening = key == (warehouse, product);
            Assert.Equal((movement.Q + (opening ? 1000m : 0m), movement.V + (opening ? 15000m : 0m)),
                await sql.QuerySingleAsync<(decimal Q, decimal V)>("SELECT Quantity,InventoryValue FROM dbo.StockBalances WHERE WarehouseId=@Warehouse AND ProductId=@Product;", new { key.Warehouse, key.Product }));
        }
        Assert.Equal(0, await sql.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM dbo.StockLedger l
LEFT JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId
LEFT JOIN dbo.StockBalances b ON b.WarehouseId=l.WarehouseId AND b.ProductId=l.ProductId
LEFT JOIN dbo.GoodsReceipts r ON l.DocumentType='GoodsReceipt' AND r.ReceiptId=l.DocumentId
LEFT JOIN dbo.GoodsReceiptLines rl ON rl.ReceiptId=r.ReceiptId AND rl.ReceiptLineId=l.DocumentLineId
LEFT JOIN dbo.SalesReceipts s ON l.DocumentType='SalesReceipt' AND s.SaleId=l.DocumentId
LEFT JOIN dbo.SalesReceiptLines sl ON sl.SaleId=s.SaleId AND sl.SaleLineId=l.DocumentLineId
WHERE b.ProductId IS NULL OR v.LedgerId IS NULL OR NOT (
 (l.DocumentType='GoodsReceipt' AND r.Status='Posted' AND rl.ReceiptLineId IS NOT NULL AND l.WarehouseId=r.WarehouseId AND l.ProductId=rl.ProductId AND l.QuantityChange=rl.Quantity AND v.ValueChange=ROUND(rl.Quantity*rl.UnitCost,4) AND l.PostedAtUtc=r.PostedAtUtc AND l.PostedByUserId=r.PostedByUserId)
 OR (l.DocumentType='SalesReceipt' AND s.Status='Posted' AND sl.SaleLineId IS NOT NULL AND l.WarehouseId=s.WarehouseId AND l.ProductId=sl.ProductId AND l.QuantityChange=-sl.Qty AND v.ValueChange=-sl.CostAmount AND l.PostedAtUtc=s.PostedAtUtc AND l.PostedByUserId=s.PostedByUserId));"));
        Assert.Equal(0, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries v LEFT JOIN dbo.StockLedger l ON l.LedgerId=v.LedgerId WHERE l.LedgerId IS NULL;"));
        Assert.Equal(0, await sql.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM (
 SELECT 'GoodsReceipt' AS DocumentType,r.ReceiptId AS DocumentId,l.ReceiptLineId AS LineId FROM dbo.GoodsReceipts r JOIN dbo.GoodsReceiptLines l ON l.ReceiptId=r.ReceiptId WHERE r.Status='Posted'
 UNION ALL SELECT 'SalesReceipt',s.SaleId,l.SaleLineId FROM dbo.SalesReceipts s JOIN dbo.SalesReceiptLines l ON l.SaleId=s.SaleId WHERE s.Status='Posted'
) source LEFT JOIN dbo.StockLedger l ON l.DocumentType=source.DocumentType AND l.DocumentId=source.DocumentId AND l.DocumentLineId=source.LineId WHERE l.LedgerId IS NULL;"));
        var sourceCount = await sql.QuerySingleAsync<int>("SELECT (SELECT COUNT(*) FROM dbo.GoodsReceiptLines l JOIN dbo.GoodsReceipts r ON r.ReceiptId=l.ReceiptId WHERE r.Status='Posted') + (SELECT COUNT(*) FROM dbo.SalesReceiptLines l JOIN dbo.SalesReceipts s ON s.SaleId=l.SaleId WHERE s.Status='Posted');");
        Assert.Equal(sourceCount, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
        Assert.Equal(sourceCount, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries;"));
    }

    private static async Task VerifyRecoveryValuation(SqlConnection sql, int openingWarehouse, int openingProduct)
    {
        var movements = (await sql.QueryAsync<RecoveryMovement>(@"
SELECT l.LedgerId,l.DocumentType,l.DocumentId,l.WarehouseId,l.ProductId,l.QuantityChange,v.ValueChange,
COALESCE(rl.LineNo,sl.LineNo) AS LineNo,rl.UnitCost AS ReceiptUnitCost,sl.PostedUnitCost,sl.CostAmount,sl.UnitPrice,sl.IsGift,sl.LineAmount
FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId
LEFT JOIN dbo.GoodsReceiptLines rl ON l.DocumentType='GoodsReceipt' AND rl.ReceiptId=l.DocumentId AND rl.ReceiptLineId=l.DocumentLineId
LEFT JOIN dbo.SalesReceiptLines sl ON l.DocumentType='SalesReceipt' AND sl.SaleId=l.DocumentId AND sl.SaleLineId=l.DocumentLineId ORDER BY l.LedgerId;")).ToArray();
        var stocks = new Dictionary<(int, int), (decimal Q, decimal V)> { [(openingWarehouse, openingProduct)] = (1000m, 15000m) };
        foreach (var posting in movements.GroupBy(row => (row.DocumentType, row.DocumentId)).OrderBy(group => group.Min(row => row.LedgerId)))
        foreach (var pool in posting.GroupBy(row => (row.WarehouseId, row.ProductId)))
        {
            stocks.TryGetValue(pool.Key, out var before);
            if (posting.Key.DocumentType == "GoodsReceipt")
            {
                decimal quantity = 0, value = 0;
                foreach (var line in pool)
                {
                    var amount = decimal.Round(line.QuantityChange * line.ReceiptUnitCost!.Value, 4, MidpointRounding.AwayFromZero);
                    Assert.Equal(amount, line.ValueChange);
                    quantity += line.QuantityChange; value += amount;
                }
                stocks[pool.Key] = (before.Q + quantity, before.V + value);
            }
            else
            {
                var quantity = -pool.Sum(row => row.QuantityChange);
                Assert.True(quantity > 0 && quantity <= before.Q);
                var cost = quantity == before.Q ? before.V : decimal.Round(before.V * quantity / before.Q, 4, MidpointRounding.AwayFromZero);
                var average = decimal.Round(before.V / before.Q, 8, MidpointRounding.AwayFromZero);
                decimal cumulative = 0, allocated = 0;
                foreach (var line in pool.OrderBy(row => row.LineNo))
                {
                    var qty = -line.QuantityChange; cumulative += qty;
                    var next = cumulative == quantity ? cost : decimal.Round(cost * cumulative / quantity, 4, MidpointRounding.AwayFromZero);
                    Assert.Equal(average, line.PostedUnitCost);
                    Assert.Equal(next - allocated, line.CostAmount);
                    Assert.Equal(-(next - allocated), line.ValueChange);
                    var revenue = line.IsGift == true ? 0 : decimal.Round(qty * line.UnitPrice!.Value, 4, MidpointRounding.AwayFromZero);
                    Assert.Equal(revenue, line.LineAmount);
                    allocated = next;
                }
                stocks[pool.Key] = (before.Q - quantity, before.V - cost);
            }
        }
        foreach (var pool in stocks)
            Assert.Equal(pool.Value, await sql.QuerySingleAsync<(decimal Q, decimal V)>("SELECT Quantity,InventoryValue FROM dbo.StockBalances WHERE WarehouseId=@warehouse AND ProductId=@product;", new { warehouse = pool.Key.Item1, product = pool.Key.Item2 }));
    }

    private static void RecoveryProperties(JObject row, params string[] keys)
    {
        if (!row.Properties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(keys.OrderBy(x => x, StringComparer.Ordinal))) throw RecoveryInvalid();
    }
    private static string RecoveryText(JToken row, string key) => row[key]?.Type == JTokenType.String ? (string)row[key]! : throw RecoveryInvalid();
    private static Guid RecoveryGuid(JToken row, string key)
    {
        var text = RecoveryText(row, key);
        if (!Guid.TryParseExact(text, "D", out var value) || value.ToString("D") != text || value == Guid.Empty) throw RecoveryInvalid();
        return value;
    }
    private static decimal RecoveryDecimal(JToken row, string key)
    {
        var text = RecoveryText(row, key);
        if (!Regex.IsMatch(text, @"\A(?:0|[1-9][0-9]*)\.[0-9]{4}\z") || !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) throw RecoveryInvalid();
        return value;
    }
    private static InvalidDataException RecoveryInvalid() => new InvalidDataException("Invalid WEB4 recovery pilot result.");
    internal static JObject RecoveryJournalFromRecord(IDataRecord record) => RecoveryJournalJson(SqlOracleExtensions.Read<RecoveryJournal>(record));
    private static JObject RecoveryJournalJson(RecoveryJournal row)
    {
        Assert.Equal("COMMITTED", row.Disposition);
        Assert.InRange(row.OperationKind, 1, 6);
        return new JObject
        {
            ["actor"] = row.Actor, ["operationId"] = row.OperationId.ToString("D"),
            ["operationKind"] = RecoveryKinds[row.OperationKind - 1],
            ["requestTargetId"] = row.RequestTargetId.HasValue ? new JValue(row.RequestTargetId.Value.ToString(CultureInfo.InvariantCulture)) : JValue.CreateNull(),
            ["resultDocumentId"] = row.ResultDocumentId.ToString(CultureInfo.InvariantCulture),
            ["requestFingerprint"] = BitConverter.ToString(row.RequestFingerprint).Replace("-", "").ToLowerInvariant(),
            ["deploymentEpoch"] = row.DeploymentEpoch.ToString("D"),
        };
    }
    private sealed class RecoveryJournal
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
    private sealed class RecoveryMovement
    {
        public long LedgerId { get; set; }
        public string DocumentType { get; set; } = "";
        public long DocumentId { get; set; }
        public int WarehouseId { get; set; }
        public int ProductId { get; set; }
        public int LineNo { get; set; }
        public decimal QuantityChange { get; set; }
        public decimal ValueChange { get; set; }
        public decimal? ReceiptUnitCost { get; set; }
        public decimal? PostedUnitCost { get; set; }
        public decimal? CostAmount { get; set; }
        public decimal? UnitPrice { get; set; }
        public bool? IsGift { get; set; }
        public decimal? LineAmount { get; set; }
    }
}
