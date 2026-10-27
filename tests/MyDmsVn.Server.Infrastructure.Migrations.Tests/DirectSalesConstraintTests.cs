using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Testing.Sql;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class DirectSalesConstraintTests
{
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Sales_preserves_duplicate_product_identity_gift_price_and_rejects_partial_cost_snapshot()
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(db.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());
            using var c = new SqlConnection(db.ConnectionString);
            await c.ExecuteAsync(@"
INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) VALUES(N'constraint',N'CONSTRAINT',N'Constraint',N'Local',N'hash',N'',N'BCrypt');
INSERT dbo.Warehouses(Code,Name) VALUES(N'W',N'Warehouse'); INSERT dbo.Products(Code,Name,Unit) VALUES(N'P',N'Product',N'EA');
INSERT dbo.Employees(Code,Name) VALUES(N'E',N'Employee'); INSERT dbo.Customers(Code,Name) VALUES(N'C',N'Customer');
INSERT dbo.SalesReceipts(SaleNo,SaleDate,WarehouseId,EmployeeId,CustomerId,CreatedByUserId) VALUES(N'S1','20261022',1,1,1,1);
INSERT dbo.SalesReceiptLines(SaleId,[LineNo],ProductId,Qty,Unit,UnitPrice,IsGift,LineAmount,CreatedByUserId) VALUES(1,1,1,2,N'EA',5,0,10,1),(1,2,1,2,N'EA',5,1,0,1);");
            Assert.Equal(2, await c.QuerySingleAsync<int>("SELECT COUNT(DISTINCT SaleLineId) FROM dbo.SalesReceiptLines WHERE ProductId=1 AND SaleId=1;"));
            Assert.Equal(5m, await c.QuerySingleAsync<decimal>("SELECT UnitPrice FROM dbo.SalesReceiptLines WHERE IsGift=1 AND LineAmount=0;"));
            Assert.Equal(2, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceiptLines WHERE PostedUnitCost IS NULL AND CostAmount IS NULL;"));
            foreach (var mutation in new[] { "Qty=0", "UnitPrice=-0.0001", "LineAmount=-0.0001", "[LineNo]=0", "Unit=NCHAR(0)", "Unit=NCHAR(9)", "PostedUnitCost=1,CostAmount=NULL", "PostedUnitCost=NULL,CostAmount=1", "PostedUnitCost=-0.00000001,CostAmount=1" })
            {
                var ex = await Assert.ThrowsAsync<SqlException>(() => c.ExecuteAsync("UPDATE dbo.SalesReceiptLines SET " + mutation + " WHERE SaleLineId=1;"));
                Assert.Equal(547, ex.Number);
            }
            var unique = await Assert.ThrowsAsync<SqlException>(() => c.ExecuteAsync("UPDATE dbo.SalesReceiptLines SET [LineNo]=1 WHERE SaleLineId=2;"));
            Assert.Contains(unique.Number, new[] { 2601, 2627 });
            await c.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(1,1,0,0);");
            foreach (var mutation in new[] { "Quantity=-0.0001", "InventoryValue=-0.0001", "InventoryValue=0.0001" })
            {
                var ex = await Assert.ThrowsAsync<SqlException>(() => c.ExecuteAsync("UPDATE dbo.StockBalances SET " + mutation + ";"));
                Assert.Equal(547, ex.Number);
            }
        }
        finally { await db.DisposeAsync(); }
    }
}
