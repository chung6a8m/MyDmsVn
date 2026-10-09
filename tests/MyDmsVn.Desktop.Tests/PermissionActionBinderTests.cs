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

        [Fact]
        public void Queued_permission_grant_cannot_reenable_an_action_after_sign_out()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var session = new TestDesktopSession();
                    var apiClient = new DeferredPermissionApiClient();
                    using (var action = new Button { Enabled = true })
                    using (var binder = new PermissionActionBinder(apiClient, session))
                    {
                        Assert.NotEqual(IntPtr.Zero, action.Handle);
                        QueuePermissionGrantFromWorker(
                            action,
                            binder,
                            apiClient,
                            cancellationToken);

                        session.SignOut();
                        Assert.False(action.Enabled);
                        System.Windows.Forms.Application.DoEvents();

                        Assert.False(action.Enabled);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Queued_permission_grant_cannot_reenable_an_action_after_binder_disposal()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var session = new TestDesktopSession();
                    var apiClient = new DeferredPermissionApiClient();
                    using (var action = new Button { Enabled = true })
                    {
                        var binder = new PermissionActionBinder(apiClient, session);
                        try
                        {
                            Assert.NotEqual(IntPtr.Zero, action.Handle);
                            QueuePermissionGrantFromWorker(
                                action,
                                binder,
                                apiClient,
                                cancellationToken);

                            binder.Dispose();
                            Assert.False(action.Enabled);
                            System.Windows.Forms.Application.DoEvents();

                            Assert.False(action.Enabled);
                        }
                        finally
                        {
                            binder.Dispose();
                        }
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Permission_update_without_a_control_handle_is_marshaled_to_the_ui_thread()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var uiThreadId = Thread.CurrentThread.ManagedThreadId;
                    var session = new TestDesktopSession();
                    using (var action = new Button { Enabled = true })
                    using (var binder = new PermissionActionBinder(
                        new StubPermissionApiClient(isAllowed: false),
                        session))
                    {
                        Assert.False(action.IsHandleCreated);
                        var enabledChangedThreadId = 0;
                        action.EnabledChanged += (_, _) =>
                            enabledChangedThreadId = Thread.CurrentThread.ManagedThreadId;

                        Task.Run(
                                () => binder.ApplyAsync(
                                    action,
                                    "Catalog.Products.Write",
                                    cancellationToken))
                            .GetAwaiter()
                            .GetResult();

                        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
                        while (action.Enabled && DateTime.UtcNow < deadline)
                        {
                            System.Windows.Forms.Application.DoEvents();
                            Thread.Sleep(10);
                        }

                        Assert.False(action.Enabled);
                        Assert.Equal(uiThreadId, enabledChangedThreadId);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        private static void QueuePermissionGrantFromWorker(
            Control action,
            PermissionActionBinder binder,
            DeferredPermissionApiClient apiClient,
            CancellationToken cancellationToken)
        {
            var applyTask = Task.Run(
                () => binder.ApplyAsync(
                    action,
                    "Catalog.Products.Write",
                    cancellationToken));
            Assert.True(
                apiClient.Started.Wait(TimeSpan.FromSeconds(2)),
                "Permission request did not start before the timeout.");

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            while (action.Enabled && DateTime.UtcNow < deadline)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }

            Assert.False(action.Enabled);
            Task.Run(() => apiClient.Complete(isAllowed: true))
                .GetAwaiter()
                .GetResult();
            applyTask.GetAwaiter().GetResult();
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

            public ManualResetEventSlim Started { get; } = new ManualResetEventSlim();

            public Task<ApiResponse<PermissionDecisionDto>> CheckAsync(
                string permissionKey,
                CancellationToken cancellationToken)
            {
                Started.Set();
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
