using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateRecoveryTests
{
    [Theory]
    [InlineData("sql-preflight")]
    [InlineData("sql-cleanup")]
    [InlineData("recovery-cleanup")]
    public async Task Cleanup_failure_after_runtime_preflight_retains_runtime_and_recovery_evidence(string failure)
    {
        var parent = Path.Combine(Path.GetTempPath(), "web6-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        var runtimePath = Path.Combine(parent, "runtime");
        var recoveryPath = Path.Combine(parent, "recovery");
        try
        {
            using var runtime = Web6PrivateDirectoryLease.CreateNew(runtimePath);
            using var recovery = Web6PrivateDirectoryLease.CreateNew(recoveryPath);
            File.WriteAllText(Path.Combine(runtimePath, "host.log"), "runtime-evidence");
            File.WriteAllText(Path.Combine(runtimePath, "applicationhost.config"), "runtime-config");
            File.WriteAllText(Path.Combine(recoveryPath, "ownership.json"), "recovery-evidence");
            var sqlDeleted = false;
            await Assert.ThrowsAsync<IOException>(() => Web6RecoveryCleanupDisposition.Execute(
                new[] { runtime }, () =>
                {
                    // These refusals prove runtime preflight ran but has not deleted anything.
                    Assert.ThrowsAny<IOException>(() => File.Delete(Path.Combine(runtimePath, "host.log")));
                    if (failure == "sql-preflight") throw new IOException("SQL ownership uncertain");
                    return Task.FromResult<IWeb6RecoveryCleanup>(new CleanupProbe(recovery,
                        () => { if (failure == "sql-cleanup") throw new IOException("SQL cleanup failed"); sqlDeleted = true; },
                        () => { if (failure == "recovery-cleanup") throw new IOException("Recovery disposition failed"); }));
                }));
            Assert.Equal(failure == "recovery-cleanup", sqlDeleted);
            Assert.Equal("runtime-evidence", File.ReadAllText(Path.Combine(runtimePath, "host.log")));
            Assert.Equal("runtime-config", File.ReadAllText(Path.Combine(runtimePath, "applicationhost.config")));
            Assert.Equal("recovery-evidence", File.ReadAllText(Path.Combine(recoveryPath, "ownership.json")));
            // Disposition released every handle; evidence remains available to the operator.
            recovery.Dispose();
            Directory.Move(runtimePath, runtimePath + "-retained");
        }
        finally { Directory.Delete(parent, true); }
    }

    private sealed class CleanupProbe : IWeb6RecoveryCleanup
    {
        private readonly Web6DirectoryCleanup _tree;
        private readonly Action _sql, _evidence;
        internal CleanupProbe(Web6PrivateDirectoryLease lease, Action sql, Action evidence)
        { _tree = lease.PrepareCleanup(); _sql = sql; _evidence = evidence; }
        public void Verify() => _tree.Verify();
        public Task DeleteSqlResources() { _sql(); return Task.CompletedTask; }
        public void DeleteEvidence() { _evidence(); _tree.Delete(); }
        public void Dispose() => _tree.Dispose();
    }

    [Fact]
    public async Task Verified_cleanup_removes_recovery_before_runtime_only_after_sql_success()
    {
        var parent = Path.Combine(Path.GetTempPath(), "web6-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        try
        {
            using var runtime = Web6PrivateDirectoryLease.CreateNew(Path.Combine(parent, "runtime"));
            using var recovery = Web6PrivateDirectoryLease.CreateNew(Path.Combine(parent, "recovery"));
            var runtimeLog = Path.Combine(runtime.Path, "host.log");
            var ownership = Path.Combine(recovery.Path, "ownership.json");
            File.WriteAllText(runtimeLog, "runtime"); File.WriteAllText(ownership, "recovery");
            var sqlDeleted = false;
            await Web6RecoveryCleanupDisposition.Execute(new[] { runtime }, () => Task.FromResult<IWeb6RecoveryCleanup>(
                new CleanupProbe(recovery, () =>
                {
                    Assert.True(File.Exists(runtimeLog)); Assert.True(File.Exists(ownership)); sqlDeleted = true;
                }, () => { Assert.True(sqlDeleted); Assert.True(File.Exists(runtimeLog)); })));
            Assert.False(Directory.Exists(runtime.Path)); Assert.False(Directory.Exists(recovery.Path));
        }
        finally { Directory.Delete(parent, true); }
    }

    [Web6RecoverySqlFact, Trait("Category", "Web6CandidateRecovery")]
    public async Task Web6_candidate_recovery_rehearses_distinct_owned_restore_before_service_admission()
    {
        var values = Web6CandidateRecoveryAcceptanceGate.EnvironmentValues();
        Web6CandidateRecoveryAcceptanceGate.RequireLive(values);
        var task4 = Web6WorkloadTask4Receipt.ReadProtected(values["MYDMSVN_WEB6_TASK4_ACCEPTANCE_PATH"]!,
            values["MYDMSVN_WEB6_TASK4_ACCEPTANCE_SHA256"]!);
        if (!string.Equals(task4.CandidateId, values["MYDMSVN_WEB6_CANDIDATE_ID"], StringComparison.Ordinal))
            throw new InvalidDataException("Task4 accepted candidate identity does not match the recovery candidate.");
        var publish = Web6CandidateFileSystem.CanonicalPath(values["MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY"]!);
        _ = Web6CandidateFileSet.MeasureTree(publish);
        var thumbprint = values["MYDMSVN_WEB_TEST_TLS_THUMBPRINT"]!;
        if (thumbprint.Length != 40 || thumbprint.Any(value => !Uri.IsHexDigit(value)))
            throw new InvalidDataException("Recovery TLS thumbprint is invalid.");
        await WebHostIisExpressTests.RunCandidateRecovery(values, task4, publish, thumbprint);
    }

    [Theory]
    [InlineData("same-database")]
    [InlineData("same-epoch")]
    [InlineData("same-principal")]
    public void Recovery_plan_refuses_unsafe_source_destination_contract(string mutation)
    {
        var plan = ValidPlan();
        plan = new Web6CandidateRecoveryPlan(plan.SourceDatabaseIdentity,
            mutation == "same-database" ? plan.SourceDatabaseIdentity : plan.DestinationDatabaseIdentity,
            plan.SourceEpoch, mutation == "same-epoch" ? plan.SourceEpoch : plan.RestoredEpoch,
            plan.SourceRuntimePrincipal, mutation == "same-principal" ? plan.SourceRuntimePrincipal : plan.RestoredRuntimePrincipal);
        Assert.Throws<InvalidDataException>(() => plan.Validate());
    }

    [Fact]
    public async Task Backup_digest_is_measured_after_failure_and_must_match_before_restore()
    {
        var operations = new FakeOperations { Failure = "backup-mismatch" };
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new Web6CandidateRecoveryCoordinator(operations).Execute(ValidPlan(), CancellationToken.None));
        Assert.Contains("detect-failure", operations.Calls);
        Assert.Contains("drain-source", operations.Calls);
        Assert.Contains("backup", operations.Calls);
        Assert.DoesNotContain("restore-distinct", operations.Calls);
    }

    [Fact]
    public async Task Recovery_coordinator_orders_oracle_rotation_principals_admission_health_and_fresh_write()
    {
        var operations = new FakeOperations();
        var result = await new Web6CandidateRecoveryCoordinator(operations).Execute(ValidPlan(), CancellationToken.None);

        Assert.Equal(new[]
        {
            "verify-source", "start-source", "verify-source-admission", "detect-failure", "drain-source", "backup", "restore-distinct", "oracle",
            "rotate-epoch", "reprovision-principals", "start-host", "verify-admission", "reject-stale", "authenticated-health",
            "fresh-write", "rpo", "drain-restored", "cleanup"
        }, operations.Calls);
        Assert.True(result.TrustedService);
        Assert.Equal(0, result.RpoLostBusinessRows);
        Assert.Equal(0, result.RpoLostJournalRows);
        Assert.True(result.RtoTicks > 0);
    }

    [Theory]
    [InlineData("start-source")]
    [InlineData("verify-source-admission")]
    [InlineData("detect-failure")]
    public async Task Source_start_or_outage_failure_drains_before_any_backup(string failure)
    {
        var operations = new FakeOperations { Failure = failure };
        await Assert.ThrowsAnyAsync<Exception>(() =>
            new Web6CandidateRecoveryCoordinator(operations).Execute(ValidPlan(), CancellationToken.None));
        Assert.Contains("drain-source", operations.Calls);
        Assert.DoesNotContain("backup", operations.Calls);
        Assert.True(operations.RetainedEvidence);
    }

    [Theory]
    [InlineData("oracle")]
    [InlineData("stale-writer")]
    [InlineData("drain")]
    [InlineData("cleanup-identity")]
    public async Task Recovery_failure_never_admits_or_deletes_uncertain_resources(string failure)
    {
        var operations = new FakeOperations { Failure = failure };
        await Assert.ThrowsAnyAsync<Exception>(() =>
            new Web6CandidateRecoveryCoordinator(operations).Execute(ValidPlan(), CancellationToken.None));
        Assert.False(operations.TrustedAdmission);
        Assert.False(operations.DeletedResources);
        Assert.True(operations.RetainedEvidence);
        if (failure is "stale-writer" or "cleanup-identity") Assert.Contains("drain-restored", operations.Calls);
    }

    [Theory]
    [InlineData("authenticated-health")]
    [InlineData("fresh-write")]
    [InlineData("rpo")]
    public async Task Admitted_recovery_failure_always_attempts_verified_drain_and_retains_evidence(string failure)
    {
        var operations = new FakeOperations { Failure = failure };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new Web6CandidateRecoveryCoordinator(operations).Execute(ValidPlan(), CancellationToken.None));
        Assert.Contains("start-host", operations.Calls);
        Assert.Contains("drain-restored", operations.Calls);
        Assert.DoesNotContain("cleanup", operations.Calls);
        Assert.True(operations.RetainedEvidence);
        Assert.False(operations.DeletedResources);
    }

    [Fact]
    public async Task Host_start_arms_independent_cleanup_before_admission_verification()
    {
        var operations = new FakeOperations { Failure = "verify-admission" };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new Web6CandidateRecoveryCoordinator(operations).Execute(ValidPlan(), CancellationToken.None));

        Assert.Contains("start-host", operations.Calls);
        Assert.Contains("drain-restored", operations.Calls);
        Assert.False(operations.DrainTokenWasCanceled);
        Assert.True(operations.RetainedEvidence);
    }

    [Fact]
    public async Task Start_then_throw_still_attempts_independent_host_and_queue_drain()
    {
        var operations = new FakeOperations { Failure = "start-host" };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new Web6CandidateRecoveryCoordinator(operations).Execute(ValidPlan(), CancellationToken.None));

        Assert.Contains("start-host", operations.Calls);
        Assert.Contains("drain-restored", operations.Calls);
        Assert.False(operations.DrainTokenWasCanceled);
        Assert.True(operations.RetainedEvidence);
    }

    [Fact]
    public async Task Canceled_main_token_does_not_cancel_restored_host_cleanup()
    {
        using var main = new CancellationTokenSource();
        var operations = new FakeOperations { Failure = "cancel-health", MainCancellation = main };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new Web6CandidateRecoveryCoordinator(operations).Execute(ValidPlan(), main.Token));

        Assert.True(main.IsCancellationRequested);
        Assert.Contains("drain-restored", operations.Calls);
        Assert.False(operations.DrainTokenWasCanceled);
        Assert.True(operations.RetainedEvidence);
    }

    [Fact]
    public void Timeline_refuses_nonmonotonic_or_missing_service_milestones()
    {
        var timeline = new Web6RecoveryTimeline();
        timeline.Record(Web6RecoveryMilestone.FailureDetected, 20);
        Assert.Throws<InvalidDataException>(() => timeline.Record(Web6RecoveryMilestone.BackupComplete, 19));
        Assert.Throws<InvalidDataException>(() => timeline.Complete(0, 0));
    }

    [Fact]
    public void Independent_RPO_oracle_rejects_any_business_or_journal_loss()
    {
        var before = new Web6RecoveryOracleSnapshot(7, 12, 4, 9, 3, 3, "abc");
        Assert.Equal((0, 0), Web6CandidateOracle.CompareRecovery(before, before));
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.CompareRecovery(before,
            new Web6RecoveryOracleSnapshot(6, 12, 4, 9, 3, 3, "abc")));
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.CompareRecovery(before,
            new Web6RecoveryOracleSnapshot(7, 11, 4, 9, 3, 3, "abc")));
    }

    [Theory]
    [InlineData(500, "{\"error\":{\"code\":\"InternalError\"}}")]
    [InlineData(0, "")]
    [InlineData(409, "{\"error\":{\"code\":\"Operation.Unavailable\"}}")]
    public void Stale_rejection_requires_exact_epoch_denial_not_error_or_lost_response(int status, string body) =>
        Assert.Throws<InvalidDataException>(() => Web6RecoveryHttpDenial.Require(status, body, "stale"));

    [Fact]
    public void Error_response_with_committed_mutation_is_rejected_by_zero_effect_oracle() =>
        Assert.Throws<InvalidDataException>(() => Web6RecoveryHttpDenial.RequireNoEffect("before", "committed-after-error"));

    [Theory]
    [InlineData("reparse")]
    [InlineData("path-swap")]
    public void Recovery_root_handle_refuses_reparse_or_path_swap_cleanup(string drift)
    {
        var parent = Path.Combine(Path.GetTempPath(), "web6-recovery-lease-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        var root = Path.Combine(parent, "root"); var outside = Directory.CreateDirectory(Path.Combine(parent, "outside")).FullName;
        using var lease = Web6PrivateDirectoryLease.CreateNew(root,
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        var victim = Path.Combine(root, "victim");
        if (drift == "reparse") CreateJunction(victim, outside);
        else Directory.CreateDirectory(victim);
        var swapped = false;
        Assert.Throws<IOException>(() => lease.DeleteTreeVerified(entry =>
        {
            if (drift != "path-swap" || swapped || !entry.Equals(victim, StringComparison.OrdinalIgnoreCase)) return;
            Directory.Delete(victim); CreateJunction(victim, outside); swapped = true;
        }));
        Assert.True(Directory.Exists(root)); Assert.Empty(Directory.GetFileSystemEntries(outside));
        Directory.Delete(victim); lease.DeleteTreeVerified(); Directory.Delete(outside); Directory.Delete(parent);
    }

    private static void CreateJunction(string junction, string target)
    {
        using var command = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            "/d /c mklink /J \"" + junction + "\" \"" + target + "\"") { UseShellExecute = false, CreateNoWindow = true });
        Assert.NotNull(command); Assert.True(command!.WaitForExit(5000)); Assert.Equal(0, command.ExitCode);
    }

    [Fact]
    public void Enabled_recovery_gate_missing_prerequisites_is_non_success()
    {
        var values = new Dictionary<string, string?> { ["MYDMSVN_WEB6_RECOVERY_ENABLE"] = "1" };
        var error = Assert.Throws<InvalidOperationException>(() => Web6CandidateRecoveryAcceptanceGate.RequireLive(values));
        Assert.Contains("BLOCKED", error.Message);
    }

    private static Web6CandidateRecoveryPlan ValidPlan() => new(
        "source-allocation", "restored-allocation", Guid.NewGuid(), Guid.NewGuid(),
        "runtime-source", "runtime-restored");

    private sealed class FakeOperations : IWeb6CandidateRecoveryOperations
    {
        internal readonly List<string> Calls = new();
        internal string? Failure;
        internal bool TrustedAdmission, DeletedResources, RetainedEvidence, DrainTokenWasCanceled;
        internal CancellationTokenSource? MainCancellation;
        private long _clock;

        public long Timestamp() => ++_clock;
        private Task Step(string name)
        {
            Calls.Add(name);
            if (Failure == name || (Failure == "stale-writer" && name == "reject-stale") ||
                (Failure == "drain" && name == "drain-restored") ||
                (Failure == "cleanup-identity" && name == "cleanup"))
                throw new InvalidOperationException(name);
            return Task.CompletedTask;
        }
        public Task VerifySource(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("verify-source");
        public Task StartSourceHost(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("start-source");
        public Task VerifySourceAdmission(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("verify-source-admission");
        public Task DetectFailure(CancellationToken token) => Step("detect-failure");
        public Task DrainSource(CancellationToken token) => Step("drain-source");
        public async Task<Web6VerifiedBackup> Backup(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            await Step("backup");
            return new Web6VerifiedBackup(new string('a', 64), Failure == "backup-mismatch" ? new string('b', 64) : new string('a', 64));
        }
        public Task RestoreDistinct(Web6CandidateRecoveryPlan plan, Web6VerifiedBackup backup, CancellationToken token) => Step("restore-distinct");
        public Task VerifyRestoreOracle(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("oracle");
        public Task RotateEpoch(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("rotate-epoch");
        public Task ReprovisionPrincipals(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("reprovision-principals");
        public Task StartRestoredHost(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("start-host");
        public async Task VerifyRestoredAdmission(Web6CandidateRecoveryPlan plan, CancellationToken token) { await Step("verify-admission"); TrustedAdmission = true; }
        public Task RejectStaleWriterReceiptAndStatus(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("reject-stale");
        public Task AuthenticatedHealth(CancellationToken token)
        {
            if (Failure == "cancel-health")
            {
                Calls.Add("authenticated-health");
                MainCancellation!.Cancel();
                throw new OperationCanceledException(MainCancellation.Token);
            }
            return Step("authenticated-health");
        }
        public Task FreshWrite(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("fresh-write");
        public async Task<(Web6RecoveryOracleSnapshot Before, Web6RecoveryOracleSnapshot After)> RecoverySnapshots(CancellationToken token)
        {
            await Step("rpo");
            var snapshot = new Web6RecoveryOracleSnapshot(1, 1, 1, 1, 1, 1, "digest");
            return (snapshot, snapshot);
        }
        public Task DrainRestored(CancellationToken token) { DrainTokenWasCanceled = token.IsCancellationRequested; return Step("drain-restored"); }
        public async Task CleanupVerified(CancellationToken token) { await Step("cleanup"); DeletedResources = true; }
        public void RetainEvidence() { RetainedEvidence = true; TrustedAdmission = false; }
    }

    private sealed class Web6RecoverySqlFactAttribute : FactAttribute
    {
        public Web6RecoverySqlFactAttribute() =>
            Skip = Web6CandidateRecoveryAcceptanceGate.OrdinarySkipReason(Web6CandidateRecoveryAcceptanceGate.EnvironmentValues());
    }
}

internal static class Web6RecoveryHttpDenial
{
    internal static void Require(int status, string body, string role)
    {
        if (status != 409 || string.IsNullOrWhiteSpace(role)) throw Invalid();
        try
        {
            var json = Newtonsoft.Json.Linq.JObject.Parse(body);
            if ((string?)json["error"]?["code"] != "Deployment.EpochMismatch" || json["data"] != null) throw Invalid();
        }
        catch (Newtonsoft.Json.JsonException) { throw Invalid(); }
    }

    internal static void RequireNoEffect(string before, string after)
    {
        if (string.IsNullOrWhiteSpace(before) || before != after)
            throw new InvalidDataException("Stale recovery request changed journal, document, stock, ledger or valuation state.");
    }

    private static InvalidDataException Invalid() =>
        new InvalidDataException("Stale recovery request did not receive the exact semantic epoch denial.");
}

internal sealed class Web6CandidateRecoveryPlan
{
    internal Web6CandidateRecoveryPlan(string sourceDatabaseIdentity, string destinationDatabaseIdentity,
        Guid sourceEpoch, Guid restoredEpoch, string sourceRuntimePrincipal, string restoredRuntimePrincipal)
    {
        SourceDatabaseIdentity = sourceDatabaseIdentity; DestinationDatabaseIdentity = destinationDatabaseIdentity;
        SourceEpoch = sourceEpoch; RestoredEpoch = restoredEpoch; SourceRuntimePrincipal = sourceRuntimePrincipal;
        RestoredRuntimePrincipal = restoredRuntimePrincipal;
    }
    internal string SourceDatabaseIdentity { get; }
    internal string DestinationDatabaseIdentity { get; }
    internal Guid SourceEpoch { get; }
    internal Guid RestoredEpoch { get; }
    internal string SourceRuntimePrincipal { get; }
    internal string RestoredRuntimePrincipal { get; }
    internal void Validate()
    {
        static bool Identity(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
        if (!Identity(SourceDatabaseIdentity) || !Identity(DestinationDatabaseIdentity) ||
            string.Equals(SourceDatabaseIdentity, DestinationDatabaseIdentity, StringComparison.OrdinalIgnoreCase) ||
            SourceEpoch == Guid.Empty || RestoredEpoch == Guid.Empty || SourceEpoch == RestoredEpoch ||
            !Identity(SourceRuntimePrincipal) || !Identity(RestoredRuntimePrincipal) ||
            string.Equals(SourceRuntimePrincipal, RestoredRuntimePrincipal, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("WEB6 recovery plan is not a distinct verified restore contract.");
    }
}

internal sealed class Web6VerifiedBackup
{
    internal Web6VerifiedBackup(string expectedSha256, string observedSha256)
    { ExpectedSha256 = expectedSha256; ObservedSha256 = observedSha256; }
    internal string ExpectedSha256 { get; }
    internal string ObservedSha256 { get; }
    internal void Validate()
    {
        static bool Hash(string value) => value?.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
        if (!Hash(ExpectedSha256) || !Hash(ObservedSha256) || ExpectedSha256 != ObservedSha256)
            throw new InvalidDataException("Recovery backup digest changed before restore.");
    }
}

internal enum Web6RecoveryMilestone
{
    FailureDetected,
    BackupComplete,
    RestoreComplete,
    HostAdmitted,
    AuthenticatedHealth,
    FreshWriteAccepted
}

internal sealed class Web6RecoveryTimeline
{
    private readonly Dictionary<Web6RecoveryMilestone, long> _values = new();
    private long _last = -1;
    private int _next;

    internal void Record(Web6RecoveryMilestone milestone, long timestamp)
    {
        if ((int)milestone != _next || timestamp < 0 || timestamp <= _last || _values.ContainsKey(milestone))
            throw new InvalidDataException("WEB6 recovery timeline is not strictly monotonic and complete.");
        _values.Add(milestone, timestamp); _last = timestamp; _next++;
    }

    internal Web6CandidateRecoveryResult Complete(long lostBusinessRows, long lostJournalRows)
    {
        if (_next != Enum.GetValues(typeof(Web6RecoveryMilestone)).Length || lostBusinessRows != 0 || lostJournalRows != 0)
            throw new InvalidDataException("WEB6 recovery cannot claim trusted service or zero RPO loss.");
        return new Web6CandidateRecoveryResult(true,
            checked(_values[Web6RecoveryMilestone.FreshWriteAccepted] - _values[Web6RecoveryMilestone.FailureDetected]),
            lostBusinessRows, lostJournalRows);
    }
}

internal sealed class Web6CandidateRecoveryResult
{
    internal Web6CandidateRecoveryResult(bool trustedService, long rtoTicks, long rpoLostBusinessRows, long rpoLostJournalRows)
    { TrustedService = trustedService; RtoTicks = rtoTicks; RpoLostBusinessRows = rpoLostBusinessRows; RpoLostJournalRows = rpoLostJournalRows; }
    internal bool TrustedService { get; }
    internal long RtoTicks { get; }
    internal long RpoLostBusinessRows { get; }
    internal long RpoLostJournalRows { get; }
}

internal interface IWeb6CandidateRecoveryOperations
{
    long Timestamp();
    Task VerifySource(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task StartSourceHost(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task VerifySourceAdmission(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task DetectFailure(CancellationToken token);
    Task DrainSource(CancellationToken token);
    Task<Web6VerifiedBackup> Backup(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task RestoreDistinct(Web6CandidateRecoveryPlan plan, Web6VerifiedBackup backup, CancellationToken token);
    Task VerifyRestoreOracle(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task RotateEpoch(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task ReprovisionPrincipals(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task StartRestoredHost(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task VerifyRestoredAdmission(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task RejectStaleWriterReceiptAndStatus(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task AuthenticatedHealth(CancellationToken token);
    Task FreshWrite(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task<(Web6RecoveryOracleSnapshot Before, Web6RecoveryOracleSnapshot After)> RecoverySnapshots(CancellationToken token);
    Task DrainRestored(CancellationToken token);
    Task CleanupVerified(CancellationToken token);
    void RetainEvidence();
}

internal sealed class Web6CandidateRecoveryCoordinator
{
    private readonly IWeb6CandidateRecoveryOperations _operations;
    internal Web6CandidateRecoveryCoordinator(IWeb6CandidateRecoveryOperations operations) =>
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));

    internal async Task<Web6CandidateRecoveryResult> Execute(Web6CandidateRecoveryPlan plan, CancellationToken token)
    {
        plan.Validate();
        var timeline = new Web6RecoveryTimeline();
        var sourceStartArmed = false;
        var sourceDrained = false;
        var hostStartArmed = false;
        var restoredDrained = false;
        try
        {
            await _operations.VerifySource(plan, token).ConfigureAwait(false);
            sourceStartArmed = true;
            await _operations.StartSourceHost(plan, token).ConfigureAwait(false);
            await _operations.VerifySourceAdmission(plan, token).ConfigureAwait(false);
            await _operations.DetectFailure(token).ConfigureAwait(false);
            timeline.Record(Web6RecoveryMilestone.FailureDetected, _operations.Timestamp());
            await _operations.DrainSource(token).ConfigureAwait(false);
            sourceDrained = true;
            var backup = await _operations.Backup(plan, token).ConfigureAwait(false);
            backup.Validate();
            timeline.Record(Web6RecoveryMilestone.BackupComplete, _operations.Timestamp());
            await _operations.RestoreDistinct(plan, backup, token).ConfigureAwait(false);
            timeline.Record(Web6RecoveryMilestone.RestoreComplete, _operations.Timestamp());
            await _operations.VerifyRestoreOracle(plan, token).ConfigureAwait(false);
            await _operations.RotateEpoch(plan, token).ConfigureAwait(false);
            await _operations.ReprovisionPrincipals(plan, token).ConfigureAwait(false);
            hostStartArmed = true;
            await _operations.StartRestoredHost(plan, token).ConfigureAwait(false);
            await _operations.VerifyRestoredAdmission(plan, token).ConfigureAwait(false);
            timeline.Record(Web6RecoveryMilestone.HostAdmitted, _operations.Timestamp());
            await _operations.RejectStaleWriterReceiptAndStatus(plan, token).ConfigureAwait(false);
            await _operations.AuthenticatedHealth(token).ConfigureAwait(false);
            timeline.Record(Web6RecoveryMilestone.AuthenticatedHealth, _operations.Timestamp());
            await _operations.FreshWrite(plan, token).ConfigureAwait(false);
            timeline.Record(Web6RecoveryMilestone.FreshWriteAccepted, _operations.Timestamp());
            var snapshots = await _operations.RecoverySnapshots(token).ConfigureAwait(false);
            var rpo = Web6CandidateOracle.CompareRecovery(snapshots.Before, snapshots.After);
            using (var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                await _operations.DrainRestored(cleanup.Token).ConfigureAwait(false);
                restoredDrained = true;
                await _operations.CleanupVerified(cleanup.Token).ConfigureAwait(false);
            }
            return timeline.Complete(rpo.LostBusinessRows, rpo.LostJournalRows);
        }
        catch (Exception failure)
        {
            var drainFailures = new List<Exception>();
            if (sourceStartArmed && !sourceDrained)
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await _operations.DrainSource(cleanup.Token).ConfigureAwait(false);
                }
                catch (Exception error) { drainFailures.Add(error); }
            }
            if (hostStartArmed && !restoredDrained)
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await _operations.DrainRestored(cleanup.Token).ConfigureAwait(false);
                }
                catch (Exception error) { drainFailures.Add(error); }
            }
            _operations.RetainEvidence();
            if (drainFailures.Count != 0)
                throw new AggregateException("WEB6 host failure and drain uncertainty; retain all resources.", new[] { failure }.Concat(drainFailures));
            throw;
        }
    }
}

internal static class Web6CandidateRecoveryAcceptanceGate
{
    private static readonly string[] Required =
    {
        "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING", "MYDMSVN_SQL_TEST_APPROVAL_FILE",
        "MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY", "MYDMSVN_WEB_TEST_BROWSER_DIRECTORY", "MYDMSVN_WEB_TEST_TLS_THUMBPRINT",
        "MYDMSVN_WEB6_CANDIDATE_ROOT", "MYDMSVN_WEB6_CANDIDATE_ID",
        "MYDMSVN_WEB6_TASK4_ACCEPTANCE_PATH", "MYDMSVN_WEB6_TASK4_ACCEPTANCE_SHA256",
        "MYDMSVN_WEB6_RECOVERY_ROOT"
    };

    internal static Dictionary<string, string?> EnvironmentValues() => Required
        .Concat(new[] { "MYDMSVN_WEB6_RECOVERY_ENABLE" })
        .ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.Ordinal);

    internal static string? OrdinarySkipReason(IReadOnlyDictionary<string, string?> values) =>
        values.TryGetValue("MYDMSVN_WEB6_RECOVERY_ENABLE", out var enabled) && enabled == "1"
            ? null : "WEB6 service recovery requires explicit nonproduction opt-in.";

    internal static void RequireLive(IReadOnlyDictionary<string, string?> values)
    {
        if (!values.TryGetValue("MYDMSVN_WEB6_RECOVERY_ENABLE", out var enabled) || enabled != "1")
            throw new InvalidOperationException("BLOCKED: MYDMSVN_WEB6_RECOVERY_ENABLE must equal 1.");
        var missing = Required.Where(name => !values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value)).ToArray();
        if (missing.Length != 0) throw new InvalidOperationException("BLOCKED: missing WEB6 recovery prerequisites: " + string.Join(",", missing));
    }
}
