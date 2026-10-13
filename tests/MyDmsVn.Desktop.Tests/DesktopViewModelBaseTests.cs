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

        [Fact]
        public void ApplyApiError_appends_safe_support_correlation_id()
        {
            var notifications = new DesktopNotificationCenter();
            var viewModel = new TestViewModel(notifications);

            viewModel.Apply(
                new ApiError(
                    ApiStatusCode.InternalServerError,
                    "InternalError",
                    "An unexpected error occurred.",
                    correlationId: "9a76a76b8fb34dd689d7145ea5d84504"));

            Assert.Equal(
                "An unexpected error occurred. Support ID: 9a76a76b8fb34dd689d7145ea5d84504",
                viewModel.ErrorMessage);
            Assert.Equal(viewModel.ErrorMessage, notifications.LastNotification!.Message);
        }

        [Fact]
        public void Unexpected_exception_message_is_not_exposed_to_the_user()
        {
            var notifications = new DesktopNotificationCenter();
            var reporter = new CapturingDesktopExceptionReporter();
            var viewModel = new TestViewModel(notifications, reporter);
            var exception = new InvalidOperationException("Server=db;Password=secret");

            viewModel.ApplyUnexpected(exception);

            Assert.Equal(
                "An unexpected error occurred. Support ID: 9a76a76b8fb34dd689d7145ea5d84504",
                viewModel.ErrorMessage);
            Assert.DoesNotContain("secret", notifications.LastNotification!.Message);
            Assert.Same(exception, reporter.Exception);
            Assert.Equal(nameof(TestViewModel), reporter.OperationName);
        }

        private sealed class TestViewModel : DesktopViewModelBase
        {
            public TestViewModel(
                IDesktopNotificationService notifications,
                IDesktopExceptionReporter? exceptionReporter = null)
                : base(notifications, exceptionReporter)
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

            public void ApplyUnexpected(Exception exception)
            {
                ApplyUnexpectedError(exception);
            }
        }

        private sealed class CapturingDesktopExceptionReporter : IDesktopExceptionReporter
        {
            public string? OperationName { get; private set; }
            public Exception? Exception { get; private set; }

            public string Report(string operationName, Exception exception)
            {
                OperationName = operationName;
                Exception = exception;
                return "9a76a76b8fb34dd689d7145ea5d84504";
            }
        }
    }
}
