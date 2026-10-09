using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;
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
                        Assert.Equal(AutoScaleMode.Dpi, shell.AutoScaleMode);
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
                        var firstPage = shell.OpenWorkspace(
                            "home",
                            "Home",
                            first,
                            shell.WorkspaceSessionVersion);
                        var secondPage = shell.OpenWorkspace(
                            "home",
                            "Changed title",
                            duplicate,
                            shell.WorkspaceSessionVersion);

                        Assert.Same(firstPage, secondPage);
                        Assert.Single(shell.Workspace.TabPages);
                        Assert.Same(first, firstPage!.Controls[0]);
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

        [Fact]
        public void Sign_out_and_session_expiry_clear_the_session_with_explicit_status()
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
                        session.SetCurrentUser(new CurrentUserDto(42, "operator", "Operator"));
                        var signedOutContent = new Panel();
                        shell.OpenWorkspace(
                            "private-sign-out",
                            "Private",
                            signedOutContent,
                            shell.WorkspaceSessionVersion);
                        shell.SignOut();

                        Assert.False(session.IsAuthenticated);
                        Assert.Equal("You have been signed out.", shell.StatusText);
                        Assert.Empty(shell.Workspace.TabPages);
                        Assert.True(signedOutContent.IsDisposed);
                        Assert.False(shell.Workspace.Enabled);
                        Assert.False(shell.Navigation.Enabled);

                        session.SetCurrentUser(new CurrentUserDto(42, "operator", "Operator"));
                        Assert.True(shell.Workspace.Enabled);
                        Assert.True(shell.Navigation.Enabled);
                        var expiredContent = new Panel();
                        shell.OpenWorkspace(
                            "private-expired",
                            "Private",
                            expiredContent,
                            shell.WorkspaceSessionVersion);
                        shell.HandleSessionExpired();

                        Assert.False(session.IsAuthenticated);
                        Assert.Equal(
                            "Your session has expired. Please sign in again.",
                            shell.StatusText);
                        Assert.Empty(shell.Workspace.TabPages);
                        Assert.True(expiredContent.IsDisposed);
                        Assert.False(shell.Workspace.Enabled);
                        Assert.False(shell.Navigation.Enabled);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Changing_authenticated_user_closes_the_previous_users_workspace()
        {
            StaTest.Run(
                _ =>
                {
                    var session = new TestDesktopSession();
                    session.SetCurrentUser(new CurrentUserDto(42, "operator", "Operator"));
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(new ReadyApiClient()),
                        session,
                        new DesktopNotificationCenter()))
                    {
                        var previousUsersContent = new Panel();
                        shell.OpenWorkspace(
                            "private",
                            "Private",
                            previousUsersContent,
                            shell.WorkspaceSessionVersion);

                        session.SetCurrentUser(new CurrentUserDto(84, "manager", "Manager"));

                        Assert.Empty(shell.Workspace.TabPages);
                        Assert.True(previousUsersContent.IsDisposed);
                        Assert.Equal("Manager", shell.CurrentUserText);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void OpenWorkspace_rejects_content_after_sign_out()
        {
            StaTest.Run(
                _ =>
                {
                    var session = new TestDesktopSession();
                    session.SetCurrentUser(new CurrentUserDto(42, "operator", "Operator"));
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(new ReadyApiClient()),
                        session,
                        new DesktopNotificationCenter()))
                    {
                        var requestSessionVersion = shell.WorkspaceSessionVersion;
                        session.SignOut();
                        var content = new Panel();

                        var page = shell.OpenWorkspace(
                            "late",
                            "Late",
                            content,
                            requestSessionVersion);

                        Assert.Null(page);
                        Assert.Empty(shell.Workspace.TabPages);
                        Assert.True(content.IsDisposed);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void OpenWorkspace_rejects_a_stale_request_after_another_user_signs_in()
        {
            StaTest.Run(
                _ =>
                {
                    var session = new TestDesktopSession();
                    session.SetCurrentUser(new CurrentUserDto(42, "operator", "Operator"));
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(new ReadyApiClient()),
                        session,
                        new DesktopNotificationCenter()))
                    {
                        var requestSessionVersion = shell.WorkspaceSessionVersion;
                        session.SignOut();
                        session.SetCurrentUser(new CurrentUserDto(84, "manager", "Manager"));
                        var content = new Panel();

                        var page = shell.OpenWorkspace(
                            "late",
                            "Late",
                            content,
                            requestSessionVersion);

                        Assert.Null(page);
                        Assert.Empty(shell.Workspace.TabPages);
                        Assert.True(content.IsDisposed);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Shell_binding_tracks_async_command_busy_and_ready_state()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var apiClient = new DeferredApiClient();
                    using (var guard = new WinFormsTestGuard())
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(apiClient)))
                    {
                        var command = shell.ViewModel.InitializeCommand.ExecuteAsync(null);

                        Assert.True(shell.BusyIndicatorVisible);

                        apiClient.Complete(new FoundationStatus(true, "Local"));
                        command.GetAwaiter().GetResult();

                        Assert.False(shell.BusyIndicatorVisible);
                        Assert.Equal("Ready (Local)", shell.StatusText);
                        Assert.Empty(guard.Exceptions);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Session_and_notification_updates_are_marshaled_to_the_shell_thread()
        {
            StaTest.Run(
                _ =>
                {
                    var uiThreadId = Thread.CurrentThread.ManagedThreadId;
                    var session = new TestDesktopSession();
                    var notifications = new DesktopNotificationCenter();
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(new ReadyApiClient(), notifications),
                        session,
                        notifications))
                    {
                        var handle = shell.Handle;
                        Assert.NotEqual(IntPtr.Zero, handle);
                        var statusStrip = shell.Controls.OfType<StatusStrip>().Single();
                        var labels = statusStrip.Items.OfType<ToolStripStatusLabel>().ToArray();
                        var statusThreadId = 0;
                        var userThreadId = 0;
                        labels[0].TextChanged += (_, _) =>
                            statusThreadId = Thread.CurrentThread.ManagedThreadId;
                        labels[1].TextChanged += (_, _) =>
                            userThreadId = Thread.CurrentThread.ManagedThreadId;

                        Task.Run(
                            () => session.SetCurrentUser(
                                new CurrentUserDto(42, "operator", "Operator")))
                            .GetAwaiter()
                            .GetResult();
                        Task.Run(
                            () => notifications.Publish(
                                new DesktopNotification(
                                    DesktopNotificationKind.Information,
                                    "Background update.")))
                            .GetAwaiter()
                            .GetResult();

                        PumpMessagesUntil(
                            () => statusThreadId != 0 && userThreadId != 0,
                            TimeSpan.FromSeconds(2));

                        Assert.Equal(uiThreadId, statusThreadId);
                        Assert.Equal(uiThreadId, userThreadId);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        private static FoundationShellForm CreateShell()
        {
            return new FoundationShellForm(new FoundationViewModel(new ReadyApiClient()));
        }

        private static void PumpMessagesUntil(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }

            Assert.True(condition(), "Expected UI update did not arrive before the timeout.");
        }

        internal sealed class ReadyApiClient : IFoundationApiClient
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

        private sealed class DeferredApiClient : IFoundationApiClient
        {
            private readonly TaskCompletionSource<ApiResponse<FoundationStatus>> _completion =
                new TaskCompletionSource<ApiResponse<FoundationStatus>>();

            public Task<ApiResponse<FoundationStatus>> GetStatusAsync(
                FoundationStatusRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.Register(() => _completion.TrySetCanceled());
                return _completion.Task;
            }

            public void Complete(FoundationStatus status)
            {
                _completion.SetResult(ApiResponse<FoundationStatus>.Success(status));
            }
        }

        private sealed class TestDesktopSession : IDesktopSession
        {
            public event EventHandler? SessionChanged;

            public bool IsAuthenticated => CurrentUser != null;

            public CurrentUserDto? CurrentUser { get; private set; }

            public long Version { get; private set; }

            public void SignOut()
            {
                SetCurrentUser(null);
            }

            public void SetCurrentUser(CurrentUserDto? currentUser)
            {
                CurrentUser = currentUser;
                Version++;
                SessionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
