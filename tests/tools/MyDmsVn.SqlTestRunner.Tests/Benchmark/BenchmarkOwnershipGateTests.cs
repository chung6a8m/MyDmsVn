using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness.Approval;
using MyDmsVn.SqlTestHarness.Ownership;
using MyDmsVn.SqlTestRunner.Benchmark;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Benchmark;

public sealed class BenchmarkOwnershipGateTests
{
    [Fact]
    public async Task Created_live_database_is_incomplete_and_recovery_artifact_is_sanitized()
    {
        using var fixture = new Fixture();
        var record = await fixture.WriteRecordAsync(DatabaseOwnershipState.Created);
        fixture.Reader.Results[record.DatabaseName] = new LiveDatabaseOwnership(
            record.DatabaseId!.Value,
            record.DatabaseCreateDateUtc!.Value,
            new DatabaseOwnershipMarker(record.RunId, record.OwnershipToken));

        var result = await fixture.ValidateAsync();

        Assert.False(result.IsComplete);
        Assert.Equal(fixture.RecoveryPath, result.RecoveryArtifactPath);
        var artifact = File.ReadAllText(fixture.RecoveryPath);
        Assert.Contains("Created", artifact, StringComparison.Ordinal);
        Assert.DoesNotContain(record.DatabaseName, artifact, StringComparison.Ordinal);
        Assert.DoesNotContain(record.OwnershipToken, artifact, StringComparison.Ordinal);
        Assert.DoesNotContain(record.InstanceFingerprintSha256, artifact, StringComparison.Ordinal);
        Assert.DoesNotContain("review-server", artifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", artifact, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Disposed_record_with_absent_database_is_complete()
    {
        using var fixture = new Fixture();
        await fixture.WriteRecordAsync(DatabaseOwnershipState.Disposed);

        var result = await fixture.ValidateAsync();

        Assert.True(result.IsComplete);
        Assert.Null(result.RecoveryArtifactPath);
        Assert.False(File.Exists(fixture.RecoveryPath));
    }

    [Theory]
    [InlineData(DatabaseOwnershipState.CreateIntent)]
    [InlineData(DatabaseOwnershipState.Created)]
    [InlineData(DatabaseOwnershipState.DropIntent)]
    [InlineData(DatabaseOwnershipState.CleanupFailed)]
    [InlineData(DatabaseOwnershipState.Quarantined)]
    public async Task Unresolved_state_is_incomplete_even_when_database_is_absent(
        DatabaseOwnershipState state)
    {
        using var fixture = new Fixture();
        await fixture.WriteRecordAsync(state);

        var result = await fixture.ValidateAsync();

        Assert.False(result.IsComplete);
        Assert.True(File.Exists(fixture.RecoveryPath));
    }

    [Fact]
    public async Task Empty_journal_is_incomplete()
    {
        using var fixture = new Fixture();

        var result = await fixture.ValidateAsync();

        Assert.False(result.IsComplete);
        Assert.True(File.Exists(fixture.RecoveryPath));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "MyDmsVn.BenchmarkOwnership",
            Guid.NewGuid().ToString("N"));
        private readonly SqlInstanceApproval _approval;
        private readonly FileDatabaseOwnershipJournal _journal;

        public Fixture()
        {
            Directory.CreateDirectory(_root);
            JournalDirectory = Path.Combine(_root, "journal");
            RecoveryPath = Path.Combine(_root, "recovery.json");
            ApprovalPath = Path.Combine(_root, "approval.json");
            var identity = new SqlInstanceIdentity("review-server", "review-machine", "review-instance", null);
            var fingerprint = SqlInstanceFingerprint.Create(identity);
            File.WriteAllText(
                ApprovalPath,
                "{\"schemaVersion\":1,\"approvalId\":\"approval-1\"," +
                "\"nonProductionAttestation\":true,\"identity\":{" +
                "\"serverName\":\"review-server\",\"machineName\":\"review-machine\"," +
                "\"instanceName\":\"review-instance\",\"endpoint\":null}," +
                "\"fingerprintSha256\":\"" + fingerprint + "\"}");
            _approval = SqlInstanceApprovalLoader.Load(ApprovalPath);
            _journal = new FileDatabaseOwnershipJournal(JournalDirectory);
        }

        public string JournalDirectory { get; }

        public string RecoveryPath { get; }

        public string ApprovalPath { get; }

        public FakeLiveOwnershipReader Reader { get; } = new FakeLiveOwnershipReader();

        public async Task<DatabaseOwnershipRecord> WriteRecordAsync(DatabaseOwnershipState target)
        {
            var record = new DatabaseOwnershipRecord(
                "execution-1",
                "operation-sensitive",
                "MyDmsVn_Test_0123456789abcdef0123456789abcdef",
                "ownership-secret",
                _approval.FingerprintSha256,
                _approval.ApprovalId,
                DatabaseOwnershipState.CreateIntent,
                DateTimeOffset.UtcNow,
                null,
                null);
            await _journal.WriteCreateIntentAsync(record);
            if (target == DatabaseOwnershipState.CreateIntent)
            {
                return record;
            }

            if (target == DatabaseOwnershipState.CleanupFailed ||
                target == DatabaseOwnershipState.Quarantined)
            {
                return await _journal.TransitionAsync(record, target);
            }

            var createdUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            record = await _journal.TransitionAsync(
                record,
                DatabaseOwnershipState.Created,
                42,
                createdUtc);
            if (target == DatabaseOwnershipState.Created)
            {
                return record;
            }

            record = await _journal.TransitionAsync(record, DatabaseOwnershipState.DropIntent);
            return target == DatabaseOwnershipState.DropIntent
                ? record
                : await _journal.TransitionAsync(record, DatabaseOwnershipState.Disposed);
        }

        public Task<BenchmarkOwnershipResult> ValidateAsync() =>
            new BenchmarkOwnershipGate(() => Reader).ValidateAsync(
                new BenchmarkOwnershipEvidence(
                    "execution-1",
                    JournalDirectory,
                    RecoveryPath,
                    ApprovalPath,
                    ProcessSucceeded: true),
                CancellationToken.None);

        public void Dispose() => Directory.Delete(_root, true);
    }

    private sealed class FakeLiveOwnershipReader : IBenchmarkLiveOwnershipReader
    {
        public Dictionary<string, LiveDatabaseOwnership?> Results { get; } =
            new Dictionary<string, LiveDatabaseOwnership?>(StringComparer.Ordinal);

        public Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<LiveDatabaseOwnership?> ReadAsync(
            string databaseName,
            CancellationToken cancellationToken)
        {
            Results.TryGetValue(databaseName, out var result);
            return Task.FromResult(result);
        }

        public void Dispose()
        {
        }
    }
}
