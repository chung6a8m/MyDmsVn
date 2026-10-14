using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Trx;

namespace MyDmsVn.SqlTestRunner.Benchmark;

public interface IBenchmarkPreflightGate
{
    Task<bool> ValidateAsync(CancellationToken cancellationToken);
}

public sealed class BenchmarkRunCoordinator
{
    private readonly IBenchmarkPreflightGate _preflight;
    private readonly IProcessRunner _processRunner;
    private readonly IBenchmarkResultGate _resultGate;

    public BenchmarkRunCoordinator(
        IBenchmarkPreflightGate preflight,
        IProcessRunner processRunner,
        IBenchmarkResultGate? resultGate = null)
    {
        _preflight = preflight;
        _processRunner = processRunner;
        _resultGate = resultGate ?? new TrxBenchmarkResultGate();
    }

    public async Task<BenchmarkRunResult> RunAsync(
        BenchmarkRunOptions options,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString("N");
        var runs = new List<BenchmarkFrameworkRun>();
        foreach (var tfm in options.TargetFrameworks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await _preflight.ValidateAsync(cancellationToken).ConfigureAwait(false))
            {
                return new BenchmarkRunResult(false, runs);
            }

            var trxName = "sqlperf-" + runId + "-" + tfm.Replace('.', '-') + ".trx";
            var arguments =
                "test \"" + options.TestProject + "\" -c Release -f " + tfm +
                " --no-build --no-restore --logger \"trx;LogFileName=" + trxName +
                "\" --results-directory \"" + options.ArtifactRoot + "\"";
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SqlTestHarnessEnvironment.ApprovalFileVariable] = options.ApprovalFile,
                [SqlTestHarnessEnvironment.RunIdVariable] = runId,
                [SqlTestHarnessEnvironment.TimingEnabledVariable] = "1",
                [SqlTestHarnessEnvironment.TimingOutputVariable] = Path.Combine(
                    options.ArtifactRoot,
                    "timings-" + tfm + ".jsonl"),
            };
            var result = await _processRunner.RunAsync(
                    new CommandSpec(
                        "dotnet",
                        arguments,
                        options.RepositoryRoot,
                        options.CommandTimeout,
                        environment),
                    cancellationToken)
                .ConfigureAwait(false);
            runs.Add(new BenchmarkFrameworkRun(tfm, trxName, result));
            var trxPath = Path.Combine(options.ArtifactRoot, trxName);
            if (result.ExitCode != 0 || result.TimedOut ||
                !_resultGate.IsValid(trxPath, tfm))
            {
                return new BenchmarkRunResult(false, runs);
            }
        }

        return new BenchmarkRunResult(true, runs);
    }
}

public interface IBenchmarkResultGate
{
    bool IsValid(string trxPath, string tfm);
}

public sealed class TrxBenchmarkResultGate : IBenchmarkResultGate
{
    public bool IsValid(string trxPath, string tfm)
    {
        var result = TrxResultParser.TryParse(trxPath, tfm);
        return result.IsComplete &&
            result.Results.Count > 0 &&
            result.Results.All(item => item.Outcome == TestOutcome.Passed);
    }
}

public sealed record BenchmarkFrameworkRun(
    string Tfm,
    string TrxName,
    CommandResult Command);

public sealed record BenchmarkRunResult(
    bool IsComplete,
    IReadOnlyList<BenchmarkFrameworkRun> Runs);
