using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed partial class WebHostIisExpressTests
{
    private sealed class IntegratedCatalog
    {
        public int Warehouse { get; set; }
        public int OtherWarehouse { get; set; }
        public int Product { get; set; }
    }

    private static async Task<IntegratedCatalog> SeedIntegratedActorsAndCatalog(SqlConnection connection)
    {
        var catalog = new IntegratedCatalog
        {
            Warehouse = await connection.QuerySingleAsync<int>("INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'WEB3-IW',N'Web3 Integrated Warehouse');"),
            OtherWarehouse = await connection.QuerySingleAsync<int>("INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'WEB3-IW2',N'Web3 Independent Warehouse');"),
            Product = await connection.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'WEB3-IP',N'Web3 Integrated Product',N'EA');"),
        };
        // These pools start empty; their complete history is created through real HTTP.
        var hash = new BcryptPasswordHasher().Hash(BrowserPassword);
        foreach (var name in new[] { "integratedEditor", "balanceReader", "cardReader" })
        {
            var id = await connection.QuerySingleAsync<int>("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(@name,@normalized,@name,N'Local',@hash,N'',N'BCrypt');", new { name, normalized = name.ToUpperInvariant(), hash });
            var permissions = new List<string> { "Catalog.Products.Read", "Catalog.Customers.Read", "Catalog.Warehouses.Read", "Catalog.Employees.Read" };
            if (name == "integratedEditor") permissions.AddRange(new[] { "Sales.Receipts.Read", "Sales.Receipts.Write", "Sales.Receipts.Post", "Inventory.GoodsReceipts.Read", "Inventory.GoodsReceipts.Write", "Inventory.GoodsReceipts.Post", "Inventory.Balances.Read", "Inventory.StockCard.Read" });
            else permissions.Add(name == "balanceReader" ? "Inventory.Balances.Read" : "Inventory.StockCard.Read");
            foreach (var key in permissions) await connection.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@id,@key,1);", new { id, key });
        }
        return catalog;
    }

    // Validate the whole union before partitioning. A duplicate or omitted record must
    // not disappear into a different scenario, document type, or noncanonical alias.
    internal static void VerifyPilotResultIdentities(JArray rows, long[] postedReceipts, long[] postedSales, bool integrated)
    {
        var receipts = new HashSet<long>(); var sales = new HashSet<long>();
        foreach (var row in rows)
        {
            if (row is not JObject) throw new InvalidDataException("Pilot result must be an object.");
            var scenario = OptionalResultString(row, "scenario");
            var type = OptionalResultString(row, "documentType");
            if (scenario == "integrated")
            {
                if (!integrated || (type != "GoodsReceipt" && type != "SalesReceipt") || OptionalResultString(row, "actor") != "integratedEditor")
                    throw new InvalidDataException("Invalid integrated pilot discriminator.");
            }
            else if (scenario != null || (type != null && type != "GoodsReceipt"))
                throw new InvalidDataException("Unknown pilot scenario or document type.");
            var receipt = type == "GoodsReceipt";
            var id = PilotResultId(row, receipt ? "receiptId" : "saleId");
            if (!(receipt ? receipts : sales).Add(id)) throw new InvalidDataException("Duplicate pilot document identity.");
        }
        if (!receipts.SetEquals(postedReceipts) || !sales.SetEquals(postedSales))
            throw new InvalidDataException("Pilot result union must exactly match physical Posted documents.");
    }

    private static string? OptionalResultString(JToken row, string key)
    {
        var token = row[key];
        if (token == null || token.Type == JTokenType.Null) return null;
        if (token.Type != JTokenType.String) throw new InvalidDataException("Pilot discriminator must be a string.");
        return token.Value<string>();
    }

    private static long PilotResultId(JToken row, string key)
    {
        var text = OptionalResultString(row, key);
        if (text == null || !Regex.IsMatch(text, "\\A[1-9][0-9]{0,18}\\z", RegexOptions.CultureInvariant) ||
            !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            throw new InvalidDataException("Pilot ID must be a canonical positive int64 string.");
        return id;
    }

    private static async Task VerifyIntegratedStock(SqlConnection sql, JToken[] rows, IntegratedCatalog catalog)
    {
        Assert.Equal(5, rows.Length);
        var receiptRows = rows.Where(row => (string?)row["documentType"] == "GoodsReceipt").ToArray();
        var saleRow = Assert.Single(rows, row => (string?)row["documentType"] == "SalesReceipt");
        Assert.Equal(4, receiptRows.Length);
        var receipts = receiptRows.Select(row => PilotResultId(row, "receiptId")).ToArray();
        var sale = PilotResultId(saleRow, "saleId");
        var actor = await sql.QuerySingleAsync<int>("SELECT UserId FROM dbo.Users WHERE Username=N'integratedEditor';");
        Assert.Equal(receipts.OrderBy(id => id), (await sql.QueryAsync<long>("SELECT r.ReceiptId FROM dbo.GoodsReceipts r WHERE r.Status='Posted' AND EXISTS(SELECT 1 FROM dbo.GoodsReceiptLines l WHERE l.ReceiptId=r.ReceiptId AND l.ProductId=@Product);", catalog)).OrderBy(id => id));
        Assert.Equal(new[] { sale }, (await sql.QueryAsync<long>("SELECT s.SaleId FROM dbo.SalesReceipts s WHERE s.Status='Posted' AND EXISTS(SELECT 1 FROM dbo.SalesReceiptLines l WHERE l.SaleId=s.SaleId AND l.ProductId=@Product);", catalog)).ToArray());
        var expected = new[]
        {
            (Warehouse: catalog.Warehouse, Quantity: 10m, Cost: 10m, Value: 100m, Date: new DateTime(2024,3,1)),
            (Warehouse: catalog.Warehouse, Quantity: 10m, Cost: 20m, Value: 200m, Date: new DateTime(2024,3,1)),
            (Warehouse: catalog.Warehouse, Quantity: 2m, Cost: 50m, Value: 100m, Date: new DateTime(2024,2,29)),
            (Warehouse: catalog.OtherWarehouse, Quantity: 3m, Cost: 7m, Value: 21m, Date: new DateTime(2024,3,1)),
        };
        var stamps = new List<DateTime>();
        for (var index = 0; index < receipts.Length; index++)
        {
            var id = receipts[index]; var target = expected[index];
            var header = await sql.QuerySingleAsync<(string Status, int Warehouse, DateTime Date, int CreatedBy, int PostedBy, DateTime PostedAt)>(
                "SELECT Status,WarehouseId,ReceiptDate,CreatedByUserId,PostedByUserId,PostedAtUtc FROM dbo.GoodsReceipts WHERE ReceiptId=@id;", new { id });
            Assert.Equal(("Posted", target.Warehouse, target.Date, actor, actor), (header.Status, header.Warehouse, header.Date, header.CreatedBy, header.PostedBy));
            stamps.Add(header.PostedAt);
            var line = Assert.Single(await sql.QueryAsync<(long Id, int Product, decimal Quantity, decimal Cost, int CreatedBy)>(
                "SELECT ReceiptLineId,ProductId,Quantity,UnitCost,CreatedByUserId FROM dbo.GoodsReceiptLines WHERE ReceiptId=@id;", new { id }));
            Assert.Equal((catalog.Product, target.Quantity, target.Cost, actor), (line.Product, line.Quantity, line.Cost, line.CreatedBy));
            Assert.Equal(target.Value, decimal.Round(line.Quantity * line.Cost, 4, MidpointRounding.AwayFromZero));
            var movement = await sql.QuerySingleAsync<(int Warehouse, int Product, decimal Quantity, decimal Value, int Actor, DateTime Stamp)>(
                "SELECT l.WarehouseId,l.ProductId,l.QuantityChange,v.ValueChange,l.PostedByUserId,l.PostedAtUtc FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId WHERE l.DocumentType='GoodsReceipt' AND l.DocumentId=@id AND l.DocumentLineId=@line;", new { id, line = line.Id });
            Assert.Equal((target.Warehouse, catalog.Product, target.Quantity, target.Value, actor, header.PostedAt), movement);
            Assert.Equal(target.Quantity.ToString("0.0000", CultureInfo.InvariantCulture), OptionalResultString(receiptRows[index], "quantity"));
            Assert.Equal(target.Value.ToString("0.0000", CultureInfo.InvariantCulture), OptionalResultString(receiptRows[index], "value"));
        }
        var saleHeader = await sql.QuerySingleAsync<(string Status, int Warehouse, int CreatedBy, int PostedBy, DateTime PostedAt)>(
            "SELECT Status,WarehouseId,CreatedByUserId,PostedByUserId,PostedAtUtc FROM dbo.SalesReceipts WHERE SaleId=@sale;", new { sale });
        Assert.Equal(("Posted", catalog.Warehouse, actor, actor), (saleHeader.Status, saleHeader.Warehouse, saleHeader.CreatedBy, saleHeader.PostedBy));
        var saleLine = Assert.Single(await sql.QueryAsync<(long Id, int Product, decimal Quantity, decimal Price, bool Gift, decimal Amount)>(
            "SELECT SaleLineId,ProductId,Qty,UnitPrice,IsGift,LineAmount FROM dbo.SalesReceiptLines WHERE SaleId=@sale;", new { sale }));
        Assert.Equal((catalog.Product, 4m, 25m, false, 100m), (saleLine.Product, saleLine.Quantity, saleLine.Price, saleLine.Gift, saleLine.Amount));
        var frozen = await sql.QuerySingleAsync<(decimal UnitCost, decimal Cost, decimal Profit)>(
            "SELECT PostedUnitCost,CostAmount,LineAmount-CostAmount FROM dbo.SalesReceiptLines WHERE SaleLineId=@id;", new { id = saleLine.Id });
        // Independent worked arithmetic: (100 + 200)/(10 + 10) = 15;
        // four units cost 60. Later receipt changes only future average, not this row.
        Assert.Equal((15m, 60m, 40m), frozen);
        Assert.Equal("4.0000", OptionalResultString(saleRow, "quantity"));
        Assert.Equal("60.0000", OptionalResultString(saleRow, "cost"));
        var saleMovement = await sql.QuerySingleAsync<(int Warehouse, int Product, decimal Quantity, decimal Value, int Actor, DateTime Stamp)>(
            "SELECT l.WarehouseId,l.ProductId,l.QuantityChange,v.ValueChange,l.PostedByUserId,l.PostedAtUtc FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId WHERE l.DocumentType='SalesReceipt' AND l.DocumentId=@sale AND l.DocumentLineId=@line;", new { sale, line = saleLine.Id });
        Assert.Equal((catalog.Warehouse, catalog.Product, -4m, -60m, actor, saleHeader.PostedAt), saleMovement);
        Assert.True(stamps[0] <= stamps[1] && stamps[1] <= saleHeader.PostedAt && saleHeader.PostedAt <= stamps[2] && stamps[2] <= stamps[3]);
        var order = (await sql.QueryAsync<(string Type, long Document)>("SELECT DocumentType,DocumentId FROM dbo.StockLedger WHERE ProductId=@Product ORDER BY LedgerId;", catalog)).ToArray();
        Assert.Equal(new[] { ("GoodsReceipt", receipts[0]), ("GoodsReceipt", receipts[1]), ("SalesReceipt", sale), ("GoodsReceipt", receipts[2]), ("GoodsReceipt", receipts[3]) }, order);
        Assert.Equal((18m, 340m), await sql.QuerySingleAsync<(decimal Q, decimal V)>("SELECT Quantity,InventoryValue FROM dbo.StockBalances WHERE WarehouseId=@Warehouse AND ProductId=@Product;", catalog));
        Assert.Equal((3m, 21m), await sql.QuerySingleAsync<(decimal Q, decimal V)>("SELECT Quantity,InventoryValue FROM dbo.StockBalances WHERE WarehouseId=@OtherWarehouse AND ProductId=@Product;", catalog));
    }

    private static async Task VerifyAllPilotMovements(SqlConnection sql, int warehouse, int product, int receiptProduct, IntegratedCatalog? integrated)
    {
        var keys = new List<(int Warehouse, int Product)> { (warehouse, product) };
        if (receiptProduct != 0) keys.Add((warehouse, receiptProduct));
        if (integrated != null) { keys.Add((integrated.Warehouse, integrated.Product)); keys.Add((integrated.OtherWarehouse, integrated.Product)); }
        var actual = (await sql.QueryAsync<(int Warehouse, int Product)>("SELECT WarehouseId,ProductId FROM dbo.StockBalances;")).ToArray();
        Assert.Equal(keys.OrderBy(key => key.Warehouse).ThenBy(key => key.Product), actual.OrderBy(key => key.Warehouse).ThenBy(key => key.Product));
        foreach (var key in keys)
        {
            var movement = await sql.QuerySingleAsync<(decimal Q, decimal V)>("SELECT COALESCE(SUM(l.QuantityChange),0),COALESCE(SUM(v.ValueChange),0) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId WHERE l.WarehouseId=@Warehouse AND l.ProductId=@Product;", new { key.Warehouse, key.Product });
            var opening = key == (warehouse, product);
            Assert.Equal((movement.Q + (opening ? 1000m : 0m), movement.V + (opening ? 15000m : 0m)),
                await sql.QuerySingleAsync<(decimal Q, decimal V)>("SELECT Quantity,InventoryValue FROM dbo.StockBalances WHERE WarehouseId=@Warehouse AND ProductId=@Product;", new { key.Warehouse, key.Product }));
        }
        // No extra ledger keys, orphan valuations, wrong source/key/audit, or draft movements.
        Assert.Equal(0, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger l LEFT JOIN dbo.StockBalances b ON b.WarehouseId=l.WarehouseId AND b.ProductId=l.ProductId WHERE b.ProductId IS NULL;"));
        Assert.Equal(0, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries v LEFT JOIN dbo.StockLedger l ON l.LedgerId=v.LedgerId WHERE l.LedgerId IS NULL;"));
        Assert.Equal(0, await sql.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM dbo.StockLedger l
LEFT JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId
LEFT JOIN dbo.GoodsReceipts r ON l.DocumentType='GoodsReceipt' AND r.ReceiptId=l.DocumentId
LEFT JOIN dbo.GoodsReceiptLines rl ON rl.ReceiptId=r.ReceiptId AND rl.ReceiptLineId=l.DocumentLineId
LEFT JOIN dbo.SalesReceipts s ON l.DocumentType='SalesReceipt' AND s.SaleId=l.DocumentId
LEFT JOIN dbo.SalesReceiptLines sl ON sl.SaleId=s.SaleId AND sl.SaleLineId=l.DocumentLineId
WHERE v.LedgerId IS NULL OR NOT (
 (l.DocumentType='GoodsReceipt' AND r.Status='Posted' AND rl.ReceiptLineId IS NOT NULL AND l.WarehouseId=r.WarehouseId AND l.ProductId=rl.ProductId AND l.QuantityChange=rl.Quantity AND v.ValueChange=ROUND(rl.Quantity*rl.UnitCost,4) AND l.PostedAtUtc=r.PostedAtUtc AND l.PostedByUserId=r.PostedByUserId)
 OR (l.DocumentType='SalesReceipt' AND s.Status='Posted' AND sl.SaleLineId IS NOT NULL AND l.WarehouseId=s.WarehouseId AND l.ProductId=sl.ProductId AND l.QuantityChange=-sl.Qty AND v.ValueChange=-sl.CostAmount AND l.PostedAtUtc=s.PostedAtUtc AND l.PostedByUserId=s.PostedByUserId));"));
        Assert.Equal(0, await sql.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM (
 SELECT 'GoodsReceipt' AS DocumentType,r.ReceiptId AS DocumentId,l.ReceiptLineId AS LineId FROM dbo.GoodsReceipts r JOIN dbo.GoodsReceiptLines l ON l.ReceiptId=r.ReceiptId WHERE r.Status='Posted'
 UNION ALL
 SELECT 'SalesReceipt',s.SaleId,l.SaleLineId FROM dbo.SalesReceipts s JOIN dbo.SalesReceiptLines l ON l.SaleId=s.SaleId WHERE s.Status='Posted'
) source LEFT JOIN dbo.StockLedger l ON l.DocumentType=source.DocumentType AND l.DocumentId=source.DocumentId AND l.DocumentLineId=source.LineId
WHERE l.LedgerId IS NULL;"));
        var sourceCount = await sql.QuerySingleAsync<int>("SELECT (SELECT COUNT(*) FROM dbo.GoodsReceiptLines l JOIN dbo.GoodsReceipts r ON r.ReceiptId=l.ReceiptId WHERE r.Status='Posted') + (SELECT COUNT(*) FROM dbo.SalesReceiptLines l JOIN dbo.SalesReceipts s ON s.SaleId=l.SaleId WHERE s.Status='Posted');");
        Assert.Equal(sourceCount, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
        Assert.Equal(sourceCount, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries;"));
    }
}
