using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal static class SqlConcurrencyCleanup
{
    private static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(15);

    public static async Task CompleteAsync(
        SqlConcurrencyGate? gate,
        CancellationTokenSource cancellation,
        Func<Task> guardedCleanup,
        params Task?[] startedTasks) =>
        await CompleteAsync(
                gate == null ? null : () => gate.ReleaseAsync(CancellationToken.None),
                gate == null ? null : new Action(gate.Dispose),
                cancellation,
                guardedCleanup,
                DefaultDrainTimeout,
                startedTasks)
            .ConfigureAwait(false);

    internal static async Task CompleteAsync(
        Func<Task>? release,
        Action? closeController,
        CancellationTokenSource cancellation,
        Func<Task> guardedCleanup,
        TimeSpan drainTimeout,
        params Task?[] startedTasks)
    {
        var errors = new List<Exception>();
        if (release != null)
        {
            await CaptureAsync(release, errors).ConfigureAwait(false);
        }

        Capture(cancellation.Cancel, errors);
        if (closeController != null)
        {
            Capture(closeController, errors);
        }

        var tasks = startedTasks.Where(task => task != null).Cast<Task>().ToArray();
        var all = Task.WhenAll(tasks);
        if (!await TryDrainAsync(all, drainTimeout).ConfigureAwait(false))
        {
            throw new SqlConcurrencyCleanupIncompleteException(errors);
        }

        await ObserveAsync(all).ConfigureAwait(false);
        await CaptureAsync(guardedCleanup, errors).ConfigureAwait(false);

        if (errors.Count != 0)
        {
            throw new AggregateException("SQL concurrency scenario cleanup failed.", errors);
        }
    }

    private static async Task<bool> TryDrainAsync(Task all, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(all, Task.Delay(timeout)).ConfigureAwait(false);
        return ReferenceEquals(completed, all);
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Faults are observed here; the original test failure remains authoritative.
        }
    }

    private static async Task CaptureAsync(Func<Task> action, ICollection<Exception> errors)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }
    }

    private static void Capture(Action action, ICollection<Exception> errors)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }
    }
}

internal sealed class SqlConcurrencyCleanupIncompleteException : TimeoutException
{
    public SqlConcurrencyCleanupIncompleteException(IReadOnlyCollection<Exception> cleanupErrors)
        : base(
            "SQL concurrency tasks remained active after controller release, cancellation and bounded drain.",
            cleanupErrors.Count == 0
                ? null
                : new AggregateException("Earlier cleanup operations also failed.", cleanupErrors))
    {
    }
}
