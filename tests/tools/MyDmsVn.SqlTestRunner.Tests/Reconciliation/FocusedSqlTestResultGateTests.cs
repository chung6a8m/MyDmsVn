using System;
using System.IO;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Reconciliation;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Reconciliation;

public sealed class FocusedSqlTestResultGateTests
{
    [Fact]
    public void Selected_source_methods_reconcile_with_discovery_and_expanded_execution()
    {
        var trx = WriteTrx(
            "Example.Tests.FastFact",
            "Example.Tests.FastTheory(value: first)",
            "Example.Tests.FastTheory(value: second)");
        try
        {
            var methods = new[]
            {
                Method("Example.Tests.FastFact", false, SqlTestCategories.FastSql),
                Method("Example.Tests.FastTheory", true, SqlTestCategories.FastSql),
                Method("Example.Tests.Schema", false, SqlTestCategories.MigrationSchema),
            };
            var discovery = "The following Tests are available:\n" +
                "    Example.Tests.FastFact\n" +
                "    Example.Tests.FastTheory(value: first)\n" +
                "    Example.Tests.FastTheory(value: second)\n";

            var result = FocusedSqlTestResultGate.Verify(
                methods,
                SqlTestCategories.FastSql,
                discovery,
                trx,
                "net8.0",
                runnerExitCode: 0);

            Assert.True(result.IsComplete, string.Join(", ", result.Failures));
            Assert.Equal(2, result.SelectedMethods);
            Assert.Equal(3, result.DiscoveredCases);
            Assert.Equal(3, result.ExecutedCases);
            Assert.Equal(0, result.SkippedCases);
        }
        finally
        {
            File.Delete(trx);
        }
    }

    [Fact]
    public void Missing_filtered_method_or_skipped_row_is_incomplete()
    {
        var trx = WriteTrx("Example.Tests.FastFact", outcome: "NotExecuted");
        try
        {
            var methods = new[]
            {
                Method("Example.Tests.FastFact", false, SqlTestCategories.FastSql),
                Method("Example.Tests.Second", false, SqlTestCategories.FastSql),
            };

            var result = FocusedSqlTestResultGate.Verify(
                methods,
                SqlTestCategories.FastSql,
                "The following Tests are available:\n    Example.Tests.FastFact\n",
                trx,
                "net48",
                runnerExitCode: 0);

            Assert.False(result.IsComplete);
            Assert.Contains(FocusedSqlTestFailure.SelectedMethodMissingFromDiscovery, result.Failures);
            Assert.Contains(FocusedSqlTestFailure.ExecutionMismatch, result.Failures);
            Assert.Equal(1, result.SkippedCases);
        }
        finally
        {
            File.Delete(trx);
        }
    }

    private static TestMethodDescriptor Method(string name, bool theory, string category) =>
        new TestMethodDescriptor(name, theory, "fingerprint", new[] { category });

    private static string WriteTrx(params string[] names) => WriteTrx(names, "Passed");

    private static string WriteTrx(string name, string outcome) => WriteTrx(new[] { name }, outcome);

    private static string WriteTrx(string[] names, string outcome)
    {
        var path = Path.Combine(Path.GetTempPath(), "MyDmsVn.Focused." + Guid.NewGuid().ToString("N") + ".trx");
        File.WriteAllText(
            path,
            "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
            string.Concat(Array.ConvertAll(names, name =>
                "<UnitTestResult testName=\"" + name + "\" outcome=\"" + outcome + "\" />")) +
            "</Results></TestRun>");
        return path;
    }
}
