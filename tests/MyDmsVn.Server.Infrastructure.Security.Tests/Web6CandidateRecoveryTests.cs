using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateRecoveryTests
{
    [Web6RecoverySqlFact, Trait("Category", "Web6CandidateRecovery")]
    public async Task Web6_candidate_recovery_rehearses_distinct_owned_restore_before_service_admission()
    {
        var values = Web6CandidateRecoveryAcceptanceGate.EnvironmentValues();
        Web6CandidateRecoveryAcceptanceGate.RequireLive(values);
        var fixture = await Web6MaintenanceRecoveryFixture.CreateAsync();
        var result = await fixture.RunAsync(Web6MaintenanceRecoveryFixture.Failure.None);
        Assert.True(result.Succeeded, result.Phase);
        Assert.True(result.PhysicalOracleMatched);
        Assert.True(result.CopyOnlyChecksummed);
        Assert.Equal(1, result.EpochRotations);
        Assert.Equal(3, result.StaleDenials);
        Assert.Equal(1, result.CurrentOperations);
        Assert.True(result.ResourcesAbsent);
    }

    [Theory]
    [InlineData("same-database")]
    [InlineData("same-epoch")]
    [InlineData("same-principal")]
    [InlineData("backup-mismatch")]
    public void Recovery_plan_refuses_unsafe_source_destination_contract(string mutation)
    {
        var plan = ValidPlan();
        plan = new Web6CandidateRecoveryPlan(plan.SourceDatabaseIdentity,
            mutation == "same-database" ? plan.SourceDatabaseIdentity : plan.DestinationDatabaseIdentity,
            plan.SourceEpoch, mutation == "same-epoch" ? plan.SourceEpoch : plan.RestoredEpoch,
            plan.SourceRuntimePrincipal, mutation == "same-principal" ? plan.SourceRuntimePrincipal : plan.RestoredRuntimePrincipal,
            plan.ExpectedBackupSha256, mutation == "backup-mismatch" ? new string('f', 64) : plan.ObservedBackupSha256);
        Assert.Throws<InvalidDataException>(() => plan.Validate());
    }

    [Fact]
    public async Task Recovery_coordinator_orders_oracle_rotation_principals_admission_health_and_fresh_write()
    {
        var operations = new FakeOperations();
        var result = await new Web6CandidateRecoveryCoordinator(operations).Execute(ValidPlan(), CancellationToken.None);

        Assert.Equal(new[]
        {
            "verify-source", "detect-failure", "drain-source", "backup", "restore-distinct", "oracle",
            "rotate-epoch", "reprovision-principals", "admit-host", "reject-stale", "authenticated-health",
            "fresh-write", "rpo", "drain-restored", "cleanup"
        }, operations.Calls);
        Assert.True(result.TrustedService);
        Assert.Equal(0, result.RpoLostBusinessRows);
        Assert.Equal(0, result.RpoLostJournalRows);
        Assert.True(result.RtoTicks > 0);
    }

    [Theory]
    [InlineData("oracle")]
    [InlineData("stale-writer")]
    [InlineData("drain")]
    [InlineData("cleanup-identity")]
    public async Task Recovery_failure_never_admits_or_deletes_uncertain_resources(string failure)
    {
        var operations = new FakeOperations { Failure = failure };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new Web6CandidateRecoveryCoordinator(operations).Execute(ValidPlan(), CancellationToken.None));
        Assert.False(operations.TrustedAdmission);
        Assert.False(operations.DeletedResources);
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

    [Fact]
    public void Enabled_recovery_gate_missing_prerequisites_is_non_success()
    {
        var values = new Dictionary<string, string?> { ["MYDMSVN_WEB6_RECOVERY_ENABLE"] = "1" };
        var error = Assert.Throws<InvalidOperationException>(() => Web6CandidateRecoveryAcceptanceGate.RequireLive(values));
        Assert.Contains("BLOCKED", error.Message);
    }

    private static Web6CandidateRecoveryPlan ValidPlan() => new(
        "source-allocation", "restored-allocation", Guid.NewGuid(), Guid.NewGuid(),
        "runtime-source", "runtime-restored", new string('a', 64), new string('a', 64));

    private sealed class FakeOperations : IWeb6CandidateRecoveryOperations
    {
        internal readonly List<string> Calls = new();
        internal string? Failure;
        internal bool TrustedAdmission, DeletedResources, RetainedEvidence;
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
        public Task DetectFailure(CancellationToken token) => Step("detect-failure");
        public Task DrainSource(CancellationToken token) => Step("drain-source");
        public Task Backup(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("backup");
        public Task RestoreDistinct(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("restore-distinct");
        public Task VerifyRestoreOracle(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("oracle");
        public Task RotateEpoch(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("rotate-epoch");
        public Task ReprovisionPrincipals(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("reprovision-principals");
        public async Task AdmitHost(Web6CandidateRecoveryPlan plan, CancellationToken token) { await Step("admit-host"); TrustedAdmission = true; }
        public Task RejectStaleWriterReceiptAndStatus(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("reject-stale");
        public Task AuthenticatedHealth(CancellationToken token) => Step("authenticated-health");
        public Task FreshWrite(Web6CandidateRecoveryPlan plan, CancellationToken token) => Step("fresh-write");
        public async Task<(Web6RecoveryOracleSnapshot Before, Web6RecoveryOracleSnapshot After)> RecoverySnapshots(CancellationToken token)
        {
            await Step("rpo");
            var snapshot = new Web6RecoveryOracleSnapshot(1, 1, 1, 1, 1, 1, "digest");
            return (snapshot, snapshot);
        }
        public Task DrainRestored(CancellationToken token) => Step("drain-restored");
        public async Task CleanupVerified(CancellationToken token) { await Step("cleanup"); DeletedResources = true; }
        public void RetainEvidence() { RetainedEvidence = true; TrustedAdmission = false; }
    }

    private sealed class Web6RecoverySqlFactAttribute : FactAttribute
    {
        public Web6RecoverySqlFactAttribute() =>
            Skip = Web6CandidateRecoveryAcceptanceGate.OrdinarySkipReason(Web6CandidateRecoveryAcceptanceGate.EnvironmentValues());
    }
}

internal sealed class Web6CandidateRecoveryPlan
{
    internal Web6CandidateRecoveryPlan(string sourceDatabaseIdentity, string destinationDatabaseIdentity,
        Guid sourceEpoch, Guid restoredEpoch, string sourceRuntimePrincipal, string restoredRuntimePrincipal,
        string expectedBackupSha256, string observedBackupSha256)
    {
        SourceDatabaseIdentity = sourceDatabaseIdentity; DestinationDatabaseIdentity = destinationDatabaseIdentity;
        SourceEpoch = sourceEpoch; RestoredEpoch = restoredEpoch; SourceRuntimePrincipal = sourceRuntimePrincipal;
        RestoredRuntimePrincipal = restoredRuntimePrincipal; ExpectedBackupSha256 = expectedBackupSha256;
        ObservedBackupSha256 = observedBackupSha256;
    }
    internal string SourceDatabaseIdentity { get; }
    internal string DestinationDatabaseIdentity { get; }
    internal Guid SourceEpoch { get; }
    internal Guid RestoredEpoch { get; }
    internal string SourceRuntimePrincipal { get; }
    internal string RestoredRuntimePrincipal { get; }
    internal string ExpectedBackupSha256 { get; }
    internal string ObservedBackupSha256 { get; }
    internal void Validate()
    {
        static bool Identity(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
        static bool Hash(string value) => value?.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
        if (!Identity(SourceDatabaseIdentity) || !Identity(DestinationDatabaseIdentity) ||
            string.Equals(SourceDatabaseIdentity, DestinationDatabaseIdentity, StringComparison.OrdinalIgnoreCase) ||
            SourceEpoch == Guid.Empty || RestoredEpoch == Guid.Empty || SourceEpoch == RestoredEpoch ||
            !Identity(SourceRuntimePrincipal) || !Identity(RestoredRuntimePrincipal) ||
            string.Equals(SourceRuntimePrincipal, RestoredRuntimePrincipal, StringComparison.OrdinalIgnoreCase) ||
            !Hash(ExpectedBackupSha256) || !Hash(ObservedBackupSha256) ||
            !string.Equals(ExpectedBackupSha256, ObservedBackupSha256, StringComparison.Ordinal))
            throw new InvalidDataException("WEB6 recovery plan is not a distinct verified restore contract.");
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
    Task DetectFailure(CancellationToken token);
    Task DrainSource(CancellationToken token);
    Task Backup(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task RestoreDistinct(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task VerifyRestoreOracle(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task RotateEpoch(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task ReprovisionPrincipals(Web6CandidateRecoveryPlan plan, CancellationToken token);
    Task AdmitHost(Web6CandidateRecoveryPlan plan, CancellationToken token);
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
        try
        {
            await _operations.VerifySource(plan, token).ConfigureAwait(false);
            await _operations.DetectFailure(token).ConfigureAwait(false);
            timeline.Record(Web6RecoveryMilestone.FailureDetected, _operations.Timestamp());
            await _operations.DrainSource(token).ConfigureAwait(false);
            await _operations.Backup(plan, token).ConfigureAwait(false);
            timeline.Record(Web6RecoveryMilestone.BackupComplete, _operations.Timestamp());
            await _operations.RestoreDistinct(plan, token).ConfigureAwait(false);
            timeline.Record(Web6RecoveryMilestone.RestoreComplete, _operations.Timestamp());
            await _operations.VerifyRestoreOracle(plan, token).ConfigureAwait(false);
            await _operations.RotateEpoch(plan, token).ConfigureAwait(false);
            await _operations.ReprovisionPrincipals(plan, token).ConfigureAwait(false);
            await _operations.AdmitHost(plan, token).ConfigureAwait(false);
            timeline.Record(Web6RecoveryMilestone.HostAdmitted, _operations.Timestamp());
            await _operations.RejectStaleWriterReceiptAndStatus(plan, token).ConfigureAwait(false);
            await _operations.AuthenticatedHealth(token).ConfigureAwait(false);
            timeline.Record(Web6RecoveryMilestone.AuthenticatedHealth, _operations.Timestamp());
            await _operations.FreshWrite(plan, token).ConfigureAwait(false);
            timeline.Record(Web6RecoveryMilestone.FreshWriteAccepted, _operations.Timestamp());
            var snapshots = await _operations.RecoverySnapshots(token).ConfigureAwait(false);
            var rpo = Web6CandidateOracle.CompareRecovery(snapshots.Before, snapshots.After);
            await _operations.DrainRestored(token).ConfigureAwait(false);
            await _operations.CleanupVerified(token).ConfigureAwait(false);
            return timeline.Complete(rpo.LostBusinessRows, rpo.LostJournalRows);
        }
        catch
        {
            _operations.RetainEvidence();
            throw;
        }
    }
}

internal static class Web6CandidateRecoveryAcceptanceGate
{
    private static readonly string[] Required =
    {
        "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING", "MYDMSVN_SQL_TEST_APPROVAL_FILE",
        "MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY", "MYDMSVN_WEB_TEST_TLS_THUMBPRINT",
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
