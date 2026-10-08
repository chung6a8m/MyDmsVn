using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace MyDmsVn.Desktop.Tests
{
    internal static class StaTest
    {
        private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);

        public static void Run(Action<CancellationToken> action, TimeSpan timeout)
        {
            ExceptionDispatchInfo? capturedException = null;
            using (var cancellation = new CancellationTokenSource())
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
                    cancellation.Cancel();
                    if (!thread.Join(CleanupTimeout))
                    {
                        throw new TimeoutException(
                            "STA test action ignored cancellation and did not clean up within 5 seconds. " +
                            "Run non-cooperative fatal or hang scenarios in a bounded subprocess.");
                    }

                    throw new TimeoutException("STA test action exceeded its timeout and was canceled cleanly.");
                }

                capturedException?.Throw();
            }
        }
    }
}
