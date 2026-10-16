using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reconciliation;
using MyDmsVn.SqlTestRunner.Reporting;
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
    private readonly IBenchmarkOwnershipGate _ownershipGate;
    private readonly IBenchmarkReportExporter _reportExporter;
    private readonly IBenchmarkClassificationGate _classificationGate;

    public BenchmarkRunCoordinator(
        IBenchmarkPreflightGate preflight,
        IProcessRunner processRunner,
        IBenchmarkResultGate? resultGate = null,
        IBenchmarkOwnershipGate? ownershipGate = null,
        IBenchmarkReportExporter? reportExporter = null,
        IBenchmarkClassificationGate? classificationGate = null)
    {
        _preflight = preflight;
        _processRunner = processRunner;
        _resultGate = resultGate ?? new TrxBenchmarkResultGate();
        _ownershipGate = ownershipGate ?? new BenchmarkOwnershipGate();
        _reportExporter = reportExporter ?? new SqlPerformanceReportExporter();
        _classificationGate = classificationGate ?? new SourceSqlTestClassificationGate();
    }

    public async Task<BenchmarkRunResult> RunAsync(
        BenchmarkRunOptions options,
        CancellationToken cancellationToken)
    {
        var runs = new List<BenchmarkFrameworkRun>();
        var recovery = new SqlExecutionRecoveryState();
        using var deadline = new CancellationTokenSource(options.OverallTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);
        try
        {
            return await RunCoreAsync(options, runs, recovery, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new BenchmarkRunResult(false, runs, recovery.RecoveryRequired);
        }
        catch (Exception) when (!recovery.RecoveryRequired)
        {
            return new BenchmarkRunResult(false, runs, recovery.RecoveryRequired);
        }
    }

    private async Task<BenchmarkRunResult> RunCoreAsync(
        BenchmarkRunOptions options,
        List<BenchmarkFrameworkRun> runs,
        SqlExecutionRecoveryState recovery,
        CancellationToken cancellationToken)
    {
        var isAcceptance = string.Equals(
            options.Mode,
            "pr-acceptance",
            StringComparison.Ordinal);
        if (isAcceptance &&
            (options.WarmupPasses != 0 ||
             options.MeasuredPasses != 1 ||
             !IsSupportedAcceptanceFrameworkProfile(
                 options.TargetFrameworks,
                 options.ManifestAuthorizedActiveFrameworkProfile)))
        {
            return new BenchmarkRunResult(false, runs, recovery.RecoveryRequired);
        }

        foreach (var tfm in options.TargetFrameworks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_classificationGate.IsValid(AssemblyPath(options, tfm), options.RepositoryRoot))
            {
                return new BenchmarkRunResult(false, runs, recovery.RecoveryRequired);
            }
        }

        var baselineId = Guid.NewGuid().ToString("N");
        var passes = Enumerable.Range(1, options.WarmupPasses)
            .Select(number => (Kind: BenchmarkPassKind.Warmup, Number: number))
            .Concat(Enumerable.Range(1, options.MeasuredPasses)
                .Select(number => (
                    Kind: isAcceptance
                        ? BenchmarkPassKind.Acceptance
                        : BenchmarkPassKind.Measured,
                    Number: number)))
            .ToArray();
        for (var passIndex = 0; passIndex < passes.Length; passIndex++)
        {
            var pass = passes[passIndex];
            var frameworks = passIndex % 2 == 0
                ? options.TargetFrameworks
                : options.TargetFrameworks.Reverse().ToArray();
            foreach (var tfm in frameworks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await _preflight.ValidateAsync(cancellationToken).ConfigureAwait(false))
                {
                    return new BenchmarkRunResult(false, runs, recovery.RecoveryRequired);
                }

                var discovery = await _processRunner.RunAsync(
                        new CommandSpec(
                            "dotnet",
                            "test \"" + options.TestProject + "\" -c Release -f " + tfm +
                            " --no-build --no-restore --list-tests",
                            options.RepositoryRoot,
                            options.CommandTimeout),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (discovery.ExitCode != 0 || discovery.TimedOut)
                {
                    return new BenchmarkRunResult(false, runs, recovery.RecoveryRequired);
                }

                var label = pass.Kind.ToString().ToLowerInvariant() + "-" + pass.Number.ToString("00");
                var executionId = baselineId + "-" + label + "-" + tfm.Replace('.', '-');
                var trxName = "sqlperf-" + executionId + ".trx";
                var timingName = "timings-" + executionId + ".jsonl";
                var timingPath = Path.Combine(options.ArtifactRoot, timingName);
                var journalDirectory = Path.Combine(
                    options.ArtifactRoot,
                    "ownership",
                    executionId);
                var recoveryPath = Path.Combine(
                    options.ArtifactRoot,
                    "ownership-recovery-" + executionId + ".json");
                Directory.CreateDirectory(journalDirectory);
                var arguments =
                    "test \"" + options.TestProject + "\" -c Release -f " + tfm +
                    " --no-build --no-restore --logger \"trx;LogFileName=" + trxName +
                    "\" --results-directory \"" + options.ArtifactRoot + "\"";
                var environment = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [SqlTestHarnessEnvironment.ApprovalFileVariable] = options.ApprovalFile,
                    [SqlTestHarnessEnvironment.RunIdVariable] = executionId,
                    [SqlTestHarnessEnvironment.JournalDirectoryVariable] = journalDirectory,
                    [SqlTestHarnessEnvironment.TimingEnabledVariable] = "1",
                    [SqlTestHarnessEnvironment.TimingOutputVariable] = timingPath,
                };
                CommandResult? result = null;
                BenchmarkOwnershipResult ownershipResult =
                    new BenchmarkOwnershipResult(false, recoveryPath);
                var processLaunched = false;
                try
                {
                    processLaunched = true;
                    recovery.ExecutionStarted();
                    result = await _processRunner.RunAsync(
                            new CommandSpec(
                                "dotnet",
                                arguments,
                                options.RepositoryRoot,
                                options.CommandTimeout,
                                environment),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    if (processLaunched)
                    {
                        using var ownershipDeadline =
                            new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        ownershipResult = await _ownershipGate.ValidateAsync(
                                new BenchmarkOwnershipEvidence(
                                    executionId,
                                    journalDirectory,
                                    recoveryPath,
                                    options.ApprovalFile,
                                    result is not null && result.ExitCode == 0 && !result.TimedOut),
                                ownershipDeadline.Token)
                            .ConfigureAwait(false);
                        if (ownershipResult.IsComplete)
                        {
                            recovery.OwnershipReconciled();
                        }
                    }
                }

                if (result is null)
                {
                    return new BenchmarkRunResult(false, runs, recovery.RecoveryRequired);
                }

                var trxPath = Path.Combine(options.ArtifactRoot, trxName);
                runs.Add(new BenchmarkFrameworkRun(
                    executionId,
                    tfm,
                    Path.GetFullPath(trxPath),
                    Path.GetFullPath(timingPath),
                    pass.Kind,
                    pass.Number,
                    result,
                    discovery.StandardOutput));
                var assemblyPath = AssemblyPath(options, tfm);
                if (!ownershipResult.IsComplete || result.ExitCode != 0 || result.TimedOut ||
                    !_resultGate.IsValid(
                        new BenchmarkResultEvidence(
                            assemblyPath,
                            discovery.StandardOutput,
                            trxPath,
                            timingPath,
                            executionId,
                            tfm,
                            result.ExitCode,
                            options.RepositoryRoot,
                            Path.Combine(
                                options.RepositoryRoot,
                                "tests",
                                "tools",
                                "sqlperf-t01-migration-invocations.json"))))
                {
                    return new BenchmarkRunResult(false, runs, recovery.RecoveryRequired);
                }
            }
        }

        var export = await _reportExporter.ExportAsync(
                new BenchmarkReportEvidence(
                    options.ArtifactRoot,
                    options.TargetFrameworks,
                    options.MeasuredPasses,
                    runs,
                    options.Mode,
                    options.Revision),
                cancellationToken)
            .ConfigureAwait(false);
        var reportEvidence = new BenchmarkReportEvidence(
            options.ArtifactRoot,
            options.TargetFrameworks,
            options.MeasuredPasses,
            runs,
            options.Mode,
            options.Revision);
        var expectedReportPaths = _reportExporter.ExpectedReportPaths(reportEvidence)
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actualReportPaths = export.ReportPaths
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!export.IsComplete || expectedReportPaths.Count == 0 ||
            !actualReportPaths.SetEquals(expectedReportPaths) ||
            expectedReportPaths.Any(path => !File.Exists(path)))
        {
            return new BenchmarkRunResult(false, runs, recovery.RecoveryRequired);
        }

        return new BenchmarkRunResult(true, runs, recovery.RecoveryRequired);
    }

    private static bool IsSupportedAcceptanceFrameworkProfile(
        IReadOnlyList<string> targetFrameworks,
        bool manifestAuthorizedActiveFrameworkProfile) =>
        (manifestAuthorizedActiveFrameworkProfile &&
         targetFrameworks.SequenceEqual(new[] { "net48" }, StringComparer.Ordinal)) ||
        targetFrameworks.SequenceEqual(new[] { "net48", "net8.0" }, StringComparer.Ordinal);

    private static string AssemblyPath(BenchmarkRunOptions options, string tfm)
    {
        var projectPath = Path.IsPathRooted(options.TestProject)
            ? options.TestProject
            : Path.Combine(options.RepositoryRoot, options.TestProject);
        return Path.Combine(
            Path.GetDirectoryName(projectPath)!,
            "bin",
            "Release",
            tfm,
            Path.GetFileNameWithoutExtension(projectPath) + ".dll");
    }
}

