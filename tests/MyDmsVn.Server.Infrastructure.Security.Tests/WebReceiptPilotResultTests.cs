using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebReceiptPilotResultTests
{
    private static JToken[] Rows(params string[] ids) => System.Array.ConvertAll(ids, id => (JToken)new JObject { ["receiptId"] = id });

    [Fact]
    public void Exact_posted_set_accepts_different_result_order_and_preserves_row_mapping() =>
        Assert.Equal(new long[] { 2, 1 }, WebHostIisExpressTests.VerifyReceiptResultIdentities(Rows("2", "1"), new long[] { 1, 2 }));

    [Fact]
    public void Aliased_receipt_cannot_hide_an_omitted_posted_receipt() =>
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.VerifyReceiptResultIdentities(Rows("1", "01"), new long[] { 1, 2 }));

    [Fact]
    public void Equal_count_with_different_posted_receipt_set_is_rejected() =>
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.VerifyReceiptResultIdentities(Rows("1", "3"), new long[] { 1, 2 }));

    [Fact]
    public void Duplicate_numeric_identity_is_rejected() =>
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.VerifyReceiptResultIdentities(Rows("1", "1"), new long[] { 1, 2 }));

    [Theory]
    [InlineData("0")]
    [InlineData("01")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("9223372036854775808")]
    [InlineData(" 1")]
    public void Noncanonical_or_overflow_identity_is_rejected(string id) =>
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.VerifyReceiptResultIdentities(Rows(id), new long[] { 1 }));

    [Fact]
    public void Numeric_JSON_identity_is_rejected() =>
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.VerifyReceiptResultIdentities(new JToken[] { new JObject { ["receiptId"] = 1 } }, new long[] { 1 }));

    [Fact]
    public void Maximum_int64_identity_is_preserved() =>
        Assert.Equal(new[] { long.MaxValue }, WebHostIisExpressTests.VerifyReceiptResultIdentities(Rows("9223372036854775807"), new[] { long.MaxValue }));
}
