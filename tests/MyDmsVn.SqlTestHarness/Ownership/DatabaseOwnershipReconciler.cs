using System;
using System.Threading.Tasks;

namespace MyDmsVn.SqlTestHarness.Ownership;

public static class DatabaseOwnershipReconciler
{
    public static Task<DatabaseRecoveryResult> ClassifyAsync(
        DatabaseOwnershipRecord record,
        LiveDatabaseOwnership? liveDatabase,
        string currentRunId)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        if (string.IsNullOrWhiteSpace(currentRunId))
        {
            throw new ArgumentException("A current run ID is required.", nameof(currentRunId));
        }

        if (liveDatabase is null)
        {
            return Result(DatabaseRecoveryClassification.Absent, "The database is absent.");
        }

        var marker = liveDatabase.Marker;
        if (marker is null ||
            !string.Equals(marker.RunId, record.RunId, StringComparison.Ordinal) ||
            !string.Equals(marker.OwnershipToken, record.OwnershipToken, StringComparison.Ordinal))
        {
            return Result(
                DatabaseRecoveryClassification.Unowned,
                "The live database has no matching ownership marker.");
        }

        if (record.DatabaseId != liveDatabase.DatabaseId ||
            record.DatabaseCreateDateUtc != liveDatabase.CreateDateUtc)
        {
            return Result(
                DatabaseRecoveryClassification.Ambiguous,
                "The live database metadata does not match the durable ownership record.");
        }

        if (!string.Equals(record.RunId, currentRunId, StringComparison.Ordinal))
        {
            return Result(
                DatabaseRecoveryClassification.ActiveOtherRun,
                "The database belongs to another run.");
        }

        if (record.State != DatabaseOwnershipState.Created &&
            record.State != DatabaseOwnershipState.DropIntent &&
            record.State != DatabaseOwnershipState.CleanupFailed)
        {
            return Result(
                DatabaseRecoveryClassification.Ambiguous,
                "The durable state does not authorize automatic cleanup.");
        }

        return Result(
            DatabaseRecoveryClassification.PositivelyOwned,
            "The durable record, marker, and live metadata match.");
    }

    private static Task<DatabaseRecoveryResult> Result(
        DatabaseRecoveryClassification classification,
        string reason) =>
        Task.FromResult(new DatabaseRecoveryResult(classification, reason));
}
