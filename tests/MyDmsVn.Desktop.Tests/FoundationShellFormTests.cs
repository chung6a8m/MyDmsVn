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
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class FoundationShellFormTests
    {
        private readonly ITestOutputHelper _output;

        public FoundationShellFormTests(ITestOutputHelper output) => _output = output;

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
                        Assert.Same(first, Assert.IsType<WorkspaceFeedbackHost>(firstPage!.Controls[0]).Content);
                        Assert.True(duplicate.IsDisposed);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Tab_close_request_removes_only_the_originating_page_and_disposes_it_once()
        {
            StaTest.Run(_ =>
            {
                using var shell = CreateShell();
                var firstContent = new Panel();
                var secondContent = new Panel();
                var first = shell.OpenWorkspace("first", "First", firstContent, shell.WorkspaceSessionVersion)!;
                var second = shell.OpenWorkspace("second", "Second", secondContent, shell.WorkspaceSessionVersion)!;
                Assert.True(shell.Workspace.IsTabClosable(first));
                Assert.True(shell.Workspace.RequestTabClose(first));

                Assert.Single(shell.Workspace.TabPages);
                Assert.Same(second, shell.Workspace.TabPages[0]);
                Assert.True(first.IsDisposed);
                Assert.True(firstContent.IsDisposed);
                Assert.False(secondContent.IsDisposed);
                Assert.Same(second, shell.OpenWorkspace("second", "Second", new Panel(), shell.WorkspaceSessionVersion));
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Security_tab_rejects_close_without_dirty_and_busy_admission()
        {
            StaTest.Run(_ =>
            {
                using var shell = CreateShell();
                var editor = new TextBox { Text = "unsaved role" };
                var page = shell.OpenWorkspace("security-administration", "Security", editor, shell.WorkspaceSessionVersion)!;
                Assert.False(shell.Workspace.IsTabClosable(page));
                Assert.False(shell.Workspace.RequestTabClose(page));
                shell.RequestWorkspaceCloseAsync(page).GetAwaiter().GetResult();
                Assert.False(editor.IsDisposed);
                Assert.Equal("unsaved role", editor.Text);
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Closing_selected_workspace_allows_the_same_sidebar_item_to_open_it_again()
        {
            StaTest.Run(_ =>
            {
                var session = new TestDesktopSession();
                session.SetCurrentUser(new CurrentUserDto(7, "operator", "Operator"));
                var notifications = new DesktopNotificationCenter();
                var factory = new RecordingGoodsReceiptControlFactory();
                using var shell = new FoundationShellForm(new FoundationViewModel(new ReadyApiClient(), notifications),
                    session, notifications, new EmptyCatalogControlFactory(), factory);
                var item = shell.Navigation.Items.Cast<BusinessCompactNavigationItem>()
                    .Single(candidate => string.Equals(candidate.Tag as string, "goods-receipts", StringComparison.Ordinal));
                shell.Navigation.SelectedItem = item;
                var first = Assert.Single(shell.Workspace.TabPages.Cast<TabPage>());
                Assert.True(shell.Workspace.RequestTabClose(first));
                Assert.Empty(shell.Workspace.TabPages);
                Assert.Null(shell.Navigation.SelectedItem);

                shell.Navigation.SelectedItem = item;
                Assert.Single(shell.Workspace.TabPages);
                Assert.Equal(2, factory.CreateCount);
            }, TimeSpan.FromSeconds(10));
        }

        [Theory]
        [InlineData(BootstrapThemeMode.Light)]
        [InlineData(BootstrapThemeMode.Dark)]
        public void Products_navigation_keeps_sidebar_vertical_only_through_open_close_and_reopen(BootstrapThemeMode mode)
        {
            StaTest.Run(token =>
            {
                var original = BootstrapThemeManager.CurrentTheme;
                try
                {
                    BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                    using var guard = new WinFormsTestGuard();
                    var session = new TestDesktopSession();
                    session.SetCurrentUser(new CurrentUserDto(7, "operator", "Operator"));
                    var notifications = new DesktopNotificationCenter();
                    var factory = new RecordingCatalogControlFactory();
                    using var shell = new FoundationShellForm(new FoundationViewModel(new ReadyApiClient(), notifications),
                        session, notifications, factory);
                    shell.Show();
                    BusinessCompactNavigationTests.Pump();
                    _output.WriteLine(UiNextNativeScaleObservation.Observe(shell, token));
                    var host = shell.Navigation.Controls.OfType<FlowLayoutPanel>().Single();
                    _output.WriteLine($"normal outer={shell.Size}; client={shell.ClientSize}; navigation={shell.Navigation.Bounds}; host={host.ClientSize}; footer={shell.Navigation.SessionArea.Bounds}");
                    BusinessCompactNavigationTests.AssertHorizontalFit(host);
                    shell.Size = new System.Drawing.Size(900, 600);
                    BusinessCompactNavigationTests.Pump();
                    _output.WriteLine($"constrained outer={shell.Size}; client={shell.ClientSize}; navigation={shell.Navigation.Bounds}; host={host.ClientSize}; footer={shell.Navigation.SessionArea.Bounds}");
                    var sidebarWidth = shell.Navigation.Width;
                    var products = shell.Navigation.Items.Cast<BusinessCompactNavigationItem>()
                        .Single(item => item.Tag is CatalogKind kind && kind == CatalogKind.Product);
                    Assert.Empty(shell.Workspace.TabPages);
                    Assert.True(host.VerticalScroll.Visible);
                    BusinessCompactNavigationTests.AssertHorizontalFit(host);

                    shell.Navigation.SelectedItem = products;
                    BusinessCompactNavigationTests.Pump();
                    var first = Assert.Single(shell.Workspace.TabPages.Cast<TabPage>());
                    Assert.Equal("catalog-product", first.Name);
                    Assert.Equal(1, factory.CreateCount);
                    BusinessCompactNavigationTests.AssertHorizontalFit(host);
                    var duplicate = shell.OpenCatalog(CatalogKind.Product);
                    Assert.Same(first, duplicate);
                    Assert.Equal(1, factory.CreateCount);
                    shell.OpenWorkspace("second", "Second", new Panel(), shell.WorkspaceSessionVersion);
                    BusinessCompactNavigationTests.Pump();
                    BusinessCompactNavigationTests.AssertHorizontalFit(host);
                    Assert.True(shell.Workspace.RequestTabClose(first));
                    BusinessCompactNavigationTests.Pump();
                    Assert.True(first.IsDisposed);
                    Assert.Null(shell.Navigation.SelectedItem);
                    BusinessCompactNavigationTests.AssertHorizontalFit(host);
                    shell.Navigation.SelectedItem = products;
                    BusinessCompactNavigationTests.Pump();
                    Assert.Equal(2, factory.CreateCount);
                    Assert.Equal("catalog-product", shell.Workspace.SelectedTab!.Name);
                    Assert.Equal(sidebarWidth, shell.Navigation.Width);
                    BusinessCompactNavigationTests.AssertHorizontalFit(host);
                    Assert.Empty(guard.Exceptions);
                }
                finally { BootstrapThemeManager.CurrentTheme = original; }
            }, TimeSpan.FromSeconds(25));
        }

        [Fact]
        public void Disposing_shell_twice_after_workspace_cleanup_is_idempotent()
        {
            StaTest.Run(_ =>
            {
                var shell = CreateShell();
                var content = new Panel();
                shell.OpenWorkspace("first", "First", content, shell.WorkspaceSessionVersion);
                shell.Dispose();
                shell.Dispose();
                Assert.True(content.IsDisposed);
            }, TimeSpan.FromSeconds(10));
        }

        [Theory]
        [InlineData(BootstrapThemeMode.Light)]
        [InlineData(BootstrapThemeMode.Dark)]
        public void Native_close_request_keeps_other_tab_and_closes_last_tab_in_both_themes(BootstrapThemeMode mode)
        {
            StaTest.Run(token =>
            {
                var original = BootstrapThemeManager.CurrentTheme;
                try
                {
                    BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                    using var guard = new WinFormsTestGuard();
                    using var shell = CreateShell();
                    shell.Show();
                    _output.WriteLine(UiNextNativeScaleObservation.Observe(shell, token));
                    var firstContent = new TextBox();
                    var secondContent = new TextBox();
                    var first = shell.OpenWorkspace("first", "First", firstContent, shell.WorkspaceSessionVersion)!;
                    var second = shell.OpenWorkspace("second", "Second", secondContent, shell.WorkspaceSessionVersion)!;
                    Assert.True(shell.Workspace.RequestTabClose(first));
                    Assert.Single(shell.Workspace.TabPages);
                    Assert.Same(second, shell.Workspace.SelectedTab);
                    Assert.True(firstContent.IsDisposed);
                    Assert.True(shell.Workspace.RequestTabClose(second));
                    Assert.Empty(shell.Workspace.TabPages);
                    Assert.True(secondContent.IsDisposed);
                    Assert.Empty(guard.Exceptions);
                }
                finally { BootstrapThemeManager.CurrentTheme = original; }
            }, TimeSpan.FromSeconds(20));
        }

        [Fact]
        public void Delayed_close_prompt_cannot_remove_reopened_page_after_session_change()
        {
            StaTest.Run(_ =>
            {
                var session = new TestDesktopSession();
                session.SetCurrentUser(new CurrentUserDto(42, "operator", "Operator"));
                var notifications = new DesktopNotificationCenter();
                using var shell = new FoundationShellForm(new FoundationViewModel(new ReadyApiClient(), notifications), session, notifications);
                var answer = new TaskCompletionSource<WorkspaceCloseChoice>();
                shell.ClosePrompt = (_, _) => answer.Task;
                var oldContent = new DirtyCloseContent();
                var oldPage = shell.OpenWorkspace("same", "Old", oldContent, shell.WorkspaceSessionVersion)!;
                Assert.True(shell.Workspace.RequestTabClose(oldPage));
                session.SignOut();
                Assert.True(oldPage.IsDisposed);
                session.SetCurrentUser(new CurrentUserDto(43, "new-operator", "New Operator"));
                var newContent = new Panel();
                var newPage = shell.OpenWorkspace("same", "New", newContent, shell.WorkspaceSessionVersion)!;
                answer.SetResult(WorkspaceCloseChoice.Discard);
                System.Windows.Forms.Application.DoEvents();
                Assert.Single(shell.Workspace.TabPages);
                Assert.Same(newPage, shell.Workspace.TabPages[0]);
                Assert.False(newContent.IsDisposed);
                Assert.Equal(1, oldContent.DisposeCount);
            }, TimeSpan.FromSeconds(10));
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
                            item => item is BusinessCompactNavigationItem sidebarItem &&
                                string.Equals(sidebarItem.Tag as string, "stock-balances", StringComparison.Ordinal));
                        Assert.Contains(
                            shell.Navigation.Items.Cast<object>(),
                            item => item is BusinessCompactNavigationItem sidebarItem &&
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
                            var toggle = shell.Navigation.SessionArea.Controls.OfType<Button>()
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
                            item => item is BusinessCompactNavigationItem sidebarItem &&
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

        private sealed class RecordingCatalogControlFactory : ICatalogControlFactory
        {
            public int CreateCount { get; private set; }
            public Control Create(CatalogKind kind)
            {
                Assert.Equal(CatalogKind.Product, kind);
                CreateCount++;
                return new Panel();
            }
        }

        private sealed class DirtyCloseContent : Panel, IWorkspaceCloseParticipant
        {
            private readonly Guid _identity = Guid.NewGuid();
            public int DisposeCount { get; private set; }
            public WorkspaceCloseState CaptureCloseState() => new WorkspaceCloseState(_identity, 1, true, false, false, true);
            public Task<WorkspaceSaveOutcome> SaveForCloseAsync(CancellationToken token) =>
                Task.FromResult(new WorkspaceSaveOutcome(WorkspaceSaveStatus.Rejected, _identity, 1));
            protected override void Dispose(bool disposing)
            {
                if (disposing && !IsDisposed) DisposeCount++;
                base.Dispose(disposing);
            }
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
