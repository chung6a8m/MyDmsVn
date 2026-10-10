using System.Collections.Generic;
using MyDmsVn.Server.Domain.Inventory;
using RepoDb;

namespace MyDmsVn.Server.Infrastructure.Inventory;

internal static class InventoryRepoDbWriteFields
{
    // SQL Server generates RowVersion. It must never appear in a RepoDb write field list;
    // UpdateDraft/Post compare the expected token separately in their SQL WHERE predicate.
    internal static readonly IEnumerable<Field> GoodsReceiptCreate = Field.Parse<GoodsReceipt>(entity => new
    {
        entity.ReceiptNumber,
        entity.ReceiptDate,
        entity.WarehouseId,
        entity.EmployeeId,
        entity.Status,
        entity.Note,
        entity.CreatedByUserId,
    });

    internal static readonly IEnumerable<Field> GoodsReceiptDraftUpdate = Field.Parse<GoodsReceipt>(entity => new
    {
        entity.ReceiptDate,
        entity.WarehouseId,
        entity.EmployeeId,
        entity.Note,
        entity.UpdatedAtUtc,
        entity.UpdatedByUserId,
    });

    internal static readonly IEnumerable<Field> GoodsReceiptLineCreate = Field.Parse<GoodsReceiptLine>(entity => new
    {
        entity.ReceiptId,
        entity.LineNumber,
        entity.ProductId,
        entity.Quantity,
        entity.UnitCost,
        entity.CreatedByUserId,
    });
}
