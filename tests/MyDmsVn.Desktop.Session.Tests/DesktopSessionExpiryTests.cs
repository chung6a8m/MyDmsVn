using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class DesktopSessionExpiryTests
    {
        [Fact]
        public async Task Idle_boundary_expires_session_and_background_dispatch_does_not_reset_activity()
        {
            var clock = new FakeMonotonicClock();
            var services = new ServiceCollection();
            services.AddSingleton<IMonotonicClock>(clock);
            services.AddSingleton(new DesktopSessionOptions(TimeSpan.FromMinutes(15)));
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddScoped<IUserAccountValidator, ActiveAccountValidator>();
            services.AddTransient<IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>, LoginHandler>();
            using (var provider = services.BuildServiceProvider())
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var activity = provider.GetRequiredService<IDesktopSessionActivity>();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None)).IsSuccess);

                clock.Advance(TimeSpan.FromMinutes(10));
                Assert.True((await identity.GetCurrentUserAsync(CancellationToken.None)).IsSuccess);
                clock.Advance(TimeSpan.FromMinutes(5));

                Assert.True(activity.ExpireIfIdle());
                Assert.False(session.IsAuthenticated);
                Assert.Equal(DesktopSessionState.Expired, session.State);
            }
        }

        [Fact]
        public async Task Activity_after_idle_deadline_expires_instead_of_reviving_session()
        {
            var clock = new FakeMonotonicClock();
            using (var provider = CreateProvider(clock))
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var activity = provider.GetRequiredService<IDesktopSessionActivity>();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None)).IsSuccess);

                clock.Advance(TimeSpan.FromMinutes(16));
                activity.RecordUserActivity();

                Assert.False(session.IsAuthenticated);
                Assert.Equal(DesktopSessionState.Expired, session.State);
                Assert.False(activity.ExpireIfIdle());
            }
        }

        [Fact]
        public async Task Request_after_idle_deadline_is_not_admitted_before_timer_tick()
        {
            var clock = new FakeMonotonicClock();
            using (var provider = CreateProvider(clock))
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None)).IsSuccess);

                clock.Advance(TimeSpan.FromMinutes(16));
                var response = await identity.GetCurrentUserAsync(CancellationToken.None);

                Assert.False(response.IsSuccess);
                Assert.False(session.IsAuthenticated);
                Assert.Equal(DesktopSessionState.Expired, session.State);
            }
        }

        [Fact]
        public async Task Request_crossing_idle_deadline_during_validation_is_not_admitted()
        {
            var clock = new FakeMonotonicClock();
            var validator = new PausingSecondAccountValidator();
            var permissionStore = new CountingPermissionStore();
            var services = new ServiceCollection();
            services.AddSingleton<IMonotonicClock>(clock);
            services.AddSingleton(new DesktopSessionOptions(TimeSpan.FromMinutes(15)));
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<IUserAccountValidator>(validator);
            services.AddSingleton<IPermissionStore>(permissionStore);
            services.AddTransient<IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>, LoginHandler>();
            using (var provider = services.BuildServiceProvider())
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                Assert.True((await identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None)).IsSuccess);

                clock.Advance(TimeSpan.FromMinutes(14));
                var permission = provider.GetRequiredService<IPermissionApiClient>()
                    .CheckAsync("Catalog.Products.Read", CancellationToken.None);
                await WaitForSignalAsync(validator.SecondCallStarted, timeout.Token);
                clock.Advance(TimeSpan.FromMinutes(2));
                validator.CompleteSecondCall();

                var response = await permission;

                Assert.False(response.IsSuccess);
                Assert.Equal(0, permissionStore.CallCount);
                Assert.False(session.IsAuthenticated);
                Assert.Equal(DesktopSessionState.Expired, session.State);
            }
        }

        private static ServiceProvider CreateProvider(FakeMonotonicClock clock)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IMonotonicClock>(clock);
            services.AddSingleton(new DesktopSessionOptions(TimeSpan.FromMinutes(15)));
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddScoped<IUserAccountValidator, ActiveAccountValidator>();
            services.AddTransient<IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>, LoginHandler>();
            return services.BuildServiceProvider();
        }

        private sealed class FakeMonotonicClock : IMonotonicClock
        {
            public TimeSpan Elapsed { get; private set; }
            public void Advance(TimeSpan duration) => Elapsed += duration;
        }

        private sealed class ActiveAccountValidator : IUserAccountValidator
        {
            public Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken) =>
                Task.FromResult(true);
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

            public void CompleteSecondCall() => _secondCall.TrySetResult(true);
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

        private sealed class LoginHandler : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
        {
            public Task<ErrorOr<CurrentUserDto>> Handle(LoginCommand request, CancellationToken cancellationToken) =>
                Task.FromResult<ErrorOr<CurrentUserDto>>(
                    new CurrentUserDto(42, request.Username, "Operator"));
        }
    }
}
