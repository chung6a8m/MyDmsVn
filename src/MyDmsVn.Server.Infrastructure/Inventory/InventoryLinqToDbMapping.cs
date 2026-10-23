using LinqToDB;
using LinqToDB.Mapping;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Domain.Sales;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Inventory;

internal sealed class InventoryLinqToDbMapping : ILinqToDbMapping
{
    public void Configure(FluentMappingBuilder builder)
    {
        ConfigureSales(builder);
        ConfigureValuation(builder);
        var header = builder.Entity<GoodsReceipt>().HasSchemaName("dbo").HasTableName("GoodsReceipts");
        header.Property(e => e.Id).HasColumnName("ReceiptId").HasDataType(DataType.Int64).IsPrimaryKey().IsIdentity().IsNotNull();
        header.Property(e => e.ReceiptNumber).HasColumnName("ReceiptNo").HasDataType(DataType.NVarChar).HasLength(32).IsNotNull();
        header.Property(e => e.ReceiptDate).HasDataType(DataType.Date).IsNotNull();
        header.Property(e => e.WarehouseId).HasDataType(DataType.Int32).IsNotNull();
        header.Property(e => e.EmployeeId).HasDataType(DataType.Int32).IsNotNull();
        header.Property(e => e.Status).HasDataType(DataType.VarChar).HasLength(16).IsNotNull();
        header.Property(e => e.Note).HasDataType(DataType.NVarChar).HasLength(1000).IsNullable(true);
        header.Property(e => e.CreatedAtUtc).HasDataType(DataType.DateTime2).IsNotNull();
        header.Property(e => e.CreatedByUserId).HasDataType(DataType.Int32).IsNotNull();
        header.Property(e => e.UpdatedAtUtc).HasDataType(DataType.DateTime2).IsNullable(true);
        header.Property(e => e.UpdatedByUserId).HasDataType(DataType.Int32).IsNullable(true);
        header.Property(e => e.PostedAtUtc).HasDataType(DataType.DateTime2).IsNullable(true);
        header.Property(e => e.PostedByUserId).HasDataType(DataType.Int32).IsNullable(true);
        header.Property(e => e.Version).HasColumnName("RowVersion").HasDataType(DataType.Timestamp).HasLength(8).HasSkipOnInsert().HasSkipOnUpdate().IsNotNull();

        var line = builder.Entity<GoodsReceiptLine>().HasSchemaName("dbo").HasTableName("GoodsReceiptLines");
        line.Property(e => e.Id).HasColumnName("ReceiptLineId").HasDataType(DataType.Int64).IsPrimaryKey().IsIdentity().IsNotNull();
        line.Property(e => e.ReceiptId).HasDataType(DataType.Int64).IsNotNull();
        line.Property(e => e.LineNumber).HasColumnName("LineNo").HasDataType(DataType.Int32).IsNotNull();
        line.Property(e => e.ProductId).HasDataType(DataType.Int32).IsNotNull();
        line.Property(e => e.Quantity).HasDataType(DataType.Decimal).HasPrecision(18).HasScale(4).IsNotNull();
        line.Property(e => e.UnitCost).HasDataType(DataType.Decimal).HasPrecision(19).HasScale(4).IsNotNull();
        line.Property(e => e.CreatedAtUtc).HasDataType(DataType.DateTime2).IsNotNull();
        line.Property(e => e.CreatedByUserId).HasDataType(DataType.Int32).IsNotNull();
        line.Property(e => e.UpdatedAtUtc).HasDataType(DataType.DateTime2).IsNullable(true);
        line.Property(e => e.UpdatedByUserId).HasDataType(DataType.Int32).IsNullable(true);

        var entry = builder.Entity<StockLedgerEntry>().HasSchemaName("dbo").HasTableName("StockLedger");
        entry.Property(e => e.Id).HasColumnName("LedgerId").HasDataType(DataType.Int64).IsPrimaryKey().IsIdentity().IsNotNull();
        entry.Property(e => e.WarehouseId).HasDataType(DataType.Int32).IsNotNull();
        entry.Property(e => e.ProductId).HasDataType(DataType.Int32).IsNotNull();
        entry.Property(e => e.DocumentType).HasDataType(DataType.VarChar).HasLength(32).IsNotNull();
        entry.Property(e => e.DocumentId).HasDataType(DataType.Int64).IsNotNull();
        entry.Property(e => e.DocumentLineId).HasDataType(DataType.Int64).IsNotNull();
        entry.Property(e => e.QuantityChange).HasDataType(DataType.Decimal).HasPrecision(18).HasScale(4).IsNotNull();
        entry.Property(e => e.PostedAtUtc).HasDataType(DataType.DateTime2).IsNotNull();
        entry.Property(e => e.PostedByUserId).HasDataType(DataType.Int32).IsNotNull();

    }
    private static void ConfigureValuation(FluentMappingBuilder builder)
    {
        var entry = builder.Entity<StockValuationEntry>().HasSchemaName("dbo").HasTableName("StockValuationEntries");
        entry.Property(e => e.LedgerId).HasDataType(DataType.Int64).IsPrimaryKey().IsNotNull();
        entry.Property(e => e.ValueChange).HasDataType(DataType.Decimal).HasPrecision(28).HasScale(4).IsNotNull();
        var balance = builder.Entity<StockBalance>().HasSchemaName("dbo").HasTableName("StockBalances");
        balance.Property(e => e.WarehouseId).HasDataType(DataType.Int32).IsPrimaryKey(1).IsNotNull();
        balance.Property(e => e.ProductId).HasDataType(DataType.Int32).IsPrimaryKey(2).IsNotNull();
        balance.Property(e => e.Quantity).HasDataType(DataType.Decimal).HasPrecision(18).HasScale(4).IsNotNull();
        balance.Property(e => e.InventoryValue).HasDataType(DataType.Decimal).HasPrecision(28).HasScale(4).IsNotNull();
        balance.Property(e => e.Version).HasColumnName("RowVersion").HasDataType(DataType.Timestamp).HasLength(8).HasSkipOnInsert().HasSkipOnUpdate().IsNotNull();
    }

