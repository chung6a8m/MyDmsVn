using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Gates;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Gates;

public sealed class SqlExecutionLeaseTests
{
    [Fact]
    public async Task Same_fingerprint_contends_with_a_bounded_wait()
    {
        using var store = new TemporaryLeaseStore();
        var factory = Factory(store);
        var first = await factory.AcquireAsync(Request(store, "instance-a"), CancellationToken.None);

        var second = await factory.AcquireAsync(
            Request(store, "instance-a", TimeSpan.FromMilliseconds(100)),
            CancellationToken.None);

        Assert.Equal(SqlExecutionLeaseAcquireStatus.Acquired, first.Status);
        Assert.Equal(SqlExecutionLeaseAcquireStatus.TimedOut, second.Status);
        Assert.Null(second.Lease);
        await first.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task Different_fingerprints_are_independent()
    {
        using var store = new TemporaryLeaseStore();
        var factory = Factory(store);
        var first = await factory.AcquireAsync(Request(store, "instance-a"), CancellationToken.None);
        var second = await factory.AcquireAsync(Request(store, "instance-b"), CancellationToken.None);

        Assert.Equal(SqlExecutionLeaseAcquireStatus.Acquired, first.Status);
        Assert.Equal(SqlExecutionLeaseAcquireStatus.Acquired, second.Status);
        await second.Lease!.DisposeAsync();
        await first.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task Cancellation_stops_waiting_without_releasing_the_owner()
    {
        using var store = new TemporaryLeaseStore();
        var factory = Factory(store);
        var first = await factory.AcquireAsync(Request(store, "instance-a"), CancellationToken.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var cancelled = await factory.AcquireAsync(
            Request(store, "instance-a", TimeSpan.FromSeconds(5)),
            cancellation.Token);

        Assert.Equal(SqlExecutionLeaseAcquireStatus.Cancelled, cancelled.Status);
        await first.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task Durable_lease_uses_only_the_hashed_resource_key()
    {
        using var store = new TemporaryLeaseStore();
        const string fingerprint = "server-with-private-instance-name";
        var acquired = await Factory(store).AcquireAsync(
            Request(store, fingerprint),
            CancellationToken.None);

        var serialized = string.Join(
            "\n",
            Directory.GetFiles(store.Root).Select(File.ReadAllText));

        Assert.Equal(SqlExecutionLeaseAcquireStatus.Acquired, acquired.Status);
        Assert.DoesNotContain(fingerprint, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(fingerprint, Path.GetFileName(Assert.Single(Directory.GetFiles(store.Root))), StringComparison.Ordinal);
        await acquired.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task Normal_disposal_releases_the_resource_for_the_next_process()
    {
        using var store = new TemporaryLeaseStore();
        var factory = Factory(store);
        var first = await factory.AcquireAsync(Request(store, "instance-a"), CancellationToken.None);
        first.Lease!.Complete();
        await first.Lease!.DisposeAsync();

        var second = await factory.AcquireAsync(Request(store, "instance-a"), CancellationToken.None);

        Assert.Equal(SqlExecutionLeaseAcquireStatus.Acquired, second.Status);
        second.Lease!.Complete();
        await second.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task Unresolved_disposal_requires_recovery_before_the_next_acquisition()
    {
        using var store = new TemporaryLeaseStore();
        var events = new List<string>();
        var factory = Factory(store);
        var first = await factory.AcquireAsync(Request(store, "instance-unresolved"), CancellationToken.None);
        await first.Lease!.DisposeAsync();

        var second = await factory.AcquireAsync(
            Request(
                store,
                "instance-unresolved",
                recovery: new RecordingRecovery(events, succeeds: false)),
            CancellationToken.None);

        Assert.Equal(new[] { "recover" }, events);
        Assert.Equal(SqlExecutionLeaseAcquireStatus.BlockedRecovery, second.Status);
        Assert.Null(second.Lease);
    }

    [Fact]
    public async Task Corrupt_existing_record_blocks_an_ordinary_acquisition()
    {
        using var store = new TemporaryLeaseStore();
        const string fingerprint = "corrupt-ordinary-acquisition";
        WritePriorLease(store, fingerprint);
        File.WriteAllText(Assert.Single(Directory.GetFiles(store.Root)), "{not-json");

        var result = await Factory(store).AcquireAsync(
            Request(store, fingerprint),
            CancellationToken.None);

        Assert.Equal(SqlExecutionLeaseAcquireStatus.BlockedRecovery, result.Status);
        Assert.Null(result.Lease);
    }

    [Fact]
    public void Lease_namespace_is_machine_wide()
    {
        Assert.StartsWith("Global\\", SqlExecutionLeaseFactory.MutexName("instance-a"), StringComparison.Ordinal);
        Assert.StartsWith(
            Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
            SqlExecutionLeaseFactory.DefaultStoreRoot,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Abandoned_owner_recovers_before_returning_the_new_lease()
    {
        using var store = new TemporaryLeaseStore();
        var events = new List<string>();
        var recovery = new RecordingRecovery(events, succeeds: true);
        const string fingerprint = "abandoned-recoverable";
        WritePriorLease(store, fingerprint);
        using var abandoned = Abandon(fingerprint);

        var result = await new SqlExecutionLeaseFactory(
            new FileSqlExecutionLeaseStore(store.Root),
            new FixedProcessProbe(isLive: false)).AcquireAsync(
                Request(store, fingerprint, recovery: recovery),
                CancellationToken.None);
        events.Add("acquire");

        Assert.Equal(SqlExecutionLeaseAcquireStatus.Recovered, result.Status);
        Assert.Equal(new[] { "recover", "acquire" }, events);
        await result.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task Missing_corrupt_mismatched_live_or_unresolved_recovery_is_blocked()
    {
        await AssertBlockedRecovery(LeaseDamage.Missing, isLive: false, recoverySucceeds: true);
        await AssertBlockedRecovery(LeaseDamage.Corrupt, isLive: false, recoverySucceeds: true);
        await AssertBlockedRecovery(LeaseDamage.Mismatched, isLive: false, recoverySucceeds: true);
        await AssertBlockedRecovery(LeaseDamage.Valid, isLive: true, recoverySucceeds: true);
        await AssertBlockedRecovery(LeaseDamage.Valid, isLive: false, recoverySucceeds: false);
    }

    [Fact]
    public async Task Cancellation_during_abandoned_owner_recovery_never_acquires()
    {
        using var store = new TemporaryLeaseStore();
        const string fingerprint = "abandoned-cancelled";
        WritePriorLease(store, fingerprint);
        using var abandoned = Abandon(fingerprint);
        using var cancellation = new CancellationTokenSource();
        var recovery = new CancellingRecovery(cancellation);

        var result = await new SqlExecutionLeaseFactory(
            new FileSqlExecutionLeaseStore(store.Root),
            new FixedProcessProbe(isLive: false)).AcquireAsync(
                Request(store, fingerprint, recovery: recovery),
                cancellation.Token);

        Assert.Equal(SqlExecutionLeaseAcquireStatus.Cancelled, result.Status);
        Assert.Null(result.Lease);
    }

    [Fact]
    public async Task Shared_SQL_entry_guard_serializes_operations_for_the_same_approved_instance()
    {
        using var store = new TemporaryLeaseStore();
        var factory = Factory(store);
        var guard = new SqlExecutionLeaseGuard(new FixedApproval("shared-instance"), factory);
        var entered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = guard.RunAsync(
            "approval.json",
            "legacy/pr-acceptance",
            Path.Combine(store.Root, "first"),
            TimeSpan.FromSeconds(2),
            async cancellationToken =>
            {
                entered.SetResult(null);
                await release.Task.WaitAsync(cancellationToken);
                return true;
            },
            result => result,
            CancellationToken.None);
        await entered.Task;

        var second = await guard.RunAsync(
            "approval.json",
            "legacy/focused-run",
            Path.Combine(store.Root, "second"),
            TimeSpan.FromMilliseconds(100),
            _ => Task.FromResult(true),
            result => result,
            CancellationToken.None);
        release.SetResult(null);
        var completed = await first;

        Assert.False(second.Acquired);
        Assert.True(completed.Acquired);
        Assert.True(completed.Result);
    }

    private static SqlExecutionLeaseFactory Factory(TemporaryLeaseStore store) =>
        new SqlExecutionLeaseFactory(new FileSqlExecutionLeaseStore(store.Root));

    private static SqlExecutionLeaseRequest Request(
        TemporaryLeaseStore store,
        string fingerprint,
        TimeSpan? timeout = null,
        IAbandonedSqlRunRecovery? recovery = null) => new SqlExecutionLeaseRequest(
            fingerprint,
            "20261016-001/TESTGATE-T03/Final",
            Path.Combine(store.Root, "private-artifacts"),
            timeout ?? TimeSpan.FromSeconds(2),
            recovery ?? new SuccessfulRecovery());

    private static async Task AssertBlockedRecovery(
        LeaseDamage damage,
        bool isLive,
        bool recoverySucceeds)
    {
        using var store = new TemporaryLeaseStore();
        var fingerprint = "blocked-" + Guid.NewGuid().ToString("N");
        if (damage != LeaseDamage.Missing)
        {
            WritePriorLease(store, fingerprint, damage == LeaseDamage.Mismatched
                ? "different-resource-key"
                : null);
        }

        if (damage == LeaseDamage.Corrupt)
        {
            File.WriteAllText(Assert.Single(Directory.GetFiles(store.Root)), "{not-json");
        }

        using var abandoned = Abandon(fingerprint);
        var result = await new SqlExecutionLeaseFactory(
            new FileSqlExecutionLeaseStore(store.Root),
            new FixedProcessProbe(isLive)).AcquireAsync(
                Request(
                    store,
                    fingerprint,
                    recovery: new RecordingRecovery(new List<string>(), recoverySucceeds)),
                CancellationToken.None);

        Assert.Equal(SqlExecutionLeaseAcquireStatus.BlockedRecovery, result.Status);
        Assert.Null(result.Lease);
    }

    private static void WritePriorLease(
        TemporaryLeaseStore store,
        string fingerprint,
        string? resourceKey = null)
    {
        var artifactRoot = Path.Combine(store.Root, "prior-artifacts");
        Directory.CreateDirectory(artifactRoot);
        var expectedKey = SqlExecutionLeaseFactory.Hash(fingerprint);
        new FileSqlExecutionLeaseStore(store.Root).Write(new SqlExecutionLeaseRecord(
            1,
            resourceKey ?? expectedKey,
            12345,
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            artifactRoot,
            "20261016-001/TESTGATE-T03/Final",
            SqlExecutionLeaseCompletionState.Active),
            pathResourceKey: expectedKey);
    }

    private static Mutex Abandon(string fingerprint)
    {
        Mutex? abandoned = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            abandoned = new Mutex(false, SqlExecutionLeaseFactory.MutexName(fingerprint));
            abandoned.WaitOne();
            ready.Set();
        });
        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        return abandoned!;
    }

    private sealed class SuccessfulRecovery : IAbandonedSqlRunRecovery
    {
        public Task<bool> ReconcileAsync(AbandonedSqlRun lease, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class RecordingRecovery : IAbandonedSqlRunRecovery
    {
        private readonly IList<string> _events;
        private readonly bool _succeeds;

        public RecordingRecovery(IList<string> events, bool succeeds)
        {
            _events = events;
            _succeeds = succeeds;
        }

        public Task<bool> ReconcileAsync(AbandonedSqlRun lease, CancellationToken cancellationToken)
        {
            _events.Add("recover");
            return Task.FromResult(_succeeds);
        }
    }

    private sealed class CancellingRecovery : IAbandonedSqlRunRecovery
    {
        private readonly CancellationTokenSource _cancellation;

        public CancellingRecovery(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public Task<bool> ReconcileAsync(AbandonedSqlRun lease, CancellationToken cancellationToken)
        {
            _cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }
    }

    private sealed class FixedProcessProbe : IProcessLivenessProbe
    {
        private readonly bool _isLive;

        public FixedProcessProbe(bool isLive)
        {
            _isLive = isLive;
        }

        public bool IsLive(int processId) => _isLive;
    }

    private sealed class FixedApproval : ITestGateApprovalReader
    {
        private readonly string _fingerprint;

        public FixedApproval(string fingerprint) => _fingerprint = fingerprint;

        public bool TryLoad(string path, out string fingerprint)
        {
            fingerprint = _fingerprint;
            return true;
        }
    }

    private enum LeaseDamage
    {
        Valid,
        Missing,
        Corrupt,
        Mismatched,
    }

    private sealed class TemporaryLeaseStore : IDisposable
    {
        public TemporaryLeaseStore()
        {
            Root = Path.Combine(Path.GetTempPath(), "mydms-testgate-lease-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
