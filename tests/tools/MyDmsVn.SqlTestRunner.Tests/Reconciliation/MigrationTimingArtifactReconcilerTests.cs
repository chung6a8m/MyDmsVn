using System;
using System.IO;
using MyDmsVn.SqlTestRunner.Reconciliation;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Reconciliation;

public sealed class MigrationTimingArtifactReconcilerTests
{
    [Fact]
    public void Reconcile_requires_one_timing_event_per_expected_invocation()
    {
        var path = WriteArtifact(
            Invocation("case-a", "net8.0", "migrate"),
            Invocation("case-a", "net8.0", "migrate"),
            Timing("case-a", "net8.0", "migrate"));

        var result = MigrationTimingArtifactReconciler.Reconcile(path, "net8.0");

        Assert.False(result.IsComplete);
        Assert.Single(result.MissingKeys);
        Assert.Empty(result.UnexpectedKeys);
    }

    [Fact]
    public void Reconcile_rejects_unexpected_duplicate_and_wrong_tfm_events()
    {
        var path = WriteArtifact(
            Invocation("case-a", "net8.0", "migrate"),
            Timing("case-a", "net8.0", "migrate"),
            Timing("case-a", "net8.0", "migrate"),
            Timing("case-a", "net48", "migrate"));

        var result = MigrationTimingArtifactReconciler.Reconcile(path, "net8.0");

        Assert.False(result.IsComplete);
        Assert.Equal(2, result.UnexpectedKeys.Count);
    }

    [Fact]
    public void Reconcile_accepts_an_exact_non_empty_multiset()
    {
        var path = WriteArtifact(
            Invocation("case-a", "net48", "first"),
            Invocation("case-a", "net48", "second"),
            Timing("case-a", "net48", "second"),
            Timing("case-a", "net48", "first"));

        var result = MigrationTimingArtifactReconciler.Reconcile(path, "net48");

        Assert.True(result.IsComplete);
        Assert.Equal(2, result.ExpectedCount);
    }

    private static string WriteArtifact(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), "MyDmsVn.TimingGate." + Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static string Invocation(string testCaseId, string tfm, string invocationId) =>
        "{\"recordType\":\"migration-invocation\",\"testCaseId\":\"" + testCaseId +
        "\",\"tfm\":\"" + tfm + "\",\"invocationId\":\"" + invocationId + "\"}";

    private static string Timing(string testCaseId, string tfm, string operationId) =>
        "{\"stage\":\"migration\",\"testCaseId\":\"" + testCaseId +
        "\",\"tfm\":\"" + tfm + "\",\"operationId\":\"" + operationId + "\"}";
}
