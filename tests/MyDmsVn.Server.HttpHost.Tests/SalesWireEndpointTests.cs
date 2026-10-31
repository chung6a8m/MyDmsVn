using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Owin;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Application.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class SalesWireEndpointTests
{
    [Theory]
    [InlineData("GET", "/sales-receipts/9223372036854775807", "sales-get-posted-response.json", null)]
    [InlineData("GET", "/sales-receipts", "sales-list-response.json", null)]
    [InlineData("POST", "/sales-receipts", "sales-create-response.json", "sales-create-request.json")]
    [InlineData("PUT", "/sales-receipts/9223372036854775807/draft", "sales-update-response.json", "sales-update-request.json")]
    [InlineData("POST", "/sales-receipts/9223372036854775807/post", "sales-post-response.json", "sales-post-request.json")]
    public async Task Sales_routes_match_canonical_golden_payloads_without_precision_loss(string method, string route, string expected, string? body)
    {
        using var fixture = new SalesWireFixture();
        fixture.State.Detail = WireCorpus.Dto<SalesReceiptDto>(expected == "sales-list-response.json" || expected == "sales-post-response.json" ? "sales-get-posted-response.json" : expected);
        using var response = await fixture.Send(method, route, body == null ? null : WireCorpus.Text(body));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = WireCorpus.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(JToken.DeepEquals(WireCorpus.Json(expected), actual), actual.ToString());
        var call = Assert.Single(fixture.State.Calls);
        if (call is CreateSalesReceiptDraftCommand create)
        {

            Assert.Equal(new[] { 0L, 0L, 0L }, create.Request.Lines.Select(l => l.SaleLineId));
            Assert.Equal(new[] { 1.2500m, 0.0001m, 2.0000m }, create.Request.Lines.Select(l => l.Qty));
        }
        if (call is UpdateSalesReceiptDraftCommand update)
        {
            Assert.Equal(long.MaxValue, update.Request.SaleId);
            Assert.Equal(9007199254740993L, update.Request.Lines[0].SaleLineId);
            Assert.Equal("AAAAAAAAAAE=", update.Request.ExpectedVersion);
        }
        if (call is ListSalesReceiptsQuery list) Assert.Equal(25, list.Request.PageSize);
        if (call is PostSalesReceiptCommand post) Assert.Equal(long.MaxValue, post.Request.SaleId);
    }

    [Fact]
    public async Task Posted_SQL_unspecified_UTC_and_fractional_timestamp_preserve_cost_precision_and_signed_profit()
    {
        using var fixture = new SalesWireFixture();
        var expected = WireCorpus.Json("sales-get-posted-response.json");
        expected["data"]!["postedAtUtc"] = "2026-10-31T03:00:00.1234567Z";
        expected["data"]!["lines"]![0]!["postedUnitCost"] = "99999999999999999999.99999999";
        expected["data"]!["totalCost"] = "999999999999999999999999.9999";
        expected["data"]!["grossProfit"] = "-999999999999999999999999.9999";
        var settings = ApiJson.CreateSerializerSettings(); settings.DateTimeZoneHandling = DateTimeZoneHandling.Unspecified;
        fixture.State.Detail = JsonConvert.DeserializeObject<SalesReceiptDto>(expected["data"]!.ToString(), settings)!;
        Assert.Equal(DateTimeKind.Unspecified, fixture.State.Detail.PostedAtUtc!.Value.Kind);
        using var response = await fixture.Send("GET", "/sales-receipts/9223372036854775807");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = WireCorpus.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(JToken.DeepEquals(expected, actual), actual.ToString());
    }
    [Fact]
    public async Task Two_hundred_duplicate_products_keep_independent_line_identity_and_exact_values()
    {
        using var fixture = new SalesWireFixture();
        var request = WireCorpus.Json("sales-create-request.json");
        var result = WireCorpus.Json("sales-create-response.json");
        var inputs = new JArray(); var outputs = new JArray();
        for (var index = 0; index < 200; index++)
        {
            var input = (JObject)request["lines"]![0]!.DeepClone();
            input["qty"] = "0.0001"; input["unitPrice"] = "1.0000";
            inputs.Add(input);
            var output = (JObject)result["data"]!["lines"]![0]!.DeepClone();
            output["saleLineId"] = (9007199254740993L + index).ToString(CultureInfo.InvariantCulture);
            output["lineNo"] = index + 1; output["qty"] = "0.0001"; output["unitPrice"] = "1.0000"; output["lineAmount"] = "0.0001";
            outputs.Add(output);
        }
        request["lines"] = inputs; result["data"]!["lines"] = outputs; result["data"]!["totalAmount"] = "0.0200";
        fixture.State.Detail = WireCorpus.From<SalesReceiptDto>(result["data"]!);
        using var response = await fixture.Send("POST", "/sales-receipts", request.ToString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = WireCorpus.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(JToken.DeepEquals(result, actual), actual.ToString());
        var saved = Assert.IsType<CreateSalesReceiptDraftCommand>(Assert.Single(fixture.State.Calls));
        Assert.Equal(200, saved.Request.Lines.Count);
        Assert.All(saved.Request.Lines, l => { Assert.Equal(104, l.ProductId); Assert.Equal(0L, l.SaleLineId); Assert.Equal(0.0001m, l.Qty); });
    }

    [Theory]
    [InlineData("qty", "99999999999999.9999", "unitPrice", "0", true)]
    [InlineData("qty", "1", "unitPrice", "999999999999999.9999", false)]
    public async Task Exact_input_decimal_caps_are_preserved(string first, string firstValue, string second, string secondValue, bool gift)
    {
        using var fixture = new SalesWireFixture();
        var body = WireCorpus.Json("sales-create-request.json");
        var line = (JObject)body["lines"]![0]!.DeepClone();
        line[first] = firstValue; line[second] = secondValue; line["isGift"] = gift; body["lines"] = new JArray(line);
        using var response = await fixture.Send("POST", "/sales-receipts", body.ToString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = Assert.IsType<CreateSalesReceiptDraftCommand>(Assert.Single(fixture.State.Calls));
        Assert.Equal(decimal.Parse(firstValue, CultureInfo.InvariantCulture), command.Request.Lines[0].Qty);
        Assert.Equal(decimal.Parse(secondValue, CultureInfo.InvariantCulture), command.Request.Lines[0].UnitPrice);
    }

    [Theory]
    [MemberData(nameof(NegativeCreateCases))]
    public async Task Invalid_create_wire_is_rejected_before_handler(string name, string body)
    {
        using var fixture = new SalesWireFixture();
        using var response = await fixture.Send("POST", "/sales-receipts", body);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, name);
        await Invalid(response);
        Assert.Empty(fixture.State.Calls);
    }

    public static IEnumerable<object[]> NegativeCreateCases()
    {
        foreach (var entry in WireCorpus.Json("negative-cases.json")["cases"]!.OfType<JObject>().Where(e => (string?)e["fixture"] == "sales-create-request.json"))
        {
            var body = WireCorpus.Json("sales-create-request.json");
            var parts = ((string)entry["pointer"]!).TrimStart('/').Split('/');
            JToken parent = body;
            foreach (var part in parts.Take(parts.Length - 1)) parent = parent is JArray array ? array[int.Parse(part, CultureInfo.InvariantCulture)] : parent[part]!;
            parent[parts.Last()] = entry["value"]!.DeepClone();
            yield return new object[] { (string)entry["name"]!, body.ToString(Formatting.None) };
        }
        var template = WireCorpus.Text("sales-create-request.json");
        foreach (var value in new[] { "1.00000000000000000000000000001", "1.00000", "+1", " 1", "1 ", "١", "NaN", "Infinity" })
            yield return new object[] { "decimal-lexeme-" + value, template.Replace("1.2500", value) };
        foreach (var json in new[] { "{}", "[]", "null", "{", template + "{}", "/*comment*/" + template, template.Replace("\"saleDate\":", "saleDate:"), template.Replace("\"customerId\": 101", "\"customerId\": 101, \"customerId\": 102"), template.Replace("\"productId\": 104", "\"productId\": 104, \"productId\": 105"), template.Replace("\"isGift\": false", "\"isGift\": false,"), template.Replace("\"isGift\": false", "\"isGift\": 0"), template.Replace("\"customerId\": 101", "\"customerId\": 101.0"), template.Replace("\"customerId\": 101", "\"customerId\": 1e2") })
            yield return new object[] { "malformed-or-coerced-JSON", json };
        var missing = WireCorpus.Json("sales-create-request.json"); missing.Remove("saleDate"); yield return new object[] { "missing-date", missing.ToString() };
        var empty = WireCorpus.Json("sales-create-request.json"); empty["lines"] = new JArray(); yield return new object[] { "empty-lines", empty.ToString() };
        var many = WireCorpus.Json("sales-create-request.json"); many["lines"] = new JArray(Enumerable.Range(0, 201).Select(_ => many["lines"]![0]!.DeepClone())); yield return new object[] { "too-many-lines", many.ToString() };
    }

    [Theory]
    [InlineData("0")][InlineData("01")][InlineData("-1")][InlineData("+1")][InlineData("1.0")][InlineData("9223372036854775808")]
    public async Task Noncanonical_route_identity_never_reaches_handler(string id)
    {
        using var fixture = new SalesWireFixture();
        using var response = await fixture.Send("GET", "/sales-receipts/" + id);
        await Invalid(response); Assert.Empty(fixture.State.Calls);
    }

    [Theory]
    [InlineData("?pageSize=0")][InlineData("?pageSize=201")][InlineData("?pageSize=")][InlineData("?pageSize=01")]
    [InlineData("?pageSize=1&pageSize=2")][InlineData("?PageSize=25")][InlineData("?unknown=1")]
    [InlineData("?customerId=2147483648")][InlineData("?customerId=")][InlineData("?customerId=1.0")]
    [InlineData("?fromDate=2026-02-29")][InlineData("?fromDate=2024-02-29T00:00:00Z")]
    [InlineData("?fromDate=2026-10-31&toDate=2024-02-29")][InlineData("?status=draft")][InlineData("?status=")]
    [InlineData("?cursor=")][InlineData("?cursor=bad")][InlineData("?search=%00")][InlineData("?search=%FF")]
    [InlineData("?%70ageSize=1&pageSize=2")][InlineData("?pageSize=1&")][InlineData("?search=%")]
    public async Task List_query_rejects_invalid_duplicate_or_unknown_input(string query)
    {
        using var fixture = new SalesWireFixture();
        using var response = await fixture.Send("GET", "/sales-receipts" + query, rawQuery: query == "?search=%" ? "search=%" : null);
        await Invalid(response); Assert.Empty(fixture.State.Calls);
    }

    [Fact]
    public async Task List_preserves_filter_bound_opaque_cursor_and_date_range_without_changing_Local_default()
    {
        using var fixture = new SalesWireFixture();
        var criteria = new SalesReceiptListRequest(new DateTime(1, 1, 1), new DateTime(9999, 12, 31), 101, null, null, "Posted", 104, "Sản phẩm", 25);
        var cursor = SalesReceiptCursor.Parse(criteria).Encode(9007199254740993L, false);
        using var response = await fixture.Send("GET", "/sales-receipts?fromDate=0001-01-01&toDate=9999-12-31&customerId=101&status=Posted&productId=104&search=" + Uri.EscapeDataString("Sản phẩm") + "&cursor=" + Uri.EscapeDataString(cursor));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.IsType<ListSalesReceiptsQuery>(Assert.Single(fixture.State.Calls));
        Assert.Equal(25, query.Request.PageSize); Assert.Equal(cursor, query.Request.Cursor); Assert.Equal("Sản phẩm", query.Request.Search);
        Assert.Equal(50, new SalesReceiptListRequest(null, null, null, null, null, null, null, null).PageSize);
    }

    [Theory]
    [InlineData("products", "Catalog.Products.Read")][InlineData("customers", "Catalog.Customers.Read")]
    [InlineData("warehouses", "Catalog.Warehouses.Read")][InlineData("employees", "Catalog.Employees.Read")]
    public async Task Catalog_lookup_uses_own_permission_and_canonical_shape(string catalog, string permission)
    {
        using var fixture = new SalesWireFixture();
        fixture.State.Denied.Add("Sales.Receipts.Read");
        fixture.State.Lookup = WireCorpus.From<IReadOnlyList<CatalogLookupDto>>(WireCorpus.Json("catalog-" + catalog + "-response.json")["data"]!);
        using var response = await fixture.Send("GET", "/catalog/" + catalog + "/lookup?search=abc&limit=25");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(JToken.DeepEquals(WireCorpus.Json("catalog-" + catalog + "-response.json"), WireCorpus.Parse(await response.Content.ReadAsStringAsync())));
        Assert.Equal(permission, Assert.IsAssignableFrom<IAuthorizedRequest>(Assert.Single(fixture.State.Calls)).PermissionKey);
        fixture.State.Denied.Add(permission); fixture.State.Calls.Clear();
        using var denied = await fixture.Send("GET", "/catalog/" + catalog + "/lookup?limit=25");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Empty(fixture.State.Calls);
    }

    [Theory]
    [InlineData("")][InlineData("?limit=")][InlineData("?limit=201")][InlineData("?limit=0")]
    [InlineData("?limit=01")][InlineData("?limit=25&limit=25")][InlineData("?limit=25&unknown=1")]
    [InlineData("?limit=25&search=%00")][InlineData("?limit=25&search=%FF")]
    public async Task Lookup_requires_explicit_valid_limit_and_strict_query(string query)
    {
        using var fixture = new SalesWireFixture();
        using var response = await fixture.Send("GET", "/catalog/products/lookup" + query);
        await Invalid(response); Assert.Empty(fixture.State.Calls);
    }

    [Theory]
    [InlineData("POST", "/sales-receipts", "sales-create-request.json")]
    [InlineData("PUT", "/sales-receipts/9223372036854775807/draft", "sales-update-request.json")]
    [InlineData("POST", "/sales-receipts/9223372036854775807/post", "sales-post-request.json")]
    public async Task Writes_do_not_gain_Read_or_retry_and_Post_does_not_Get(string method, string route, string file)
    {
        using var fixture = new SalesWireFixture(); fixture.State.Denied.Add("Sales.Receipts.Read");
        using var response = await fixture.Send(method, route, WireCorpus.Text(file));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Single(fixture.State.Calls);
        using var get = await fixture.Send("GET", "/sales-receipts/9223372036854775807");
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode); Assert.Single(fixture.State.Calls);
    }

    [Theory]
    [InlineData("SalesReceipt.ConcurrencyConflict", 409)][InlineData("SalesReceipt.AlreadyPosted", 409)]
    [InlineData("SalesReceipt.NotFound", 404)]
    public async Task Business_errors_keep_status_and_code_without_second_mutation(string code, int status)
    {
        using var fixture = new SalesWireFixture(); fixture.State.Error = status == 404 ? Error.NotFound(code, "The document was not found.") : Error.Conflict(code, "The document has changed.");
        using var response = await fixture.Send("POST", "/sales-receipts/9223372036854775807/post", WireCorpus.Text("sales-post-request.json"));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(code, (string?)WireCorpus.Parse(await response.Content.ReadAsStringAsync())["error"]!["code"]); Assert.Single(fixture.State.Calls);
    }

    [Fact]
    public async Task Application_validation_maps_ordered_line_fields_and_fieldless_issues()
    {
        using var fixture = new SalesWireFixture();
        fixture.State.Errors = new List<Error> { Error.Validation("SalesReceipt.Test", "The line is invalid.", new Dictionary<string, object> { ["Field"] = "Request.Lines[1].Qty" }), Error.Validation("SalesReceipt.Other", "A document issue.") };
        using var response = await fixture.Send("POST", "/sales-receipts", WireCorpus.Text("sales-create-request.json"));
        await Invalid(response);
        var details = WireCorpus.Parse(await response.Content.ReadAsStringAsync())["error"]!["details"]!;
        Assert.Equal("lines[1].qty", (string?)details[0]!["field"]); Assert.Null(details[1]!["field"]);
    }

    [Theory]
    [InlineData("POST", "/sales-receipts?actor=1", "sales-create-request.json")]
    [InlineData("POST", "/sales-receipts/1/post?token=secret", "sales-post-request.json")]
    [InlineData("PUT", "/sales-receipts/1/draft?saleId=1", "sales-update-request.json")]
    [InlineData("GET", "/sales-receipts/1?unknown=1", null)]
    [InlineData("GET", "/sales-receipts", "sales-create-request.json")]
    [InlineData("GET", "/catalog/products/lookup?limit=25", "sales-create-request.json")]
    public async Task Routes_reject_query_or_body_outside_their_contract(string method, string route, string? file)
    {
        using var fixture = new SalesWireFixture();
        using var response = await fixture.Send(method, route, file == null ? null : WireCorpus.Text(file));
        await Invalid(response); Assert.Empty(fixture.State.Calls);
    }

    [Theory]
    [InlineData("{}")][InlineData("{\"expectedVersion\":1}")][InlineData("{\"expectedVersion\":\"bad\"}")]
    [InlineData("{\"expectedVersion\":\"AAAAAAAAAAE=\",\"saleId\":\"1\"}")]
    public async Task Post_accepts_only_valid_version_object(string body)
    {
        using var fixture = new SalesWireFixture();
        using var response = await fixture.Send("POST", "/sales-receipts/1/post", body);
        await Invalid(response); Assert.Empty(fixture.State.Calls);
    }

    [Theory]
    [InlineData("Sales.Receipts.Read", "GET", "/sales-receipts", null)]
    [InlineData("Sales.Receipts.Write", "POST", "/sales-receipts", "sales-create-request.json")]
    [InlineData("Sales.Receipts.Post", "POST", "/sales-receipts/1/post", "sales-post-request.json")]
    public async Task Sales_routes_require_authenticated_and_independently_authorized_actor(string denied, string method, string route, string? file)
    {
        using var fixture = new SalesWireFixture();
        using var anonymous = await fixture.Send(method, route, file == null ? null : WireCorpus.Text(file), authenticated: false);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        fixture.State.Denied.Add(denied);
        using var forbidden = await fixture.Send(method, route, file == null ? null : WireCorpus.Text(file));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); Assert.Empty(fixture.State.Calls);
    }

    internal static async Task Invalid(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = WireCorpus.Parse(await response.Content.ReadAsStringAsync()); Assert.Equal("ValidationError", (string?)json["error"]!["code"]);
        Assert.Equal(Assert.Single(response.Headers.GetValues("X-Correlation-ID")), (string?)json["error"]!["correlationId"]);
    }
}

