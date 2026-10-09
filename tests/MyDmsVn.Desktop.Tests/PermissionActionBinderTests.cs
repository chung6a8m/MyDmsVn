using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class PermissionActionBinderTests
    {
        [Fact]
        public void Action_stays_disabled_until_permission_is_granted()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var session = new TestDesktopSession();
                    using (var action = new Button { Enabled = true })
                    using (var binder = new PermissionActionBinder(
                        new StubPermissionApiClient(isAllowed: true),
                        session))
                    {
                        binder.ApplyAsync(action, "Catalog.Products.Write", cancellationToken)
                            .GetAwaiter()
                            .GetResult();

                        Assert.True(action.Enabled);

                        session.SignOut();

                        Assert.False(action.Enabled);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Action_is_disabled_when_permission_is_denied_or_cannot_be_verified()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var denied = new Button { Enabled = true })
                    using (var failed = new Button { Enabled = true })
                    using (var deniedBinder = new PermissionActionBinder(
                        new StubPermissionApiClient(isAllowed: false),
                        new TestDesktopSession()))
                    using (var failedBinder = new PermissionActionBinder(
                        new FailingPermissionApiClient(),
                        new TestDesktopSession()))
                    {
                        deniedBinder.ApplyAsync(
                                denied,
                                "Catalog.Products.Write",
                                cancellationToken)
                            .GetAwaiter()
                            .GetResult();
                        failedBinder.ApplyAsync(
                                failed,
                                "Catalog.Products.Write",
                                cancellationToken)
                            .GetAwaiter()
                            .GetResult();

                        Assert.False(denied.Enabled);
                        Assert.False(failed.Enabled);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Delayed_permission_result_cannot_reenable_an_action_after_sign_out()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var session = new TestDesktopSession();
                    var apiClient = new DeferredPermissionApiClient();
                    using (var action = new Button { Enabled = true })
                    using (var binder = new PermissionActionBinder(apiClient, session))
                    {
                        var applyTask = binder.ApplyAsync(
                            action,
                            "Catalog.Products.Write",
                            cancellationToken);
                        Assert.False(action.Enabled);

                        session.SignOut();
                        apiClient.Complete(isAllowed: true);
                        applyTask.GetAwaiter().GetResult();

                        Assert.False(action.Enabled);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        private sealed class StubPermissionApiClient : IPermissionApiClient
        {
            private readonly bool _isAllowed;

            public StubPermissionApiClient(bool isAllowed)
            {
                _isAllowed = isAllowed;
            }

            public Task<ApiResponse<PermissionDecisionDto>> CheckAsync(
                string permissionKey,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    ApiResponse<PermissionDecisionDto>.Success(
                        new PermissionDecisionDto(permissionKey, _isAllowed)));
            }
        }

        private sealed class FailingPermissionApiClient : IPermissionApiClient
        {
            public Task<ApiResponse<PermissionDecisionDto>> CheckAsync(
                string permissionKey,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    ApiResponse<PermissionDecisionDto>.Failure(
                        new ApiError(
                            ApiStatusCode.InternalServerError,
                            "Permission.Unavailable",
                            "Permission could not be checked.")));
            }
        }

        private sealed class DeferredPermissionApiClient : IPermissionApiClient
        {
            private readonly TaskCompletionSource<ApiResponse<PermissionDecisionDto>> _completion =
                new TaskCompletionSource<ApiResponse<PermissionDecisionDto>>();

            public Task<ApiResponse<PermissionDecisionDto>> CheckAsync(
                string permissionKey,
                CancellationToken cancellationToken)
            {
                return _completion.Task;
            }

            public void Complete(bool isAllowed)
            {
                _completion.SetResult(
                    ApiResponse<PermissionDecisionDto>.Success(
                        new PermissionDecisionDto("Catalog.Products.Write", isAllowed)));
            }
        }

        private sealed class TestDesktopSession : IDesktopSession
        {
            public TestDesktopSession()
            {
                CurrentUser = new CurrentUserDto(42, "operator", "Operator");
            }

            public event EventHandler? SessionChanged;

            public bool IsAuthenticated => CurrentUser != null;

            public CurrentUserDto? CurrentUser { get; private set; }

            public long Version { get; private set; }

            public void SignOut()
            {
                Version++;
                CurrentUser = null;
                SessionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
