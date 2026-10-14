using System;

namespace MyDmsVn.SqlTestHarness.Ownership;

public sealed class LiveDatabaseOwnership
{
    public LiveDatabaseOwnership(
        int databaseId,
        DateTimeOffset createDateUtc,
        DatabaseOwnershipMarker? marker)
    {
        DatabaseId = databaseId;
        CreateDateUtc = createDateUtc;
        Marker = marker;
    }

    public int DatabaseId { get; }

    public DateTimeOffset CreateDateUtc { get; }

    public DatabaseOwnershipMarker? Marker { get; }
}
