using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace MyDmsVn.Server.Domain.Inventory;

public sealed class InventoryIssueLine
{
    public InventoryIssueLine(int lineNo, decimal qty) { LineNo = lineNo; Qty = qty; }
    public int LineNo { get; }
    public decimal Qty { get; }
}

public sealed class InventoryIssueCost
{
    internal InventoryIssueCost(decimal quantity, decimal amount, decimal unitCost, IDictionary<int, decimal> lines)
    {
        IssueQuantity = quantity; CostAmount = amount; PostedUnitCost = unitCost;
        LineCostAmounts = new ReadOnlyDictionary<int, decimal>(new Dictionary<int, decimal>(lines));
    }
    public decimal IssueQuantity { get; }
    public decimal CostAmount { get; }
    public decimal PostedUnitCost { get; }
    public IReadOnlyDictionary<int, decimal> LineCostAmounts { get; }
}

public static class InventoryValuationMath
{
    public const decimal MaximumQuantity = 99999999999999.9999m;
    public const decimal MaximumUnitPrice = 999999999999999.9999m;
    public const decimal MaximumAmount = 999999999999999999999999.9999m;
    public const decimal MaximumUnitCost = 99999999999999999999.99999999m;

    public static decimal CalculateRevenue(decimal qty, decimal unitPrice, bool isGift)
    {
        var q = Input(qty, MaximumQuantity, true); var p = Input(unitPrice, MaximumUnitPrice, false);
        return isGift ? 0m : Amount(Round(q * p, 10000));
    }

    public static decimal CalculateReceiptValue(decimal qty, decimal unitCost) => CalculateRevenue(qty, unitCost, false);

    public static InventoryIssueCost CalculateIssue(decimal quantityOnHand, decimal inventoryValue, IReadOnlyList<InventoryIssueLine> lines)
    {
        var q = Input(quantityOnHand, MaximumQuantity, true);
        var v = Input(inventoryValue, MaximumAmount, false);
        if (lines == null || lines.Count == 0 || lines.Count > 200) throw new ArgumentException("Require 1–200 lines.", nameof(lines));
        var ordered = lines.OrderBy(line => line?.LineNo ?? 0).ToArray();
        var quantities = new Dictionary<int, BigInteger>();
        foreach (var line in ordered)
        {
            if (line == null || line.LineNo <= 0 || quantities.ContainsKey(line.LineNo)) throw new ArgumentException("Unique positive LineNo required.", nameof(lines));
            quantities.Add(line.LineNo, Input(line.Qty, MaximumQuantity, true));
        }
        var sum = quantities.Values.Aggregate(BigInteger.Zero, (a, b) => a + b);
        var issueQty = Quantity(sum);
        if (sum > q) throw new ArgumentException("Insufficient stock.", nameof(lines));
        var cost = sum == q ? v : Round(v * sum, q);
        var snapshot = Convert(Round(v * BigInteger.Pow(10, 8), q), 8, MaximumUnitCost);
        var allocations = new Dictionary<int, decimal>();
        BigInteger cumulative = 0, previous = 0;
        foreach (var line in ordered)
        {
            cumulative += quantities[line.LineNo];
            var allocated = cumulative == sum ? cost : Round(cost * cumulative, sum);
            allocations.Add(line.LineNo, Amount(allocated - previous));
            previous = allocated;
        }
        return new InventoryIssueCost(issueQty, Amount(cost), snapshot, allocations);
    }

    public static decimal SumAmounts(IEnumerable<decimal> amounts) => Amount(Sum(amounts, MaximumAmount));
    public static decimal SumQuantities(IEnumerable<decimal> quantities) => Quantity(Sum(quantities, MaximumQuantity));

    private static BigInteger Sum(IEnumerable<decimal> values, decimal maximum)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        return values.Aggregate(BigInteger.Zero, (sum, value) => sum + Input(value, maximum, false));
    }

    private static BigInteger Input(decimal value, decimal maximum, bool positive)
    {
        if (value < 0 || (positive && value == 0) || value > maximum) throw new ArgumentOutOfRangeException(nameof(value));
        var bits = decimal.GetBits(value);
        var scale = (bits[3] >> 16) & 255;
        var coefficient = (BigInteger)(uint)bits[0] + ((BigInteger)(uint)bits[1] << 32) + ((BigInteger)(uint)bits[2] << 64);
        if (scale <= 4) return coefficient * BigInteger.Pow(10, 4 - scale);
        var divisor = BigInteger.Pow(10, scale - 4);
        if (coefficient % divisor != 0) throw new ArgumentException("At most four fractional places.", nameof(value));
        return coefficient / divisor;
    }

    private static BigInteger Round(BigInteger n, BigInteger d)
    {
        var result = BigInteger.DivRem(n, d, out var remainder);
        return result + (remainder * 2 >= d ? BigInteger.One : BigInteger.Zero);
    }
    private static decimal Amount(BigInteger value) => Convert(value, 4, MaximumAmount);
    private static decimal Quantity(BigInteger value) => Convert(value, 4, MaximumQuantity);
    private static decimal Convert(BigInteger value, int scale, decimal maximum)
    {
        var limit = new BigInteger(maximum * (scale == 4 ? 10000m : 100000000m));
        if (value < 0 || value > limit) throw new OverflowException("Fixed-point result exceeds storage precision.");
        return new decimal((int)(uint)(value & uint.MaxValue), (int)(uint)((value >> 32) & uint.MaxValue), (int)(uint)((value >> 64) & uint.MaxValue), false, (byte)scale);
    }
}
