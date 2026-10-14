using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace MyDmsVn.SqlTestHarness.Approval;

public static class SqlInstanceApprovalLoader
{
    private const int SupportedSchemaVersion = 1;

    public static SqlInstanceApproval Load(string path) =>
        Load(path, DateTimeOffset.UtcNow);

    public static SqlInstanceApproval Load(string path, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("The SQL instance approval file path is required.");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The SQL instance approval file does not exist.");
        }

        ApprovalDocument document;
        string rawJson;
        try
        {
            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error,
            };
            rawJson = File.ReadAllText(path);
            document = JsonConvert.DeserializeObject<ApprovalDocument>(rawJson, settings)
                ?? throw new InvalidOperationException("The SQL instance approval file must contain valid JSON.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The SQL instance approval file must contain valid JSON with the exact supported schema.",
                exception);
        }

        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported SQL instance approval schemaVersion '{document.SchemaVersion}'.");
        }

        if (document.NonProductionAttestation != true)
        {
            throw new InvalidOperationException(
                "The SQL instance approval requires an explicit non-production attestation.");
        }

        var approvalId = document.ApprovalId;
        if (string.IsNullOrWhiteSpace(approvalId))
        {
            throw new InvalidOperationException("The SQL instance approvalId is required.");
        }

        if (document.Identity is null)
        {
            throw new InvalidOperationException("The SQL instance approval identity is required.");
        }

        DateTimeOffset? validUntilUtc = null;
        if (!string.IsNullOrWhiteSpace(document.ValidUntilUtc))
        {
            if (!DateTimeOffset.TryParse(
                    document.ValidUntilUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsedValidUntil))
            {
                throw new InvalidOperationException("The SQL instance approval validUntilUtc must be ISO-8601 UTC.");
            }

            validUntilUtc = parsedValidUntil;
            if (validUntilUtc <= utcNow)
            {
                throw new InvalidOperationException("The SQL instance approval has expired.");
            }
        }

        SqlInstanceIdentity identity;
        try
        {
            identity = new SqlInstanceIdentity(
                document.Identity.ServerName ?? string.Empty,
                document.Identity.MachineName ?? string.Empty,
                document.Identity.InstanceName,
                document.Identity.Endpoint);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("The SQL instance approval identity is incomplete.", exception);
        }

        var actualFingerprint = SqlInstanceFingerprint.Create(identity);
        if (string.IsNullOrWhiteSpace(document.FingerprintSha256) ||
            !string.Equals(document.FingerprintSha256, actualFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The SQL instance approval fingerprint does not match its declared identity.");
        }

        return new SqlInstanceApproval(
            approvalId!,
            identity,
            actualFingerprint,
            validUntilUtc,
            ComputeSha256(rawJson));
    }

    private static string ComputeSha256(string value)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private sealed class ApprovalDocument
    {
        [JsonProperty("schemaVersion", Required = Required.Always)]
        public int SchemaVersion { get; set; }

        [JsonProperty("approvalId", Required = Required.Always)]
        public string? ApprovalId { get; set; }

        [JsonProperty("nonProductionAttestation", Required = Required.AllowNull)]
        public bool? NonProductionAttestation { get; set; }

        [JsonProperty("validUntilUtc")]
        public string? ValidUntilUtc { get; set; }

        [JsonProperty("identity", Required = Required.Always)]
        public IdentityDocument? Identity { get; set; }

        [JsonProperty("fingerprintSha256", Required = Required.Always)]
        public string? FingerprintSha256 { get; set; }
    }

    private sealed class IdentityDocument
    {
        [JsonProperty("serverName", Required = Required.Always)]
        public string? ServerName { get; set; }

        [JsonProperty("machineName", Required = Required.Always)]
        public string? MachineName { get; set; }

        [JsonProperty("instanceName", Required = Required.AllowNull)]
        public string? InstanceName { get; set; }

        [JsonProperty("endpoint", Required = Required.AllowNull)]
        public string? Endpoint { get; set; }
    }
}
