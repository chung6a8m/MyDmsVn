using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Application.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Application.Tests;

public sealed class SalesReceiptQueryContractTests
{
    [Fact]
    public async Task Exact_search_limit_cursor_size_and_public_validation_paths_are_preserved()
    {
        var query = new RecordingQuery(); using var provider = Provider(query); var sender = provider.GetRequiredService<ISender>();
        Assert.False((await sender.Send(new ListSalesReceiptsQuery(Request(search: new string('x', 256))))).IsError);
        var invalid = await sender.Send(new ListSalesReceiptsQuery(Request(cursor: "invalid")));
        var mapped = ApiResponseMapper.Map(invalid, "Request"); Assert.Equal("cursor", Assert.Single(mapped.Error!.Details).Field);
        var encoded = Cursor(Request(), "next", 1); var bytes = Convert.FromBase64String(Pad(encoded));
        var exact = new byte[3072]; Array.Copy(bytes, exact, bytes.Length); for (var i = bytes.Length; i < exact.Length; i++) exact[i] = 32;
        Assert.Equal(4096, EncodeBytes(exact).Length);
        Assert.False((await sender.Send(new ListSalesReceiptsQuery(Request(cursor: EncodeBytes(exact))))).IsError);
        // Alter unused terminal base64 bits without altering decoded bytes: canonical spelling must reject.
        var canonical = Encode("{}"); var alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        var bad = canonical.Substring(0, canonical.Length - 1) + alphabet[alphabet.IndexOf(canonical[canonical.Length - 1]) + 1];
        var result = await sender.Send(new ListSalesReceiptsQuery(Request(cursor: bad))); Assert.True(result.IsError); Assert.Equal("Validation.InvalidCursor", result.FirstError.Code);
    }
    [Theory]
    [InlineData("size0")]
    [InlineData("size201")]
    [InlineData("customer")]
    [InlineData("warehouse")]
    [InlineData("employee")]
    [InlineData("product")]
    [InlineData("status")]
    [InlineData("blank-status")]
    [InlineData("dates")]
    [InlineData("search-length")]
    [InlineData("search-nul")]
    public async Task Invalid_criteria_are_field_validation_before_query(string boundary)
    {
        var request = new SalesReceiptListRequest(boundary == "dates" ? new DateTime(2026, 10, 23) : (DateTime?)null,
            boundary == "dates" ? new DateTime(2026, 10, 22) : (DateTime?)null,
            boundary == "customer" ? 0 : (int?)null, boundary == "warehouse" ? -1 : (int?)null,
            boundary == "employee" ? 0 : (int?)null, boundary == "status" ? "posted" : boundary == "blank-status" ? " " : null,
            boundary == "product" ? -1 : (int?)null, boundary == "search-length" ? new string('x', 257) : boundary == "search-nul" ? "a\0b" : null,
            boundary == "size0" ? 0 : boundary == "size201" ? 201 : 50);
        var query = new RecordingQuery(); using var provider = Provider(query);
        var result = await provider.GetRequiredService<ISender>().Send(new ListSalesReceiptsQuery(request));
        Assert.True(result.IsError); Assert.All(result.Errors, error => Assert.Equal(ErrorType.Validation, error.Type));
        Assert.All(result.Errors, error => Assert.StartsWith("Request.", (string)error.Metadata!["Field"])); Assert.Null(query.Received);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(200)]
    public async Task Valid_criteria_normalize_once_without_case_or_literal_wildcard_changes(int size)
    {
        var query = new RecordingQuery(); using var provider = Provider(query);
        var result = await provider.GetRequiredService<ISender>().Send(new ListSalesReceiptsQuery(new SalesReceiptListRequest(
            new DateTime(1, 1, 1, 17, 0, 0, DateTimeKind.Local), DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc),
            1, 2, 3, "Posted", 4, "  Đ%_[  ", size)));
        Assert.False(result.IsError); Assert.NotNull(query.Received); Assert.Equal("Đ%_[", query.Received!.Search);
        Assert.Equal(DateTimeKind.Unspecified, query.Received.FromDate!.Value.Kind); Assert.Equal(new DateTime(1, 1, 1), query.Received.FromDate);
        Assert.Equal(new DateTime(9999, 12, 31), query.Received.ToDate); Assert.Equal(size, query.Received.PageSize);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Blank_search_and_default_size_have_one_normalized_shape(string? search)
    {
        var query = new RecordingQuery(); using var provider = Provider(query);
        Assert.False((await provider.GetRequiredService<ISender>().Send(new ListSalesReceiptsQuery(Request(search: search)))).IsError);
        Assert.Null(query.Received!.Search); Assert.Equal(50, query.Received.PageSize);
    }

