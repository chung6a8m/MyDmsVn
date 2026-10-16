using System;
using System.Collections.Generic;
using System.Linq;
using MyDmsVn.SqlTestRunner.Gates;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Trx;

namespace MyDmsVn.SqlTestRunner.Reconciliation;

public static class LogicalCaseInventoryReconciler
{
    public static LogicalCaseReconciliation Reconcile(
        IReadOnlyCollection<ExpectedLogicalCase> expected,
        IReadOnlyCollection<BoundLogicalCase> source,
        IReadOnlyCollection<BoundLogicalCase> discovered,
        IReadOnlyCollection<ExecutedLogicalCase> executed,
        IReadOnlyCollection<IdentityMappingDefinition>? mappings = null,
        IReadOnlyCollection<TestGateExemptionDefinition>? exemptions = null)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(discovered);
        ArgumentNullException.ThrowIfNull(executed);

        var reasons = new HashSet<LogicalCaseReconciliationFailure>();
        var expectedByKey = expected
            .GroupBy(item => item.LogicalKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        if (expectedByKey.Any(item => item.Value.Length != 1))
        {
            reasons.Add(LogicalCaseReconciliationFailure.DuplicateExpected);
        }

        ValidateInventory(
            expectedByKey,
            source,
            LogicalCaseReconciliationFailure.DuplicateSource,
            LogicalCaseReconciliationFailure.MissingSource,
            LogicalCaseReconciliationFailure.UnexpectedSource,
            reasons);
        ValidateInventory(
            expectedByKey,
            discovered,
            LogicalCaseReconciliationFailure.DuplicateDiscovery,
            LogicalCaseReconciliationFailure.MissingDiscovery,
            LogicalCaseReconciliationFailure.UnexpectedDiscovery,
            reasons);
        ValidateInventory(
            expectedByKey,
            executed,
            LogicalCaseReconciliationFailure.DuplicateExecution,
            LogicalCaseReconciliationFailure.MissingExecution,
            LogicalCaseReconciliationFailure.UnexpectedExecution,
            reasons);

        if (source.GroupBy(item => item.LogicalKey, StringComparer.Ordinal)
            .Any(group => group.Select(item => item.OwnerProject)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != 1))
        {
            reasons.Add(LogicalCaseReconciliationFailure.DuplicateGlobalOwner);
        }

        foreach (var item in source)
        {
            ValidateMetadata(item, expectedByKey, checkFingerprint: true, reasons);
        }

        foreach (var item in discovered)
        {
            ValidateMetadata(item, expectedByKey, checkFingerprint: false, reasons);
        }

        foreach (var item in executed)
        {
            ValidateMetadata(item, expectedByKey, checkFingerprint: false, reasons);
            if (item.Outcome == TestOutcome.Failed)
            {
                reasons.Add(LogicalCaseReconciliationFailure.FailedTest);
            }
            else if (item.Outcome == TestOutcome.Skipped)
            {
                reasons.Add(LogicalCaseReconciliationFailure.SkippedTest);
            }
        }

        ValidateMappings(expected, mappings ?? Array.Empty<IdentityMappingDefinition>(), reasons);
        ValidateExemptions(expected, exemptions ?? Array.Empty<TestGateExemptionDefinition>(), reasons);

        return new LogicalCaseReconciliation(reasons.OrderBy(value => value).ToArray());
    }

    private static void ValidateInventory<T>(
        IReadOnlyDictionary<string, ExpectedLogicalCase[]> expectedByKey,
        IReadOnlyCollection<T> actual,
        LogicalCaseReconciliationFailure duplicate,
        LogicalCaseReconciliationFailure missing,
        LogicalCaseReconciliationFailure unexpected,
        ISet<LogicalCaseReconciliationFailure> reasons)
        where T : notnull
    {
        var actualByKey = actual
            .Select(item => (Item: item, Key: ReadLogicalKey(item)))
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        if (actualByKey.Any(item => item.Value != 1))
        {
            reasons.Add(duplicate);
        }

        if (expectedByKey.Keys.Except(actualByKey.Keys, StringComparer.Ordinal).Any())
        {
            reasons.Add(missing);
        }

        if (actualByKey.Keys.Except(expectedByKey.Keys, StringComparer.Ordinal).Any())
        {
            reasons.Add(unexpected);
        }
    }

    private static string ReadLogicalKey<T>(T item) where T : notnull => item switch
    {
        BoundLogicalCase bound => bound.LogicalKey,
        ExecutedLogicalCase executed => executed.LogicalKey,
        _ => throw new InvalidOperationException("Unsupported logical inventory type."),
    };

    private static void ValidateMetadata(
        BoundLogicalCase actual,
        IReadOnlyDictionary<string, ExpectedLogicalCase[]> expectedByKey,
        bool checkFingerprint,
        ISet<LogicalCaseReconciliationFailure> reasons)
    {
        if (!TryExpected(actual.LogicalKey, expectedByKey, out var expected))
        {
            return;
        }

        ValidateMetadataCore(
            actual.OwnerProject,
            actual.Tfm,
            actual.NormalizedMethodSignature,
            actual.RowKey,
            actual.ReviewedCodeFingerprint,
            actual.Classification,
            expected,
            checkFingerprint,
            reasons);
    }

    private static void ValidateMetadata(
        ExecutedLogicalCase actual,
        IReadOnlyDictionary<string, ExpectedLogicalCase[]> expectedByKey,
        bool checkFingerprint,
        ISet<LogicalCaseReconciliationFailure> reasons)
    {
        if (!TryExpected(actual.LogicalKey, expectedByKey, out var expected))
        {
            return;
        }

        ValidateMetadataCore(
            actual.OwnerProject,
            actual.Tfm,
            actual.NormalizedMethodSignature,
            actual.RowKey,
            actual.ReviewedCodeFingerprint,
            actual.Classification,
            expected,
            checkFingerprint,
            reasons);
    }

