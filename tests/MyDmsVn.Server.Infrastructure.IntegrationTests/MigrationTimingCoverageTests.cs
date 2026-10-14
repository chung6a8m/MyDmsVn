using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MyDmsVn.SqlTestHarness.Timing;
using Newtonsoft.Json;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class MigrationTimingCoverageTests
{
    [Fact]
    public void Audited_sources_match_the_reviewed_migration_manifest()
    {
        var root = FindRepositoryRoot();
        var manifestPath = Path.Combine(root, "tests", "tools", "sqlperf-t01-migration-invocations.json");
        var entries = JsonConvert.DeserializeObject<List<MigrationInvocationManifestEntry>>(
            File.ReadAllText(manifestPath))!;

        Assert.NotEmpty(entries);
        Assert.Equal(entries.Count, entries.Select(entry => entry.InvocationId).Distinct().Count());

        foreach (var group in entries.GroupBy(entry => entry.SourceFile))
        {
            var source = File.ReadAllText(Path.Combine(root, group.Key.Replace('/', Path.DirectorySeparatorChar)));
            var sourceIds = Regex.Matches(
                    source,
                    "\\\"(?<id>" + Regex.Escape(group.First().Class) +
                    "\\.[A-Za-z0-9_]+\\.(?:migration-[0-9]+|direct-[A-Za-z0-9_-]+))\\\"")
                .Cast<Match>()
                .Select(match => match.Groups["id"].Value)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var manifestIds = group.Select(entry => entry.InvocationId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(manifestIds, sourceIds);
            Assert.Equal(
                group.Count(entry => entry.InvocationId.IndexOf(".direct-", StringComparison.Ordinal) >= 0),
                Regex.Matches(source, "\\.PerformUpgrade\\(\\)").Count);

            Assert.DoesNotContain("new DatabaseMigrationRunner().Migrate(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TestDatabaseMigrationRunner.Migrate(", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Reconciliation_is_incomplete_when_an_invocation_has_no_timing_event()
    {
        var result = MigrationTimingCoverage.Reconcile(
            new[] { "normal", "direct-dbup" },
            new[] { MigrationEvent("normal", SqlTestTimingOutcome.Succeeded) });

        Assert.False(result.IsComplete);
        Assert.Equal(new[] { "direct-dbup" }, result.MissingInvocationIds);
        Assert.Equal(2, result.ExpectedCount);
        Assert.Equal(1, result.CoveredCount);
    }

    [Theory]
    [InlineData(SqlTestTimingOutcome.Succeeded)]
    [InlineData(SqlTestTimingOutcome.Failed)]
    [InlineData(SqlTestTimingOutcome.Canceled)]
    public void Reconciliation_counts_all_terminal_migration_outcomes(SqlTestTimingOutcome outcome)
    {
        var result = MigrationTimingCoverage.Reconcile(
            new[] { "invocation" },
            new[] { MigrationEvent("invocation", outcome) });

        Assert.True(result.IsComplete);
        Assert.Equal(1, result.CoveredCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Measured_runner_records_thrown_failure_and_cancellation(bool canceled)
    {
        var sink = new RecordingSink();
        var context = SqlTestMeasurementContext.Create(
            "run",
            "test",
            "net8.0",
            "database",
            sink,
            enabled: true);

        if (canceled)
        {
            Assert.Throws<OperationCanceledException>(() =>
                MeasuredDatabaseMigrationRunner.Execute<int>(
                    context,
                    "canceled",
                    () => throw new OperationCanceledException()));
            Assert.Equal(SqlTestTimingOutcome.Canceled, Assert.Single(sink.Events).Outcome);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() =>
                MeasuredDatabaseMigrationRunner.Execute<int>(
                    context,
                    "failed",
                    () => throw new InvalidOperationException()));
            Assert.Equal(SqlTestTimingOutcome.Failed, Assert.Single(sink.Events).Outcome);
        }
    }

    private static SqlTestTimingEvent MigrationEvent(
        string invocationId,
        SqlTestTimingOutcome outcome) =>
        new SqlTestTimingEvent(
            "run",
            "test",
            invocationId,
            parentOperationId: null,
            "migration",
            1,
            2,
            1m,
            outcome,
            "net8.0",
            "database");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MyDmsVn.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class MigrationInvocationManifestEntry
    {
        public string InvocationId { get; set; } = string.Empty;

        public string Class { get; set; } = string.Empty;

        public string SourceFile { get; set; } = string.Empty;
    }

    private sealed class RecordingSink : ISqlTestTimingSink
    {
        public ConcurrentQueue<SqlTestTimingEvent> Events { get; } =
            new ConcurrentQueue<SqlTestTimingEvent>();

        public void Write(SqlTestTimingEvent timingEvent) => Events.Enqueue(timingEvent);
    }
}
