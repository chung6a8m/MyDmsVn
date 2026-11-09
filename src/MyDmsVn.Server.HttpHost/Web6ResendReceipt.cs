using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Operations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

internal sealed class Web6ResendIdentity
{
    private readonly byte[] _fingerprint;

    internal Web6ResendIdentity(int actorUserId, string candidateId, Guid deploymentEpoch, Guid operationId,
        WebOperationKind kind, long? requestTargetId, byte[] fingerprint)
    {
        var operation = operationId.ToString("D");
        var create = kind == WebOperationKind.SalesCreate || kind == WebOperationKind.ReceiptCreate;
        if (actorUserId <= 0 || candidateId == null ||
            !Regex.IsMatch(candidateId, "\\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\\z", RegexOptions.CultureInvariant) ||
            deploymentEpoch == Guid.Empty || operationId == Guid.Empty || operation[14] != '4' || "89ab".IndexOf(operation[19]) < 0 ||
            !Enum.IsDefined(typeof(WebOperationKind), kind) || (create ? requestTargetId.HasValue : !requestTargetId.HasValue || requestTargetId <= 0) ||
            fingerprint == null || fingerprint.Length != 32)
            throw new ArgumentException("A complete canonical WEB6 operation identity is required.");
        ActorUserId = actorUserId;
        CandidateId = candidateId;
        DeploymentEpoch = deploymentEpoch;
        OperationId = operationId;
        Kind = kind;
        RequestTargetId = requestTargetId;
        _fingerprint = (byte[])fingerprint.Clone();
    }

    internal int ActorUserId { get; }
    internal string CandidateId { get; }
    internal Guid DeploymentEpoch { get; }
    internal Guid OperationId { get; }
    internal WebOperationKind Kind { get; }
    internal string OperationKind => WebOperationWireInput.KindName(Kind);
    internal string Method => WebOperationWireInput.Method(Kind);
    internal string RouteScope => WebOperationWireInput.RouteScope(Kind);
    internal long? RequestTargetId { get; }
    internal byte[] Fingerprint => (byte[])_fingerprint.Clone();
    internal string FingerprintHex => WebOperationWireInput.FingerprintHex(_fingerprint);
}

internal sealed class IssuedWeb6ResendReceipt
{
    internal IssuedWeb6ResendReceipt(string token, DateTime issuedAtUtc, DateTime eligibleUntilUtc)
    { Token = token; IssuedAtUtc = issuedAtUtc; EligibleUntilUtc = eligibleUntilUtc; }
    internal string Token { get; }
    internal DateTime IssuedAtUtc { get; }
    internal DateTime EligibleUntilUtc { get; }
}

internal sealed class ValidatedWeb6ResendReceipt
{
    internal ValidatedWeb6ResendReceipt(DateTime issuedAtUtc, DateTime eligibleUntilUtc, bool expired)
    { IssuedAtUtc = issuedAtUtc; EligibleUntilUtc = eligibleUntilUtc; IsExpired = expired; }
    internal DateTime IssuedAtUtc { get; }
    internal DateTime EligibleUntilUtc { get; }
    internal bool IsExpired { get; }
}

internal sealed class Web6ResendReceiptCodec
{
    internal const string ContractVersion = "web6-resend-eligibility-v1";
    internal const string TokenType = "MYDMSVN-WEB6-RESEND-RECEIPT";
    internal const int MaximumReceiptLength = 4096;
    internal const long LifetimeSeconds = 604800;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    private static readonly string[] HeaderNames = { "alg", "kid", "typ" };
    private static readonly string[] PayloadNames = { "aud", "candidate", "epoch", "exp", "fingerprint", "iat", "iss", "kind", "method", "nbf", "operation", "route", "sub", "target" };
    private static readonly byte[] Purpose = Utf8.GetBytes("MyDmsVn.Web6.ResendEligibility.SigningKey.v1");
    private readonly WebHostOptions _options;
    private readonly IUtcClock _clock;

