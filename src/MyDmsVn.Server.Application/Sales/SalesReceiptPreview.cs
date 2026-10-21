using System.Collections.Generic;
using MyDmsVn.Server.Domain.Inventory;

namespace MyDmsVn.Server.Application.Sales;

/// <summary>Provider-free preview facade; the domain owns all arithmetic.</summary>
public static class SalesReceiptPreview
{
    public static decimal CalculateRevenue(decimal qty, decimal unitPrice, bool isGift) => InventoryValuationMath.CalculateRevenue(qty, unitPrice, isGift);
    public static decimal SumAmounts(IEnumerable<decimal> amounts) => InventoryValuationMath.SumAmounts(amounts);
}
