using System;
using System.Threading;
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

                        Assert.Same(expected, guard.Exceptions[0]);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void WinFormsTestGuard_suppresses_process_error_dialogs_while_active()
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

                        Assert.Same(expected, guard.Exceptions[0]);
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
