using System;

namespace MyDmsVn.Server.Domain.Sales;

public sealed class SalesReceipt
{
    public long SaleId { get; set; }

    public string SaleNumber { get; set; } = string.Empty;

    public DateTime SaleDate { get; set; }

    public int CustomerId { get; set; }

    public int WarehouseId { get; set; }

    public int EmployeeId { get; set; }

    public string Status { get; set; } = SalesReceiptStatuses.Draft;

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public int? UpdatedByUserId { get; set; }

    public DateTime? PostedAtUtc { get; set; }

    public int? PostedByUserId { get; set; }

    public byte[] Version { get; set; } = Array.Empty<byte>();
}
