using System;
using System.Data;
using System.Diagnostics;
using LinqToDB;
using LinqToDB.Data;
using MyDmsVn.Server.Infrastructure.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Contracts;
using MyDmsVn.Testing.Sql;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class InventoryValuationReconciliationTests
{
    [SqlServerFact]
    public Task Cancellation_after_materialization_releases_owned_transaction_and_exclusive_gate() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        await ValuationTestData.SeedAsync(oracle, 2, 3);
        using var provider = ValuationTestData.Provider(database.ConnectionString);
        using var cancellation = new CancellationTokenSource();
        var observed = 0;
        var sessions = new MaterializationObserver(provider.GetRequiredService<ILinqToDbSessionFactory>(), () =>
        {
            // LINQ Completed fires after the final source reader has closed, not before enumeration.
            using var probe = oracle.BeginTransaction();
            using var command = new SqlCommand("SELECT APPLOCK_TEST('public','MyDmsVn.InventoryValuation.v1','Shared','Transaction')", oracle, probe);
            Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
            probe.Rollback();
            observed++;
            cancellation.Cancel();
        });
        var service = new SqlStockReconciliationQueryService(provider.GetRequiredService<IDbConnectionFactory>(), sessions);
        var failure = await Record.ExceptionAsync(() => service.FindDifferencesAsync(cancellation.Token));
        Assert.Equal(1, observed);
        Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(ConnectionState.Closed, sessions.OwnedConnection!.State);
        using (var probe = oracle.BeginTransaction())
        {
            Assert.True(await NativeSql.Scalar<int>(oracle, probe,
                "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='MyDmsVn.InventoryValuation.v1',@LockMode='Shared',@LockOwner='Transaction',@DbPrincipal='public',@LockTimeout=0; SELECT @r;") >= 0);
            probe.Rollback();
        }
        Assert.Equal(0, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.StockLedger"));
        Assert.Equal("Draft", await NativeSql.Scalar<string>(oracle, null, "SELECT Status FROM dbo.GoodsReceipts"));
    });

    private sealed class MaterializationObserver(ILinqToDbSessionFactory inner, Action completed) : ILinqToDbSessionFactory
    {
        public IDbConnection? OwnedConnection { get; private set; }
        public DataConnection Borrow(ISqlExecutionContext context, bool requireTransaction)
        {
            OwnedConnection = context.Connection;
            using var borrowed = inner.Borrow(context, requireTransaction);
            var finalSource = false;
            return new DataConnection(borrowed.Options.UseTracing(TraceLevel.Verbose, trace =>
            {
                if (trace.TraceInfoStep == TraceInfoStep.BeforeExecute)
                    finalSource = (trace.SqlText ?? string.Empty).IndexOf("SalesReceiptLines", StringComparison.Ordinal) >= 0;
                if (trace.TraceInfoStep == TraceInfoStep.Completed && finalSource)
                {
                    finalSource = false;
                    completed();
                }
            }));
        }
        public Task<DataConnection> OpenOwnedAsync(CancellationToken token) => inner.OpenOwnedAsync(token);
    }

    [SqlServerFact]
    public Task Matching_quantity_does_not_hide_missing_value_companion() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString);
        await oracle.OpenAsync();
        var seed = await ValuationTestData.SeedAsync(oracle);
        await oracle.ExecuteAsync("INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) VALUES(@WarehouseId,@ProductId,'GoodsReceipt',@Id,@LineId,0.0001,@Stamp,@Actor); INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@WarehouseId,@ProductId,0.0001,0);",
            new { seed.Header.WarehouseId, seed.Lines[0].ProductId, seed.Header.Id, LineId = seed.Lines[0].Id, Stamp = ValuationTestData.Stamp, seed.Actor });
        using var provider = ValuationTestData.Provider(database.ConnectionString);
        var differences = await provider.GetRequiredService<IStockReconciliationQueryService>().FindDifferencesAsync(CancellationToken.None);
        Assert.NotEmpty(differences);
        Assert.Equal(0, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
    });
    [SqlServerTheory]
    [InlineData("quantity", "QuantityMismatch")]
    [InlineData("value", "ValueMismatch")]
    [InlineData("source-value", "ReceiptValueMismatch")]
    [InlineData("missing-companion", "ValuationCounterpart")]
    [InlineData("extra-companion", "OrphanValuation")]
    [InlineData("extra-zero-key", "StockKeyCounterpart")]
    [InlineData("missing-balance", "StockKeyCounterpart")]
    [InlineData("missing-source-movement", "ReceiptMovementCounterpart")]
    [InlineData("unknown", "UnsupportedMovement")]
    [InlineData("overflow", "NumericOutOfRange")]
    public Task Independent_corruption_is_reported_and_never_repaired(string corruption, string issue) => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var seed = await ValuationTestData.SeedAsync(oracle, 2, 3);
        using var provider = ValuationTestData.Provider(database.ConnectionString, seed.Actor);
        Assert.False((await ValuationTestData.PostAsync(provider, seed.Header)).IsError);
        string sql;
        switch (corruption)
        {
            case "quantity": sql = "UPDATE dbo.StockBalances SET Quantity=3"; break;
            case "value": sql = "UPDATE dbo.StockBalances SET InventoryValue=7"; break;
            case "source-value": sql = "DISABLE TRIGGER ALL ON dbo.StockValuationEntries; UPDATE dbo.StockValuationEntries SET ValueChange=7; ENABLE TRIGGER ALL ON dbo.StockValuationEntries; UPDATE dbo.StockBalances SET InventoryValue=7"; break;
            case "missing-companion": sql = "DISABLE TRIGGER ALL ON dbo.StockValuationEntries; DELETE dbo.StockValuationEntries; ENABLE TRIGGER ALL ON dbo.StockValuationEntries"; break;
            case "extra-companion": sql = "ALTER TABLE dbo.StockValuationEntries NOCHECK CONSTRAINT ALL; INSERT dbo.StockValuationEntries(LedgerId,ValueChange) VALUES(9223372036854775807,0)"; break;
            case "extra-zero-key": sql = "INSERT dbo.Products(Code,Name,Unit) VALUES(N'EXTRA',N'Extra',N'EA'); INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) SELECT TOP(1) WarehouseId,CONVERT(int,SCOPE_IDENTITY()),0,0 FROM dbo.Warehouses"; break;
            case "missing-balance": sql = "DELETE dbo.StockBalances"; break;
            case "missing-source-movement": sql = "DISABLE TRIGGER ALL ON dbo.StockValuationEntries; DELETE dbo.StockValuationEntries; ENABLE TRIGGER ALL ON dbo.StockValuationEntries; DISABLE TRIGGER ALL ON dbo.StockLedger; DELETE dbo.StockLedger; ENABLE TRIGGER ALL ON dbo.StockLedger"; break;
            case "unknown": sql = "INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) SELECT WarehouseId,ProductId,'Unknown',99,99,1,PostedAtUtc,PostedByUserId FROM dbo.StockLedger"; break;
            default: sql = "INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) SELECT WarehouseId,ProductId,'Unknown',99,99,99999999999999.9999,PostedAtUtc,PostedByUserId FROM dbo.StockLedger"; break;
        }
        await NativeSql.Execute(oracle, null, sql);
        var before = await NativeSql.Scalar<string>(oracle, null, "SELECT CONCAT((SELECT COUNT(*) FROM dbo.StockLedger),':',(SELECT COUNT(*) FROM dbo.StockValuationEntries),':',(SELECT COUNT(*) FROM dbo.StockBalances),':',COALESCE((SELECT CONVERT(varchar(80),SUM(InventoryValue)) FROM dbo.StockBalances),'null'))");
        var differences = await provider.GetRequiredService<IStockReconciliationQueryService>().FindDifferencesAsync(CancellationToken.None);
        Assert.Contains(differences, row => row.Issues.Contains(issue));
        var after = await NativeSql.Scalar<string>(oracle, null, "SELECT CONCAT((SELECT COUNT(*) FROM dbo.StockLedger),':',(SELECT COUNT(*) FROM dbo.StockValuationEntries),':',(SELECT COUNT(*) FROM dbo.StockBalances),':',COALESCE((SELECT CONVERT(varchar(80),SUM(InventoryValue)) FROM dbo.StockBalances),'null'))");
        Assert.Equal(before, after);
    });

    [SqlServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Sale_source_cost_and_gift_companions_are_checked_without_recosting(bool corrupt) => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var seed = await ValuationTestData.SeedAsync(oracle, 10, 3);
        using var provider = ValuationTestData.Provider(database.ConnectionString, seed.Actor);
        Assert.False((await ValuationTestData.PostAsync(provider, seed.Header)).IsError);
        await oracle.ExecuteAsync(@"DECLARE @customer int, @sale bigint;
INSERT dbo.Customers(Code,Name) VALUES(N'C',N'Customer'); SET @customer=SCOPE_IDENTITY();
INSERT dbo.SalesReceipts(SaleNo,SaleDate,CustomerId,WarehouseId,EmployeeId,Status,CreatedByUserId,PostedAtUtc,PostedByUserId)
VALUES(N'SA0000000001','20261022',@customer,@WarehouseId,@EmployeeId,'Posted',@Actor,@Stamp,@Actor); SET @sale=SCOPE_IDENTITY();
INSERT dbo.SalesReceiptLines(SaleId,[LineNo],ProductId,Unit,Qty,UnitPrice,IsGift,LineAmount,PostedUnitCost,CostAmount,CreatedByUserId)
VALUES(@sale,1,@ProductId,N'EA',2,5,0,10,3,6,@Actor),(@sale,2,@ProductId,N'EA',1,9,1,0,3,3,@Actor);
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId)
SELECT @WarehouseId,ProductId,'SalesReceipt',@sale,SaleLineId,-Qty,@Stamp,@Actor FROM dbo.SalesReceiptLines;
INSERT dbo.StockValuationEntries(LedgerId,ValueChange) SELECT l.LedgerId,-s.CostAmount FROM dbo.StockLedger l JOIN dbo.SalesReceiptLines s ON s.SaleLineId=l.DocumentLineId WHERE l.DocumentType='SalesReceipt';
UPDATE dbo.StockBalances SET Quantity=7,InventoryValue=21;",
            new { seed.Header.WarehouseId, seed.Header.EmployeeId, seed.Actor, Stamp = ValuationTestData.Stamp, seed.Lines[0].ProductId });
        var card = await provider.GetRequiredService<IInventoryQueryService>().GetStockCardAsync(
            new StockCardQuery(seed.Header.WarehouseId,seed.Lines[0].ProductId,null,null,1,20),CancellationToken.None);
        var saleRows = card.Items.Where(row => row.DocumentType == "SalesReceipt").ToArray();
        Assert.Equal(2,saleRows.Length); Assert.All(saleRows,row => Assert.Equal("SA0000000001",row.DocumentNumber));
        Assert.Equal(new[] { -6m,-3m },saleRows.Select(row => row.ValueChange));
        if (corrupt) await NativeSql.Execute(oracle, null, "UPDATE dbo.SalesReceiptLines SET CostAmount=4 WHERE IsGift=1");
        var differences = await provider.GetRequiredService<IStockReconciliationQueryService>().FindDifferencesAsync(CancellationToken.None);
        if (corrupt) Assert.Contains(differences, row => row.Issues.Contains("SaleSourceMismatch"));
        else Assert.Empty(differences);
    });

}
