using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using MyDmsVn.Server.Application.Security;
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
        public async Task Login_while_authenticated_is_rejected_without_replacing_the_current_user()
        {
            using (var provider = CreateProvider())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var first = await client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);

                var second = await client.LoginAsync(
                    new LoginRequest("manager", "secret"),
                    CancellationToken.None);

                Assert.True(first.IsSuccess);
                Assert.False(second.IsSuccess);
                Assert.Equal("Auth.AlreadyAuthenticated", second.Error!.Code);
                Assert.Equal(42, session.CurrentUser!.UserId);
                Assert.Equal("operator", session.CurrentUser.Username);
            }
        }

        [Fact]
        public async Task Login_result_does_not_create_a_session_when_sql_account_validation_rejects_it()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddScoped<IUserAccountValidator, InactiveAccountValidator>();
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                SuccessfulLoginHandler>();
            using (var provider = services.BuildServiceProvider())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();

                var response = await client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);

                Assert.False(response.IsSuccess);
                Assert.Equal("Auth.Unauthorized", response.Error!.Code);
                Assert.False(session.IsAuthenticated);
            }
        }

        [Fact]
        public async Task Disabled_account_revalidation_clears_the_desktop_session()
        {
            var validator = new SwitchableAccountValidator();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<IUserAccountValidator>(validator);
            services.AddTransient<IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>, SuccessfulLoginHandler>();
            using (var provider = services.BuildServiceProvider())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                Assert.True((await client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None)).IsSuccess);
                validator.IsActive = false;

                var current = await client.GetCurrentUserAsync(CancellationToken.None);

                Assert.False(current.IsSuccess);
                Assert.Equal("Auth.Unauthorized", current.Error!.Code);
                Assert.False(session.IsAuthenticated);
                Assert.Equal(DesktopSessionState.Expired, session.State);
            }
        }

        [Fact]
        public async Task Sql_validation_failure_is_not_reported_as_bad_credentials()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddScoped<IUserAccountValidator, ThrowingAccountValidator>();
            services.AddTransient<IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>, SuccessfulLoginHandler>();
            using (var provider = services.BuildServiceProvider())
            {
                var response = await provider.GetRequiredService<IIdentityApiClient>().LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);

                Assert.False(response.IsSuccess);
                Assert.Equal("Database.Unavailable", response.Error!.Code);
                Assert.Equal(ApiStatusCode.InternalServerError, response.Error.Status);
                Assert.False(provider.GetRequiredService<IDesktopSession>().IsAuthenticated);
            }
        }

        [Fact]
        public async Task Sql_failure_during_credential_lookup_is_reported_as_database_unavailable()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                ThrowingLoginHandler>();
            using (var provider = services.BuildServiceProvider())
            {
                var response = await provider.GetRequiredService<IIdentityApiClient>().LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);

                Assert.False(response.IsSuccess);
                Assert.Equal("Database.Unavailable", response.Error!.Code);
                Assert.Equal(DesktopSessionState.Unauthenticated,
                    provider.GetRequiredService<IDesktopSession>().State);
            }
        }

        [Fact]
        public async Task Late_account_validation_from_user_a_cannot_expire_user_b_session()
        {
            var validator = new PausingSecondAccountValidator();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<IUserAccountValidator>(validator);
            services.AddSingleton<IPermissionStore>(new AllowPermissionStore());
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                UserSpecificLoginHandler>();
            using (var provider = services.BuildServiceProvider())
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None)).IsSuccess);

                var oldRequest = provider.GetRequiredService<IPermissionApiClient>()
                    .CheckAsync("Catalog.Products.Read", CancellationToken.None);
                await validator.SecondCallStarted;
                session.SignOut();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("manager", "secret"),
                    CancellationToken.None)).IsSuccess);

                validator.CompleteSecondCall(isActive: false);
                var oldResponse = await oldRequest;

                Assert.False(oldResponse.IsSuccess);
                Assert.True(session.IsAuthenticated);
                Assert.Equal("manager", session.CurrentUser!.Username);
            }
        }

        [Fact]
        public async Task Late_current_user_validation_from_user_a_cannot_sign_out_user_b()
        {
            var validator = new PausingSecondAccountValidator();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<IUserAccountValidator>(validator);
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                UserSpecificLoginHandler>();
            using (var provider = services.BuildServiceProvider())
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None)).IsSuccess);

                var staleCurrentUser = identity.GetCurrentUserAsync(CancellationToken.None);
                await WaitForSignalAsync(validator.SecondCallStarted, timeout.Token);
                session.SignOut();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("manager", "secret"),
                    CancellationToken.None)).IsSuccess);

                validator.CompleteSecondCall(isActive: false);
                var staleResponse = await staleCurrentUser;

                Assert.False(staleResponse.IsSuccess);
                Assert.True(session.IsAuthenticated);
                Assert.Equal("manager", session.CurrentUser!.Username);
            }
        }

        [Fact]
        public async Task Request_waiting_for_account_validation_is_rejected_after_session_switch()
        {
            var validator = new PausingSecondAccountValidator();
            var permissionStore = new CountingPermissionStore();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<IUserAccountValidator>(validator);
            services.AddSingleton<IPermissionStore>(permissionStore);
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                UserSpecificLoginHandler>();
            using (var provider = services.BuildServiceProvider())
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None)).IsSuccess);

                var staleRequest = provider.GetRequiredService<IPermissionApiClient>()
                    .CheckAsync("Catalog.Products.Read", CancellationToken.None);
                await WaitForSignalAsync(validator.SecondCallStarted, timeout.Token);
                session.SignOut();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("manager", "secret"),
                    CancellationToken.None)).IsSuccess);

                validator.CompleteSecondCall(isActive: true);
                var staleResponse = await staleRequest;

                Assert.False(staleResponse.IsSuccess);
                Assert.Equal("Auth.SessionChanged", staleResponse.Error!.Code);
                Assert.Equal(0, permissionStore.CallCount);
                Assert.True(session.IsAuthenticated);
                Assert.Equal("manager", session.CurrentUser!.Username);
            }
        }

        [Fact]
        public async Task Late_validation_from_old_login_attempt_cannot_clear_new_session()
        {
            var validator = new PausingFirstAccountValidator();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<IUserAccountValidator>(validator);
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                UserSpecificLoginHandler>();
            using (var provider = services.BuildServiceProvider())
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var oldLogin = identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);
                await validator.FirstCallStarted;

                session.SignOut();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("manager", "secret"),
                    CancellationToken.None)).IsSuccess);
                validator.CompleteFirstCall(isActive: false);
                var oldResponse = await oldLogin;

                Assert.False(oldResponse.IsSuccess);
                Assert.True(session.IsAuthenticated);
                Assert.Equal("manager", session.CurrentUser!.Username);
            }
        }

        [Fact]
        public async Task Signing_out_invalidates_a_login_that_is_still_in_flight()
        {
            var delayedHandler = new DelayedLoginHandler();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddScoped<IUserAccountValidator, ActiveAccountValidator>();
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
        public async Task Concurrent_login_attempts_allow_only_the_latest_attempt_to_publish_session()
        {
            var handler = new TwoAttemptLoginHandler();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddScoped<IUserAccountValidator, ActiveAccountValidator>();
            services.AddSingleton<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>>(handler);
            using (var provider = services.BuildServiceProvider())
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var first = identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);
                await WaitForSignalAsync(handler.FirstStarted, timeout.Token);
                var second = identity.LoginAsync(
                    new LoginRequest("manager", "secret"),
                    CancellationToken.None);
                await WaitForSignalAsync(handler.SecondStarted, timeout.Token);

                handler.CompleteFirst();
                var firstResponse = await first;
                Assert.False(firstResponse.IsSuccess);
                Assert.Equal("Auth.SessionChanged", firstResponse.Error!.Code);
                Assert.False(session.IsAuthenticated);

                handler.CompleteSecond();
                var secondResponse = await second;
                Assert.True(secondResponse.IsSuccess);
                Assert.True(session.IsAuthenticated);
                Assert.Equal("manager", session.CurrentUser!.Username);
            }
        }

        [Fact]
        public async Task Parallel_local_requests_keep_the_actor_captured_for_each_session()
        {
            var permissionStore = new PausingRecordingPermissionStore();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddScoped<IUserAccountValidator, ActiveAccountValidator>();
            services.AddSingleton<IPermissionStore>(permissionStore);
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                UserSpecificLoginHandler>();
            using (var provider = services.BuildServiceProvider())
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var permissions = provider.GetRequiredService<IPermissionApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None)).IsSuccess);

                var first = permissions.CheckAsync("Catalog.Products.Read", CancellationToken.None);
                var second = permissions.CheckAsync("Catalog.Products.Write", CancellationToken.None);
                await permissionStore.WaitForCallAsync(timeout.Token);
                await permissionStore.WaitForCallAsync(timeout.Token);

                session.SignOut();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("manager", "secret"),
                    CancellationToken.None)).IsSuccess);
                var third = permissions.CheckAsync("Inventory.Balances.Read", CancellationToken.None);
                await permissionStore.WaitForCallAsync(timeout.Token);

                permissionStore.Complete();
                var responses = await Task.WhenAll(first, second, third);

                Assert.All(responses, response => Assert.True(response.IsSuccess));
                Assert.Equal(new[] { 42, 42, 84 }, permissionStore.UserIds.OrderBy(id => id));
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
                Assert.Equal(DesktopSessionState.Unauthenticated, session.State);
            }
        }

        [Fact]
        public async Task Canceled_login_cannot_establish_a_session_when_account_validation_ignores_cancellation()
        {
            var validator = new PausingFirstAccountValidator();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<IUserAccountValidator>(validator);
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                SuccessfulLoginHandler>();

            using (var provider = services.BuildServiceProvider())
            using (var cancellation = new CancellationTokenSource())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var login = client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    cancellation.Token);
                await validator.FirstCallStarted;

                cancellation.Cancel();
                validator.CompleteFirstCall(isActive: true);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login);
                Assert.False(session.IsAuthenticated);
                Assert.Equal(DesktopSessionState.Unauthenticated, session.State);
            }
        }

        [Fact]
        public async Task Canceled_login_cleans_up_when_account_validation_observes_cancellation()
        {
            var validator = new CancellationAwareAccountValidator();
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<IUserAccountValidator>(validator);
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                SuccessfulLoginHandler>();

            using (var provider = services.BuildServiceProvider())
            using (var cancellation = new CancellationTokenSource())
            {
                var client = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var login = client.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    cancellation.Token);
                await validator.Started;

                cancellation.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login);
                Assert.False(session.IsAuthenticated);
                Assert.Equal(DesktopSessionState.Unauthenticated, session.State);
            }
        }

        [Fact]
        public void Explicit_logout_then_pending_login_invalidates_the_old_principals_desktop_requests()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var handler = new SwitchingLoginHandler();
                    var services = new ServiceCollection();
                    services.AddServerApplication();
                    services.AddLocalDesktopAdapter();
                    services.AddScoped<IUserAccountValidator, ActiveAccountValidator>();
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
                            session.SignOut();
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
            services.AddScoped<IUserAccountValidator, ActiveAccountValidator>();
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                SuccessfulLoginHandler>();
            return services.BuildServiceProvider();
        }

        private static async Task WaitForSignalAsync(
            Task signal,
            CancellationToken cancellationToken)
        {
            var canceled = Task.Delay(Timeout.Infinite, cancellationToken);
            if (await Task.WhenAny(signal, canceled) != signal)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            await signal;
        }

        private sealed class ActiveAccountValidator : IUserAccountValidator
        {
            public Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken)
            {
                return Task.FromResult(true);
            }
        }

        private sealed class AllowPermissionStore : IPermissionStore
        {
            public Task<PermissionSnapshot> GetSnapshotAsync(
                int userId,
                string permissionKey,
                CancellationToken cancellationToken) =>
                Task.FromResult(new PermissionSnapshot(true, false));
        }

        private sealed class CountingPermissionStore : IPermissionStore
        {
            public int CallCount { get; private set; }

            public Task<PermissionSnapshot> GetSnapshotAsync(
                int userId,
                string permissionKey,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(new PermissionSnapshot(true, false));
            }
        }

        private sealed class PausingRecordingPermissionStore : IPermissionStore
        {
            private readonly ConcurrentBag<int> _userIds = new ConcurrentBag<int>();
            private readonly SemaphoreSlim _calls = new SemaphoreSlim(0);
            private readonly TaskCompletionSource<bool> _completion =
                new TaskCompletionSource<bool>();

            public int[] UserIds => _userIds.ToArray();

            public async Task<PermissionSnapshot> GetSnapshotAsync(
                int userId,
                string permissionKey,
                CancellationToken cancellationToken)
            {
                _userIds.Add(userId);
                _calls.Release();
                await _completion.Task.ConfigureAwait(false);
                return new PermissionSnapshot(true, false);
            }

            public Task WaitForCallAsync(CancellationToken cancellationToken) =>
                _calls.WaitAsync(cancellationToken);

            public void Complete() => _completion.TrySetResult(true);
        }

        private sealed class InactiveAccountValidator : IUserAccountValidator
        {
            public Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken)
            {
                return Task.FromResult(false);
            }
        }

        private sealed class SwitchableAccountValidator : IUserAccountValidator
        {
            public bool IsActive { get; set; } = true;

            public Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken) =>
                Task.FromResult(IsActive);
        }

        private sealed class ThrowingAccountValidator : IUserAccountValidator
        {
            public Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("SQL detail");
            }
        }

        private sealed class ThrowingLoginHandler
            : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
        {
            public Task<ErrorOr<CurrentUserDto>> Handle(
                LoginCommand request,
                CancellationToken cancellationToken) =>
                throw new InvalidOperationException("SQL detail");
        }

        private sealed class UserSpecificLoginHandler
            : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
        {
            public Task<ErrorOr<CurrentUserDto>> Handle(
                LoginCommand request,
                CancellationToken cancellationToken)
            {
                var userId = request.Username == "manager" ? 84 : 42;
                return Task.FromResult<ErrorOr<CurrentUserDto>>(
                    new CurrentUserDto(userId, request.Username, request.Username));
            }
        }

        private sealed class PausingSecondAccountValidator : IUserAccountValidator
        {
            private readonly TaskCompletionSource<bool> _secondCallStarted =
                new TaskCompletionSource<bool>();
            private readonly TaskCompletionSource<bool> _secondCall =
                new TaskCompletionSource<bool>();
            private int _callCount;

            public Task SecondCallStarted => _secondCallStarted.Task;

            public Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken)
            {
                if (Interlocked.Increment(ref _callCount) != 2)
                {
                    return Task.FromResult(true);
                }

                _secondCallStarted.TrySetResult(true);
                return _secondCall.Task;
            }

            public void CompleteSecondCall(bool isActive) =>
                _secondCall.TrySetResult(isActive);
        }

        private sealed class PausingFirstAccountValidator : IUserAccountValidator
        {
            private readonly TaskCompletionSource<bool> _firstCallStarted =
                new TaskCompletionSource<bool>();
            private readonly TaskCompletionSource<bool> _firstCall =
                new TaskCompletionSource<bool>();
            private int _callCount;

            public Task FirstCallStarted => _firstCallStarted.Task;

            public Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken)
            {
                if (Interlocked.Increment(ref _callCount) != 1)
                {
                    return Task.FromResult(true);
                }

                _firstCallStarted.TrySetResult(true);
                return _firstCall.Task;
            }

            public void CompleteFirstCall(bool isActive) =>
                _firstCall.TrySetResult(isActive);
        }

        private sealed class CancellationAwareAccountValidator : IUserAccountValidator
        {
            private readonly TaskCompletionSource<bool> _started =
                new TaskCompletionSource<bool>();

            public Task Started => _started.Task;

            public async Task<bool> IsActiveAsync(
                int userId,
                CancellationToken cancellationToken)
            {
                _started.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return true;
            }
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

        private sealed class TwoAttemptLoginHandler
            : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
        {
            private readonly TaskCompletionSource<bool> _firstStarted =
                new TaskCompletionSource<bool>();
            private readonly TaskCompletionSource<bool> _secondStarted =
                new TaskCompletionSource<bool>();
            private readonly TaskCompletionSource<ErrorOr<CurrentUserDto>> _first =
                new TaskCompletionSource<ErrorOr<CurrentUserDto>>();
            private readonly TaskCompletionSource<ErrorOr<CurrentUserDto>> _second =
                new TaskCompletionSource<ErrorOr<CurrentUserDto>>();

            public Task FirstStarted => _firstStarted.Task;

            public Task SecondStarted => _secondStarted.Task;

            public Task<ErrorOr<CurrentUserDto>> Handle(
                LoginCommand request,
                CancellationToken cancellationToken)
            {
                if (request.Username == "operator")
                {
                    _firstStarted.TrySetResult(true);
                    return _first.Task;
                }

                _secondStarted.TrySetResult(true);
                return _second.Task;
            }

            public void CompleteFirst() =>
                _first.TrySetResult(new CurrentUserDto(42, "operator", "Operator"));

            public void CompleteSecond() =>
                _second.TrySetResult(new CurrentUserDto(84, "manager", "Manager"));
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
