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
                    {
                        var duplicate = new Panel();
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
                        Assert.True(duplicate.IsDisposed);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Inventory_navigation_opens_and_reuses_balance_and_stock_card_workspaces()
        {
            StaTest.Run(
                _ =>
                {
                    var session = new TestDesktopSession();
                    session.SetCurrentUser(new CurrentUserDto(42, "operator", "Operator"));
                    var notifications = new DesktopNotificationCenter();
                    var inventory = new RecordingInventoryControlFactory();
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(new ReadyApiClient(), notifications),
                        session,
                        notifications,
                        new EmptyCatalogControlFactory(),
                        new RecordingGoodsReceiptControlFactory(),
                        inventory))
                    {
                        var balances = shell.OpenStockBalances();
                        var balancesAgain = shell.OpenStockBalances();
                        var stockCard = shell.OpenStockCard();
                        var stockCardAgain = shell.OpenStockCard();

                        Assert.Same(balances, balancesAgain);
                        Assert.Same(stockCard, stockCardAgain);
                        Assert.Equal("stock-balances", balances!.Name);
                        Assert.Equal("Tồn kho", balances.Text);
                        Assert.Equal("stock-card", stockCard!.Name);
                        Assert.Equal("Thẻ kho", stockCard.Text);
                        Assert.Equal(1, inventory.BalanceCreateCount);
                        Assert.Equal(1, inventory.CardCreateCount);
                        Assert.Contains(
                            shell.Navigation.Items.Cast<object>(),
                            item => item is MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSidebarItem sidebarItem &&
                                string.Equals(sidebarItem.Tag as string, "stock-balances", StringComparison.Ordinal));
                        Assert.Contains(
                            shell.Navigation.Items.Cast<object>(),
                            item => item is MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSidebarItem sidebarItem &&
                                string.Equals(sidebarItem.Tag as string, "stock-card", StringComparison.Ordinal));
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
                            shell.Show();
                            System.Windows.Forms.Application.DoEvents();
                            var toggle = shell.Controls.OfType<ToolStrip>()
                                .SelectMany(strip => strip.Items.OfType<ToolStripControlHost>())
                                .Select(host => host.Control).OfType<Button>()
                                .Single(button => button.AccessibleName == "Theme toggle");
                            Assert.True(toggle.IsHandleCreated);
                            Assert.Equal(AccessibleRole.PushButton, toggle.AccessibleRole);
                            Assert.Equal("Current theme: Light", toggle.AccessibleDescription);
                            shell.ToggleTheme();

                            Assert.Equal(
                                BootstrapThemeMode.Dark,
                                BootstrapThemeManager.CurrentTheme.Mode);
                              Assert.Equal("Current theme: Dark", toggle.AccessibilityObject.Description);
                              toggle.PerformClick();
                              Assert.Equal(BootstrapThemeMode.Light, BootstrapThemeManager.CurrentTheme.Mode);
                              Assert.Equal("Current theme: Light", toggle.AccessibilityObject.Description);
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

        [Fact]
        public void Session_change_before_handle_creation_is_reconciled_when_handle_is_created()
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
                        Assert.False(shell.IsHandleCreated);
                        var content = new Panel();
                        shell.OpenWorkspace(
                            "private",
                            "Private",
                            content,
                            shell.WorkspaceSessionVersion);

                        Task.Run(session.SignOut).GetAwaiter().GetResult();
                        Assert.False(shell.IsHandleCreated);

                        Assert.NotEqual(IntPtr.Zero, shell.Handle);

                        Assert.Empty(shell.Workspace.TabPages);
                        Assert.True(content.IsDisposed);
                        Assert.False(shell.Navigation.Enabled);
                        Assert.False(shell.Workspace.Enabled);
                        Assert.Equal("Not signed in", shell.CurrentUserText);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Ready_view_model_state_before_handle_creation_is_reconciled()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var apiClient = new DeferredApiClient();
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(apiClient)))
                    {
                        var initialization = Task.Run(
                            () => shell.ViewModel.InitializeAsync(cancellationToken));
                        Assert.True(
                            apiClient.Started.Wait(TimeSpan.FromSeconds(2)),
                            "Foundation request did not start before the timeout.");
                        apiClient.Complete(new FoundationStatus(true, "Background"));
                        initialization.GetAwaiter().GetResult();
                        Assert.False(shell.IsHandleCreated);

                        Assert.NotEqual(IntPtr.Zero, shell.Handle);

                        Assert.False(shell.BusyIndicatorVisible);
                        Assert.Equal("Ready (Background)", shell.StatusText);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Busy_view_model_state_before_handle_creation_is_reconciled()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var apiClient = new DeferredApiClient();
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(apiClient)))
                    {
                        var initialization = Task.Run(
                            () => shell.ViewModel.InitializeAsync(cancellationToken));
                        Assert.True(
                            apiClient.Started.Wait(TimeSpan.FromSeconds(2)),
                            "Foundation request did not start before the timeout.");
                        Assert.False(shell.IsHandleCreated);

                        try
                        {
                            Assert.NotEqual(IntPtr.Zero, shell.Handle);
                            Assert.True(shell.BusyIndicatorVisible);
                        }
                        finally
                        {
                            apiClient.Complete(new FoundationStatus(true, "Background"));
                            initialization.GetAwaiter().GetResult();
                            System.Windows.Forms.Application.DoEvents();
                        }
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Notification_before_handle_creation_is_reconciled()
        {
            StaTest.Run(
                _ =>
                {
                    var notifications = new DesktopNotificationCenter();
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(new ReadyApiClient(), notifications),
                        new TestDesktopSession(),
                        notifications))
                    {
                        Task.Run(
                                () => notifications.Publish(
                                    new DesktopNotification(
                                        DesktopNotificationKind.Error,
                                        "Background failure.")))
                            .GetAwaiter()
                            .GetResult();
                        Assert.False(shell.IsHandleCreated);

                        Assert.NotEqual(IntPtr.Zero, shell.Handle);

                        Assert.Equal("Background failure.", shell.StatusText);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Goods_receipt_navigation_reuses_one_workspace_and_sign_out_disposes_it()
        {
            StaTest.Run(
                _ =>
                {
                    var session = new TestDesktopSession();
                    session.SetCurrentUser(new CurrentUserDto(7, "operator", "Operator"));
                    var notifications = new DesktopNotificationCenter();
                    var factory = new RecordingGoodsReceiptControlFactory();
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(new ReadyApiClient(), notifications),
                        session,
                        notifications,
                        new EmptyCatalogControlFactory(),
                        factory))
                    {
                        var first = shell.OpenGoodsReceipts();
                        var second = shell.OpenGoodsReceipts();

                        Assert.Same(first, second);
                        Assert.Equal("goods-receipts", first!.Name);
                        Assert.Equal("Goods Receipts", first.Text);
                        Assert.Equal(1, factory.CreateCount);
                        Assert.Contains(
                            shell.Navigation.Items.Cast<object>(),
                            item => item is MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSidebarItem sidebarItem &&
                                sidebarItem.Text == "Goods Receipts");

                        shell.SignOut();

                        Assert.Empty(shell.Workspace.TabPages);
                        Assert.True(factory.CreatedControl!.IsDisposed);
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

            public ManualResetEventSlim Started { get; } = new ManualResetEventSlim();

            public Task<ApiResponse<FoundationStatus>> GetStatusAsync(
                FoundationStatusRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.Register(() => _completion.TrySetCanceled());
                Started.Set();
                return _completion.Task;
            }

            public void Complete(FoundationStatus status)
            {
                _completion.SetResult(ApiResponse<FoundationStatus>.Success(status));
            }
        }

        private sealed class EmptyCatalogControlFactory : ICatalogControlFactory
        {
            public Control Create(CatalogKind kind) => new Panel();
        }

        private sealed class RecordingGoodsReceiptControlFactory : IGoodsReceiptControlFactory
        {
            public int CreateCount { get; private set; }
            public Control? CreatedControl { get; private set; }

            public Control Create()
            {
                CreateCount++;
                CreatedControl = new Panel();
                return CreatedControl;
            }
        }

        private sealed class RecordingInventoryControlFactory : IInventoryControlFactory
        {
            public int BalanceCreateCount { get; private set; }
            public int CardCreateCount { get; private set; }

            public Control CreateStockBalances()
            {
                BalanceCreateCount++;
                return new Panel();
            }

            public Control CreateStockCard()
            {
                CardCreateCount++;
                return new Panel();
            }
        }

        private sealed class TestDesktopSession : IDesktopSession
        {
            public event EventHandler? SessionChanged;

            public bool IsAuthenticated => CurrentUser != null;

            public DesktopSessionState State => IsAuthenticated
                ? DesktopSessionState.Authenticated
                : DesktopSessionState.Unauthenticated;

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
