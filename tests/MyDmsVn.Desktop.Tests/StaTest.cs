using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Desktop.Tests
{
    internal static class StaTest
    {
        private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);

        public static void Run(Action<CancellationToken> action, TimeSpan timeout)
        {
            GuiTestSafety.ThrowIfPoisoned();
            ExceptionDispatchInfo? capturedException = null;
            var cancellation = new CancellationTokenSource();
            var safeToDispose = true;
            try
            {
                var thread = new Thread(
                    () =>
                    {
                        try
                        {
                            action(cancellation.Token);
                        }
                        catch (Exception exception)
                        {
                            capturedException = ExceptionDispatchInfo.Capture(exception);
                        }
                    })
                {
                    IsBackground = true,
                };

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                if (!thread.Join(timeout))
                {
                    var cleanupClock = Stopwatch.StartNew();
                    var cancel = Task.Run(() => cancellation.Cancel());
                    var joined = thread.Join(CleanupTimeout);
                    var remaining = CleanupTimeout - cleanupClock.Elapsed;
                    var cancelDrained = cancel.IsCompleted || (remaining > TimeSpan.Zero &&
                        Task.WhenAny(cancel, Task.Delay(remaining)).GetAwaiter().GetResult() == cancel);
                    if (!joined || !cancelDrained)
                    {
                        safeToDispose = false;
                        var failure = new TimeoutException(
                            "STA test action ignored cancellation and did not clean up within 5 seconds. " +
                            "Run non-cooperative fatal or hang scenarios in a bounded subprocess.", capturedException?.SourceException);
                        GuiTestSafety.Poison(failure);
                        throw failure;
                    }

                    var cause = capturedException?.SourceException;
                    if (cancel.IsFaulted)
                        cause = cause == null ? cancel.Exception : new AggregateException(cause, cancel.Exception!);
                    throw new TimeoutException("STA test action exceeded its timeout; cooperative cleanup drained.", cause);
                }

                capturedException?.Throw();
            }
            finally
            {
                // A noncooperative worker can still use its CTS after this bounded wait.
                if (safeToDispose) cancellation.Dispose();
            }
        }
    }
}
