using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebIntegratedPilotResultTests
{
    private static JObject Receipt(string id, bool integrated = false) => new JObject
    {
        ["receiptId"] = id, ["documentType"] = "GoodsReceipt",
        ["scenario"] = integrated ? "integrated" : null,
        ["actor"] = integrated ? "integratedEditor" : "receiptEditor",
    };
    private static JObject Sale(string id, bool integrated = false)
    {
        var row = new JObject { ["saleId"] = id };
        if (integrated) { row["documentType"] = "SalesReceipt"; row["scenario"] = "integrated"; row["actor"] = "integratedEditor"; }
        return row;
    }
    private static void Verify(JArray rows, long[] receipts, long[] sales, bool integrated = true) =>
        WebHostIisExpressTests.VerifyPilotResultIdentities(rows, receipts, sales, integrated);

    [Fact]
    public void Module_scoped_same_id_and_reordered_complete_union_are_valid() =>
        Verify(new JArray(Sale("1", true), Receipt("2"), Receipt("1", true)), new long[] { 1, 2 }, new long[] { 1 });

    [Fact]
    public void Duplicate_receipt_across_scenarios_cannot_hide_an_omitted_document() =>
        Assert.Throws<InvalidDataException>(() => Verify(new JArray(Receipt("1"), Receipt("1", true)), new long[] { 1, 2 }, Array.Empty<long>()));

    [Fact]
    public void Duplicate_sale_across_scenarios_is_rejected() =>
        Assert.Throws<InvalidDataException>(() => Verify(new JArray(Sale("1"), Sale("1", true)), Array.Empty<long>(), new long[] { 1, 2 }));

    [Fact]
    public void Omitted_physical_posted_sale_is_rejected() =>
        Assert.Throws<InvalidDataException>(() => Verify(new JArray(Receipt("1", true)), new long[] { 1 }, new long[] { 1 }));

    [Fact]
    public void Invented_receipt_is_rejected() =>
        Assert.Throws<InvalidDataException>(() => Verify(new JArray(Receipt("2", true)), new long[] { 1 }, Array.Empty<long>()));

    [Theory]
    [InlineData("01")]
    [InlineData("0")]
    [InlineData("9223372036854775808")]
    public void Noncanonical_sale_identity_is_rejected(string id) =>
        Assert.Throws<InvalidDataException>(() => Verify(new JArray(Sale(id, true)), Array.Empty<long>(), new long[] { 1 }));

    [Fact]
    public void Numeric_identity_is_rejected()
    {
        var row = Sale("1", true); row["saleId"] = 1;
        Assert.Throws<InvalidDataException>(() => Verify(new JArray(row), Array.Empty<long>(), new long[] { 1 }));
    }

    [Theory]
    [InlineData("scenario", "other")]
    [InlineData("documentType", "Transfer")]
    [InlineData("actor", "alice")]
    public void Unknown_integrated_discriminant_is_rejected(string field, string value)
    {
        var row = Receipt("1", true); row[field] = value;
        Assert.Throws<InvalidDataException>(() => Verify(new JArray(row), new long[] { 1 }, Array.Empty<long>()));
    }

    [Fact]
    public void Integrated_rows_are_rejected_by_original_pilot_modes() =>
        Assert.Throws<InvalidDataException>(() => Verify(new JArray(Receipt("1", true)), new long[] { 1 }, Array.Empty<long>(), false));

    [Fact]
    public void Untagged_explicit_sale_type_is_not_silently_partitioned_away()
    {
        var row = Sale("1"); row["documentType"] = "SalesReceipt";
        Assert.Throws<InvalidDataException>(() => Verify(new JArray(row), Array.Empty<long>(), new long[] { 1 }));
    }
}
