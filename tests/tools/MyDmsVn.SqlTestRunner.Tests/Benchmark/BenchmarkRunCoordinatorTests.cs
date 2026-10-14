using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reporting;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Benchmark;

public sealed class BenchmarkRunCoordinatorTests
{
    [Fact]
    public async Task Blocked_preflight_never_launches_a_test_process()
    {
        var process = new RecordingProcessRunner();
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(false),
            process,
            new StubResultGate(),
            new StubOwnershipGate(),
            new StubReportExporter());

        var result = await coordinator.RunAsync(Options(), CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Empty(process.Commands);
    }

    [Fact]
    public async Task Overall_deadline_cancels_a_blocked_preflight_and_returns_incomplete()
    {
        var process = new RecordingProcessRunner();
        var coordinator = new BenchmarkRunCoordinator(
            new BlockingPreflightGate(),
            process,
            new StubResultGate(),
            new StubOwnershipGate(),
            new StubReportExporter());
        var options = Options() with { OverallTimeout = TimeSpan.FromMilliseconds(50) };

        var result = await coordinator.RunAsync(options, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Empty(process.Commands);
    }

    [Fact]
    public async Task Baseline_runs_one_warmup_and_three_measured_passes_in_alternating_tfm_order()
    {
        using var fixture = new CoordinatorFixture();
        var process = new RecordingProcessRunner();
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            process,
            new StubResultGate(),
            new StubOwnershipGate(),
            new StubReportExporter());

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.True(result.IsComplete);
        Assert.Equal(16, process.Commands.Count);
        Assert.NotEqual(process.Commands[0].Arguments, process.Commands[1].Arguments);
        Assert.Equal(8, process.Commands.FindAll(command => command.Arguments.Contains("--list-tests")).Count);
        var executions = process.Commands.FindAll(command => !command.Arguments.Contains("--list-tests"));
        Assert.Collection(
            executions,
            command => Assert.Contains("-f net48", command.Arguments),
            command => Assert.Contains("-f net8.0", command.Arguments),
            command => Assert.Contains("-f net8.0", command.Arguments),
            command => Assert.Contains("-f net48", command.Arguments),
            command => Assert.Contains("-f net48", command.Arguments),
            command => Assert.Contains("-f net8.0", command.Arguments),
            command => Assert.Contains("-f net8.0", command.Arguments),
            command => Assert.Contains("-f net48", command.Arguments));
        Assert.Equal(8, executions.ConvertAll(command => command.Environment["MYDMSVN_SQL_TEST_RUN_ID"]).Distinct().Count());
        Assert.Equal(8, executions.ConvertAll(command => command.Environment["MYDMSVN_SQL_TEST_TIMING_OUTPUT"]).Distinct().Count());
        Assert.Equal(2, result.Runs.Count(run => run.Kind == BenchmarkPassKind.Warmup));
        Assert.Equal(6, result.Runs.Count(run => run.Kind == BenchmarkPassKind.Measured));
        Assert.Equal(8, result.Runs.Select(run => run.RunId).Distinct().Count());
        Assert.All(result.Runs, run =>
        {
            Assert.True(Path.IsPathRooted(run.TrxPath));
            Assert.True(Path.IsPathRooted(run.TimingPath));
        });
        Assert.All(executions, command =>
        {
            Assert.Contains("--no-build", command.Arguments);
            Assert.Contains("--logger", command.Arguments);
            Assert.True(command.Environment.ContainsKey("MYDMSVN_SQL_TEST_APPROVAL_FILE"));
            Assert.True(command.Environment.ContainsKey("MYDMSVN_SQL_TEST_RUN_ID"));
        });
    }

