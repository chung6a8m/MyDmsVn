using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using MyDmsVn.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class InventoryWireTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public void Save_preserves_unique_lines_and_exact_maxima(int count)
    {
        var body = Fixture("receipt-create-request.json");
        body["lines"] = new JArray(Enumerable.Range(1, count).Select(i => new JObject
        { ["productId"] = i, ["quantity"] = "99999999999999.9999", ["unitCost"] = "999999999999999.9999" }));
        var parsed = InventoryWireInput.Create(body);
        Assert.Equal(count, parsed.Lines.Count);
        Assert.Equal(new DateTime(2024, 2, 29), parsed.ReceiptDate);
        Assert.All(parsed.Lines, line => { Assert.Equal(99999999999999.9999m, line.Quantity); Assert.Equal(999999999999999.9999m, line.UnitCost); });
    }

    [Theory]
    [InlineData("quantity", "1.00000")]
    [InlineData("quantity", "0")]
    [InlineData("quantity", "100000000000000")]
    [InlineData("quantity", "1e2")]
    [InlineData("quantity", "-0")]
    [InlineData("unitCost", "1000000000000000")]
    [InlineData("unitCost", "1.00000")]
    [InlineData("unitCost", "+1")]
    [InlineData("unitCost", " 1")]
    public void Save_rejects_noncanonical_or_out_of_range_decimal(string field, string text)
    {
        var body = Fixture("receipt-create-request.json"); body["lines"]![0]![field] = text;
        Assert.Equal("lines[0]." + field, Assert.Throws<WireInputException>(() => InventoryWireInput.Create(body)).Field);
    }

    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("2025-02-29")]
    [InlineData("2024-02-29T00:00:00Z")]
    public void Save_rejects_invalid_calendar_date(string text)
    {
        var body = Fixture("receipt-create-request.json"); body["receiptDate"] = text;
        Assert.Throws<WireInputException>(() => InventoryWireInput.Create(body));
    }

    [Fact]
    public void Save_note_omission_null_empty_and_Unicode_have_distinct_contracts()
    {
        var body = Fixture("receipt-create-request.json"); body.Remove("note");
        Assert.Null(InventoryWireInput.Create(body).Note);
        body["note"] = JValue.CreateNull(); Assert.Null(InventoryWireInput.Create(body).Note);
        body["note"] = ""; Assert.Equal("", InventoryWireInput.Create(body).Note);
        body["note"] = "Nhập kho 😀"; Assert.Equal("Nhập kho 😀", InventoryWireInput.Create(body).Note);
        foreach (var note in new[] { new string('a', 1001), "\0", "\ud800" })
        { body["note"] = note; Assert.Throws<WireInputException>(() => InventoryWireInput.Create(body)); }
    }

    [Theory]
    [InlineData("warehouseId")]
    [InlineData("employeeId")]
    public void Header_identity_requires_positive_int32_number(string field)
    {
        foreach (var value in new JToken[] { new JValue("1"), new JValue(0), new JValue(2147483648L), new JValue(1.5), JValue.CreateNull() })
        { var body = Fixture("receipt-create-request.json"); body[field] = value; Assert.Throws<WireInputException>(() => InventoryWireInput.Create(body)); }
    }

    [Fact]
    public void Save_rejects_line_identity_numeric_decimals_and_line_count_bounds()
    {
        foreach (var count in new[] { 0, 201 })
        {
            var body = Fixture("receipt-create-request.json");
            body["lines"] = new JArray(Enumerable.Range(1, count).Select(i => new JObject { ["productId"] = i, ["quantity"] = "1", ["unitCost"] = "0" }));
            Assert.Throws<WireInputException>(() => InventoryWireInput.Create(body));
        }
        foreach (var field in new[] { "receiptLineId", "lineNumber", "quantity" })
        {
            var body = Fixture("receipt-create-request.json"); body["lines"]![0]![field] = 1;
            Assert.Throws<WireInputException>(() => InventoryWireInput.Create(body));
        }
    }

    [Fact]
    public void Duplicate_products_reach_authoritative_Application_validation_without_deduplication()
    {
        var body = Fixture("receipt-create-request.json"); body["lines"]![1]!["productId"] = body["lines"]![0]!["productId"]!.DeepClone();
        var parsed = InventoryWireInput.Create(body);
        Assert.Equal(new[] { 7, 7 }, parsed.Lines.Select(line => line.ProductId));
        Assert.Equal(new[] { 1.2500m, 2.0000m }, parsed.Lines.Select(line => line.Quantity));
    }

    [Theory]
    [InlineData("AAAAAAAAAAE=")]
    [InlineData("//////////8=")]
    public void Update_and_post_preserve_canonical_version_and_int64_id(string version)
    {
        var body = Fixture("receipt-update-request.json"); body["expectedVersion"] = version;
        var update = InventoryWireInput.Update(long.MaxValue, body);
        Assert.Equal(long.MaxValue, update.ReceiptId); Assert.Equal(version, update.ExpectedVersion);
        Assert.Equal(version, InventoryWireInput.Post(long.MaxValue, new JObject { ["expectedVersion"] = version }).ExpectedVersion);
    }

    [Theory]
    [InlineData("AAAAAAAAAAE= ")]
    [InlineData("AAAAAAAAAAF=")]
    [InlineData("AAAAAAAAAAE")]
    [InlineData("AAAAAAAAAAAA")]
    public void Version_rejects_whitespace_noncanonical_padding_and_wrong_length(string version) =>
        Assert.Throws<WireInputException>(() => InventoryWireInput.Post(1, new JObject { ["expectedVersion"] = version }));

    [Theory]
    [InlineData("0")]
    [InlineData("01")]
    [InlineData("+1")]
    [InlineData("9223372036854775808")]
    public void Route_id_rejects_noncanonical_or_overflow(string id) => Assert.Throws<WireInputException>(() => InventoryWireInput.Long(id));

    [Theory]
    [InlineData("pageNumber=1")]
    [InlineData("pageNumber=1&pageSize=201")]
    [InlineData("pageNumber=01&pageSize=25")]
    [InlineData("pageNumber=1&pageSize=25&page%53ize=25")]
    [InlineData("pageNumber=1&pageSize=25&search=%C0%AF")]
    [InlineData("pageNumber=1&pageSize=25&search=%GG")]
    [InlineData("pageNumber=1&pageSize=25&search=%00")]
    [InlineData("pageNumber=1&pageSize=25&status=")]
    [InlineData("pageNumber=1&pageSize=25&productId=7")]
    public void Receipt_query_rejects_missing_ambiguous_or_unsupported_filters(string query)
    { using var request = Request(query); Assert.Throws<WireInputException>(() => InventoryWireInput.List(request)); }

    [Fact]
    public void Query_preserves_explicit_pages_and_distinct_search_limits()
    {
        using var list = Request("pageNumber=2&pageSize=200&warehouseId=2147483647&status=Posted&search=%20abc%20");
        var parsed = InventoryWireInput.List(list);
        Assert.Equal(2, parsed.PageNumber); Assert.Equal(200, parsed.PageSize); Assert.Equal(int.MaxValue, parsed.WarehouseId); Assert.Equal(" abc ", parsed.Search);
        using var longSearch = Request("pageNumber=1&pageSize=25&search=" + new string('a', 129));
        Assert.Throws<WireInputException>(() => InventoryWireInput.List(longSearch));
        Assert.Equal(129, InventoryWireInput.Balances(longSearch).Search!.Length);
        using var tooLong = Request("pageNumber=1&pageSize=25&search=" + new string('a', 257));
        Assert.Throws<WireInputException>(() => InventoryWireInput.Balances(tooLong));
    }

    [Theory]
    [InlineData("2026-11-01T02:03:04+07:00")]
    [InlineData("2026-11-01T02:03:04.12345678Z")]
    [InlineData("2026-11-01T02:03:60Z")]
    [InlineData("2026-02-29T02:03:04Z")]
    public void Card_rejects_invalid_or_non_UTC_timestamps(string timestamp)
    { using var request = Request("pageNumber=1&pageSize=25&warehouseId=1&productId=7&fromUtc=" + Uri.EscapeDataString(timestamp)); Assert.Throws<WireInputException>(() => InventoryWireInput.StockCard(request)); }

    [Fact]
    public void Card_requires_identity_and_preserves_seven_fractional_digits_and_range_order()
    {
        using var missing = Request("pageNumber=1&pageSize=25&warehouseId=1");
        Assert.Throws<WireInputException>(() => InventoryWireInput.StockCard(missing));
        using var valid = Request("pageNumber=1&pageSize=25&warehouseId=1&productId=7&fromUtc=2026-11-01T02:03:04.1234567Z");
        var result = InventoryWireInput.StockCard(valid);
        Assert.Equal(DateTimeKind.Utc, result.FromUtc!.Value.Kind); Assert.Equal(1234567, result.FromUtc.Value.Ticks % TimeSpan.TicksPerSecond);
        using var reversed = Request("pageNumber=1&pageSize=25&warehouseId=1&productId=7&fromUtc=2026-11-02T00:00:00Z&toUtc=2026-11-01T00:00:00Z");
        Assert.Throws<WireInputException>(() => InventoryWireInput.StockCard(reversed));
    }

    [Theory]
    [InlineData("{\"expectedVersion\":\"AAAAAAAAAAE=\",\"expectedVersion\":\"AAAAAAAAAAE=\"}")]
    [InlineData("{\"note\":\"\\ud800\"}")]
    [InlineData("{\"a\":1,}")]
    public async Task Body_rejects_duplicate_members_invalid_Unicode_and_JSON_extensions(string text)
    {
        using var request = Request(""); request.Content = new StringContent(text, Encoding.UTF8, "application/json");
        await Assert.ThrowsAsync<WireInputException>(() => InventoryWireInput.Body(request));
    }

    [Fact]
    public async Task Body_rejects_invalid_UTF8_content_type_encoding_and_query_and_GET_body()
    {
        using var request = Request(""); request.Content = new ByteArrayContent(new byte[] { 0xc0, 0xaf });
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        await Assert.ThrowsAsync<WireInputException>(() => InventoryWireInput.Body(request));
        request.Content = new StringContent("{}", Encoding.UTF8, "text/plain");
        await Assert.ThrowsAsync<WireInputException>(() => InventoryWireInput.Body(request));
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json"); request.Content.Headers.ContentEncoding.Add("gzip");
        await Assert.ThrowsAsync<WireInputException>(() => InventoryWireInput.Body(request));
        await Assert.ThrowsAsync<WireInputException>(() => InventoryWireInput.EmptyBody(request));
        using var queried = Request("extra=1"); queried.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        await Assert.ThrowsAsync<WireInputException>(() => InventoryWireInput.Body(queried));
        Assert.Throws<WireInputException>(() => InventoryWireInput.NoQuery(queried));
        using var empty = Request(""); await InventoryWireInput.EmptyBody(empty); InventoryWireInput.NoQuery(empty);
    }

    [Theory]
    [InlineData("receipt-create-response.json")]
    [InlineData("receipt-update-response.json")]
    [InlineData("receipt-get-draft-response.json")]
    [InlineData("receipt-get-posted-response.json")]
    public void Receipt_projection_matches_canonical_fields_nulls_and_lossless_values(string name)
    {
        var expected = Fixture(name)["data"]!;
        AssertJson(expected, InventoryWireOutput.Detail(Receipt(expected)));
        Assert.Equal(NullValueHandling.Ignore, ApiJson.CreateSerializerSettings().NullValueHandling);
    }

    [Theory]
    [InlineData("receipt-list-response.json")]
    [InlineData("receipt-list-empty-response.json")]
    public void Receipt_page_preserves_full_rows_and_string_counts(string name)
    {
        var expected = Fixture(name)["data"]!;
        var page = new PagedResult<GoodsReceiptDto>(expected["items"]!.Select(Receipt), (int)expected["pageNumber"]!, (int)expected["pageSize"]!, (long)expected["totalCount"]!);
        AssertJson(expected, InventoryWireOutput.ReceiptPage(page));
    }

    [Theory]
    [InlineData("balances-response.json")]
    [InlineData("balances-zero-response.json")]
    public void Balance_projection_preserves_null_average_and_physical_QV(string name)
    {
        var expected = Fixture(name)["data"]!;
        AssertJson(expected, InventoryWireOutput.BalancesPage(expected.ToObject<PagedResult<StockBalanceRow>>()!));
    }

    [Fact]
    public void Card_projection_preserves_signed_movements_and_unspecified_SQL_UTC()
    {
        var expected = Fixture("stock-card-response.json")["data"]!;
        var settings = ApiJson.CreateSerializerSettings(); settings.DateTimeZoneHandling = DateTimeZoneHandling.Unspecified;
        var page = JsonConvert.DeserializeObject<PagedResult<StockCardRow>>(expected.ToString(), settings)!;
        Assert.Equal(DateTimeKind.Unspecified, page.Items[1].PostedAtUtc.Kind);
        AssertJson(expected, InventoryWireOutput.StockCardPage(page));
    }

    [Fact]
    public void Post_ack_has_no_version_and_retains_server_id_and_UTC()
    {
        var expected = Fixture("receipt-post-response.json")["data"]!;
        AssertJson(expected, InventoryWireOutput.Post(expected.ToObject<PostGoodsReceiptResponse>()!));
    }

    [Fact]
    public void Balance_projection_fails_closed_on_inconsistent_zero_quantity()
    {
        var row = new StockBalanceRow(1, "W", "Warehouse", true, 7, "P", "Product", true, 0m, 1m, null);
        Assert.Throws<InvalidOperationException>(() => InventoryWireOutput.BalancesPage(new PagedResult<StockBalanceRow>(new[] { row }, 1, 25, 1)));
    }

    [Fact]
    public void Balance_projection_keeps_independent_count_and_full_int64_precision()
    {
        var row = new StockBalanceRow(1, "W", "Warehouse", true, 7, "P", "Product", true, 0m, 0m, null);
        var output = InventoryWireOutput.BalancesPage(new PagedResult<StockBalanceRow>(new[] { row }, 1, 25, 0));
        Assert.Single(output["items"]!); Assert.Equal("0", (string?)output["totalCount"]);
        output = InventoryWireOutput.BalancesPage(new PagedResult<StockBalanceRow>(new[] { row }, 1, 25, long.MaxValue));
        Assert.Equal("9223372036854775807", (string?)output["totalCount"]);
    }

    [Fact]
    public void Output_rejects_precision_loss_negative_balance_and_missing_positive_average()
    {
        foreach (var row in new[] {
            new StockBalanceRow(1, "W", "Warehouse", true, 7, "P", "Product", true, -1m, 0m, null),
            new StockBalanceRow(1, "W", "Warehouse", true, 7, "P", "Product", true, 1m, 1m, null),
            new StockBalanceRow(1, "W", "Warehouse", true, 7, "P", "Product", true, 1.00001m, 1m, 1m),
            new StockBalanceRow(1, "W", "Warehouse", true, 7, "P", "Product", true, 1m, 1m, 1.000000001m) })
            Assert.Throws<InvalidOperationException>(() => InventoryWireOutput.BalancesPage(new PagedResult<StockBalanceRow>(new[] { row }, 1, 25, 1)));
    }

    [Fact]
    public void Output_rejects_ambiguous_receipt_identity_or_line_order()
    {
        var expected = Fixture("receipt-create-response.json")["data"]!;
        foreach (var field in new[] { "receiptLineId", "lineNumber", "productId" })
        {
            var invalid = expected.DeepClone(); invalid["lines"]![1]![field] = invalid["lines"]![0]![field]!.DeepClone();
            Assert.Throws<InvalidOperationException>(() => InventoryWireOutput.Detail(Receipt(invalid)));
        }
    }

    [Fact]
    public void Unknown_stock_source_remains_read_only_server_text_without_invented_fields()
    {
        var expected = Fixture("stock-card-response.json")["data"]!;
        expected["items"]![0]!["documentType"] = "ExternalAdjustment";
        expected["items"]![0]!["documentNumber"] = "ExternalAdjustment#9007199254740993";
        AssertJson(expected, InventoryWireOutput.StockCardPage(expected.ToObject<PagedResult<StockCardRow>>()!));
    }

    private static void AssertJson(JToken expected, object value)
    {
        var actual = JsonConvert.DeserializeObject<JToken>(JsonConvert.SerializeObject(value, ApiJson.CreateSerializerSettings()), new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })!;
        Assert.True(JToken.DeepEquals(expected, actual), actual.ToString());
    }

    internal static GoodsReceiptDto Receipt(JToken token)
    {
        var dto = (JObject)token.DeepClone(); dto["id"] = dto["receiptId"]; dto.Remove("receiptId");
        foreach (var line in dto["lines"]!.Cast<JObject>()) { line["id"] = line["receiptLineId"]; line.Remove("receiptLineId"); }
        return dto.ToObject<GoodsReceiptDto>()!;
    }
    internal static JObject Fixture(string name)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "docs", "contracts", "web3-v1"))) directory = directory.Parent;
        if (directory == null) throw new InvalidOperationException("WEB3 corpus not found.");
        return JsonConvert.DeserializeObject<JObject>(File.ReadAllText(Path.Combine(directory.FullName, "docs", "contracts", "web3-v1", name)), new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })!;
    }
    private static HttpRequestMessage Request(string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/v1/goods-receipts");
        var context = new OwinContext(); context.Request.QueryString = new QueryString(query); request.SetOwinContext(context); return request;
    }
}
