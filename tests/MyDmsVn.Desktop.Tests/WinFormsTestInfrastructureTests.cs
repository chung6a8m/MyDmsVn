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

            StaTest.Run(() => observedState = Thread.CurrentThread.GetApartmentState());

            Assert.Equal(ApartmentState.STA, observedState);
        }

        [Fact]
        public void WinFormsTestGuard_captures_thread_exception()
        {
            StaTest.Run(
                () =>
                {
                    using (var guard = new WinFormsTestGuard())
                    {
                        var expected = new InvalidOperationException("test failure");

                        System.Windows.Forms.Application.OnThreadException(expected);

                        Assert.Same(expected, guard.Exceptions[0]);
                    }
                });
        }

        [Fact]
        public void WinFormsTestGuard_captures_grid_data_error_without_throwing()
        {
            StaTest.Run(
                () =>
                {
                    using (var guard = new WinFormsTestGuard())
                    using (var grid = new TestDataGridView())
                    {
                        guard.Attach(grid);
                        var expected = new InvalidOperationException("invalid cell");

                        grid.RaiseDataError(expected);

                        Assert.Same(expected, guard.Exceptions[0]);
                    }
                });
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
