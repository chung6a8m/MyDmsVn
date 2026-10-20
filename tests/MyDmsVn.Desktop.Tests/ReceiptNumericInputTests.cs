using System;
using System.Globalization;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class ReceiptNumericInputTests
{
    [Theory]
    [InlineData("en-US", false)]
    [InlineData("en-US", true)]
    [InlineData("vi-VN", false)]
    [InlineData("vi-VN", true)]
    public void Extra_fractional_zeros_are_exact_but_meaningful_overprecision_is_rejected(string cultureName, bool cost)
    {
        // A decimal parse followed by Round would lose meaningful far-tail digits.
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var separator = culture.NumberFormat.NumberDecimalSeparator;
        var field = cost ? ReceiptNumericField.UnitCost : ReceiptNumericField.Quantity;
        Accept("1" + separator + "23000", field, culture, 1.23m);
        Accept("1" + separator + "2345" + new string('0', 80), field, culture, 1.2345m);
        Accept("00000000000000000000000000000000000000001" + separator + "23000", field, culture, 1.23m);
        Reject("1" + separator + "23001", field, culture);
        Reject("1" + separator + "2300" + new string('0', 80) + "1", field, culture);
        Reject("0" + separator + "0000000000000000000000000000000000000001", field, culture);
    }

    [Theory]
    [InlineData("en-US", false)]
    [InlineData("en-US", true)]
    [InlineData("vi-VN", false)]
    [InlineData("vi-VN", true)]
    public void Culture_and_paste_preserve_the_same_decimal_value(string cultureName, bool cost)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var field = cost ? ReceiptNumericField.UnitCost : ReceiptNumericField.Quantity;
        var separator = culture.NumberFormat.NumberDecimalSeparator;
        var otherSeparator = separator == "." ? "," : ".";
        Accept("1" + separator + "23000", field, culture, 1.23m);
        Accept(" \t+1234" + separator + "5000\r\n", field, culture, 1234.5m);
        Accept("\u00a01234" + separator + "5\u00a0", field, culture, 1234.5m);
        Accept(separator + "5", field, culture, 0.5m);
        Accept("1" + separator, field, culture, 1m);
        Reject("1" + otherSeparator + "23000", field, culture);
        Reject("1" + otherSeparator + "234" + separator + "5", field, culture);
        Reject("1 234" + separator + "5", field, culture);
        Reject("1\u00a0234" + separator + "5", field, culture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Field_bounds_and_zero_rules_match_receipt_contracts(bool cost)
    {
        var field = cost ? ReceiptNumericField.UnitCost : ReceiptNumericField.Quantity;
        var maximum = cost ? GoodsReceiptContractLimits.MaximumUnitCost : GoodsReceiptContractLimits.MaximumQuantity;
        foreach (var cultureName in new[] { "en-US", "vi-VN" })
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            Accept(maximum.ToString(culture), field, culture, maximum);
            Accept(maximum.ToString(culture) + new string('0', 80), field, culture, maximum);
            Reject((maximum + 0.0001m).ToString(culture), field, culture);
            Reject((maximum + 1m).ToString(culture), field, culture);
            Reject(decimal.MaxValue.ToString(culture), field, culture);
            Reject(new string('9', 80), field, culture);
            Reject("-1", field, culture);
            Reject("", field, culture);
            Reject(" \t\r\n", field, culture);
            if (cost)
            {
                Accept("0", field, culture, 0m);
                Accept("-0", field, culture, 0m);
                Accept("0" + culture.NumberFormat.NumberDecimalSeparator + new string('0', 80), field, culture, 0m);
            }
            else
            {
                Reject("0", field, culture);
                Reject("-0", field, culture);
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData(".")]
    [InlineData(",")]
    [InlineData("1e2")]
    [InlineData("1E-2")]
    [InlineData("$1")]
    [InlineData("1đ")]
    [InlineData("(1)")]
    [InlineData("1-")]
    [InlineData("++1")]
    [InlineData("+-1")]
    [InlineData("+ 1")]
    [InlineData("1\t2")]
    [InlineData("1\n2")]
    [InlineData("1.2.3")]
    [InlineData("1,2,3")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("١٢")]
    [InlineData("１２")]
    [InlineData("1\0")]
    [InlineData("1.23\0")]
    public void Malformed_candidates_are_rejected_with_a_field_error(string? text)
    {
        foreach (var culture in new[] { CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("vi-VN") })
        {
            Reject(text, ReceiptNumericField.Quantity, culture);
            Reject(text, ReceiptNumericField.UnitCost, culture);
        }
    }

    [Fact]
    public void Explicit_culture_controls_conversion_even_when_the_thread_culture_differs()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            Accept("1.23000", ReceiptNumericField.Quantity, CultureInfo.GetCultureInfo("en-US"), 1.23m);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Accept("1,23000", ReceiptNumericField.Quantity, CultureInfo.GetCultureInfo("vi-VN"), 1.23m);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Culture_signs_and_multicharacter_separator_are_used_literally()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.NumberFormat.PositiveSign = "plus";
        culture.NumberFormat.NegativeSign = "minus";
        culture.NumberFormat.NumberDecimalSeparator = "::";
        Accept("plus1::23000", ReceiptNumericField.Quantity, culture, 1.23m);
        Reject("minus1::23", ReceiptNumericField.UnitCost, culture);
        Reject("+1::23", ReceiptNumericField.Quantity, culture);
        Reject("1:23", ReceiptNumericField.Quantity, culture);
    }

    private static void Accept(string text, ReceiptNumericField field, CultureInfo culture, decimal expected)
    {
        Assert.True(ReceiptNumericInput.TryParse(text, field, culture, out var value, out var error), error);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    private static void Reject(string? text, ReceiptNumericField field, CultureInfo culture)
    {
        Assert.False(ReceiptNumericInput.TryParse(text!, field, culture, out var value, out var error));
        Assert.Equal(0m, value); // An error cannot accidentally supply a rounded/clamped candidate.
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Contains(field == ReceiptNumericField.Quantity ? "Quantity" : "Unit cost", error!);
    }
}
