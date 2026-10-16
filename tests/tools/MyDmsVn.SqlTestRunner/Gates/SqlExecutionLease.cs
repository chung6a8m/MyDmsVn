using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.SqlTestRunner.Gates;

public enum SqlExecutionLeaseAcquireStatus
{
    Acquired,
    TimedOut,
    Cancelled,
    Recovered,
    BlockedRecovery,
}

public sealed record SqlExecutionLeaseRequest(
    string ApprovedInstanceFingerprint,
    string GateIdentity,
    string PrivateArtifactRoot,
    TimeSpan ResourceTimeout,
    TimeSpan RecoveryTimeout,
    IAbandonedSqlRunRecovery Recovery);

public sealed record SqlExecutionLeaseAcquireResult(
    SqlExecutionLeaseAcquireStatus Status,
    SqlExecutionLease? Lease);

public sealed record AbandonedSqlRun(
    string ResourceKey,
    int OwningProcessId,
    string InvocationId,
    DateTimeOffset StartedAtUtc,
    string PrivateArtifactRoot,
    string GateIdentity);

public interface IAbandonedSqlRunRecovery
{
    Task<bool> ReconcileAsync(AbandonedSqlRun lease, CancellationToken cancellationToken);
}

public interface ISqlExecutionLeaseFactory
{
    Task<SqlExecutionLeaseAcquireResult> AcquireAsync(
        SqlExecutionLeaseRequest request,
        CancellationToken cancellationToken);
}

public interface IProcessLivenessProbe
{
    bool IsLive(int processId, DateTimeOffset leaseStartedAtUtc);
}

public sealed record SqlExecutionLeaseGuardResult<T>(bool Acquired, T Result);

public sealed class SqlExecutionLeaseGuard
{
    private readonly ITestGateApprovalReader _approval;
    private readonly ISqlExecutionLeaseFactory _leases;

    public SqlExecutionLeaseGuard(
        ITestGateApprovalReader? approval = null,
        ISqlExecutionLeaseFactory? leases = null)
    {
        _approval = approval ?? new TestGateApprovalReader();
        _leases = leases ?? new SqlExecutionLeaseFactory();
    }

    public async Task<SqlExecutionLeaseGuardResult<T>> RunAsync<T>(
        string approvalFile,
        string gateIdentity,
        string artifactRoot,
        TimeSpan timeout,
        Func<CancellationToken, Task<T>> operation,
        Func<T, bool> canReleaseSafely,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(canReleaseSafely);
        if (!_approval.TryLoad(approvalFile, out var fingerprint))
        {
            return new SqlExecutionLeaseGuardResult<T>(false, default!);
        }

        var acquired = await _leases.AcquireAsync(
            new SqlExecutionLeaseRequest(
                fingerprint,
                gateIdentity,
                artifactRoot,
                timeout,
                timeout,
                new BenchmarkAbandonedSqlRunRecovery(approvalFile)),
            cancellationToken).ConfigureAwait(false);
        if (acquired.Lease is null)
        {
            return new SqlExecutionLeaseGuardResult<T>(false, default!);
        }

        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);
            if (canReleaseSafely(result))
            {
                acquired.Lease.Complete();
            }

            return new SqlExecutionLeaseGuardResult<T>(true, result);
        }
        finally
        {
            await acquired.Lease.DisposeAsync().ConfigureAwait(false);
        }
    }
}

public sealed class SqlExecutionLease : IAsyncDisposable
{
    private readonly EventWaitHandle _release;
    private readonly Task _completion;
    private int _disposed;
    private int _completed;

    internal SqlExecutionLease(EventWaitHandle release, Task completion)
    {
        _release = release;
        _completion = completion;
    }

    public void Complete()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(SqlExecutionLease));
        }

        Interlocked.Exchange(ref _completed, 1);
    }

    internal bool IsCompleted => Volatile.Read(ref _completed) != 0;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _release.Set();
        await _completion.ConfigureAwait(false);
        _release.Dispose();
    }
}

public sealed class SqlExecutionLeaseFactory : ISqlExecutionLeaseFactory
{
    private const string MutexPrefix = "Global\\MyDmsVn.TestGate.Sql.";
    private readonly FileSqlExecutionLeaseStore _store;
    private readonly IProcessLivenessProbe _processes;

    public SqlExecutionLeaseFactory()
        : this(new FileSqlExecutionLeaseStore(DefaultStoreRoot))
    {
    }

    public SqlExecutionLeaseFactory(
        FileSqlExecutionLeaseStore store,
        IProcessLivenessProbe? processes = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _processes = processes ?? new SystemProcessLivenessProbe();
    }

