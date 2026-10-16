using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reconciliation;
using MyDmsVn.SqlTestRunner.Reporting;
using MyDmsVn.SqlTestRunner.Trx;
using MyDmsVn.SqlTestHarness;
using MyDmsVn.SqlTestHarness.Approval;

namespace MyDmsVn.SqlTestRunner.Gates;

public enum TestGateRunStatus
{
    Succeeded,
    ResourceDenied,
    Incomplete,
}

internal static class TestGateTimeoutBudget
{
    public static TimeSpan Calculate(IEnumerable<int> sequentialGateTimeoutSeconds)
    {
        ArgumentNullException.ThrowIfNull(sequentialGateTimeoutSeconds);
        long totalSeconds = 0;
        var hasGate = false;
        foreach (var seconds in sequentialGateTimeoutSeconds)
        {
            if (seconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequentialGateTimeoutSeconds));
            }

            totalSeconds = checked(totalSeconds + seconds);
            hasGate = true;
        }

        if (!hasGate)
        {
            throw new ArgumentException("At least one sequential gate timeout is required.", nameof(sequentialGateTimeoutSeconds));
        }

        return TimeSpan.FromSeconds(totalSeconds);
    }
}

public sealed record TestGateRunOptions(
    string RepositoryRoot,
    string ManifestPath,
    TestGateMode Mode,
    string PlanId,
    string TaskId,
    string ArtifactRoot,
    string? ApprovalFile,
    string? Revision);

public sealed record TestGateRunResult(
    TestGateRunStatus Status,
    IReadOnlyList<TestGateFrameworkResult> Frameworks);

public sealed record TestGateFrameworkResult(
    string GateId,
    string Tfm,
    int SourceCount,
    int DiscoveredCount,
    int ExecutedCount,
    TimeSpan Duration);

public interface ITestGateSourceInventory
{
    IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath);
}

public sealed class TestGateSourceInventory : ITestGateSourceInventory
{
    public IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath) =>
        TestMethodInventory.Read(assemblyPath);
}

public interface ITestGateApprovalReader
{
    bool TryLoad(string path, out string fingerprint);
}

public sealed class TestGateApprovalReader : ITestGateApprovalReader
{
    public bool TryLoad(string path, out string fingerprint)
    {
        try
        {
            fingerprint = SqlInstanceApprovalLoader.Load(path).FingerprintSha256;
            return true;
        }
        catch (InvalidOperationException)
        {
            fingerprint = string.Empty;
            return false;
        }
    }
}

public interface ITestGateSqlAcceptanceRunner
{
    Task<BenchmarkRunResult> RunAsync(BenchmarkRunOptions options, CancellationToken cancellationToken);
}

public sealed class TestGateSqlAcceptanceRunner : ITestGateSqlAcceptanceRunner
{
    private readonly IProcessRunner _processes;

    public TestGateSqlAcceptanceRunner(IProcessRunner processes)
    {
        _processes = processes;
    }

    public Task<BenchmarkRunResult> RunAsync(
        BenchmarkRunOptions options,
        CancellationToken cancellationToken) => new BenchmarkRunCoordinator(
            new EnvironmentPreflightGate(),
            _processes,
            reportExporter: new SqlAcceptanceReportExporter()).RunAsync(options, cancellationToken);
}

public sealed class TestGateCoordinator
{
    private readonly IProcessRunner _processes;
    private readonly ITestGateSourceInventory _sourceInventory;
    private readonly ITestGateReportExporter _reports;
    private readonly ITestGateApprovalReader _approval;
    private readonly ISqlExecutionLeaseFactory _leases;
    private readonly ITestGateSqlAcceptanceRunner _sqlAcceptance;
    private readonly IAbandonedSqlRunRecovery? _recovery;

    public TestGateCoordinator(
        IProcessRunner processes,
        ITestGateSourceInventory sourceInventory,
        ITestGateReportExporter reports,
        ITestGateApprovalReader? approval = null,
        ISqlExecutionLeaseFactory? leases = null,
        ITestGateSqlAcceptanceRunner? sqlAcceptance = null,
        IAbandonedSqlRunRecovery? recovery = null)
    {
        _processes = processes;
        _sourceInventory = sourceInventory;
        _reports = reports;
        _approval = approval ?? new TestGateApprovalReader();
        _leases = leases ?? new SqlExecutionLeaseFactory();
        _sqlAcceptance = sqlAcceptance ?? new TestGateSqlAcceptanceRunner(processes);
        _recovery = recovery;
    }

