using System;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Reconciliation;
using MyDmsVn.SqlTestRunner.Trx;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Reconciliation;

public sealed class SqlTestInventoryReconcilerTests
{
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
}
