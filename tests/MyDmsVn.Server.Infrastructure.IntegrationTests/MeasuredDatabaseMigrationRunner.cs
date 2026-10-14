using System;
using System.Collections.Generic;
using System.Linq;
using DbUp.Engine;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.SqlTestHarness.Timing;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal static class MeasuredDatabaseMigrationRunner
{
    public static DatabaseUpgradeResult Migrate(
        SqlTestMeasurementContext context,
        string invocationId,
        string connectionString) =>
        Execute(
            context,
            invocationId,
            () => new DatabaseMigrationRunner().Migrate(connectionString));

    public static DatabaseUpgradeResult Execute(
        SqlTestMeasurementContext context,
        string invocationId,
        Func<DatabaseUpgradeResult> migration)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (migration is null)
        {
            throw new ArgumentNullException(nameof(migration));
        }

        context.RecordMigrationInvocation(invocationId);
        using var timing = context.StartOperation("migration", invocationId);
        try
        {
            var result = migration();
            if (!result.Successful)
            {
                timing.Fail();
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            timing.Cancel();
            throw;
        }
        catch
        {
            timing.Fail();
            throw;
        }
    }

    public static T Execute<T>(
        SqlTestMeasurementContext context,
        string invocationId,
        Func<T> migration)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (migration is null)
        {
            throw new ArgumentNullException(nameof(migration));
        }

        context.RecordMigrationInvocation(invocationId);
        using var timing = context.StartOperation("migration", invocationId);
        try
        {
            return migration();
        }
        catch (OperationCanceledException)
        {
            timing.Cancel();
            throw;
        }
        catch
        {
            timing.Fail();
            throw;
        }
    }
}

internal static class MigrationTimingCoverage
{
    public static MigrationTimingCoverageResult Reconcile(
        IEnumerable<string> expectedInvocationIds,
        IEnumerable<SqlTestTimingEvent> events)
    {
        var expected = expectedInvocationIds.ToList();
        var actual = events
            .Where(item => string.Equals(item.Stage, "migration", StringComparison.Ordinal))
            .Select(item => item.OperationId)
            .ToList();
        var expectedCounts = expected.GroupBy(item => item, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var actualCounts = actual.GroupBy(item => item, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var allIds = expectedCounts.Keys.Concat(actualCounts.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var missing = new List<string>();
        var unexpected = new List<string>();
        var coveredCount = 0;
        foreach (var invocationId in allIds)
        {
            expectedCounts.TryGetValue(invocationId, out var expectedCount);
            actualCounts.TryGetValue(invocationId, out var actualCount);
            coveredCount += Math.Min(expectedCount, actualCount);
            missing.AddRange(Enumerable.Repeat(invocationId, Math.Max(0, expectedCount - actualCount)));
            unexpected.AddRange(Enumerable.Repeat(invocationId, Math.Max(0, actualCount - expectedCount)));
        }

        return new MigrationTimingCoverageResult(
            expected.Count,
            coveredCount,
            missing,
            unexpected);
    }
}

internal sealed class MigrationTimingCoverageResult
{
    public MigrationTimingCoverageResult(
        int expectedCount,
        int coveredCount,
        IReadOnlyList<string> missingInvocationIds,
        IReadOnlyList<string> unexpectedInvocationIds)
    {
        ExpectedCount = expectedCount;
        CoveredCount = coveredCount;
        MissingInvocationIds = missingInvocationIds;
        UnexpectedInvocationIds = unexpectedInvocationIds;
    }

    public int ExpectedCount { get; }

    public int CoveredCount { get; }

    public IReadOnlyList<string> MissingInvocationIds { get; }

    public IReadOnlyList<string> UnexpectedInvocationIds { get; }

    public bool IsComplete =>
        ExpectedCount == CoveredCount &&
        MissingInvocationIds.Count == 0 &&
        UnexpectedInvocationIds.Count == 0;
}
