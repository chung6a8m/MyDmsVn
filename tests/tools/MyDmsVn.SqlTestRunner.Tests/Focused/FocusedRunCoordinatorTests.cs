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
        Assert.Equal(failedOwnership.Evidence[0].ExecutionId, evidence.ExecutionId);
        Assert.Equal(
            Path.Combine(fixture.Options.ArtifactRoot, "ownership", Path.GetFileName(evidence.JournalDirectory)),
            evidence.JournalDirectory);
        Assert.Equal(
            Path.Combine(fixture.Options.ArtifactRoot, "recovery-" + Path.GetFileName(evidence.JournalDirectory) + ".json"),
            evidence.RecoveryArtifactPath);
        Assert.Equal(
            evidence.ExecutionId,
            File.ReadAllText(Path.Combine(evidence.JournalDirectory, "execution-id.txt")));
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

    [Fact]
    public async Task Recovery_accepts_new_layout_metadata_journal_when_discovery_and_trx_prove_zero_execution()
    {
        foreach (var hasIncompleteWrite in new[] { false, true })
        {
            var artifactRoot = Path.Combine(
                Path.GetTempPath(), "mdv-zero-execution-new-" + Guid.NewGuid().ToString("N"));
            var runId = "focused-fastsql-net48-zero";
            var physicalId = "r-0123456789abcdef";
            var runRoot = Path.Combine(artifactRoot, physicalId);
            var journal = Path.Combine(runRoot, "ownership", physicalId);
            Directory.CreateDirectory(journal);
            TestGateArtifactPathPlanner.WriteLogicalRunId(journal, runId);
            if (hasIncompleteWrite)
            {
                File.WriteAllText(Path.Combine(journal, "execution-id.txt.tmp-incomplete"), runId);
            }
            File.WriteAllText(
                Path.Combine(runRoot, "discovery.txt"),
                "No test matches the given testcase filter `Category=FastSQL`.\r\n");
            File.WriteAllText(
                Path.Combine(runRoot, "t-" + physicalId + ".trx"),
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
                Assert.Equal(!hasIncompleteWrite, await recovery.ReconcileAsync(abandoned, CancellationToken.None));
                Assert.Equal(hasIncompleteWrite ? 1 : 0, ownership.Evidence.Count);
            }
            finally
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Recovery_requires_a_positive_database_absence_proof_for_journal_path_failures(
        bool noOwnedDatabases)
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "mdv-journal-path-recovery-" + Guid.NewGuid().ToString("N"));
        var runId = "strict-final-net48-journal-path";
        var runRoot = Path.Combine(artifactRoot, runId);
        var journal = Path.Combine(artifactRoot, "ownership", runId);
        Directory.CreateDirectory(runRoot);
        Directory.CreateDirectory(journal);
        File.WriteAllText(
            Path.Combine(runRoot, "strict-final.trx"),
            "<TestRun><ResultSummary outcome=\"Failed\"><Counters total=\"23\" executed=\"23\" passed=\"22\" failed=\"1\" error=\"0\" timeout=\"0\" aborted=\"0\" inconclusive=\"0\" notExecuted=\"0\" /></ResultSummary><Results><UnitTestResult outcome=\"Failed\"><Output><ErrorInfo><Message>System.IO.DirectoryNotFoundException: " + journal + "</Message><StackTrace>FileDatabaseOwnershipJournal.WriteAtomically SqlTestDatabase.&lt;CreateAsync&gt;d__23.MoveNext</StackTrace></ErrorInfo></Output></UnitTestResult></Results></TestRun>");

        var ownership = new RecordingOwnershipGate(isComplete: false);
        var recovery = new BenchmarkAbandonedSqlRunRecovery(
            "unused-approval",
            ownership,
            (_, _, _) => Task.FromResult(noOwnedDatabases));
        var abandoned = new AbandonedSqlRun(
            "resource",
            123,
            "invocation",
            DateTimeOffset.UtcNow,
            artifactRoot,
            "strict-final");

        try
        {
            Assert.Equal(noOwnedDatabases, await recovery.ReconcileAsync(abandoned, CancellationToken.None));
            if (noOwnedDatabases)
            {
                Assert.Empty(ownership.Evidence);
            }
            else
            {
                Assert.Single(ownership.Evidence);
            }
        }
        finally
        {
            Directory.Delete(artifactRoot, recursive: true);
        }
    }

    [Fact]
    public async Task New_layout_journal_path_failure_requires_positive_database_absence_proof()
    {
        var scenarios = new[]
        {
            (NoOwnedDatabases: true, HasIncompleteWrite: false),
            (NoOwnedDatabases: false, HasIncompleteWrite: false),
            (NoOwnedDatabases: true, HasIncompleteWrite: true),
            (NoOwnedDatabases: false, HasIncompleteWrite: true),
        };
        foreach (var scenario in scenarios)
        {
            var artifactRoot = Path.Combine(
                Path.GetTempPath(), "mdv-journal-path-new-" + Guid.NewGuid().ToString("N"));
            var runId = "strict-final-net48-journal-path";
            var physicalId = "r-0123456789abcdef";
            var journal = Path.Combine(artifactRoot, "ownership", physicalId);
            Directory.CreateDirectory(journal);
            TestGateArtifactPathPlanner.WriteLogicalRunId(journal, runId);
            if (scenario.HasIncompleteWrite)
            {
                File.WriteAllText(Path.Combine(journal, "execution-id.txt.tmp-incomplete"), runId);
            }
            File.WriteAllText(
                Path.Combine(artifactRoot, "t-" + physicalId + ".trx"),
                "<TestRun><ResultSummary outcome=\"Failed\"><Counters total=\"23\" executed=\"23\" passed=\"22\" failed=\"1\" error=\"0\" timeout=\"0\" aborted=\"0\" inconclusive=\"0\" notExecuted=\"0\" /></ResultSummary><Results><UnitTestResult outcome=\"Failed\"><Output><ErrorInfo><Message>System.IO.DirectoryNotFoundException: " + journal + "</Message><StackTrace>FileDatabaseOwnershipJournal.WriteAtomically SqlTestDatabase.&lt;CreateAsync&gt;d__23.MoveNext</StackTrace></ErrorInfo></Output></UnitTestResult></Results></TestRun>");

            var ownership = new RecordingOwnershipGate(isComplete: false);
            var absenceProofCalls = 0;
            var recovery = new BenchmarkAbandonedSqlRunRecovery(
                "unused-approval",
                ownership,
                (_, _, _) =>
                {
                    absenceProofCalls++;
                    return Task.FromResult(scenario.NoOwnedDatabases);
                });
            var abandoned = new AbandonedSqlRun(
                "resource",
                123,
                "invocation",
                DateTimeOffset.UtcNow,
                artifactRoot,
                "strict-final");

            try
            {
                var expectedComplete = scenario.NoOwnedDatabases && !scenario.HasIncompleteWrite;
                Assert.Equal(expectedComplete, await recovery.ReconcileAsync(abandoned, CancellationToken.None));
                Assert.Equal(expectedComplete ? 0 : 1, ownership.Evidence.Count);
                Assert.Equal(scenario.HasIncompleteWrite ? 0 : 1, absenceProofCalls);
            }
            finally
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
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
