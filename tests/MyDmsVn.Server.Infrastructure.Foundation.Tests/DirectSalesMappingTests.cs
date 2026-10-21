using System;
using System.Linq;
using LinqToDB;
using MyDmsVn.Server.Domain.Sales;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class DirectSalesMappingTests
{
    [Fact]
    public void Shared_mapping_publishes_sales_identity_number_quantity_cost_and_opaque_version_contracts()
    {
        var schema = new LinqToDbMappingSchemaProvider(new ILinqToDbMapping[] { new InventoryLinqToDbMapping() }).GetSchema();
        var header = schema.GetEntityDescriptor(typeof(SalesReceipt));
        Assert.Equal("dbo", header.SchemaName);
        Assert.Equal("SalesReceipts", header.TableName);
        var id = header.Columns.Single(c => c.MemberName == nameof(SalesReceipt.SaleId));
        Assert.True(id.IsPrimaryKey && id.IsIdentity);
        Assert.Equal("SaleNo", header.Columns.Single(c => c.MemberName == nameof(SalesReceipt.SaleNumber)).ColumnName);
        var version = header.Columns.Single(c => c.MemberName == nameof(SalesReceipt.Version));
        Assert.Equal("RowVersion", version.ColumnName);
        Assert.True(version.SkipOnInsert && version.SkipOnUpdate);
        Assert.Equal(DataType.Timestamp, version.DataType);
        var line = schema.GetEntityDescriptor(typeof(SalesReceiptLine));
        Assert.Equal("SalesReceiptLines", line.TableName);
        Assert.Equal(18, line.Columns.Single(c => c.MemberName == nameof(SalesReceiptLine.Qty)).Precision);
        Assert.Equal(4, line.Columns.Single(c => c.MemberName == nameof(SalesReceiptLine.Qty)).Scale);
        var cost = line.Columns.Single(c => c.MemberName == nameof(SalesReceiptLine.PostedUnitCost));
        Assert.Equal(28, cost.Precision);
        Assert.Equal(8, cost.Scale);
        Assert.True(cost.CanBeNull);
        Assert.Equal(28, line.Columns.Single(c => c.MemberName == nameof(SalesReceiptLine.CostAmount)).Precision);
        Assert.Equal(4, line.Columns.Single(c => c.MemberName == nameof(SalesReceiptLine.CostAmount)).Scale);
        var valuation = schema.GetEntityDescriptor(typeof(StockValuationEntry));
        Assert.Equal("StockValuationEntries", valuation.TableName);
        var ledgerId = valuation.Columns.Single(c => c.MemberName == nameof(StockValuationEntry.LedgerId));
        Assert.True(ledgerId.IsPrimaryKey);
        Assert.False(ledgerId.IsIdentity);
        Assert.Equal(28, valuation.Columns.Single(c => c.MemberName == nameof(StockValuationEntry.ValueChange)).Precision);
        Assert.Equal(4, valuation.Columns.Single(c => c.MemberName == nameof(StockValuationEntry.ValueChange)).Scale);
        var balance = schema.GetEntityDescriptor(typeof(StockBalance));
        Assert.Equal("StockBalances", balance.TableName);
        Assert.Equal(2, balance.Columns.Count(c => c.IsPrimaryKey));
        Assert.Equal(28, balance.Columns.Single(c => c.MemberName == nameof(StockBalance.InventoryValue)).Precision);
        Assert.False(balance.Columns.Single(c => c.MemberName == nameof(StockBalance.InventoryValue)).CanBeNull);
        var balanceVersion = balance.Columns.Single(c => c.MemberName == nameof(StockBalance.Version));
        Assert.True(balanceVersion.SkipOnInsert && balanceVersion.SkipOnUpdate);
        Assert.Equal("RowVersion", balanceVersion.ColumnName);
    }
}
