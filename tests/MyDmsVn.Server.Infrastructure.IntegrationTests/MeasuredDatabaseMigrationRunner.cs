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
        var covered = new HashSet<string>(
            events.Where(item => string.Equals(item.Stage, "migration", StringComparison.Ordinal))
                .Select(item => item.OperationId),
            StringComparer.Ordinal);
        var missing = expected.Where(item => !covered.Contains(item)).Distinct().ToArray();
        return new MigrationTimingCoverageResult(expected.Count, expected.Count - missing.Length, missing);
    }
}

internal sealed class MigrationTimingCoverageResult
{
    public MigrationTimingCoverageResult(
        int expectedCount,
        int coveredCount,
        IReadOnlyList<string> missingInvocationIds)
    {
        ExpectedCount = expectedCount;
        CoveredCount = coveredCount;
        MissingInvocationIds = missingInvocationIds;
    }

    public int ExpectedCount { get; }

    public int CoveredCount { get; }

    public IReadOnlyList<string> MissingInvocationIds { get; }

    public bool IsComplete => ExpectedCount == CoveredCount && MissingInvocationIds.Count == 0;
}
