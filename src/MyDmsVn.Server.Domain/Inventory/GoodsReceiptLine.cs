using System;

namespace MyDmsVn.Server.Domain.Inventory;

public sealed class GoodsReceiptLine
{
    public long Id { get; set; }

    public long ReceiptId { get; set; }

    public int LineNumber { get; set; }

    public int ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public int? UpdatedByUserId { get; set; }
}
