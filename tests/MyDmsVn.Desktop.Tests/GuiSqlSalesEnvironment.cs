using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Identity;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

internal sealed partial class GuiSqlEnvironment
{
    private string? _salesDraftVersion;
    private string? _salesDraftLines;
    private string? _salesWinnerVersion;
    private string? _salesWinnerLines;

    internal async Task CaptureSalesWinnerVersionAsync(bool post)
    {
        if (post) await AssertSalesGoldenAsync();
        else await AssertSalesDraftAsync("SAL winner");
        _salesWinnerVersion = await ScalarAsync<string>(_oracle, "SELECT CONVERT(varchar(16),RowVersion,2) FROM dbo.SalesReceipts;");
        _salesWinnerLines = await ScalarAsync<string>(_oracle, SalesLineSnapshotSql);
    }

    internal async Task AssertSalesWinnerUnchangedAsync(bool post)
    {
        if (post) await AssertSalesGoldenAsync();
        else await AssertSalesDraftAsync("SAL winner");
        Assert.False(string.IsNullOrEmpty(_salesWinnerVersion));
        Assert.False(string.IsNullOrEmpty(_salesWinnerLines));
        Assert.Equal(_salesWinnerVersion, await ScalarAsync<string>(_oracle, "SELECT CONVERT(varchar(16),RowVersion,2) FROM dbo.SalesReceipts;"));
        Assert.Equal(_salesWinnerLines, await ScalarAsync<string>(_oracle, SalesLineSnapshotSql));
    }
    private const string SalesPostTimeoutTrigger = "CREATE TRIGGER dbo.GuiSal_PostTimeout ON dbo.SalesReceipts AFTER UPDATE AS " +
        "BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.SaleId=i.SaleId " +
        "WHERE i.Status='Posted' AND d.Status='Draft') WAITFOR DELAY '00:00:45'; END;";
    private int _salesPostTimeoutTriggerId;

    internal async Task CaptureSalesDraftVersionAsync()
    {
        await AssertSalesDraftAsync("SAL golden");
        _salesDraftVersion = await ScalarAsync<string>(_oracle, "SELECT CONVERT(varchar(16),RowVersion,2) FROM dbo.SalesReceipts;");
        _salesDraftLines = await ScalarAsync<string>(_oracle, SalesLineSnapshotSql);
    }

