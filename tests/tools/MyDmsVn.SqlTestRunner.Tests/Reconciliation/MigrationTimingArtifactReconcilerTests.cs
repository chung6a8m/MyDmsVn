using System;
using System.IO;
using System.Linq;
using MyDmsVn.SqlTestRunner.Reconciliation;
using MyDmsVn.SqlTestRunner.Trx;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Reconciliation;

public sealed class MigrationTimingArtifactReconcilerTests
{
    [Fact]
    public void Reconcile_fails_when_an_entire_manifest_invocation_pair_is_absent()
    {
        using var fixture = new Fixture(
            Entry("ExampleTests.Case.migration-01", "Case"),
            Entry("ExampleTests.Case.migration-02", "Case"));
        fixture.WriteArtifact(
            Invocation("Case", "net8.0", "ExampleTests.Case.migration-01"),
            Timing("Case", "net8.0", "ExampleTests.Case.migration-01"));

        var result = fixture.Reconcile("net8.0", Executed("Namespace.ExampleTests.Case", "net8.0"));

        Assert.False(result.IsComplete);
        Assert.Equal(2, result.ExpectedCount);
        Assert.Contains(result.MissingKeys, key => key.EndsWith("migration-02", StringComparison.Ordinal));
    }

    [Fact]
    public void Reconcile_requires_one_timing_event_per_recorded_invocation()
    {
        using var fixture = new Fixture(Entry("ExampleTests.Case.migrate", "Case", count: 2));
        fixture.WriteArtifact(
            Invocation("Case", "net8.0", "ExampleTests.Case.migrate"),
            Invocation("Case", "net8.0", "ExampleTests.Case.migrate"),
            Timing("Case", "net8.0", "ExampleTests.Case.migrate"));

        var result = fixture.Reconcile("net8.0", Executed("Namespace.ExampleTests.Case", "net8.0"));

        Assert.False(result.IsComplete);
        Assert.Single(result.MissingKeys);
        Assert.Empty(result.UnexpectedKeys);
    }

    [Fact]
    public void Reconcile_rejects_unexpected_duplicate_and_wrong_tfm_events()
    {
        using var fixture = new Fixture(Entry("ExampleTests.Case.migrate", "Case"));
        fixture.WriteArtifact(
            Invocation("Case", "net8.0", "ExampleTests.Case.migrate"),
            Timing("Case", "net8.0", "ExampleTests.Case.migrate"),
            Timing("Case", "net8.0", "ExampleTests.Case.migrate"),
            Timing("Case", "net48", "ExampleTests.Case.migrate"));

        var result = fixture.Reconcile("net8.0", Executed("Namespace.ExampleTests.Case", "net8.0"));

        Assert.False(result.IsComplete);
        Assert.Equal(2, result.UnexpectedKeys.Count);
    }

    [Fact]
    public void Reconcile_accepts_an_exact_non_empty_manifest_multiset()
    {
        using var fixture = new Fixture(
            Entry("ExampleTests.Case.first", "Case"),
            Entry("ExampleTests.Case.second", "Case"));
        fixture.WriteArtifact(
            Invocation("Case", "net48", "ExampleTests.Case.first"),
            Invocation("Case", "net48", "ExampleTests.Case.second"),
            Timing("Case", "net48", "ExampleTests.Case.second"),
            Timing("Case", "net48", "ExampleTests.Case.first"));

        var result = fixture.Reconcile("net48", Executed("Namespace.ExampleTests.Case", "net48"));

        Assert.True(result.IsComplete);
        Assert.Equal(2, result.ExpectedCount);
    }

