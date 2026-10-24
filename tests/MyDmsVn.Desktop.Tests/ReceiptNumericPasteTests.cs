using System;
using System.Globalization;
using System.Linq;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class ReceiptNumericPasteTests
{
    [Fact]
    public void Planner_accepts_existing_rectangle_without_changing_any_line()
    {
        var first = new object(); var second = new object();
        var snapshot = new NumericDraftSnapshot<object>(Guid.NewGuid(), 7, 13, new[] { first, second });
        var targets = new[] { Address(7, first, "qty"), Address(7, first, "unitPrice"),
            Address(7, second, "qty"), Address(7, second, "unitPrice") };

        var result = SalesPlanner().Plan("2\t10.50\r\n3\t0\r\n", snapshot, targets);

        Assert.True(result.IsAccepted);
        Assert.Empty(result.Failures);
        Assert.Equal(new[] { 2m, 10.50m, 3m, 0m }, result.Patch!.Changes.Select(change => change.Value));
        Assert.Same(first, result.Patch.Changes[0].Line);
        Assert.Same(second, result.Patch.Changes[2].Line);
        Assert.Equal(13, snapshot.Revision);
    }

    [Theory]
    [InlineData("1,234\t2", "grouping")]
    [InlineData("=1+2\t2", "formula")]
    [InlineData("1.00001\t2", "scale")]
    [InlineData("\t2", "empty")]
    [InlineData("1\t2\t3", "extra column")]
    [InlineData("1\t2\n3", "ragged")]
    public void Planner_rejects_invalid_or_nonrectangular_text_as_a_whole(string text, string reason)
    {
        var first = new object(); var second = new object();
        var snapshot = new NumericDraftSnapshot<object>(Guid.NewGuid(), 7, 13, new[] { first, second });
        var targets = new[] { Address(7, first, "qty"), Address(7, first, "unitPrice"),
            Address(7, second, "qty"), Address(7, second, "unitPrice") };

        var result = SalesPlanner().Plan(text, snapshot, targets);

        Assert.False(result.IsAccepted, reason);
        Assert.Null(result.Patch);
        Assert.NotEmpty(result.Failures);
        Assert.Equal(13, snapshot.Revision);
    }

    [Theory]
    [InlineData("productId")]
    [InlineData("unit")]
    [InlineData("isGift")]
    [InlineData("unitCost")]
    public void Sales_planner_rejects_non_numeric_or_other_receipt_targets(string field)
    {
        var line = new object();
        var snapshot = new NumericDraftSnapshot<object>(Guid.NewGuid(), 7, 13, new[] { line });
        var result = SalesPlanner().Plan("12", snapshot, new[] { Address(7, line, field) });
        Assert.False(result.IsAccepted);
        Assert.Null(result.Patch);
    }

    [Fact]
    public void Planner_rejects_stale_generation_missing_row_and_oversize_clipboard()
    {
        var line = new object(); var absent = new object();
        var snapshot = new NumericDraftSnapshot<object>(Guid.NewGuid(), 7, 13, new[] { line });
        Assert.False(SalesPlanner().Plan("12", snapshot, new[] { Address(6, line, "qty") }).IsAccepted);
        Assert.False(SalesPlanner().Plan("12", snapshot, new[] { Address(7, absent, "qty") }).IsAccepted);
        Assert.False(SalesPlanner().Plan(new string('1', 262145), snapshot,
            new[] { Address(7, line, "qty") }).IsAccepted);
    }

    private static ReceiptNumericPastePlanner SalesPlanner()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        return new ReceiptNumericPastePlanner(ReceiptNumericField.UnitPrice,
            ReceiptDecimalInputProfiles.For(ReceiptNumericField.Quantity, culture),
            ReceiptDecimalInputProfiles.For(ReceiptNumericField.UnitPrice, culture));
    }

    private static InlineValidationAddress Address(long generation, object line, string field) =>
        new InlineValidationAddress(generation, line, field);
}
