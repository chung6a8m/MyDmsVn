using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class BootstrapAuthority : IDisposable
{
    private const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";
    private static readonly string[] Keys =
    {
        "version", "candidateId", "taskParent", "sqlControlParent", "origins", "drillClasses",
        "backendSha", "frontendSha", "oldBackendSha", "oldFrontendSha", "expiresAtUtc",
    };
    private static readonly string[] AllowedDrills = { "cutover", "pair", "recovery", "workload" };
    private readonly ProtectedJsonFile? _receipt;

    private BootstrapAuthority(JObject value, DateTimeOffset now, ProtectedJsonFile? receipt = null)
    {
        CandidateId = BootstrapJson.CandidateId(value);
        TaskParent = BootstrapJson.AbsolutePath(BootstrapJson.Text(value, "taskParent"));
        SqlControlParent = BootstrapJson.AbsolutePath(BootstrapJson.Text(value, "sqlControlParent"));
        BootstrapJson.RequireSeparateRoots(TaskParent, SqlControlParent);

        var origins = BootstrapJson.Strings(value, "origins");
        if (origins.Length is < 1 or > 4 || !IsSortedDistinct(origins) || origins.Any(origin => !IsLoopbackHttpsOrigin(origin)))
            throw new IOException("Bootstrap Owner authority origins are invalid.");
        Origins = Array.AsReadOnly(origins);

        var drills = BootstrapJson.Strings(value, "drillClasses");
        if (drills.Length == 0 || !IsSortedDistinct(drills) || drills.Any(drill => !AllowedDrills.Contains(drill)))
            throw new IOException("Bootstrap Owner authority drill classes are invalid.");
        DrillClasses = Array.AsReadOnly(drills);

        BackendSha = BootstrapJson.Commit(value, "backendSha");
        FrontendSha = BootstrapJson.Commit(value, "frontendSha");
        OldBackendSha = BootstrapJson.Commit(value, "oldBackendSha");
        OldFrontendSha = BootstrapJson.Commit(value, "oldFrontendSha");
        var expiry = BootstrapJson.Text(value, "expiresAtUtc");
        if (!DateTimeOffset.TryParseExact(expiry, UtcFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var expiresAtUtc) || expiresAtUtc <= now)
            throw new IOException("Bootstrap Owner authority expiry is invalid or elapsed.");
        ExpiresAtUtc = expiresAtUtc;
        _receipt = receipt;
    }

    internal string CandidateId { get; }
    internal string TaskParent { get; }
    internal string SqlControlParent { get; }
    internal IReadOnlyList<string> Origins { get; }
    internal IReadOnlyList<string> DrillClasses { get; }
    internal string BackendSha { get; }
    internal string FrontendSha { get; }
    internal string OldBackendSha { get; }
    internal string OldFrontendSha { get; }
    internal DateTimeOffset ExpiresAtUtc { get; }

    internal string Path => Receipt.Path;
    internal string Sha256 => Receipt.Sha256;
    private ProtectedJsonFile Receipt => _receipt ?? throw new InvalidOperationException("Bootstrap authority requires a protected receipt.");

    internal static string Authorize(string input, string output, CancellationToken token)
    {
        var document = BootstrapJson.Parse(ProtectedJsonFile.ReadInput(input), Keys);
        using var authority = new BootstrapAuthority(document, DateTimeOffset.UtcNow);
        var bytes = authority.Serialize();
        token.ThrowIfCancellationRequested();
        return ProtectedJsonFile.WriteNew(output, bytes);
    }

    internal static BootstrapAuthority ReadProtected(string path, string sha256, DateTimeOffset now)
    {
        var receipt = ProtectedJsonFile.Open(path, sha256);
        try
        {
            var authority = new BootstrapAuthority(BootstrapJson.Parse(receipt.Bytes, Keys), now, receipt);
            if (!receipt.Bytes.SequenceEqual(authority.Serialize()))
                throw new IOException("Bootstrap Owner authority must use canonical encoding.");
            receipt.Verify();
            return authority;
        }
        catch
        {
            receipt.Dispose();
            throw;
        }
    }

    internal void Verify(DateTimeOffset now)
    {
        if (ExpiresAtUtc <= now)
            throw new IOException("Bootstrap Owner authority expired.");
        Receipt.Verify();
    }

    public void Dispose() => _receipt?.Dispose();

    private byte[] Serialize() => BootstrapJson.Serialize(new JObject
    {
        ["version"] = 1,
        ["candidateId"] = CandidateId,
        ["taskParent"] = TaskParent,
        ["sqlControlParent"] = SqlControlParent,
        ["origins"] = new JArray(Origins),
        ["drillClasses"] = new JArray(DrillClasses),
        ["backendSha"] = BackendSha,
        ["frontendSha"] = FrontendSha,
        ["oldBackendSha"] = OldBackendSha,
        ["oldFrontendSha"] = OldFrontendSha,
        ["expiresAtUtc"] = ExpiresAtUtc.ToString(UtcFormat, CultureInfo.InvariantCulture),
    });

    private static bool IsSortedDistinct(string[] values) =>
        values.SequenceEqual(values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal));

    private static bool IsLoopbackHttpsOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsLoopback && uri.Port is >= 1 and <= 65535 &&
        origin == "https://" + uri.Host + ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
}