public interface IBenchmarkResultGate
{
    bool IsValid(BenchmarkResultEvidence evidence);
}

public sealed class TrxBenchmarkResultGate : IBenchmarkResultGate
{
    private readonly IBenchmarkSourceInventory _sourceInventory;
    private readonly IBenchmarkMigrationManifestGate _migrationManifestGate;
    private readonly IBenchmarkClassificationGate _classificationGate;

    public TrxBenchmarkResultGate()
        : this(
            new ReflectionBenchmarkSourceInventory(),
            new SourceMigrationManifestGate(),
            new SourceSqlTestClassificationGate())
    {
    }

    public TrxBenchmarkResultGate(
        IBenchmarkSourceInventory sourceInventory,
        IBenchmarkMigrationManifestGate migrationManifestGate)
        : this(sourceInventory, migrationManifestGate, new AllowAllBenchmarkClassificationGate())
    {
    }

    public TrxBenchmarkResultGate(
        IBenchmarkSourceInventory sourceInventory,
        IBenchmarkMigrationManifestGate migrationManifestGate,
        IBenchmarkClassificationGate classificationGate)
    {
        _sourceInventory = sourceInventory ?? throw new ArgumentNullException(nameof(sourceInventory));
        _migrationManifestGate = migrationManifestGate ?? throw new ArgumentNullException(nameof(migrationManifestGate));
        _classificationGate = classificationGate ?? throw new ArgumentNullException(nameof(classificationGate));
    }

