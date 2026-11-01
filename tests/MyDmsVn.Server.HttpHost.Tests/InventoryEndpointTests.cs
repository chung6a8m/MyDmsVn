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
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Security;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class InventoryEndpointTests
{
    [Theory]
    [InlineData("POST", "/goods-receipts", "receipt-create-request.json")]
    [InlineData("PUT", "/goods-receipts/1/draft", "receipt-update-request.json")]
    [InlineData("POST", "/goods-receipts/1/post", "receipt-post-request.json")]
    public async Task Every_write_requires_same_origin(string method, string route, string body)
    {
        using var fixture = new InventoryEndpointFixture();
        using var response = await fixture.Send(method, route, InventoryEndpointCorpus.Text(body), change: request =>
        { request.Headers.Remove("Origin"); request.Headers.Add("Origin", "https://other.invalid"); });
        await AuthEndpointTests.Error(response, 403, "Auth.Forbidden");
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("POST", "/inventory/balances")]
    [InlineData("PUT", "/inventory/balances")]
    [InlineData("DELETE", "/inventory/balances")]
    [InlineData("POST", "/inventory/stock-card")]
    [InlineData("PUT", "/inventory/stock-card")]
    [InlineData("DELETE", "/inventory/stock-card")]
    public async Task Inventory_has_no_mutation_routes(string method, string route)
    {
        using var fixture = new InventoryEndpointFixture();
        using var response = await fixture.Send(method, route, "{}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [MemberData(nameof(InvalidCreateBodies))]
    public async Task Invalid_write_shapes_are_rejected_before_dispatch(string name, string body)
    {
        using var fixture = new InventoryEndpointFixture();
        using var response = await fixture.Send("POST", "/goods-receipts", body);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, name);
        await SalesWireEndpointTests.Invalid(response);
        Assert.Empty(fixture.Calls);
    }
    public static IEnumerable<object[]> InvalidCreateBodies()
    {
        foreach (var json in new[] { "{}", "[]", "null", "{", "{\"receiptDate\":\"2026-11-01\",\"receiptDate\":\"2026-11-02\"}", "/*comment*/{}" })
            yield return new object[] { "malformed-shape", json };
        foreach (var field in new[] { "receiptLineId", "clientLineId", "lineNumber" })
        {
            var body = InventoryEndpointCorpus.Json("receipt-create-request.json");
            body["lines"]![0]![field] = "1";
            yield return new object[] { "untrusted-line-identity-" + field, body.ToString() };
        }
        foreach (var value in new JToken[] { new JValue(1.25), new JValue("1.00000"), new JValue("1e2"), new JValue("0"), new JValue("100000000000000") })
        {
            var body = InventoryEndpointCorpus.Json("receipt-create-request.json"); body["lines"]![0]!["quantity"] = value;
            yield return new object[] { "invalid-quantity", body.ToString() };
        }
        foreach (var count in new[] { 0, 201 })
        {
            var body = InventoryEndpointCorpus.Json("receipt-create-request.json"); var line = body["lines"]![0]!;
            body["lines"] = new JArray(Enumerable.Range(1, count).Select(index => { var copy = (JObject)line.DeepClone(); copy["productId"] = index; return copy; }));
            yield return new object[] { "line-count-" + count, body.ToString() };
        }
    }

    [Fact]
    public async Task Duplicate_product_uses_Application_validation_code_and_ordered_field_before_handler()
    {
        using var fixture = new InventoryEndpointFixture();
        var body = InventoryEndpointCorpus.Json("receipt-create-request.json");
        body["lines"] = new JArray(body["lines"]![0]!.DeepClone(), body["lines"]![0]!.DeepClone());
        using var response = await fixture.Send("POST", "/goods-receipts", body.ToString());
        await SalesWireEndpointTests.Invalid(response);
        var detail = Assert.Single(WireCorpus.Parse(await response.Content.ReadAsStringAsync())["error"]!["details"]!);
        Assert.Equal("GoodsReceipt.DuplicateProduct", (string?)detail["code"]);
        Assert.Equal("lines[1].productId", (string?)detail["field"]);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"expectedVersion\":1}")]
    [InlineData("{\"expectedVersion\":\"AAAAAAAAAAF=\"}")]
    [InlineData("{\"expectedVersion\":\"AAAAAAAAAAE=\",\"receiptId\":\"1\"}")]
    public async Task Post_accepts_only_exact_canonical_version_body(string body)
    {
        using var fixture = new InventoryEndpointFixture();
        using var response = await fixture.Send("POST", "/goods-receipts/1/post", body);
        await SalesWireEndpointTests.Invalid(response);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("Inventory.GoodsReceipts.Read", "GET", "/goods-receipts?pageNumber=1&pageSize=25", null)]
    [InlineData("Inventory.GoodsReceipts.Read", "GET", "/goods-receipts/1", null)]
    [InlineData("Inventory.GoodsReceipts.Write", "POST", "/goods-receipts", "receipt-create-request.json")]
    [InlineData("Inventory.GoodsReceipts.Write", "PUT", "/goods-receipts/1/draft", "receipt-update-request.json")]
    [InlineData("Inventory.GoodsReceipts.Post", "POST", "/goods-receipts/1/post", "receipt-post-request.json")]
    [InlineData("Inventory.Balances.Read", "GET", "/inventory/balances?pageNumber=1&pageSize=25", null)]
    [InlineData("Inventory.StockCard.Read", "GET", "/inventory/stock-card?warehouseId=1&productId=7&pageNumber=1&pageSize=25", null)]
    public async Task Each_route_requires_authenticated_actor_and_its_own_permission(string permission, string method, string route, string? body)
    {
        using var fixture = new InventoryEndpointFixture();
        var json = body == null ? null : InventoryEndpointCorpus.Text(body);
        using var anonymous = await fixture.Send(method, route, json, authenticated: false);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        fixture.Denied.Add(permission);
        using var denied = await fixture.Send(method, route, json);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("POST", "/goods-receipts", "receipt-create-request.json", "receipt-create-response.json")]
    [InlineData("PUT", "/goods-receipts/9007199254740993/draft", "receipt-update-request.json", "receipt-update-response.json")]
    [InlineData("POST", "/goods-receipts/9007199254740993/post", "receipt-post-request.json", "receipt-post-response.json")]
    public async Task Write_and_post_success_do_not_require_read_or_dispatch_followup_get(string method, string route, string body, string expected)
    {
        using var fixture = new InventoryEndpointFixture { ResultFile = expected };
        fixture.Denied.Add("Inventory.GoodsReceipts.Read");
        using var response = await fixture.Send(method, route, InventoryEndpointCorpus.Text(body));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(fixture.Calls);
        var data = WireCorpus.Parse(await response.Content.ReadAsStringAsync())["data"]!;
        if (route.EndsWith("/post", StringComparison.Ordinal)) Assert.Null(data["version"]);
        using var get = await fixture.Send("GET", "/goods-receipts/9007199254740993");
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Single(fixture.Calls);
    }

    [Theory]
    [InlineData("GoodsReceipt.ConcurrencyConflict", 409)]
    [InlineData("GoodsReceipt.AlreadyPosted", 409)]
    [InlineData("GoodsReceipt.NotFound", 404)]
    public async Task Business_error_status_is_preserved_without_retry(string code, int status)
    {
        using var fixture = new InventoryEndpointFixture();
        fixture.Error = status == 404 ? Error.NotFound(code, "Not found.") : Error.Conflict(code, "Changed.");
        using var response = await fixture.Send("POST", "/goods-receipts/1/post", InventoryEndpointCorpus.Text("receipt-post-request.json"));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(code, (string?)WireCorpus.Parse(await response.Content.ReadAsStringAsync())["error"]!["code"]);
        Assert.Single(fixture.Calls);
    }

    [Fact]
    public async Task Application_errors_keep_line_paths_and_fieldless_issues_and_exceptions_are_sanitized()
    {
        using var fixture = new InventoryEndpointFixture();
        fixture.Errors = new List<Error> { Error.Validation("GoodsReceipt.ProductInactive", "Inactive.", new Dictionary<string, object> { ["Field"] = "Request.Lines[1].ProductId" }), Error.Validation("GoodsReceipt.ValuationOutOfRange", "Out of range.") };
        using var response = await fixture.Send("POST", "/goods-receipts", InventoryEndpointCorpus.Text("receipt-create-request.json"));
        await SalesWireEndpointTests.Invalid(response);
        var details = WireCorpus.Parse(await response.Content.ReadAsStringAsync())["error"]!["details"]!;
        Assert.Equal("lines[1].productId", (string?)details[0]!["field"]);
        Assert.Null(details[1]!["field"]);
        fixture.Errors = null; fixture.Throw = true;
        using var failure = await fixture.Send("POST", "/goods-receipts", InventoryEndpointCorpus.Text("receipt-create-request.json"));
        await AuthEndpointTests.Error(failure, 500, "InternalError");
        Assert.Equal(2, fixture.Calls.Count);
    }

    [Fact]
    public async Task Revoked_session_is_denied_before_business_dispatch()
    {
        using var fixture = new InventoryEndpointFixture();
        using var initial = await fixture.Send("GET", "/auth/capabilities?feature=goods-receipts");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        fixture.RevokeSession();
        using var denied = await fixture.Send("POST", "/goods-receipts", InventoryEndpointCorpus.Text("receipt-create-request.json"));
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("GET", "/goods-receipts/01", null)]
    [InlineData("GET", "/goods-receipts/9223372036854775808", null)]
    [InlineData("GET", "/goods-receipts/1?unknown=1", null)]
    [InlineData("GET", "/goods-receipts?pageNumber=1&pageSize=25", "receipt-create-request.json")]
    [InlineData("GET", "/inventory/balances?pageNumber=1&pageSize=25", "receipt-create-request.json")]
    [InlineData("GET", "/inventory/stock-card?warehouseId=1&productId=7&pageNumber=1&pageSize=25", "receipt-create-request.json")]
    [InlineData("POST", "/goods-receipts?actor=1", "receipt-create-request.json")]
    [InlineData("PUT", "/goods-receipts/1/draft?receiptId=1", "receipt-update-request.json")]
    [InlineData("POST", "/goods-receipts/1/post?unknown=1", "receipt-post-request.json")]
    public async Task Unexpected_route_query_or_get_body_fails_before_dispatch(string method, string route, string? body)
    {
        using var fixture = new InventoryEndpointFixture();
        using var response = await fixture.Send(method, route, body == null ? null : InventoryEndpointCorpus.Text(body));
        await SalesWireEndpointTests.Invalid(response);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("goods-receipts", "")]
    [InlineData("goods-receipts", "pageNumber=1&pageSize=25&productId=1")]
    [InlineData("goods-receipts", "pageNumber=1&pageSize=25&status=")]
    [InlineData("goods-receipts", "pageNumber=1&pageSize=25&fromDate=2026-11-01")]
    [InlineData("goods-receipts", "pageNumber=1&pageSize=25&cursor=bad")]
    [InlineData("inventory/balances", "pageNumber=1&pageSize=25&pageSize=25")]
    [InlineData("inventory/balances", "pageNumber=1&pageSize=201")]
    [InlineData("inventory/balances", "pageNumber=01&pageSize=25")]
    [InlineData("inventory/balances", "pageNumber=1&pageSize=25&search=%FF")]
    [InlineData("inventory/balances", "pageNumber=1&pageSize=25&search=%")]
    [InlineData("inventory/stock-card", "pageNumber=1&pageSize=25")]
    [InlineData("inventory/stock-card", "pageNumber=1&pageSize=25&warehouseId=1&productId=7&fromUtc=2026-11-02T00%3A00%3A00Z&toUtc=2026-11-01T00%3A00%3A00Z")]
    [InlineData("inventory/stock-card", "pageNumber=1&pageSize=25&warehouseId=1&productId=7&fromUtc=2026-11-01T00%3A00%3A00%2B00%3A00")]
    public async Task Invalid_queries_fail_before_dispatch(string route, string query)
    {
        using var fixture = new InventoryEndpointFixture();
        using var response = await fixture.Send("GET", "/" + route + "?" + query, rawQuery: query);
        await SalesWireEndpointTests.Invalid(response);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("goods-receipts&feature=goods-receipts")]
    [InlineData("goods-receipts&unknown=1")]
    [InlineData("goods-receipt")]
    [InlineData("Inventory.Balances.Read")]
    public async Task Capability_whitelist_rejects_unknown_or_ambiguous_features(string feature)
    {
        using var fixture = new InventoryEndpointFixture();
        using var response = await fixture.Send("GET", "/auth/capabilities?feature=" + feature);
        await AuthEndpointTests.Error(response, 400, "ValidationError");
    }

    [Theory]
    [InlineData("origin", 403)]
    [InlineData("content-type", 400)]
    [InlineData("charset", 400)]
    [InlineData("encoding", 400)]
    [InlineData("utf8", 400)]
    [InlineData("size", 413)]
    public async Task Write_transport_guards_apply_before_dispatch(string defect, int status)
    {
        using var fixture = new InventoryEndpointFixture();
        using var response = await fixture.Send("POST", "/goods-receipts", InventoryEndpointCorpus.Text("receipt-create-request.json"), change: request =>
        {
            if (defect == "origin") request.Headers.Remove("Origin");
            if (defect == "content-type") request.Content.Headers.ContentType!.MediaType = "text/plain";
            if (defect == "charset") request.Content.Headers.ContentType!.CharSet = "utf-16";
            if (defect == "encoding") request.Content.Headers.ContentEncoding.Add("gzip");
            if (defect == "utf8") { request.Content.Dispose(); request.Content = new ByteArrayContent(new byte[] { 0xff }); request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"); }
            if (defect == "size") { request.Content.Dispose(); request.Content = new StringContent(new string('x', 2 * 1024 * 1024)); }
        });
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("GET", "/goods-receipts/9007199254740993", "receipt-get-posted-response.json", null)]
    [InlineData("GET", "/goods-receipts?pageNumber=1&pageSize=25", "receipt-list-response.json", null)]
    [InlineData("POST", "/goods-receipts", "receipt-create-response.json", "receipt-create-request.json")]
    [InlineData("PUT", "/goods-receipts/9007199254740993/draft", "receipt-update-response.json", "receipt-update-request.json")]
    [InlineData("POST", "/goods-receipts/9007199254740993/post", "receipt-post-response.json", "receipt-post-request.json")]
    [InlineData("GET", "/inventory/balances?pageNumber=1&pageSize=25", "balances-response.json", null)]
    [InlineData("GET", "/inventory/stock-card?warehouseId=1&productId=7&pageNumber=1&pageSize=25", "stock-card-response.json", null)]
    public async Task Routes_match_canonical_corpus_and_dispatch_once(string method, string route, string expected, string? body)
    {
        using var fixture = new InventoryEndpointFixture();
        fixture.ResultFile = expected;
        using var response = await fixture.Send(method, route, body == null ? null : InventoryEndpointCorpus.Text(body));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = WireCorpus.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(JToken.DeepEquals(InventoryEndpointCorpus.Json(expected), actual), actual.ToString());
        var call = Assert.Single(fixture.Calls);
        if (call is CreateGoodsReceiptDraftCommand create) Assert.Equal(1.2500m, create.Request.Lines[0].Quantity);
        if (call is UpdateGoodsReceiptDraftCommand update) Assert.Equal(9007199254740993L, update.Request.ReceiptId);
        if (call is PostGoodsReceiptCommand post) Assert.Equal(9007199254740993L, post.Request.ReceiptId);
    }

    [Theory]
    [InlineData("goods-receipts", "Inventory.GoodsReceipts", true)]
    [InlineData("inventory-balances", "Inventory.Balances", false)]
    [InlineData("inventory-stock-card", "Inventory.StockCard", false)]
    [InlineData("sales", "Sales.Receipts", true)]
    public async Task Capabilities_are_live_independent_and_feature_scoped(string feature, string permission, bool writable)
    {
        using var fixture = new InventoryEndpointFixture();
        fixture.Denied.Add(permission + ".Read");
        using var response = await fixture.Send("GET", "/auth/capabilities?feature=" + feature);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (JObject)WireCorpus.Parse(await response.Content.ReadAsStringAsync())["data"]!;
        Assert.Equal(writable ? new[] { "read", "write", "post" } : new[] { "read" }, data.Properties().Select(p => p.Name));
        Assert.False((bool)data["read"]!);
        if (writable) { Assert.True((bool)data["write"]!); Assert.True((bool)data["post"]!); }
        fixture.Denied.Remove(permission + ".Read");
        using var granted = await fixture.Send("GET", "/auth/capabilities?feature=" + feature);
        Assert.True((bool)WireCorpus.Parse(await granted.Content.ReadAsStringAsync())["data"]!["read"]!);
        Assert.Empty(fixture.Calls);
    }
}

internal static class InventoryEndpointCorpus
{
    private static string Root
    {
        get
        {
            for (var path = new DirectoryInfo(AppContext.BaseDirectory); path != null; path = path.Parent)
            {
                var root = Path.Combine(path.FullName, "docs", "contracts", "web3-v1");
                if (Directory.Exists(root)) return root;
            }
            throw new DirectoryNotFoundException("The canonical WEB3 corpus is required.");
        }
    }
    public static string Text(string file) => File.ReadAllText(Path.Combine(Root, file));
    public static JObject Json(string file) => WireCorpus.Parse(Text(file));
    public static GoodsReceiptDto Detail(JToken data)
    {
        var clone = (JObject)data.DeepClone();
        clone["id"] = clone["receiptId"];
        foreach (var line in clone["lines"]!) line["id"] = line["receiptLineId"];
        return WireCorpus.From<GoodsReceiptDto>(clone);
    }
}

internal sealed class InventoryEndpointFixture : IDisposable, IPermissionStore
{
    public readonly List<object> Calls = new List<object>();
    public readonly HashSet<string> Denied = new HashSet<string>(StringComparer.Ordinal);
    public string ResultFile = "receipt-create-response.json";
    public Error? Error;
    public List<Error>? Errors;
    public bool Throw;
    private readonly AuthFixture _host;
    private string? _token;
    public InventoryEndpointFixture()
    {
        _host = new AuthFixture(services =>
        {
            services.AddSingleton<IPermissionStore>(this);
            Add<CreateGoodsReceiptDraftCommand, GoodsReceiptDto>(services, () => InventoryEndpointCorpus.Detail(Data()));
            Add<UpdateGoodsReceiptDraftCommand, GoodsReceiptDto>(services, () => InventoryEndpointCorpus.Detail(Data()));
            Add<GetGoodsReceiptByIdQuery, GoodsReceiptDto>(services, () => InventoryEndpointCorpus.Detail(Data()));
            Add<PostGoodsReceiptCommand, PostGoodsReceiptResponse>(services, () => WireCorpus.From<PostGoodsReceiptResponse>(Data()));
            Add<ListGoodsReceiptsQuery, PagedResult<GoodsReceiptDto>>(services, () => new PagedResult<GoodsReceiptDto>(Data()["items"]!.Select(InventoryEndpointCorpus.Detail), (int)Data()["pageNumber"]!, (int)Data()["pageSize"]!, long.Parse((string)Data()["totalCount"]!, CultureInfo.InvariantCulture)));
            Add<GetStockBalancesQuery, PagedResult<StockBalanceRow>>(services, () => WireCorpus.From<PagedResult<StockBalanceRow>>(Data()));
            Add<GetStockCardQuery, PagedResult<StockCardRow>>(services, () => WireCorpus.From<PagedResult<StockCardRow>>(Data()));
        }, context =>
        {
            var raw = context.Request.Headers.Get("X-Test-Raw-Query");
            if (raw != null) context.Request.QueryString = new QueryString(raw);
        });
    }
    private JToken Data() => InventoryEndpointCorpus.Json(ResultFile)["data"]!;
    private void Add<TRequest, TResult>(IServiceCollection services, Func<TResult> result) where TRequest : IRequest<ErrorOr<TResult>> =>
        services.AddSingleton<IRequestHandler<TRequest, ErrorOr<TResult>>>(new Handler<TRequest, TResult>(request =>
        {
            Calls.Add(request!);
            if (Throw) throw new InvalidOperationException("secret SQL connection");
            if (Error.HasValue) return Error.Value;
            if (Errors != null) return Errors;
            return result();
        }));
    public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string permissionKey, CancellationToken cancellationToken)
    {
        Assert.Equal(1, userId);
        return Task.FromResult(new PermissionSnapshot(!Denied.Contains(permissionKey), false));
    }
    public async Task<HttpResponseMessage> Send(string method, string route, string? body = null, bool authenticated = true, string? rawQuery = null, Action<HttpRequestMessage>? change = null)
    {
        if (authenticated && _token == null)
        {
            using var login = await _host.Login();
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            _token = (string)WireCorpus.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
        }
        using var request = new HttpRequestMessage(new HttpMethod(method), AuthFixture.Origin + "/api/v1" + route);
        if (authenticated) request.Headers.Add("Authorization", "Bearer " + _token);
        if (rawQuery != null) request.Headers.Add("X-Test-Raw-Query", rawQuery);
        if (method != "GET") { request.Headers.Add("Origin", AuthFixture.Origin); request.Headers.Add("Sec-Fetch-Site", "same-origin"); }
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        change?.Invoke(request);
        return await _host.Server.HttpClient.SendAsync(request);
    }
    public void RevokeSession() => _host.Store.Revoked = true;
    public void Dispose() => _host.Dispose();
    private sealed class Handler<TRequest, TResult> : IRequestHandler<TRequest, ErrorOr<TResult>> where TRequest : IRequest<ErrorOr<TResult>>
    {
        private readonly Func<TRequest, ErrorOr<TResult>> _handle;
        public Handler(Func<TRequest, ErrorOr<TResult>> handle) => _handle = handle;
        public Task<ErrorOr<TResult>> Handle(TRequest request, CancellationToken cancellationToken) => Task.FromResult(_handle(request));
    }
}
