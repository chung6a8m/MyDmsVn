using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MyDmsVn.Server.Application.Identity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

public sealed class BearerTokenValidator
{
    private readonly WebHostOptions _options;
    private readonly IUtcClock _clock;
    public BearerTokenValidator(WebHostOptions options, IUtcClock clock) { _options = options; _clock = clock; }

    public ValidatedBearer? Validate(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 8192) return null;
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3 || parts.Any(p => p.Length == 0 || p.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))) return null;
            var header = ReadObject(parts[0]);
            var payload = ReadObject(parts[1]);
            if ((string?)header["alg"] != SecurityAlgorithms.HmacSha256 || header["kid"]?.Type != JTokenType.String ||
                header.Properties().Any(p => p.Name != "alg" && p.Name != "kid" && p.Name != "typ") ||
                (header["typ"] != null && (string?)header["typ"] != "JWT")) return null;
            var keyId = (string)header["kid"]!;
            var key = _options.FindKey(keyId);
            if (key == null || payload["iss"]?.Type != JTokenType.String || payload["aud"]?.Type != JTokenType.String ||
                (string?)payload["iss"] != _options.Issuer || (string?)payload["aud"] != _options.Audience ||
                payload["sub"]?.Type != JTokenType.String || payload["sid"]?.Type != JTokenType.String || payload["jti"]?.Type != JTokenType.String) return null;
            var sub = (string)payload["sub"]!;
            if (!int.TryParse(sub, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0 ||
                sub != userId.ToString(CultureInfo.InvariantCulture) || !CanonicalGuid((string)payload["sid"]!, out var sessionId) ||
                !CanonicalGuid((string)payload["jti"]!, out _)) return null;
            if (!NumericDate(payload["iat"], out var issued) || !NumericDate(payload["nbf"], out var notBefore) ||
                !NumericDate(payload["exp"], out var expiry)) return null;
            var now = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeSeconds();
            var skew = _options.ClockSkewSeconds;
            if (issued > now + skew || notBefore > now + skew || expiry <= now - skew ||
                expiry <= issued || expiry <= notBefore || expiry - issued > _options.AccessLifetimeMinutes * 60 || notBefore < issued - skew) return null;
            var validator = new JwtSecurityTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = 8192 };
            validator.ValidateToken(token, new TokenValidationParameters
            {
                RequireSignedTokens = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(key) { KeyId = keyId },
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, ValidateIssuer = true, ValidIssuer = _options.Issuer,
                ValidateAudience = true, ValidAudience = _options.Audience, RequireAudience = true, RequireExpirationTime = true,
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(skew),
                LifetimeValidator = (nbf, exp, _, _) => nbf.HasValue && exp.HasValue && notBefore <= now + skew && expiry > now - skew,
            }, out _);
            return new ValidatedBearer(userId, sessionId);
        }
        catch (Exception exception) when (exception is SecurityTokenException || exception is ArgumentException ||
            exception is JsonException || exception is FormatException || exception is OverflowException)
        { return null; }
    }
    private static JObject ReadObject(string encoded)
    {
        var json = new UTF8Encoding(false, true).GetString(Base64UrlEncoder.DecodeBytes(encoded));
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 8 };
        var value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read()) throw new JsonReaderException();
        return value;
    }
    private static bool NumericDate(JToken? value, out long result)
    {
        result = 0;
        return value?.Type == JTokenType.Integer && long.TryParse(value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= 0 && result <= 253402300799;
    }
    private static bool CanonicalGuid(string value, out Guid result) => Guid.TryParseExact(value, "D", out result) && result != Guid.Empty && value == result.ToString("D");
}
public sealed class ValidatedBearer
{
    internal ValidatedBearer(int userId, Guid sessionId) { UserId = userId; SessionId = sessionId; }
    public int UserId { get; }
    public Guid SessionId { get; }
}
