using System.Data;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using RepoDb;

namespace MyDmsVn.Server.Infrastructure.Inventory;

internal sealed class InventoryRepoDbMapping : IRepoDbMapping
{
    public void Configure()
    {
        ConfigureGoodsReceipt();
        ConfigureGoodsReceiptLine();
    }

    private static void ConfigureGoodsReceipt()
    {
        ClassMapper.Add<GoodsReceipt>("dbo.GoodsReceipts");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.Id, "ReceiptId");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.ReceiptNumber, "ReceiptNo");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.ReceiptDate, "ReceiptDate");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.WarehouseId, "WarehouseId");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.EmployeeId, "EmployeeId");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.Status, "Status");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.Note!, "Note");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.CreatedAtUtc, "CreatedAtUtc");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.CreatedByUserId, "CreatedByUserId");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.UpdatedAtUtc!, "UpdatedAtUtc");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.UpdatedByUserId!, "UpdatedByUserId");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.PostedAtUtc!, "PostedAtUtc");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.PostedByUserId!, "PostedByUserId");
        PropertyMapper.Add<GoodsReceipt>(entity => entity.Version, "RowVersion");
        TypeMapper.Add<GoodsReceipt>(entity => entity.Id, DbType.Int64);
        TypeMapper.Add<GoodsReceipt>(entity => entity.ReceiptNumber, DbType.String);
        TypeMapper.Add<GoodsReceipt>(entity => entity.ReceiptDate, DbType.Date);
        TypeMapper.Add<GoodsReceipt>(entity => entity.WarehouseId, DbType.Int32);
        TypeMapper.Add<GoodsReceipt>(entity => entity.EmployeeId, DbType.Int32);
        TypeMapper.Add<GoodsReceipt>(entity => entity.Status, DbType.String);
        TypeMapper.Add<GoodsReceipt>(entity => entity.Note!, DbType.String);
        TypeMapper.Add<GoodsReceipt>(entity => entity.CreatedAtUtc, DbType.DateTime2);
        TypeMapper.Add<GoodsReceipt>(entity => entity.CreatedByUserId, DbType.Int32);
        TypeMapper.Add<GoodsReceipt>(entity => entity.UpdatedAtUtc!, DbType.DateTime2);
        TypeMapper.Add<GoodsReceipt>(entity => entity.UpdatedByUserId!, DbType.Int32);
        TypeMapper.Add<GoodsReceipt>(entity => entity.PostedAtUtc!, DbType.DateTime2);
        TypeMapper.Add<GoodsReceipt>(entity => entity.PostedByUserId!, DbType.Int32);
        TypeMapper.Add<GoodsReceipt>(entity => entity.Version, DbType.Binary);
        PrimaryMapper.Add<GoodsReceipt>(entity => entity.Id);
        IdentityMapper.Add<GoodsReceipt>(entity => entity.Id);
    }

    private static void ConfigureGoodsReceiptLine()
    {
        ClassMapper.Add<GoodsReceiptLine>("dbo.GoodsReceiptLines");
        PropertyMapper.Add<GoodsReceiptLine>(entity => entity.Id, "ReceiptLineId");
        PropertyMapper.Add<GoodsReceiptLine>(entity => entity.ReceiptId, "ReceiptId");
        PropertyMapper.Add<GoodsReceiptLine>(entity => entity.LineNumber, "LineNo");
        PropertyMapper.Add<GoodsReceiptLine>(entity => entity.ProductId, "ProductId");
        PropertyMapper.Add<GoodsReceiptLine>(entity => entity.Quantity, "Quantity");
        PropertyMapper.Add<GoodsReceiptLine>(entity => entity.UnitCost, "UnitCost");
        PropertyMapper.Add<GoodsReceiptLine>(entity => entity.CreatedAtUtc, "CreatedAtUtc");
        PropertyMapper.Add<GoodsReceiptLine>(entity => entity.CreatedByUserId, "CreatedByUserId");
        PropertyMapper.Add<GoodsReceiptLine>(entity => entity.UpdatedAtUtc!, "UpdatedAtUtc");
        PropertyMapper.Add<GoodsReceiptLine>(entity => entity.UpdatedByUserId!, "UpdatedByUserId");
        TypeMapper.Add<GoodsReceiptLine>(entity => entity.Id, DbType.Int64);
        TypeMapper.Add<GoodsReceiptLine>(entity => entity.ReceiptId, DbType.Int64);
        TypeMapper.Add<GoodsReceiptLine>(entity => entity.LineNumber, DbType.Int32);
        TypeMapper.Add<GoodsReceiptLine>(entity => entity.ProductId, DbType.Int32);
        TypeMapper.Add<GoodsReceiptLine>(entity => entity.Quantity, DbType.Decimal);
        TypeMapper.Add<GoodsReceiptLine>(entity => entity.UnitCost, DbType.Decimal);
        TypeMapper.Add<GoodsReceiptLine>(entity => entity.CreatedAtUtc, DbType.DateTime2);
        TypeMapper.Add<GoodsReceiptLine>(entity => entity.CreatedByUserId, DbType.Int32);
        TypeMapper.Add<GoodsReceiptLine>(entity => entity.UpdatedAtUtc!, DbType.DateTime2);
        TypeMapper.Add<GoodsReceiptLine>(entity => entity.UpdatedByUserId!, DbType.Int32);
        PrimaryMapper.Add<GoodsReceiptLine>(entity => entity.Id);
        IdentityMapper.Add<GoodsReceiptLine>(entity => entity.Id);
    }
}
