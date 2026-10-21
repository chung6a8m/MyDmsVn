using System;
namespace MyDmsVn.Server.Domain.Inventory;
public sealed class StockBalance
{
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal InventoryValue { get; set; }
    public byte[] Version { get; set; } = Array.Empty<byte>();
}
