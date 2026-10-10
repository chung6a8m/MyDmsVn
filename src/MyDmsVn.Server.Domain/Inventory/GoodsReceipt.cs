using System;

namespace MyDmsVn.Server.Domain.Inventory;

public sealed class GoodsReceipt
{
    public long Id { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; }

    public int WarehouseId { get; set; }

    public int EmployeeId { get; set; }

    public string Status { get; set; } = GoodsReceiptStatuses.Draft;

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public int? UpdatedByUserId { get; set; }

    public DateTime? PostedAtUtc { get; set; }

    public int? PostedByUserId { get; set; }

    public byte[] Version { get; set; } = Array.Empty<byte>();
}
