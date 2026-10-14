using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;
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
            new StubResultGate());

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
            new StubResultGate());
        var options = Options() with { OverallTimeout = TimeSpan.FromMilliseconds(50) };

        var result = await coordinator.RunAsync(options, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Empty(process.Commands);
    }

    [Fact]
    public async Task Baseline_runs_one_warmup_and_three_measured_passes_in_alternating_tfm_order()
    {
        var process = new RecordingProcessRunner();
        var coordinator = new BenchmarkRunCoordinator(
            new StubPreflightGate(true),
            process,
            new StubResultGate());

        var result = await coordinator.RunAsync(Options(), CancellationToken.None);

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
        Assert.All(executions, command =>
        {
            Assert.Contains("--no-build", command.Arguments);
            Assert.Contains("--logger", command.Arguments);
            Assert.True(command.Environment.ContainsKey("MYDMSVN_SQL_TEST_APPROVAL_FILE"));
            Assert.True(command.Environment.ContainsKey("MYDMSVN_SQL_TEST_RUN_ID"));
        });
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

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<CommandSpec> Commands { get; } = new List<CommandSpec>();

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            var output = command.Arguments.Contains("--list-tests")
                ? "The following Tests are available:\n    Example.Tests.First\n"
                : string.Empty;
            return Task.FromResult(new CommandResult(0, false, 1, output, string.Empty, Array.Empty<int>()));
        }
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