internal static class WireCorpus
{
    private static readonly string Root = FindRoot();
    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "docs", "contracts", "web2-v1"))) return Path.Combine(directory.FullName, "docs", "contracts", "web2-v1");
        throw new DirectoryNotFoundException("The canonical wire corpus is required.");
    }
    public static string Text(string file) => File.ReadAllText(Path.Combine(Root, file));
    public static JObject Json(string file) => Parse(Text(file));
    public static JObject Parse(string json) { using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None }; return JObject.Load(reader); }
    public static T Dto<T>(string file) => From<T>(Json(file)["data"]!);
    public static T From<T>(JToken token) => JsonConvert.DeserializeObject<T>(token.ToString(), ApiJson.CreateSerializerSettings())!;
}
internal sealed class SalesWireFixture : IDisposable
{
    public readonly SalesWireState State = new SalesWireState();
    private readonly AuthFixture _host;
    private string? _token;
    public SalesWireFixture()
    {
        _host = new AuthFixture(services =>
        {
            services.AddSingleton<IPermissionStore>(State);
            Add<CreateSalesReceiptDraftCommand, SalesReceiptDto>(services, _ => State.Detail);
            Add<UpdateSalesReceiptDraftCommand, SalesReceiptDto>(services, _ => State.Detail);
            Add<PostSalesReceiptCommand, PostSalesReceiptResponse>(services, _ => WireCorpus.Dto<PostSalesReceiptResponse>("sales-post-response.json"));
            Add<GetSalesReceiptByIdQuery, SalesReceiptDto>(services, _ => State.Detail);
            Add<ListSalesReceiptsQuery, SalesReceiptListPage>(services, _ => WireCorpus.Dto<SalesReceiptListPage>("sales-list-response.json"));
            Add<LookupProductsQuery, IReadOnlyList<CatalogLookupDto>>(services, _ => State.Lookup);
            Add<LookupCustomersQuery, IReadOnlyList<CatalogLookupDto>>(services, _ => State.Lookup);
            Add<LookupWarehousesQuery, IReadOnlyList<CatalogLookupDto>>(services, _ => State.Lookup);
            Add<LookupEmployeesQuery, IReadOnlyList<CatalogLookupDto>>(services, _ => State.Lookup);
        }, context =>
        {
            var raw = context.Request.Headers.Get("X-Test-Raw-Query");
            if (raw != null) context.Request.QueryString = new QueryString(raw);
        });
    }
    private void Add<TRequest, TResult>(IServiceCollection services, Func<TRequest, TResult> result) where TRequest : IRequest<ErrorOr<TResult>> =>
        services.AddSingleton<IRequestHandler<TRequest, ErrorOr<TResult>>>(new CaptureHandler<TRequest, TResult>(request =>
        {
            State.Calls.Add(request!);
            if (State.Error.HasValue) return State.Error.Value;
            if (State.Errors != null) return State.Errors;
            return result(request);
        }));
    public async Task<HttpResponseMessage> Send(string method, string route, string? body = null, bool authenticated = true, string? rawQuery = null)
    {
        if (authenticated && _token == null)
        {
            using var login = await _host.Login(); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            _token = (string)WireCorpus.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
        }
        using var request = new HttpRequestMessage(new HttpMethod(method), AuthFixture.Origin + "/api/v1" + route);
        if (authenticated) request.Headers.Add("Authorization", "Bearer " + _token);
        if (rawQuery != null) request.Headers.Add("X-Test-Raw-Query", rawQuery);
        if (method != "GET") { request.Headers.Add("Origin", AuthFixture.Origin); request.Headers.Add("Sec-Fetch-Site", "same-origin"); }
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await _host.Server.HttpClient.SendAsync(request);
    }
    public void Dispose() => _host.Dispose();
    private sealed class CaptureHandler<TRequest, TResult> : IRequestHandler<TRequest, ErrorOr<TResult>> where TRequest : IRequest<ErrorOr<TResult>>
    {
        private readonly Func<TRequest, ErrorOr<TResult>> _handle;
        public CaptureHandler(Func<TRequest, ErrorOr<TResult>> handle) { _handle = handle; }
        public Task<ErrorOr<TResult>> Handle(TRequest request, CancellationToken cancellationToken) => Task.FromResult(_handle(request));
    }
}
internal sealed class SalesWireState : IPermissionStore
{
    public readonly List<object> Calls = new List<object>();
    public readonly HashSet<string> Denied = new HashSet<string>(StringComparer.Ordinal);
    public Error? Error;
    public List<Error>? Errors;
    public SalesReceiptDto Detail = WireCorpus.Dto<SalesReceiptDto>("sales-create-response.json");
    public IReadOnlyList<CatalogLookupDto> Lookup = new[] { new CatalogLookupDto(104, "SP104", "Sản phẩm", true) };
    public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string permissionKey, CancellationToken cancellationToken) => Task.FromResult(new PermissionSnapshot(!Denied.Contains(permissionKey), false));
}