    [Fact]
    public async Task Passed_process_with_unresolved_ownership_is_incomplete()
    {
        using var fixture = new CoordinatorFixture();
        var process = new RecordingProcessRunner();
        var ownership = new StubOwnershipGate(isComplete: false, writeRecoveryArtifact: true);
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            process,
            new StubResultGate(),
            ownership,
            new StubReportExporter());

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.False(result.IsComplete);
        var evidence = Assert.Single(ownership.Evidence);
        Assert.True(evidence.ProcessSucceeded);
        Assert.True(File.Exists(evidence.RecoveryArtifactPath));
    }

    [Fact]
    public async Task Timed_out_process_still_reconciles_ownership()
    {
        using var fixture = new CoordinatorFixture();
        var process = new RecordingProcessRunner(command =>
            command.Arguments.Contains("--list-tests")
                ? Success(command)
                : new CommandResult(-1, true, 1, string.Empty, "timed out", Array.Empty<int>(), TimeSpan.FromSeconds(1)));
        var ownership = new StubOwnershipGate(isComplete: false, writeRecoveryArtifact: true);
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            process,
            new StubResultGate(),
            ownership,
            new StubReportExporter());

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.False(result.IsComplete);
        var evidence = Assert.Single(ownership.Evidence);
        Assert.False(evidence.ProcessSucceeded);
        Assert.True(File.Exists(evidence.RecoveryArtifactPath));
    }

    [Fact]
    public async Task Overall_deadline_after_child_launch_uses_bounded_ownership_reconciliation()
    {
        using var fixture = new CoordinatorFixture();
        var process = new DeadlineProcessRunner();
        var ownership = new StubOwnershipGate(isComplete: false, writeRecoveryArtifact: true);
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            process,
            new StubResultGate(),
            ownership,
            new StubReportExporter());

        var result = await coordinator.RunAsync(
            fixture.Options with { OverallTimeout = TimeSpan.FromMilliseconds(50) },
            CancellationToken.None);

        Assert.False(result.IsComplete);
        var evidence = Assert.Single(ownership.Evidence);
        Assert.False(ownership.ReceivedCancelledToken);
        Assert.True(File.Exists(evidence.RecoveryArtifactPath));
    }

    [Fact]
    public async Task Every_execution_receives_the_isolated_journal_directory()
    {
        using var fixture = new CoordinatorFixture();
        var process = new RecordingProcessRunner();
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            process,
            new StubResultGate(),
            new StubOwnershipGate(),
            new StubReportExporter());

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.True(result.IsComplete);
        var executions = process.Commands.FindAll(command => !command.Arguments.Contains("--list-tests"));
        var journalDirectories = executions.ConvertAll(command =>
            command.Environment["MYDMSVN_SQL_TEST_JOURNAL_DIRECTORY"]);
        Assert.Equal(8, journalDirectories.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(journalDirectories, path =>
            Assert.StartsWith(fixture.ArtifactRoot, path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Required_report_export_failure_is_incomplete()
    {
        using var fixture = new CoordinatorFixture();
        var exporter = new StubReportExporter(isComplete: false);
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            new RecordingProcessRunner(),
            new StubResultGate(),
            new StubOwnershipGate(),
            exporter);

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.False(result.IsComplete);
        var evidence = Assert.Single(exporter.Evidence);
        Assert.Equal(2, evidence.Runs.Count(run => run.Kind == BenchmarkPassKind.Warmup));
        Assert.Equal(6, evidence.Runs.Count(run => run.Kind == BenchmarkPassKind.Measured));
    }

    [Fact]
    public async Task Missing_required_report_output_is_incomplete()
    {
        using var fixture = new CoordinatorFixture();
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            new RecordingProcessRunner(),
            new StubResultGate(),
            new StubOwnershipGate(),
            new StubReportExporter(returnMissingPaths: true));

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.False(result.IsComplete);
    }

    [Fact]
    public void Trx_gate_rejects_a_passed_run_missing_one_discovered_case()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MyDmsVn.InventoryGate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var trxPath = Path.Combine(directory, "missing.trx");
            File.WriteAllText(
                trxPath,
                "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
                "<UnitTestResult testId=\"a\" testName=\"Example.Tests.First\" outcome=\"Passed\" duration=\"00:00:00.0100000\" />" +
                "</Results></TestRun>");
            var discovery = "The following Tests are available:\n" +
                "    Example.Tests.First\n" +
                "    Example.Tests.Second\n";
            var gate = new TrxBenchmarkResultGate(
                new StaticSourceInventory(
                    new TestMethodDescriptor("Example.Tests.First", false, "a"),
                    new TestMethodDescriptor("Example.Tests.Second", false, "b")),
                new StubMigrationManifestGate());

            var valid = gate.IsValid(
                new BenchmarkResultEvidence("assembly.dll", discovery, trxPath, "timing.jsonl", "net8.0", 0));

            Assert.False(valid);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Trx_gate_requires_exact_migration_timing_coverage()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MyDmsVn.MigrationGate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var trxPath = Path.Combine(directory, "complete.trx");
            var timingPath = Path.Combine(directory, "timing.jsonl");
            var manifestPath = Path.Combine(directory, "migration-manifest.json");
            File.WriteAllText(
                trxPath,
                "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
                "<UnitTestResult testId=\"a\" testName=\"Example.Tests.First\" outcome=\"Passed\" duration=\"00:00:00.0100000\" />" +
                "</Results></TestRun>");
            File.WriteAllLines(
                timingPath,
                new[]
                {
                    "{\"recordType\":\"migration-invocation\",\"testCaseId\":\"First\",\"tfm\":\"net8.0\",\"invocationId\":\"Example.Tests.First.migrate\"}",
                    "{\"stage\":\"migration\",\"testCaseId\":\"First\",\"tfm\":\"net8.0\",\"operationId\":\"Example.Tests.First.migrate\"}",
                });
            File.WriteAllText(
                manifestPath,
                "[{\"invocationId\":\"Example.Tests.First.migrate\",\"class\":\"Tests\"," +
                "\"method\":\"First\",\"sourceFile\":\"ExampleTests.cs\",\"tfms\":[\"net8.0\"]," +
                "\"expectedInvocationRule\":\"once-per-test-execution\"," +
                "\"expectedInvocations\":[{\"testMethod\":\"First\",\"count\":1}]}]");
            var gate = new TrxBenchmarkResultGate(
                new StaticSourceInventory(new TestMethodDescriptor("Example.Tests.First", false, "a")),
                new StubMigrationManifestGate());
            var evidence = new BenchmarkResultEvidence(
                "assembly.dll",
                "The following Tests are available:\n    Example.Tests.First\n",
                trxPath,
                timingPath,
                "net8.0",
                0,
                directory,
                manifestPath);

            Assert.True(gate.IsValid(evidence));
            File.AppendAllText(
                timingPath,
                "{\"stage\":\"migration\",\"testCaseId\":\"First\",\"tfm\":\"net8.0\",\"operationId\":\"Example.Tests.First.migrate\"}" + Environment.NewLine);
            Assert.False(gate.IsValid(evidence));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Trx_gate_rejects_duplicate_discovery_for_a_fact()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MyDmsVn.DuplicateFactGate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var trxPath = Path.Combine(directory, "duplicate.trx");
            File.WriteAllText(
                trxPath,
                "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
                "<UnitTestResult testName=\"Example.Tests.First\" outcome=\"Passed\" />" +
                "<UnitTestResult testName=\"Example.Tests.First\" outcome=\"Passed\" />" +
                "</Results></TestRun>");
            var gate = new TrxBenchmarkResultGate(
                new StaticSourceInventory(new TestMethodDescriptor("Example.Tests.First", false, "a")),
                new StubMigrationManifestGate());

            var valid = gate.IsValid(
                new BenchmarkResultEvidence(
                    "assembly.dll",
                    "The following Tests are available:\n    Example.Tests.First\n    Example.Tests.First\n",
                    trxPath,
                    "missing-timing.jsonl",
                    "net8.0",
                    0));

            Assert.False(valid);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static BenchmarkRunOptions Options() => new BenchmarkRunOptions(
        "repo",
        "tests.csproj",
        "artifacts",
        "approval.json",
        new[] { "net48", "net8.0" },
        TimeSpan.FromMinutes(1));

    private sealed class StubPreflightGate : IBenchmarkPreflightGate
    {
        private readonly bool _allowed;

        public StubPreflightGate(bool allowed) => _allowed = allowed;

        public Task<bool> ValidateAsync(CancellationToken cancellationToken) => Task.FromResult(_allowed);
    }

    private sealed class BlockingPreflightGate : IBenchmarkPreflightGate
    {
        public async Task<bool> ValidateAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return true;
        }
    }

    private static CommandResult Success(CommandSpec command)
    {
        var output = command.Arguments.Contains("--list-tests")
            ? "The following Tests are available:\n    Example.Tests.First\n"
            : string.Empty;
        return new CommandResult(0, false, 1, output, string.Empty, Array.Empty<int>(), TimeSpan.FromSeconds(1));
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        private readonly Func<CommandSpec, CommandResult> _result;

        public RecordingProcessRunner(Func<CommandSpec, CommandResult>? result = null) =>
            _result = result ?? Success;

        public List<CommandSpec> Commands { get; } = new List<CommandSpec>();

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(_result(command));
        }
    }

    private sealed class DeadlineProcessRunner : IProcessRunner
    {
        private int _callCount;

        public async Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        {
            _callCount++;
            if (_callCount == 1)
            {
                return Success(command);
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }

    private sealed class StubOwnershipGate : IBenchmarkOwnershipGate
    {
        private readonly bool _isComplete;
        private readonly bool _writeRecoveryArtifact;

        public StubOwnershipGate(bool isComplete = true, bool writeRecoveryArtifact = false)
        {
            _isComplete = isComplete;
            _writeRecoveryArtifact = writeRecoveryArtifact;
        }

        public List<BenchmarkOwnershipEvidence> Evidence { get; } =
            new List<BenchmarkOwnershipEvidence>();

        public bool ReceivedCancelledToken { get; private set; }

        public Task<BenchmarkOwnershipResult> ValidateAsync(
            BenchmarkOwnershipEvidence evidence,
            CancellationToken cancellationToken)
        {
            Evidence.Add(evidence);
            ReceivedCancelledToken |= cancellationToken.IsCancellationRequested;
            if (_writeRecoveryArtifact)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(evidence.RecoveryArtifactPath)!);
                File.WriteAllText(evidence.RecoveryArtifactPath, "{}");
            }

            return Task.FromResult(new BenchmarkOwnershipResult(
                _isComplete,
                _writeRecoveryArtifact ? evidence.RecoveryArtifactPath : null));
        }
    }

    private sealed class StubReportExporter : IBenchmarkReportExporter
    {
        private readonly bool _isComplete;
        private readonly bool _returnMissingPaths;

        public StubReportExporter(bool isComplete = true, bool returnMissingPaths = false)
        {
            _isComplete = isComplete;
            _returnMissingPaths = returnMissingPaths;
        }

        public List<BenchmarkReportEvidence> Evidence { get; } =
            new List<BenchmarkReportEvidence>();

        public Task<BenchmarkReportExportResult> ExportAsync(
            BenchmarkReportEvidence evidence,
            CancellationToken cancellationToken)
        {
            Evidence.Add(evidence);
            if (!_isComplete)
            {
                return Task.FromResult(BenchmarkReportExportResult.Incomplete());
            }

            var paths = evidence.TargetFrameworks.SelectMany(tfm => new[]
            {
                Path.Combine(evidence.ArtifactRoot, "sqlperf-baseline-" + tfm + ".json"),
                Path.Combine(evidence.ArtifactRoot, "sqlperf-baseline-" + tfm + ".md"),
            }).ToArray();
            if (_returnMissingPaths)
            {
                return Task.FromResult(new BenchmarkReportExportResult(true, paths));
            }

            Directory.CreateDirectory(evidence.ArtifactRoot);
            foreach (var path in paths)
            {
                File.WriteAllText(path, string.Empty);
            }

            return Task.FromResult(new BenchmarkReportExportResult(
                true,
                paths));
        }
    }

    private sealed class CoordinatorFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "MyDmsVn.BenchmarkCoordinator",
            Guid.NewGuid().ToString("N"));

        public CoordinatorFixture()
        {
            ArtifactRoot = Path.Combine(_root, "artifacts");
            Directory.CreateDirectory(ArtifactRoot);
            Options = BenchmarkRunCoordinatorTests.Options() with
            {
                RepositoryRoot = _root,
                ArtifactRoot = ArtifactRoot,
                ApprovalFile = Path.Combine(_root, "approval.json"),
            };
        }

        public string ArtifactRoot { get; }

        public BenchmarkRunOptions Options { get; }

        public void Dispose() => Directory.Delete(_root, true);
    }

    private sealed class StubResultGate : IBenchmarkResultGate
    {
        public bool IsValid(BenchmarkResultEvidence evidence) => true;
    }

    private sealed class StaticSourceInventory : IBenchmarkSourceInventory
    {
        private readonly IReadOnlyList<TestMethodDescriptor> _methods;

        public StaticSourceInventory(params TestMethodDescriptor[] methods) => _methods = methods;

        public IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath) => _methods;
    }

    private sealed class StubMigrationManifestGate : IBenchmarkMigrationManifestGate
    {
        public bool IsValid(string repositoryRoot, string manifestPath, string tfm) => true;
    }
}