    [Theory]
    [InlineData("write")]
    [InlineData("post")]
    [InlineData("deny")]
    [InlineData("inactive")]
    public async Task Cursor_reconstruction_never_bypasses_current_Read_authority(string grant)
    {
        var query = new RecordingQuery(); using var provider = Provider(query, grant);
        var request = Request(); var cursor = Cursor(request, "next", 1);
        var result = await provider.GetRequiredService<ISender>().Send(new ListSalesReceiptsQuery(Request(cursor: cursor)));
        Assert.True(result.IsError); Assert.Equal(grant == "inactive" ? ErrorType.Unauthorized : ErrorType.Forbidden, result.FirstError.Type); Assert.Null(query.Received);
    }

    [Theory]
    [MemberData(nameof(BadCursors))]
    public async Task Malformed_cursor_is_stable_field_validation_without_query(string label, string cursor)
    {
        var query = new RecordingQuery(); using var provider = Provider(query);
        var result = await provider.GetRequiredService<ISender>().Send(new ListSalesReceiptsQuery(Request(cursor: cursor)));
        Assert.True(result.IsError, label); Assert.Contains(result.Errors, error => error.Code == "Validation.InvalidCursor" && (string)error.Metadata!["Field"] == "Request.Cursor");
        Assert.Null(query.Received);
    }