    public Task<SqlExecutionLeaseAcquireResult> AcquireAsync(
        SqlExecutionLeaseRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var completion = new TaskCompletionSource<SqlExecutionLeaseAcquireResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var resourceKey = Hash(request.ApprovedInstanceFingerprint);
        var holder = new Thread(() => Hold(
            resourceKey,
            request,
            cancellationToken,
            completion))
        {
            IsBackground = true,
            Name = "MyDmsVn SQL test-gate lease",
        };
        holder.Start();
        return completion.Task;
    }

    private void Hold(
        string resourceKey,
        SqlExecutionLeaseRequest request,
        CancellationToken cancellationToken,
        TaskCompletionSource<SqlExecutionLeaseAcquireResult> acquisition)
    {
        using var release = new EventWaitHandle(false, EventResetMode.ManualReset);
        Mutex? mutex = null;
        var acquired = false;
        try
        {
            mutex = new Mutex(initiallyOwned: false, MutexPrefix + resourceKey);
            var timeoutMilliseconds = checked((int)Math.Min(
                request.ResourceTimeout.TotalMilliseconds,
                int.MaxValue));
            int signalled;
            try
            {
                signalled = WaitHandle.WaitAny(
                    new WaitHandle[] { mutex, cancellationToken.WaitHandle },
                    timeoutMilliseconds);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
                var recovery = Recover(resourceKey, request, cancellationToken, requireRecord: true);
                if (recovery.Status is SqlExecutionLeaseAcquireStatus.BlockedRecovery or
                    SqlExecutionLeaseAcquireStatus.Cancelled)
                {
                    acquisition.TrySetResult(new SqlExecutionLeaseAcquireResult(
                        recovery.Status,
                        null));
                    Drain(recovery.PendingTask);
                    return;
                }

                HoldAcquired(
                    resourceKey,
                    request,
                    release,
                    acquisition,
                    SqlExecutionLeaseAcquireStatus.Recovered);
                return;
            }

            if (signalled == WaitHandle.WaitTimeout)
            {
                acquisition.TrySetResult(new SqlExecutionLeaseAcquireResult(
                    SqlExecutionLeaseAcquireStatus.TimedOut,
                    null));
                return;
            }

            if (signalled == 1)
            {
                acquisition.TrySetResult(new SqlExecutionLeaseAcquireResult(
                    SqlExecutionLeaseAcquireStatus.Cancelled,
                    null));
                return;
            }

            acquired = true;
            var prior = Recover(resourceKey, request, cancellationToken, requireRecord: false);
            if (prior.Status is SqlExecutionLeaseAcquireStatus.BlockedRecovery or
                SqlExecutionLeaseAcquireStatus.Cancelled)
            {
                acquisition.TrySetResult(new SqlExecutionLeaseAcquireResult(prior.Status, null));
                Drain(prior.PendingTask);
                return;
            }

            HoldAcquired(
                resourceKey,
                request,
                release,
                acquisition,
                prior.Status);
        }
        catch (UnauthorizedAccessException)
        {
            acquisition.TrySetResult(new SqlExecutionLeaseAcquireResult(
                SqlExecutionLeaseAcquireStatus.BlockedRecovery,
                null));
        }
        catch (Exception exception)
        {
            acquisition.TrySetException(exception);
        }
        finally
        {
            if (acquired)
            {
                mutex!.ReleaseMutex();
            }

            mutex?.Dispose();
        }
    }

