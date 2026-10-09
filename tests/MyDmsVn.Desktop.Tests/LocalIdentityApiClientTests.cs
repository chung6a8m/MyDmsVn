using System.Threading;
using System.Threading.Tasks;
using System;
using System.Windows.Forms;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Desktop.WinForms;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class LocalIdentityApiClientTests
    {
        [Fact]
        public async Task Successful_login_updates_the_shared_local_session_and_current_user_query()
        {
            using (var provider = CreateProvider())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();

                var login = await client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);
                var current = await client.GetCurrentUserAsync(CancellationToken.None);

                Assert.True(login.IsSuccess);
                Assert.True(current.IsSuccess);
                Assert.True(session.IsAuthenticated);
                Assert.Equal("Warehouse Operator", session.CurrentUser!.DisplayName);
                Assert.Equal(login.Data!.UserId, current.Data!.UserId);
            }
        }

        [Fact]
        public async Task Signing_out_clears_the_server_current_user_for_subsequent_requests()
        {
            using (var provider = CreateProvider())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                await client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);

                session.SignOut();
                var current = await client.GetCurrentUserAsync(CancellationToken.None);

                Assert.False(session.IsAuthenticated);
                Assert.False(current.IsSuccess);
                Assert.Equal("Auth.Unauthorized", current.Error!.Code);
            }
        }

        [Fact]
        public async Task Signing_out_invalidates_a_login_that_is_still_in_flight()
        {
            var delayedHandler = new DelayedLoginHandler();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>>(delayedHandler);

            using (var provider = services.BuildServiceProvider())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var loginTask = client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);
                await delayedHandler.Started;

                session.SignOut();
                delayedHandler.Complete();
                var response = await loginTask;

                Assert.False(response.IsSuccess);
                Assert.Equal("Auth.SessionChanged", response.Error!.Code);
                Assert.Equal(ApiStatusCode.Unauthorized, response.Error.Status);
                Assert.False(session.IsAuthenticated);
            }
        }

        [Fact]
        public async Task Canceled_login_cannot_establish_a_session_when_the_handler_ignores_cancellation()
        {
            var delayedHandler = new DelayedLoginHandler();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>>(delayedHandler);

            using (var provider = services.BuildServiceProvider())
            using (var cancellation = new CancellationTokenSource())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var login = client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    cancellation.Token);
                await delayedHandler.Started;

                cancellation.Cancel();
                delayedHandler.Complete();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login);
                Assert.False(session.IsAuthenticated);
            }
        }

        [Fact]
        public void Pending_login_invalidates_the_old_principals_desktop_requests()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var handler = new SwitchingLoginHandler();
                    var services = new ServiceCollection();
                    services.AddServerApplication();
                    services.AddLocalDesktopAdapter();
                    services.AddSingleton<
                        IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>>(handler);

                    using (var provider = services.BuildServiceProvider())
                    {
                        var client = provider.GetRequiredService<IIdentityApiClient>();
                        var session = provider.GetRequiredService<IDesktopSession>();
                        client.LoginAsync(
                                new LoginRequest("operator", "secret"),
                                cancellationToken)
                            .GetAwaiter()
                            .GetResult();

                        var notifications = new DesktopNotificationCenter();
                        using (var shell = new FoundationShellForm(
                            new FoundationViewModel(new ReadyFoundationApiClient()),
                            session,
                            notifications))
                        using (var action = new Button { Enabled = true })
                        using (var binder = new PermissionActionBinder(
                            new AllowedPermissionApiClient(),
                            session))
                        {
                            var login = client.LoginAsync(
                                new LoginRequest("manager", "secret"),
                                cancellationToken);
                            Assert.True(
                                handler.SecondLoginStarted.Wait(TimeSpan.FromSeconds(2)),
                                "Second login did not start before the timeout.");

                            var content = new Panel();
                            var page = shell.OpenWorkspace(
                                "pending",
                                "Pending",
                                content,
                                shell.WorkspaceSessionVersion);
                            binder.ApplyAsync(
                                    action,
                                    "Catalog.Products.Write",
                                    cancellationToken)
                                .GetAwaiter()
                                .GetResult();

                            Assert.False(session.IsAuthenticated);
                            Assert.Null(page);
                            Assert.True(content.IsDisposed);
                            Assert.False(action.Enabled);

                            handler.CompleteSecondLogin();
                            var response = login.GetAwaiter().GetResult();

                            Assert.True(response.IsSuccess);
                            Assert.Equal(84, session.CurrentUser!.UserId);
                        }
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        private static ServiceProvider CreateProvider()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                SuccessfulLoginHandler>();
            return services.BuildServiceProvider();
        }

        private sealed class SuccessfulLoginHandler
            : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
        {
            public Task<ErrorOr<CurrentUserDto>> Handle(
                LoginCommand request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<ErrorOr<CurrentUserDto>>(
                    new CurrentUserDto(42, request.Username, "Warehouse Operator"));
            }
        }

        private sealed class DelayedLoginHandler
            : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
        {
            private readonly TaskCompletionSource<bool> _started =
                new TaskCompletionSource<bool>();
            private readonly TaskCompletionSource<ErrorOr<CurrentUserDto>> _completion =
                new TaskCompletionSource<ErrorOr<CurrentUserDto>>();

            public Task Started => _started.Task;

            public Task<ErrorOr<CurrentUserDto>> Handle(
                LoginCommand request,
                CancellationToken cancellationToken)
            {
                _started.TrySetResult(true);
                return _completion.Task;
            }

            public void Complete()
            {
                _completion.SetResult(
                    new CurrentUserDto(42, "operator", "Warehouse Operator"));
            }
        }

        private sealed class SwitchingLoginHandler
            : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
        {
            private readonly TaskCompletionSource<ErrorOr<CurrentUserDto>> _secondLogin =
                new TaskCompletionSource<ErrorOr<CurrentUserDto>>();

            public ManualResetEventSlim SecondLoginStarted { get; } =
                new ManualResetEventSlim();

            public Task<ErrorOr<CurrentUserDto>> Handle(
                LoginCommand request,
                CancellationToken cancellationToken)
            {
                if (request.Username == "operator")
                {
                    return Task.FromResult<ErrorOr<CurrentUserDto>>(
                        new CurrentUserDto(42, "operator", "Operator"));
                }

                SecondLoginStarted.Set();
                return _secondLogin.Task;
            }

            public void CompleteSecondLogin()
            {
                _secondLogin.SetResult(
                    new CurrentUserDto(84, "manager", "Manager"));
            }
        }

        private sealed class ReadyFoundationApiClient : IFoundationApiClient
        {
            public Task<ApiResponse<FoundationStatus>> GetStatusAsync(
                FoundationStatusRequest request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    ApiResponse<FoundationStatus>.Success(
                        new FoundationStatus(true, "Local")));
            }
        }

        private sealed class AllowedPermissionApiClient : IPermissionApiClient
        {
            public Task<ApiResponse<PermissionDecisionDto>> CheckAsync(
                string permissionKey,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    ApiResponse<PermissionDecisionDto>.Success(
                        new PermissionDecisionDto(permissionKey, true)));
            }
        }
    }
}