    public static IEnumerable<object[]> BadCursors()
    {
        var valid = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(Pad(Cursor(Request(), "next", 7)))));
        foreach (var pair in new[] { ("v", (JToken)new JValue(2)), ("order", new JValue("date")), ("direction", new JValue("NEXT")),
            ("anchor", new JValue(0)), ("anchor", new JValue(-1)), ("anchor", new JValue("7")), ("anchor", new JValue(1.5)),
            ("pageSize", new JValue(0)), ("pageSize", new JValue(51)), ("filterHash", new JValue("bad")), ("filterHash", JValue.CreateNull()) })
        { var copy = (JObject)valid.DeepClone(); copy[pair.Item1] = pair.Item2; yield return new object[] { pair.Item1 + pair.Item2.Type, Encode(copy.ToString(Formatting.None)) }; }
        foreach (var name in valid.Properties().Select(p => p.Name)) { var copy = (JObject)valid.DeepClone(); copy.Remove(name); yield return new object[] { "missing-" + name, Encode(copy.ToString(Formatting.None)) }; }
        var json = valid.ToString(Formatting.None);
        foreach (var pair in new[] { ("empty", ""), ("padding", Cursor(Request(),"next",7)+"="), ("alphabet", "+/a"), ("encoded-limit", new string('a',4097)),
            ("bytes-limit", Encode(new string(' ',3073))), ("invalid-utf8", Convert.ToBase64String(new byte[]{0xc3,0x28}).TrimEnd('=').Replace('+','-').Replace('/','_')),
            ("duplicate",Encode(json.Replace("\"v\":1", "\"v\":1,\"v\":1"))), ("unknown",Encode(json.Replace("\"v\":1", "\"v\":1,\"extra\":1"))),
            ("comment",Encode("/*x*/"+json)), ("trailing",Encode(json+"{}")), ("single-quote",Encode(json.Replace('"','\''))), ("trailing-comma",Encode(json.Insert(json.Length-1,","))),
            ("nested",Encode("{\"v\":[[[[[1]]]]]}")), ("overflow",Encode(json.Replace("\"anchor\":7","\"anchor\":9223372036854775808"))),
            ("primitive",Encode("null")), ("hash-string-limit",Encode(json.Replace((string)valid["filterHash"]!,new string('x',3000)))) })
            yield return new object[] { pair.Item1, pair.Item2 };
    }

    [Theory]
    [InlineData("FromDate")]
    [InlineData("ToDate")]
    [InlineData("CustomerId")]
    [InlineData("WarehouseId")]
    [InlineData("EmployeeId")]
    [InlineData("Status")]
    [InlineData("ProductId")]
    [InlineData("Search")]
    [InlineData("PageSize")]
    public async Task Every_normalized_filter_is_bound_to_cursor(string field)
    {
        var cursor = Cursor(Request(), "previous", long.MaxValue);
        var request = new SalesReceiptListRequest(field == "FromDate" ? new DateTime(2026, 1, 1) : (DateTime?)null, field == "ToDate" ? new DateTime(2026, 2, 1) : (DateTime?)null,
            field == "CustomerId" ? 1 : (int?)null, field == "WarehouseId" ? 2 : (int?)null, field == "EmployeeId" ? 3 : (int?)null, field == "Status" ? "Draft" : null,
            field == "ProductId" ? 4 : (int?)null, field == "Search" ? "x" : null, field == "PageSize" ? 1 : 50, cursor);
        var query = new RecordingQuery(); using var provider = Provider(query);
        var result = await provider.GetRequiredService<ISender>().Send(new ListSalesReceiptsQuery(request));
        Assert.True(result.IsError); Assert.Equal("Validation.InvalidCursor", result.FirstError.Code); Assert.Null(query.Received);
    }

    [Theory]
    [InlineData("next")]
    [InlineData("previous")]
    public async Task Reconstructed_cursor_with_missing_anchor_and_normalized_filters_is_accepted(string direction)
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            var request = Request(search: "Đ%_["); var cursor = Cursor(request, direction, long.MaxValue);
            var query = new RecordingQuery(); using var provider = Provider(query);
            var result = await provider.GetRequiredService<ISender>().Send(new ListSalesReceiptsQuery(Request(search: "  Đ%_[  ", cursor: cursor)));
            Assert.False(result.IsError); Assert.NotNull(query.Received);
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    private static SalesReceiptListRequest Request(string? search = null, string? cursor = null) => new(null, null, null, null, null, null, null, search, cursor: cursor);
    private static string Cursor(SalesReceiptListRequest r, string direction, long anchor)
    {
        var canonical = new JObject
        {
            ["FromDate"] = r.FromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["ToDate"] = r.ToDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["CustomerId"] = r.CustomerId,
            ["WarehouseId"] = r.WarehouseId,
            ["EmployeeId"] = r.EmployeeId,
            ["Status"] = r.Status,
            ["ProductId"] = r.ProductId,
            ["Search"] = r.Search,
            ["PageSize"] = r.PageSize
        };
        using var sha = SHA256.Create(); var hash = EncodeBytes(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString(Formatting.None))));
        return Encode(new JObject { ["v"] = 1, ["order"] = "SaleIdDesc", ["direction"] = direction, ["anchor"] = anchor, ["pageSize"] = r.PageSize, ["filterHash"] = hash }.ToString(Formatting.None));
    }
    private static string Encode(string value) => EncodeBytes(Encoding.UTF8.GetBytes(value));
    private static string EncodeBytes(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Pad(string value) => value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4);
    private static ServiceProvider Provider(RecordingQuery query, string grant = "read")
    {
        var services = new ServiceCollection(); services.AddServerApplication(); services.AddSingleton<ISalesReceiptQueryService>(query);
        services.AddSingleton<ICurrentUserAccessor>(new Actor(grant != "inactive")); services.AddSingleton<IPermissionStore>(new Permissions(grant)); return services.BuildServiceProvider();
    }
    private sealed class RecordingQuery : ISalesReceiptQueryService
    {
        public SalesReceiptListRequest? Received { get; private set; }
        public Task<SalesReceiptListPage> ListAsync(SalesReceiptListRequest request, CancellationToken token) { Received = request; return Task.FromResult(new SalesReceiptListPage(Array.Empty<SalesReceiptListRow>(), null, null)); }
        public Task<SalesReceiptDto?> GetByIdAsync(long id, CancellationToken token) => Task.FromResult<SalesReceiptDto?>(null);
    }
    private sealed class Actor : ICurrentUserAccessor { public Actor(bool active) => Current = CurrentUser.Authenticated(42, "reader", "Reader", active); public CurrentUser Current { get; } }
    private sealed class Permissions : IPermissionStore
    {
        private readonly string _grant; public Permissions(string grant) => _grant = grant;
        public Task<PermissionSnapshot> GetSnapshotAsync(int id, string key, CancellationToken token) => Task.FromResult(new PermissionSnapshot(_grant == "deny" ? false : (bool?)null,
            hasRoleGrant: _grant == "deny" || key == (_grant == "write" ? PermissionKeys.SalesReceiptsWrite : _grant == "post" ? PermissionKeys.SalesReceiptsPost : PermissionKeys.SalesReceiptsRead)));
    }
}
