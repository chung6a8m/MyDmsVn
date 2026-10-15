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

namespace MyDmsVn.SqlTestRunner.Focused;

public sealed record FocusedRunOptions(
    string RepositoryRoot,
    string TestProject,
    string ArtifactRoot,
    string ApprovalFile,
    string Category,
    IReadOnlyList<string> TargetFrameworks,
    TimeSpan CommandTimeout,
    TimeSpan OverallTimeout);

public sealed record FocusedFrameworkRun(
    string RunId,
    string Tfm,
    FocusedSqlTestResult Result,
    CommandResult Command);

public sealed record FocusedRunResult(
    bool IsComplete,
    IReadOnlyList<FocusedFrameworkRun> Runs);

public sealed record FocusedResultEvidence(
    string AssemblyPath,
    string Category,
    string DiscoveryOutput,
    string TrxPath,
    string Tfm,
    int RunnerExitCode);

public interface IFocusedResultGate
{
    FocusedSqlTestResult Verify(FocusedResultEvidence evidence);
}

public sealed class SourceFocusedResultGate : IFocusedResultGate
{
    public FocusedSqlTestResult Verify(FocusedResultEvidence evidence) =>
        FocusedSqlTestResultGate.Verify(
            TestMethodInventory.Read(evidence.AssemblyPath),
            evidence.Category,
            evidence.DiscoveryOutput,
            evidence.TrxPath,
            evidence.Tfm,
            evidence.RunnerExitCode);
}

public sealed class FocusedRunCoordinator
{
    private readonly IBenchmarkPreflightGate _preflight;
    private readonly IProcessRunner _processRunner;
    private readonly IBenchmarkClassificationGate _classificationGate;
    private readonly IFocusedResultGate _resultGate;
    private readonly IBenchmarkOwnershipGate _ownershipGate;

    public FocusedRunCoordinator(
        IBenchmarkPreflightGate preflight,
        IProcessRunner processRunner,
        IBenchmarkClassificationGate? classificationGate = null,
        IFocusedResultGate? resultGate = null,
        IBenchmarkOwnershipGate? ownershipGate = null)
    {
        _preflight = preflight;
        _processRunner = processRunner;
        _classificationGate = classificationGate ?? new SourceSqlTestClassificationGate();
        _resultGate = resultGate ?? new SourceFocusedResultGate();
        _ownershipGate = ownershipGate ?? new BenchmarkOwnershipGate();
    }

    public async Task<FocusedRunResult> RunAsync(
        FocusedRunOptions options,
        CancellationToken cancellationToken)
    {
        var runs = new List<FocusedFrameworkRun>();
        using var deadline = new CancellationTokenSource(options.OverallTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);
        try
        {
            if (!await _preflight.ValidateAsync(linked.Token).ConfigureAwait(false))
            {
                return new FocusedRunResult(false, runs);
            }

            foreach (var tfm in options.TargetFrameworks)
            {
                var assemblyPath = ResolveAssemblyPath(options.TestProject, tfm);
                if (!_classificationGate.IsValid(assemblyPath, options.RepositoryRoot))
                {
                    return new FocusedRunResult(false, runs);
                }

                var filter = "Category=" + options.Category;
                var discovery = await _processRunner.RunAsync(
                        new CommandSpec(
                            "dotnet",
                            TestArguments(options.TestProject, tfm) +
                            " --list-tests --filter \"" + filter + "\"",
                            options.RepositoryRoot,
                            options.CommandTimeout),
                        linked.Token)
                    .ConfigureAwait(false);
                if (discovery.ExitCode != 0 || discovery.TimedOut)
                {
                    return new FocusedRunResult(false, runs);
                }

                var runId = "focused-" + options.Category.ToLowerInvariant() + "-" +
                    tfm.Replace('.', '-') + "-" + Guid.NewGuid().ToString("N");
                var runRoot = Path.Combine(options.ArtifactRoot, runId);
                var journal = Path.Combine(runRoot, "ownership");
                var discoveryPath = Path.Combine(runRoot, "discovery.txt");
                var trxName = runId + ".trx";
                var trxPath = Path.Combine(runRoot, trxName);
                var timingPath = Path.Combine(runRoot, "timing.jsonl");
                var recoveryPath = Path.Combine(runRoot, "ownership-recovery.json");
                Directory.CreateDirectory(journal);
                File.WriteAllText(discoveryPath, discovery.StandardOutput);

                var environment = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [SqlTestHarnessEnvironment.ApprovalFileVariable] = options.ApprovalFile,
                    [SqlTestHarnessEnvironment.RunIdVariable] = runId,
                    [SqlTestHarnessEnvironment.JournalDirectoryVariable] = journal,
                    [SqlTestHarnessEnvironment.TimingEnabledVariable] = "1",
                    [SqlTestHarnessEnvironment.TimingOutputVariable] = timingPath,
                };
                var arguments = TestArguments(options.TestProject, tfm) +
                    " --filter \"" + filter + "\"" +
                    " --logger \"trx;LogFileName=" + trxName + "\"" +
                    " --results-directory \"" + runRoot + "\"";
                CommandResult? command = null;
                BenchmarkOwnershipResult ownership = new BenchmarkOwnershipResult(false, recoveryPath);
                var processLaunched = false;
                try
                {
                    processLaunched = true;
                    command = await _processRunner.RunAsync(
                            new CommandSpec(
                                "dotnet",
                                arguments,
                                options.RepositoryRoot,
                                options.CommandTimeout,
                                environment),
                            linked.Token)
                        .ConfigureAwait(false);
                }
                finally
                {
                    if (processLaunched)
                    {
                        using var ownershipDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        ownership = await _ownershipGate.ValidateAsync(
                                new BenchmarkOwnershipEvidence(
                                    runId,
                                    journal,
                                    recoveryPath,
                                    options.ApprovalFile,
                                    command is not null && command.ExitCode == 0 && !command.TimedOut),
                                ownershipDeadline.Token)
                            .ConfigureAwait(false);
                    }
                }

                if (command is null || command.ExitCode != 0 || command.TimedOut || !ownership.IsComplete)
                {
                    return new FocusedRunResult(false, runs);
                }

                var result = _resultGate.Verify(new FocusedResultEvidence(
                    assemblyPath,
                    options.Category,
                    discovery.StandardOutput,
                    trxPath,
                    tfm,
                    command.ExitCode));
                runs.Add(new FocusedFrameworkRun(runId, tfm, result, command));
                Console.WriteLine(
                    "category={0} tfm={1} selectedMethods={2} discoveredCases={3} executedCases={4} skippedCases={5} status={6}",
                    options.Category,
                    tfm,
                    result.SelectedMethods,
                    result.DiscoveredCases,
                    result.ExecutedCases,
                    result.SkippedCases,
                    result.IsComplete ? "complete" : "incomplete");
                if (!result.IsComplete)
                {
                    return new FocusedRunResult(false, runs);
                }
            }

            return new FocusedRunResult(true, runs);
        }
        catch (OperationCanceledException) when (
            deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new FocusedRunResult(false, runs);
        }
    }

    private static string ResolveAssemblyPath(string projectPath, string tfm)
    {
        var fullProjectPath = Path.GetFullPath(projectPath);
        return Path.Combine(
            Path.GetDirectoryName(fullProjectPath)!,
            "bin",
            "Release",
            tfm,
            Path.GetFileNameWithoutExtension(fullProjectPath) + ".dll");
    }

    private static string TestArguments(string projectPath, string tfm) =>
        "test \"" + projectPath + "\" -c Release -f " + tfm + " --no-build --no-restore";
}
