using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness.Timing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.SqlTestHarness.Tests.Timing;

public sealed class SqlTestOperationScopeTests
{
    [Fact]
    public void Successful_scope_emits_every_required_field()
    {
        var sink = new RecordingSink();
        var context = Context(sink, enabled: true);

        using (context.StartOperation("migration", "migration-1", "create-1"))
        {
        }

        var actual = Assert.Single(sink.Events);
        Assert.Equal("run-1", actual.RunId);
        Assert.Equal("Tests.Class.Method#row-a", actual.TestCaseId);
        Assert.Equal("migration-1", actual.OperationId);
        Assert.Equal("create-1", actual.ParentOperationId);
        Assert.Equal("migration", actual.Stage);
        Assert.Equal(SqlTestTimingOutcome.Succeeded, actual.Outcome);
        Assert.Equal("net8.0", actual.Tfm);
        Assert.Equal("db-opaque", actual.AnonymizedDatabaseId);
        Assert.True(actual.EndTimestamp >= actual.StartTimestamp);
        Assert.True(actual.DurationMs >= 0m);
    }

    [Fact]
    public void Failure_and_cancellation_outcomes_are_explicit()
    {
        var sink = new RecordingSink();
        var context = Context(sink, enabled: true);

        using (var failed = context.StartOperation("migration", "migration-failed"))
        {
            failed.Fail();
        }

        using (var canceled = context.StartOperation("database-drop", "drop-canceled"))
        {
            canceled.Cancel();
        }

        var events = sink.Events.ToArray();
        Assert.Equal(SqlTestTimingOutcome.Failed, events[0].Outcome);
        Assert.Equal(SqlTestTimingOutcome.Canceled, events[1].Outcome);
    }

    [Fact]
    public void Disabled_context_emits_no_event()
    {
        var sink = new RecordingSink();
        var context = Context(sink, enabled: false);

        using (context.StartOperation("migration", "migration-1"))
        {
        }

        Assert.Empty(sink.Events);
    }

    [Fact]
    public void Enabled_context_records_expected_migration_invocation_separately()
    {
        var sink = new RecordingSink();
        var context = Context(sink, enabled: true);

        context.RecordMigrationInvocation("migration-expected");

        var invocation = Assert.Single(sink.Invocations);
        Assert.Equal("run-1", invocation.RunId);
        Assert.Equal("Tests.Class.Method#row-a", invocation.TestCaseId);
        Assert.Equal("migration-expected", invocation.InvocationId);
        Assert.Equal("net8.0", invocation.Tfm);
    }

    [Fact]
    public async Task Concurrent_json_lines_are_complete_and_exclude_sensitive_values()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MyDmsVn.TimingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "timings.jsonl");
            var tasks = Enumerable.Range(0, 12)
                .Select(index => Task.Run(() =>
                {
                    var sink = new JsonLinesSqlTestTimingSink(path);
                    using (Context(sink, enabled: true).StartOperation("migration", "operation-" + index))
                    {
                    }
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            var lines = File.ReadAllLines(path);
            Assert.Equal(12, lines.Length);
            Assert.All(lines, line => Assert.Equal("run-1", JObject.Parse(line)["runId"]!.Value<string>()));
            var joined = string.Join("\n", lines);
            Assert.DoesNotContain("Server=", joined, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password", joined, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static SqlTestMeasurementContext Context(ISqlTestTimingSink sink, bool enabled) =>
        SqlTestMeasurementContext.Create(
            "run-1",
            "Tests.Class.Method#row-a",
            "net8.0",
            "db-opaque",
            sink,
            enabled);

    private sealed class RecordingSink : ISqlTestTimingSink, ISqlTestInvocationSink
    {
        public ConcurrentQueue<SqlTestTimingEvent> Events { get; } =
            new ConcurrentQueue<SqlTestTimingEvent>();

        public void Write(SqlTestTimingEvent timingEvent) => Events.Enqueue(timingEvent);

        public ConcurrentQueue<SqlTestMigrationInvocation> Invocations { get; } =
            new ConcurrentQueue<SqlTestMigrationInvocation>();

        public void WriteInvocation(SqlTestMigrationInvocation invocation) =>
            Invocations.Enqueue(invocation);
    }
}
