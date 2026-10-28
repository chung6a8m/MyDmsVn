using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms;
using MyDmsVn.Desktop.WinForms.Features.Security;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Ui.Tests;

[Trait("Module", "Security")]
public sealed class SecurityProductHostParityTests
{
    private readonly ITestOutputHelper _output;
    public SecurityProductHostParityTests(ITestOutputHelper output) => _output = output;

    // Catches a different shell/Lab palette, lost ReducedMotion, editor replacement on
    // theme switching, or shell chrome taking space from the approved review viewport.
    [Theory]
    [InlineData(1200, 760, 952, 657, BootstrapThemeMode.Light)]
    [InlineData(1200, 760, 952, 657, BootstrapThemeMode.Dark)]
    [InlineData(900, 600, 652, 497, BootstrapThemeMode.Light)]
    [InlineData(900, 600, 652, 497, BootstrapThemeMode.Dark)]
    public void Sidebar_security_matches_direct_lab_content_and_retains_editor_on_theme_toggle(
        int width, int height, int contentWidth, int contentHeight, BootstrapThemeMode mode) => Run((token, guard) =>
    {
        BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode, true);
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var shell = CreateShell(scene);
        shell.Size = new Size(width, height);
        shell.Show();
        var control = OpenThroughSidebar(shell, guard, token);
        var contentSize = new Size(contentWidth, contentHeight);
        _output.WriteLine("Product outer={0}; shell client={1}; Security client={2}; DeviceDpi={3}",
            shell.Size, shell.ClientSize, control.ClientSize, control.DeviceDpi);
        Assert.Equal(new Size(width, height), shell.Size);
        Assert.Equal(contentSize, control.ClientSize);
        AssertPresentation(control, mode);

        using var lab = new SecurityLabForm();
        try
        {
            lab.Show();
            Until(() => lab.CurrentSecurityControl != null && !lab.IsResetting, token);
            var labControl = lab.CurrentSecurityControl!;
            AttachGrids(labControl, guard);
            Assert.IsType<NumericUpDown>(Assert.Single(lab.Controls.Find("ReviewViewportWidth", true))).Value = control.ClientSize.Width;
            Assert.IsType<NumericUpDown>(Assert.Single(lab.Controls.Find("ReviewViewportHeight", true))).Value = control.ClientSize.Height;
            Assert.IsType<Button>(Assert.Single(lab.Controls.Find("ReviewViewportApply", true))).PerformClick();
            Pump(token);
            Assert.Equal(contentSize, labControl.ClientSize);
            AssertPresentation(labControl, mode);
            Assert.Equal(control.UserSearch.Font, labControl.UserSearch.Font);
            Assert.Equal(control.UserDetail.Header.Font, labControl.UserDetail.Header.Font);

            control.RoleSearch.Text = "Kho";
            Assert.True(control.ViewModel.BeginRolePermissions());
            control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
            Assert.True(control.ViewModel.HasDirtyEditor);
            var viewModel = control.ViewModel;
            var editorIdentity = viewModel.EditorIdentity;
            var editorRevision = viewModel.EditorRevision;
            var search = control.RoleSearch;
            var grants = Assert.Single(Children(control.RoleDetail.GrantArea).OfType<BootstrapDataGridView>());
            var labViewModel = labControl.ViewModel;
            for (var index = 0; index < 2; index++)
            {
                shell.ToggleTheme();
                Pump(token);
                var expected = index == 0
                    ? mode == BootstrapThemeMode.Light ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light
                    : mode;
                AssertPresentation(control, expected);
                AssertPresentation(labControl, expected);
                Assert.True(BootstrapThemeManager.CurrentTheme.ReducedMotion);
                Assert.Same(control, Content(Assert.Single(shell.Workspace.TabPages.Cast<TabPage>())));
                Assert.Same(viewModel, control.ViewModel);
                Assert.Same(search, control.RoleSearch);
                Assert.Same(grants, Assert.Single(Children(control.RoleDetail.GrantArea).OfType<BootstrapDataGridView>()));
                Assert.Equal("Kho", search.Text);
                Assert.Equal(editorIdentity, viewModel.EditorIdentity);
                Assert.Equal(editorRevision, viewModel.EditorRevision);
                Assert.True(viewModel.HasDirtyEditor);
                Assert.Same(labControl, lab.CurrentSecurityControl);
                Assert.Same(labViewModel, labControl.ViewModel);
                Assert.Equal(contentSize, control.ClientSize);
                Assert.Equal(contentSize, labControl.ClientSize);
            }
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        }
        finally
        {
            lab.Close();
            Until(() => lab.IsDisposed, token);
        }
    });

    // Catches bypassed close admission, stale document reuse, or missing disposal
    // when the product shell crosses its session boundary.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sidebar_security_close_reopen_and_session_boundaries_own_the_production_control(bool replaceSession) => Run((token, guard) =>
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        using var shell = CreateShell(scene, rebindSession: true);
        shell.Show();
        var first = OpenThroughSidebar(shell, guard, token);
        var firstPage = Assert.Single(shell.Workspace.TabPages.Cast<TabPage>());
        Assert.Same(firstPage, shell.OpenSecurityAdministration());
        Assert.Same(first, Content(firstPage));

        Wait(shell.RequestWorkspaceCloseAsync(firstPage), token);
        Assert.True(first.IsDisposed);
        Assert.False(first.ViewModel.IsCurrent);
        Assert.Empty(shell.Workspace.TabPages);
        Assert.Null(shell.Navigation.SelectedItem);

        var second = OpenThroughSidebar(shell, guard, token);
        Assert.NotSame(first, second);
        Assert.NotSame(first.ViewModel, second.ViewModel);
        Assert.Equal(scene.Session.Version, second.ViewModel.OwnerVersion);
        var secondPage = Assert.Single(shell.Workspace.TabPages.Cast<TabPage>());
        Assert.True(second.ViewModel.BeginRolePermissions());
        second.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
        var identity = second.ViewModel.EditorIdentity;
        shell.ClosePrompt = (_, __) => Task.FromResult(WorkspaceCloseChoice.Cancel);
        Wait(shell.RequestWorkspaceCloseAsync(secondPage), token);
        Assert.False(second.IsDisposed);
        Assert.Same(secondPage, Assert.Single(shell.Workspace.TabPages.Cast<TabPage>()));
        Assert.True(second.ViewModel.HasDirtyEditor);
        Assert.Equal(identity, second.ViewModel.EditorIdentity);
        shell.ClosePrompt = (_, __) => Task.FromResult(WorkspaceCloseChoice.Discard);
        Wait(shell.RequestWorkspaceCloseAsync(secondPage), token);
        Assert.True(second.IsDisposed);
        Assert.Empty(shell.Workspace.TabPages);

        var third = OpenThroughSidebar(shell, guard, token);
        Assert.NotSame(second, third);
        if (replaceSession) scene.Session.Replace();
        else shell.SignOut();
        Pump(token);
        Assert.True(third.IsDisposed);
        Assert.False(third.ViewModel.IsCurrent);
        Assert.Empty(shell.Workspace.TabPages);
        Assert.Equal(scene.Session.Version, shell.WorkspaceSessionVersion);
        var current = third;
        if (replaceSession)
        {
            Assert.Null(shell.Navigation.SelectedItem);
            current = OpenThroughSidebar(shell, guard, token);
            Assert.NotSame(third, current);
            Assert.NotSame(third.ViewModel, current.ViewModel);
            Assert.Equal(scene.Session.Version, current.ViewModel.OwnerVersion);
            Assert.True(current.ViewModel.IsCurrent);
        }
        shell.SignOut();
        Pump(token);
        Assert.True(current.IsDisposed);
        Assert.False(shell.Navigation.Enabled);
        Assert.False(shell.Workspace.Enabled);
        Assert.Null(shell.OpenSecurityAdministration());
        Assert.Empty(shell.Workspace.TabPages);
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });

    [Fact]
    public void Product_dirty_close_uses_native_owned_prompt_and_escape_keeps_workspace_and_draft() => Run((token, guard) =>
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L10);
        using var shell = CreateShell(scene);
        shell.Show();
        var control = OpenThroughSidebar(shell, guard, token);
        var page = Assert.Single(shell.Workspace.TabPages.Cast<TabPage>());
        control.Tabs.SelectedIndex = 1;
        Assert.True(control.ViewModel.BeginRolePermissions());
        control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
        var editorIdentity = control.ViewModel.EditorIdentity;
        Assert.True(control.ViewModel.HasDirtyEditor);
        NativeLookupTypingDriver.Activate(shell);

        var observed = false;
        ExceptionDispatchInfo? failure = null;
        var watch = Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, __) =>
        {
            var dialog = GetForegroundWindow();
            var className = WindowText(dialog, className: true);
            GetWindowThreadProcessId(dialog, out var processId);
            if (dialog == IntPtr.Zero || className != "#32770" || processId != (uint)Process.GetCurrentProcess().Id)
            {
                if (watch.Elapsed < TimeSpan.FromSeconds(5)) return;
                timer.Stop();
                failure = ExceptionDispatchInfo.Capture(new TimeoutException("Native product Close prompt did not appear within 5 seconds."));
                return;
            }
            timer.Stop();
            try
            {
                observed = true;
                Assert.Equal("Close workspace", WindowText(dialog, className: false));
                Assert.Equal(shell.Handle, GetWindow(dialog, 4));
                var focus = GetFocus();
                Assert.NotEqual(IntPtr.Zero, focus);
                Assert.True(IsChild(dialog, focus), "Native Close prompt must own the keyboard focus.");
                _output.WriteLine("PRODUCT_CLOSE owner={0}; title={1}; focused={2}",
                    GetWindow(dialog, 4), WindowText(dialog, false), WindowText(focus, false));
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
            }
            catch (Exception error)
            {
                failure = ExceptionDispatchInfo.Capture(error);
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
            }
        };
        try
        {
            timer.Start();
            Assert.True(shell.Workspace.RequestTabClose(page, TabCloseReason.Keyboard));
            Assert.False(timer.Enabled, "Native product Close prompt did not complete its bounded observation.");
            failure?.Throw();
        }
        finally { timer.Stop(); }

        Assert.True(observed);
        Assert.Same(page, Assert.Single(shell.Workspace.TabPages.Cast<TabPage>()));
        Assert.False(control.IsDisposed);
        Assert.True(control.ViewModel.HasDirtyEditor);
        Assert.Equal(editorIdentity, control.ViewModel.EditorIdentity);
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });

    // Catches a dialog attached to a helper/Lab window, stale theme subscriptions,
    // or account secrets surviving an ordinary canceled product modal.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Product_account_modal_keeps_shell_ownership_and_live_theme_until_canceled(bool resetPassword) => Run((token, guard) =>
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L03);
        using var shell = CreateShell(scene);
        shell.Show();
        var control = OpenThroughSidebar(shell, guard, token);
        var command = Assert.Single(control.UserDetail.HeaderActions.Controls.OfType<Button>(), button =>
            button.Text == (resetPassword ? "Đặt lại mật khẩu…" : "Tạo người dùng…"));
        Form? observed = null;
        BootstrapTextBox? secret = null;
        ExceptionDispatchInfo? failure = null;
        var watch = Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, __) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Modal &&
                (resetPassword ? form.Text.StartsWith("Đặt lại mật khẩu — ", StringComparison.Ordinal) : form.Text == "Tạo người dùng"));
            if (dialog == null && watch.Elapsed < TimeSpan.FromSeconds(5) && !token.IsCancellationRequested) return;
            timer.Stop();
            try
            {
                Assert.NotNull(dialog);
                observed = dialog!;
                Assert.Same(shell, observed.Owner);
                Assert.False(IsWindowEnabled(shell.Handle), "The product owner HWND must be disabled while its modal is open.");
                var fields = Children(observed).OfType<BootstrapTextBox>().ToArray();
                secret = Assert.Single(fields, field => field.UseSystemPasswordChar);
                secret.Text = "cancel-only-secret";
                var viewModel = control.ViewModel;
                foreach (var expected in new[] { BootstrapThemeMode.Dark, BootstrapThemeMode.Light })
                {
                    shell.ToggleTheme();
                    Pump(token);
                    Assert.Equal(expected, BootstrapThemeManager.CurrentTheme.Mode);
                    Assert.True(BootstrapThemeManager.CurrentTheme.ReducedMotion);
                    Assert.Equal(expected == BootstrapThemeMode.Dark ? Color.FromArgb(22, 31, 41) : Color.FromArgb(245, 247, 251), observed.BackColor);
                    Assert.Equal(expected == BootstrapThemeMode.Dark ? Color.FromArgb(29, 39, 51) : Color.White, secret.BackColor);
                    Assert.Equal("Segoe UI", secret.Font.Name);
                    Assert.Equal(10f, secret.Font.SizeInPoints);
                    Assert.Equal("cancel-only-secret", secret.Text);
                    Assert.Same(secret, Assert.Single(Children(observed).OfType<BootstrapTextBox>(), field => field.UseSystemPasswordChar));
                    Assert.Same(viewModel, control.ViewModel);
                    Assert.Same(shell, observed.Owner);
                }
                Assert.IsType<BootstrapButton>(observed.CancelButton).PerformClick();
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            finally
            {
                dialog?.Close();
                foreach (var owned in shell.OwnedForms.Where(form => form.Modal).ToArray()) owned.Close();
            }
        };
        try
        {
            timer.Start();
            command.PerformClick();
            Assert.False(timer.Enabled, "The product command did not open its modal.");
            failure?.Throw();
            Assert.NotNull(observed);
            Assert.True(observed!.IsDisposed);
            Assert.NotNull(secret);
            Assert.True(secret!.IsDisposed);
            Assert.Empty(secret.Text);
            Assert.True(IsWindowEnabled(shell.Handle));
            Assert.False(control.IsDisposed);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        }
        finally { timer.Stop(); }
    });

    private static FoundationShellForm CreateShell(SecurityScenario scene, bool rebindSession = false)
    {
        var dispatcher = new SynchronizationContextUiDispatcher(SynchronizationContext.Current ?? throw new InvalidOperationException("A UI context is required."));
        var other = new RejectUnrelatedFactories();
        var notifications = new DesktopNotificationCenter();
        var root = new SessionBindingSecurityPorts(scene);
        ISecurityAdministrationApiClient administration = rebindSession ? root : scene.Administration;
        IPermissionCatalogApiClient catalog = rebindSession ? root : scene.Catalog;
        return new FoundationShellForm(new FoundationViewModel(new ReadyFoundationApi(), notifications), scene.Session,
            notifications, other, other, other,
            new SecurityAdministrationControlFactory(administration, catalog, scene.Session, dispatcher), other);
    }

    private static SecurityWorkspaceControl OpenThroughSidebar(FoundationShellForm shell, WinFormsTestGuard guard, CancellationToken token)
    {
        shell.Navigation.SelectedItem = Assert.Single(shell.Navigation.Items,
            item => Equals(item.Tag, "security-administration"));
        var page = Assert.Single(shell.Workspace.TabPages.Cast<TabPage>());
        Assert.Equal("security-administration", page.Name);
        Assert.True(shell.Workspace.IsTabClosable(page));
        var control = Content(page);
        AttachGrids(control, guard);
        Wait(control.LastOperation, token);
        Assert.True(control.ViewModel.IsCurrent);
        Assert.NotEmpty(control.Users.Items.Cast<ListViewItem>());
        Assert.NotEmpty(control.Roles.Items.Cast<ListViewItem>());
        return control;
    }

    private static SecurityWorkspaceControl Content(TabPage page) => Assert.IsType<SecurityWorkspaceControl>(
        Assert.IsType<WorkspaceFeedbackHost>(Assert.Single(page.Controls.Cast<Control>())).Content);

    private static void AttachGrids(Control control, WinFormsTestGuard guard)
    {
        foreach (var grid in Children(control).OfType<DataGridView>()) guard.Attach(grid);
    }

    private static void AssertPresentation(SecurityWorkspaceControl control, BootstrapThemeMode mode)
    {
        Assert.Equal(mode, BootstrapThemeManager.CurrentTheme.Mode);
        Assert.True(BootstrapThemeManager.CurrentTheme.ReducedMotion);
        Assert.Equal(mode == BootstrapThemeMode.Dark ? Color.FromArgb(81, 160, 255) : Color.FromArgb(18, 98, 239), BootstrapThemeManager.CurrentTheme.Colors.Primary);
        Assert.Equal(mode == BootstrapThemeMode.Dark ? Color.FromArgb(22, 31, 41) : Color.FromArgb(245, 247, 251), control.BackColor);
        Assert.Equal(mode == BootstrapThemeMode.Dark ? Color.FromArgb(29, 39, 51) : Color.White, control.UserSearch.BackColor);
        Assert.Equal("Segoe UI", control.UserSearch.Font.Name);
        Assert.Equal(10f, control.UserSearch.Font.SizeInPoints);
        Assert.Equal("Segoe UI", control.UserDetail.Header.Font.Name);
        Assert.Equal(13f, control.UserDetail.Header.Font.SizeInPoints);
        Assert.True(control.UserDetail.Header.Font.Bold);
    }

    private static IEnumerable<Control> Children(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Children(child)) yield return descendant;
        }
    }

    private static void Pump(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        System.Windows.Forms.Application.DoEvents();
    }

    private static void Until(Func<bool> condition, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(8)) throw new TimeoutException("The product/Lab operation did not settle.");
            Pump(token);
            Thread.Sleep(10);
        }
        Pump(token);
    }

    private static void Wait(Task task, CancellationToken token)
    {
        Until(() => task.IsCompleted, token);
        task.GetAwaiter().GetResult();
    }

    private static void Run(Action<CancellationToken, WinFormsTestGuard> action) => StaTest.Run(token =>
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        using var guard = new WinFormsTestGuard();
        var prior = BootstrapThemeManager.CurrentTheme;
        ExceptionDispatchInfo? failure = null;
        using var loop = new ApplicationContext();
        using var startup = new Control();
        startup.CreateControl();
        startup.BeginInvoke(new Action(() =>
        {
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(BootstrapThemeMode.Light, true);
                action(token, guard);
                Assert.Empty(guard.Exceptions);
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            finally { BootstrapThemeManager.CurrentTheme = prior; loop.ExitThread(); }
        }));
        System.Windows.Forms.Application.Run(loop);
        failure?.Throw();
    }, TimeSpan.FromSeconds(30));

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr window);

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetFocus();
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int count);

    private static string WindowText(IntPtr window, bool className)
    {
        var text = new StringBuilder(256);
        if (className) GetClassName(window, text, text.Capacity);
        else GetWindowText(window, text, text.Capacity);
        return text.ToString();
    }

    private sealed class ReadyFoundationApi : IFoundationApiClient
    {
        public Task<ApiResponse<FoundationStatus>> GetStatusAsync(FoundationStatusRequest request, CancellationToken token) =>
            Task.FromResult(ApiResponse<FoundationStatus>.Success(new FoundationStatus(true, "SQL-free product host")));
    }

    // Mirrors production root ports: only ForSession admits calls. Each workspace
    // captures its own bound ports while the fixture data survives session replacement.
    private sealed class SessionBindingSecurityPorts : ISecurityAdministrationApiClient, IPermissionCatalogApiClient
    {
        private readonly SecurityScenario _scene;
        internal SessionBindingSecurityPorts(SecurityScenario scene) => _scene = scene;

        ISecurityAdministrationApiClient ISecurityAdministrationApiClient.ForSession(long expectedSessionVersion) =>
            new ScenarioSecurityAdministrationApiClient(_scene.Data, _scene.Session, expectedSessionVersion, _scene.Journal);
        IPermissionCatalogApiClient IPermissionCatalogApiClient.ForSession(long expectedSessionVersion) =>
            new ScenarioPermissionCatalogApiClient(_scene.Data, _scene.Session, expectedSessionVersion, _scene.Journal);

        public Task<ApiResponse<IReadOnlyList<SecurityUserDto>>> ListUsersAsync(CancellationToken token) => throw Unbound();
        public Task<ApiResponse<IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(CancellationToken token) => throw Unbound();
        public Task<ApiResponse<PermissionCatalogDto>> GetCatalogAsync(CancellationToken token) => throw Unbound();
        public Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(SecurityAdministrationMutationRequest request, CancellationToken token) => throw Unbound();
        public Task<ApiResponse<PermissionAssignmentImportPreviewDto>> PreviewPermissionImportAsync(PermissionImportTargetTypeDto targetType, int targetId, string json, CancellationToken token) => throw Unbound();
        public Task<ApiResponse<PermissionAssignmentImportResultDto>> ApplyPermissionImportAsync(PermissionImportTargetTypeDto targetType, int targetId, PermissionAssignmentImportRequest request, CancellationToken token) => throw Unbound();
        private static InvalidOperationException Unbound() => new InvalidOperationException("Security root ports must be bound through ForSession.");
    }

    private sealed class RejectUnrelatedFactories : ICatalogControlFactory, IGoodsReceiptControlFactory, IInventoryControlFactory, ISalesReceiptControlFactory
    {
        Control ICatalogControlFactory.Create(CatalogKind kind) => throw new InvalidOperationException("Security navigation constructed Catalog.");
        Control IGoodsReceiptControlFactory.Create() => throw new InvalidOperationException("Security navigation constructed Goods Receipts.");
        Control IInventoryControlFactory.CreateStockBalances() => throw new InvalidOperationException("Security navigation constructed Stock Balances.");
        Control IInventoryControlFactory.CreateStockCard() => throw new InvalidOperationException("Security navigation constructed Stock Card.");
        Control ISalesReceiptControlFactory.Create() => throw new InvalidOperationException("Security navigation constructed Sales.");
    }
}