    public async Task<TestGateRunResult> RunAsync(
        TestGateRunOptions options,
        CancellationToken cancellationToken)
    {
        var runs = new List<TestGateFrameworkResult>();
        TestGateSelection selection;
        TestGateManifest manifest;
        try
        {
            manifest = TestGateManifestLoader.Load(options.ManifestPath);
            selection = TestGateManifestValidator.Select(
                manifest,
                options.RepositoryRoot,
                options.Mode,
                options.PlanId,
                options.TaskId);
        }
        catch (InvalidDataException)
        {
            return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
        }

        if (options.Mode == TestGateMode.Task && selection.Gates.Any(gate =>
                gate.StrictMode != TestGateStrictMode.DotnetTest ||
                gate.Resources.Sql != TestGateResourceRequirement.None ||
                gate.Resources.Gui != TestGateResourceRequirement.None))
        {
            return new TestGateRunResult(TestGateRunStatus.ResourceDenied, runs);
        }

        var overallTimeout = TestGateTimeoutBudget.Calculate(
            selection.Gates.Select(gate => gate.OverallTimeoutSeconds));
        using var overall = new CancellationTokenSource(overallTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, overall.Token);
        try
        {
            var built = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prerequisite in selection.Gates.SelectMany(gate => gate.BuildPrerequisites)
                         .DistinctBy(item => item.Project + "|" + string.Join(",", item.Frameworks), StringComparer.OrdinalIgnoreCase))
            {
                foreach (var tfm in prerequisite.Frameworks)
                {
                    var buildKey = prerequisite.Project + "|" + tfm;
                    var build = await RunCommand(
                        "build \"" + FullPath(options.RepositoryRoot, prerequisite.Project) +
                        "\" -c Release -f " + tfm + " --no-restore",
                        options,
                        selection.Gates[0].CommandTimeoutSeconds,
                        linked.Token).ConfigureAwait(false);
                    if (!Successful(build))
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    built.Add(buildKey);
                }
            }

            var manifestFingerprint = HashFile(options.ManifestPath);
            var globalExpected = new List<ExpectedLogicalCase>();
            var globalSource = new List<BoundLogicalCase>();
            var globalDiscovered = new List<BoundLogicalCase>();
            var globalExecuted = new List<ExecutedLogicalCase>();
            foreach (var gate in selection.Gates)
            {
                if (gate.StrictMode == TestGateStrictMode.SqlPrAcceptance)
                {
                    var sqlStatus = await RunSqlGateAsync(
                        options,
                        manifest,
                        gate,
                        manifestFingerprint,
                        runs,
                        globalExpected,
                        globalSource,
                        globalDiscovered,
                        globalExecuted,
                        linked.Token).ConfigureAwait(false);
                    if (sqlStatus != TestGateRunStatus.Succeeded)
                    {
                        return new TestGateRunResult(sqlStatus, runs);
                    }

                    continue;
                }

                if (gate.StrictMode != TestGateStrictMode.DotnetTest)
                {
                    return new TestGateRunResult(TestGateRunStatus.ResourceDenied, runs);
                }

                foreach (var tfm in gate.Frameworks)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var project = FullPath(options.RepositoryRoot, gate.Project);
                    if (built.Add(gate.Project + "|" + tfm))
                    {
                        var build = await RunCommand(
                            "build \"" + project + "\" -c Release -f " + tfm + " --no-restore",
                            options,
                            gate.CommandTimeoutSeconds,
                            linked.Token).ConfigureAwait(false);
                        if (!Successful(build))
                        {
                            return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                        }
                    }

                    var filter = Filter(gate.Selection);
                    var testArguments = "test \"" + project + "\" -c Release -f " + tfm +
                        " --no-build --no-restore" + filter;
                    var discovery = await RunCommand(
                        testArguments + " --list-tests",
                        options,
                        gate.CommandTimeoutSeconds,
                        linked.Token).ConfigureAwait(false);
                    if (!Successful(discovery))
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    var runId = gate.GateId + "-" + tfm.Replace('.', '-') + "-" + Guid.NewGuid().ToString("N");
                    var runRoot = Path.Combine(options.ArtifactRoot, runId);
                    Directory.CreateDirectory(runRoot);
                    var trxName = runId + ".trx";
                    var execution = await RunCommand(
                        testArguments + " --logger \"trx;LogFileName=" + trxName +
                        "\" --results-directory \"" + runRoot + "\"",
                        options,
                        gate.CommandTimeoutSeconds,
                        linked.Token).ConfigureAwait(false);
                    if (!Successful(execution))
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    var definitions = manifest.LogicalCases;
                    var selectedDefinitions = SelectDefinitions(definitions, gate, tfm);
                    var source = _sourceInventory.Read(AssemblyPath(project, tfm));
                    var discovered = VstestDiscoveryParser.Parse(discovery.StandardOutput, tfm);
                    var trx = TrxResultParser.TryParse(Path.Combine(runRoot, trxName), tfm);
                    if (!trx.IsComplete)
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    var fullSourceBinding = LogicalCaseBinder.BindSource(
                        source,
                        definitions,
                        gate.Project,
                        tfm,
                        manifest.Exemptions);
                    var selectedIds = selectedDefinitions.Select(item => item.LogicalCaseId)
                        .ToHashSet(StringComparer.Ordinal);
                    var selectedSource = fullSourceBinding.Cases
                        .Where(item => selectedIds.Contains(item.LogicalCaseId))
                        .ToArray();
                    var discoveryBinding = LogicalCaseBinder.BindDiscovery(discovered, selectedDefinitions, gate.Project, tfm);
                    var executionBinding = LogicalCaseBinder.BindExecution(trx.Results, selectedDefinitions, gate.Project, tfm);
                    if (!fullSourceBinding.IsComplete || !discoveryBinding.IsComplete || !executionBinding.IsComplete)
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    var expected = selectedSource.Select(item => new ExpectedLogicalCase(
                            item.LogicalKey,
                            item.LogicalCaseId,
                            item.OwnerProject,
                            item.Tfm,
                            item.NormalizedMethodSignature,
                            item.RowKey,
                            item.ReviewedCodeFingerprint,
                            item.Classification))
                        .ToArray();
                    var reconciliation = LogicalCaseInventoryReconciler.Reconcile(
                        expected,
                        selectedSource,
                        discoveryBinding.Cases,
                        executionBinding.Cases);
                    if (!reconciliation.IsComplete)
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    globalExpected.AddRange(expected);
                    globalSource.AddRange(selectedSource);
                    globalDiscovered.AddRange(discoveryBinding.Cases);
                    globalExecuted.AddRange(executionBinding.Cases);

                    stopwatch.Stop();
                    var framework = new TestGateFrameworkResult(
                        gate.GateId,
                        tfm,
                        selectedSource.Length,
                        discoveryBinding.Cases.Count,
                        executionBinding.Cases.Count,
                        stopwatch.Elapsed);
                    runs.Add(framework);
                    var report = await _reports.ExportAsync(
                        Evidence(
                            options,
                            gate,
                            framework,
                            executionBinding.Cases,
                            manifestFingerprint,
                            Path.Combine(options.ArtifactRoot, gate.GateId + "-" + tfm + ".sanitized.json")),
                        linked.Token).ConfigureAwait(false);
                    if (!report.IsComplete)
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }
                }
            }

            var globalSelectedIds = globalExpected.Select(item => item.LogicalCaseId)
                .ToHashSet(StringComparer.Ordinal);
            var selectedSignatures = globalExpected.Select(item => item.NormalizedMethodSignature)
                .ToHashSet(StringComparer.Ordinal);
            if (!LogicalCaseInventoryReconciler.Reconcile(
                    globalExpected,
                    globalSource,
                    globalDiscovered,
                    globalExecuted,
                    manifest.IdentityMappings.Where(mapping =>
                            globalSelectedIds.Contains(mapping.OldLogicalCaseId) &&
                            globalSelectedIds.Contains(mapping.NewLogicalCaseId))
                        .ToArray(),
                    manifest.Exemptions.Where(exemption =>
                            selectedSignatures.Contains(exemption.NormalizedMethodSignature))
                        .ToArray()).IsComplete)
            {
                return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
            }

            return new TestGateRunResult(TestGateRunStatus.Succeeded, runs);
        }
        catch (OperationCanceledException)
        {
            return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is InvalidDataException ||
            exception is ArgumentException)
        {
            return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
        }
    }

    private async Task<TestGateRunStatus> RunSqlGateAsync(
        TestGateRunOptions options,
        TestGateManifest manifest,
        TestGateDefinition gate,
        string manifestFingerprint,
        List<TestGateFrameworkResult> frameworks,
        List<ExpectedLogicalCase> globalExpected,
        List<BoundLogicalCase> globalSource,
        List<BoundLogicalCase> globalDiscovered,
        List<ExecutedLogicalCase> globalExecuted,
        CancellationToken cancellationToken)
    {
        if (options.Mode != TestGateMode.Final ||
            string.IsNullOrWhiteSpace(options.ApprovalFile) ||
            string.IsNullOrWhiteSpace(options.Revision) ||
            gate.Frameworks.Count == 0 ||
            !_approval.TryLoad(options.ApprovalFile, out var instanceFingerprint))
        {
            return TestGateRunStatus.ResourceDenied;
        }

        var recovery = _recovery ?? new BenchmarkAbandonedSqlRunRecovery(options.ApprovalFile);
        var acquired = await _leases.AcquireAsync(
            new SqlExecutionLeaseRequest(
                instanceFingerprint,
                options.PlanId + "/" + options.TaskId + "/" + gate.GateId,
                options.ArtifactRoot,
                TimeSpan.FromSeconds(gate.CommandTimeoutSeconds),
                TimeSpan.FromSeconds(gate.CommandTimeoutSeconds),
                recovery),
            cancellationToken).ConfigureAwait(false);
        if (acquired.Lease is null ||
            acquired.Status is SqlExecutionLeaseAcquireStatus.TimedOut or
                SqlExecutionLeaseAcquireStatus.Cancelled or
                SqlExecutionLeaseAcquireStatus.BlockedRecovery)
        {
            return TestGateRunStatus.ResourceDenied;
        }

        try
        {
            Environment.SetEnvironmentVariable(
                SqlTestHarnessEnvironment.ApprovalFileVariable,
                options.ApprovalFile);
            var benchmark = await _sqlAcceptance.RunAsync(
                new BenchmarkRunOptions(
                    options.RepositoryRoot,
                    FullPath(options.RepositoryRoot, gate.Project),
                    options.ArtifactRoot,
                    options.ApprovalFile,
                    gate.Frameworks,
                    TimeSpan.FromSeconds(gate.CommandTimeoutSeconds),
                    WarmupPasses: 0,
                    MeasuredPasses: 1,
                    OverallRunTimeout: TimeSpan.FromSeconds(gate.OverallTimeoutSeconds))
                {
                    Mode = "pr-acceptance",
                    Revision = options.Revision,
                },
                cancellationToken).ConfigureAwait(false);
            if (!benchmark.RecoveryRequired)
            {
                acquired.Lease.Complete();
            }

            if (!benchmark.IsComplete || benchmark.Runs.Count != gate.Frameworks.Count ||
                !benchmark.Runs.Select(run => run.Tfm).SequenceEqual(gate.Frameworks, StringComparer.Ordinal))
            {
                return TestGateRunStatus.Incomplete;
            }

            var expected = new List<ExpectedLogicalCase>();
            var source = new List<BoundLogicalCase>();
            var discovered = new List<BoundLogicalCase>();
            var executed = new List<ExecutedLogicalCase>();
            foreach (var run in benchmark.Runs)
            {
                var project = FullPath(options.RepositoryRoot, gate.Project);
                var sourceBinding = LogicalCaseBinder.BindSource(
                    _sourceInventory.Read(AssemblyPath(project, run.Tfm)),
                    manifest.LogicalCases,
                    gate.Project,
                    run.Tfm,
                    manifest.Exemptions);
                var discoveryBinding = LogicalCaseBinder.BindDiscovery(
                    VstestDiscoveryParser.Parse(run.DiscoveryOutput, run.Tfm),
                    manifest.LogicalCases,
                    gate.Project,
                    run.Tfm);
                var trx = TrxResultParser.TryParse(run.TrxPath, run.Tfm);
                if (!trx.IsComplete)
                {
                    return TestGateRunStatus.Incomplete;
                }

                var executionBinding = LogicalCaseBinder.BindExecution(
                    trx.Results,
                    manifest.LogicalCases,
                    gate.Project,
                    run.Tfm);
                if (!sourceBinding.IsComplete || !discoveryBinding.IsComplete || !executionBinding.IsComplete)
                {
                    return TestGateRunStatus.Incomplete;
                }

                source.AddRange(sourceBinding.Cases);
                discovered.AddRange(discoveryBinding.Cases);
                executed.AddRange(executionBinding.Cases);
                expected.AddRange(sourceBinding.Cases.Select(item => new ExpectedLogicalCase(
                    item.LogicalKey,
                    item.LogicalCaseId,
                    item.OwnerProject,
                    item.Tfm,
                    item.NormalizedMethodSignature,
                    item.RowKey,
                    item.ReviewedCodeFingerprint,
                    item.Classification)));
                frameworks.Add(new TestGateFrameworkResult(
                    gate.GateId,
                    run.Tfm,
                    sourceBinding.Cases.Count,
                    discoveryBinding.Cases.Count,
                    executionBinding.Cases.Count,
                    run.Command.Duration));
            }

            if (!LogicalCaseInventoryReconciler.Reconcile(
                    expected,
                    source,
                    discovered,
                    executed).IsComplete)
            {
                return TestGateRunStatus.Incomplete;
            }

            globalExpected.AddRange(expected);
            globalSource.AddRange(source);
            globalDiscovered.AddRange(discovered);
            globalExecuted.AddRange(executed);

            var report = await _reports.ExportAsync(
                new TestGateReportEvidence(
                    Path.Combine(options.ArtifactRoot, gate.GateId + ".sanitized.json"),
                    options.PlanId,
                    options.TaskId,
                    options.Mode,
                    gate.GateId,
                    options.Revision,
                    manifestFingerprint,
                    gate.Frameworks.Select(tfm => gate.Module + "/" + tfm).ToArray(),
                    source.Count,
                    discovered.Count,
                    executed.Count,
                    executed.Count(item => item.Outcome == TestOutcome.Passed),
                    executed.Count(item => item.Outcome == TestOutcome.Failed),
                    executed.Count(item => item.Outcome == TestOutcome.Skipped),
                    TimeSpan.FromTicks(benchmark.Runs.Sum(run => run.Command.Duration.Ticks)),
                    TimedOut: benchmark.Runs.Any(run => run.Command.TimedOut),
                    ResourceDenied: false,
                    RecoveryRequired: acquired.Status == SqlExecutionLeaseAcquireStatus.Recovered,
                    WatchdogClean: benchmark.Runs.All(run => !run.Command.TimedOut),
                    OwnershipClean: benchmark.IsComplete,
                    InventoryComplete: true,
                    PrivateValues: new[]
                    {
                        options.ArtifactRoot,
                        options.ApprovalFile,
                        instanceFingerprint,
                    }),
                cancellationToken).ConfigureAwait(false);
            if (!report.IsComplete)
            {
                return TestGateRunStatus.Incomplete;
            }

            acquired.Lease.Complete();
            return TestGateRunStatus.Succeeded;
        }
        finally
        {
            await acquired.Lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    private Task<CommandResult> RunCommand(
        string arguments,
        TestGateRunOptions options,
        int timeoutSeconds,
        CancellationToken cancellationToken) => _processes.RunAsync(
        new CommandSpec(
            "dotnet",
            arguments,
            options.RepositoryRoot,
            TimeSpan.FromSeconds(timeoutSeconds)),
        cancellationToken);

    private static bool Successful(CommandResult result) => result.ExitCode == 0 && !result.TimedOut;

    private static string Filter(TestGateSelectionDefinition selection) => selection.Kind switch
    {
        TestGateSelectionKind.UnfilteredProject when selection.Patterns.Count == 0 => string.Empty,
        TestGateSelectionKind.FullyQualifiedName when selection.Patterns.Count != 0 =>
            " --filter \"" + string.Join("|", selection.Patterns.Select(value => "FullyQualifiedName~" + value)) + "\"",
        TestGateSelectionKind.Category when selection.Patterns.Count != 0 =>
            " --filter \"" + string.Join("|", selection.Patterns.Select(value => "Category=" + value)) + "\"",
        _ => throw new InvalidDataException("Unsupported manifest-owned test selection."),
    };

    private static IReadOnlyList<LogicalCaseDefinition> SelectDefinitions(
        IReadOnlyCollection<LogicalCaseDefinition> definitions,
        TestGateDefinition gate,
        string tfm)
    {
        var owned = definitions.Where(definition =>
            string.Equals(definition.OwnerProject, gate.Project, StringComparison.OrdinalIgnoreCase) &&
            definition.Frameworks.Contains(tfm, StringComparer.OrdinalIgnoreCase));
        return gate.Selection.Kind switch
        {
            TestGateSelectionKind.UnfilteredProject => owned.ToArray(),
            TestGateSelectionKind.FullyQualifiedName => owned.Where(definition =>
                    gate.Selection.Patterns.Any(pattern => MethodName(definition.NormalizedMethodSignature)
                        .Contains(pattern, StringComparison.Ordinal)))
                .ToArray(),
            TestGateSelectionKind.Category => owned.Where(definition =>
                    gate.Selection.Patterns.Any(pattern => definition.Classification.Contains(
                        pattern,
                        StringComparer.Ordinal)))
                .ToArray(),
            _ => Array.Empty<LogicalCaseDefinition>(),
        };
    }

    private static string MethodName(string normalizedSignature)
    {
        var parenthesis = normalizedSignature.IndexOf('(');
        return parenthesis < 0 ? normalizedSignature : normalizedSignature.Substring(0, parenthesis);
    }

    private static string FullPath(string root, string relative) => Path.GetFullPath(Path.Combine(root, relative));

    private static string AssemblyPath(string project, string tfm) => Path.Combine(
        Path.GetDirectoryName(project)!,
        "bin",
        "Release",
        tfm,
        Path.GetFileNameWithoutExtension(project) + ".dll");

    private static string HashFile(string path) => Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(path)))
        .ToLowerInvariant();

    private static TestGateReportEvidence Evidence(
        TestGateRunOptions options,
        TestGateDefinition gate,
        TestGateFrameworkResult framework,
        IReadOnlyCollection<ExecutedLogicalCase> executed,
        string manifestFingerprint,
        string outputPath) => new(
        outputPath,
        options.PlanId,
        options.TaskId,
        options.Mode,
        gate.GateId,
        options.Revision ?? string.Empty,
        manifestFingerprint,
        new[] { gate.Module + "/" + framework.Tfm },
        framework.SourceCount,
        framework.DiscoveredCount,
        framework.ExecutedCount,
        executed.Count(item => item.Outcome == TestOutcome.Passed),
        executed.Count(item => item.Outcome == TestOutcome.Failed),
        executed.Count(item => item.Outcome == TestOutcome.Skipped),
        framework.Duration,
        TimedOut: false,
        ResourceDenied: false,
        RecoveryRequired: false,
        WatchdogClean: true,
        OwnershipClean: true,
        InventoryComplete: true,
        PrivateValues: new[] { options.ArtifactRoot, options.ApprovalFile ?? string.Empty });
}

