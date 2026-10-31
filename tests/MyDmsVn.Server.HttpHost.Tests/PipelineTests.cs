using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin.Testing;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Sales;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class PipelineTests
{
    [Fact]
    public async Task Protected_api_rejects_anonymous_before_dispatch()
    {
        using var fixture = new HostFixture();
        using var response = await fixture.GetAsync("probe/actor", null);
        await AssertError(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized");
        Assert.Equal(0, fixture.State.Calls);
        Assert.Equal(0, fixture.Store.Lookups);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Fact]
    public async Task Health_is_minimal_and_anonymous_but_all_other_routes_are_protected()
    {
        using var fixture = new HostFixture();
        using var health = await fixture.GetAsync("health", null);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("{\"data\":{\"status\":\"ok\"}}", await health.Content.ReadAsStringAsync());
        Assert.Equal(0, fixture.Store.Lookups);
        using var missing = await fixture.GetAsync("missing", fixture.Token());
        await AssertError(missing, HttpStatusCode.NotFound, "NotFound");
    }

    [Theory]
    [InlineData("forged")][InlineData("expired")][InlineData("audience")][InlineData("issuer")]
    [InlineData("kid")][InlineData("algorithm")][InlineData("none")][InlineData("future")]
    [InlineData("missing-iat")][InlineData("missing-nbf")][InlineData("missing-exp")]
    [InlineData("invalid-sub")][InlineData("overflow-sub")][InlineData("invalid-sid")][InlineData("missing-jti")]
    [InlineData("duplicate-sub")][InlineData("duplicate-alg")][InlineData("string-exp")][InlineData("long-lived")]
    [InlineData("invalid-utf8")][InlineData("numeric-sid")][InlineData("jku")][InlineData("invalid-jti")]
    public async Task Untrusted_tokens_are_rejected_before_SQL_and_handler(string defect)
    {
        using var fixture = new HostFixture();
        using var response = await fixture.GetAsync("probe/actor", fixture.Token(defect: defect));
        await AssertError(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized");
        Assert.Equal(0, fixture.Store.Lookups);
        Assert.Equal(0, fixture.State.Calls);
    }

    [Theory]
    [InlineData("cookie")][InlineData("query")][InlineData("duplicate")][InlineData("comma")][InlineData("scheme")]
    public async Task Only_one_well_formed_authorization_header_can_supply_bearer(string source)
    {
        using var fixture = new HostFixture();
        using var request = fixture.Request("probe/actor" + (source == "query" ? "?access_token=" + fixture.Token() : ""));
        if (source == "cookie") request.Headers.Add("Cookie", "access_token=" + fixture.Token());
        else if (source == "duplicate") request.Headers.TryAddWithoutValidation("Authorization", new[] { "Bearer " + fixture.Token(), "Bearer " + fixture.Token() });
        else if (source == "comma") request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + fixture.Token() + ", Bearer " + fixture.Token());
        else if (source == "scheme") request.Headers.TryAddWithoutValidation("Authorization", "Basic " + fixture.Token());
        using var response = await fixture.Server.HttpClient.SendAsync(request);
        await AssertError(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized");
        Assert.Equal(0, fixture.Store.Lookups);
    }

    [Theory]
    [InlineData("revoked")][InlineData("disabled")][InlineData("different-user")]
    public async Task Authoritative_session_and_active_account_are_required(string state)
    {
        using var fixture = new HostFixture();
        fixture.Store.Mode = state;
        using var response = await fixture.GetAsync("probe/actor", fixture.Token());
        await AssertError(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized");
        Assert.Equal(1, fixture.Store.Lookups);
        Assert.Equal(0, fixture.State.Calls);
    }

    [Fact]
    public async Task SQL_failure_is_generic_JSON_500_and_disposes_auth_scope()
    {
        using var fixture = new HostFixture();
        fixture.Store.Mode = "failure";
        using var response = await fixture.GetAsync("probe/actor", fixture.Token());
        await AssertError(response, HttpStatusCode.InternalServerError, "InternalError");
        Assert.Equal(1, fixture.Store.Disposals);
        Assert.Equal(0, fixture.State.Calls);
    }

    [Fact]
    public async Task Truly_overlapping_requests_retain_distinct_immutable_actors_and_dispose_scopes()
    {
        using var fixture = new HostFixture();
        fixture.State.WaitForBoth = true;
        var first = fixture.GetAsync("probe/actor", fixture.Token(1));
        var second = fixture.GetAsync("probe/actor", fixture.Token(2));
        var responses = await Task.WhenAll(first, second);
        try
        {
            Assert.Equal(2, fixture.State.MaximumConcurrent);
            var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
            Assert.Equal(new[] { 1, 2 }, bodies.Select(b => (int)JObject.Parse(b)["data"]!["before"]!).OrderBy(x => x));
            foreach (var body in bodies)
            {
                var actor = JObject.Parse(body)["data"]!;
                Assert.Equal((int)actor["before"]!, (int)actor["after"]!);
                Assert.Equal("sql-user-" + (int)actor["before"]!, (string?)actor["username"]);
            }
            Assert.Equal(2, fixture.State.Disposals);
            Assert.Equal(2, fixture.Store.Disposals);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData("forbidden", 403, "Auth.Forbidden")][InlineData("failure", 500, "InternalError")]
    public async Task Downstream_failures_keep_status_safe_JSON_and_dispose(string path, int status, string code)
    {
        using var fixture = new HostFixture();
        using var response = await fixture.GetAsync("probe/" + path, fixture.Token());
        await AssertError(response, (HttpStatusCode)status, code);
        Assert.Equal(1, fixture.State.Disposals);
        Assert.Equal(1, fixture.Store.Disposals);
    }

    [Fact]
    public async Task Request_cancellation_drains_and_disposes_scope_without_replacing_actor()
    {
        using var fixture = new HostFixture();
        using var cancellation = new CancellationTokenSource();
        using var request = fixture.Request("probe/cancel", fixture.Token());
        var pending = fixture.Server.HttpClient.SendAsync(request, cancellation.Token);
        Assert.Same(fixture.State.Entered.Task, await Task.WhenAny(fixture.State.Entered.Task, Task.Delay(10000)));
        cancellation.Cancel();
        Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(10000)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Same(fixture.State.Disposed.Task, await Task.WhenAny(fixture.State.Disposed.Task, Task.Delay(10000)));
        Assert.Equal(1, fixture.State.Disposals);
        Assert.Equal(1, fixture.Store.Disposals);
    }

    [Theory]
    [InlineData("http", 400)][InlineData("host", 400)][InlineData("forwarded", 400)]
    [InlineData("origin", 403)][InlineData("body", 413)][InlineData("chunked-body", 413)]
    [InlineData("missing-origin", 403)][InlineData("duplicate-origin", 403)][InlineData("fetch-site", 403)]
    public async Task Transport_guards_reject_before_authentication(string defect, int status)
    {
        using var fixture = new HostFixture();
        using var request = fixture.Request("probe/actor", fixture.Token());
        if (defect == "http") request.RequestUri = new Uri("http://web2.mydmsvn.test/api/v1/probe/actor");
        if (defect == "host") request.RequestUri = new Uri("https://untrusted.invalid/api/v1/probe/actor");
        if (defect == "forwarded") request.Headers.Add("X-Forwarded-Proto", "https");
        if (defect == "origin") request.Headers.Add("Origin", "https://evil.invalid");
        if (defect == "duplicate-origin") request.Headers.TryAddWithoutValidation("Origin", new[] { HostFixture.Origin, HostFixture.Origin });
        if (defect == "missing-origin" || defect == "fetch-site") request.Method = HttpMethod.Post;
        if (defect == "fetch-site") { request.Headers.Add("Origin", HostFixture.Origin); request.Headers.Add("Sec-Fetch-Site", "cross-site"); }
        if (defect.EndsWith("body", StringComparison.Ordinal))
        {
            request.Method = HttpMethod.Post;
            request.Headers.Add("Origin", HostFixture.Origin);
            request.Content = new StringContent(new string('x', 2049), Encoding.UTF8, "application/json");
            if (defect == "chunked-body") request.Headers.TransferEncodingChunked = true;
        }
        using var response = await fixture.Server.HttpClient.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
        Assert.Equal(0, fixture.Store.Lookups);
    }

    [Fact]
    public async Task Real_application_pipeline_resolves_scoped_audit_and_uses_admitted_actor()
    {
        using var fixture = new HostFixture();
        using var response = await fixture.GetAsync("probe/application", fixture.Token(2));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = JObject.Parse(await response.Content.ReadAsStringAsync())["data"]!;
        Assert.Equal(2, (int)user["userId"]!);
        Assert.Equal("sql-user-2", (string?)user["username"]);
    }
    [Fact]
    public async Task Bearer_permission_claim_does_not_bypass_real_Application_authorizer()
    {
        using var fixture = new HostFixture();
        using var response = await fixture.GetAsync("probe/application-denied", fixture.Token());
        await AssertError(response, HttpStatusCode.Forbidden, "Auth.Forbidden");
    }
    [Theory]
    [InlineData("active", 200)][InlineData("revoked", 401)]
    public async Task Authentication_store_audit_gets_correlation_before_actor_admission(string mode, int status)
    {
        using var fixture = new HostFixture();
        fixture.Store.AuditOnLookup = true;
        fixture.Store.Mode = mode;
        using var request = fixture.Request("probe/actor", fixture.Token());
        request.Headers.Add("X-Correlation-ID", "auth-stage-correlation");
        using var response = await fixture.Server.HttpClient.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("auth-stage-correlation", Assert.Single(fixture.Store.Audits).CorrelationId);
    }
    [Fact]
    public async Task Invalid_correlation_is_replaced_and_valid_correlation_reaches_scoped_handler()
    {
        using var fixture = new HostFixture();
        using var request = fixture.Request("probe/actor", fixture.Token());
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "unsafe/secret");
        using var response = await fixture.Server.HttpClient.SendAsync(request);
        var correlation = response.Headers.GetValues("X-Correlation-ID").Single();
        Assert.True(Guid.TryParseExact(correlation, "N", out _));
        Assert.Equal(correlation, (string?)JObject.Parse(await response.Content.ReadAsStringAsync())["data"]!["correlation"]);
        using var request2 = fixture.Request("probe/actor", fixture.Token());
        request2.Headers.Add("X-Correlation-ID", "test-123");
        using var response2 = await fixture.Server.HttpClient.SendAsync(request2);
        Assert.Equal("test-123", response2.Headers.GetValues("X-Correlation-ID").Single());
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
        Assert.True(response.Headers.CacheControl.NoStore);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret", body);
        Assert.DoesNotContain("Exception", body);
        var error = JObject.Parse(body)["error"]!;
        Assert.Equal(code, (string?)error["code"]);
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), (string?)error["correlationId"]);
        Assert.Null(error["status"]);
    }
}

internal sealed class HostFixture : IDisposable
{
    public const string Origin = "https://web2.mydmsvn.test";
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("test-only-random-looking-key-32-bytes-minimum");
    public static readonly DateTime Now = new DateTime(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc);
    public readonly StoreState Store = new StoreState();
    public readonly ProbeState State = new ProbeState();
    private readonly ServiceProvider _provider;
    public TestServer Server { get; }
    public HostFixture()
    {
        var options = new WebHostOptions(Origin, Origin, "web2-tests", "test", new Dictionary<string, byte[]> { ["test"] = Key }, maximumRequestBytes: 2048);
        var services = new ServiceCollection();
        services.AddServerApplication().AddHttpRequestIdentity();
        services.AddSingleton<IUtcClock>(new FixedClock());
        services.AddSingleton(State); services.AddSingleton(Store);
        services.AddSingleton<ISecurityAuditSink>(Store);
        services.AddScoped<IUserAccountValidator, ActiveAccount>();
        services.AddScoped<IWebAuthSessionStore, TestSessionStore>();
        services.AddScoped<ProbeLifetime>(); services.AddTransient<ProbeController>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Server = TestServer.Create(app => Web2Pipeline.Configure(app, options, _provider, config =>
            config.Services.Replace(typeof(IAssembliesResolver), new TestAssemblies())));
    }
    public HttpRequestMessage Request(string path, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Origin + "/api/v1/" + path);
        if (token != null) request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        return request;
    }
    public async Task<HttpResponseMessage> GetAsync(string path, string? token)
    {
        using var request = Request(path, token);
        return await Server.HttpClient.SendAsync(request);
    }
    public string Token(int userId = 1, string? defect = null)
    {
        var epoch = new DateTimeOffset(Now).ToUnixTimeSeconds();
        var payload = new JObject
        {
            ["iss"] = defect == "issuer" ? "https://invalid.test" : Origin,
            ["aud"] = defect == "audience" ? "invalid" : "web2-tests",
            ["sub"] = defect == "invalid-sub" ? "0" : defect == "overflow-sub" ? "2147483648" : userId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sid"] = defect == "invalid-sid" ? "bad" : StoreState.SessionId.ToString("D"),
            ["jti"] = Guid.NewGuid().ToString("D"), ["iat"] = epoch, ["nbf"] = epoch, ["exp"] = epoch + 600,
            ["username"] = "untrusted-token-name", ["permissions"] = new JArray("Sales.Receipts.Read"),
        };
        if (defect == "expired") { payload["iat"] = epoch - 1200; payload["nbf"] = epoch - 1200; payload["exp"] = epoch - 600; }
        if (defect == "future") { payload["iat"] = epoch + 120; payload["nbf"] = epoch + 120; payload["exp"] = epoch + 720; }
        if (defect == "long-lived") payload["exp"] = epoch + 3600;
        if (defect == "string-exp") payload["exp"] = (epoch + 600).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (defect?.StartsWith("missing-", StringComparison.Ordinal) == true) payload.Remove(defect.Substring(8));
        if (defect == "numeric-sid") payload["sid"] = 42;
        if (defect == "invalid-jti") payload["jti"] = "not-a-guid";
        var algorithm = defect == "algorithm" ? "HS384" : defect == "none" ? "none" : "HS256";
        var header = new JObject { ["alg"] = algorithm, ["kid"] = defect == "kid" ? "unknown" : "test", ["typ"] = "JWT" };
        if (defect == "jku") header["jku"] = "https://untrusted.invalid/key";
        var headerText = header.ToString(Newtonsoft.Json.Formatting.None);
        var payloadText = payload.ToString(Newtonsoft.Json.Formatting.None);
        if (defect == "duplicate-sub") payloadText = payloadText.Replace("\"sub\":\"1\"", "\"sub\":\"1\",\"sub\":\"2\"");
        if (defect == "duplicate-alg") headerText = headerText.Replace("\"alg\":\"HS256\"", "\"alg\":\"none\",\"alg\":\"HS256\"");
        if (defect == "invalid-utf8") return Base64UrlEncoder.Encode(new byte[] { 0xff, 0xfe }) + "." + Base64UrlEncoder.Encode(payloadText) + ".invalid";
        var unsigned = Base64UrlEncoder.Encode(headerText) + "." + Base64UrlEncoder.Encode(payloadText);
        using var hmac = new System.Security.Cryptography.HMACSHA256(defect == "forged" ? Encoding.UTF8.GetBytes("test-only-wrong-key-that-is-at-least-32-bytes") : Key);
        return unsigned + "." + (defect == "none" ? "" : Base64UrlEncoder.Encode(hmac.ComputeHash(Encoding.ASCII.GetBytes(unsigned))));
    }
    public void Dispose() { Server.Dispose(); _provider.Dispose(); }
    private sealed class ActiveAccount : IUserAccountValidator
    { public Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken) => Task.FromResult(true); }
    private sealed class FixedClock : IUtcClock { public DateTime UtcNow => Now; }
    private sealed class TestAssemblies : DefaultAssembliesResolver
    { public override ICollection<System.Reflection.Assembly> GetAssemblies() => new[] { typeof(ProbeController).Assembly, typeof(HealthController).Assembly }; }
}
internal sealed class StoreState : ISecurityAuditSink
{
    public static readonly Guid SessionId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public string Mode = "active";
    public bool AuditOnLookup;
    public readonly System.Collections.Concurrent.ConcurrentBag<SecurityAuditEntry> Audits = new System.Collections.Concurrent.ConcurrentBag<SecurityAuditEntry>();
    public Task WriteAsync(SecurityAuditEntry entry, CancellationToken cancellationToken) { Audits.Add(entry); return Task.CompletedTask; }
    public int Lookups;
    public int Disposals;
}
internal sealed class TestSessionStore : IWebAuthSessionStore, IDisposable
{
    private readonly StoreState _state;
    private readonly ISecurityAuditWriter _audit;
    public TestSessionStore(StoreState state, ISecurityAuditWriter audit) { _state = state; _audit = audit; }
    public async Task<CurrentUser?> GetActiveUserAsync(Guid sessionId, int userId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _state.Lookups);
        Assert.Equal(StoreState.SessionId, sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        if (_state.Mode == "failure") throw new InvalidOperationException("secret connection/password details");
        if (_state.AuditOnLookup) await _audit.WriteAsync(new SecurityAuditEntry(SecurityAuditAction.Authentication, SecurityAuditOutcome.Succeeded, userId, HostFixture.Now), false, cancellationToken);
        return _state.Mode == "revoked" ? null : CurrentUser.Authenticated(_state.Mode == "different-user" ? 99 : userId,
            "sql-user-" + userId, "SQL User " + userId, _state.Mode != "disabled");
    }
    public Task<WebAuthSessionToken?> IssueAsync(int userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<WebAuthSessionToken?> RotateAsync(string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();
    public void Dispose() => Interlocked.Increment(ref _state.Disposals);
}
public sealed class ProbeState
{
    public int Calls, Disposals, Concurrent, MaximumConcurrent;
    public bool WaitForBoth;
    public readonly TaskCompletionSource<bool> Both = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource<bool> Disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
}
public sealed class ProbeLifetime : IDisposable
{
    private readonly ProbeState _state;
    public ProbeLifetime(ProbeState state) { _state = state; }
    public void Dispose() { Interlocked.Increment(ref _state.Disposals); _state.Disposed.TrySetResult(true); }
}
[RoutePrefix("api/v1/probe")]
public sealed class ProbeController : ApiController
{
    private readonly ICurrentUserAccessor _actor;
    private readonly IRequestCorrelationAccessor _correlation;
    private readonly ProbeState _state;
    public ProbeController(ICurrentUserAccessor actor, IRequestCorrelationAccessor correlation, ProbeState state, ProbeLifetime lifetime)
    { _actor = actor; _correlation = correlation; _state = state; GC.KeepAlive(lifetime); }
    [HttpGet, Route("actor")]
    public async Task<HttpResponseMessage> Actor()
    {
        Interlocked.Increment(ref _state.Calls);
        var before = _actor.Current.UserId;
        if (_state.WaitForBoth)
        {
            var count = Interlocked.Increment(ref _state.Concurrent);
            _state.MaximumConcurrent = Math.Max(_state.MaximumConcurrent, count);
            if (count == 2) _state.Both.TrySetResult(true);
            var finished = await Task.WhenAny(_state.Both.Task, Task.Delay(10000));
            Assert.Same(_state.Both.Task, finished);
            Interlocked.Decrement(ref _state.Concurrent);
        }
        return HttpJson.Success(new { before, after = _actor.Current.UserId, username = _actor.Current.Username, correlation = _correlation.CorrelationId });
    }
    [HttpGet, Route("application")]
    public async Task<HttpResponseMessage> Application(CancellationToken cancellationToken)
    {
        var mediator = (IMediator)Request.GetDependencyScope().GetService(typeof(IMediator))!;
        var result = await mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        return HttpJson.From(ApiResponseMapper.Map(result, null, _correlation.CorrelationId));
    }
    [HttpGet, Route("application-denied")]
    public async Task<HttpResponseMessage> ApplicationDenied(CancellationToken cancellationToken)
    {
        var mediator = (IMediator)Request.GetDependencyScope().GetService(typeof(IMediator))!;
        var result = await mediator.Send(new GetSalesReceiptByIdQuery(1), cancellationToken);
        return HttpJson.From(ApiResponseMapper.Map(result, null, _correlation.CorrelationId));
    }
    [HttpGet, Route("forbidden")]
    public HttpResponseMessage Forbidden() => HttpJson.Error(403, "Auth.Forbidden", "Access is denied.", _correlation.CorrelationId!);
    [HttpGet, Route("failure")]
    public HttpResponseMessage Failure() => throw new InvalidOperationException("secret database text");
    [HttpGet, Route("cancel")]
    public async Task<HttpResponseMessage> Cancel(CancellationToken cancellationToken)
    { _state.Entered.TrySetResult(true); await Task.Delay(Timeout.Infinite, cancellationToken); return HttpJson.Success(new { }); }
}
