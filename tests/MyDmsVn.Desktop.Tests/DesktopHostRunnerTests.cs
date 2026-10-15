using System;
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
                _ =>
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
    }
}
