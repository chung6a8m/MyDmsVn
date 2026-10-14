using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Reconciliation;
using MyDmsVn.SqlTestRunner.Trx;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Reconciliation;

public sealed class SqlTestInventoryReconcilerTests
{
    [Fact]
    public void Theory_rows_reconcile_by_redacted_row_identity_independent_of_order()
    {
        var discovered = VstestDiscoveryParser.Parse(
            "The following Tests are available:\n" +
            "    Example.Tests.Theory(value: secret-a)\n" +
            "    Example.Tests.Theory(value: secret-b)\n",
            "net8.0");
        var executed = ParseTrx(
            "Example.Tests.Theory(value: secret-b)",
            "Example.Tests.Theory(value: secret-a)");

        var result = SqlTestInventoryReconciler.Reconcile(0, discovered, executed);

        Assert.True(result.IsComplete);
        Assert.All(discovered, row => Assert.DoesNotContain("secret", row.StableCaseId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Theory_row_substitution_a_b_to_a_a_is_incomplete()
    {
        var discovered = VstestDiscoveryParser.Parse(
            "The following Tests are available:\n" +
            "    Example.Tests.Theory(value: secret-a)\n" +
            "    Example.Tests.Theory(value: secret-b)\n",
            "net8.0");
        var executed = ParseTrx(
            "Example.Tests.Theory(value: secret-a)",
            "Example.Tests.Theory(value: secret-a)");

        var result = SqlTestInventoryReconciler.Reconcile(0, discovered, executed);

        Assert.False(result.IsComplete);
        Assert.Contains(InventoryIncompleteReason.DuplicateResult, result.Reasons);
        Assert.Contains(InventoryIncompleteReason.DiscoveredNotExecuted, result.Reasons);
    }

    [Fact]
    public void Duplicate_or_deferred_discovery_rows_are_incomplete()
    {
        var duplicate = VstestDiscoveryParser.Parse(
            "The following Tests are available:\n" +
            "    Example.Tests.Theory(value: a)\n" +
            "    Example.Tests.Theory(value: a)\n",
            "net8.0");
        var deferred = VstestDiscoveryParser.Parse(
            "The following Tests are available:\n    Example.Tests.Theory\n",
            "net8.0");
        var executed = ParseTrx(
            "Example.Tests.Theory(value: a)",
            "Example.Tests.Theory(value: b)");

        Assert.Contains(
            InventoryIncompleteReason.DuplicateDiscovery,
            SqlTestInventoryReconciler.Reconcile(0, duplicate, executed).Reasons);
        Assert.False(SqlTestInventoryReconciler.Reconcile(0, deferred, executed).IsComplete);
    }

    [Fact]
    public void Complete_one_to_one_run_is_valid()
    {
        var discovered = new[] { VstestDiscoveryParser.CreateCase("Example.Tests.SqlFact", "net8.0", 0) };
        var executed = new[] { TrxTestResult.Passed(discovered[0].StableCaseId, "Example.Tests.SqlFact", "net8.0", TimeSpan.Zero) };

        var result = SqlTestInventoryReconciler.Reconcile(0, discovered, executed);

        Assert.True(result.IsComplete);
    }

    [Theory]
    [InlineData(1, InventoryIncompleteReason.RunnerFailed)]
    [InlineData(0, InventoryIncompleteReason.FailedTest)]
    public void Failed_runner_or_test_is_incomplete(int exitCode, InventoryIncompleteReason expected)
    {
        var discovered = new[] { VstestDiscoveryParser.CreateCase("Example.Tests.SqlFact", "net8.0", 0) };
        var executed = new[] { new TrxTestResult(discovered[0].StableCaseId, "Example.Tests.SqlFact", "net8.0", TestOutcome.Failed, TimeSpan.Zero) };

        var result = SqlTestInventoryReconciler.Reconcile(exitCode, discovered, executed);

        Assert.False(result.IsComplete);
        Assert.Contains(expected, result.Reasons);
    }

    [Fact]
    public void Missing_duplicate_skipped_and_tfm_mismatch_are_all_fail_closed()
    {
        var first = VstestDiscoveryParser.CreateCase("Example.Tests.First", "net8.0", 0);
        var second = VstestDiscoveryParser.CreateCase("Example.Tests.Second", "net8.0", 0);
        var executed = new[]
        {
            new TrxTestResult(first.StableCaseId, first.MethodName, "net48", TestOutcome.Skipped, TimeSpan.Zero),
            new TrxTestResult(first.StableCaseId, first.MethodName, "net48", TestOutcome.Skipped, TimeSpan.Zero),
        };

        var result = SqlTestInventoryReconciler.Reconcile(0, new[] { first, second }, executed);

        Assert.Contains(InventoryIncompleteReason.DiscoveredNotExecuted, result.Reasons);
        Assert.Contains(InventoryIncompleteReason.DuplicateResult, result.Reasons);
        Assert.Contains(InventoryIncompleteReason.SkippedMandatorySql, result.Reasons);
        Assert.Contains(InventoryIncompleteReason.TfmMismatch, result.Reasons);
    }

    private static IReadOnlyList<TrxTestResult> ParseTrx(params string[] displayNames)
    {
        var path = Path.Combine(Path.GetTempPath(), "MyDmsVn.TheoryRows." + Guid.NewGuid().ToString("N") + ".trx");
        try
        {
            File.WriteAllText(
                path,
                "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
                string.Concat(displayNames.Select(display =>
                    "<UnitTestResult testName=\"" + display + "\" outcome=\"Passed\" />")) +
                "</Results></TestRun>");
            return TrxResultParser.Parse(path, "net8.0").Results;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
