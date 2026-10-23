using System.Collections.Generic;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application.Sales;

namespace MyDmsVn.Desktop.Infrastructure.Local;

internal sealed class LocalSalesReceiptPreviewCalculator : ISalesReceiptPreviewCalculator
{
    public decimal CalculateRevenue(decimal qty, decimal unitPrice, bool isGift) => SalesReceiptPreview.CalculateRevenue(qty, unitPrice, isGift);
    public decimal SumAmounts(IEnumerable<decimal> amounts) => SalesReceiptPreview.SumAmounts(amounts);
}
