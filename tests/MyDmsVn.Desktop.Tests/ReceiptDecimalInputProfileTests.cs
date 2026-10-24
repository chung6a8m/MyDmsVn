using System;
using System.Globalization;
using MyDmsVn.Bootstrap5WinFormUI.Formatting;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class ReceiptDecimalInputProfileTests
{
    [Theory]
    [InlineData(0, "en-US")]
    [InlineData(0, "vi-VN")]
    [InlineData(1, "en-US")]
    [InlineData(2, "vi-VN")]
    public void Field_profiles_match_business_limits_and_culture(int fieldId, string cultureName)
    {
        var field = (ReceiptNumericField)fieldId;
        var profile = ReceiptDecimalInputProfiles.For(field, CultureInfo.GetCultureInfo(cultureName));

        Assert.False(profile.IsNullable);
        Assert.Equal(GoodsReceiptContractLimits.DecimalPlaces, profile.Scale);
        Assert.Equal(field == ReceiptNumericField.Quantity ? 0.0001m : 0m, profile.Minimum);
        var maximum = field == ReceiptNumericField.Quantity ? GoodsReceiptContractLimits.MaximumQuantity :
            field == ReceiptNumericField.UnitCost ? GoodsReceiptContractLimits.MaximumUnitCost : SalesContractLimits.MaximumUnitPrice;
        Assert.Equal(maximum, profile.Maximum);
        Assert.Equal(cultureName, profile.Culture.Name);
    }

    [Theory]
    [InlineData(0, "0", false)]
    [InlineData(0, "0.0001", true)]
    [InlineData(1, "0", true)]
    [InlineData(2, "999999999999999.9999", true)]
    [InlineData(2, "1000000000000000", false)]
    public void Profile_admission_obeys_exact_bounds(int fieldId, string text, bool expectedValid)
    {
        var field = (ReceiptNumericField)fieldId;
        var profile = ReceiptDecimalInputProfiles.For(field, CultureInfo.GetCultureInfo("en-US"));
        var result = DecimalInputParser.TryParsePending(text, profile);
        Assert.Equal(expectedValid, result.State == InputParseState.Valid);
    }

    [Fact]
    public void Profile_freezes_mutable_culture_and_rejects_unknown_field()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        var profile = ReceiptDecimalInputProfiles.For(ReceiptNumericField.Quantity, culture);
        culture.NumberFormat.NumberDecimalSeparator = ",";
        Assert.Equal(".", profile.Culture.NumberFormat.NumberDecimalSeparator);
        Assert.Equal(InputParseState.Valid, DecimalInputParser.TryParsePending("1.25", profile).State);
        Assert.Throws<ArgumentOutOfRangeException>(() => ReceiptDecimalInputProfiles.For((ReceiptNumericField)42, culture));
        Assert.Throws<ArgumentNullException>(() => ReceiptDecimalInputProfiles.For(ReceiptNumericField.Quantity, null!));
    }
}
