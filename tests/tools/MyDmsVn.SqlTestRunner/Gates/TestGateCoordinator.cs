using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
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
    private readonly ISqlGateManifestPreflight _sqlManifestPreflight;

    public TestGateCoordinator(
        IProcessRunner processes,
        ITestGateSourceInventory sourceInventory,
        ITestGateReportExporter reports,
        ITestGateApprovalReader? approval = null,
        ISqlExecutionLeaseFactory? leases = null,
        ITestGateSqlAcceptanceRunner? sqlAcceptance = null,
        IAbandonedSqlRunRecovery? recovery = null,
        ISqlGateManifestPreflight? sqlManifestPreflight = null)
    {
        _processes = processes;
        _sourceInventory = sourceInventory;
        _reports = reports;
        _approval = approval ?? new TestGateApprovalReader();
        _leases = leases ?? new SqlExecutionLeaseFactory();
        _sqlAcceptance = sqlAcceptance ?? new TestGateSqlAcceptanceRunner(processes);
        _recovery = recovery;
        _sqlManifestPreflight = sqlManifestPreflight ??
            new SqlGateManifestPreflight(processes, sourceInventory);
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

        if (TestGateArtifactPathPlanner.FindOverBudgetPaths(
                options.ArtifactRoot, selection.Gates, options.RepositoryRoot).Count != 0)
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
            var sqlGates = selection.Gates.Where(gate =>
                gate.StrictMode == TestGateStrictMode.SqlPrAcceptance).ToArray();
            var sqlGatesExecuted = false;
            foreach (var gate in selection.Gates)
            {
                if (gate.StrictMode == TestGateStrictMode.SqlPrAcceptance)
                {
                    if (sqlGatesExecuted)
                    {
                        continue;
                    }

                    sqlGatesExecuted = true;
                    var sqlStatus = await RunSqlGatesAsync(
                            options, manifest, sqlGates, manifestFingerprint, runs,
                            globalExpected, globalSource, globalDiscovered, globalExecuted,
                            linked.Token)
                        .ConfigureAwait(false);
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

                    var definitions = manifest.LogicalCases;
                    var selectedDefinitions = SelectDefinitions(definitions, gate, tfm);
                    if (selectedDefinitions.Count == 0)
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    var source = _sourceInventory.Read(AssemblyPath(project, tfm));
                    var discovered = VstestDiscoveryParser.Parse(discovery.StandardOutput, tfm);
                    var taskFingerprints = options.Mode == TestGateMode.Task
                        ? selectedDefinitions.Select(item => item.NormalizedMethodSignature)
                            .ToHashSet(StringComparer.Ordinal)
                        : null;
                    var fullSourceBinding = LogicalCaseBinder.BindSource(
                        source,
                        definitions,
                        gate.Project,
                        tfm,
                        manifest.Exemptions,
                        taskFingerprints);
                    var selectedIds = selectedDefinitions.Select(item => item.LogicalCaseId)
                        .ToHashSet(StringComparer.Ordinal);
                    var selectedSource = fullSourceBinding.Cases
                        .Where(item => selectedIds.Contains(item.LogicalCaseId))
                        .ToArray();
                    var discoveryBinding = LogicalCaseBinder.BindDiscovery(
                        discovered, selectedDefinitions, gate.Project, tfm);
                    if (!fullSourceBinding.IsComplete || !discoveryBinding.IsComplete ||
                        selectedSource.Length == 0)
                    {
                        // Report safe identities, never raw Theory arguments or environment data.
                        // Manifest errors fail before executing any selected test.
                        Console.Error.WriteLine(
                            "Task/Final manifest preflight: " + gate.GateId + "/" + tfm +
                            ": source=" + string.Join(",", fullSourceBinding.Failures) +
                            "; discovery=" + string.Join(",", discoveryBinding.Failures));
                        foreach (var expectedMethod in selectedDefinitions.Take(20))
                        {
                            var methods = source.Where(item => string.Equals(
                                item.NormalizedSignature, expectedMethod.NormalizedMethodSignature,
                                StringComparison.Ordinal)).ToArray();
                            if (methods.Length == 0)
                            {
                                Console.Error.WriteLine(
                                    "MissingMethod: " + expectedMethod.NormalizedMethodSignature);
                            }
                            else if (methods.Length == 1 &&
                                !string.Equals(methods[0].BodyFingerprint,
                                    expectedMethod.ReviewedCodeFingerprint, StringComparison.Ordinal))
                            {
                                Console.Error.WriteLine(
                                    "FingerprintMismatch: " + expectedMethod.NormalizedMethodSignature);
                            }
                        }

                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    var runId = gate.GateId + "-" + tfm.Replace('.', '-') + "-" + Guid.NewGuid().ToString("N");
                    var runRoot = Path.Combine(options.ArtifactRoot, TestGateArtifactPathPlanner.NewPhysicalId());
                    Directory.CreateDirectory(runRoot);
                    TestGateArtifactPathPlanner.WriteLogicalRunId(runRoot, runId);
                    var trxName = "result.trx";
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

                    var trx = TrxResultParser.TryParse(Path.Combine(runRoot, trxName), tfm);
                    if (!trx.IsComplete)
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    var executionBinding = LogicalCaseBinder.BindExecution(
                        trx.Results, selectedDefinitions, gate.Project, tfm);
                    if (!executionBinding.IsComplete)
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

    private async Task<TestGateRunStatus> RunSqlGatesAsync(
        TestGateRunOptions options,
        TestGateManifest manifest,
        IReadOnlyList<TestGateDefinition> gates,
        string manifestFingerprint,
        List<TestGateFrameworkResult> frameworks,
        List<ExpectedLogicalCase> globalExpected,
        List<BoundLogicalCase> globalSource,
        List<BoundLogicalCase> globalDiscovered,
        List<ExecutedLogicalCase> globalExecuted,
        CancellationToken cancellationToken)
    {
        if (options.Mode != TestGateMode.Final || gates.Count == 0 ||
            string.IsNullOrWhiteSpace(options.ApprovalFile) ||
            string.IsNullOrWhiteSpace(options.Revision) ||
            gates.Any(gate => gate.Frameworks.Count == 0) ||
            !_approval.TryLoad(options.ApprovalFile, out var instanceFingerprint))
        {
            return TestGateRunStatus.ResourceDenied;
        }

        // Do not acquire the SQL lease or launch any SQL acceptance process
        // until source, fingerprints, ownership and expanded discovery agree.
        var preflight = await _sqlManifestPreflight.VerifyAsync(
            options, manifest, gates, cancellationToken).ConfigureAwait(false);
        if (!preflight.IsComplete)
        {
            foreach (var issue in preflight.Issues.Take(40))
            {
                Console.Error.WriteLine(
                    "SQL manifest preflight: " + issue.GateId + "/" + issue.Framework +
                    ": " + issue.Reason);
            }

            if (preflight.Issues.Count > 40)
            {
                Console.Error.WriteLine(
                    "SQL manifest preflight: " + (preflight.Issues.Count - 40) +
                    " further issues omitted.");
            }

            return TestGateRunStatus.Incomplete;
        }

        var recovery = _recovery ?? new BenchmarkAbandonedSqlRunRecovery(options.ApprovalFile);
        var leaseTimeout = TestGateTimeoutBudget.Calculate(gates.Select(gate => gate.OverallTimeoutSeconds));
        var acquired = await _leases.AcquireAsync(
            new SqlExecutionLeaseRequest(
                instanceFingerprint,
                options.PlanId + "/" + options.TaskId + "/" + string.Join("+", gates.Select(gate => gate.GateId)),
                options.ArtifactRoot,
                leaseTimeout,
                leaseTimeout,
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
            // Recovery of a prior invocation is historical evidence, not an unresolved
            // resource owned by the current invocation. Only the latter may keep
            // the current lease in RecoveryRequired when it is disposed.
            var priorLeaseRecovered = acquired.Status == SqlExecutionLeaseAcquireStatus.Recovered;
            var currentRunNeedsRecovery = false;
            foreach (var gate in gates)
            {
                var benchmarkArtifactRoot = Path.Combine(options.ArtifactRoot, gate.GateId);
                Directory.CreateDirectory(benchmarkArtifactRoot);
                var benchmark = await _sqlAcceptance.RunAsync(
                    new BenchmarkRunOptions(
                        options.RepositoryRoot,
                        FullPath(options.RepositoryRoot, gate.Project),
                        benchmarkArtifactRoot,
                        options.ApprovalFile,
                        gate.Frameworks,
                        TimeSpan.FromSeconds(gate.CommandTimeoutSeconds),
                        WarmupPasses: 0,
                        MeasuredPasses: 1,
                        OverallRunTimeout: TimeSpan.FromSeconds(gate.OverallTimeoutSeconds))
                    {
                        Mode = "pr-acceptance",
                        Revision = options.Revision,
                        ManifestAuthorizedActiveFrameworkProfile = true,
                    }, cancellationToken).ConfigureAwait(false);
                currentRunNeedsRecovery |= benchmark.RecoveryRequired;

                if (!benchmark.IsComplete || currentRunNeedsRecovery || benchmark.Runs.Count != gate.Frameworks.Count ||
                    !benchmark.Runs.Select(run => run.Tfm).SequenceEqual(gate.Frameworks, StringComparer.Ordinal))
                {
                    if (!currentRunNeedsRecovery)
                    {
                        acquired.Lease.Complete();
                    }

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
                        RecoveryRequired: priorLeaseRecovered || currentRunNeedsRecovery,
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
            }

            if (!currentRunNeedsRecovery)
            {
                acquired.Lease.Complete();
            }

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
    private readonly Func<string, string, CancellationToken, Task<bool>> _emptyJournalPathFailureVerifier;

    public BenchmarkAbandonedSqlRunRecovery(string approvalFile)
        : this(approvalFile, new BenchmarkOwnershipGate())
    {
    }

    internal BenchmarkAbandonedSqlRunRecovery(
        string approvalFile,
        IBenchmarkOwnershipGate ownershipGate,
        Func<string, string, CancellationToken, Task<bool>>? emptyJournalPathFailureVerifier = null)
    {
        _approvalFile = approvalFile;
        _ownershipGate = ownershipGate ?? throw new ArgumentNullException(nameof(ownershipGate));
        _emptyJournalPathFailureVerifier = emptyJournalPathFailureVerifier ?? VerifyNoOwnedDatabasesAsync;
    }

    public async Task<bool> ReconcileAsync(AbandonedSqlRun lease, CancellationToken cancellationToken)
    {
        var artifactRoots = new[] { lease.PrivateArtifactRoot }
            .Concat(Directory.Exists(lease.PrivateArtifactRoot)
                ? Directory.GetDirectories(lease.PrivateArtifactRoot)
                : Array.Empty<string>())
            .ToArray();
        var journals = artifactRoots
            .SelectMany(root =>
            {
                var ownershipRoot = Path.Combine(root, "ownership");
                return Directory.Exists(ownershipRoot) ? Directory.GetDirectories(ownershipRoot) : Array.Empty<string>();
            })
            .ToArray();
        if (journals.Length == 0)
        {
            return false;
        }

        foreach (var journal in journals)
        {
            var runId = TestGateArtifactPathPlanner.ReadLogicalRunId(journal);
            var artifactRoot = Directory.GetParent(journal)!.Parent!.FullName;
            if (IsVerifiedZeroExecution(artifactRoot, journal, runId))
            {
                continue;
            }

            if (IsVerifiedJournalPathFailure(artifactRoot, journal, runId) &&
                await _emptyJournalPathFailureVerifier(artifactRoot, journal, cancellationToken)
                    .ConfigureAwait(false))
            {
                continue;
            }

            var result = await _ownershipGate.ValidateAsync(
                new BenchmarkOwnershipEvidence(
                    runId,
                    journal,
                    RecoveryPath(artifactRoot, journal, runId),
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

    private static string RecoveryPath(string artifactRoot, string journal, string logicalRunId)
    {
        var hasPhysicalMapping = File.Exists(Path.Combine(
            journal, TestGateArtifactPathPlanner.LogicalRunIdFileName));
        return hasPhysicalMapping
            ? Path.Combine(artifactRoot, "recovery-" + Path.GetFileName(journal) + ".json")
            : Path.Combine(artifactRoot, "ownership-recovery-" + logicalRunId + ".json");
    }

    private async Task<bool> VerifyNoOwnedDatabasesAsync(
        string artifactRoot,
        string journal,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable(
                SqlTestHarnessEnvironment.ConnectionStringVariable);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            var approval = SqlInstanceApprovalLoader.Load(_approvalFile);
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using (var reader = new SqlBenchmarkLiveOwnershipReader(connectionString))
            {
                await reader.OpenAsync(cancellationToken).ConfigureAwait(false);
                var identity = await reader.ReadIdentityAsync(cancellationToken).ConfigureAwait(false);
                if (identity is null || !string.Equals(
                        SqlInstanceFingerprint.Create(identity),
                        approval.FingerprintSha256,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name LIKE N'MyDmsVn[_]Test[_]%';";
            command.CommandTimeout = 30;
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            return count == 0;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is InvalidDataException ||
            exception is InvalidOperationException ||
            exception is SqlException ||
            exception is UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsVerifiedJournalPathFailure(
        string artifactRoot,
        string journal,
        string logicalRunId)
    {
        journal = Path.GetFullPath(journal);
        var runRoot = Path.Combine(artifactRoot, Path.GetFileName(journal));
        if (Directory.Exists(runRoot))
        {
            artifactRoot = runRoot;
        }

        if (!TestGateArtifactPathPlanner.IsEmptyOrMetadataOnlyJournal(journal, logicalRunId))
        {
            return false;
        }

        var trxFiles = Directory.GetFiles(artifactRoot, "*.trx", SearchOption.TopDirectoryOnly);
        if (trxFiles.Length != 1)
        {
            return false;
        }

        try
        {
            var document = XDocument.Load(trxFiles[0]);
            var counters = document.Descendants().SingleOrDefault(element => element.Name.LocalName == "Counters");
            var summary = document.Descendants().SingleOrDefault(element => element.Name.LocalName == "ResultSummary");
            var failed = document.Descendants().Where(element =>
                element.Name.LocalName == "UnitTestResult" &&
                string.Equals((string?)element.Attribute("outcome"), "Failed", StringComparison.Ordinal)).ToArray();
            if (counters is null || summary is null || failed.Length == 0 ||
                !string.Equals((string?)summary.Attribute("outcome"), "Failed", StringComparison.Ordinal) ||
                (string?)counters.Attribute("executed") != (string?)counters.Attribute("total") ||
                (string?)counters.Attribute("total") is not { } total ||
                (string?)counters.Attribute("passed") is not { } passed ||
                (string?)counters.Attribute("failed") is not { } failures ||
                !int.TryParse(total, out var totalCount) ||
                !int.TryParse(passed, out var passedCount) ||
                !int.TryParse(failures, out var failedCount) ||
                passedCount + failedCount != totalCount ||
                failedCount != failed.Length ||
                new[] { "error", "timeout", "aborted", "inconclusive", "notExecuted" }
                    .Any(name => (string?)counters.Attribute(name) != "0"))
            {
                return false;
            }

            return failed.All(result =>
            {
                var message = result.Descendants().SingleOrDefault(element => element.Name.LocalName == "Message")?.Value;
                var stack = result.Descendants().SingleOrDefault(element => element.Name.LocalName == "StackTrace")?.Value;
                return message is not null &&
                    message.Contains("DirectoryNotFoundException", StringComparison.Ordinal) &&
                    message.Contains(journal, StringComparison.OrdinalIgnoreCase) &&
                    stack is not null &&
                    stack.Contains("FileDatabaseOwnershipJournal.WriteAtomically", StringComparison.Ordinal) &&
                    (stack.Contains("SqlTestDatabase.CreateAsync", StringComparison.Ordinal) ||
                     stack.Contains("SqlTestDatabase.<CreateAsync>", StringComparison.Ordinal));
            });
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    private static bool IsVerifiedZeroExecution(
        string artifactRoot,
        string journal,
        string logicalRunId)
    {
        var runRoot = Path.Combine(artifactRoot, Path.GetFileName(journal));
        if (Directory.Exists(runRoot))
        {
            artifactRoot = runRoot;
        }

        if (!TestGateArtifactPathPlanner.IsEmptyOrMetadataOnlyJournal(journal, logicalRunId))
        {
            return false;
        }

        var discoveryPath = Path.Combine(artifactRoot, "discovery.txt");
        if (!File.Exists(discoveryPath) ||
            !File.ReadAllText(discoveryPath).Contains(
                "No test matches the given testcase filter",
                StringComparison.Ordinal))
        {
            return false;
        }

        var trxFiles = Directory.GetFiles(artifactRoot, "*.trx", SearchOption.TopDirectoryOnly);
        if (trxFiles.Length != 1)
        {
            return false;
        }

        try
        {
            var document = XDocument.Load(trxFiles[0]);
            var counters = document.Descendants().SingleOrDefault(element => element.Name.LocalName == "Counters");
            var summary = document.Descendants().SingleOrDefault(element => element.Name.LocalName == "ResultSummary");
            var unitTestResults = document.Descendants().Count(element => element.Name.LocalName == "UnitTestResult");
            return counters is not null &&
                summary is not null &&
                string.Equals((string?)summary.Attribute("outcome"), "Completed", StringComparison.Ordinal) &&
                unitTestResults == 0 &&
                new[] { "total", "executed", "passed", "failed", "error", "timeout", "aborted", "inconclusive", "notExecuted" }
                    .All(name => (string?)counters.Attribute(name) == "0");
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }
}
