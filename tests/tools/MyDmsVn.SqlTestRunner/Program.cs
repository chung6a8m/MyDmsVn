using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Processes;

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
