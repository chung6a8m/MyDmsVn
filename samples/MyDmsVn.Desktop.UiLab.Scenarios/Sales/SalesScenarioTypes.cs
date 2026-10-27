using System;
using System.Collections.Generic;
using System.Linq;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.UiLab.Scenarios;

public enum SalesScenarioId
{
    S01Draft,
    S02Validation,
    S03LookupRace,
    S04DateCaret,
    S05DirtyClose,
    S06UnknownPost,
}

public sealed class ScenarioPreviewCalculator : ISalesReceiptPreviewCalculator
{
    public decimal CalculateRevenue(decimal qty, decimal unitPrice, bool isGift)
    {
        if (qty < 0 || qty > 10000 || unitPrice < 0 || unitPrice > 1000000)
            throw new ArgumentOutOfRangeException(nameof(qty), "Outside the UI Lab demo range.");
        return isGift ? 0m : decimal.Round(qty * unitPrice, 4, MidpointRounding.AwayFromZero);
    }

    public decimal SumAmounts(IEnumerable<decimal> amounts) => amounts.Aggregate(0m, (sum, amount) => checked(sum + amount));
}
