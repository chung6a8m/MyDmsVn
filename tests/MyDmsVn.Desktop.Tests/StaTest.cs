using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace MyDmsVn.Desktop.Tests
{
    internal static class StaTest
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

        public static void Run(Action action)
        {
            ExceptionDispatchInfo? capturedException = null;
            var thread = new Thread(
                () =>
                {
                    try
                    {
                        action();
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
            if (!thread.Join(DefaultTimeout))
            {
                throw new TimeoutException("STA test action did not finish within 10 seconds.");
            }

            capturedException?.Throw();
        }
    }
}