    private static void ConfigureSales(FluentMappingBuilder builder)
    {
        var header = builder.Entity<SalesReceipt>().HasSchemaName("dbo").HasTableName("SalesReceipts");
        header.Property(e => e.SaleId).HasDataType(DataType.Int64).IsPrimaryKey().IsIdentity().IsNotNull();
        header.Property(e => e.SaleNumber).HasColumnName("SaleNo").HasDataType(DataType.NVarChar).HasLength(32).IsNotNull();
        header.Property(e => e.SaleDate).HasDataType(DataType.Date).IsNotNull();
        header.Property(e => e.CustomerId).HasDataType(DataType.Int32).IsNotNull();
        header.Property(e => e.WarehouseId).HasDataType(DataType.Int32).IsNotNull();
        header.Property(e => e.EmployeeId).HasDataType(DataType.Int32).IsNotNull();
        header.Property(e => e.Status).HasDataType(DataType.VarChar).HasLength(16).IsNotNull();
        header.Property(e => e.Note).HasDataType(DataType.NVarChar).HasLength(1000).IsNullable(true);
        header.Property(e => e.CreatedAtUtc).HasDataType(DataType.DateTime2).IsNotNull();
        header.Property(e => e.CreatedByUserId).HasDataType(DataType.Int32).IsNotNull();
        header.Property(e => e.UpdatedAtUtc).HasDataType(DataType.DateTime2).IsNullable(true);
        header.Property(e => e.UpdatedByUserId).HasDataType(DataType.Int32).IsNullable(true);
        header.Property(e => e.PostedAtUtc).HasDataType(DataType.DateTime2).IsNullable(true);
        header.Property(e => e.PostedByUserId).HasDataType(DataType.Int32).IsNullable(true);
        header.Property(e => e.Version).HasColumnName("RowVersion").HasDataType(DataType.Timestamp).HasLength(8).HasSkipOnInsert().HasSkipOnUpdate().IsNotNull();
        var line = builder.Entity<SalesReceiptLine>().HasSchemaName("dbo").HasTableName("SalesReceiptLines");
        line.Property(e => e.SaleLineId).HasDataType(DataType.Int64).IsPrimaryKey().IsIdentity().IsNotNull();
        line.Property(e => e.SaleId).HasDataType(DataType.Int64).IsNotNull();
        line.Property(e => e.LineNo).HasDataType(DataType.Int32).IsNotNull();
        line.Property(e => e.ProductId).HasDataType(DataType.Int32).IsNotNull();
        line.Property(e => e.Unit).HasDataType(DataType.NVarChar).HasLength(32).IsNotNull();
        line.Property(e => e.Qty).HasDataType(DataType.Decimal).HasPrecision(18).HasScale(4).IsNotNull();
        line.Property(e => e.UnitPrice).HasDataType(DataType.Decimal).HasPrecision(19).HasScale(4).IsNotNull();
        line.Property(e => e.IsGift).HasDataType(DataType.Boolean).IsNotNull();
        line.Property(e => e.LineAmount).HasDataType(DataType.Decimal).HasPrecision(28).HasScale(4).IsNotNull();
        line.Property(e => e.PostedUnitCost).HasDataType(DataType.Decimal).HasPrecision(28).HasScale(8).IsNullable(true);
        line.Property(e => e.CostAmount).HasDataType(DataType.Decimal).HasPrecision(28).HasScale(4).IsNullable(true);
        line.Property(e => e.CreatedAtUtc).HasDataType(DataType.DateTime2).IsNotNull();
        line.Property(e => e.CreatedByUserId).HasDataType(DataType.Int32).IsNotNull();
        line.Property(e => e.UpdatedAtUtc).HasDataType(DataType.DateTime2).IsNullable(true);
        line.Property(e => e.UpdatedByUserId).HasDataType(DataType.Int32).IsNullable(true);
    }}