    private static bool TryExpected(
        string logicalKey,
        IReadOnlyDictionary<string, ExpectedLogicalCase[]> expectedByKey,
        out ExpectedLogicalCase expected)
    {
        if (expectedByKey.TryGetValue(logicalKey, out var matches) && matches.Length == 1)
        {
            expected = matches[0];
            return true;
        }

        expected = null!;
        return false;
    }

    private static void ValidateMetadataCore(
        string ownerProject,
        string tfm,
        string normalizedMethodSignature,
        string rowKey,
        string fingerprint,
        IReadOnlyCollection<string> classification,
        ExpectedLogicalCase expected,
        bool checkFingerprint,
        ISet<LogicalCaseReconciliationFailure> reasons)
    {
        if (!string.Equals(tfm, expected.Tfm, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(LogicalCaseReconciliationFailure.TfmMismatch);
        }

        if (!string.Equals(ownerProject, expected.OwnerProject, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(LogicalCaseReconciliationFailure.PhysicalOwnerMismatch);
        }

        if (!string.Equals(
                normalizedMethodSignature,
                expected.NormalizedMethodSignature,
                StringComparison.Ordinal) ||
            !string.Equals(rowKey, expected.RowKey, StringComparison.Ordinal))
        {
            reasons.Add(LogicalCaseReconciliationFailure.IdentityMetadataMismatch);
        }

        if (!SetEquals(classification, expected.Classification))
        {
            reasons.Add(LogicalCaseReconciliationFailure.ClassificationDrift);
        }

        if (checkFingerprint &&
            !string.Equals(fingerprint, expected.ReviewedCodeFingerprint, StringComparison.Ordinal))
        {
            reasons.Add(LogicalCaseReconciliationFailure.StaleFingerprint);
        }
    }

    private static void ValidateMappings(
        IReadOnlyCollection<ExpectedLogicalCase> expected,
        IReadOnlyCollection<IdentityMappingDefinition> mappings,
        ISet<LogicalCaseReconciliationFailure> reasons)
    {
        var ids = expected.Select(item => item.LogicalCaseId).ToHashSet(StringComparer.Ordinal);
        if (mappings.Any(mapping =>
                string.IsNullOrWhiteSpace(mapping.Rationale) ||
                !ids.Contains(mapping.OldLogicalCaseId) ||
                !ids.Contains(mapping.NewLogicalCaseId)))
        {
            reasons.Add(LogicalCaseReconciliationFailure.MissingMappingEndpoint);
        }

        if (mappings.GroupBy(mapping => mapping.OldLogicalCaseId, StringComparer.Ordinal)
                .Any(group => group.Count() != 1) ||
            mappings.GroupBy(mapping => mapping.NewLogicalCaseId, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            reasons.Add(LogicalCaseReconciliationFailure.AmbiguousMapping);
        }

        var next = mappings
            .GroupBy(mapping => mapping.OldLogicalCaseId, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(
                group => group.Key,
                group => group.Single().NewLogicalCaseId,
                StringComparer.Ordinal);
        foreach (var start in next.Keys)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (var current = start; next.TryGetValue(current, out var successor); current = successor)
            {
                if (!visited.Add(current) || string.Equals(successor, start, StringComparison.Ordinal))
                {
                    reasons.Add(LogicalCaseReconciliationFailure.MappingCycle);
                    break;
                }
            }
        }
    }

    private static void ValidateExemptions(
        IReadOnlyCollection<ExpectedLogicalCase> expected,
        IReadOnlyCollection<TestGateExemptionDefinition> exemptions,
        ISet<LogicalCaseReconciliationFailure> reasons)
    {
        if (exemptions.GroupBy(item => item.NormalizedMethodSignature, StringComparer.Ordinal)
            .Any(group => group.Count() != 1))
        {
            reasons.Add(LogicalCaseReconciliationFailure.InvalidExemption);
        }

        foreach (var exemption in exemptions)
        {
            var matches = expected.Where(item => string.Equals(
                    item.NormalizedMethodSignature,
                    exemption.NormalizedMethodSignature,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0 ||
                matches.Any(item =>
                    !SetEquals(item.Classification, exemption.Classification) ||
                    !string.Equals(
                        item.ReviewedCodeFingerprint,
                        exemption.ReviewedCodeFingerprint,
                        StringComparison.Ordinal)))
            {
                reasons.Add(LogicalCaseReconciliationFailure.InvalidExemption);
            }
        }
    }

    private static bool SetEquals(
        IEnumerable<string> first,
        IEnumerable<string> second) => new HashSet<string>(first, StringComparer.Ordinal)
        .SetEquals(second);
}

public enum LogicalCaseReconciliationFailure
{
    DuplicateExpected,
    DuplicateSource,
    DuplicateDiscovery,
    DuplicateExecution,
    DuplicateGlobalOwner,
    MissingSource,
    MissingDiscovery,
    MissingExecution,
    UnexpectedSource,
    UnexpectedDiscovery,
    UnexpectedExecution,
    FailedTest,
    SkippedTest,
    TfmMismatch,
    PhysicalOwnerMismatch,
    IdentityMetadataMismatch,
    ClassificationDrift,
    StaleFingerprint,
    InvalidExemption,
    MissingMappingEndpoint,
    AmbiguousMapping,
    MappingCycle,
}

public sealed record LogicalCaseReconciliation(
    IReadOnlyList<LogicalCaseReconciliationFailure> Reasons)
{
    public bool IsComplete => Reasons.Count == 0;
}
