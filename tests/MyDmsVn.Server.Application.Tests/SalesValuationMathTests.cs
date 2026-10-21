using System;
using System.Linq;
using MyDmsVn.Server.Domain.Inventory;
using Xunit;
namespace MyDmsVn.Server.Application.Tests;
public sealed class SalesValuationMathTests
{
    [Fact]
    public void Receipts_sale_gift_and_later_receipt_conserve_value()
    {
        var value = InventoryValuationMath.CalculateReceiptValue(10, 10) + InventoryValuationMath.CalculateReceiptValue(10, 20);
        Assert.Equal(300m, value);
        var issue = InventoryValuationMath.CalculateIssue(20, value, new[] { new InventoryIssueLine(1, 3), new InventoryIssueLine(2, 2) });
        Assert.Equal(75m, issue.CostAmount); Assert.Equal(45m, issue.LineCostAmounts[1]); Assert.Equal(30m, issue.LineCostAmounts[2]);
        Assert.Equal(75m, InventoryValuationMath.CalculateRevenue(3, 25, false)); Assert.Equal(0m, InventoryValuationMath.CalculateRevenue(2, 25, true));
        Assert.Equal(375m, value - issue.CostAmount + InventoryValuationMath.CalculateReceiptValue(5, 30)); Assert.Equal(75m, issue.CostAmount);
    }
    [Fact]
    public void Cumulative_rounding_and_full_depletion_preserve_tiny_residual()
    {
        var issue = InventoryValuationMath.CalculateIssue(3, .0002m, new[] { new InventoryIssueLine(2, 1), new InventoryIssueLine(1, 1) });
        Assert.Equal(.0001m, issue.CostAmount); Assert.Equal(.0001m, issue.LineCostAmounts[1]); Assert.Equal(0m, issue.LineCostAmounts[2]); Assert.Equal(.00006667m, issue.PostedUnitCost);
        Assert.Equal(.0001m, InventoryValuationMath.CalculateIssue(1, .0001m, new[] { new InventoryIssueLine(1, 1) }).CostAmount);
    }
    [Theory]
    [InlineData("0.0001", "0.5", "0.0001")]
    [InlineData("1", "0", "0")]
    [InlineData("1.23000", "2", "2.46")]
    public void Exact_products_round_midpoints_away(string q, string p, string expected)
    { var c = System.Globalization.CultureInfo.InvariantCulture; Assert.Equal(decimal.Parse(expected, c), InventoryValuationMath.CalculateReceiptValue(decimal.Parse(q, c), decimal.Parse(p, c))); }
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.00001")]
    [InlineData("100000000000000")]
    public void Invalid_quantities_rejected(string quantity) => Assert.ThrowsAny<ArgumentException>(() => InventoryValuationMath.CalculateRevenue(decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture), 1, true));
    [Fact]
    public void Bounds_and_wide_intermediates_are_checked()
    {
        Assert.Equal(InventoryValuationMath.MaximumAmount, InventoryValuationMath.CalculateIssue(100000, InventoryValuationMath.MaximumAmount, new[] { new InventoryIssueLine(1, 100000) }).CostAmount);
        Assert.Throws<OverflowException>(() => InventoryValuationMath.CalculateReceiptValue(InventoryValuationMath.MaximumQuantity, InventoryValuationMath.MaximumUnitPrice));
        Assert.Throws<OverflowException>(() => InventoryValuationMath.SumAmounts(new[] { InventoryValuationMath.MaximumAmount, .0001m }));
        Assert.Throws<OverflowException>(() => InventoryValuationMath.SumQuantities(new[] { InventoryValuationMath.MaximumQuantity, .0001m }));
        Assert.Throws<OverflowException>(() => InventoryValuationMath.CalculateIssue(.0001m, InventoryValuationMath.MaximumAmount, new[] { new InventoryIssueLine(1, .0001m) }));
        Assert.ThrowsAny<ArgumentException>(() => InventoryValuationMath.CalculateRevenue(1, -1, true));
        Assert.ThrowsAny<ArgumentException>(() => InventoryValuationMath.CalculateRevenue(1, .00001m, true));
        Assert.ThrowsAny<ArgumentException>(() => InventoryValuationMath.CalculateRevenue(1, 1000000000000000m, true));
    }
    [Fact]
    public void Line_limit_identity_and_combined_availability_are_enforced()
    {
        var lines = Enumerable.Range(1, 200).Select(i => new InventoryIssueLine(i, 1)).ToArray();
        Assert.Equal(200m, InventoryValuationMath.CalculateIssue(200, 0, lines).IssueQuantity);
        Assert.ThrowsAny<ArgumentException>(() => InventoryValuationMath.CalculateIssue(201, 0, lines.Concat(new[] { new InventoryIssueLine(201, 1) }).ToArray()));
        Assert.ThrowsAny<ArgumentException>(() => InventoryValuationMath.CalculateIssue(1, 0, new[] { new InventoryIssueLine(1, 1), new InventoryIssueLine(2, 1) }));
        Assert.ThrowsAny<ArgumentException>(() => InventoryValuationMath.CalculateIssue(3, 0, new[] { new InventoryIssueLine(1, 1), new InventoryIssueLine(1, 1) }));
    }
}
