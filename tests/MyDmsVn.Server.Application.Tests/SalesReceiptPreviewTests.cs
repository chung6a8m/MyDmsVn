using System;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Domain.Inventory;
using Xunit;

namespace MyDmsVn.Server.Application.Tests;

public sealed class SalesReceiptPreviewTests
{
    [Fact]
    public void Facade_preserves_existing_exact_tie_wide_bounds_gift_validation_and_sum_rules()
    {
        Assert.Equal(.0001m, SalesReceiptPreview.CalculateRevenue(.0001m, .5m, false));
        Assert.Equal(2.46m, SalesReceiptPreview.CalculateRevenue(1.23000m, 2m, false));
        Assert.Equal(0m, SalesReceiptPreview.CalculateRevenue(2m, 25m, true));
        Assert.Equal(InventoryValuationMath.CalculateRevenue(12345678901234.5678m, 1000000000m, false),
            SalesReceiptPreview.CalculateRevenue(12345678901234.5678m, 1000000000m, false));
        Assert.ThrowsAny<ArgumentException>(() => SalesReceiptPreview.CalculateRevenue(1, .00001m, true));
        Assert.Throws<OverflowException>(() => SalesReceiptPreview.CalculateRevenue(InventoryValuationMath.MaximumQuantity, InventoryValuationMath.MaximumUnitPrice, false));
        Assert.Throws<OverflowException>(() => SalesReceiptPreview.SumAmounts(new[] { InventoryValuationMath.MaximumAmount, .0001m }));
        Assert.Equal(8m, SalesReceiptPreview.SumAmounts(new[] { 5m, 3m }));
    }
}
