using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class SqlFixtureTestScope
{
    private readonly object _sync = new object();
    private readonly string _connectionString;
    private readonly Func<bool, Task> _closed;
    private readonly TimeSpan _cleanupTimeout;
    private readonly List<Func<Task>> _cleanup = new List<Func<Task>>();
    private readonly List<(Task Task, Action Stop)> _workers = new List<(Task, Action)>();
    private Task? _disposal;
    private bool _disposing;

    internal SqlFixtureTestScope(string caseLabel, string connectionString, Func<bool, Task> closed, TimeSpan? cleanupTimeout = null)
    {
        CaseLabel = caseLabel;
        _connectionString = connectionString;
        _closed = closed;
        _cleanupTimeout = ValidateCleanupTimeout(cleanupTimeout);
    }

    public string CaseLabel { get; }
    internal TimeSpan CleanupElapsed { get; private set; }
    internal bool IsDrained { get; private set; }

    internal static TimeSpan ValidateCleanupTimeout(TimeSpan? timeout)
    {
        var value = timeout ?? TimeSpan.FromMinutes(2);
        if (value <= TimeSpan.Zero || value.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        return value;
    }
    public string ConnectionString
    {
        get { lock (_sync) { ThrowIfDisposing(); return _connectionString; } }
    }

    public void RegisterCleanup(Func<Task> cleanup)
    {
        if (cleanup is null) { throw new ArgumentNullException(nameof(cleanup)); }
        lock (_sync) { ThrowIfDisposing(); _cleanup.Add(cleanup); }
    }

    public void RegisterWorker(Task worker, Action requestStop)
    {
        if (worker is null) { throw new ArgumentNullException(nameof(worker)); }
        if (requestStop is null) { throw new ArgumentNullException(nameof(requestStop)); }
        lock (_sync) { ThrowIfDisposing(); _workers.Add((worker, requestStop)); }
    }

    public Task DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposal != null) { return _disposal; }
            _disposing = true;
            return _disposal = DisposeCoreAsync();
        }
    }

    private async Task DisposeCoreAsync()
    {
        var watch = Stopwatch.StartNew();
        var errors = new List<Exception>();
        // Invoke callbacks off-thread too: a synchronous stop/cleanup callback can itself block.
        var workers = _workers.Select(worker => Task.Run(worker.Stop))
            .Concat(_workers.Select(worker => worker.Task)).ToArray();
        var drained = true;
        foreach (var operation in workers)
        {
            if (!await TryDrainAsync(operation, watch, errors).ConfigureAwait(false)) { drained = false; break; }
        }
        if (!drained)
        {
            foreach (var operation in workers) { ObserveLateFailure(operation); }
        }
        else
        {
            for (var index = _cleanup.Count - 1; index >= 0; index--)
            {
                var cleanup = _cleanup[index];
                if (!await TryDrainAsync(Task.Run(cleanup), watch, errors).ConfigureAwait(false))
                {
                    drained = false;
                    break; // Later cleanup may conflict with an operation that is still active.
                }
            }
        }
        IsDrained = drained;
        CleanupElapsed = watch.Elapsed;
        try { await DrainAsync(_closed(errors.Count == 0), errors).ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count > 1) { throw new AggregateException("SQL scope cleanup failed.", errors); }
        if (errors.Count == 1) { ExceptionDispatchInfo.Capture(errors[0]).Throw(); }
    }

    private async Task<bool> TryDrainAsync(Task operation, Stopwatch watch, List<Exception> errors)
    {
        if (!operation.IsCompleted)
        {
            var remaining = _cleanupTimeout - watch.Elapsed;
            using var delay = new CancellationTokenSource();
            if (remaining <= TimeSpan.Zero ||
                await Task.WhenAny(operation, Task.Delay(remaining, delay.Token)).ConfigureAwait(false) != operation)
            {
                errors.Add(new TimeoutException("SQL scope cleanup exceeded its cooperative drain budget; outstanding work requires quarantine."));
                ObserveLateFailure(operation);
                return false;
            }
            delay.Cancel();
        }
        await DrainAsync(operation, errors).ConfigureAwait(false);
        return true;
    }

    private static void ObserveLateFailure(Task operation) => operation.ContinueWith(
        completed => { _ = completed.Exception; }, CancellationToken.None,
        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private static async Task DrainAsync(Task operation, List<Exception> errors)
    {
        try { await operation.ConfigureAwait(false); }
        catch (Exception error)
        {
            // Await unwraps only one Task.WhenAll failure; retain every tracked failure.
            var failures = operation.Exception;
            if (failures != null) { errors.AddRange(failures.InnerExceptions); }
            else { errors.Add(error); }
        }
    }

    private void ThrowIfDisposing()
    {
        if (_disposing) { throw new ObjectDisposedException(nameof(SqlFixtureTestScope)); }
    }
}
