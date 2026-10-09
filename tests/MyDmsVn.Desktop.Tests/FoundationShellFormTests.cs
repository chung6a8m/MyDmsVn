using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class FoundationShellFormTests
    {
        [Fact]
        public void Shell_exposes_navigation_workspace_and_session_status()
        {
            StaTest.Run(
                _ =>
                {
                    using (var shell = CreateShell())
                    {
                        Assert.NotEmpty(shell.Navigation.Items);
                        Assert.Empty(shell.Workspace.TabPages);
                        Assert.Equal("Ready", shell.StatusText);
                        Assert.Equal("Not signed in", shell.CurrentUserText);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void OpenWorkspace_reuses_existing_document_for_the_same_key()
        {
            StaTest.Run(
                _ =>
                {
                    using (var shell = CreateShell())
                    using (var first = new Panel())
                    using (var duplicate = new Panel())
                    {
                        var firstPage = shell.OpenWorkspace("home", "Home", first);
                        var secondPage = shell.OpenWorkspace("home", "Changed title", duplicate);

                        Assert.Same(firstPage, secondPage);
                        Assert.Single(shell.Workspace.TabPages);
                        Assert.Same(first, firstPage.Controls[0]);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void ToggleTheme_switches_between_light_and_dark()
        {
            StaTest.Run(
                _ =>
                {
                    var original = BootstrapThemeManager.CurrentTheme;
                    try
                    {
                        BootstrapThemeManager.CurrentTheme =
                            BootstrapTheme.CreateDefault(BootstrapThemeMode.Light);
                        using (var shell = CreateShell())
                        {
                            shell.ToggleTheme();

                            Assert.Equal(
                                BootstrapThemeMode.Dark,
                                BootstrapThemeManager.CurrentTheme.Mode);
                        }
                    }
                    finally
                    {
                        BootstrapThemeManager.CurrentTheme = original;
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Shell_reflects_shared_session_and_notification_changes()
        {
            StaTest.Run(
                _ =>
                {
                    var session = new TestDesktopSession();
                    var notifications = new DesktopNotificationCenter();
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(new ReadyApiClient(), notifications),
                        session,
                        notifications))
                    {
                        session.SetCurrentUser(
                            new CurrentUserDto(42, "operator", "Warehouse Operator"));
                        notifications.Publish(
                            new DesktopNotification(
                                DesktopNotificationKind.Success,
                                "Signed in."));

                        Assert.Equal("Warehouse Operator", shell.CurrentUserText);
                        Assert.Equal("Signed in.", shell.StatusText);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        private static FoundationShellForm CreateShell()
        {
            return new FoundationShellForm(new FoundationViewModel(new ReadyApiClient()));
        }

        private sealed class ReadyApiClient : IFoundationApiClient
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

        private sealed class TestDesktopSession : IDesktopSession
        {
            public event EventHandler? SessionChanged;

            public bool IsAuthenticated => CurrentUser != null;

            public CurrentUserDto? CurrentUser { get; private set; }

            public void SignOut()
            {
                SetCurrentUser(null);
            }

            public void SetCurrentUser(CurrentUserDto? currentUser)
            {
                CurrentUser = currentUser;
                SessionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