internal sealed class BenchmarkAbandonedSqlRunRecovery : IAbandonedSqlRunRecovery
{
    private readonly string _approvalFile;
    private readonly IBenchmarkOwnershipGate _ownershipGate;

    public BenchmarkAbandonedSqlRunRecovery(string approvalFile)
        : this(approvalFile, new BenchmarkOwnershipGate())
    {
    }

    internal BenchmarkAbandonedSqlRunRecovery(
        string approvalFile,
        IBenchmarkOwnershipGate ownershipGate)
    {
        _approvalFile = approvalFile;
        _ownershipGate = ownershipGate ?? throw new ArgumentNullException(nameof(ownershipGate));
    }

    public async Task<bool> ReconcileAsync(AbandonedSqlRun lease, CancellationToken cancellationToken)
    {
        var ownershipRoot = Path.Combine(lease.PrivateArtifactRoot, "ownership");
        if (!Directory.Exists(ownershipRoot))
        {
            return false;
        }

        var journals = Directory.GetDirectories(ownershipRoot);
        if (journals.Length == 0)
        {
            return false;
        }

        foreach (var journal in journals)
        {
            var runId = Path.GetFileName(journal);
            var result = await _ownershipGate.ValidateAsync(
                new BenchmarkOwnershipEvidence(
                    runId,
                    journal,
                    Path.Combine(lease.PrivateArtifactRoot, "ownership-recovery-" + runId + ".json"),
                    _approvalFile,
                    ProcessSucceeded: false),
                cancellationToken).ConfigureAwait(false);
            if (!result.IsComplete)
            {
                return false;
            }
        }

        return true;
    }
}
