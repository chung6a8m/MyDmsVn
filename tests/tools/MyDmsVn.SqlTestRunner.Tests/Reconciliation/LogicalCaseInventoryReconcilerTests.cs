using System;
using MyDmsVn.SqlTestRunner.Gates;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Reconciliation;
using MyDmsVn.SqlTestRunner.Trx;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Reconciliation;

public sealed class LogicalCaseInventoryReconcilerTests
{
    [Fact]
    public void Exact_bijection_is_complete_with_overlapping_categories()
    {
        var expected = Expected(classification: new[] { "FastSQL", "SqlConcurrency" });

        var result = LogicalCaseInventoryReconciler.Reconcile(
            new[] { expected },
            new[] { Bound(expected, classification: new[] { "SqlConcurrency", "FastSQL" }) },
            new[] { Bound(expected) },
            new[] { Executed(expected, TestOutcome.Passed) });

        Assert.True(result.IsComplete);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Duplicate_logical_key_across_physical_projects_is_rejected()
    {
        var expected = Expected();

        var result = LogicalCaseInventoryReconciler.Reconcile(
            new[] { expected },
            new[]
            {
                Bound(expected),
                Bound(expected, ownerProject: "tests/Other.Tests/Other.Tests.csproj"),
            },
            new[] { Bound(expected) },
            new[] { Executed(expected, TestOutcome.Passed) });

        Assert.Contains(LogicalCaseReconciliationFailure.DuplicateGlobalOwner, result.Reasons);
        Assert.Contains(LogicalCaseReconciliationFailure.DuplicateSource, result.Reasons);
    }

    [Fact]
    public void Missing_source_discovery_or_execution_and_extra_trx_are_rejected()
    {
        var expected = Expected();
        var extra = Expected(logicalKey: "v1:extra", logicalCaseId: "extra");

        var result = LogicalCaseInventoryReconciler.Reconcile(
            new[] { expected },
            Array.Empty<BoundLogicalCase>(),
            Array.Empty<BoundLogicalCase>(),
            new[] { Executed(extra, TestOutcome.Passed) });

        Assert.Contains(LogicalCaseReconciliationFailure.MissingSource, result.Reasons);
        Assert.Contains(LogicalCaseReconciliationFailure.MissingDiscovery, result.Reasons);
        Assert.Contains(LogicalCaseReconciliationFailure.MissingExecution, result.Reasons);
        Assert.Contains(LogicalCaseReconciliationFailure.UnexpectedExecution, result.Reasons);
    }

    [Theory]
    [InlineData(TestOutcome.Failed, LogicalCaseReconciliationFailure.FailedTest)]
    [InlineData(TestOutcome.Skipped, LogicalCaseReconciliationFailure.SkippedTest)]
    public void Non_passed_execution_is_rejected(
        TestOutcome outcome,
        LogicalCaseReconciliationFailure expectedFailure)
    {
        var expected = Expected();

        var result = LogicalCaseInventoryReconciler.Reconcile(
            new[] { expected },
            new[] { Bound(expected) },
            new[] { Bound(expected) },
            new[] { Executed(expected, outcome) });

        Assert.Contains(expectedFailure, result.Reasons);
    }

    [Fact]
    public void Tfm_classification_and_source_fingerprint_drift_are_rejected()
    {
        var expected = Expected();

        var result = LogicalCaseInventoryReconciler.Reconcile(
            new[] { expected },
            new[]
            {
                Bound(
                    expected,
                    tfm: "net48",
                    fingerprint: new string('f', 64),
                    classification: new[] { "FastSQL" }),
            },
            new[] { Bound(expected) },
            new[] { Executed(expected, TestOutcome.Passed) });

        Assert.Contains(LogicalCaseReconciliationFailure.TfmMismatch, result.Reasons);
        Assert.Contains(LogicalCaseReconciliationFailure.ClassificationDrift, result.Reasons);
        Assert.Contains(LogicalCaseReconciliationFailure.StaleFingerprint, result.Reasons);
    }

    [Fact]
    public void Invalid_method_exemption_is_rejected()
    {
        var expected = Expected();
        var invalid = new TestGateExemptionDefinition(
            "exemption-a",
            expected.NormalizedMethodSignature,
            new[] { "FastSQL" },
            "Not a non-SQL exemption.",
            expected.ReviewedCodeFingerprint);

        var result = LogicalCaseInventoryReconciler.Reconcile(
            new[] { expected },
            new[] { Bound(expected) },
            new[] { Bound(expected) },
            new[] { Executed(expected, TestOutcome.Passed) },
            Array.Empty<IdentityMappingDefinition>(),
            new[] { invalid });

        Assert.Contains(LogicalCaseReconciliationFailure.InvalidExemption, result.Reasons);
    }

    [Fact]
    public void Missing_or_ambiguous_identity_mapping_is_rejected()
    {
        var first = Expected(logicalKey: "v1:first", logicalCaseId: "first");
        var second = Expected(logicalKey: "v1:second", logicalCaseId: "second");
        var mappings = new[]
        {
            new IdentityMappingDefinition("missing", "unknown", "second", "missing endpoint"),
            new IdentityMappingDefinition("first-to-second", "first", "second", "first move"),
            new IdentityMappingDefinition("other-to-second", "other", "second", "ambiguous target"),
        };

        var result = LogicalCaseInventoryReconciler.Reconcile(
            new[] { first, second },
            new[] { Bound(first), Bound(second) },
            new[] { Bound(first), Bound(second) },
            new[]
            {
                Executed(first, TestOutcome.Passed),
                Executed(second, TestOutcome.Passed),
            },
            mappings,
            Array.Empty<TestGateExemptionDefinition>());

        Assert.Contains(LogicalCaseReconciliationFailure.MissingMappingEndpoint, result.Reasons);
        Assert.Contains(LogicalCaseReconciliationFailure.AmbiguousMapping, result.Reasons);
    }

    private const string Fingerprint =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static ExpectedLogicalCase Expected(
        string logicalKey = "v1:case-a",
        string logicalCaseId = "case-a",
        string ownerProject = "tests/Example.Tests/Example.Tests.csproj",
        string tfm = "net8.0",
        string fingerprint = Fingerprint,
        string[]? classification = null) => new ExpectedLogicalCase(
            logicalKey,
            logicalCaseId,
            ownerProject,
            tfm,
            "Example.Tests.Case()",
            "fact",
            fingerprint,
            classification ?? new[] { "NonSql" });

    private static BoundLogicalCase Bound(
        ExpectedLogicalCase expected,
        string? ownerProject = null,
        string? tfm = null,
        string? fingerprint = null,
        string[]? classification = null) => new BoundLogicalCase(
            expected.LogicalKey,
            expected.LogicalCaseId,
            ownerProject ?? expected.OwnerProject,
            tfm ?? expected.Tfm,
            expected.NormalizedMethodSignature,
            expected.RowKey,
            fingerprint ?? expected.ReviewedCodeFingerprint,
            classification ?? expected.Classification);

    private static ExecutedLogicalCase Executed(
        ExpectedLogicalCase expected,
        TestOutcome outcome) => new ExecutedLogicalCase(
            expected.LogicalKey,
            expected.LogicalCaseId,
            expected.OwnerProject,
            expected.Tfm,
            expected.NormalizedMethodSignature,
            expected.RowKey,
            expected.ReviewedCodeFingerprint,
            expected.Classification,
            outcome,
            TimeSpan.FromMilliseconds(1));
}
