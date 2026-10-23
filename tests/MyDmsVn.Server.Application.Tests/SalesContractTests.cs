using System;
using System.Globalization;
using System.Linq;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Domain.Sales;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
namespace MyDmsVn.Server.Application.Tests;
public sealed class SalesContractTests
{
    private static readonly JsonSerializerSettings Settings = ApiJson.CreateSerializerSettings();
    [Theory]
    [InlineData("vi-VN", DateTimeKind.Local)]
    [InlineData("en-US", DateTimeKind.Utc)]
    [InlineData("ar-SA", DateTimeKind.Unspecified)]
    public void Calendar_roundtrip_preserves_date_components(string culture, DateTimeKind kind)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var request = new SaveSalesReceiptRequest(new DateTime(2026, 10, 22, 23, 45, 1, kind), 1, 2, 3, null, new[] { new SaveSalesReceiptLineRequest(0, 1, 2, 25, true) });
            var json = JsonConvert.SerializeObject(request, Settings);
            Assert.Contains("\"saleDate\":\"2026-10-22\"", json);
            var copy = JsonConvert.DeserializeObject<SaveSalesReceiptRequest>(json, Settings)!;
            Assert.Equal(new DateTime(2026, 10, 22), copy.SaleDate); Assert.Equal(DateTimeKind.Unspecified, copy.SaleDate.Kind);
            Assert.Equal(25m, copy.Lines[0].UnitPrice); Assert.True(copy.Lines[0].IsGift);
            var filters = JsonConvert.DeserializeObject<SalesReceiptListRequest>("{\"fromDate\":\"2026-10-22\",\"toDate\":null}", Settings)!;
            Assert.Equal(DateTimeKind.Unspecified, filters.FromDate!.Value.Kind); Assert.Null(filters.ToDate); Assert.Equal(50, filters.PageSize);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Theory]
    [InlineData("2026-10-22T00:00:00Z")]
    [InlineData("2026-10-22T00:00:00+07:00")]
    [InlineData("2026-02-30")]
    [InlineData("22/10/2026")]
    [InlineData("2026-1-2")]
    public void Calendar_rejects_timestamps_offsets_and_invalid_dates(string input)
    {
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<SaveSalesReceiptRequest>("{\"saleDate\":" + JsonConvert.SerializeObject(input) + ",\"lines\":[]}", Settings));
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<SalesReceiptListRequest>("{\"fromDate\":" + JsonConvert.SerializeObject(input) + "}", Settings));
    }
    [Fact]
    public void Commands_have_exact_allowlists_and_opaque_versions()
    {
        Assert.Equal(new[] { "EmployeeId", "ExpectedVersion", "Lines", "Note", "SaleDate", "SaleId", "WarehouseId", "CustomerId" }.OrderBy(x => x), typeof(UpdateSalesReceiptDraftRequest).GetProperties().Select(p => p.Name).OrderBy(x => x));
        Assert.Equal(new[] { "IsGift", "ProductId", "Qty", "SaleLineId", "UnitPrice" }, typeof(SaveSalesReceiptLineRequest).GetProperties().Select(p => p.Name).OrderBy(x => x));
        Assert.Equal(new[] { "ExpectedVersion", "SaleId" }, typeof(PostSalesReceiptRequest).GetProperties().Select(p => p.Name).OrderBy(x => x));
        var result = new PostSalesReceiptResponse(7, "Posted", new DateTime(2026, 10, 22, 3, 0, 0, DateTimeKind.Utc), "opaque+/==");
        var json = JsonConvert.SerializeObject(result, Settings);
        Assert.Equal("opaque+/==", JsonConvert.DeserializeObject<PostSalesReceiptResponse>(json, Settings)!.Version);
        Assert.Contains("2026-10-22T03:00:00Z", json);
        Assert.Equal(new[] { "PostedAtUtc", "SaleId", "Status", "Version" }, typeof(PostSalesReceiptResponse).GetProperties().Select(p => p.Name).OrderBy(x => x));
    }
    [Fact]
    public void Draft_costs_are_nullable_and_posted_zero_cost_is_distinct()
    {
        var draft = new SalesReceiptLineDto(1, 1, 1, "ea", 1, 25, true, 0, null, null, "Product", true);
        var posted = new SalesReceiptLineDto(1, 1, 1, "ea", 1, 25, true, 0, 0, 0, "Product", false);
        Assert.Null(JsonConvert.DeserializeObject<SalesReceiptLineDto>(JsonConvert.SerializeObject(draft, Settings), Settings)!.CostAmount);
        Assert.Equal(0m, JsonConvert.DeserializeObject<SalesReceiptLineDto>(JsonConvert.SerializeObject(posted, Settings), Settings)!.CostAmount);
        var domain = new SalesReceipt(); Assert.Equal(SalesReceiptStatuses.Draft, domain.Status); Assert.Empty(domain.Version);
        Assert.Null(new SalesReceiptLine().PostedUnitCost);
    }
    [Fact]
    public void Posted_detail_requires_costs_and_draft_detail_rejects_costs()
    {
        var line = new SalesReceiptLineDto(1, 1, 1, "ea", 1, 1, false, 1, null, null, null, true);
        Assert.Throws<ArgumentException>(() => new SalesReceiptDto(1, "SA0000000001", DateTime.Today, 1, 1, 1, "Posted", null, DateTime.UtcNow, "v", new[] { line }, 1, null, null, null, true, null, true, null, true));
        var costed = new SalesReceiptLineDto(1, 1, 1, "ea", 1, 1, false, 1, 0, 0, null, true);
        Assert.Throws<ArgumentException>(() => new SalesReceiptDto(1, "SA0000000001", DateTime.Today, 1, 1, 1, "Draft", null, null, "v", new[] { costed }, 1, null, null, null, true, null, true, null, true));
        var draft = new SalesReceiptDto(1, "SA0000000001", DateTime.Today, 1, 1, 1, "Draft", null, null, "v", new[] { line }, 1, null, null, null, true, null, true, null, true);
        var copy = JsonConvert.DeserializeObject<SalesReceiptDto>(JsonConvert.SerializeObject(draft, Settings), Settings)!;
        Assert.Null(copy.TotalCost); Assert.Null(copy.GrossProfit); Assert.Equal("v", copy.Version);
        var posted = new SalesReceiptDto(1, "SA0000000001", DateTime.Today, 1, 1, 1, "Posted", null, DateTime.UtcNow, "v2", new[] { costed }, 1, 0, 1, null, true, null, true, null, true);
        Assert.Equal(0m, JsonConvert.DeserializeObject<SalesReceiptDto>(JsonConvert.SerializeObject(posted, Settings), Settings)!.TotalCost);
    }

}
