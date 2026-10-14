using System;
using System.Collections.Generic;
using System.Linq;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Trx;

namespace MyDmsVn.SqlTestRunner.Reconciliation;

public static class SqlTestInventoryReconciler
{
    public static SqlTestInventoryReconciliation Reconcile(
        int runnerExitCode,
        IReadOnlyCollection<DiscoveredTestCase> discovered,
        IReadOnlyCollection<TrxTestResult> executed)
    {
        var reasons = new HashSet<InventoryIncompleteReason>();
        if (runnerExitCode != 0)
        {
            reasons.Add(InventoryIncompleteReason.RunnerFailed);
        }

        if (executed.Any(item => item.Outcome == TestOutcome.Failed))
        {
            reasons.Add(InventoryIncompleteReason.FailedTest);
        }

        if (executed.Any(item => item.Outcome == TestOutcome.Skipped))
        {
            reasons.Add(InventoryIncompleteReason.SkippedMandatorySql);
        }

        if (executed.GroupBy(item => item.StableCaseId).Any(group => group.Count() != 1))
        {
            reasons.Add(InventoryIncompleteReason.DuplicateResult);
        }

        if (discovered.GroupBy(item => item.StableCaseId).Any(group => group.Count() != 1))
        {
            reasons.Add(InventoryIncompleteReason.DuplicateDiscovery);
        }

        var discoveredIds = new HashSet<string>(discovered.Select(item => item.StableCaseId), StringComparer.Ordinal);
        var executedIds = new HashSet<string>(executed.Select(item => item.StableCaseId), StringComparer.Ordinal);
        if (discoveredIds.Except(executedIds).Any())
        {
            reasons.Add(InventoryIncompleteReason.DiscoveredNotExecuted);
        }

        if (executedIds.Except(discoveredIds).Any())
        {
            reasons.Add(InventoryIncompleteReason.ExecutedNotDiscovered);
        }

        var tfms = new HashSet<string>(discovered.Select(item => item.Tfm), StringComparer.OrdinalIgnoreCase);
        if (executed.Any(item => !tfms.Contains(item.Tfm)))
        {
            reasons.Add(InventoryIncompleteReason.TfmMismatch);
        }

        return new SqlTestInventoryReconciliation(reasons.OrderBy(value => value).ToArray());
    }
}

public enum InventoryIncompleteReason
{
    RunnerFailed,
    FailedTest,
    SkippedMandatorySql,
    DuplicateResult,
    DuplicateDiscovery,
    DiscoveredNotExecuted,
    ExecutedNotDiscovered,
    TfmMismatch,
}

public sealed record SqlTestInventoryReconciliation(
    IReadOnlyList<InventoryIncompleteReason> Reasons)
{
    public bool IsComplete => Reasons.Count == 0;
}
