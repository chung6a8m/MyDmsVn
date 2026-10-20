using System;
using System.Globalization;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.WinForms;

internal enum ReceiptNumericField { Quantity, UnitCost }

internal static class ReceiptNumericInput
{
    internal static bool TryParse(string text, ReceiptNumericField field, CultureInfo culture, out decimal value, out string? error)
    {
        if (culture == null) throw new ArgumentNullException(nameof(culture));
        value = 0m;
        if (field != ReceiptNumericField.Quantity && field != ReceiptNumericField.UnitCost)
        {
            error = "Select a supported receipt numeric field.";
            return false;
        }

        var quantity = field == ReceiptNumericField.Quantity;
        var label = quantity ? "Quantity" : "Unit cost";
        var maximum = quantity ? GoodsReceiptContractLimits.MaximumQuantity : GoodsReceiptContractLimits.MaximumUnitCost;
        var candidate = text?.Trim() ?? string.Empty;
        var format = culture.NumberFormat;
        var separator = format.NumberDecimalSeparator;
        error = label + " must be a number using '" + separator + "' as the decimal separator, without grouping, currency or exponent notation.";
        if (candidate.Length == 0) return false;

        var index = 0;
        if (!string.IsNullOrEmpty(format.NegativeSign) && candidate.StartsWith(format.NegativeSign, StringComparison.Ordinal))
            index = format.NegativeSign.Length;
        else if (!string.IsNullOrEmpty(format.PositiveSign) && candidate.StartsWith(format.PositiveSign, StringComparison.Ordinal))
            index = format.PositiveSign.Length;

        var integerStart = index;
        while (index < candidate.Length && IsDigit(candidate[index])) index++;
        var digitCount = index - integerStart;
        var fractionalStart = -1;
        if (candidate.Length - index >= separator.Length &&
            string.CompareOrdinal(candidate, index, separator, 0, separator.Length) == 0)
        {
            index += separator.Length;
            fractionalStart = index;
            while (index < candidate.Length && IsDigit(candidate[index])) index++;
            digitCount += index - fractionalStart;
        }
        if (index != candidate.Length || digitCount == 0) return false;

        if (fractionalStart >= 0 && candidate.Length - fractionalStart > GoodsReceiptContractLimits.DecimalPlaces)
        {
            // Inspect every excess digit before conversion: decimal.TryParse can round a
            // meaningful far-tail digit away, even to zero. Only proven zero padding is removed.
            var paddingStart = fractionalStart + GoodsReceiptContractLimits.DecimalPlaces;
            for (var padding = paddingStart; padding < candidate.Length; padding++)
            {
                if (candidate[padding] != '0')
                {
                    error = label + " supports at most " + GoodsReceiptContractLimits.DecimalPlaces + " effective decimal places; remove excess nonzero digits.";
                    return false;
                }
            }
            candidate = candidate.Substring(0, paddingStart);
        }

        if (!decimal.TryParse(candidate, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, culture, out var parsed) ||
            parsed > maximum || (quantity ? parsed <= 0m : parsed < 0m))
        {
            error = label + (quantity ? " must be greater than zero" : " must be zero or greater") +
                " and no greater than " + maximum.ToString(culture) + ".";
            return false;
        }

        value = parsed;
        error = null;
        return true;
    }

    private static bool IsDigit(char value) => value >= '0' && value <= '9';
}
