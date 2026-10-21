namespace MyDmsVn.Server.Domain.Inventory;
public sealed class StockValuationEntry
{
    public long LedgerId { get; set; }
    public decimal ValueChange { get; set; }
}
