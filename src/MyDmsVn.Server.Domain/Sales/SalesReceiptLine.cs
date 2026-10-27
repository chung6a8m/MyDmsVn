using System;
namespace MyDmsVn.Server.Domain.Sales;
public sealed class SalesReceiptLine
{
    public long SaleLineId { get; set; }
    public int LineNo { get; set; }
    public int ProductId { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsGift { get; set; }
    public decimal LineAmount { get; set; }
    public decimal? PostedUnitCost { get; set; }
    public decimal? CostAmount { get; set; }
    public long SaleId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
}
