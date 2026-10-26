using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms.Features.Security;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests;

public sealed class SecurityNativeAndResetTests
{
    readonly ITestOutputHelper output;

    public SecurityNativeAndResetTests(ITestOutputHelper output) => this.output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_tab_moves_from_search_through_filter_to_master_list(bool roles) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, _) =>
        {
            if (roles) control.Tabs.SelectedIndex = 1;
            var form = control.FindForm()!;
            NativeLookupTypingDriver.Activate(form);
            var search = roles ? control.RoleSearch : control.UserSearch;
            var activity = roles ? control.RoleActivity : control.UserActivity;
            var list = roles ? control.Roles : control.Users;
            Assert.True(search.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.True(activity.ContainsFocus, "Tab from search should focus the activity filter.");
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.True(list.ContainsFocus, "Tab from activity filter should focus the master list.");
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_enter_activates_explicit_create_without_mutating_on_cancel(bool roles) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            if (roles) control.Tabs.SelectedIndex = 1;
            var form = control.FindForm()!;
            NativeLookupTypingDriver.Activate(form);
            var detail = roles ? (Control)control.RoleDetail : control.UserDetail;
            var button = Descendants(detail).OfType<Button>()
                .Single(item => item.Text == (roles ? "Tạo vai trò…" : "Tạo người dùng…"));
            var opened = false;
            Exception? dialogError = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(item => item.Text == (roles ? "Tạo vai trò" : "Tạo người dùng"));
                if (dialog == null) return;
                timer.Stop();
                try
                {
                    opened = true;
                    Assert.True(dialog.Controls.Cast<Control>().SelectMany(Descendants).OfType<TextBox>().First().Focus());
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                    Assert.Equal(DialogResult.Cancel, dialog.DialogResult);
                }
                catch (Exception error) { dialogError = error; dialog.Close(); }
            };
            Assert.True(button.Focus());
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not activate the focused create action.");
            if (dialogError != null) throw dialogError;
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_escape_cancels_identity_edit_without_mutation(bool roles) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L10, (control, scene) =>
        {
            if (roles) control.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var detail = roles ? (Control)control.RoleDetail : control.UserDetail;
            var button = Descendants(detail).OfType<Button>()
                .Single(item => item.Text == (roles ? "Sửa tên…" : "Sửa thông tin…"));
            var opened = false;
            Exception? dialogError = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(item => item.Text.StartsWith("Sửa — "));
                if (dialog == null) return;
                timer.Stop();
                try
                {
                    opened = true;
                    Assert.Equal("Hủy", Assert.IsType<Button>(dialog.CancelButton).Text);
                    Assert.True(dialog.Controls.Cast<Control>().SelectMany(Descendants).OfType<TextBox>().First().Focus());
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                    Assert.Equal(DialogResult.Cancel, dialog.DialogResult);
                }
                catch (Exception error) { dialogError = error; dialog.Close(); }
            };
            Assert.True(button.Focus());
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not activate the focused identity edit action.");
            if (dialogError != null) throw dialogError;
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_membership_dialog_names_role_and_escape_cancels_without_dispatch() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L04, (control, scene) =>
        {
            control.UserDetail.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var button = Descendants(control.UserDetail).OfType<Button>().Single(item => item.Text == "Gán vai trò…");
            var opened = false;
            Exception? dialogError = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(item => item.Text.StartsWith("Gán vai trò — "));
                if (dialog == null) return;
                timer.Stop();
                try
                {
                    opened = true;
                    Assert.Contains(Descendants(dialog).OfType<Label>(), item => item.Visible && item.Text == "Vai trò");
                    var roles = Assert.Single(Descendants(dialog).OfType<ComboBox>());
                    Assert.Equal("Vai trò", roles.AccessibleName);
                    Assert.Equal("Hủy", Assert.IsType<Button>(dialog.CancelButton).Text);
                    Assert.True(roles.Focus());
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                    Assert.Equal(DialogResult.Cancel, dialog.DialogResult);
                }
                catch (Exception error) { dialogError = error; dialog.Close(); }
            };
            Assert.True(button.Focus());
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not open the membership dialog.");
            if (dialogError != null) throw dialogError;
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_membership_dialog_enter_confirms_selected_role_once() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L04, (control, scene) =>
        {
            control.UserDetail.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var button = Descendants(control.UserDetail).OfType<Button>().Single(item => item.Text == "Gán vai trò…");
            var opened = false;
            Exception? dialogError = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(item => item.Text.StartsWith("Gán vai trò — "));
                if (dialog == null) return;
                timer.Stop();
                try
                {
                    opened = true;
                    var roles = Assert.Single(Descendants(dialog).OfType<ComboBox>());
                    roles.SelectedIndex = 1;
                    Assert.True(roles.Focus());
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
                    Assert.Equal(DialogResult.OK, dialog.DialogResult);
                }
                catch (Exception error) { dialogError = error; dialog.Close(); }
            };
            Assert.True(button.Focus());
            timer.Start();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(opened, "Native Enter did not open the membership dialog.");
            if (dialogError != null) throw dialogError;
            var elapsed = Stopwatch.StartNew();
            while (!control.LastOperation.IsCompleted && elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Yield();
            }
            Assert.True(control.LastOperation.IsCompleted);
            Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate" &&
                entry.Request is SecurityMutationJournalEntry mutation &&
                mutation.Target == SecurityAdministrationTargetDto.User &&
                mutation.Operation == SecurityAdministrationOperationDto.SetMembership);
        });

    [Fact]
    public void Native_tab_preserves_staged_role_grant_after_mouse_toggle() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L07, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            control.RoleDetail.Tabs.SelectedIndex = 0;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var edit = Assert.Single(Descendants(control.RoleDetail).OfType<Button>(), item => item.Text == "Sửa quyền");
            edit.PerformClick();
            var grants = Assert.Single(Descendants(control.RoleDetail).OfType<CheckedListBox>());
            var index = grants.Items.IndexOf("Sales.Receipts.Post");
            Assert.True(index >= 0);
            var item = grants.GetItemRectangle(index);
            NativeLookupTypingDriver.Click(grants, new Point(item.Left + 20, item.Top + item.Height / 2));
            Assert.True(grants.GetItemChecked(index));
            Assert.Contains("Sales.Receipts.Post", control.ViewModel.RoleDraft!.DesiredKeys);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
            Assert.False(grants.ContainsFocus);
            Assert.Contains("Sales.Receipts.Post", control.ViewModel.RoleDraft.DesiredKeys);
            Assert.True(grants.GetItemChecked(index));
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_enter_starts_and_discards_role_grant_draft_without_dispatch() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            control.RoleDetail.Tabs.SelectedIndex = 0;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var buttons = Descendants(control.RoleDetail).OfType<Button>().ToArray();
            var edit = Assert.Single(buttons, item => item.Text == "Sửa quyền");
            var cancel = Assert.Single(buttons, item => item.Text == "Hủy thay đổi");
            var grants = Assert.Single(Descendants(control.RoleDetail).OfType<CheckedListBox>());
            grants.SelectedIndex = 0;
            var before = grants.GetItemChecked(0);
            Assert.True(grants.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Equal(before, grants.GetItemChecked(0));
            Assert.Null(control.ViewModel.RoleDraft);
            Assert.True(edit.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.NotNull(control.ViewModel.RoleDraft);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            grants.SelectedIndex = 0;
            Assert.True(grants.Focus());
            var item = grants.GetItemRectangle(0);
            NativeLookupTypingDriver.Click(grants, new Point(item.Left + 20, item.Top + item.Height / 2));
            Assert.Equal(!before, grants.GetItemChecked(0));
            Assert.Equal(0, grants.SelectedIndex);
            Assert.True(grants.ContainsFocus);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Equal(before, grants.GetItemChecked(0));
            Assert.False(control.ViewModel.HasDirtyEditor);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            Assert.True(cancel.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.Null(control.ViewModel.RoleDraft);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_keyboard_selects_direct_key_and_tri_state_without_implicit_write() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 0;
            control.UserDetail.Tabs.SelectedIndex = 3;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var controls = Descendants(control.UserDetail).ToArray();
            var key = Assert.Single(controls.OfType<ComboBox>(), item => item.AccessibleName == "Quyền riêng");
            Assert.True(key.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Down);
            Assert.Equal("Sales.Receipts.Read", control.ViewModel.DirectPermissionKey);
            var deny = Assert.Single(controls.OfType<RadioButton>(), item => item.Text == "Từ chối");
            Assert.True(deny.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Equal(false, control.ViewModel.DirectDecision);
            Assert.True(control.ViewModel.HasDirtyEditor);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            var allow = Assert.Single(controls.OfType<RadioButton>(), item => item.Text == "Cho phép");
            Assert.True(allow.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Equal(true, control.ViewModel.DirectDecision);
            var inherit = Assert.Single(controls.OfType<RadioButton>(), item => item.Text == "Theo vai trò");
            Assert.True(inherit.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.Null(control.ViewModel.DirectDecision);
            Assert.False(control.ViewModel.HasDirtyEditor);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Native_catalog_navigation_never_dispatches_an_assignment() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 2;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var grid = control.PermissionCatalog.Keys;
            grid.CurrentCell = grid.Rows[0].Cells[0];
            Assert.True(grid.Focus());
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Down);
            Assert.Contains("Sales.Receipts.Write", control.PermissionCatalog.Details.Text);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Space);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    [Theory]
    [InlineData(900, 600, false)]
    [InlineData(900, 600, true)]
    [InlineData(1200, 760, false)]
    [InlineData(1200, 760, true)]
    public void Native_search_preserves_caret_focus_and_committed_owner(int width, int height, bool dark) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            var priorTheme = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
                var form = control.FindForm()!;
                form.ClientSize = new Size(width, height);
                NativeLookupTypingDriver.Activate(form);
                ObserveNativeScale(form, control);
                var owner = control.ViewModel.SelectedUserId;
                var search = control.UserSearch;
                Assert.True(search.Focus());
                var handle = search.Handle;
                var text = "";
                foreach (var character in "inactive")
                {
                    SendMessage(handle, 0x0102, new IntPtr(character), IntPtr.Zero);
                    text += character;
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal(text, search.Text);
                    Assert.Equal(text.Length, search.SelectionStart);
                    Assert.Equal(0, search.SelectionLength);
                    Assert.Equal(handle, GetFocus());
                    Assert.Equal(owner, control.ViewModel.SelectedUserId);
                }
                Assert.Single(control.Users.Items);
                Assert.Equal(102, ((SecurityUserDto)control.Users.Items[0]).UserId);
                SendMessage(handle, 0x00B1, new IntPtr(2), new IntPtr(4));
                Assert.Equal(2, search.SelectionStart);
                Assert.Equal(2, search.SelectionLength);
                SendMessage(handle, 0x0102, new IntPtr('X'), IntPtr.Zero);
                System.Windows.Forms.Application.DoEvents();
                Assert.Equal("inXtive", search.Text);
                Assert.Equal(3, search.SelectionStart);
                Assert.Equal(0, search.SelectionLength);
                Assert.Empty(control.Users.Items);
                Assert.Equal(handle, GetFocus());
                Assert.Equal(owner, control.ViewModel.SelectedUserId);
                Assert.True(search.RectangleToScreen(search.ClientRectangle).Width > 0);
                Assert.DoesNotContain(scene.Journal.Entries, e => e.Operation.Contains("Mutate"));
            }
            finally { BootstrapThemeManager.CurrentTheme = priorTheme; }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Security_reset_drains_rearmed_held_reads_before_replacement(bool ignoresCancellation) =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, _) =>
        {
            using var form = new UiLabForm();
            form.Show();
            Until(() => form.CurrentSalesControl != null && !form.IsResetting);
            form.ModuleSelector.SelectedIndex = 1;
            Until(() => form.CurrentSecurityControl != null && !form.IsResetting);
            for (var round = 0; round < 2; round++)
            {
                var old = form.CurrentSecurityControl!;
                var data = form.CurrentSecurityScenarioData!;
                var gate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>> { IgnoreCancellation = ignoresCancellation };
                data.Administration.UsersGate = gate;
                var reload = old.ActivateAsync(default);
                Until(() => data.Administration.OutstandingOperations.Any(t => !t.IsCompleted));
                var reset = form.ResetForTestAsync();
                Until(() => reset.IsCompleted);
                reset.GetAwaiter().GetResult();
                Assert.True(reload.IsCompleted);
                Assert.True(old.IsDisposed);
                Assert.True(gate.IsCompleted);
                Assert.False(form.IsPoisoned);
                Assert.NotSame(old, form.CurrentSecurityControl);
                Assert.NotSame(data, form.CurrentSecurityScenarioData);
                Assert.All(data.Administration.OutstandingOperations, t => Assert.True(t.IsCompleted));
                Assert.False(gate.Complete(ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>.Success(data.Data.Users)));
                Assert.Equal(3, form.CurrentSecurityControl!.Users.Items.Count);
            }
            form.CloseWorkspaceButton.PerformClick();
            Until(() => form.CurrentSecurityControl == null);
        });

    [Fact]
    public void Security_reset_timeout_poisons_host_until_old_response_is_released() =>
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, _) =>
        {
            using var form = new UiLabForm();
            form.Show();
            Until(() => form.CurrentSalesControl != null && !form.IsResetting);
            form.ModuleSelector.SelectedIndex = 1;
            Until(() => form.CurrentSecurityControl != null && !form.IsResetting);
            var old = form.CurrentSecurityControl!;
            var data = form.CurrentSecurityScenarioData!;
            var gate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>> { IgnoreCancellation = true, IgnoreOwnerRelease = true };
            data.Administration.UsersGate = gate;
            var reload = old.ActivateAsync(default);
            Until(() => data.Administration.OutstandingOperations.Any(t => !t.IsCompleted));
            var reset = form.ResetForTestAsync();
            Until(() => reset.IsCompleted);
            reset.GetAwaiter().GetResult();
            Assert.True(form.IsPoisoned);
            Assert.Contains("did not drain", form.StatusText);
            Assert.Same(old, form.CurrentSecurityControl);
            Assert.Same(data, form.CurrentSecurityScenarioData);
            Assert.False(form.ModuleSelector.Enabled);
            Assert.False(form.ScenarioSelector.Enabled);
            form.ResetForTestAsync().GetAwaiter().GetResult();
            Assert.Same(old, form.CurrentSecurityControl);
            Assert.True(gate.Complete(ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>.Success(data.Data.Users)));
            Until(() => reload.IsCompleted);
            form.Close();
            Until(() => form.IsDisposed);
        });

    static void Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(7))
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }
        Assert.True(condition(), "Security native/Reset operation did not complete within its bounded pump.");
    }

    [DllImport("user32.dll")] static extern IntPtr GetFocus();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("shcore.dll")] static extern int GetScaleFactorForMonitor(IntPtr monitor, out int scale);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

    void ObserveNativeScale(Form form, SecurityWorkspaceControl control)
    {
        var monitor = MonitorFromWindow(form.Handle, 2);
        Assert.NotEqual(IntPtr.Zero, monitor);
        Assert.Equal(0, GetScaleFactorForMonitor(monitor, out var scale));
        Assert.Equal(0, GetDpiForMonitor(monitor, 0, out var dpiX, out var dpiY));
        var expected = Environment.GetEnvironmentVariable("MYDMSVN_UIN_EXPECTED_SCALE");
        if (!string.IsNullOrWhiteSpace(expected))
            Assert.Equal(int.Parse(expected), scale);
        output.WriteLine("PHYSICAL_SCALE={0};MONITOR_EFFECTIVE_DPI={1}x{2};WINDOW_DPI={3};CLIENT={4}x{5};WORKSPACE={6};LIST={7};DETAIL={8};TABS={9}",
            scale, dpiX, dpiY, GetDpiForWindow(form.Handle), form.ClientSize.Width, form.ClientSize.Height,
            control.Bounds, control.Users.Bounds, control.UserDetail.Bounds, control.Tabs.Bounds);
    }
}
