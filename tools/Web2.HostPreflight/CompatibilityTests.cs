using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin;
using Microsoft.Owin.Host.SystemWeb;
using Microsoft.Owin.Testing;
using Owin;
using Xunit;

namespace Web2.HostPreflight;

// Executable dependency probe only: this is not production authentication or a shipped host.
public sealed class CompatibilityTests
{
    private const string Issuer = "https://web2.mydmsvn.test";
    private const string Audience = "mydmsvn-web2";
    private const string CookieName = "__Host-mydmsvn-rt";

    [Fact]
    public async Task WebApi2RunsInKatanaInMemoryWithExistingJsonVersion()
    {
        using var configuration = new HttpConfiguration();
        configuration.Routes.MapHttpRoute("preflight", "preflight", new { controller = "Preflight" });
        using var server = TestServer.Create(app => app.UseWebApi(configuration));
        using var response = await server.HttpClient.GetAsync("https://web2.mydmsvn.test/preflight");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("9007199254740993", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Hs256RoundtripLoadsAlongsideSqlClientAndStrongNamedAssemblies()
    {
        var key = NewKey();
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var token = CreateToken(key, "valid");
        var principal = handler.ValidateToken(token, Parameters(key), out var validated);
        Assert.Equal("42", principal.FindFirst("sub")?.Value);
        Assert.Equal(SecurityAlgorithms.HmacSha256, Assert.IsType<JwtSecurityToken>(validated).Header.Alg);
        // SqlClient's existing OIDC dependency must load against the host's aligned IdentityModel family.
        var oidc = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration { Issuer = Issuer };
        Assert.Equal(Issuer, oidc.Issuer);
        using var connection = new SqlConnection(new SqlConnectionStringBuilder
        {
            DataSource = "not-connected.invalid", InitialCatalog = "PreflightOnly", IntegratedSecurity = true,
        }.ConnectionString);
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
        Assert.NotEmpty(typeof(JwtSecurityTokenHandler).Assembly.GetName().GetPublicKeyToken()!);
        Assert.NotEmpty(typeof(SystemWebCookieManager).Assembly.GetName().GetPublicKeyToken()!);
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("wrong-algorithm")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("expired")]
    [InlineData("not-yet-valid")]
    [InlineData("missing-expiry")]
    [InlineData("unknown-kid")]
    [InlineData("missing-kid")]
    [InlineData("unsigned")]
    public void IdentityModelRejectsInvalidTokensWithExplicitValidationPolicy(string scenario)
    {
        var key = NewKey();
        var token = CreateToken(scenario == "wrong-key" ? NewKey() : key, scenario);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(token, Parameters(key), out _));
    }

    [Fact]
    public void SystemWebCookieManagerSupportsStrictSecureScopeAndDeletionOnNet48()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var http = new HttpContext(new HttpRequest("", Issuer + "/", ""), new HttpResponse(output));
        var context = new OwinContext();
        context.Set<HttpContextBase>(typeof(HttpContextBase).FullName, new HttpContextWrapper(http));
        var manager = new SystemWebCookieManager();
        var options = CookieOptions();
        manager.AppendResponseCookie(context, CookieName, Guid.NewGuid().ToString("D"), options);
        AssertCookie(http.Response.Cookies[CookieName]);
        var deletionHttp = new HttpContext(new HttpRequest("", Issuer + "/", ""), new HttpResponse(output));
        var deletionContext = new OwinContext();
        deletionContext.Set<HttpContextBase>(typeof(HttpContextBase).FullName, new HttpContextWrapper(deletionHttp));
        manager.DeleteCookie(deletionContext, CookieName, options);
        var deleted = deletionHttp.Response.Cookies[CookieName];
        AssertCookie(deleted);
        Assert.Equal(string.Empty, deleted.Value);
        Assert.True(deleted.Expires.ToUniversalTime() < DateTime.UtcNow);
    }

    [Fact]
    public void OwinHeaderWriterPreservesStrictCookieFlagsAndExposesMaxAgeGap()
    {
        var context = new OwinContext();
        context.Response.Cookies.Append(CookieName, Guid.NewGuid().ToString("D"), CookieOptions());
        var header = Assert.Single(context.Response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("; path=/", header);
        Assert.Contains("; secure", header);
        Assert.Contains("; HttpOnly", header);
        Assert.Contains("; SameSite=Strict", header);
        Assert.DoesNotContain("domain=", header);
        // Katana 4.2.3 cannot fulfill the contract's Max-Age using CookieOptions alone.
        Assert.DoesNotContain("max-age", header.ToLowerInvariant());
        Assert.Null(typeof(CookieOptions).GetProperty("MaxAge"));
    }

    private static void AssertCookie(HttpCookie cookie)
    {
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(System.Web.SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal("/", cookie.Path);
        Assert.True(string.IsNullOrEmpty(cookie.Domain));
    }

    private static CookieOptions CookieOptions() => new CookieOptions
    {
        Path = "/", Secure = true, HttpOnly = true, SameSite = Microsoft.Owin.SameSiteMode.Strict,
        Expires = DateTime.UtcNow.AddDays(7),
    };

    private static SymmetricSecurityKey NewKey()
    {
        var bytes = new byte[48];
        using var random = RandomNumberGenerator.Create();
        random.GetBytes(bytes);
        return new SymmetricSecurityKey(bytes) { KeyId = "preflight-key" };
    }

    private static TokenValidationParameters Parameters(SecurityKey key) => new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = Issuer,
        ValidateAudience = true, ValidAudience = Audience,
        RequireSignedTokens = true, ValidateIssuerSigningKey = true,
        RequireExpirationTime = true, ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        TryAllIssuerSigningKeys = false,
        IssuerSigningKeyResolver = (_, _, kid, _) =>
            string.Equals(kid, key.KeyId, StringComparison.Ordinal) ? new[] { key } : Array.Empty<SecurityKey>(),
    };

    private static string CreateToken(SymmetricSecurityKey key, string scenario)
    {
        var now = DateTime.UtcNow;
        key = new SymmetricSecurityKey(key.Key) { KeyId = key.KeyId };
        if (scenario == "unknown-kid") { key.KeyId = "unrecognized-key"; }
        if (scenario == "missing-kid") { key.KeyId = null; }
        var credentials = scenario == "unsigned" ? null : new SigningCredentials(key,
            scenario == "wrong-algorithm" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            scenario == "wrong-issuer" ? "https://untrusted.invalid" : Issuer,
            scenario == "wrong-audience" ? "untrusted-audience" : Audience,
            new[] { new Claim("sub", "42"), new Claim("sid", Guid.NewGuid().ToString("D")),
                new Claim("jti", Guid.NewGuid().ToString("D")),
                new Claim("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64) },
            scenario == "expired" ? now.AddHours(-2) : scenario == "not-yet-valid" ? now.AddHours(1) : now.AddMinutes(-1),
            scenario == "expired" ? now.AddHours(-1) : scenario == "missing-expiry" ? null : now.AddHours(2),
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class PreflightController : ApiController
{
    public IHttpActionResult Get() => Ok(new { saleId = "9007199254740993" });
}
