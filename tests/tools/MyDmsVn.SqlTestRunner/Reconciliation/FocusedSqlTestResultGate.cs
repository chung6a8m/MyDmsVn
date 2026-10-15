using System;
using System.Collections.Generic;
using System.Linq;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Trx;

namespace MyDmsVn.SqlTestRunner.Reconciliation;

public static class FocusedSqlTestResultGate
{
    public static FocusedSqlTestResult Verify(
        IReadOnlyList<TestMethodDescriptor> methods,
        string category,
        string discoveryOutput,
        string trxPath,
        string tfm,
        int runnerExitCode)
    {
        var failures = new HashSet<FocusedSqlTestFailure>();
        if (!SqlTestCategories.All.Contains(category, StringComparer.Ordinal))
        {
            failures.Add(FocusedSqlTestFailure.UnknownCategory);
        }

        var selected = methods
            .Where(method => method.Categories.Contains(category, StringComparer.Ordinal))
            .ToArray();
        var discovered = VstestDiscoveryParser.Parse(discoveryOutput, tfm);
        var selectedNames = selected.Select(method => method.FullyQualifiedName)
            .ToHashSet(StringComparer.Ordinal);
        var discoveredNames = discovered.Select(testCase => testCase.MethodName)
            .ToHashSet(StringComparer.Ordinal);
        if (selectedNames.Except(discoveredNames).Any())
        {
            failures.Add(FocusedSqlTestFailure.SelectedMethodMissingFromDiscovery);
        }

        if (discoveredNames.Except(selectedNames).Any())
        {
            failures.Add(FocusedSqlTestFailure.UnexpectedDiscoveredMethod);
        }

        foreach (var method in selected)
        {
            var count = discovered.Count(testCase =>
                string.Equals(testCase.MethodName, method.FullyQualifiedName, StringComparison.Ordinal));
            if ((!method.IsTheory && count != 1) || (method.IsTheory && count == 0))
            {
                failures.Add(FocusedSqlTestFailure.DiscoveryCardinalityMismatch);
            }
        }

        var trx = TrxResultParser.TryParse(trxPath, tfm);
        if (!trx.IsComplete ||
            !SqlTestInventoryReconciler.Reconcile(runnerExitCode, discovered, trx.Results).IsComplete)
        {
            failures.Add(FocusedSqlTestFailure.ExecutionMismatch);
        }

        return new FocusedSqlTestResult(
            failures.OrderBy(value => value).ToArray(),
            selected.Length,
            discovered.Count,
            trx.Results.Count,
            trx.Results.Count(result => result.Outcome == TestOutcome.Skipped));
    }
}

public enum FocusedSqlTestFailure
{
    UnknownCategory,
    SelectedMethodMissingFromDiscovery,
    UnexpectedDiscoveredMethod,
    DiscoveryCardinalityMismatch,
    ExecutionMismatch,
}

public sealed record FocusedSqlTestResult(
    IReadOnlyList<FocusedSqlTestFailure> Failures,
    int SelectedMethods,
    int DiscoveredCases,
    int ExecutedCases,
    int SkippedCases)
{
    public bool IsComplete => Failures.Count == 0;
}
