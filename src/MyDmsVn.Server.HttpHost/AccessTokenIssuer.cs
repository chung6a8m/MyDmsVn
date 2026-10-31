using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.HttpHost;

internal sealed class AccessTokenIssuer
{
    private readonly WebHostOptions _options;
    private readonly IUtcClock _clock;
    public AccessTokenIssuer(WebHostOptions options, IUtcClock clock) { _options = options; _clock = clock; }
    public AccessToken Issue(WebAuthSessionToken session)
    {
        var now = new DateTimeOffset(_clock.UtcNow).ToUnixTimeSeconds();
        var expiry = Math.Min(now + _options.AccessLifetimeMinutes * 60, new DateTimeOffset(session.ExpiresAtUtc).ToUnixTimeSeconds());
        if (expiry <= now || !session.User.IsAuthenticated || !session.User.IsActive || session.User.UserId <= 0 || !session.User.UserId.HasValue || session.SessionId == Guid.Empty)
            throw new InvalidOperationException("An active web session is required.");
        var credentials = new SigningCredentials(new SymmetricSecurityKey(_options.FindKey(_options.ActiveKeyId)!) { KeyId = _options.ActiveKeyId }, SecurityAlgorithms.HmacSha256);
        var payload = new JwtPayload
        {
            ["iss"] = _options.Issuer, ["aud"] = _options.Audience,
            ["sub"] = session.User.UserId.Value.ToString(CultureInfo.InvariantCulture),
            ["sid"] = session.SessionId.ToString("D"), ["jti"] = Guid.NewGuid().ToString("D"),
            ["iat"] = now, ["nbf"] = now, ["exp"] = expiry,
        };
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload)),
            DateTimeOffset.FromUnixTimeSeconds(expiry).UtcDateTime);
    }
}
internal sealed class AccessToken
{
    public AccessToken(string value, DateTime expiresAtUtc) { Value = value; ExpiresAtUtc = expiresAtUtc; }
    public string Value { get; }
    public DateTime ExpiresAtUtc { get; }
}
