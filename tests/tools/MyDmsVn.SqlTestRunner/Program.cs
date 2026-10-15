using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reconciliation;

namespace MyDmsVn.SqlTestRunner;

internal static class Program
{
    public static Task<int> Main(string[] args) =>
        RunAsync(args, RunBaselineAsync, CancellationToken.None);

    internal static async Task<int> RunAsync(
        string[] args,
        Func<BenchmarkRunOptions, CancellationToken, Task<BenchmarkRunResult>> baselineRunner,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return 64;
        }

        if (string.Equals(args[0], "preflight", StringComparison.OrdinalIgnoreCase))
        {
            return await new EnvironmentPreflightGate()
                .ValidateAsync(cancellationToken)
                .ConfigureAwait(false)
                ? 0
                : 2;
        }

        if (string.Equals(args[0], "focused-verify", StringComparison.OrdinalIgnoreCase))
        {
            return await VerifyFocusedAsync(ParseOptions(args), cancellationToken).ConfigureAwait(false);
        }

        if (!string.Equals(args[0], "baseline", StringComparison.OrdinalIgnoreCase))
        {
            return 64;
        }

        var values = ParseOptions(args);
        if (!values.TryGetValue("repo", out var repository) ||
            !values.TryGetValue("project", out var project) ||
            !values.TryGetValue("artifacts", out var artifacts) ||
            !values.TryGetValue("approval", out var approval))
        {
            return 64;
        }

        Environment.SetEnvironmentVariable(
            SqlTestHarnessEnvironment.ApprovalFileVariable,
            approval);
        Directory.CreateDirectory(artifacts);
        var result = await baselineRunner(
                new BenchmarkRunOptions(
                    Path.GetFullPath(repository),
                    Path.GetFullPath(project),
                    Path.GetFullPath(artifacts),
                    Path.GetFullPath(approval),
                    new[] { "net48", "net8.0" },
                    TimeSpan.FromMinutes(30),
                    OverallRunTimeout: TimeSpan.FromHours(4)),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsComplete ? 0 : 3;
    }

    private static async Task<int> VerifyFocusedAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var required = new[]
        {
            "assembly", "manifest", "category", "discovery", "trx", "tfm", "exit-code",
            "run-id", "journal", "recovery", "approval",
        };
        if (required.Any(key => !values.ContainsKey(key)) ||
            !int.TryParse(values["exit-code"], out var exitCode))
        {
            return 64;
        }

        IReadOnlyList<TestMethodDescriptor> methods;
        try
        {
            methods = TestMethodInventory.Read(values["assembly"]);
            if (!SqlTestClassificationVerifier.Verify(
                    methods,
                    NonSqlExemptionManifest.Read(values["manifest"]))
                .IsComplete)
            {
                return 3;
            }
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is InvalidDataException ||
            exception is InvalidOperationException)
        {
            return 3;
        }

        var result = FocusedSqlTestResultGate.Verify(
            methods,
            values["category"],
            File.ReadAllText(values["discovery"]),
            values["trx"],
            values["tfm"],
            exitCode);
        using var ownershipDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            ownershipDeadline.Token);
        var ownership = await new BenchmarkOwnershipGate().ValidateAsync(
                new BenchmarkOwnershipEvidence(
                    values["run-id"],
                    values["journal"],
                    values["recovery"],
                    values["approval"],
                    exitCode == 0),
                linked.Token)
            .ConfigureAwait(false);

        var composition = methods
            .Where(method => method.Categories.Contains(values["category"], StringComparer.Ordinal))
            .SelectMany(method => method.Categories)
            .GroupBy(category => category, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key + ":" + group.Count());
        Console.WriteLine(
            "category={0} tfm={1} selectedMethods={2} discoveredCases={3} executedCases={4} skippedCases={5} composition={6} status={7}",
            values["category"],
            values["tfm"],
            result.SelectedMethods,
            result.DiscoveredCases,
            result.ExecutedCases,
            result.SkippedCases,
            string.Join(",", composition),
            result.IsComplete && ownership.IsComplete ? "complete" : "incomplete");
        return result.IsComplete && ownership.IsComplete ? 0 : 3;
    }

    private static async Task<BenchmarkRunResult> RunBaselineAsync(
        BenchmarkRunOptions options,
        CancellationToken cancellationToken)
    {
        using var runner = new OwnedProcessRunner();
        return await new BenchmarkRunCoordinator(
                new EnvironmentPreflightGate(),
                runner)
            .RunAsync(options, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index + 1 < args.Length; index += 2)
        {
            if (args[index].StartsWith("--", StringComparison.Ordinal))
            {
                values[args[index].Substring(2)] = args[index + 1];
            }
        }

        return values;
    }
}
