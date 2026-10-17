using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Gates;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reporting;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Gates;

public sealed class TestGateCoordinatorTests
{
    [Fact]
    public async Task Task_gate_runs_manifest_project_in_order_and_reconciles_complete_trx()
    {
        using var repository = new GateRepository();
        var events = new List<string>();
        var processes = new GateProcessRunner(repository, events);
        var reports = new RecordingReportExporter(events);
        var coordinator = new TestGateCoordinator(processes, new StaticInventory(), reports);

        var result = await coordinator.RunAsync(repository.Options(), CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Succeeded, result.Status);
        Assert.Equal(new[] { "build", "discover", "execute", "report" }, events);
        Assert.All(processes.Commands, command => Assert.DoesNotContain(".sln", command.Arguments, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("--list-tests", processes.Commands[1].Arguments, StringComparison.Ordinal);
        Assert.Contains("--logger \"trx;LogFileName=", processes.Commands[2].Arguments, StringComparison.Ordinal);
        var runRoot = Regex.Match(
            processes.Commands[2].Arguments,
            "--results-directory \\\"([^\\\"]+)\\\"").Groups[1].Value;
        var trxName = Regex.Match(
            processes.Commands[2].Arguments,
            "LogFileName=([^\\\"]+)\\\"").Groups[1].Value;
        Assert.Equal("result.trx", trxName);
        var logicalRunId = File.ReadAllText(Path.Combine(
            runRoot, TestGateArtifactPathPlanner.LogicalRunIdFileName));
        Assert.StartsWith("task-example-net8-", logicalRunId, StringComparison.Ordinal);
        Assert.NotEqual(Path.GetFileName(runRoot), logicalRunId);
        Assert.Single(
            processes.Commands.Select(command => command.Arguments),
            arguments => arguments.Contains("--logger", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Exact_filtered_selection_is_owned_by_the_manifest()
    {
        using var repository = new GateRepository(TestGateSelectionKind.FullyQualifiedName);
        var processes = new GateProcessRunner(repository, new List<string>());

        var result = await new TestGateCoordinator(
            processes,
            new TwoMethodInventory(),
            new RecordingReportExporter(new List<string>())).RunAsync(
                repository.Options(),
                CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Succeeded, result.Status);
        Assert.All(
            processes.Commands.Where(command => command.Arguments.Contains("test ", StringComparison.Ordinal)),
            command => Assert.Contains("--filter \"FullyQualifiedName~Example.Tests.Case\"", command.Arguments));
    }

    [Fact]
    public async Task Task_fingerprint_scope_allows_unrelated_drift_in_the_same_project()
    {
        using var repository = new GateRepository(TestGateSelectionKind.FullyQualifiedName);
        var events = new List<string>();
        var processes = new GateProcessRunner(repository, events);

        var result = await new TestGateCoordinator(
            processes,
            new TwoMethodInventory(staleUnselected: true),
            new RecordingReportExporter(events)).RunAsync(
                repository.Options(), CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Succeeded, result.Status);
        Assert.Equal(new[] { "build", "discover", "execute", "report" }, events);
    }

    [Fact]
    public async Task Task_rejects_selected_fingerprint_drift_before_execution()
    {
        using var repository = new GateRepository(TestGateSelectionKind.FullyQualifiedName);
        var events = new List<string>();
        var processes = new GateProcessRunner(repository, events);

        var result = await new TestGateCoordinator(
            processes,
            new TwoMethodInventory(staleSelected: true),
            new RecordingReportExporter(events)).RunAsync(
                repository.Options(), CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Incomplete, result.Status);
        Assert.Equal(new[] { "build", "discover" }, events);
        Assert.DoesNotContain(processes.Commands,
            command => command.Arguments.Contains("--logger", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(GateFailure.EmptyDiscovery)]
    [InlineData(GateFailure.UnexpectedDiscovery)]
    [InlineData(GateFailure.PartialTrx)]
    [InlineData(GateFailure.Failed)]
    [InlineData(GateFailure.Skipped)]
    [InlineData(GateFailure.ExitCode)]
    [InlineData(GateFailure.Timeout)]
    public async Task Incomplete_discovery_execution_or_process_result_fails_closed(GateFailure failure)
    {
        using var repository = new GateRepository();
        var result = await new TestGateCoordinator(
            new GateProcessRunner(repository, new List<string>(), failure),
            new StaticInventory(),
            new RecordingReportExporter(new List<string>())).RunAsync(
                repository.Options(),
                CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Incomplete, result.Status);
    }

    [Fact]
    public async Task Task_mode_rejects_a_SQL_strict_gate_before_process_launch()
    {
        using var repository = new GateRepository(strictMode: TestGateStrictMode.SqlPrAcceptance);
        var processes = new GateProcessRunner(repository, new List<string>());

        var result = await new TestGateCoordinator(
            processes,
            new StaticInventory(),
            new RecordingReportExporter(new List<string>())).RunAsync(
                repository.Options(),
                CancellationToken.None);

        Assert.Equal(TestGateRunStatus.ResourceDenied, result.Status);
        Assert.Empty(processes.Commands);
    }

    [Fact]
    public async Task Duplicate_logical_key_across_selected_projects_fails_global_reconciliation()
    {
        using var repository = new GateRepository(duplicateProject: true);

        var result = await new TestGateCoordinator(
            new GateProcessRunner(repository, new List<string>()),
            new StaticInventory(),
            new RecordingReportExporter(new List<string>())).RunAsync(
                repository.Options(),
                CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Incomplete, result.Status);
    }

    public enum GateFailure
    {
        None,
        EmptyDiscovery,
        UnexpectedDiscovery,
        PartialTrx,
        Failed,
        Skipped,
        ExitCode,
        Timeout,
    }

    private sealed class StaticInventory : ITestGateSourceInventory
    {
        public IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath) => new[]
        {
            new TestMethodDescriptor("Example.Tests.Case", false, GateRepository.Fingerprint)
            {
                NormalizedSignature = "Example.Tests.Case()",
            },
        };
    }

    private sealed class TwoMethodInventory : ITestGateSourceInventory
    {
        private readonly bool _staleSelected;
        private readonly bool _staleUnselected;

        public TwoMethodInventory(bool staleSelected = false, bool staleUnselected = false)
        {
            _staleSelected = staleSelected;
            _staleUnselected = staleUnselected;
        }

        public IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath) => new[]
        {
            new TestMethodDescriptor("Example.Tests.Case", false,
                _staleSelected ? new string('f', 64) : GateRepository.Fingerprint)
            {
                NormalizedSignature = "Example.Tests.Case()",
            },
            new TestMethodDescriptor("Example.Tests.Other", false,
                _staleUnselected ? new string('e', 64) : GateRepository.OtherFingerprint)
            {
                NormalizedSignature = "Example.Tests.Other()",
            },
        };
    }

    private sealed class RecordingReportExporter : ITestGateReportExporter
    {
        private readonly IList<string> _events;

        public RecordingReportExporter(IList<string> events) => _events = events;

        public Task<TestGateReportExportResult> ExportAsync(
            TestGateReportEvidence evidence,
            CancellationToken cancellationToken)
        {
            _events.Add("report");
            return Task.FromResult(new TestGateReportExportResult(true, "report.json"));
        }
    }

    private sealed class GateProcessRunner : IProcessRunner
    {
        private readonly GateRepository _repository;
        private readonly IList<string> _events;
        private readonly GateFailure _failure;

        public GateProcessRunner(GateRepository repository, IList<string> events, GateFailure failure = GateFailure.None)
        {
            _repository = repository;
            _events = events;
            _failure = failure;
        }

        public List<CommandSpec> Commands { get; } = new();

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            if (command.Arguments.StartsWith("build ", StringComparison.Ordinal))
            {
                _events.Add("build");
                return Task.FromResult(Result());
            }

            if (command.Arguments.Contains("--list-tests", StringComparison.Ordinal))
            {
                _events.Add("discover");
                var output = _failure switch
                {
                    GateFailure.EmptyDiscovery => "The following Tests are available:\n",
                    GateFailure.UnexpectedDiscovery => "The following Tests are available:\n    Example.Tests.Other\n",
                    _ => "The following Tests are available:\n    Example.Tests.Case\n",
                };
                return Task.FromResult(Result(output: output));
            }

            _events.Add("execute");
            WriteTrx(command.Arguments);
            return Task.FromResult(Result(
                exitCode: _failure == GateFailure.ExitCode ? 9 : 0,
                timedOut: _failure == GateFailure.Timeout));
        }

        private void WriteTrx(string arguments)
        {
            var directory = Regex.Match(arguments, "--results-directory \\\"([^\\\"]+)\\\"").Groups[1].Value;
            var name = Regex.Match(arguments, "LogFileName=([^\\\"]+)\\\"").Groups[1].Value;
            Directory.CreateDirectory(directory);
            var outcome = _failure == GateFailure.Failed
                ? "Failed"
                : _failure == GateFailure.Skipped ? "NotExecuted" : "Passed";
            var row = _failure == GateFailure.PartialTrx
                ? string.Empty
                : $"<UnitTestResult testName=\"Example.Tests.Case\" outcome=\"{outcome}\" />";
            File.WriteAllText(
                Path.Combine(directory, name),
                "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
                row + "</Results></TestRun>");
        }

        private static CommandResult Result(int exitCode = 0, bool timedOut = false, string output = "") =>
            new(exitCode, timedOut, 1, output, string.Empty, Array.Empty<int>(), TimeSpan.FromMilliseconds(1));
    }

    private sealed class GateRepository : IDisposable
    {
        public const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        public const string OtherFingerprint = "1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        public GateRepository(
            TestGateSelectionKind selectionKind = TestGateSelectionKind.UnfilteredProject,
            TestGateStrictMode strictMode = TestGateStrictMode.DotnetTest,
            bool duplicateProject = false)
        {
            Root = Path.Combine(Path.GetTempPath(), "mydms-test-gate-" + Guid.NewGuid().ToString("N"));
            Project = Path.Combine(Root, "tests", "Example.Tests", "Example.Tests.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(Project)!);
            File.WriteAllText(Project, "<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
            if (duplicateProject)
            {
                var duplicateProjectPath = Path.Combine(Root, "tests", "Duplicate.Tests", "Duplicate.Tests.csproj");
                Directory.CreateDirectory(Path.GetDirectoryName(duplicateProjectPath)!);
                File.WriteAllText(duplicateProjectPath, "<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
            }
            Manifest = Path.Combine(Root, "tests", "test-gates.v1.json");
            File.WriteAllText(Manifest, Json(selectionKind, strictMode, duplicateProject));
        }

        public string Root { get; }
        public string Project { get; }
        public string Manifest { get; }

        public TestGateRunOptions Options() => new(
            Root,
            Manifest,
            TestGateMode.Task,
            "20261016-001",
            "TESTGATE-T03",
            Path.Combine(Root, "artifacts"),
            ApprovalFile: null,
            Revision: null);

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static string Json(
            TestGateSelectionKind selectionKind,
            TestGateStrictMode strictMode,
            bool duplicateProject)
        {
            var patterns = selectionKind == TestGateSelectionKind.UnfilteredProject
                ? "[]"
                : "[\"Example.Tests.Case\"]";
            var otherCase = selectionKind == TestGateSelectionKind.UnfilteredProject
                ? string.Empty
                : $$"""
                ,{
                  "logicalCaseId": "case-other", "ownerProject": "tests/Example.Tests/Example.Tests.csproj", "frameworks": ["net8.0"],
                  "normalizedMethodSignature": "Example.Tests.Other()", "reviewedCodeFingerprint": "{{OtherFingerprint}}",
                  "classification": ["NonSql"], "rows": [{ "rowKey": "fact", "locatorSha256": null }]
                }
                """;
            var duplicateGate = duplicateProject
                ? $$"""
                ,{
                  "gateId": "task-duplicate", "planId": "20261016-001", "taskId": "TESTGATE-T03", "mode": "Task",
                  "project": "tests/Duplicate.Tests/Duplicate.Tests.csproj", "frameworks": ["net8.0"], "module": "Duplicate", "classification": ["NonSql"],
                  "selection": { "kind": "UnfilteredProject", "patterns": [] },
                  "buildPrerequisites": [{ "project": "tests/Duplicate.Tests/Duplicate.Tests.csproj", "frameworks": ["net8.0"] }],
                  "resources": { "sql": "None", "gui": "None" }, "strictMode": "DotnetTest",
                  "commandTimeoutSeconds": 5, "overallTimeoutSeconds": 30,
                  "evidence": ["Source", "Discovery", "Trx", "SanitizedReport"]
                }
                """
                : string.Empty;
            var duplicateCase = duplicateProject
                ? $$"""
                ,{
                  "logicalCaseId": "case-duplicate-owner", "ownerProject": "tests/Duplicate.Tests/Duplicate.Tests.csproj", "frameworks": ["net8.0"],
                  "normalizedMethodSignature": "Example.Tests.Case()", "reviewedCodeFingerprint": "{{Fingerprint}}",
                  "classification": ["NonSql"], "rows": [{ "rowKey": "fact", "locatorSha256": null }]
                }
                """
                : string.Empty;
            return $$"""
            {
              "schemaVersion": 1,
              "gates": [{
                "gateId": "task-example", "planId": "20261016-001", "taskId": "TESTGATE-T03", "mode": "Task",
                "project": "tests/Example.Tests/Example.Tests.csproj", "frameworks": ["net8.0"], "module": "Example", "classification": ["NonSql"],
                "selection": { "kind": "{{selectionKind}}", "patterns": {{patterns}} },
                "buildPrerequisites": [{ "project": "tests/Example.Tests/Example.Tests.csproj", "frameworks": ["net8.0"] }],
                "resources": { "sql": "None", "gui": "None" }, "strictMode": "{{strictMode}}",
                "commandTimeoutSeconds": 5, "overallTimeoutSeconds": 30,
                "evidence": ["Source", "Discovery", "Trx", "SanitizedReport"]
              }{{duplicateGate}}],
              "logicalCases": [{
                "logicalCaseId": "case-example", "ownerProject": "tests/Example.Tests/Example.Tests.csproj", "frameworks": ["net8.0"],
                "normalizedMethodSignature": "Example.Tests.Case()", "reviewedCodeFingerprint": "{{Fingerprint}}",
                "classification": ["NonSql"], "rows": [{ "rowKey": "fact", "locatorSha256": null }]
              }{{otherCase}}{{duplicateCase}}],
              "identityMappings": [], "exemptions": []
            }
            """;
        }
    }
}
