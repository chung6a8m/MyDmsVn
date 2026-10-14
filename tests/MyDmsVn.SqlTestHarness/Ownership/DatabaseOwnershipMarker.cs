using System;
using Newtonsoft.Json;

namespace MyDmsVn.SqlTestHarness.Ownership;

public sealed class DatabaseOwnershipMarker
{
    public DatabaseOwnershipMarker(string runId, string ownershipToken)
    {
        RunId = string.IsNullOrWhiteSpace(runId)
            ? throw new ArgumentException("A run ID is required.", nameof(runId))
            : runId;
        OwnershipToken = string.IsNullOrWhiteSpace(ownershipToken)
            ? throw new ArgumentException("An ownership token is required.", nameof(ownershipToken))
            : ownershipToken;
    }

    public string RunId { get; }

    public string OwnershipToken { get; }

    public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

    public static DatabaseOwnershipMarker? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var marker = JsonConvert.DeserializeObject<DatabaseOwnershipMarker>(value!);
            return marker is null ||
                   string.IsNullOrWhiteSpace(marker.RunId) ||
                   string.IsNullOrWhiteSpace(marker.OwnershipToken)
                ? null
                : marker;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
