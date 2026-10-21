using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using DbUp;
using DbUp.Engine;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class DirectSalesBootstrapTests
{
    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bootstrap_preserves_exact_source_ledger_audit_and_rounds_each_line_before_sum(bool zeroCost)
    {
        var db = await Create9();
        try
        {
            using var c = new SqlConnection(db.ConnectionString);
            await Seed(c);
            if (zeroCost) await c.ExecuteAsync("UPDATE dbo.GoodsReceiptLines SET UnitCost=0;");
            var before = await Snapshot(c);
            var result = Run010(db.ConnectionString, Script());
            Assert.True(result.Successful, result.Error?.ToString());
            Assert.Equal(before, await Snapshot(c));
            Assert.Equal(2, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries WHERE ValueChange=@value;", new { value = zeroCost ? 0m : 0.0001m }));
            Assert.Equal(0, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger e FULL JOIN dbo.StockValuationEntries v ON e.LedgerId=v.LedgerId WHERE e.LedgerId IS NULL OR v.LedgerId IS NULL;"));
            Assert.Equal(zeroCost ? 0m : 0.0002m, await c.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances;"));
            Assert.Equal(0.0002m, await c.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances;"));
            Assert.Equal(10, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        finally { await db.DisposeAsync(); }
    }

    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData("unknown")]
    [InlineData("wrong-case")]
    [InlineData("trailing-space")]
    [InlineData("too-many-lines")]
    [InlineData("negative")]
    [InlineData("wrong-document")]
    [InlineData("wrong-line")]
    [InlineData("wrong-quantity")]
    [InlineData("wrong-warehouse")]
    [InlineData("wrong-product")]
    [InlineData("duplicate")]
    [InlineData("wrong-actor")]
    [InlineData("wrong-time")]
    [InlineData("not-posted")]
    [InlineData("missing-ledger")]
    [InlineData("extra-zero-key")]
    [InlineData("missing-balance")]
    [InlineData("wrong-balance")]
    [InlineData("empty-posted")]
    [InlineData("line-overflow")]
    [InlineData("sum-overflow")]
    [InlineData("quantity-overflow")]
    public async Task Invalid_history_refuses_atomically_without_inventing_values(string scenario)
    {
        var db = await Create9();
        try
        {
            using var c = new SqlConnection(db.ConnectionString);
            await Seed(c);
            await Corrupt(c, scenario);
            var before = await Snapshot(c);
            var balanceBefore = await BalanceSnapshot(c);
            if (scenario == "duplicate") Assert.Equal("Migration.PartialSchema", Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().Migrate(db.ConnectionString)).Code);
            var result = Run010(db.ConnectionString, Script());
            Assert.False(result.Successful);
            var error = Assert.IsType<SqlException>(result.Error);
            var expected = scenario == "line-overflow" || scenario == "sum-overflow" ? 8115 :
                scenario == "empty-posted" || scenario == "too-many-lines" ? 51012 :
                (scenario == "missing-ledger" || scenario == "duplicate") ? 51013 :
                scenario == "extra-zero-key" || scenario == "missing-balance" || scenario == "wrong-balance" || scenario == "quantity-overflow" ? 51014 : 51011;
            Assert.Equal(expected, error.Number);
            Assert.Equal(before, await Snapshot(c));
            Assert.Equal(balanceBefore, await BalanceSnapshot(c));
            await Assert9(c, scenario != "duplicate");
        }
        finally { await db.DisposeAsync(); }
    }

    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData("before")]
    [InlineData("during")]
    [InlineData("after")]
    [InlineData("journal")]
    public async Task Injected_fault_rolls_back_every010_batch_and_rerun_succeeds(string stage)
    {
        var db = await Create9();
        try
        {
            using var c = new SqlConnection(db.ConnectionString);
            await Seed(c);
            var before = await Snapshot(c);
            var balanceBefore = await BalanceSnapshot(c);
            var script = Script();
            const string fault = "\r\nTHROW 51999, 'Expected migration fault.', 1;\r\n";
            if (stage == "before") script = fault + script;
            if (stage == "during") script = script.Replace("ALTER TABLE dbo.StockBalances ADD InventoryValue", fault + "ALTER TABLE dbo.StockBalances ADD InventoryValue");
            if (stage == "after") script += fault;
            if (stage == "journal") await c.ExecuteAsync("EXEC(N'CREATE TRIGGER dbo.Test010JournalFailure ON dbo.SchemaVersions AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE ScriptName LIKE ''%010_DirectSalesAndValuation.sql'') THROW 51999, ''Expected journal failure.'', 1;');");
            var result = Run010(db.ConnectionString, script);
            Assert.False(result.Successful);
            Assert.Equal(51999, Assert.IsType<SqlException>(result.Error).Number);
            Assert.Equal(before, await Snapshot(c));
            Assert.Equal(balanceBefore, await BalanceSnapshot(c));
            if (stage == "journal") await c.ExecuteAsync("DROP TRIGGER dbo.Test010JournalFailure;");
            await Assert9(c);
            var retry = Run010(db.ConnectionString, Script());
            Assert.True(retry.Successful, retry.Error?.ToString());
            Assert.Equal(before, await Snapshot(c));
        }
        finally { await db.DisposeAsync(); }
    }

    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData("0.0001", "0.5000")]
    [InlineData("99999999999999.9999", "0.0001")]
    [InlineData("99999999999999.9999", "0")]
    [InlineData("99999999999999.9999", "999999999999999.9999")]
    [InlineData("1000000000", "999999999999999.9999")]
    public async Task Historical_SQL_product_matches_independent_integer_rounding_and_range_oracle(string quantity, string cost)
    {
        var db = await Create9();
        try
        {
            var q = decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture);
            var u = decimal.Parse(cost, System.Globalization.CultureInfo.InvariantCulture);
            // Inputs are rescaled exactly to four places; no production calculator in the oracle.
            var qCoefficient = new BigInteger(q * 10000m);
            var uCoefficient = new BigInteger(u * 10000m);
            var coefficient = BigInteger.DivRem(qCoefficient * uCoefficient, 10000, out var remainder);
            if (remainder * 2 >= 10000) coefficient++;
            using var c = new SqlConnection(db.ConnectionString);
            const string sql = "SELECT CONVERT(decimal(28,4),ROUND(CONVERT(decimal(18,4),@q)*CONVERT(decimal(19,4),@u),4));";
            if (coefficient > BigInteger.Pow(10, 28) - 1)
            {
                var ex = await Assert.ThrowsAsync<SqlException>(() => c.QuerySingleAsync<decimal>(sql, new { q, u }));
                Assert.Equal(8115, ex.Number);
            }
            else Assert.Equal((decimal)coefficient / 10000m, await c.QuerySingleAsync<decimal>(sql, new { q, u }));
        }
        finally { await db.DisposeAsync(); }
    }
    private static async Task<SqlTestDatabase> Create9()
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            DirectSalesSchemaCompatibilityTests.Install9(db.ConnectionString);
            using var c = new SqlConnection(db.ConnectionString);
            await c.OpenAsync();
            await db.VerifyOwnershipAsync(c, CancellationToken.None);
            await c.ExecuteAsync("ALTER DATABASE CURRENT SET RESTRICTED_USER;");
            return db;
        }
        catch { await db.DisposeAsync(); throw; }
    }
    private static string Script()
    {
        var a = typeof(DatabaseMigrationRunner).Assembly;
        using var r = new StreamReader(a.GetManifestResourceStream(a.GetManifestResourceNames().Single(n => n.EndsWith(".Scripts.010_DirectSalesAndValuation.sql", StringComparison.Ordinal)))!);
        return r.ReadToEnd();
    }
    private static DatabaseUpgradeResult Run010(string cs, string text) => DeployChanges.To.SqlDatabase(cs)
        .WithScript("MyDmsVn.Server.DbMigrator.Scripts.010_DirectSalesAndValuation.sql", text)
        .JournalToSqlTable("dbo", "SchemaVersions").WithTransactionPerScript().LogToNowhere().Build().PerformUpgrade();
    private static async Task Assert9(SqlConnection c, bool exact = true)
    {
        Assert.Equal(9, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        Assert.Equal(0, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN(N'SalesReceipts',N'SalesReceiptLines',N'StockValuationEntries');"));
        Assert.Equal(0, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StockBalances') AND name=N'InventoryValue';"));
        if (c.State != System.Data.ConnectionState.Open) await c.OpenAsync();
        var source = await SqlSchemaCompatibilityChecker.CheckExactSchema9Async(c, CancellationToken.None);
        if (exact) Assert.True(source.IsCompatible); else Assert.Equal(SchemaCompatibilityStatus.Partial, source.Status);
    }
    private static Task Seed(SqlConnection c) => c.ExecuteAsync(@"
INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) VALUES(N't01',N'T01',N'T01',N'Local',N'hash',N'',N'BCrypt');
INSERT dbo.Warehouses(Code,Name) VALUES(N'W',N'Warehouse');
INSERT dbo.Products(Code,Name,Unit) VALUES(N'P',N'Product',N'EA'),(N'P2',N'Unused',N'EA');
INSERT dbo.Employees(Code,Name) VALUES(N'E',N'Employee');
DECLARE @now datetime2(7)='2026-10-22T01:02:03.1234567';
INSERT dbo.GoodsReceipts(ReceiptNo,ReceiptDate,WarehouseId,EmployeeId,Status,CreatedByUserId,PostedAtUtc,PostedByUserId) VALUES(N'R1','20261022',1,1,'Posted',1,@now,1),(N'R2','20261022',1,1,'Posted',1,@now,1);
INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId) VALUES(1,1,1,0.0001,0.5000,1),(2,1,1,0.0001,0.5000,1);
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) VALUES(1,1,'GoodsReceipt',1,1,0.0001,@now,1),(1,1,'GoodsReceipt',2,2,0.0001,@now,1);
INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity) VALUES(1,1,0.0002);");
    private static Task<string> BalanceSnapshot(SqlConnection c) => c.QuerySingleAsync<string>("SELECT (SELECT WarehouseId,ProductId,Quantity,RowVersion FROM dbo.StockBalances ORDER BY WarehouseId,ProductId FOR JSON PATH,INCLUDE_NULL_VALUES);");
    private static Task<string> Snapshot(SqlConnection c) => c.QuerySingleAsync<string>(@"
SELECT (SELECT * FROM dbo.StockLedger ORDER BY LedgerId FOR JSON PATH,INCLUDE_NULL_VALUES) +
(SELECT * FROM dbo.GoodsReceipts ORDER BY ReceiptId FOR JSON PATH,INCLUDE_NULL_VALUES) +
(SELECT * FROM dbo.GoodsReceiptLines ORDER BY ReceiptLineId FOR JSON PATH,INCLUDE_NULL_VALUES);");
    private static Task Corrupt(SqlConnection c, string scenario)
    {
        string sql;
        switch (scenario)
        {
            case "wrong-case": sql = "UPDATE dbo.StockLedger SET DocumentType='goodsreceipt' WHERE LedgerId=1;"; break;
            case "trailing-space": sql = "UPDATE dbo.StockLedger SET DocumentType='GoodsReceipt ' WHERE LedgerId=1;"; break;
            case "too-many-lines": sql = @"
;WITH n AS(SELECT 1 i UNION ALL SELECT i+1 FROM n WHERE i<201) INSERT dbo.Products(Code,Name,Unit) SELECT CONCAT(N'PX',i),N'Extra',N'EA' FROM n OPTION(MAXRECURSION 0);
INSERT dbo.GoodsReceipts(ReceiptNo,ReceiptDate,WarehouseId,EmployeeId,Status,CreatedByUserId,PostedAtUtc,PostedByUserId) VALUES(N'R3','20261022',1,1,'Posted',1,'20261022',1);
INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId) SELECT 3,ProductId-2,ProductId,1,1,1 FROM dbo.Products WHERE ProductId>2;
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) SELECT 1,ProductId,'GoodsReceipt',3,ReceiptLineId,1,'20261022',1 FROM dbo.GoodsReceiptLines WHERE ReceiptId=3;
INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity) SELECT 1,ProductId,1 FROM dbo.Products WHERE ProductId>2;"; break;
            case "unknown": sql = "UPDATE dbo.StockLedger SET DocumentType='Unknown' WHERE LedgerId=1;"; break;
            case "negative": sql = "UPDATE dbo.StockLedger SET QuantityChange=-0.0001 WHERE LedgerId=1;"; break;
            case "wrong-document": sql = "UPDATE dbo.StockLedger SET DocumentId=2 WHERE LedgerId=1;"; break;
            case "wrong-line": sql = "UPDATE dbo.StockLedger SET DocumentLineId=999 WHERE LedgerId=1;"; break;
            case "wrong-warehouse": sql = "INSERT dbo.Warehouses(Code,Name) VALUES(N'W2',N'Other'); UPDATE dbo.StockLedger SET WarehouseId=2 WHERE LedgerId=1;"; break;
            case "wrong-product": sql = "UPDATE dbo.StockLedger SET ProductId=2 WHERE LedgerId=1;"; break;
            case "duplicate": sql = "DROP INDEX UX_StockLedger_DocumentType_DocumentLineId ON dbo.StockLedger; INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) SELECT WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId FROM dbo.StockLedger WHERE LedgerId=1;"; break;
            case "wrong-quantity": sql = "UPDATE dbo.StockLedger SET QuantityChange=2 WHERE LedgerId=1;"; break;
            case "wrong-actor": sql = "UPDATE dbo.GoodsReceipts SET PostedByUserId=2 WHERE ReceiptId=1;"; sql = "INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) VALUES(N't02',N'T02',N'T02',N'Local',N'hash',N'',N'BCrypt');" + sql; break;
            case "wrong-time": sql = "UPDATE dbo.GoodsReceipts SET PostedAtUtc=DATEADD(second,1,PostedAtUtc) WHERE ReceiptId=1;"; break;
            case "not-posted": sql = "UPDATE dbo.GoodsReceipts SET Status='Draft',PostedAtUtc=NULL,PostedByUserId=NULL WHERE ReceiptId=1;"; break;
            case "missing-ledger": sql = "DELETE dbo.StockLedger WHERE LedgerId=1;"; break;
            case "extra-zero-key": sql = "INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity) VALUES(1,2,0);"; break;
            case "missing-balance": sql = "DELETE dbo.StockBalances;"; break;
            case "wrong-balance": sql = "UPDATE dbo.StockBalances SET Quantity=1;"; break;
            case "empty-posted": sql = "DELETE dbo.StockLedger WHERE LedgerId=1; DELETE dbo.GoodsReceiptLines WHERE ReceiptId=1; UPDATE dbo.StockBalances SET Quantity=0.0001;"; break;
            case "line-overflow": sql = "UPDATE dbo.GoodsReceiptLines SET Quantity=99999999999999.9999,UnitCost=999999999999999.9999 WHERE ReceiptLineId=1; UPDATE dbo.StockLedger SET QuantityChange=99999999999999.9999 WHERE LedgerId=1; DELETE dbo.StockLedger WHERE LedgerId=2; DELETE dbo.GoodsReceiptLines WHERE ReceiptId=2; DELETE dbo.GoodsReceipts WHERE ReceiptId=2; UPDATE dbo.StockBalances SET Quantity=99999999999999.9999;"; break;
            case "quantity-overflow": sql = "UPDATE dbo.GoodsReceiptLines SET Quantity=99999999999999.9999,UnitCost=0; UPDATE dbo.StockLedger SET QuantityChange=99999999999999.9999; UPDATE dbo.StockBalances SET Quantity=99999999999999.9999;"; break;
            case "sum-overflow": sql = "UPDATE dbo.GoodsReceiptLines SET Quantity=600000000,UnitCost=999999999999999.9999; UPDATE dbo.StockLedger SET QuantityChange=600000000; UPDATE dbo.StockBalances SET Quantity=1200000000;"; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        return c.ExecuteAsync("DISABLE TRIGGER dbo.TR_StockLedger_AppendOnly ON dbo.StockLedger; " + sql + " ENABLE TRIGGER dbo.TR_StockLedger_AppendOnly ON dbo.StockLedger;");
    }
}