    [Fact]
    public void Reconcile_ignores_manifest_entries_owned_by_other_test_projects()
    {
        using var fixture = new Fixture(
            Entry("ExampleTests.Case.migrate", "Case"),
            Entry(
                "CatalogTests.CatalogCase.migrate",
                "CatalogCase",
                sourceFile: "tests/CatalogTests/CatalogTests.cs",
                className: "CatalogTests"));
        fixture.WriteArtifact(
            Invocation("Case", "net8.0", "ExampleTests.Case.migrate"),
            Timing("Case", "net8.0", "ExampleTests.Case.migrate"));

        var result = fixture.Reconcile(
            "net8.0",
            Executed("Namespace.ExampleTests.Case", "net8.0"),
            "tests/ExampleTests/ExampleTests.csproj");

        Assert.True(result.IsComplete);
        Assert.Equal(1, result.ExpectedCount);

        using var emptyScope = new Fixture();
        emptyScope.WriteArtifact();
        var emptyResult = emptyScope.Reconcile(
            "net8.0",
            Executed("Namespace.ExampleTests.Case", "net8.0"),
            "tests/ExampleTests/ExampleTests.csproj");
        Assert.True(emptyResult.IsComplete);
        Assert.Equal(0, emptyResult.ExpectedCount);

        using var unreviewedScope = new Fixture();
        unreviewedScope.WriteArtifact(
            Invocation("Case", "net8.0", "ExampleTests.Case.unreviewed"),
            Timing("Case", "net8.0", "ExampleTests.Case.unreviewed"));
        var unreviewedResult = unreviewedScope.Reconcile(
            "net8.0",
            Executed("Namespace.ExampleTests.Case", "net8.0"),
            "tests/ExampleTests/ExampleTests.csproj");
        Assert.False(unreviewedResult.IsComplete);
        Assert.NotEmpty(unreviewedResult.UnexpectedKeys);
    }

    [Fact]
    public void Reconcile_rejects_complete_pairs_from_a_different_run()
    {
        using var fixture = new Fixture(Entry("ExampleTests.Case.migrate", "Case"));
        fixture.WriteArtifact(
            Invocation("Case", "net8.0", "ExampleTests.Case.migrate", "other-run"),
            Timing("Case", "net8.0", "ExampleTests.Case.migrate", "other-run"));

        var result = fixture.Reconcile("net8.0", Executed("Namespace.ExampleTests.Case", "net8.0"));

        Assert.False(result.IsComplete);
        Assert.NotEmpty(result.UnexpectedKeys);
    }

    [Fact]
    public void Multiline_helper_caller_missing_both_records_fails_closed()
    {
        using var fixture = new Fixture(HelperEntry("ExampleTests.CreateDatabase.migrate"));
        fixture.WriteSource(
            "public class ExampleTests\n{\n" +
            "    public async Task\n" +
            "        First() { await CreateDatabase(); }\n" +
            "    public async Task Second() { await CreateDatabase(); }\n" +
            "    private Task CreateDatabase() => Task.CompletedTask;\n}");
        fixture.WriteArtifact(
            Invocation("Second", "net8.0", "ExampleTests.CreateDatabase.migrate"),
            Timing("Second", "net8.0", "ExampleTests.CreateDatabase.migrate"));

        var result = fixture.Reconcile(
            "net8.0",
            Executed("Namespace.ExampleTests.First", "net8.0")
                .Concat(Executed("Namespace.ExampleTests.Second", "net8.0"))
                .ToArray());

        Assert.False(result.IsComplete);
        Assert.Contains(result.MissingKeys, key => key.StartsWith("First|", StringComparison.Ordinal));
    }

    [Fact]
    public void Theory_rows_cannot_satisfy_each_others_migration_expectations()
    {
        using var fixture = new Fixture(Entry("ExampleTests.Case.migrate", "Case"));
        var executed = new[]
        {
            TrxTestResult.Passed("row-a", "Namespace.ExampleTests.Case", "net8.0", TimeSpan.Zero),
            TrxTestResult.Passed("row-b", "Namespace.ExampleTests.Case", "net8.0", TimeSpan.Zero),
        };
        fixture.WriteArtifact(
            Invocation("Case", "net8.0", "ExampleTests.Case.migrate"),
            Timing("Case", "net8.0", "ExampleTests.Case.migrate"),
            Invocation("Case", "net8.0", "ExampleTests.Case.migrate"),
            Timing("Case", "net8.0", "ExampleTests.Case.migrate"));

        var collapsed = fixture.Reconcile("net8.0", executed);

        Assert.False(collapsed.IsComplete);
        Assert.Contains(collapsed.MissingKeys, key => key.StartsWith("row-a|", StringComparison.Ordinal));
        Assert.Contains(collapsed.MissingKeys, key => key.StartsWith("row-b|", StringComparison.Ordinal));

        fixture.WriteArtifact(
            Invocation("row-a", "net8.0", "ExampleTests.Case.migrate"),
            Timing("row-a", "net8.0", "ExampleTests.Case.migrate"),
            Invocation("row-a", "net8.0", "ExampleTests.Case.migrate"),
            Timing("row-a", "net8.0", "ExampleTests.Case.migrate"));

        var swapped = fixture.Reconcile("net8.0", executed);

        Assert.False(swapped.IsComplete);
        Assert.Contains(swapped.MissingKeys, key => key.StartsWith("row-b|", StringComparison.Ordinal));
        Assert.Contains(swapped.UnexpectedKeys, key => key.StartsWith("row-a|", StringComparison.Ordinal));
    }

