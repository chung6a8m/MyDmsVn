using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class WinFormsTestInfrastructureTests
    {
        [Fact]
        public void StaTest_runs_action_on_sta_thread()
        {
            ApartmentState observedState = ApartmentState.Unknown;

            StaTest.Run(
                _ => observedState = Thread.CurrentThread.GetApartmentState(),
                TimeSpan.FromSeconds(10));

            Assert.Equal(ApartmentState.STA, observedState);
        }

        [Fact]
        public void StaTest_cancels_timed_out_action_and_waits_for_cleanup()
        {
            bool cleanupCompleted = false;

            Assert.Throws<TimeoutException>(
                () => StaTest.Run(
                    cancellationToken =>
                    {
                        try
                        {
                            cancellationToken.WaitHandle.WaitOne();
                        }
                        finally
                        {
                            cleanupCompleted = true;
                        }
                    },
                    TimeSpan.FromMilliseconds(50)));

            Assert.True(cleanupCompleted);
        }

        [Fact]
        public void StaTest_rethrows_action_failure_on_test_thread()
        {
            var expected = new InvalidOperationException("test failure");

            var actual = Assert.Throws<InvalidOperationException>(
                () => StaTest.Run(_ => throw expected, TimeSpan.FromSeconds(1)));

            Assert.Same(expected, actual);
        }

        [Fact]
        public void WinFormsTestGuard_captures_thread_exception()
        {
            StaTest.Run(
                _ =>
                {
                    using (var guard = new WinFormsTestGuard())
                    {
                        var expected = new InvalidOperationException("test failure");

                        System.Windows.Forms.Application.OnThreadException(expected);

                        var captured = guard.DrainExceptions();
                        Assert.Same(expected, captured.Single());
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void WinFormsTestGuard_fails_when_captured_exception_is_not_consumed()
        {
            var expected = new InvalidOperationException("unexpected UI failure");

            var actual = Assert.Throws<AggregateException>(
                () => StaTest.Run(
                    _ =>
                    {
                        using (var guard = new WinFormsTestGuard())
                        {
                            System.Windows.Forms.Application.OnThreadException(expected);
                        }
                    },
                    TimeSpan.FromSeconds(10)));

            Assert.Same(expected, actual.InnerExceptions.Single());
        }

        [Fact]
        public void WinFormsTestGuard_suppresses_error_dialogs_on_current_sta_thread()
        {
            StaTest.Run(
                _ =>
                {
                    using (var guard = new WinFormsTestGuard())
                    {
                        Assert.True(guard.FatalErrorDialogsSuppressed);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task WinFormsTestGuard_disposal_does_not_disable_another_sta_thread_guard()
        {
            using (var firstReady = new ManualResetEventSlim())
            using (var secondReady = new ManualResetEventSlim())
            using (var firstDisposed = new ManualResetEventSlim())
            {
                bool secondStillSuppressed = false;
                var first = Task.Run(
                    () => StaTest.Run(
                        cancellationToken =>
                        {
                            var guard = new WinFormsTestGuard();
                            firstReady.Set();
                            secondReady.Wait(cancellationToken);
                            guard.Dispose();
                            firstDisposed.Set();
                        },
                        TimeSpan.FromSeconds(10)));

                var second = Task.Run(
                    () => StaTest.Run(
                        cancellationToken =>
                        {
                            firstReady.Wait(cancellationToken);
                            using (var guard = new WinFormsTestGuard())
                            {
                                secondReady.Set();
                                firstDisposed.Wait(cancellationToken);
                                secondStillSuppressed = guard.FatalErrorDialogsSuppressed;
                            }
                        },
                        TimeSpan.FromSeconds(10)));

                await Task.WhenAll(first, second);

                Assert.True(secondStillSuppressed);
            }
        }

        [Fact]
        public void WinFormsTestGuard_captures_grid_data_error_without_throwing()
        {
            StaTest.Run(
                _ =>
                {
                    using (var guard = new WinFormsTestGuard())
                    using (var grid = new TestDataGridView())
                    {
                        guard.Attach(grid);
                        var expected = new InvalidOperationException("invalid cell");

                        grid.RaiseDataError(expected);

                        var captured = guard.DrainExceptions();
                        Assert.Same(expected, captured.Single());
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        private sealed class TestDataGridView : DataGridView
        {
            public void RaiseDataError(Exception exception)
            {
                OnDataError(
                    false,
                    new DataGridViewDataErrorEventArgs(exception, 0, 0, DataGridViewDataErrorContexts.Commit));
            }
        }
    }
}
