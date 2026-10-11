using System;

namespace MyDmsVn.Server.Domain.Inventory;

public sealed class StockLedgerEntry
{
    public long Id { get; set; }

    public int WarehouseId { get; set; }

    public int ProductId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public long DocumentLineId { get; set; }

    public decimal QuantityChange { get; set; }

    public DateTime PostedAtUtc { get; set; }

    public int PostedByUserId { get; set; }
}
