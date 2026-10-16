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
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reconciliation;
using MyDmsVn.SqlTestRunner.Reporting;
using MyDmsVn.SqlTestRunner.Trx;

namespace MyDmsVn.SqlTestRunner.Gates;

public enum TestGateRunStatus
{
    Succeeded,
    ResourceDenied,
    Incomplete,
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

public sealed class TestGateCoordinator
{
    private readonly IProcessRunner _processes;
    private readonly ITestGateSourceInventory _sourceInventory;
    private readonly ITestGateReportExporter _reports;

    public TestGateCoordinator(
        IProcessRunner processes,
        ITestGateSourceInventory sourceInventory,
        ITestGateReportExporter reports)
    {
        _processes = processes;
        _sourceInventory = sourceInventory;
        _reports = reports;
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

        var overallSeconds = selection.Gates.Min(gate => gate.OverallTimeoutSeconds);
        using var overall = new CancellationTokenSource(TimeSpan.FromSeconds(overallSeconds));
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
            foreach (var gate in selection.Gates)
            {
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
                    var source = _sourceInventory.Read(AssemblyPath(project, tfm));
                    var discovered = VstestDiscoveryParser.Parse(discovery.StandardOutput, tfm);
                    var trx = TrxResultParser.TryParse(Path.Combine(runRoot, trxName), tfm);
                    if (!trx.IsComplete)
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    var sourceBinding = LogicalCaseBinder.BindSource(source, definitions, gate.Project, tfm);
                    var discoveryBinding = LogicalCaseBinder.BindDiscovery(discovered, definitions, gate.Project, tfm);
                    var executionBinding = LogicalCaseBinder.BindExecution(trx.Results, definitions, gate.Project, tfm);
                    if (!sourceBinding.IsComplete || !discoveryBinding.IsComplete || !executionBinding.IsComplete)
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    var expected = sourceBinding.Cases.Select(item => new ExpectedLogicalCase(
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
                        sourceBinding.Cases,
                        discoveryBinding.Cases,
                        executionBinding.Cases,
                        manifest.IdentityMappings,
                        manifest.Exemptions);
                    if (!reconciliation.IsComplete)
                    {
                        return new TestGateRunResult(TestGateRunStatus.Incomplete, runs);
                    }

                    stopwatch.Stop();
                    var framework = new TestGateFrameworkResult(
                        gate.GateId,
                        tfm,
                        sourceBinding.Cases.Count,
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
