using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyDmsVn.SqlTestRunner.Trx;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.SqlTestRunner.Reconciliation;

public static class MigrationTimingArtifactReconciler
{
    public static MigrationTimingArtifactReconciliation Reconcile(
        string artifactPath,
        string manifestPath,
        string repositoryRoot,
        string runId,
        string tfm,
        IReadOnlyCollection<TrxTestResult> executed,
        string ownerProjectPath = "")
    {
        if (!File.Exists(artifactPath) || !File.Exists(manifestPath))
        {
            return MigrationTimingArtifactReconciliation.Invalid("missing-artifact-or-manifest");
        }

        var invalid = new List<string>();
        var entries = MigrationInvocationManifest.Read(manifestPath);
        if (!string.IsNullOrWhiteSpace(ownerProjectPath))
        {
            var ownerDirectory = Path.GetDirectoryName(Path.GetFullPath(
                Path.Combine(repositoryRoot, ownerProjectPath.Replace('/', Path.DirectorySeparatorChar))))!;
            ownerDirectory = ownerDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            entries = entries.Where(entry => Path.GetFullPath(Path.Combine(
                    repositoryRoot,
                    entry.SourceFile.Replace('/', Path.DirectorySeparatorChar)))
                .StartsWith(ownerDirectory, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var manifestEntries = entries
            .Where(entry => entry.Tfms.Contains(tfm, StringComparer.Ordinal))
            .ToArray();
        var auditedInvocationSuffixes = new HashSet<string>(
            manifestEntries.Select(entry => "|" + entry.InvocationId),
            StringComparer.Ordinal);
        var manifestExpected = BuildManifestExpectations(
            manifestEntries,
            repositoryRoot,
            tfm,
            executed,
            invalid);
        var recordedInvocations = new List<string>();
        var timingEvents = new List<string>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(artifactPath))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var record = JObject.Parse(line);
                var recordRunId = (string?)record["runId"];
                var recordTfm = (string?)record["tfm"];
                var testCaseId = (string?)record["testCaseId"];
                var recordType = (string?)record["recordType"];
                var stage = (string?)record["stage"];
                if (string.Equals(recordType, "migration-invocation", StringComparison.Ordinal))
                {
                    AddRecord(
                        recordedInvocations,
                        recordRunId,
                        recordTfm,
                        testCaseId,
                        (string?)record["invocationId"],
                        runId,
                        tfm,
                        invalid,
                        lineNumber);
                }
                else if (string.Equals(stage, "migration", StringComparison.Ordinal))
                {
                    AddRecord(
                        timingEvents,
                        recordRunId,
                        recordTfm,
                        testCaseId,
                        (string?)record["operationId"],
                        runId,
                        tfm,
                        invalid,
                        lineNumber);
                }
            }
            catch
            {
                invalid.Add("line:" + lineNumber);
            }
        }

        var missing = new List<string>();
        var unexpected = new List<string>(invalid);
        Compare(
            manifestExpected,
            recordedInvocations.Where(key => auditedInvocationSuffixes.Any(
                suffix => key.EndsWith(suffix, StringComparison.Ordinal))),
            missing,
            unexpected);
        Compare(recordedInvocations, timingEvents, missing, unexpected);
        return new MigrationTimingArtifactReconciliation(
            manifestExpected.Count,
            missing.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            unexpected.OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }

    private static List<string> BuildManifestExpectations(
        IReadOnlyCollection<MigrationInvocationManifestEntry> manifestEntries,
        string repositoryRoot,
        string tfm,
        IReadOnlyCollection<TrxTestResult> executed,
        ICollection<string> invalid)
    {
        var expected = new List<string>();
        foreach (var entry in manifestEntries)
        {
            foreach (var expectation in MigrationInvocationManifest.ResolveExpectations(entry, repositoryRoot))
            {
                var suffix = "." + entry.Class + "." + expectation.TestMethod;
                var executions = executed.Where(result =>
                        string.Equals(result.Tfm, tfm, StringComparison.Ordinal) &&
                        result.MethodName.EndsWith(suffix, StringComparison.Ordinal))
                    .ToArray();
                if (executions.Length == 0)
                {
                    invalid.Add("manifest-target-not-executed:" + entry.Class + "." + expectation.TestMethod);
                    continue;
                }

                if (executions.Length == 1)
                {
                    expected.AddRange(Enumerable.Repeat(
                        Key(expectation.TestMethod, tfm, entry.InvocationId),
                        expectation.Count));
                    continue;
                }

                foreach (var execution in executions)
                {
                    expected.AddRange(Enumerable.Repeat(
                        Key(execution.StableCaseId, tfm, entry.InvocationId),
                        expectation.Count));
                }
            }
        }

        return expected;
    }

    private static void Compare(
        IEnumerable<string> expected,
        IEnumerable<string> actual,
        List<string> missing,
        List<string> unexpected)
    {
        var expectedCounts = Counts(expected);
        var actualCounts = Counts(actual);
        foreach (var key in expectedCounts.Keys.Concat(actualCounts.Keys)
                     .Distinct(StringComparer.Ordinal))
        {
            expectedCounts.TryGetValue(key, out var expectedCount);
            actualCounts.TryGetValue(key, out var actualCount);
            missing.AddRange(Enumerable.Repeat(key, Math.Max(0, expectedCount - actualCount)));
            unexpected.AddRange(Enumerable.Repeat(key, Math.Max(0, actualCount - expectedCount)));
        }
    }

    private static void AddRecord(
        ICollection<string> destination,
        string? recordRunId,
        string? recordTfm,
        string? testCaseId,
        string? invocationId,
        string expectedRunId,
        string expectedTfm,
        ICollection<string> invalid,
        int lineNumber)
    {
        if (!string.Equals(recordRunId, expectedRunId, StringComparison.Ordinal) ||
            !string.Equals(recordTfm, expectedTfm, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(testCaseId) ||
            string.IsNullOrWhiteSpace(invocationId))
        {
            invalid.Add("line:" + lineNumber);
            return;
        }

        destination.Add(Key(testCaseId, recordTfm!, invocationId));
    }

    private static string Key(string testCaseId, string tfm, string invocationId) =>
        testCaseId + "|" + tfm + "|" + invocationId;

    private static Dictionary<string, int> Counts(IEnumerable<string> keys) =>
        keys.GroupBy(key => key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
}

public sealed class MigrationTimingArtifactReconciliation
{
    public MigrationTimingArtifactReconciliation(
        int expectedCount,
        IReadOnlyList<string> missingKeys,
        IReadOnlyList<string> unexpectedKeys)
    {
        ExpectedCount = expectedCount;
        MissingKeys = missingKeys;
        UnexpectedKeys = unexpectedKeys;
    }

    public int ExpectedCount { get; }

    public IReadOnlyList<string> MissingKeys { get; }

    public IReadOnlyList<string> UnexpectedKeys { get; }

    public bool IsComplete =>
        ExpectedCount > 0 && MissingKeys.Count == 0 && UnexpectedKeys.Count == 0;

    public static MigrationTimingArtifactReconciliation Invalid(string reason) =>
        new MigrationTimingArtifactReconciliation(0, Array.Empty<string>(), new[] { reason });
}