    private RecoveryOutcome Recover(
        string resourceKey,
        SqlExecutionLeaseRequest request,
        CancellationToken cancellationToken,
        bool requireRecord)
    {
        var readStatus = _store.Read(resourceKey, out var record);
        if (readStatus == SqlExecutionLeaseReadStatus.Invalid)
        {
            return RecoveryOutcome.Blocked;
        }

        if (readStatus == SqlExecutionLeaseReadStatus.Missing)
        {
            return new RecoveryOutcome(
                requireRecord
                    ? SqlExecutionLeaseAcquireStatus.BlockedRecovery
                    : SqlExecutionLeaseAcquireStatus.Acquired,
                null);
        }

        if (!record.IsValidFor(resourceKey))
        {
            return RecoveryOutcome.Blocked;
        }

        if (record.CompletionState is SqlExecutionLeaseCompletionState.Completed or
            SqlExecutionLeaseCompletionState.Recovered)
        {
            return RecoveryOutcome.Acquired;
        }

        if (!Directory.Exists(record.PrivateArtifactRoot))
        {
            return RecoveryOutcome.Blocked;
        }

        if (record.CompletionState == SqlExecutionLeaseCompletionState.Active &&
            IsLiveOrUnknown(record.OwningProcessId, record.StartedAtUtc))
        {
            return RecoveryOutcome.Blocked;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var recoveryDeadline = new CancellationTokenSource(request.RecoveryTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                recoveryDeadline.Token);
            var abandoned = new AbandonedSqlRun(
                record.ResourceKey,
                record.OwningProcessId,
                record.InvocationId,
                record.StartedAtUtc,
                record.PrivateArtifactRoot,
                record.GateIdentity);
            var recoveryTask = Task.Run(
                async () => await request.Recovery.ReconcileAsync(abandoned, linked.Token)
                    .ConfigureAwait(false),
                CancellationToken.None);
            var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, linked.Token);
            var completed = Task.WhenAny(recoveryTask, cancellationTask)
                .GetAwaiter()
                .GetResult();
            if (completed != recoveryTask)
            {
                return new RecoveryOutcome(
                    cancellationToken.IsCancellationRequested
                        ? SqlExecutionLeaseAcquireStatus.Cancelled
                        : SqlExecutionLeaseAcquireStatus.BlockedRecovery,
                    recoveryTask);
            }

            var recovered = recoveryTask.GetAwaiter().GetResult();
            if (!recovered)
            {
                return RecoveryOutcome.Blocked;
            }

            _store.MarkRecovered(record);
            return RecoveryOutcome.Recovered;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return RecoveryOutcome.Cancelled;
        }
        catch (OperationCanceledException)
        {
            return RecoveryOutcome.Blocked;
        }
    }

    private static void Drain(Task? pendingTask)
    {
        if (pendingTask is null)
        {
            return;
        }

        try
        {
            pendingTask.GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // The acquisition result is already fail-closed. The mutex remains held until this task ends.
        }
    }

    private bool IsLiveOrUnknown(int processId, DateTimeOffset leaseStartedAtUtc)
    {
        try
        {
            return _processes.IsLive(processId, leaseStartedAtUtc);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException ||
            exception is System.ComponentModel.Win32Exception ||
            exception is NotSupportedException)
        {
            return true;
        }
    }

    private void HoldAcquired(
        string resourceKey,
        SqlExecutionLeaseRequest request,
        EventWaitHandle release,
        TaskCompletionSource<SqlExecutionLeaseAcquireResult> acquisition,
        SqlExecutionLeaseAcquireStatus status)
    {
        Directory.CreateDirectory(request.PrivateArtifactRoot);
        var record = SqlExecutionLeaseRecord.Active(
            resourceKey,
            request.GateIdentity,
            request.PrivateArtifactRoot);
        _store.Write(record);
        var holderCompletion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = new SqlExecutionLease(release, holderCompletion.Task);
        acquisition.TrySetResult(new SqlExecutionLeaseAcquireResult(status, lease));
        release.WaitOne();
        try
        {
            if (lease.IsCompleted)
            {
                _store.MarkCompleted(record);
            }
            else
            {
                _store.MarkRecoveryRequired(record);
            }
            holderCompletion.TrySetResult(null);
        }
        catch (Exception exception)
        {
            holderCompletion.TrySetException(exception);
        }
    }

    private static void Validate(SqlExecutionLeaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Recovery);
        if (string.IsNullOrWhiteSpace(request.ApprovedInstanceFingerprint) ||
            string.IsNullOrWhiteSpace(request.GateIdentity) ||
            string.IsNullOrWhiteSpace(request.PrivateArtifactRoot) ||
            request.ResourceTimeout <= TimeSpan.Zero ||
            request.RecoveryTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("A complete positive SQL lease request is required.", nameof(request));
        }
    }

    internal static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    internal static string MutexName(string fingerprint) => MutexPrefix + Hash(fingerprint);

    internal static string DefaultStoreRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "MyDmsVn",
        "TestGate",
        "leases");

    private sealed record RecoveryOutcome(
        SqlExecutionLeaseAcquireStatus Status,
        Task? PendingTask)
    {
        public static RecoveryOutcome Acquired { get; } =
            new RecoveryOutcome(SqlExecutionLeaseAcquireStatus.Acquired, null);

        public static RecoveryOutcome Recovered { get; } =
            new RecoveryOutcome(SqlExecutionLeaseAcquireStatus.Recovered, null);

        public static RecoveryOutcome Blocked { get; } =
            new RecoveryOutcome(SqlExecutionLeaseAcquireStatus.BlockedRecovery, null);

        public static RecoveryOutcome Cancelled { get; } =
            new RecoveryOutcome(SqlExecutionLeaseAcquireStatus.Cancelled, null);
    }
}

