using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Dispatcher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Owin.Testing;
using Microsoft.Owin;
using Owin;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.HttpHost;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class AuthEndpointTests
{
    [Fact]
    public async Task Login_issues_verifiable_active_key_JWT_and_host_cookie_without_refresh_in_JSON()
    {
        using var host = new AuthFixture();
        using var response = await host.Post("login", "{\"username\":\" alice \",\"password\":\"correct-secret\"}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
        var data = json["data"]!;
        Assert.Equal(new[] { "accessToken", "expiresAtUtc", "user" }, ((JObject)data).Properties().Select(p => p.Name).OrderBy(x => x));
        Assert.Equal(1, (int)data["user"]!["userId"]!);
        Assert.Equal("authoritative-alice", (string?)data["user"]!["username"]);
        Assert.Contains("\"expiresAtUtc\":\"2026-10-31T09:10:00Z\"", await response.Content.ReadAsStringAsync());
        var token = (string)data["accessToken"]!;
        Assert.NotNull(new BearerTokenValidator(host.Options, host.Clock).Validate(token));
        var header = JObject.Parse(Encoding.UTF8.GetString(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(token.Split('.')[0])));
        Assert.Equal("active", (string?)header["kid"]);
        var claims = JObject.Parse(Encoding.UTF8.GetString(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(token.Split('.')[1])));
        Assert.Null(claims["permissions"]);
        Assert.Null(claims["username"]);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Equal("__Host-mydmsvn-rt=22222222-2222-4222-8222-222222222222; Max-Age=604800; Path=/; Secure; HttpOnly; SameSite=Strict", cookie);
        Assert.DoesNotContain(AuthFixture.InitialRefresh, json.ToString());
        Assert.True(response.Headers.CacheControl.NoStore);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Theory]
    [InlineData("{\"username\":\"missing\",\"password\":\"correct-secret\"}")]
    [InlineData("{\"username\":\"alice\",\"password\":\"wrong-secret\"}")]
    [InlineData("{\"username\":\"disabled\",\"password\":\"correct-secret\"}")]
    public async Task Login_reuses_Application_generic_invalid_credentials(string body)
    {
        using var host = new AuthFixture();
        using var response = await host.Post("login", body);
        await Error(response, 401, "Auth.InvalidCredentials");
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(0, host.Store.Issues);
        Assert.Single(host.Store.Audits);
    }

    [Fact]
    public async Task Login_persistence_failure_cannot_return_access_or_cookie()
    {
        using var host = new AuthFixture();
        host.Store.FailIssue = true;
        using var response = await host.Login();
        await Error(response, 500, "InternalError");
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("{}")][InlineData("[]")][InlineData("null")][InlineData("not-json")]
    [InlineData("{\"Username\":\"alice\",\"password\":\"correct-secret\"}")]
    [InlineData("{\"username\":\"alice\",\"username\":\"bob\",\"password\":\"correct-secret\"}")]
    [InlineData("{\"username\":\"alice\",\"password\":\"correct-secret\",\"actor\":1}")]
    [InlineData("{\"username\":1,\"password\":\"correct-secret\"}")]
    [InlineData("{\"username\":\"alice\",\"password\":null}")]
    [InlineData("{\"username\":\" \",\"password\":\"correct-secret\"}")]
    [InlineData("{\"username\":\"alice\",\"password\":\"correct-secret\"} {}")]
    [InlineData("{/*comment*/\"username\":\"alice\",\"password\":\"correct-secret\"}")]
    [InlineData("{\"username\":\"alice\",\"password\":\"correct-secret\",}")]
    public async Task Login_rejects_ambiguous_or_noncanonical_body_before_credentials(string body)
    {
        using var host = new AuthFixture();
        using var response = await host.Post("login", body);
        await Error(response, 400, "ValidationError");
        Assert.Equal(0, host.Store.CredentialLookups);
        Assert.Equal(0, host.Store.Issues);
    }

    [Theory]
    [InlineData("missing-origin")][InlineData("cross-origin")][InlineData("missing-header")]
    [InlineData("wrong-header")][InlineData("duplicate-header")][InlineData("cross-site")]
    [InlineData("duplicate-fetch")]
    public async Task Cookie_changes_require_exact_Origin_and_CSRF_header(string defect)
    {
        using var host = new AuthFixture();
        using var request = host.PostRequest("login", "{\"username\":\"alice\",\"password\":\"correct-secret\"}");
        if (defect.Contains("origin")) { request.Headers.Remove("Origin"); if (defect == "cross-origin") request.Headers.Add("Origin", "https://evil.invalid"); }
        if (defect.Contains("header")) { request.Headers.Remove("X-Requested-With"); if (defect == "wrong-header") request.Headers.Add("X-Requested-With", "Fetch"); if (defect == "duplicate-header") request.Headers.TryAddWithoutValidation("X-Requested-With", new[] { "XMLHttpRequest", "XMLHttpRequest" }); }
        if (defect == "cross-site") { request.Headers.Remove("Sec-Fetch-Site"); request.Headers.Add("Sec-Fetch-Site", "cross-site"); }
        if (defect == "duplicate-fetch") { request.Headers.Remove("Sec-Fetch-Site"); request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", new[] { "same-origin", "same-origin" }); }
        using var response = await host.Server.HttpClient.SendAsync(request);
        await Error(response, 403, "Auth.Forbidden");
        Assert.Equal(0, host.Store.CredentialLookups);
    }

    [Theory]
    [InlineData("refresh")][InlineData("logout")]
    public async Task Cookie_only_endpoints_ignore_invalid_optional_bearer(string route)
    {
        using var host = new AuthFixture();
        using var request = host.PostRequest(route);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer invalid-expired-or-malformed");
        request.Headers.Add("Cookie", "__Host-mydmsvn-rt=" + AuthFixture.InitialRefresh);
        using var response = await host.Server.HttpClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains(route == "refresh" ? AuthFixture.ChildRefresh : "Max-Age=0", cookie);
        Assert.Equal(route == "refresh" ? 1 : 0, host.Store.Rotations);
        Assert.Equal(route == "logout" ? 1 : 0, host.Store.Revocations);
        Assert.Equal(0, host.Store.ActiveLookups);
    }

    [Theory]
    [InlineData("refresh", "{}")][InlineData("refresh", " ")][InlineData("logout", "{}")]
    [InlineData("logout", "null")]
    public async Task Cookie_only_endpoints_require_strictly_empty_body(string route, string body)
    {
        using var host = new AuthFixture();
        using var response = await host.Post(route, body, AuthFixture.InitialRefresh);
        await Error(response, 400, "ValidationError");
        Assert.Equal(0, host.Store.Rotations + host.Store.Revocations);
    }

    [Theory]
    [InlineData("refresh", "?token=22222222-2222-4222-8222-222222222222")]
    [InlineData("logout", "?refreshToken=22222222-2222-4222-8222-222222222222")]
    [InlineData("login", "?username=alice")]
    public async Task Auth_writes_reject_query_material(string route, string query)
    {
        using var host = new AuthFixture();
        using var response = await host.Post(route + query, route == "login" ? "{\"username\":\"alice\",\"password\":\"correct-secret\"}" : null, AuthFixture.InitialRefresh);
        await Error(response, 400, "ValidationError");
        Assert.Equal(0, host.Store.CredentialLookups + host.Store.Rotations + host.Store.Revocations);
    }

    [Theory]
    [InlineData("X-Refresh-Token")][InlineData("Refresh-Token")]
    public async Task Refresh_rejects_header_token_substitution(string header)
    {
        using var host = new AuthFixture();
        using var request = host.PostRequest("refresh");
        request.Headers.Add(header, AuthFixture.InitialRefresh);
        using var response = await host.Server.HttpClient.SendAsync(request);
        await Error(response, 400, "ValidationError");
        Assert.Equal(0, host.Store.Rotations);
    }

    [Theory]
    [InlineData(null)][InlineData("bad")][InlineData("22222222222242228222222222222222")]
    [InlineData("22222222-2222-4222-8222-222222222222; __Host-mydmsvn-rt=22222222-2222-4222-8222-222222222222")]
    public async Task Refresh_rejects_absent_malformed_or_duplicate_cookie(string? cookie)
    {
        using var host = new AuthFixture();
        using var response = await host.Post("refresh", null, cookie);
        await Error(response, 401, "Auth.Unauthorized");
        Assert.Equal(0, host.Store.Rotations);
    }

    [Fact]
    public async Task Logout_is_idempotent_and_revoked_session_denies_unexpired_access()
    {
        using var host = new AuthFixture();
        using var login = await host.Login();
        var token = (string)JObject.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
        using (var me = await host.Get("me", token)) Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using (var logout = await host.Post("logout", null, AuthFixture.InitialRefresh)) Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        using (var logout = await host.Post("logout"))
        {
            Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
            Assert.Equal("__Host-mydmsvn-rt=; Max-Age=0; Path=/; Secure; HttpOnly; SameSite=Strict", Assert.Single(logout.Headers.GetValues("Set-Cookie")));
        }
        using var denied = await host.Get("me", token);
        await Error(denied, 401, "Auth.Unauthorized");
    }

    [Fact]
    public async Task Me_and_capabilities_use_authoritative_actor_and_live_independent_permissions()
    {
        using var host = new AuthFixture();
        using var login = await host.Login();
        var token = (string)JObject.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
        using var me = await host.Get("me", token);
        Assert.Equal("authoritative-alice", (string?)JObject.Parse(await me.Content.ReadAsStringAsync())["data"]!["username"]);
        using var capabilities = await host.Get("capabilities?feature=sales", token);
        Assert.Equal(HttpStatusCode.OK, capabilities.StatusCode);
        Assert.Equal("{\"read\":true,\"write\":false,\"post\":true}", JObject.Parse(await capabilities.Content.ReadAsStringAsync())["data"]!.ToString(Newtonsoft.Json.Formatting.None));
        host.Store.ReadAllowed = false;
        using var revoked = await host.Get("capabilities?feature=sales", token);
        Assert.False((bool)JObject.Parse(await revoked.Content.ReadAsStringAsync())["data"]!["read"]!);
    }

    [Theory]
    [InlineData("capabilities")][InlineData("capabilities?feature=other")]
    [InlineData("capabilities?feature=sales&feature=sales")][InlineData("capabilities?feature=sales&unknown=1")]
    [InlineData("me?token=secret")]
    public async Task Auth_reads_reject_unsupported_or_duplicate_query(string route)
    {
        using var host = new AuthFixture();
        using var login = await host.Login();
        var token = (string)JObject.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
        using var response = await host.Get(route, token);
        await Error(response, 400, "ValidationError");
    }

    [Theory]
    [InlineData("login")][InlineData("refresh")]
    public async Task Bounded_auth_throttle_stops_attempts_and_reopens_after_backoff(string route)
    {
        using var host = new AuthFixture();
        var statuses = new List<int>();
        for (var attempt = 0; attempt < 61; attempt++)
        {
            using var response = await host.Post(route, route == "login" ? "{\"username\":\"alice\",\"password\":\"wrong-secret\"}" : null, AuthFixture.InitialRefresh);
            statuses.Add((int)response.StatusCode);
        }
        Assert.Contains(429, statuses);
        Assert.True(host.Store.CredentialLookups < 61 && host.Store.Rotations < 61);
        host.Clock.Now = host.Clock.Now.AddMinutes(6);
        using var after = await host.Post(route, route == "login" ? "{\"username\":\"alice\",\"password\":\"wrong-secret\"}" : null, AuthFixture.InitialRefresh);
        Assert.NotEqual(429, (int)after.StatusCode);
    }

    [Theory]
    [InlineData("refresh", false, "WebAuthRefresh", SecurityAuditOutcome.Succeeded)]
    [InlineData("refresh", true, "WebAuthRefresh", SecurityAuditOutcome.Denied)]
    [InlineData("logout", false, "WebAuthLogout", SecurityAuditOutcome.Succeeded)]
    public async Task Cookie_auth_outcomes_are_audited_without_token_material(string route, bool revoked, string category, SecurityAuditOutcome outcome)
    {
        using var host = new AuthFixture();
        host.Store.Revoked = revoked;
        using var request = host.PostRequest(route);
        request.Headers.Add("Cookie", "__Host-mydmsvn-rt=" + AuthFixture.InitialRefresh);
        request.Headers.Add("X-Correlation-ID", "cookie-auth-123");
        using var response = await host.Server.HttpClient.SendAsync(request);
        Assert.Equal(revoked ? 401 : 200, (int)response.StatusCode);
        var audit = Assert.Single(host.Store.Audits);
        Assert.Equal(category, audit.DocumentType);
        Assert.Equal(outcome, audit.Outcome);
        Assert.Null(audit.DocumentId);
        Assert.Equal("cookie-auth-123", audit.CorrelationId);
    }

    [Fact]
    public async Task Login_IP_limit_applies_when_attacker_changes_accounts()
    {
        using var host = new AuthFixture();
        for (var attempt = 0; attempt < 30; attempt++)
        {
            using var response = await host.Post("login", "{\"username\":\"missing-" + attempt + "\",\"password\":\"wrong-secret\"}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var blocked = await host.Login();
        await Error(blocked, 429, "Auth.RateLimited");
        Assert.Equal("WebAuthThrottled", host.Store.Audits.Last().DocumentType);
        Assert.Equal(SecurityAuditOutcome.Denied, host.Store.Audits.Last().Outcome);
        Assert.True(blocked.Headers.RetryAfter.Delta > TimeSpan.Zero);
    }
    [Theory]
    [InlineData("../AUTH/logout", "POST")][InlineData("logout/", "POST")]
    [InlineData("../AUTH/refresh", "POST")][InlineData("refresh/", "POST")]
    [InlineData("../AUTH/login", "POST")][InlineData("login/", "POST")]
    [InlineData("logout", "post")]
    public async Task Cookie_endpoint_route_variants_cannot_bypass_CSRF_with_valid_Bearer(string route, string method)
    {
        using var host = new AuthFixture();
        using var login = await host.Login();
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var bearer = (string)JObject.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
        using var request = host.PostRequest(route, route.TrimEnd('/').EndsWith("login", StringComparison.Ordinal) ? "{\"username\":\"alice\",\"password\":\"correct-secret\"}" : null);
        request.Headers.Remove("X-Requested-With");
        request.Method = new HttpMethod(method);
        request.Headers.Add("Authorization", "Bearer " + bearer);
        request.Headers.Add("Cookie", "__Host-mydmsvn-rt=" + AuthFixture.InitialRefresh);
        using var response = await host.Server.HttpClient.SendAsync(request);
        await Error(response, 403, "Auth.Forbidden");
        Assert.Equal(0, host.Store.Rotations + host.Store.Revocations);
        Assert.Equal(1, host.Store.Issues);
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Rejected_auth_attempts_cannot_fill_worker_capacity_and_starve_independent_callers(bool exhaustedIp)
    {
        using var host = new AuthFixture();
        var allowed = exhaustedIp ? 30 : 10;
        for (var attempt = 0; attempt < allowed + 4096; attempt++)
        {
            var account = exhaustedIp ? "missing-" + attempt : "alice";
            using var request = host.PostRequest("login", "{\"username\":\"" + account + "\",\"password\":\"wrong-secret\"}");
            request.Headers.Add("X-Test-Remote-IP", exhaustedIp ? "192.0.2.1" : "198.51." + (attempt / 256) + "." + (attempt % 256));
            using var response = await host.Server.HttpClient.SendAsync(request);
            Assert.Equal(attempt < allowed ? 401 : 429, (int)response.StatusCode);
        }
        using var independent = host.PostRequest("login", "{\"username\":\"independent\",\"password\":\"wrong-secret\"}");
        independent.Headers.Add("X-Test-Remote-IP", "203.0.113.1");
        using var admitted = await host.Server.HttpClient.SendAsync(independent);
        await Error(admitted, 401, "Auth.InvalidCredentials");
    }
    internal static async Task Error(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.True(response.Headers.CacheControl.NoStore);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret", body);
        Assert.DoesNotContain(AuthFixture.InitialRefresh, body);
        Assert.Equal(code, (string?)JObject.Parse(body)["error"]!["code"]);
    }
}

internal sealed class AuthFixture : IDisposable
{
    public const string Origin = "https://web2.mydmsvn.test";
    public const string InitialRefresh = "22222222-2222-4222-8222-222222222222";
    public const string ChildRefresh = "33333333-3333-4333-8333-333333333333";
    public readonly AuthState Store = new AuthState();
    public readonly AuthClock Clock = new AuthClock();
    private readonly ServiceProvider _provider;
    public WebHostOptions Options { get; }
    public TestServer Server { get; }
    public AuthFixture()
    {
        Options = new WebHostOptions(Origin, Origin, "auth-tests", "active", new Dictionary<string, byte[]> { ["active"] = Encoding.UTF8.GetBytes("active-test-key-at-least-32-bytes-long"), ["previous"] = Encoding.UTF8.GetBytes("previous-test-key-at-least-32-bytes-long") });
        var services = new ServiceCollection();
        services.AddServerApplication().AddHttpRequestIdentity();
        services.AddSingleton<IUtcClock>(Clock);
        services.AddSingleton(Store);
        services.AddSingleton<ISecurityAuditSink>(Store);
        services.AddSingleton<IUserStore>(Store);
        services.AddSingleton<IWebAuthSessionStore>(Store);
        services.AddSingleton<IPermissionStore>(Store);
        services.AddSingleton<IUserAccountValidator>(Store);
        services.AddSingleton<IPasswordHasher, TestPasswords>();
        services.AddSingleton<ILegacyPasswordVerifier, TestPasswords>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Server = TestServer.Create(app =>
        {
            // The worker receives this value from the socket in production, never from a client header.
            app.Use(async (context, next) =>
            {
                context.Request.RemoteIpAddress = context.Request.Headers.Get("X-Test-Remote-IP") ?? "192.0.2.1";
                await next();
            });
            Web2Pipeline.Configure(app, Options, _provider, config => config.Services.Replace(typeof(IAssembliesResolver), new AuthAssemblies()));
        });
    }
    public HttpRequestMessage PostRequest(string route, string? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Origin + "/api/v1/auth/" + route);
        request.Headers.Add("Origin", Origin); request.Headers.Add("X-Requested-With", "XMLHttpRequest"); request.Headers.Add("Sec-Fetch-Site", "same-origin");
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }
    public Task<HttpResponseMessage> Login() => Post("login", "{\"username\":\"alice\",\"password\":\"correct-secret\"}");
    public async Task<HttpResponseMessage> Post(string route, string? body = null, string? refresh = null)
    {
        using var request = PostRequest(route, body);
        if (refresh != null) request.Headers.Add("Cookie", "__Host-mydmsvn-rt=" + refresh);
        return await Server.HttpClient.SendAsync(request);
    }
    public async Task<HttpResponseMessage> Get(string route, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Origin + "/api/v1/auth/" + route);
        request.Headers.Add("Authorization", "Bearer " + token);
        return await Server.HttpClient.SendAsync(request);
    }
    public void Dispose() { Server.Dispose(); _provider.Dispose(); }
    private sealed class AuthAssemblies : DefaultAssembliesResolver
    { public override ICollection<System.Reflection.Assembly> GetAssemblies() => new[] { typeof(HealthController).Assembly }; }
    private sealed class TestPasswords : IPasswordHasher, ILegacyPasswordVerifier
    {
        public string Algorithm => "test";
        public string Hash(string password) => password;
        public bool Verify(string password, string passwordHash) => password == "correct-secret" && passwordHash == "test-hash";
        public bool NeedsRehash(string passwordHash) => false;
        public void VerifyForTiming(string password) { }
        public bool Supports(string algorithm) => false;
        public bool Verify(string algorithm, string password, string passwordHash, string passwordSalt) => false;
    }
}
internal sealed class AuthClock : IUtcClock
{
    public DateTime Now = new DateTime(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc);
    public DateTime UtcNow => Now;
}
internal sealed class AuthState : IWebAuthSessionStore, IUserStore, IPermissionStore, IUserAccountValidator, ISecurityAuditSink
{
    private static readonly Guid Session = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public int Issues, Rotations, Revocations, CredentialLookups, ActiveLookups;
    public bool FailIssue, Revoked, ReadAllowed = true;
    public readonly List<SecurityAuditEntry> Audits = new List<SecurityAuditEntry>();
    public Task WriteAsync(SecurityAuditEntry entry, CancellationToken cancellationToken) { Audits.Add(entry); return Task.CompletedTask; }
    public Task<UserAccount?> FindByNormalizedUsernameAsync(string name, CancellationToken cancellationToken)
    { CredentialLookups++; return Task.FromResult(name == "ALICE" || name == "DISABLED" ? new UserAccount(1, "alice", "ALICE", "Alice", name == "ALICE", "test-hash", "", "test") : null); }
    public Task<bool> TryReplacePasswordAsync(PasswordReplacement replacement, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<WebAuthSessionToken?> IssueAsync(int userId, CancellationToken cancellationToken)
    { Issues++; if (FailIssue) throw new InvalidOperationException("secret SQL connection"); return Task.FromResult<WebAuthSessionToken?>(Token(AuthFixture.InitialRefresh)); }
    public Task<WebAuthSessionToken?> RotateAsync(string refreshToken, CancellationToken cancellationToken)
    { Rotations++; return Task.FromResult<WebAuthSessionToken?>(Revoked ? null : Token(AuthFixture.ChildRefresh)); }
    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken) { Revocations++; Revoked = true; return Task.CompletedTask; }
    public Task<CurrentUser?> GetActiveUserAsync(Guid sessionId, int userId, CancellationToken cancellationToken)
    { ActiveLookups++; return Task.FromResult<CurrentUser?>(Revoked || sessionId != Session || userId != 1 ? null : CurrentUser.Authenticated(1, "authoritative-alice", "Authoritative Alice", true)); }
    public Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken) => Task.FromResult(!Revoked);
    public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string permissionKey, CancellationToken cancellationToken)
    { Assert.Equal(1, userId); return Task.FromResult(new PermissionSnapshot(permissionKey == "Sales.Receipts.Read" ? ReadAllowed : permissionKey == "Sales.Receipts.Post", true)); }
    private static WebAuthSessionToken Token(string refresh) => new WebAuthSessionToken(Session, CurrentUser.Authenticated(1, "authoritative-alice", "Authoritative Alice", true), refresh, AuthFixtureTime.AddDays(7), AuthFixtureTime.AddDays(30));
    private static DateTime AuthFixtureTime => new DateTime(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc);
}