    public bool IsValid(BenchmarkResultEvidence evidence)
    {
        if (!_migrationManifestGate.IsValid(
                evidence.RepositoryRoot,
                evidence.MigrationManifestPath,
                evidence.Tfm))
        {
            return false;
        }

        var sourceMethods = _sourceInventory.Read(evidence.AssemblyPath);
        if (!_classificationGate.IsValid(evidence.AssemblyPath, evidence.RepositoryRoot))
        {
            return false;
        }

        var discovered = VstestDiscoveryParser.Parse(evidence.DiscoveryOutput, evidence.Tfm);
        if (sourceMethods.Count == 0 || discovered.Count == 0)
        {
            return false;
        }

        var sourceNames = new HashSet<string>(
            sourceMethods.Select(method => method.FullyQualifiedName),
            StringComparer.Ordinal);
        var discoveredNames = new HashSet<string>(
            discovered.Select(testCase => testCase.MethodName),
            StringComparer.Ordinal);
        if (sourceNames.Except(discoveredNames).Any() || discoveredNames.Except(sourceNames).Any())
        {
            return false;
        }

        foreach (var method in sourceMethods)
        {
            var discoveredCount = discovered.Count(testCase =>
                string.Equals(testCase.MethodName, method.FullyQualifiedName, StringComparison.Ordinal));
            if ((!method.IsTheory && discoveredCount != 1) ||
                (method.IsTheory && discoveredCount == 0))
            {
                return false;
            }
        }

        var trx = TrxResultParser.TryParse(evidence.TrxPath, evidence.Tfm);
        if (!trx.IsComplete)
        {
            return false;
        }

        if (!SqlTestInventoryReconciler.Reconcile(
                evidence.RunnerExitCode,
                discovered,
                trx.Results)
            .IsComplete)
        {
            return false;
        }

        return MigrationTimingArtifactReconciler.Reconcile(
                evidence.TimingPath,
                evidence.MigrationManifestPath,
                evidence.RepositoryRoot,
                evidence.RunId,
                evidence.Tfm,
                trx.Results)
            .IsComplete;
    }
}

