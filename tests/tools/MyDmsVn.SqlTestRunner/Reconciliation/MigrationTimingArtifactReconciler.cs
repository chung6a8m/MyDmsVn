using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.SqlTestRunner.Reconciliation;

public static class MigrationTimingArtifactReconciler
{
    public static MigrationTimingArtifactReconciliation Reconcile(string path, string tfm)
    {
        if (!File.Exists(path))
        {
            return MigrationTimingArtifactReconciliation.Invalid("missing-artifact");
        }

        var expected = new List<string>();
        var actual = new List<string>();
        var invalid = new List<string>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var record = JObject.Parse(line);
                var recordTfm = (string?)record["tfm"];
                var testCaseId = (string?)record["testCaseId"];
                var recordType = (string?)record["recordType"];
                var stage = (string?)record["stage"];
                if (string.Equals(recordType, "migration-invocation", StringComparison.Ordinal))
                {
                    AddRecord(expected, recordTfm, testCaseId, (string?)record["invocationId"], tfm, invalid, lineNumber);
                }
                else if (string.Equals(stage, "migration", StringComparison.Ordinal))
                {
                    AddRecord(actual, recordTfm, testCaseId, (string?)record["operationId"], tfm, invalid, lineNumber);
                }
            }
            catch
            {
                invalid.Add("line:" + lineNumber);
            }
        }

        var expectedCounts = Counts(expected);
        var actualCounts = Counts(actual);
        var keys = expectedCounts.Keys.Concat(actualCounts.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal);
        var missing = new List<string>();
        var unexpected = new List<string>(invalid);
        foreach (var key in keys)
        {
            expectedCounts.TryGetValue(key, out var expectedCount);
            actualCounts.TryGetValue(key, out var actualCount);
            missing.AddRange(Enumerable.Repeat(key, Math.Max(0, expectedCount - actualCount)));
            unexpected.AddRange(Enumerable.Repeat(key, Math.Max(0, actualCount - expectedCount)));
        }

        return new MigrationTimingArtifactReconciliation(expected.Count, missing, unexpected);
    }

    private static void AddRecord(
        ICollection<string> destination,
        string? recordTfm,
        string? testCaseId,
        string? invocationId,
        string expectedTfm,
        ICollection<string> invalid,
        int lineNumber)
    {
        if (!string.Equals(recordTfm, expectedTfm, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(testCaseId) ||
            string.IsNullOrWhiteSpace(invocationId))
        {
            invalid.Add("line:" + lineNumber);
            return;
        }

        destination.Add(testCaseId + "|" + recordTfm + "|" + invocationId);
    }

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
