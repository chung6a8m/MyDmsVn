using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using MediatR;
using Microsoft.Owin;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

[RoutePrefix("api/v1/auth")]
public sealed class AuthController : ApiController
{
    internal const string OptionsKey = "mydmsvn.authOptions";
    internal const string LimiterKey = "mydmsvn.authLimiter";
    private const string CookieName = "__Host-mydmsvn-rt";
    // The login grammar admits only a strict JSON object with string values. Json.NET by itself
    // also accepts comments, trailing commas, single quotes and unquoted names.
    private const string JsonString = "\"(?:[^\"\\\\\\x00-\\x1f]|\\\\(?:[\"\\\\/bfnrt]|u[0-9a-fA-F]{4}))*\"";
    private static readonly Regex LoginGrammar = new Regex("\\A[ \\t\\r\\n]*\\{[ \\t\\r\\n]*" + JsonString + "[ \\t\\r\\n]*:[ \\t\\r\\n]*" + JsonString + "[ \\t\\r\\n]*,[ \\t\\r\\n]*" + JsonString + "[ \\t\\r\\n]*:[ \\t\\r\\n]*" + JsonString + "[ \\t\\r\\n]*\\}[ \\t\\r\\n]*\\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private readonly IMediator _mediator;
    private readonly IWebAuthSessionStore _sessions;
    private readonly IUtcClock _clock;
    private readonly IPermissionAuthorizationService _permissions;
    private readonly IRequestCorrelationAccessor _correlation;
    private readonly ISecurityAuditWriter _audit;
    public AuthController(IMediator mediator, IWebAuthSessionStore sessions, IUtcClock clock,
        IPermissionAuthorizationService permissions, IRequestCorrelationAccessor correlation, ISecurityAuditWriter audit)
    { _mediator = mediator; _sessions = sessions; _clock = clock; _permissions = permissions; _correlation = correlation; _audit = audit; }
    private IOwinContext Context => Request.GetOwinContext();

    [HttpPost, Route("login")]
    public async Task<HttpResponseMessage> Login(CancellationToken cancellationToken)
    {
        if (!NoQueryOrTokenHeaders()) return Invalid();
        var credentials = await ReadCredentials().ConfigureAwait(false);
        if (credentials == null) return Invalid();
        if (!Admit("login", UsernameNormalizer.Normalize((string)credentials["username"]!))) return await Throttled(cancellationToken).ConfigureAwait(false);
        var result = await _mediator.Send(new LoginCommand((string)credentials["username"]!, (string)credentials["password"]!), cancellationToken).ConfigureAwait(false);
        if (result.IsError) return HttpJson.From(ApiResponseMapper.Map(result, null, _correlation.CorrelationId));
        var session = await _sessions.IssueAsync(result.Value.UserId, cancellationToken).ConfigureAwait(false);
        return session == null ? AuthenticationRequired() : Authenticated(session);
    }

    [HttpPost, Route("refresh")]
    public async Task<HttpResponseMessage> Refresh(CancellationToken cancellationToken)
    {
        if (!NoQueryOrTokenHeaders() || !await EmptyBody().ConfigureAwait(false)) return Invalid();
        var cookie = ReadCookie();
        if (!Admit("refresh", cookie ?? "missing-or-invalid")) return await Throttled(cancellationToken).ConfigureAwait(false);
        if (cookie == null)
        {
            await Audit("WebAuthRefresh", SecurityAuditOutcome.Denied, null, false, cancellationToken).ConfigureAwait(false);
            return AuthenticationRequired();
        }
        var session = await _sessions.RotateAsync(cookie, cancellationToken).ConfigureAwait(false);
        await Audit("WebAuthRefresh", session == null ? SecurityAuditOutcome.Denied : SecurityAuditOutcome.Succeeded,
            session?.User.UserId, session != null, session == null ? cancellationToken : CancellationToken.None).ConfigureAwait(false);
        return session == null ? AuthenticationRequired() : Authenticated(session);
    }

    [HttpPost, Route("logout")]
    public async Task<HttpResponseMessage> Logout(CancellationToken cancellationToken)
    {
        if (!NoQueryOrTokenHeaders() || !await EmptyBody().ConfigureAwait(false)) return Invalid();
        var cookie = ReadCookie();
        if (cookie != null) await _sessions.RevokeAsync(cookie, cancellationToken).ConfigureAwait(false);
        await Audit("WebAuthLogout", SecurityAuditOutcome.Succeeded, null, cookie != null, cookie == null ? cancellationToken : CancellationToken.None).ConfigureAwait(false);
        var response = HttpJson.Success(new { });
        WriteCookie(response, null);
        return response;
    }

    [HttpGet, Route("me")]
    public async Task<HttpResponseMessage> Me(CancellationToken cancellationToken)
    {
        if (!NoQueryOrTokenHeaders() || !await EmptyBody().ConfigureAwait(false)) return Invalid();
        return HttpJson.From(ApiResponseMapper.Map(await _mediator.Send(new GetCurrentUserQuery(), cancellationToken).ConfigureAwait(false), null, _correlation.CorrelationId));
    }

    [HttpGet, Route("capabilities")]
    public async Task<HttpResponseMessage> Capabilities(CancellationToken cancellationToken)
    {
        if (HasTokenHeaders() || !await EmptyBody().ConfigureAwait(false)) return Invalid();
        string permission;
        switch (Request.RequestUri.Query)
        {
            case "?feature=sales": permission = "Sales.Receipts"; break;
            case "?feature=goods-receipts": permission = "Inventory.GoodsReceipts"; break;
            case "?feature=inventory-balances":
                return HttpJson.Success(new { read = await _permissions.IsAllowedAsync("Inventory.Balances.Read", cancellationToken).ConfigureAwait(false) });
            case "?feature=inventory-stock-card":
                return HttpJson.Success(new { read = await _permissions.IsAllowedAsync("Inventory.StockCard.Read", cancellationToken).ConfigureAwait(false) });
            default: return Invalid();
        }
        var read = await _permissions.IsAllowedAsync(permission + ".Read", cancellationToken).ConfigureAwait(false);
        var write = await _permissions.IsAllowedAsync(permission + ".Write", cancellationToken).ConfigureAwait(false);
        var post = await _permissions.IsAllowedAsync(permission + ".Post", cancellationToken).ConfigureAwait(false);
        return HttpJson.Success(new { read, write, post });
    }

    private async Task<JObject?> ReadCredentials()
    {
        if (Request.Content == null || Request.Content.Headers.ContentType?.MediaType != "application/json" ||
            (!string.IsNullOrEmpty(Request.Content.Headers.ContentType.CharSet) && !string.Equals(Request.Content.Headers.ContentType.CharSet.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase)) ||
            Request.Content.Headers.ContentEncoding.Any()) return null;
        try
        {
            var json = new UTF8Encoding(false, true).GetString(await Request.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
            if (!LoginGrammar.IsMatch(json)) return null;
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 2 };
            var value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (value.Count != 2 || value["username"]?.Type != JTokenType.String || value["password"]?.Type != JTokenType.String) return null;
            var username = (string)value["username"]!;
            var password = (string)value["password"]!;
            return !string.IsNullOrWhiteSpace(username) && username.Length <= 256 && password.Length > 0 && Encoding.UTF8.GetByteCount(password) <= 72 ? value : null;
        }
        catch (Exception exception) when (exception is JsonException || exception is DecoderFallbackException || exception is RegexMatchTimeoutException) { return null; }
    }
    private async Task<bool> EmptyBody() => Request.Content == null || (await Request.Content.ReadAsByteArrayAsync().ConfigureAwait(false)).Length == 0;
    private bool NoQueryOrTokenHeaders() => string.IsNullOrEmpty(Request.RequestUri.Query) && !HasTokenHeaders();
    private bool HasTokenHeaders() => Request.Headers.Contains("X-Refresh-Token") || Request.Headers.Contains("Refresh-Token");
    private bool Admit(string operation, string subject) => Context.Get<AuthAttemptLimiter>(LimiterKey)
        .TryAdmit(operation, subject, Context.Request.RemoteIpAddress ?? "unknown", _clock.UtcNow);
    private HttpResponseMessage Invalid() => HttpJson.Error(400, "ValidationError", "The request is invalid.", _correlation.CorrelationId!);
    private HttpResponseMessage AuthenticationRequired() => HttpJson.Error(401, "Auth.Unauthorized", "Authentication is required.", _correlation.CorrelationId!);
    private Task Audit(string category, SecurityAuditOutcome outcome, int? userId, bool committed, CancellationToken cancellationToken) =>
        _audit.WriteAsync(new SecurityAuditEntry(SecurityAuditAction.Authentication, outcome, userId, _clock.UtcNow, category), committed, cancellationToken);
    private async Task<HttpResponseMessage> Throttled(CancellationToken cancellationToken)
    {
        await Audit("WebAuthThrottled", SecurityAuditOutcome.Denied, null, false, cancellationToken).ConfigureAwait(false);
        var response = HttpJson.Error(429, "Auth.RateLimited", "Too many authentication attempts. Try again later.", _correlation.CorrelationId!);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
        return response;
    }
    private string? ReadCookie()
    {
        var values = Context.Request.Headers.GetValues("Cookie");
        if (values == null) return null;
        var matches = values.SelectMany(v => v.Split(';')).Select(v => v.Trim()).Where(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1) return null;
        var token = matches[0].Substring(CookieName.Length + 1);
        return Guid.TryParseExact(token, "D", out var guid) && guid != Guid.Empty && token == guid.ToString("D") && token[14] == '4' && "89ab".Contains(token[19]) ? token : null;
    }
    private HttpResponseMessage Authenticated(WebAuthSessionToken session)
    {
        var token = new AccessTokenIssuer(Context.Get<WebHostOptions>(OptionsKey), _clock).Issue(session);
        var user = session.User;
        var response = HttpJson.Success(new
        {
            accessToken = token.Value,
            expiresAtUtc = token.ExpiresAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            user = new CurrentUserDto(user.UserId!.Value, user.Username!, user.DisplayName!),
        });
        WriteCookie(response, session);
        return response;
    }
    private void WriteCookie(HttpResponseMessage response, WebAuthSessionToken? session)
    {
        var seconds = session == null ? 0 : Math.Max(0, (long)Math.Floor((session.ExpiresAtUtc - _clock.UtcNow).TotalSeconds));
        // Explicit serialization avoids framework-version SameSite/default path differences.
        response.Headers.TryAddWithoutValidation("Set-Cookie", CookieName + "=" + (session?.RefreshToken ?? "") + "; Max-Age=" + seconds.ToString(CultureInfo.InvariantCulture) + "; Path=/; Secure; HttpOnly; SameSite=Strict");
    }
}
