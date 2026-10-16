using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Focused;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reconciliation;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Focused;

public sealed class FocusedRunCoordinatorTests
{
    [Fact]
    public async Task Stale_classification_blocks_before_discovery_or_execution()
    {
        using var fixture = new Fixture();
        var process = new RecordingProcessRunner(_ => Success());
        var ownership = new RecordingOwnershipGate();
        var coordinator = new FocusedRunCoordinator(
            new StubPreflightGate(),
            process,
            new StubClassificationGate(false),
            new StubResultGate(true),
            ownership);

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.False(result.RecoveryRequired);
        Assert.Empty(process.Commands);
        Assert.Empty(ownership.Evidence);
    }

    [Fact]
    public async Task Timed_out_execution_still_uses_bounded_ownership_reconciliation()
    {
        using var fixture = new Fixture();
        var process = new RecordingProcessRunner(command =>
            command.Arguments.Contains("--list-tests", StringComparison.Ordinal)
                ? Success("The following Tests are available:\n    Example.Tests.Fast\n")
                : new CommandResult(
                    -1,
                    true,
                    42,
                    string.Empty,
                    "timed out",
                    new[] { 42 },
                    TimeSpan.FromSeconds(1)));
        var ownership = new RecordingOwnershipGate();
        var coordinator = new FocusedRunCoordinator(
            new StubPreflightGate(),
            process,
            new StubClassificationGate(true),
            new StubResultGate(true),
            ownership);

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.False(result.RecoveryRequired);
        Assert.Equal(2, process.Commands.Count);
        Assert.All(process.Commands, command => Assert.Equal(TimeSpan.FromSeconds(5), command.Timeout));
        var evidence = Assert.Single(ownership.Evidence);
        Assert.False(evidence.ProcessSucceeded);
        Assert.False(ownership.ReceivedCancelledToken);
    }

    [Fact]
    public async Task Failed_result_gate_after_execution_cannot_skip_ownership_reconciliation()
    {
        using var fixture = new Fixture();
        var process = new RecordingProcessRunner(command =>
            command.Arguments.Contains("--list-tests", StringComparison.Ordinal)
                ? Success("The following Tests are available:\n    Example.Tests.Fast\n")
                : Success());
        var ownership = new RecordingOwnershipGate();
        var coordinator = new FocusedRunCoordinator(
            new StubPreflightGate(),
            process,
            new StubClassificationGate(true),
            new StubResultGate(false),
            ownership);

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.False(result.IsComplete);
        var evidence = Assert.Single(ownership.Evidence);
        Assert.True(evidence.ProcessSucceeded);
    }

    private static CommandResult Success(string output = "") => new CommandResult(
        0,
        false,
        1,
        output,
        string.Empty,
        Array.Empty<int>(),
        TimeSpan.FromMilliseconds(10));

    private sealed class StubPreflightGate : IBenchmarkPreflightGate
    {
        public Task<bool> ValidateAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class StubClassificationGate : IBenchmarkClassificationGate
    {
        private readonly bool _valid;

        public StubClassificationGate(bool valid) => _valid = valid;

        public bool IsValid(string assemblyPath, string repositoryRoot) => _valid;
    }

    private sealed class StubResultGate : IFocusedResultGate
    {
        private readonly bool _complete;

        public StubResultGate(bool complete) => _complete = complete;

        public FocusedSqlTestResult Verify(FocusedResultEvidence evidence) => new FocusedSqlTestResult(
            _complete ? Array.Empty<FocusedSqlTestFailure>() : new[] { FocusedSqlTestFailure.ExecutionMismatch },
            1,
            1,
            _complete ? 1 : 0,
            0);
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        private readonly Func<CommandSpec, CommandResult> _result;

        public RecordingProcessRunner(Func<CommandSpec, CommandResult> result) => _result = result;

        public List<CommandSpec> Commands { get; } = new List<CommandSpec>();

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(_result(command));
        }
    }

    private sealed class RecordingOwnershipGate : IBenchmarkOwnershipGate
    {
        public List<BenchmarkOwnershipEvidence> Evidence { get; } = new List<BenchmarkOwnershipEvidence>();

        public bool ReceivedCancelledToken { get; private set; }

        public Task<BenchmarkOwnershipResult> ValidateAsync(
            BenchmarkOwnershipEvidence evidence,
            CancellationToken cancellationToken)
        {
            Evidence.Add(evidence);
            ReceivedCancelledToken |= cancellationToken.IsCancellationRequested;
            return Task.FromResult(new BenchmarkOwnershipResult(true, evidence.RecoveryArtifactPath));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "MyDmsVn.FocusedCoordinator",
            Guid.NewGuid().ToString("N"));

        public Fixture()
        {
            Directory.CreateDirectory(_root);
            Options = new FocusedRunOptions(
                _root,
                Path.Combine(_root, "tests.csproj"),
                Path.Combine(_root, "artifacts"),
                Path.Combine(_root, "approval.json"),
                "FastSQL",
                new[] { "net48" },
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMinutes(1));
        }

        public FocusedRunOptions Options { get; }

        public void Dispose() => Directory.Delete(_root, true);
    }
}