    internal Web6ResendReceiptCodec(WebHostOptions options, IUtcClock clock)
    { _options = options ?? throw new ArgumentNullException(nameof(options)); _clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

    internal IssuedWeb6ResendReceipt Issue(Web6ResendIdentity identity)
    {
        if (!_options.Web6ResendEligibilityEnabled || _options.Web6CandidateId != identity.CandidateId)
            throw new WebOperationException("Operation.Unavailable");
        var now = NumericNow();
        var expiry = checked(now + LifetimeSeconds);
        var root = _options.FindKey(_options.ActiveKeyId) ?? throw new WebOperationException("Operation.Unavailable");
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Derive(root)) { KeyId = _options.ActiveKeyId }, SecurityAlgorithms.HmacSha256);
        var header = new JwtHeader(credentials) { ["typ"] = TokenType };
        var payload = new JwtPayload
        {
            ["iss"] = _options.Issuer,
            ["aud"] = Audience(),
            ["sub"] = identity.ActorUserId.ToString(CultureInfo.InvariantCulture),
            ["candidate"] = identity.CandidateId,
            ["epoch"] = identity.DeploymentEpoch.ToString("D"),
            ["operation"] = identity.OperationId.ToString("D"),
            ["fingerprint"] = identity.FingerprintHex,
            ["kind"] = identity.OperationKind,
            ["method"] = identity.Method,
            ["route"] = identity.RouteScope,
            ["target"] = identity.RequestTargetId.HasValue ? identity.RequestTargetId.Value.ToString(CultureInfo.InvariantCulture) : null,
            ["iat"] = now,
            ["nbf"] = now,
            ["exp"] = expiry
        };
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
        if (token.Length > MaximumReceiptLength) throw new WebOperationException("Operation.Unavailable");
        return new IssuedWeb6ResendReceipt(token, FromNumeric(now), FromNumeric(expiry));
    }

    internal ValidatedWeb6ResendReceipt Validate(string compactReceipt, Web6ResendIdentity expected)
    {
        if (string.IsNullOrEmpty(compactReceipt) || compactReceipt.Length > MaximumReceiptLength || compactReceipt.Any(c => c > 127))
            throw new WireInputException();
        try
        {
            var parts = compactReceipt.Split('.');
            if (parts.Length != 3 || parts.Any(part => part.Length == 0 || part.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_')))
                throw new WireInputException();
            var header = ReadObject(parts[0]);
            var payload = ReadObject(parts[1]);
            ExactProperties(header, HeaderNames);
            ExactProperties(payload, PayloadNames);
            if ((string?)header["alg"] != SecurityAlgorithms.HmacSha256 || (string?)header["typ"] != TokenType || header["kid"]?.Type != JTokenType.String)
                throw new WireInputException();
            var keyId = (string)header["kid"]!;
            var root = _options.FindKey(keyId) ?? throw new WireInputException();
            new JwtSecurityTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = MaximumReceiptLength }.ValidateToken(compactReceipt,
                new TokenValidationParameters
                {
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Derive(root)) { KeyId = keyId },
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience(),
                    RequireAudience = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = false
                }, out _);

            var actor = CanonicalPositiveInt(payload["sub"]);
            var epoch = CanonicalGuid(payload["epoch"], false);
            var operation = CanonicalGuid(payload["operation"], true);
            var fingerprint = CanonicalFingerprint(payload["fingerprint"]);
            var kind = CanonicalKind(payload["kind"]);
            var target = CanonicalTarget(payload["target"], kind);
            var issued = NumericDate(payload["iat"]);
            var notBefore = NumericDate(payload["nbf"]);
            var expiry = NumericDate(payload["exp"]);
            var now = NumericNow();
            if (issued != notBefore || expiry - issued != LifetimeSeconds || issued > now + _options.ClockSkewSeconds ||
                (string?)payload["candidate"] != _options.Web6CandidateId || (string?)payload["iss"] != _options.Issuer ||
                (string?)payload["aud"] != Audience() || (string?)payload["method"] != WebOperationWireInput.Method(kind) ||
                (string?)payload["route"] != WebOperationWireInput.RouteScope(kind))
                throw new WireInputException();

            if (actor != expected.ActorUserId) throw new WebOperationException("Auth.Unauthorized");
            if ((string?)payload["candidate"] != expected.CandidateId || operation != expected.OperationId || kind != expected.Kind || target != expected.RequestTargetId)
                throw new WebOperationException("Idempotency.KeyConflict");
            if (epoch != expected.DeploymentEpoch) throw new WebOperationException("Deployment.EpochMismatch");
            if (!fingerprint.SequenceEqual(expected.Fingerprint)) throw new WebOperationException("Idempotency.FingerprintMismatch");
            return new ValidatedWeb6ResendReceipt(FromNumeric(issued), FromNumeric(expiry), now >= expiry);
        }
        catch (WireInputException) { throw; }
        catch (WebOperationException) { throw; }
        catch (Exception error) when (error is SecurityTokenException || error is ArgumentException || error is JsonException ||
            error is FormatException || error is OverflowException || error is CryptographicException)
        { throw new WireInputException(); }
    }

    private string Audience() => _options.Audience + ":" + ContractVersion;
    private long NumericNow() => new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeSeconds();
    private static DateTime FromNumeric(long value) => DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime;
    private static byte[] Derive(byte[] root) { using var hmac = new HMACSHA256(root); return hmac.ComputeHash(Purpose); }

    private static JObject ReadObject(string encoded)
    {
        var json = Utf8.GetString(Base64UrlEncoder.DecodeBytes(encoded));
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 8 };
        var value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read()) throw new JsonReaderException();
        return value;
    }

    private static void ExactProperties(JObject value, IEnumerable<string> expected)
    {
        if (!value.Properties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new WireInputException();
    }

    private static long NumericDate(JToken? value)
    {
        if (value?.Type != JTokenType.Integer || !long.TryParse(value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result < 0 || result > 253402300799)
            throw new WireInputException();
        return result;
    }

    private static int CanonicalPositiveInt(JToken? value)
    {
        var text = value?.Type == JTokenType.String ? (string?)value : null;
        if (text == null || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result <= 0 || text != result.ToString(CultureInfo.InvariantCulture))
            throw new WireInputException();
        return result;
    }

    private static Guid CanonicalGuid(JToken? value, bool v4)
    {
        var text = value?.Type == JTokenType.String ? (string?)value : null;
        if (text == null || !Guid.TryParseExact(text, "D", out var result) || result == Guid.Empty || text != result.ToString("D") ||
            (v4 && (text[14] != '4' || "89ab".IndexOf(text[19]) < 0)))
            throw new WireInputException();
        return result;
    }

    private static byte[] CanonicalFingerprint(JToken? value)
    {
        var text = value?.Type == JTokenType.String ? (string?)value : null;
        if (text == null || !Regex.IsMatch(text, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant))
            throw new WireInputException();
        return Enumerable.Range(0, 32).Select(index => byte.Parse(text.Substring(index * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)).ToArray();
    }

    private static WebOperationKind CanonicalKind(JToken? value)
    {
        var text = value?.Type == JTokenType.String ? (string?)value : null;
        if (text == null) throw new WireInputException();
        return WebOperationWireInput.ParseKind(text);
    }

    private static long? CanonicalTarget(JToken? value, WebOperationKind kind)
    {
        var create = kind == WebOperationKind.SalesCreate || kind == WebOperationKind.ReceiptCreate;
        if (create) { if (value?.Type != JTokenType.Null) throw new WireInputException(); return null; }
        var text = value?.Type == JTokenType.String ? (string?)value : null;
        if (text == null || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result <= 0 || text != result.ToString(CultureInfo.InvariantCulture))
            throw new WireInputException();
        return result;
    }
}
