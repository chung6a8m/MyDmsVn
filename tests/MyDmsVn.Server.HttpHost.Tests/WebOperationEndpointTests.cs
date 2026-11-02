using System;
using System.Collections.Generic;
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
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Sales;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class WebOperationEndpointTests
{
    public static IEnumerable<object[]> Writes()
    {
        yield return new object[] { "sales-create", "POST", "sales-receipts", WebOperationKind.SalesCreate };
        yield return new object[] { "sales-update", "PUT", "sales-receipts/9223372036854775807/draft", WebOperationKind.SalesUpdate };
        yield return new object[] { "sales-post", "POST", "sales-receipts/9223372036854775807/post", WebOperationKind.SalesPost };
        yield return new object[] { "receipt-create", "POST", "goods-receipts", WebOperationKind.ReceiptCreate };
        yield return new object[] { "receipt-update", "PUT", "goods-receipts/9007199254740993/draft", WebOperationKind.ReceiptUpdate };
        yield return new object[] { "receipt-post", "POST", "goods-receipts/9007199254740993/post", WebOperationKind.ReceiptPost };
    }

    [Theory, MemberData(nameof(Writes))]
    public async Task All_six_writes_require_key_before_application_dispatch(string stem, string method, string route, WebOperationKind kind)
    {
        using var probe = new Probe(true);
        using var response = await probe.Send(method, route, Corpus(stem + "-request.json"), keyed: false);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, kind.ToString());
        Assert.Empty(probe.Operations);
        Assert.Equal("ValidationError", (string?)JObject.Parse(await response.Content.ReadAsStringAsync())["error"]!["code"]);
    }

    [Theory, MemberData(nameof(Writes))]
    public async Task All_six_writes_attach_frozen_identity_and_validated_session(string stem, string method, string route, WebOperationKind kind)
    {
        using var probe = new Probe(true);
        using var response = await probe.Send(method, route, Corpus(stem + "-request.json"), keyed: true);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // Explicit capture-only handler, no SQL claim.
        var operation = Assert.Single(probe.Operations);
        Assert.NotNull(operation);
        Assert.Equal(kind, operation!.Kind);
        Assert.Equal(Probe.Operation, operation.OperationId);
        Assert.Equal(Probe.Epoch, operation.DeploymentEpoch);
        Assert.Equal(Guid.Parse("11111111-1111-4111-8111-111111111111"), operation.SessionId);
        var expected = JObject.Parse(Corpus(stem + "-status.json"))["data"]!;
        Assert.Equal((string?)expected["requestTargetId"], operation.RequestTargetId?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal((string?)expected["requestFingerprint"], BitConverter.ToString(operation.Fingerprint).Replace("-", "").ToLowerInvariant());
    }

    [Fact]
    public async Task Legacy_mode_rejects_keyed_write_instead_of_pretending_it_was_journaled()
    {
        using var probe = new Probe(false);
        using var response = await probe.Send("POST", "sales-receipts", Corpus("sales-create-request.json"), keyed: true);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(probe.Operations);
    }

    [Theory]
    [InlineData("Operation.ResultExpired", 410)]
    [InlineData("Operation.ResultUnavailable", 503)]
    [InlineData("Operation.Unavailable", 503)]
    [InlineData("Idempotency.KeyConflict", 409)]
    [InlineData("Deployment.EpochMismatch", 409)]
    public async Task Frozen_protocol_errors_have_exact_status_and_empty_details(string code, int status)
    {
        using var probe = new Probe(true) { ErrorCode = code };
        using var response = await probe.Send("POST", "sales-receipts", Corpus("sales-create-request.json"), keyed: true);
        Assert.Equal(status, (int)response.StatusCode);
        var error = JObject.Parse(await response.Content.ReadAsStringAsync())["error"]!;
        Assert.Equal(code, (string?)error["code"]);
        Assert.Empty((JArray)error["details"]!);
        Assert.DoesNotContain("private-handler-diagnostic", error.ToString());
    }

    internal static string Corpus(string name)
    {
        for (var root = new DirectoryInfo(AppContext.BaseDirectory); root != null; root = root.Parent)
        {
            var path = Path.Combine(root.FullName, "docs", "contracts", "web4-v1", name);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(name);
    }

    private sealed class Probe : IDisposable, IPermissionStore
    {
        internal static readonly Guid Operation = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
        internal static readonly Guid Epoch = Guid.Parse("11111111-2222-4333-8444-555555555555");
        internal readonly List<WebOperationContext?> Operations = new List<WebOperationContext?>();
        internal string? ErrorCode;
        private readonly AuthFixture _host;
        private string? _access;
        internal Probe(bool web4)
        {
            _host = new AuthFixture(services =>
            {
                services.AddSingleton<IPermissionStore>(this);
                Add<CreateSalesReceiptDraftCommand, SalesReceiptDto>(services, r => r.Operation);
                Add<UpdateSalesReceiptDraftCommand, SalesReceiptDto>(services, r => r.Operation);
                Add<PostSalesReceiptCommand, PostSalesReceiptResponse>(services, r => r.Operation);
                Add<CreateGoodsReceiptDraftCommand, GoodsReceiptDto>(services, r => r.Operation);
                Add<UpdateGoodsReceiptDraftCommand, GoodsReceiptDto>(services, r => r.Operation);
                Add<PostGoodsReceiptCommand, PostGoodsReceiptResponse>(services, r => r.Operation);
            }, web4Enabled: web4);
        }
        private void Add<T, R>(IServiceCollection services, Func<T, WebOperationContext?> metadata) where T : IRequest<ErrorOr<R>> =>
            services.AddSingleton<IRequestHandler<T, ErrorOr<R>>>(new Handler<T, R>(r =>
            {
                Operations.Add(metadata(r));
                return ErrorCode == "Operation.Unavailable" ? Error.Unexpected(ErrorCode, "private-handler-diagnostic")
                    : Error.Conflict(ErrorCode ?? "Test.Captured", "private-handler-diagnostic");
            }));
        internal async Task<HttpResponseMessage> Send(string method, string route, string body, bool keyed)
        {
            if (_access == null)
            {
                using var login = await _host.Login();
                _access = (string)JObject.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
            }
            using var request = new HttpRequestMessage(new HttpMethod(method), AuthFixture.Origin + "/api/v1/" + route);
            request.Headers.Add("Authorization", "Bearer " + _access);
            request.Headers.Add("Origin", AuthFixture.Origin);
            if (keyed)
            {
                request.Headers.Add("Idempotency-Key", Operation.ToString("D"));
                request.Headers.Add("X-MyDmsVn-Deployment-Epoch", Epoch.ToString("D"));
            }
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return await _host.Server.HttpClient.SendAsync(request);
        }
        public void Dispose() => _host.Dispose();
        public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string permissionKey, CancellationToken cancellationToken) =>
            Task.FromResult(new PermissionSnapshot(permissionKey == "Sales.Receipts.Write" || permissionKey == "Sales.Receipts.Post" ||
                permissionKey == PermissionKeys.InventoryGoodsReceiptsWrite || permissionKey == PermissionKeys.InventoryGoodsReceiptsPost, false));
    }
    private sealed class Handler<T, R> : IRequestHandler<T, ErrorOr<R>> where T : IRequest<ErrorOr<R>>
    {
        private readonly Func<T, ErrorOr<R>> _action;
        internal Handler(Func<T, ErrorOr<R>> action) { _action = action; }
        public Task<ErrorOr<R>> Handle(T request, CancellationToken token) => Task.FromResult(_action(request));
    }
}
