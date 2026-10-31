using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyDmsVn.Server.HttpHost;

public sealed class WebHostOptions
{
    private readonly IReadOnlyDictionary<string, byte[]> _keys;
    public WebHostOptions(string publicOrigin, string issuer, string audience, string activeKeyId,
        IReadOnlyDictionary<string, byte[]> validationKeys, int clockSkewSeconds = 30,
        int maximumRequestBytes = 1048576, int accessLifetimeMinutes = 10)
    {
        if (!Uri.TryCreate(publicOrigin, UriKind.Absolute, out var origin) || origin.Scheme != "https" ||
            origin.UserInfo.Length != 0 || publicOrigin != origin.GetLeftPart(UriPartial.Authority))
            throw new ArgumentException("An exact HTTPS public origin is required.", nameof(publicOrigin));
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri) || issuerUri.Scheme != "https" ||
            issuerUri.UserInfo.Length != 0 || issuer.Length > 256 || issuerUri.Query.Length != 0 || issuerUri.Fragment.Length != 0)
            throw new ArgumentException("An HTTPS issuer is required.", nameof(issuer));
        if (string.IsNullOrWhiteSpace(audience) || audience.Length > 128 || audience.Any(char.IsWhiteSpace))
            throw new ArgumentException("An audience is required.", nameof(audience));
        if (validationKeys == null || validationKeys.Count == 0 || validationKeys.Count > 5 ||
            validationKeys.Any(pair => !ValidKeyId(pair.Key) || pair.Value == null || pair.Value.Length < 32) ||
            !ValidKeyId(activeKeyId) || !validationKeys.ContainsKey(activeKeyId))
            throw new ArgumentException("A bounded allowlist of signing keys is required.", nameof(validationKeys));
        if (clockSkewSeconds < 0 || clockSkewSeconds > 30) throw new ArgumentOutOfRangeException(nameof(clockSkewSeconds));
        if (maximumRequestBytes < 1024 || maximumRequestBytes > 1048576) throw new ArgumentOutOfRangeException(nameof(maximumRequestBytes));
        if (accessLifetimeMinutes < 1 || accessLifetimeMinutes > 15) throw new ArgumentOutOfRangeException(nameof(accessLifetimeMinutes));
        PublicOrigin = publicOrigin; Issuer = issuer; Audience = audience; ActiveKeyId = activeKeyId;
        ClockSkewSeconds = clockSkewSeconds; MaximumRequestBytes = maximumRequestBytes; AccessLifetimeMinutes = accessLifetimeMinutes;
        _keys = new ReadOnlyDictionary<string, byte[]>(validationKeys.ToDictionary(p => p.Key, p => (byte[])p.Value.Clone(), StringComparer.Ordinal));
    }
    public string PublicOrigin { get; }
    public string Issuer { get; }
    public string Audience { get; }
    public string ActiveKeyId { get; }
    public int ClockSkewSeconds { get; }
    public int MaximumRequestBytes { get; }
    public int AccessLifetimeMinutes { get; }
    internal byte[]? FindKey(string keyId) => _keys.TryGetValue(keyId, out var value) ? (byte[])value.Clone() : null;
    private static bool ValidKeyId(string value) => value != null && Regex.IsMatch(value, "\\A[A-Za-z0-9_-]{1,64}\\z");

    public static WebHostOptions FromEnvironment()
    {
        try
        {
            string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException();
            var keys = Required("MYDMSVN_WEB_JWT_KEYS").Split(',').Select(entry => entry.Split(':'))
                .ToDictionary(parts => parts.Length == 2 ? parts[0] : throw new FormatException(), parts => Convert.FromBase64String(parts[1]), StringComparer.Ordinal);
            return new WebHostOptions(Required("MYDMSVN_WEB_ORIGIN"), Required("MYDMSVN_WEB_JWT_ISSUER"),
                Required("MYDMSVN_WEB_JWT_AUDIENCE"), Required("MYDMSVN_WEB_JWT_ACTIVE_KID"), keys);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is InvalidOperationException || exception is IndexOutOfRangeException)
        { throw new InvalidOperationException("WEB2 configuration is missing or invalid."); }
    }
}
