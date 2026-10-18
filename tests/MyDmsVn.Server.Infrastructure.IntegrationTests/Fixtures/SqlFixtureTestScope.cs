using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class SqlFixtureTestScope
{
    private readonly object _sync = new object();
    private readonly string _connectionString;
    private readonly Func<bool, Task> _closed;
    private readonly List<Func<Task>> _cleanup = new List<Func<Task>>();
    private readonly List<(Task Task, Action Stop)> _workers = new List<(Task, Action)>();
    private Task? _disposal;
    private bool _disposing;

    internal SqlFixtureTestScope(string caseLabel, string connectionString, Func<bool, Task> closed)
    {
        CaseLabel = caseLabel;
        _connectionString = connectionString;
        _closed = closed;
    }

    public string CaseLabel { get; }
    internal TimeSpan CleanupElapsed { get; private set; }
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
        foreach (var worker in _workers)
        {
            try { worker.Stop(); } catch (Exception error) { errors.Add(error); }
        }
        foreach (var worker in _workers)
        {
            await DrainAsync(worker.Task, errors).ConfigureAwait(false);
        }
        for (var index = _cleanup.Count - 1; index >= 0; index--)
        {
            try { await DrainAsync(_cleanup[index](), errors).ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); }
        }
        CleanupElapsed = watch.Elapsed;
        try { await DrainAsync(_closed(errors.Count == 0), errors).ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count > 1) { throw new AggregateException("SQL scope cleanup failed.", errors); }
        if (errors.Count == 1) { ExceptionDispatchInfo.Capture(errors[0]).Throw(); }
    }

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