public interface IBenchmarkSourceInventory
{
    IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath);
}

public sealed class ReflectionBenchmarkSourceInventory : IBenchmarkSourceInventory
{
    public IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath) =>
        TestMethodInventory.Read(assemblyPath);
}

public interface IBenchmarkMigrationManifestGate
{
    bool IsValid(string repositoryRoot, string manifestPath, string tfm);
}

public sealed class SourceMigrationManifestGate : IBenchmarkMigrationManifestGate
{
    public bool IsValid(string repositoryRoot, string manifestPath, string tfm) =>
        MigrationInvocationManifestAuditor.IsValid(repositoryRoot, manifestPath, tfm);
}

public interface IBenchmarkClassificationGate
{
    bool IsValid(string assemblyPath, string repositoryRoot);
}

public sealed class SourceSqlTestClassificationGate : IBenchmarkClassificationGate
{
    public bool IsValid(string assemblyPath, string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return false;
        }

        var manifestPath = Path.Combine(
            repositoryRoot,
            "tests",
            "tools",
            "sql-test-nonsql-exemptions.json");
        try
        {
            return SqlTestClassificationVerifier.Verify(
                    TestMethodInventory.Read(assemblyPath),
                    NonSqlExemptionManifest.Read(manifestPath))
                .IsComplete;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is InvalidDataException ||
            exception is InvalidOperationException)
        {
            return false;
        }
    }
}

internal sealed class AllowAllBenchmarkClassificationGate : IBenchmarkClassificationGate
{
    public bool IsValid(string assemblyPath, string repositoryRoot) => true;
}

public sealed record BenchmarkResultEvidence(
    string AssemblyPath,
    string DiscoveryOutput,
    string TrxPath,
    string TimingPath,
    string RunId,
    string Tfm,
    int RunnerExitCode,
    string RepositoryRoot = "",
    string MigrationManifestPath = "");

public enum BenchmarkPassKind
{
    Warmup,
    Measured,
    Acceptance,
}

public sealed record BenchmarkFrameworkRun(
    string RunId,
    string Tfm,
    string TrxPath,
    string TimingPath,
    BenchmarkPassKind Kind,
    int PassNumber,
    CommandResult Command,
    string DiscoveryOutput = "");

public sealed record BenchmarkRunResult(
    bool IsComplete,
    IReadOnlyList<BenchmarkFrameworkRun> Runs,
    bool RecoveryRequired = false);

internal sealed class SqlExecutionRecoveryState
{
    private int _unreconciledExecutions;

    public bool RecoveryRequired => _unreconciledExecutions != 0;

    public void ExecutionStarted() => _unreconciledExecutions++;

    public void OwnershipReconciled() => _unreconciledExecutions--;
}