    private static string Entry(
        string invocationId,
        string method,
        int count = 1,
        string sourceFile = "tests/ExampleTests/ExampleTests.cs",
        string className = "ExampleTests") =>
        "{\"invocationId\":\"" + invocationId + "\",\"class\":\"" + className + "\"," +
        "\"method\":\"" + method + "\",\"sourceFile\":\"" + sourceFile + "\"," +
        "\"tfms\":[\"net48\",\"net8.0\"],\"expectedInvocationRule\":\"once-per-test-execution\"," +
        "\"expectedInvocations\":[{\"testMethod\":\"Case\",\"count\":" + count + "}]}";

    private static string HelperEntry(string invocationId) =>
        "{\"invocationId\":\"" + invocationId + "\",\"class\":\"ExampleTests\"," +
        "\"method\":\"CreateDatabase\",\"sourceFile\":\"tests/ExampleTests/ExampleTests.cs\"," +
        "\"tfms\":[\"net8.0\"],\"expectedInvocationRule\":\"once-per-caller-execution\"}";

    private static TrxTestResult[] Executed(string method, string tfm) =>
        new[] { TrxTestResult.Passed("case", method, tfm, TimeSpan.Zero) };

    private static string Invocation(
        string testCaseId,
        string tfm,
        string invocationId,
        string runId = "run-1") =>
        "{\"recordType\":\"migration-invocation\",\"runId\":\"" + runId +
        "\",\"testCaseId\":\"" + testCaseId +
        "\",\"tfm\":\"" + tfm + "\",\"invocationId\":\"" + invocationId + "\"}";

    private static string Timing(
        string testCaseId,
        string tfm,
        string operationId,
        string runId = "run-1") =>
        "{\"stage\":\"migration\",\"runId\":\"" + runId +
        "\",\"testCaseId\":\"" + testCaseId +
        "\",\"tfm\":\"" + tfm + "\",\"operationId\":\"" + operationId + "\"}";

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "MyDmsVn.TimingGate",
            Guid.NewGuid().ToString("N"));

        public Fixture(params string[] entries)
        {
            Directory.CreateDirectory(_root);
            ArtifactPath = Path.Combine(_root, "timing.jsonl");
            ManifestPath = Path.Combine(_root, "manifest.json");
            File.WriteAllText(ManifestPath, "[" + string.Join(",", entries) + "]");
        }

        public string ArtifactPath { get; }

        public string ManifestPath { get; }

        public void WriteArtifact(params string[] lines) => File.WriteAllLines(ArtifactPath, lines);

        public void WriteSource(string source)
        {
            var sourcePath = Path.Combine(_root, "tests", "ExampleTests", "ExampleTests.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, source);
        }

        public MigrationTimingArtifactReconciliation Reconcile(
            string tfm,
            TrxTestResult[] executed,
            string ownerProject = "tests/ExampleTests/ExampleTests.csproj") =>
            MigrationTimingArtifactReconciler.Reconcile(
                ArtifactPath,
                ManifestPath,
                _root,
                "run-1",
                tfm,
                executed,
                ownerProject);

        public void Dispose() => Directory.Delete(_root, true);
    }
}
