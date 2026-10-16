using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Focused;
using MyDmsVn.SqlTestRunner.Gates;
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
    public async Task Preflight_exception_before_execution_returns_incomplete_without_recovery()
    {
        using var fixture = new Fixture();
        var coordinator = new FocusedRunCoordinator(
            new ThrowingPreflightGate(),
            new RecordingProcessRunner(_ => Success()),
            new StubClassificationGate(true));

        var result = await coordinator.RunAsync(fixture.Options, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.False(result.RecoveryRequired);
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

    [Fact]
    public async Task Failed_focused_run_is_recovered_from_the_shared_ownership_layout_on_next_acquisition()
    {
        using var fixture = new Fixture();
        var leaseStore = new FileSqlExecutionLeaseStore(Path.Combine(fixture.Root, "lease-store"));
        var leaseFactory = new SqlExecutionLeaseFactory(leaseStore);
        const string fingerprint = "focused-recovery-layout";
        var firstRequest = new SqlExecutionLeaseRequest(
            fingerprint,
            "20261016-001/TESTGATE-T03/Task",
            fixture.Options.ArtifactRoot,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            new SuccessfulRecovery());
        var first = await leaseFactory.AcquireAsync(firstRequest, CancellationToken.None);
        var process = new RecordingProcessRunner(command =>
            command.Arguments.Contains("--list-tests", StringComparison.Ordinal)
                ? Success("The following Tests are available:\n    Example.Tests.Fast\n")
                : Success());
        var failedOwnership = new RecordingOwnershipGate(isComplete: false);
        var focused = new FocusedRunCoordinator(
            new StubPreflightGate(),
            process,
            new StubClassificationGate(true),
            new StubResultGate(true),
            failedOwnership);

        var focusedResult = await focused.RunAsync(fixture.Options, CancellationToken.None);
        await first.Lease!.DisposeAsync();
        var recoveryOwnership = new RecordingOwnershipGate(isComplete: true);
        var second = await leaseFactory.AcquireAsync(
            firstRequest with
            {
                Recovery = new BenchmarkAbandonedSqlRunRecovery(
                    fixture.Options.ApprovalFile,
                    recoveryOwnership),
            },
            CancellationToken.None);

        Assert.False(focusedResult.IsComplete);
        Assert.True(focusedResult.RecoveryRequired);
        Assert.Equal(SqlExecutionLeaseAcquireStatus.Recovered, second.Status);
        var evidence = Assert.Single(recoveryOwnership.Evidence);
        Assert.Equal(
            Path.Combine(fixture.Options.ArtifactRoot, "ownership", evidence.ExecutionId),
            evidence.JournalDirectory);
        Assert.Equal(
            Path.Combine(fixture.Options.ArtifactRoot, "ownership-recovery-" + evidence.ExecutionId + ".json"),
            evidence.RecoveryArtifactPath);
        second.Lease!.Complete();
        await second.Lease.DisposeAsync();
    }

    [Fact]
    public async Task Recovery_accepts_an_empty_journal_only_when_discovery_and_trx_prove_zero_execution()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "mdv-zero-execution-" + Guid.NewGuid().ToString("N"));
        var runId = "focused-fastsql-net48-zero";
        var runRoot = Path.Combine(artifactRoot, runId);
        var journal = Path.Combine(runRoot, "ownership", runId);
        Directory.CreateDirectory(journal);
        File.WriteAllText(
            Path.Combine(runRoot, "discovery.txt"),
            "No test matches the given testcase filter `Category=FastSQL`.\r\n");
        File.WriteAllText(
            Path.Combine(runRoot, runId + ".trx"),
            "<TestRun><ResultSummary outcome=\"Completed\"><Counters total=\"0\" executed=\"0\" passed=\"0\" failed=\"0\" error=\"0\" timeout=\"0\" aborted=\"0\" inconclusive=\"0\" notExecuted=\"0\" /></ResultSummary></TestRun>");
        var ownership = new RecordingOwnershipGate(isComplete: false);
        var recovery = new BenchmarkAbandonedSqlRunRecovery("unused-approval", ownership);
        var abandoned = new AbandonedSqlRun(
            "resource",
            123,
            "invocation",
            DateTimeOffset.UtcNow,
            artifactRoot,
            "legacy/focused-run/FastSQL");

        try
        {
            Assert.True(await recovery.ReconcileAsync(abandoned, CancellationToken.None));
            Assert.Empty(ownership.Evidence);
        }
        finally
        {
            Directory.Delete(artifactRoot, recursive: true);
        }
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

    private sealed class ThrowingPreflightGate : IBenchmarkPreflightGate
    {
        public Task<bool> ValidateAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The SQL instance approval has expired.");
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
        private readonly bool _isComplete;

        public RecordingOwnershipGate(bool isComplete = true) => _isComplete = isComplete;

        public List<BenchmarkOwnershipEvidence> Evidence { get; } = new List<BenchmarkOwnershipEvidence>();

        public bool ReceivedCancelledToken { get; private set; }

        public Task<BenchmarkOwnershipResult> ValidateAsync(
            BenchmarkOwnershipEvidence evidence,
            CancellationToken cancellationToken)
        {
            Evidence.Add(evidence);
            ReceivedCancelledToken |= cancellationToken.IsCancellationRequested;
            return Task.FromResult(new BenchmarkOwnershipResult(_isComplete, evidence.RecoveryArtifactPath));
        }
    }

    private sealed class SuccessfulRecovery : IAbandonedSqlRunRecovery
    {
        public Task<bool> ReconcileAsync(AbandonedSqlRun lease, CancellationToken cancellationToken) =>
            Task.FromResult(true);
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

        public string Root => _root;

        public FocusedRunOptions Options { get; }

        public void Dispose() => Directory.Delete(_root, true);
    }
}
