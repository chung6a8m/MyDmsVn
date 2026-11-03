using System;
using System.Threading;

namespace MyDmsVn.WebOperation.Operator;

public sealed class OperatorProcessDeadline : IDisposable
{
    private readonly Action _expire;
    private readonly Timer _timer;
    private int _expired;

    public OperatorProcessDeadline(TimeSpan deadline, Action expire)
    {
        if (deadline <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(deadline));
        _expire = expire ?? throw new ArgumentNullException(nameof(expire));
        _timer = new Timer(Expire, null, deadline, Timeout.InfiniteTimeSpan);
    }

    private void Expire(object? state)
    {
        if (Interlocked.Exchange(ref _expired, 1) == 0)
            _expire();
    }

    public void Dispose() => _timer.Dispose();
}
