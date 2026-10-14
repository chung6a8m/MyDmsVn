using System;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness.Ownership;
using Xunit;

namespace MyDmsVn.SqlTestHarness.Tests.Ownership;

public sealed class DatabaseOwnershipReconcilerTests
{
    [Fact]
    public async Task Missing_live_database_is_absent()
    {
        var result = await DatabaseOwnershipReconciler.ClassifyAsync(
            Record(DatabaseOwnershipState.Created),
            liveDatabase: null,
            currentRunId: "run-001");

        Assert.Equal(DatabaseRecoveryClassification.Absent, result.Classification);
        Assert.False(result.MayAutomaticallyClean);
    }

    [Fact]
    public async Task Exact_created_record_marker_and_metadata_are_positively_owned()
    {
        var result = await DatabaseOwnershipReconciler.ClassifyAsync(
            Record(DatabaseOwnershipState.Created),
            Live(),
            currentRunId: "run-001");

        Assert.Equal(DatabaseRecoveryClassification.PositivelyOwned, result.Classification);
        Assert.True(result.MayAutomaticallyClean);
    }

    [Theory]
    [InlineData(DatabaseOwnershipState.CreateIntent)]
    [InlineData(DatabaseOwnershipState.Quarantined)]
    [InlineData(DatabaseOwnershipState.Disposed)]
    public async Task Crash_window_or_terminal_records_are_ambiguous_when_database_exists(
        DatabaseOwnershipState state)
    {
        var result = await DatabaseOwnershipReconciler.ClassifyAsync(
            Record(state),
            Live(),
            currentRunId: "run-001");

        Assert.Equal(DatabaseRecoveryClassification.Ambiguous, result.Classification);
        Assert.False(result.MayAutomaticallyClean);
    }

    [Fact]
    public async Task Missing_or_wrong_marker_is_unowned()
    {
        var withoutMarker = await DatabaseOwnershipReconciler.ClassifyAsync(
            Record(DatabaseOwnershipState.Created),
            new LiveDatabaseOwnership(
                42,
                new DateTimeOffset(2026, 10, 14, 1, 2, 0, TimeSpan.Zero),
                marker: null),
            currentRunId: "run-001");
        var wrongMarker = await DatabaseOwnershipReconciler.ClassifyAsync(
            Record(DatabaseOwnershipState.Created),
            Live(new DatabaseOwnershipMarker("other-run", "other-token")),
            currentRunId: "run-001");

        Assert.Equal(DatabaseRecoveryClassification.Unowned, withoutMarker.Classification);
        Assert.Equal(DatabaseRecoveryClassification.Unowned, wrongMarker.Classification);
        Assert.False(withoutMarker.MayAutomaticallyClean);
        Assert.False(wrongMarker.MayAutomaticallyClean);
    }

    [Fact]
    public async Task Stale_database_id_or_create_date_is_ambiguous()
    {
        var wrongId = await DatabaseOwnershipReconciler.ClassifyAsync(
            Record(DatabaseOwnershipState.Created),
            Live(databaseId: 999),
            currentRunId: "run-001");
        var wrongDate = await DatabaseOwnershipReconciler.ClassifyAsync(
            Record(DatabaseOwnershipState.Created),
            Live(createDateUtc: new DateTimeOffset(2026, 10, 14, 2, 0, 0, TimeSpan.Zero)),
            currentRunId: "run-001");

        Assert.Equal(DatabaseRecoveryClassification.Ambiguous, wrongId.Classification);
        Assert.Equal(DatabaseRecoveryClassification.Ambiguous, wrongDate.Classification);
        Assert.False(wrongId.MayAutomaticallyClean);
        Assert.False(wrongDate.MayAutomaticallyClean);
    }

    [Fact]
    public async Task Matching_database_owned_by_another_active_run_is_not_cleanable()
    {
        var record = Record(DatabaseOwnershipState.Created, runId: "run-other");
        var live = Live(new DatabaseOwnershipMarker("run-other", "ownership-0"));

        var result = await DatabaseOwnershipReconciler.ClassifyAsync(
            record,
            live,
            currentRunId: "run-001");

        Assert.Equal(DatabaseRecoveryClassification.ActiveOtherRun, result.Classification);
        Assert.False(result.MayAutomaticallyClean);
    }

    private static DatabaseOwnershipRecord Record(
        DatabaseOwnershipState state,
        string runId = "run-001") =>
        new DatabaseOwnershipRecord(
            runId,
            "operation-0",
            "MyDmsVn_Test_00000000000000000000000000000000",
            "ownership-0",
            "instance-fingerprint",
            "approval-001",
            state,
            new DateTimeOffset(2026, 10, 14, 1, 2, 3, TimeSpan.Zero),
            databaseId: 42,
            databaseCreateDateUtc: new DateTimeOffset(2026, 10, 14, 1, 2, 0, TimeSpan.Zero));

    private static LiveDatabaseOwnership Live(
        DatabaseOwnershipMarker? marker = null,
        int databaseId = 42,
        DateTimeOffset? createDateUtc = null) =>
        new LiveDatabaseOwnership(
            databaseId,
            createDateUtc ?? new DateTimeOffset(2026, 10, 14, 1, 2, 0, TimeSpan.Zero),
            marker ?? new DatabaseOwnershipMarker("run-001", "ownership-0"));
}
