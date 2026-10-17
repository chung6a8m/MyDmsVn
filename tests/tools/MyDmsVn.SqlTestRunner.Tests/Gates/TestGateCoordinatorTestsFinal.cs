using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Gates;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Reporting;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Gates;

public sealed class TestGateCoordinatorTestsFinal
{
    [Fact]
    public void Sequential_gate_timeout_budget_is_additive()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(8100),
            TestGateTimeoutBudget.Calculate(new[] { 900, 7200 }));
    }

    [Fact]
    public async Task Final_SQL_gate_holds_one_lease_through_T02_reconciliation_and_report()
    {
        using var repository = new FinalRepository();
        var events = new List<string>();
        var result = await Coordinator(repository, events).RunAsync(repository.Options(), CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Succeeded, result.Status);
        Assert.Equal(new[]
        {
            "approval", "lease", "preflight-net48", "run-net48", "preflight-net8.0", "run-net8.0",
            "ownership", "reconcile", "report", "release",
        }, events);
        Assert.Equal(new[] { "net48", "net8.0" }, result.Frameworks.Select(item => item.Tfm));
    }

    [Fact]
    public async Task Recovered_prior_lease_is_completed_when_current_Final_run_is_clean()
    {
        using var repository = new FinalRepository();
        var events = new List<string>();
        var leases = new FinalLeaseFactory(events, FinalFailure.PriorLeaseRecovered);
        var coordinator = new TestGateCoordinator(
            new NoProcessRunner(),
            new FinalInventory(events),
            new FinalReport(events, complete: true),
            new FinalApproval(events, valid: true),
            leases,
            new FinalSqlRunner(repository, events, FinalFailure.PriorLeaseRecovered),
            new NoRecovery(),
            new AcceptSqlManifestPreflight());

        var result = await coordinator.RunAsync(repository.Options(), CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Succeeded, result.Status);
        Assert.NotNull(leases.LastLease);
        Assert.True(leases.LastLease.IsCompleted);
        Assert.Contains("release", events);
    }

    [Fact]
    public async Task Unreconciled_current_SQL_execution_does_not_complete_its_lease()
    {
        using var repository = new FinalRepository();
        var events = new List<string>();
        var leases = new FinalLeaseFactory(events, FinalFailure.UnreconciledOwnership);
        var coordinator = new TestGateCoordinator(
            new NoProcessRunner(),
            new FinalInventory(events),
            new FinalReport(events, complete: true),
            new FinalApproval(events, valid: true),
            leases,
            new FinalSqlRunner(repository, events, FinalFailure.UnreconciledOwnership),
            new NoRecovery(),
            new AcceptSqlManifestPreflight());

        var result = await coordinator.RunAsync(repository.Options(), CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Incomplete, result.Status);
        Assert.NotNull(leases.LastLease);
        Assert.False(leases.LastLease.IsCompleted);
        Assert.Contains("release", events);
    }

    [Fact]
    public async Task Modular_Final_runs_every_SQL_project_under_one_global_lease()
    {
        using var repository = new FinalRepository(multiProject: true);
        var events = new List<string>();

        var result = await Coordinator(repository, events).RunAsync(repository.Options(), CancellationToken.None);

        Assert.True(result.Status == TestGateRunStatus.Succeeded,
            result.Status + ": " + string.Join(",", events));
        Assert.Equal(1, events.Count(item => item == "approval"));
        Assert.Equal(1, events.Count(item => item == "lease"));
        Assert.Equal(4, events.Count(item => item.StartsWith("run-", StringComparison.Ordinal)));
        Assert.Equal(2, events.Count(item => item == "report"));
        Assert.Equal(4, result.Frameworks.Count);
        Assert.Equal(1, events.Count(item => item == "release"));
    }

    [Theory]
    [InlineData(FinalFailure.Approval, TestGateRunStatus.ResourceDenied)]
    [InlineData(FinalFailure.LeaseTimeout, TestGateRunStatus.ResourceDenied)]
    [InlineData(FinalFailure.RecoveryDenied, TestGateRunStatus.ResourceDenied)]
    [InlineData(FinalFailure.SqlIncomplete, TestGateRunStatus.Incomplete)]
    [InlineData(FinalFailure.Reconciliation, TestGateRunStatus.Incomplete)]
    [InlineData(FinalFailure.Report, TestGateRunStatus.Incomplete)]
    public async Task Final_failure_stops_before_any_later_unsafe_stage(
        FinalFailure failure,
        TestGateRunStatus expected)
    {
        using var repository = new FinalRepository();
        var events = new List<string>();

        var result = await Coordinator(repository, events, failure).RunAsync(
            repository.Options(),
            CancellationToken.None);

        Assert.Equal(expected, result.Status);
        if (failure == FinalFailure.Approval)
        {
            Assert.Equal(new[] { "approval" }, events);
        }
        else if (failure is FinalFailure.LeaseTimeout or FinalFailure.RecoveryDenied)
        {
            Assert.Equal(new[] { "approval", "lease" }, events);
        }
        else
        {
            Assert.Equal("release", events[^1]);
        }
    }

    [Fact]
    public async Task SQL_manifest_preflight_failure_never_acquires_a_lease()
    {
        using var repository = new FinalRepository();
        var events = new List<string>();
        var leases = new FinalLeaseFactory(events, FinalFailure.None);
        var coordinator = new TestGateCoordinator(
            new NoProcessRunner(),
            new FinalInventory(events),
            new FinalReport(events, complete: true),
            new FinalApproval(events, valid: true),
            leases,
            new FinalSqlRunner(repository, events, FinalFailure.None),
            new NoRecovery(),
            new RejectSqlManifestPreflight());

        var result = await coordinator.RunAsync(repository.Options(), CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Incomplete, result.Status);
        Assert.Equal(new[] { "approval" }, events);
        Assert.Null(leases.LastLease);
    }

    [Fact]
    public async Task Long_artifact_root_is_rejected_before_SQL_preflight_or_lease()
    {
        using var repository = new FinalRepository();
        var events = new List<string>();
        var leases = new FinalLeaseFactory(events, FinalFailure.None);
        var coordinator = new TestGateCoordinator(
            new NoProcessRunner(),
            new FinalInventory(events),
            new FinalReport(events, complete: true),
            new FinalApproval(events, valid: true),
            leases,
            new FinalSqlRunner(repository, events, FinalFailure.None),
            new NoRecovery(),
            new SqlGateManifestPreflight(new NoProcessRunner(), new FinalInventory(events)));
        var longArtifactRoot = Path.Combine(Path.GetTempPath(), new string('a', 230));

        var result = await coordinator.RunAsync(
            repository.Options() with { ArtifactRoot = longArtifactRoot },
            CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Incomplete, result.Status);
        Assert.Empty(events);
        Assert.Null(leases.LastLease);
    }

    [Fact]
    public async Task SQL_manifest_preflight_uses_a_unique_output_root_for_each_invocation()
    {
        using var repository = new FinalRepository();
        var processes = new OverlappingManifestDiscoveryProcessRunner();
        var manifest = TestGateManifestLoader.Load(repository.Manifest);

        var first = new SqlGateManifestPreflight(
            processes, new FinalInventory(new List<string>())).VerifyAsync(
                repository.Options(), manifest, manifest.Gates, CancellationToken.None);
        var second = new SqlGateManifestPreflight(
            processes, new FinalInventory(new List<string>())).VerifyAsync(
                repository.Options(), manifest, manifest.Gates, CancellationToken.None);
        var bothBuilds = await Task.WhenAny(
            processes.BothBuildsStarted.Task,
            Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(processes.BothBuildsStarted.Task, bothBuilds);
        processes.ReleaseBuilds.TrySetResult(true);
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.IsComplete, string.Join(",", result.Issues)));
        var buildOutputs = processes.Commands
            .Where(command => command.Arguments.StartsWith("build ", StringComparison.Ordinal))
            .Select(command => command.Arguments
                .Split(new[] { "-p:BaseOutputPath=\"" }, StringSplitOptions.None)[1]
                .Split('"')[0])
            .ToArray();
        Assert.Equal(2, buildOutputs.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var discoveryOutputs = processes.Commands
            .Where(command => command.Arguments.Contains("--list-tests", StringComparison.Ordinal))
            .Select(command => command.Arguments
                .Split(new[] { "-p:BaseOutputPath=\"" }, StringSplitOptions.None)[1]
                .Split('"')[0])
            .ToArray();
        Assert.Equal(4, discoveryOutputs.Length);
        Assert.All(discoveryOutputs, output => Assert.Contains(output, buildOutputs));
    }

    [Fact]
    public async Task SQL_manifest_preflight_detects_stale_fingerprint_without_running_a_test()
    {
        using var repository = new FinalRepository();
        var processes = new ManifestDiscoveryProcessRunner();
        var manifest = TestGateManifestLoader.Load(repository.Manifest);
        var drifted = manifest with
        {
            LogicalCases = manifest.LogicalCases.Select(item =>
                item with { ReviewedCodeFingerprint = new string('f', 64) }).ToArray(),
        };

        var result = await new SqlGateManifestPreflight(
            processes, new FinalInventory(new List<string>())).VerifyAsync(
                repository.Options(), drifted, drifted.Gates, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Contains(result.Issues, issue => issue.Reason.Contains(
            "FingerprintMismatch", StringComparison.Ordinal));
        Assert.DoesNotContain(processes.Commands, command =>
            command.Arguments.Contains("--logger", StringComparison.Ordinal));
        Assert.All(processes.Commands, command =>
            Assert.True(command.Arguments.StartsWith("build ", StringComparison.Ordinal) ||
                command.Arguments.Contains("--list-tests", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task SQL_manifest_preflight_accepts_complete_source_and_discovery_without_running_SQL()
    {
        using var repository = new FinalRepository();
        var processes = new ManifestDiscoveryProcessRunner();
        var manifest = TestGateManifestLoader.Load(repository.Manifest);

        var result = await new SqlGateManifestPreflight(
            processes, new FinalInventory(new List<string>())).VerifyAsync(
                repository.Options(), manifest, manifest.Gates, CancellationToken.None);

        Assert.True(result.IsComplete, string.Join(",", result.Issues));
        Assert.Equal(4, processes.Commands.Count);
        Assert.DoesNotContain(processes.Commands, command =>
            command.Arguments.Contains("--logger", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancellation_releases_lease_only_after_SQL_cleanup()
    {
        using var repository = new FinalRepository();
        var events = new List<string>();

        var result = await Coordinator(repository, events, FinalFailure.Cancelled).RunAsync(
            repository.Options(),
            CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Incomplete, result.Status);
        Assert.True(events.IndexOf("cleanup") < events.IndexOf("release"));
        Assert.DoesNotContain("run-net8.0", events);
    }

    [Fact]
    public async Task Final_preflight_rejection_releases_without_recovery()
    {
        using var repository = new FinalRepository();
        var events = new List<string>();
        var leases = new FinalLeaseFactory(events, FinalFailure.PreflightRejected);
        var coordinator = new TestGateCoordinator(
            new NoProcessRunner(),
            new FinalInventory(events),
            new FinalReport(events, complete: true),
            new FinalApproval(events, valid: true),
            leases,
            new FinalSqlRunner(repository, events, FinalFailure.PreflightRejected),
            new NoRecovery(),
            new AcceptSqlManifestPreflight());

        var result = await coordinator.RunAsync(repository.Options(), CancellationToken.None);

        Assert.Equal(TestGateRunStatus.Incomplete, result.Status);
        Assert.NotNull(leases.LastLease);
        Assert.True(leases.LastLease.IsCompleted);
        Assert.DoesNotContain(events, item => item.StartsWith("run-", StringComparison.Ordinal));
    }

    private static TestGateCoordinator Coordinator(
        FinalRepository repository,
        IList<string> events,
        FinalFailure failure = FinalFailure.None) => new(
        new NoProcessRunner(),
        new FinalInventory(events),
        new FinalReport(events, failure != FinalFailure.Report),
        new FinalApproval(events, failure != FinalFailure.Approval),
        new FinalLeaseFactory(events, failure),
        new FinalSqlRunner(repository, events, failure),
        new NoRecovery(),
        new AcceptSqlManifestPreflight());

    public enum FinalFailure
    {
        None,
        Approval,
        LeaseTimeout,
        RecoveryDenied,
        SqlIncomplete,
        Reconciliation,
        Report,
        Cancelled,
        PreflightRejected,
        PriorLeaseRecovered,
        UnreconciledOwnership,
    }

    private sealed class FinalApproval : ITestGateApprovalReader
    {
        private readonly IList<string> _events;
        private readonly bool _valid;

        public FinalApproval(IList<string> events, bool valid)
        {
            _events = events;
            _valid = valid;
        }

        public bool TryLoad(string path, out string fingerprint)
        {
            _events.Add("approval");
            fingerprint = new string('a', 64);
            return _valid;
        }
    }

    private sealed class FinalLeaseFactory : ISqlExecutionLeaseFactory
    {
        private readonly IList<string> _events;
        private readonly FinalFailure _failure;

        public FinalLeaseFactory(IList<string> events, FinalFailure failure)
        {
            _events = events;
            _failure = failure;
        }

        public SqlExecutionLease? LastLease { get; private set; }

        public Task<SqlExecutionLeaseAcquireResult> AcquireAsync(
            SqlExecutionLeaseRequest request,
            CancellationToken cancellationToken)
        {
            _events.Add("lease");
            if (_failure == FinalFailure.LeaseTimeout)
            {
                return Task.FromResult(new SqlExecutionLeaseAcquireResult(SqlExecutionLeaseAcquireStatus.TimedOut, null));
            }

            if (_failure == FinalFailure.RecoveryDenied)
            {
                return Task.FromResult(new SqlExecutionLeaseAcquireResult(SqlExecutionLeaseAcquireStatus.BlockedRecovery, null));
            }

            var release = new EventWaitHandle(false, EventResetMode.ManualReset);
            var completion = Task.Run(() =>
            {
                release.WaitOne();
                _events.Add("release");
            });
            LastLease = new SqlExecutionLease(release, completion);
            return Task.FromResult(new SqlExecutionLeaseAcquireResult(
                _failure == FinalFailure.PriorLeaseRecovered
                    ? SqlExecutionLeaseAcquireStatus.Recovered
                    : SqlExecutionLeaseAcquireStatus.Acquired,
                LastLease));
        }
    }

    private sealed class FinalSqlRunner : ITestGateSqlAcceptanceRunner
    {
        private readonly FinalRepository _repository;
        private readonly IList<string> _events;
        private readonly FinalFailure _failure;

        public FinalSqlRunner(FinalRepository repository, IList<string> events, FinalFailure failure)
        {
            _repository = repository;
            _events = events;
            _failure = failure;
        }

        public Task<BenchmarkRunResult> RunAsync(BenchmarkRunOptions options, CancellationToken cancellationToken)
        {
            Assert.Equal("pr-acceptance", options.Mode);
            Assert.Equal(0, options.WarmupPasses);
            Assert.Equal(1, options.MeasuredPasses);
            Assert.Equal(new[] { "net48", "net8.0" }, options.TargetFrameworks);
            if (_failure == FinalFailure.PreflightRejected)
            {
                _events.Add("preflight-net48");
                return Task.FromResult(new BenchmarkRunResult(false, Array.Empty<BenchmarkFrameworkRun>()));
            }

            if (_failure == FinalFailure.Cancelled)
            {
                _events.Add("preflight-net48");
                _events.Add("run-net48");
                _events.Add("cleanup");
                throw new OperationCanceledException();
            }

            var runs = new List<BenchmarkFrameworkRun>();
            var testName = options.TestProject.Contains("Sql.Module.Tests", StringComparison.Ordinal)
                ? "Sql.Module.Tests.Case"
                : "Sql.Tests.Case";
            foreach (var tfm in options.TargetFrameworks)
            {
                _events.Add("preflight-" + tfm);
                _events.Add("run-" + tfm);
                var trx = Path.Combine(_repository.Root, tfm + ".trx");
                var name = _failure == FinalFailure.Reconciliation ? "Sql.Tests.Unexpected" : testName;
                File.WriteAllText(
                    trx,
                    "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
                    $"<UnitTestResult testName=\"{name}\" outcome=\"Passed\" />" +
                    "</Results></TestRun>");
                runs.Add(new BenchmarkFrameworkRun(
                    "run-" + tfm,
                    tfm,
                    trx,
                    Path.Combine(_repository.Root, tfm + ".timing"),
                    BenchmarkPassKind.Acceptance,
                    1,
                    new CommandResult(0, false, 1, string.Empty, string.Empty, Array.Empty<int>(), TimeSpan.Zero),
                    "The following Tests are available:\n    " + name + "\n"));
            }

            _events.Add("ownership");
            return Task.FromResult(new BenchmarkRunResult(
                _failure != FinalFailure.SqlIncomplete,
                runs,
                _failure == FinalFailure.UnreconciledOwnership));
        }
    }

    private sealed class FinalInventory : ITestGateSourceInventory
    {
        private readonly IList<string> _events;
        private bool _recorded;

        public FinalInventory(IList<string> events) => _events = events;

        public IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath)
        {
            if (!_recorded)
            {
                _events.Add("reconcile");
                _recorded = true;
            }

            var methodName = assemblyPath.Contains("Sql.Module.Tests", StringComparison.Ordinal)
                ? "Sql.Module.Tests.Case"
                : "Sql.Tests.Case";
            return new[]
            {
                new TestMethodDescriptor(methodName, false, FinalRepository.Fingerprint)
                {
                    NormalizedSignature = methodName + "()",
                },
            };
        }
    }

    private sealed class FinalReport : ITestGateReportExporter
    {
        private readonly IList<string> _events;
        private readonly bool _complete;

        public FinalReport(IList<string> events, bool complete)
        {
            _events = events;
            _complete = complete;
        }

        public Task<TestGateReportExportResult> ExportAsync(TestGateReportEvidence evidence, CancellationToken cancellationToken)
        {
            _events.Add("report");
            return Task.FromResult(new TestGateReportExportResult(_complete, "report.json"));
        }
    }

    private sealed class AcceptSqlManifestPreflight : ISqlGateManifestPreflight
    {
        public Task<SqlGateManifestPreflightResult> VerifyAsync(
            TestGateRunOptions options,
            TestGateManifest manifest,
            IReadOnlyList<TestGateDefinition> gates,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SqlGateManifestPreflightResult(
                Array.Empty<SqlGateManifestPreflightIssue>()));
    }

    private sealed class RejectSqlManifestPreflight : ISqlGateManifestPreflight
    {
        public Task<SqlGateManifestPreflightResult> VerifyAsync(
            TestGateRunOptions options,
            TestGateManifest manifest,
            IReadOnlyList<TestGateDefinition> gates,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SqlGateManifestPreflightResult(new[]
            {
                new SqlGateManifestPreflightIssue("final-sql", "net48", "FingerprintMismatch"),
            }));
    }

    private sealed class ManifestDiscoveryProcessRunner : IProcessRunner
    {
        public List<CommandSpec> Commands { get; } = new();

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            var name = command.Arguments.Contains("Sql.Module.Tests", StringComparison.Ordinal)
                ? "Sql.Module.Tests.Case"
                : "Sql.Tests.Case";
            var output = command.Arguments.Contains("--list-tests", StringComparison.Ordinal)
                ? "The following Tests are available:\n    " + name + "\n"
                : string.Empty;
            return Task.FromResult(new CommandResult(
                0, false, 1, output, string.Empty, Array.Empty<int>(), TimeSpan.Zero));
        }
    }

    private sealed class OverlappingManifestDiscoveryProcessRunner : IProcessRunner
    {
        private int _buildCalls;

        public ConcurrentBag<CommandSpec> Commands { get; } = new();

        public TaskCompletionSource<bool> BothBuildsStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseBuilds { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            if (command.Arguments.StartsWith("build ", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _buildCalls) == 2)
                {
                    BothBuildsStarted.TrySetResult(true);
                }

                await ReleaseBuilds.Task.WaitAsync(cancellationToken);
                return new CommandResult(
                    0, false, 1, string.Empty, string.Empty, Array.Empty<int>(), TimeSpan.Zero);
            }

            return new CommandResult(
                0, false, 1, "The following Tests are available:\n    Sql.Tests.Case\n",
                string.Empty, Array.Empty<int>(), TimeSpan.Zero);
        }
    }

    private sealed class NoRecovery : IAbandonedSqlRunRecovery
    {
        public Task<bool> ReconcileAsync(AbandonedSqlRun lease, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class NoProcessRunner : IProcessRunner
    {
        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Final SQL gate must delegate to T02 once.");
    }

    private sealed class FinalRepository : IDisposable
    {
        public const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        public FinalRepository(bool multiProject = false)
        {
            Root = Path.Combine(Path.GetTempPath(), "mydms-final-gate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "tests", "Sql.Tests"));
            if (multiProject)
            {
                Directory.CreateDirectory(Path.Combine(Root, "tests", "Sql.Module.Tests"));
                File.WriteAllText(
                    Path.Combine(Root, "tests", "Sql.Module.Tests", "Sql.Module.Tests.csproj"),
                    "<Project><PropertyGroup><TargetFrameworks>net48;net8.0</TargetFrameworks></PropertyGroup></Project>");
            }
            File.WriteAllText(
                Path.Combine(Root, "tests", "Sql.Tests", "Sql.Tests.csproj"),
                "<Project><PropertyGroup><TargetFrameworks>net48;net8.0</TargetFrameworks></PropertyGroup></Project>");
            Manifest = Path.Combine(Root, "manifest.json");
            File.WriteAllText(Manifest, multiProject ? MultiProjectJson() : Json());
            Approval = Path.Combine(Root, "approval.json");
            File.WriteAllText(Approval, "{}");
        }

        public string Root { get; }
        public string Manifest { get; }
        public string Approval { get; }

        public TestGateRunOptions Options() => new(
            Root, Manifest, TestGateMode.Final, "20261016-001", "TESTGATE-T03",
            Path.Combine(Root, "artifacts"), Approval,
            "0123456789abcdef0123456789abcdef01234567");

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static string Json() => $$"""
        {
          "schemaVersion": 1,
          "gates": [{
            "gateId": "final-sql", "planId": "20261016-001", "taskId": "TESTGATE-T03", "mode": "Final",
            "project": "tests/Sql.Tests/Sql.Tests.csproj", "frameworks": ["net48", "net8.0"], "module": "SQL", "classification": ["NonSql"],
            "selection": { "kind": "UnfilteredProject", "patterns": [] }, "buildPrerequisites": [],
            "resources": { "sql": "RequiredExclusive", "gui": "None" }, "strictMode": "SqlPrAcceptance",
            "commandTimeoutSeconds": 5, "overallTimeoutSeconds": 30,
            "evidence": ["Source", "Discovery", "Trx", "SanitizedReport", "Ownership"]
          }],
          "logicalCases": [{
            "logicalCaseId": "sql-case", "ownerProject": "tests/Sql.Tests/Sql.Tests.csproj", "frameworks": ["net48", "net8.0"],
            "normalizedMethodSignature": "Sql.Tests.Case()", "reviewedCodeFingerprint": "{{Fingerprint}}",
            "classification": ["NonSql"], "rows": [{ "rowKey": "fact", "locatorSha256": null }]
          }],
          "identityMappings": [], "exemptions": []
        }
        """;

        private static string MultiProjectJson() => $$"""
        {
          "schemaVersion": 1,
          "gates": [
            {
              "gateId": "final-sql-core", "planId": "20261016-001", "taskId": "TESTGATE-T03", "mode": "Final",
              "project": "tests/Sql.Tests/Sql.Tests.csproj", "frameworks": ["net48", "net8.0"], "module": "Core", "classification": ["NonSql"],
              "selection": { "kind": "UnfilteredProject", "patterns": [] }, "buildPrerequisites": [],
              "resources": { "sql": "RequiredExclusive", "gui": "None" }, "strictMode": "SqlPrAcceptance",
              "commandTimeoutSeconds": 5, "overallTimeoutSeconds": 30,
              "evidence": ["Source", "Discovery", "Trx", "SanitizedReport", "Ownership"]
            },
            {
              "gateId": "final-sql-module", "planId": "20261016-001", "taskId": "TESTGATE-T03", "mode": "Final",
              "project": "tests/Sql.Module.Tests/Sql.Module.Tests.csproj", "frameworks": ["net48", "net8.0"], "module": "Module", "classification": ["NonSql"],
              "selection": { "kind": "UnfilteredProject", "patterns": [] }, "buildPrerequisites": [],
              "resources": { "sql": "RequiredExclusive", "gui": "None" }, "strictMode": "SqlPrAcceptance",
              "commandTimeoutSeconds": 5, "overallTimeoutSeconds": 30,
              "evidence": ["Source", "Discovery", "Trx", "SanitizedReport", "Ownership"]
            }
          ],
          "logicalCases": [
            {
              "logicalCaseId": "sql-case-core", "ownerProject": "tests/Sql.Tests/Sql.Tests.csproj", "frameworks": ["net48", "net8.0"],
              "normalizedMethodSignature": "Sql.Tests.Case()", "reviewedCodeFingerprint": "{{Fingerprint}}",
              "classification": ["NonSql"], "rows": [{ "rowKey": "fact", "locatorSha256": null }]
            },
            {
              "logicalCaseId": "sql-case-module", "ownerProject": "tests/Sql.Module.Tests/Sql.Module.Tests.csproj", "frameworks": ["net48", "net8.0"],
              "normalizedMethodSignature": "Sql.Module.Tests.Case()", "reviewedCodeFingerprint": "{{Fingerprint}}",
              "classification": ["NonSql"], "rows": [{ "rowKey": "fact", "locatorSha256": null }]
            }
          ],
          "identityMappings": [], "exemptions": []
        }
        """;
    }
}