internal sealed class SystemProcessLivenessProbe : IProcessLivenessProbe
{
    public bool IsLive(int processId, DateTimeOffset leaseStartedAtUtc)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited &&
                new DateTimeOffset(process.StartTime).ToUniversalTime() <= leaseStartedAtUtc;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public sealed class FileSqlExecutionLeaseStore
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
    private readonly string _root;
    private readonly Func<string, string> _readAllText;

    public FileSqlExecutionLeaseStore(string root)
        : this(root, File.ReadAllText)
    {
    }

    internal FileSqlExecutionLeaseStore(string root, Func<string, string> readAllText)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("A private lease-store root is required.", nameof(root));
        }

        _root = Path.GetFullPath(root);
        _readAllText = readAllText ?? throw new ArgumentNullException(nameof(readAllText));
        Directory.CreateDirectory(_root);
    }

    internal void Write(SqlExecutionLeaseRecord record, string? pathResourceKey = null) =>
        WriteAtomic(PathFor(pathResourceKey ?? record.ResourceKey), record);

    internal SqlExecutionLeaseReadStatus Read(string resourceKey, out SqlExecutionLeaseRecord record)
    {
        try
        {
            var path = PathFor(resourceKey);
            record = JsonSerializer.Deserialize<SqlExecutionLeaseRecord>(
                    _readAllText(path),
                    JsonOptions) ??
                throw new InvalidDataException("The SQL lease record is empty.");
            return SqlExecutionLeaseReadStatus.Valid;
        }
        catch (Exception exception) when (
            exception is FileNotFoundException ||
            exception is DirectoryNotFoundException)
        {
            record = null!;
            return SqlExecutionLeaseReadStatus.Missing;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is JsonException ||
            exception is InvalidDataException)
        {
            record = null!;
            return SqlExecutionLeaseReadStatus.Invalid;
        }
    }

    internal void MarkCompleted(SqlExecutionLeaseRecord record) => WriteAtomic(
        PathFor(record.ResourceKey),
        record with { CompletionState = SqlExecutionLeaseCompletionState.Completed });

    internal void MarkRecovered(SqlExecutionLeaseRecord record) => WriteAtomic(
        PathFor(record.ResourceKey),
        record with { CompletionState = SqlExecutionLeaseCompletionState.Recovered });

    internal void MarkRecoveryRequired(SqlExecutionLeaseRecord record) => WriteAtomic(
        PathFor(record.ResourceKey),
        record with { CompletionState = SqlExecutionLeaseCompletionState.RecoveryRequired });

    internal string PathFor(string resourceKey) => Path.Combine(_root, resourceKey + ".lease.json");

    private static void WriteAtomic(string path, SqlExecutionLeaseRecord record)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(record, JsonOptions), new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}

internal enum SqlExecutionLeaseReadStatus
{
    Missing,
    Valid,
    Invalid,
}

internal enum SqlExecutionLeaseCompletionState
{
    Active,
    Completed,
    Recovered,
    RecoveryRequired,
}

internal sealed record SqlExecutionLeaseRecord(
    int SchemaVersion,
    string ResourceKey,
    int OwningProcessId,
    string InvocationId,
    DateTimeOffset StartedAtUtc,
    string PrivateArtifactRoot,
    string GateIdentity,
    SqlExecutionLeaseCompletionState CompletionState)
{
    public bool IsValidFor(string resourceKey) =>
        SchemaVersion == 1 &&
        string.Equals(ResourceKey, resourceKey, StringComparison.Ordinal) &&
        ResourceKey.Length == 64 &&
        OwningProcessId > 0 &&
        !string.IsNullOrWhiteSpace(InvocationId) &&
        StartedAtUtc <= DateTimeOffset.UtcNow &&
        Path.IsPathFullyQualified(PrivateArtifactRoot) &&
        !string.IsNullOrWhiteSpace(GateIdentity) &&
        Enum.IsDefined(CompletionState);

    public static SqlExecutionLeaseRecord Active(
        string resourceKey,
        string gateIdentity,
        string privateArtifactRoot) => new SqlExecutionLeaseRecord(
            1,
            resourceKey,
            Environment.ProcessId,
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            Path.GetFullPath(privateArtifactRoot),
            gateIdentity,
            SqlExecutionLeaseCompletionState.Active);
}
