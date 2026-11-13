using System;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Testing.Sql;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Explicit monotonic deadline. Children share the clock and cannot extend their parent.
internal sealed class Web6CutoverDeadline
{
    private readonly Func<TimeSpan> _clock;
    private readonly TimeSpan _end;
    internal Web6CutoverDeadline(TimeSpan allowance, Func<TimeSpan>? clock = null)
    {
        if (allowance <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(allowance));
        if (clock == null) { var watch = Stopwatch.StartNew(); clock = () => watch.Elapsed; }
        _clock = clock;
        _end = _clock() + allowance;
    }
    private Web6CutoverDeadline(Func<TimeSpan> clock, TimeSpan end) { _clock = clock; _end = end; }
    internal TimeSpan Remaining
    {
        get { var remaining = _end - _clock(); return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero; }
    }
    internal Web6CutoverDeadline Clamp(TimeSpan cap)
    {
        if (cap <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cap));
        var end = _clock() + cap;
        return new Web6CutoverDeadline(_clock, end < _end ? end : _end);
    }
    internal void ThrowIfExpired(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Remaining <= TimeSpan.Zero) throw new TimeoutException("WEB6 cutover phase deadline.");
    }
    internal int SqlTimeout(CancellationToken token)
    {
        ThrowIfExpired(token);
        // SqlClient interprets zero as infinite; token cancellation enforces a subsecond remainder.
        return Math.Max(1, (int)Math.Min(30, Math.Floor(Remaining.TotalSeconds)));
    }
    internal int Milliseconds(CancellationToken token)
    {
        ThrowIfExpired(token);
        return Math.Max(1, (int)Math.Min(int.MaxValue, Math.Floor(Remaining.TotalMilliseconds)));
    }
    internal SqlTestCommand Command(string text, CancellationToken token, object? parameters = null, IDbTransaction? transaction = null) =>
        new SqlTestCommand(text, parameters, transaction, SqlTimeout(token), cancellationToken: token);
    internal CancellationTokenSource Link(CancellationToken token)
    {
        ThrowIfExpired(token);
        var source = CancellationTokenSource.CreateLinkedTokenSource(token);
        source.CancelAfter(Remaining);
        return source;
    }
    internal T Observe<T>(Func<T> operation, CancellationToken token)
    {
        ThrowIfExpired(token);
        var result = operation(); // Never race or abandon synchronous provider/file work.
        ThrowIfExpired(token);
        return result;
    }
    internal void Observe(Action operation, CancellationToken token) => Observe(() => { operation(); return true; }, token);
    internal async Task Open(SqlConnection sql, CancellationToken token, Func<CancellationToken, Task>? open = null)
    {
        using var linked = Link(token);
        await (open == null ? sql.OpenAsync(linked.Token) : open(linked.Token)).ConfigureAwait(false);
        ThrowIfExpired(linked.Token);
    }
    internal async Task Delay(TimeSpan delay, CancellationToken token, Func<TimeSpan, CancellationToken, Task>? wait = null)
    {
        ThrowIfExpired(token);
        var remaining = Remaining;
        var bounded = delay < remaining ? delay : remaining;
        await (wait == null ? Task.Delay(bounded, token) : wait(bounded, token)).ConfigureAwait(false);
        ThrowIfExpired(token);
    }
}
