using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Focused;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reconciliation;
using MyDmsVn.SqlTestRunner.Reporting;

namespace MyDmsVn.SqlTestRunner;

internal static class Program
{
    private static readonly Regex RevisionPattern = new Regex(
        "^[0-9a-fA-F]{40}$",
        RegexOptions.CultureInvariant);

    public static Task<int> Main(string[] args) =>
        RunAsync(args, RunBaselineAsync, RunPrAcceptanceAsync, CancellationToken.None);

    internal static async Task<int> RunAsync(
        string[] args,
        Func<BenchmarkRunOptions, CancellationToken, Task<BenchmarkRunResult>> baselineRunner,
        CancellationToken cancellationToken) =>
        await RunAsync(args, baselineRunner, RunPrAcceptanceAsync, cancellationToken)
            .ConfigureAwait(false);

    internal static async Task<int> RunAsync(
        string[] args,
        Func<BenchmarkRunOptions, CancellationToken, Task<BenchmarkRunResult>> baselineRunner,
        Func<BenchmarkRunOptions, CancellationToken, Task<BenchmarkRunResult>> acceptanceRunner,
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

        if (string.Equals(args[0], "focused-run", StringComparison.OrdinalIgnoreCase))
        {
            return await RunFocusedAsync(ParseOptions(args), cancellationToken).ConfigureAwait(false);
        }

        var isBaseline = string.Equals(args[0], "baseline", StringComparison.OrdinalIgnoreCase);
        var isAcceptance = string.Equals(
            args[0],
            "pr-acceptance",
            StringComparison.OrdinalIgnoreCase);
        if (!isBaseline && !isAcceptance)
        {
            return 64;
        }

        var values = ParseOptions(args);
        if (!values.TryGetValue("repo", out var repository) ||
            !values.TryGetValue("project", out var project) ||
            !values.TryGetValue("artifacts", out var artifacts) ||
            !values.TryGetValue("approval", out var approval) ||
            (isAcceptance &&
             (!values.TryGetValue("revision", out var revision) ||
              !RevisionPattern.IsMatch(revision))))
        {
            return 64;
        }

        Environment.SetEnvironmentVariable(
            SqlTestHarnessEnvironment.ApprovalFileVariable,
            approval);
        Directory.CreateDirectory(artifacts);
        var options = new BenchmarkRunOptions(
                    Path.GetFullPath(repository),
                    Path.GetFullPath(project),
                    Path.GetFullPath(artifacts),
                    Path.GetFullPath(approval),
                    new[] { "net48", "net8.0" },
                    TimeSpan.FromMinutes(30),
                    WarmupPasses: isAcceptance ? 0 : 1,
                    MeasuredPasses: isAcceptance ? 1 : 3,
                    OverallRunTimeout: TimeSpan.FromHours(4))
        {
            Mode = isAcceptance ? "pr-acceptance" : "baseline",
            Revision = isAcceptance ? values["revision"] : string.Empty,
        };
        var result = await (isAcceptance ? acceptanceRunner : baselineRunner)(
                options,
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsComplete ? 0 : 3;
    }

    private static async Task<int> RunFocusedAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var required = new[]
        {
            "repo", "project", "artifacts", "approval", "category", "frameworks",
        };
        if (required.Any(key => !values.ContainsKey(key)))
        {
            return 64;
        }

        Environment.SetEnvironmentVariable(SqlTestHarnessEnvironment.ApprovalFileVariable, values["approval"]);
        Directory.CreateDirectory(values["artifacts"]);
        using var runner = new OwnedProcessRunner();
        var result = await new FocusedRunCoordinator(new EnvironmentPreflightGate(), runner)
            .RunAsync(
                new FocusedRunOptions(
                    Path.GetFullPath(values["repo"]),
                    Path.GetFullPath(values["project"]),
                    Path.GetFullPath(values["artifacts"]),
                    Path.GetFullPath(values["approval"]),
                    values["category"],
                    values["frameworks"].Split(',', StringSplitOptions.RemoveEmptyEntries),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(1)),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsComplete ? 0 : 3;
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

    private static async Task<BenchmarkRunResult> RunPrAcceptanceAsync(
        BenchmarkRunOptions options,
        CancellationToken cancellationToken)
    {
        using var runner = new OwnedProcessRunner();
        return await new BenchmarkRunCoordinator(
                new EnvironmentPreflightGate(),
                runner,
                reportExporter: new SqlAcceptanceReportExporter())
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
