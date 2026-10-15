using System;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class DesktopHostRunnerTests
    {
        [Fact]
        public void Smoke_mode_creates_and_renders_login_without_bypassing_authentication()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var guard = new WinFormsTestGuard())
                    {
                        var session = new SmokeSession();
                        var login = new LoginForm(
                            new LoginViewModel(
                                new SmokeIdentityClient(),
                                new DesktopNotificationCenter()));
                        var shellCreated = false;
                        using (login)
                        {
                            var exitCode = DesktopHostRunner.Run(
                                () => login,
                                () =>
                                {
                                    shellCreated = true;
                                    return new FoundationShellForm(
                                        new FoundationViewModel(
                                            new FoundationShellFormTests.ReadyApiClient()));
                                },
                                session,
                                smokeTest: true);

                            Assert.Equal(0, exitCode);
                            Assert.True(login.IsHandleCreated);
                            Assert.False(shellCreated);
                            Assert.False(session.IsAuthenticated);
                            Assert.Empty(guard.Exceptions);
                        }
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Production_smoke_resolves_login_inside_a_disposed_scope()
        {
            StaTest.Run(
                _ =>
                {
                    using (var guard = new WinFormsTestGuard())
                    {
                        var services = new ServiceCollection();
                        var session = new SmokeSession();
                        var tracker = new ScopedDisposalTracker();
                        services.AddSingleton<IDesktopSession>(session);
                        services.AddScoped(_ => tracker);
                        services.AddScoped(provider =>
                        {
                            provider.GetRequiredService<ScopedDisposalTracker>();
                            return new LoginForm(
                                new LoginViewModel(
                                    new SmokeIdentityClient(),
                                    new DesktopNotificationCenter()));
                        });
                        using (var provider = services.BuildServiceProvider())
                        {
                            var exitCode = DesktopHostRunner.Run(
                                provider.GetRequiredService<IServiceScopeFactory>(),
                                session,
                                smokeTest: true);

                            Assert.Equal(0, exitCode);
                            Assert.True(tracker.IsDisposed);
                            Assert.Empty(guard.Exceptions);
                        }
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        private sealed class SmokeSession : IDesktopSession
        {
            public event EventHandler? SessionChanged;
            public bool IsAuthenticated => false;
            public DesktopSessionState State => DesktopSessionState.Unauthenticated;
            public MyDmsVn.Contracts.CurrentUserDto? CurrentUser => null;
            public long Version => 0;
            public void SignOut() => SessionChanged?.Invoke(this, EventArgs.Empty);
        }

        private sealed class SmokeIdentityClient : IIdentityApiClient
        {
            public System.Threading.Tasks.Task<MyDmsVn.Contracts.ApiResponse<MyDmsVn.Contracts.CurrentUserDto>> LoginAsync(
                MyDmsVn.Contracts.LoginRequest request,
                System.Threading.CancellationToken cancellationToken) =>
                throw new InvalidOperationException("Smoke mode must not authenticate.");

            public System.Threading.Tasks.Task<MyDmsVn.Contracts.ApiResponse<MyDmsVn.Contracts.CurrentUserDto>> GetCurrentUserAsync(
                System.Threading.CancellationToken cancellationToken) =>
                throw new InvalidOperationException("Smoke mode must not authenticate.");
        }

        private sealed class ScopedDisposalTracker : IDisposable
        {
            public bool IsDisposed { get; private set; }
            public void Dispose() => IsDisposed = true;
        }
    }
}
