using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class DesktopViewModelBaseTests
    {
        [Fact]
        public async Task ExecuteBusyAsync_exposes_busy_state_and_cancels_the_active_operation()
        {
            var notifications = new DesktopNotificationCenter();
            var viewModel = new TestViewModel(notifications);
            var started = new TaskCompletionSource<bool>();

            var execution = viewModel.RunAsync(
                async cancellationToken =>
                {
                    started.SetResult(true);
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                },
                CancellationToken.None);
            await started.Task;

            Assert.True(viewModel.IsBusy);
            viewModel.CancelCurrentOperation();
            await execution;

            Assert.False(viewModel.IsBusy);
            Assert.Equal("Operation canceled.", notifications.LastNotification!.Message);
        }

        [Fact]
        public void ApplyApiError_maps_field_details_and_publishes_one_error_notification()
        {
            var notifications = new DesktopNotificationCenter();
            var viewModel = new TestViewModel(notifications);
            var error = new ApiError(
                ApiStatusCode.BadRequest,
                "ValidationError",
                "Please correct the highlighted fields.",
                new[]
                {
                    new ApiErrorDetail("Validation.Required", "Client is required.", "clientName"),
                    new ApiErrorDetail("Validation.Required", "Another message.", "clientName"),
                });

            viewModel.Apply(error);

            Assert.Equal(
                new[] { "Client is required.", "Another message." },
                ((IEnumerable)viewModel.GetErrors("clientName")).Cast<string>());
            Assert.Equal("Please correct the highlighted fields.", viewModel.ErrorMessage);
            Assert.Equal(DesktopNotificationKind.Error, notifications.LastNotification!.Kind);
        }

        private sealed class TestViewModel : DesktopViewModelBase
        {
            public TestViewModel(IDesktopNotificationService notifications)
                : base(notifications)
            {
            }

            public Task RunAsync(
                Func<CancellationToken, Task> operation,
                CancellationToken cancellationToken)
            {
                return ExecuteBusyAsync(operation, cancellationToken);
            }

            public void Apply(ApiError error)
            {
                ApplyApiError(error);
            }
        }
    }
}