    internal async Task AssertSalesDraftAsync(string note, bool unchangedVersion = false)
    {
        await AssertSalesInventoryAsync(20m, 300m, 0);
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.SalesReceipts;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.SalesReceipts WHERE Status='Draft' AND Note=@note AND PostedAtUtc IS NULL;", new SqlParameter("@note", note)));
        Assert.Equal(2, await ScalarAsync<int>(_oracle, @"
SELECT COUNT(*) FROM dbo.SalesReceiptLines WHERE PostedUnitCost IS NULL AND CostAmount IS NULL AND Unit=N'pcs'
AND (([LineNo]=1 AND Qty=3 AND UnitPrice=25 AND IsGift=0 AND LineAmount=75)
 OR ([LineNo]=2 AND Qty=2 AND UnitPrice=40 AND IsGift=1 AND LineAmount=0));"));
        if (unchangedVersion)
        {
            Assert.Equal(_salesDraftVersion, await ScalarAsync<string>(_oracle, "SELECT CONVERT(varchar(16),RowVersion,2) FROM dbo.SalesReceipts;"));
            Assert.Equal(_salesDraftLines, await ScalarAsync<string>(_oracle, SalesLineSnapshotSql));
        }
    }

    private const string SalesLineSnapshotSql = @"SELECT SaleLineId,[LineNo],ProductId,Qty,Unit,UnitPrice,IsGift,LineAmount,
PostedUnitCost,CostAmount,CreatedAtUtc,CreatedByUserId,UpdatedAtUtc,UpdatedByUserId FROM dbo.SalesReceiptLines
ORDER BY [LineNo] FOR XML RAW('line'),ROOT('lines');";

    internal async Task DenySalesPermissionAsync(string permission)
    {
        Assert.Contains(permission, new[] { "Read", "Write", "Post" });
        await CaptureSalesDraftVersionAsync();
        await ExecuteAsync(_migration, "INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) SELECT UserId,N'Sales.Receipts." + permission + "',0 FROM dbo.Users WHERE NormalizedUsername=N'GUI.SAL.WRITER';");
        Assert.Equal(1, await ScalarAsync<int>(_oracle, @"
SELECT COUNT(*) FROM dbo.UserPermissions up JOIN dbo.UserRoles ur ON ur.UserId=up.UserId
JOIN dbo.RolePermissions rp ON rp.RoleId=ur.RoleId AND rp.PermissionKey=up.PermissionKey
WHERE up.Granted=0 AND up.PermissionKey=@key;", new SqlParameter("@key", "Sales.Receipts." + permission)));
    }

    internal async Task InstallOwnedSalesPostTimeoutAsync()
    {
        await CaptureSalesDraftVersionAsync();
        await ExecuteAsync(_migration, SalesPostTimeoutTrigger);
        _salesPostTimeoutTriggerId = await ScalarAsync<int>(_migration, "SELECT OBJECT_ID(N'dbo.GuiSal_PostTimeout',N'TR');");
        Assert.True(_salesPostTimeoutTriggerId > 0);
    }

    internal async Task AssertSalesTimeoutAndRemoveFaultAsync()
    {
        await AssertSalesDraftAsync("SAL golden", unchangedVersion: true);
        using var connection = new SqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await _database.VerifyOwnershipAsync(connection, System.Threading.CancellationToken.None);
        Assert.Equal(_salesPostTimeoutTriggerId, await ScalarAsync<int>(_migration, "SELECT OBJECT_ID(N'dbo.GuiSal_PostTimeout',N'TR');"));
        Assert.Equal(SalesPostTimeoutTrigger, await ScalarAsync<string>(_migration, "SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.GuiSal_PostTimeout',N'TR'));"));
        await ExecuteAsync(_migration, "DROP TRIGGER dbo.GuiSal_PostTimeout;");
    }

    internal async Task SeedSalesWriterAsync()
    {
        await VerifySeparationAsync();
        using var connection = new SqlConnection(_migration);
        await connection.OpenAsync();
        using var command = new SqlCommand(@"
INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm,IsActive)
VALUES(N'gui.sal.writer',N'GUI.SAL.WRITER',N'SAL Writer',N'Local',@hash,N'',N'BCrypt',1);
DECLARE @actor int=SCOPE_IDENTITY();
INSERT dbo.Roles(RoleName,NormalizedRoleName) VALUES(N'SAL Writer',N'SAL WRITER');
DECLARE @role int=SCOPE_IDENTITY(); INSERT dbo.UserRoles(UserId,RoleId) VALUES(@actor,@role);
INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES
(@role,N'Catalog.Products.Read'),(@role,N'Catalog.Warehouses.Read'),(@role,N'Catalog.Employees.Read'),(@role,N'Catalog.Customers.Read'),
(@role,N'Sales.Receipts.Read'),(@role,N'Sales.Receipts.Write'),(@role,N'Sales.Receipts.Post');
INSERT dbo.Products(Code,Name,Unit) VALUES(N'SAL-P',N'Sales Product',N'pcs');
INSERT dbo.Warehouses(Code,Name) VALUES(N'SAL-W',N'Sales Warehouse');
INSERT dbo.Employees(Code,Name) VALUES(N'SAL-E',N'Sales Employee');
INSERT dbo.Customers(Code,Name) VALUES(N'SAL-C',N'Sales Customer');
", connection) { CommandTimeout = 15 };
        command.Parameters.AddWithValue("@hash", new BcryptPasswordHasher(10).Hash(ActorPassword));
        await command.ExecuteNonQueryAsync();
        await AddOwnedSalesStockAsync(false);
        if (_expectedSchema == 10) await AssertSalesInventoryAsync(20m, 300m, 0);
        else Assert.Equal(20m, await ScalarAsync<decimal>(_oracle, "SELECT Quantity FROM dbo.StockBalances;"));
    }

    internal Task SeedSalesListProductAsync() => ExecuteAsync(_migration,
        "INSERT dbo.Products(Code,Name,Unit) VALUES(N'SAL-X',N'Other Sales Product',N'pcs');");

    internal async Task AssertSalesListFixtureAsync(string output)
    {
        Assert.Equal(4, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.SalesReceipts;"));
        Assert.Equal(3, await ScalarAsync<int>(_oracle, @"
SELECT COUNT(*) FROM dbo.SalesReceipts h WHERE EXISTS
(SELECT 1 FROM dbo.SalesReceiptLines l JOIN dbo.Products p ON p.ProductId=l.ProductId WHERE l.SaleId=h.SaleId AND p.Code=N'SAL-P');"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, @"
SELECT COUNT(*) FROM dbo.SalesReceipts h WHERE h.Note=N'SAL list other-only' AND h.Status='Draft'
AND (SELECT COUNT(*) FROM dbo.SalesReceiptLines l WHERE l.SaleId=h.SaleId)=1
AND EXISTS(SELECT 1 FROM dbo.SalesReceiptLines l JOIN dbo.Products p ON p.ProductId=l.ProductId
 WHERE l.SaleId=h.SaleId AND p.Code=N'SAL-X' AND l.Qty=1 AND l.UnitPrice=11 AND l.LineAmount=11 AND l.IsGift=0);"));
        Assert.Equal(2, await ScalarAsync<int>(_oracle, @"
SELECT COUNT(*) FROM dbo.SalesReceiptLines l JOIN dbo.SalesReceipts h ON h.SaleId=l.SaleId
JOIN dbo.Products p ON p.ProductId=l.ProductId WHERE h.Note=N'SAL list mixed' AND h.Status='Draft' AND l.IsGift=0
AND ((p.Code=N'SAL-P' AND l.Qty=1 AND l.UnitPrice=2 AND l.LineAmount=2)
 OR (p.Code=N'SAL-X' AND l.Qty=2 AND l.UnitPrice=7 AND l.LineAmount=14));"));
        Assert.Equal(16m, await ScalarAsync<decimal>(_oracle, "SELECT SUM(l.LineAmount) FROM dbo.SalesReceiptLines l JOIN dbo.SalesReceipts h ON h.SaleId=l.SaleId WHERE h.Note=N'SAL list mixed';"));
        Assert.Equal(3m, await ScalarAsync<decimal>(_oracle, "SELECT SUM(l.LineAmount) FROM dbo.SalesReceiptLines l JOIN dbo.SalesReceipts h ON h.SaleId=l.SaleId WHERE h.Note=N'SAL list pure';"));
        var expected = await ScalarAsync<string>(_oracle, @"
SELECT STUFF((SELECT N'|Open '+SaleNo FROM dbo.SalesReceipts
WHERE Note IN(N'SAL list mixed',N'SAL list pure',N'SAL golden') ORDER BY SaleId DESC
FOR XML PATH(''),TYPE).value('.','nvarchar(max)'),1,1,N'');");
        Assert.Contains("SALES_FILTERED_ACTIONS=" + expected, output);
    }

    // Consistent administrative fixture: every opening/later inflow has its Posted
    // source, exact audit, quantity movement and valuation companion. No orphan Q/V.
    internal Task AddOwnedSalesStockAsync(bool later) => ExecuteAsync(_migration, @"
SET XACT_ABORT ON; BEGIN TRANSACTION;
DECLARE @actor int=(SELECT UserId FROM dbo.Users WHERE NormalizedUsername=N'GUI.SAL.WRITER'),
@warehouse int=(SELECT WarehouseId FROM dbo.Warehouses WHERE Code=N'SAL-W'),
@employee int=(SELECT EmployeeId FROM dbo.Employees WHERE Code=N'SAL-E'),
@product int=(SELECT ProductId FROM dbo.Products WHERE Code=N'SAL-P'), @at datetime2(7)=SYSUTCDATETIME();
INSERT dbo.GoodsReceipts(ReceiptNo,ReceiptDate,WarehouseId,EmployeeId,Status,CreatedByUserId,PostedAtUtc,PostedByUserId)
VALUES(N'" + (later ? "SAL-LATER" : "SAL-OPENING") + @"','2026-01-01',@warehouse,@employee,'Posted',@actor,@at,@actor);
DECLARE @receipt bigint=SCOPE_IDENTITY();
INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId)
VALUES(@receipt,1,@product," + (later ? "5,30" : "20,15") + @",@actor);
DECLARE @line bigint=SCOPE_IDENTITY();
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId)
VALUES(@warehouse,@product,'GoodsReceipt',@receipt,@line," + (later ? "5" : "20") + @",@at,@actor);
" + (_expectedSchema == 10 ? "INSERT dbo.StockValuationEntries(LedgerId,ValueChange) VALUES(SCOPE_IDENTITY()," + (later ? "150" : "300") + ");" : "") + @"
" + (_expectedSchema == 9 ? "INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity) VALUES(@warehouse,@product,20);" : later ? "UPDATE dbo.StockBalances SET Quantity=Quantity+5,InventoryValue=InventoryValue+150 WHERE WarehouseId=@warehouse AND ProductId=@product;"
            : "INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@warehouse,@product,20,300);") + @"
COMMIT;");

    internal async Task AssertSalesInventoryAsync(decimal quantity, decimal value, int saleMovements)
    {
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockBalances;"));
        Assert.Equal(quantity, await ScalarAsync<decimal>(_oracle, "SELECT Quantity FROM dbo.StockBalances;"));
        Assert.Equal(value, await ScalarAsync<decimal>(_oracle, "SELECT InventoryValue FROM dbo.StockBalances;"));
        Assert.Equal(saleMovements, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentType='SalesReceipt';"));
        Assert.Equal(0, await ScalarAsync<int>(_oracle, @"
SELECT COUNT(*) FROM dbo.StockLedger l FULL JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId
WHERE l.LedgerId IS NULL OR v.LedgerId IS NULL;"));
        Assert.Equal(0, await ScalarAsync<int>(_oracle, @"
SELECT COUNT(*) FROM dbo.StockBalances b JOIN
(SELECT l.WarehouseId,l.ProductId,SUM(l.QuantityChange) Q,SUM(v.ValueChange) V
 FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId GROUP BY l.WarehouseId,l.ProductId) x
ON x.WarehouseId=b.WarehouseId AND x.ProductId=b.ProductId WHERE x.Q<>b.Quantity OR x.V<>b.InventoryValue;"));
    }

    internal async Task AssertSalesGoldenAsync(bool later = false)
    {
        await AssertSalesInventoryAsync(later ? 20m : 15m, later ? 375m : 225m, 2);
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.SalesReceipts WHERE Status='Posted' AND Note=N'SAL golden' AND PostedAtUtc IS NOT NULL;"));
        Assert.Equal(2, await ScalarAsync<int>(_oracle, @"
SELECT COUNT(*) FROM dbo.SalesReceiptLines s JOIN dbo.SalesReceipts h ON h.SaleId=s.SaleId
JOIN dbo.StockLedger l ON l.DocumentType='SalesReceipt' AND l.DocumentId=h.SaleId AND l.DocumentLineId=s.SaleLineId
JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId
WHERE h.Note=N'SAL golden' AND s.Unit=N'pcs' AND s.PostedUnitCost=15 AND l.QuantityChange=-s.Qty
AND v.ValueChange=-s.CostAmount AND l.WarehouseId=h.WarehouseId AND l.ProductId=s.ProductId
AND l.PostedByUserId=h.PostedByUserId AND l.PostedAtUtc=h.PostedAtUtc
AND ((s.[LineNo]=1 AND s.Qty=3 AND s.UnitPrice=25 AND s.IsGift=0 AND s.LineAmount=75 AND s.CostAmount=45)
 OR (s.[LineNo]=2 AND s.Qty=2 AND s.UnitPrice=40 AND s.IsGift=1 AND s.LineAmount=0 AND s.CostAmount=30));"));
        Assert.Equal(75m, await ScalarAsync<decimal>(_oracle, "SELECT SUM(CostAmount) FROM dbo.SalesReceiptLines WHERE CostAmount IS NOT NULL;"));
        Assert.Equal(0m, await ScalarAsync<decimal>(_oracle, "SELECT SUM(LineAmount-CostAmount) FROM dbo.SalesReceiptLines WHERE CostAmount IS NOT NULL;"));
    }
}
