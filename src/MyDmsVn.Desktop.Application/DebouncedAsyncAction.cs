using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Desktop.Application
{
    public sealed class DebouncedAsyncAction : IDisposable
    {
        private readonly object _sync = new object();
        private readonly IAsyncDelay _delay;
        private readonly TimeSpan _interval;
        private CancellationTokenSource? _pendingCancellation;
        private bool _disposed;

        public DebouncedAsyncAction(IAsyncDelay delay, TimeSpan interval)
        {
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
            if (interval < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval));
            }

            _interval = interval;
        }

        public Task Schedule(Func<CancellationToken, Task> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            CancellationTokenSource cancellation;
            lock (_sync)
            {
                ThrowIfDisposed();
                CancelAndDispose(_pendingCancellation);
                cancellation = new CancellationTokenSource();
                _pendingCancellation = cancellation;
            }

            return RunAsync(operation, cancellation);
        }

        public void Cancel()
        {
            lock (_sync)
            {
                CancelAndDispose(_pendingCancellation);
                _pendingCancellation = null;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                CancelAndDispose(_pendingCancellation);
                _pendingCancellation = null;
            }
        }

        private async Task RunAsync(
            Func<CancellationToken, Task> operation,
            CancellationTokenSource cancellation)
        {
            var token = cancellation.Token;
            try
            {
                await _delay.DelayAsync(_interval, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                await operation(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_pendingCancellation, cancellation))
                    {
                        _pendingCancellation = null;
                        cancellation.Dispose();
                    }
                }
            }
        }

        private static void CancelAndDispose(CancellationTokenSource? cancellation)
        {
            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();
            cancellation.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DebouncedAsyncAction));
            }
        }
    }
}
