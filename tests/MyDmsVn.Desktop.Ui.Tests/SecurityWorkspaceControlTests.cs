using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms.Features.Security;
using Xunit;
using MyDmsVn.Bootstrap5WinFormUI.Theme;

namespace MyDmsVn.Desktop.Tests;
public sealed class SecurityWorkspaceControlTests
{
    [Fact]
    public void Lab_module_selection_mounts_candidate_and_preserves_sales_reset() => Run(SecurityScenarioId.SEC_L01, (_, _) =>
    {
        using var form = new MyDmsVn.Desktop.UiLab.UiLabForm();
        form.Show();
        Until(() => form.CurrentSalesControl != null && !form.IsResetting);
        var sales = form.CurrentSalesControl!;
        form.ModuleSelector.SelectedIndex = 1;
        Until(() => form.CurrentSecurityControl != null && !form.IsResetting);
        Assert.True(sales.IsDisposed);
        Assert.Null(form.CurrentSalesControl);
        var security = form.CurrentSecurityControl!;
        Assert.IsType<SecurityWorkspaceControl>(security);
        Assert.Equal(SecurityDatasetState.Ready, security.ViewModel.Users.State);
        form.ModuleSelector.SelectedIndex = 0;
        Until(() => form.CurrentSalesControl != null && !form.IsResetting);
        Assert.True(security.IsDisposed);
        Assert.Null(form.CurrentSecurityControl);
        form.CloseWorkspaceButton.PerformClick();
        Until(() => form.CurrentSalesControl == null);
    });

    [Theory]
    [InlineData(900, 600, false)]
    [InlineData(900, 600, true)]
    [InlineData(1200, 760, false)]
    [InlineData(1200, 760, true)]
    public void Normal_and_minimum_hosts_keep_catalog_actions_reachable(int width, int height, bool dark) => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        var theme = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
            var form = control.FindForm()!;
            form.Size = new System.Drawing.Size(width, height);
            control.Tabs.SelectedIndex = 2;
            form.PerformLayout();
            System.Windows.Forms.Application.DoEvents();
            Assert.True(form.ClientSize.Width > 0 && form.ClientSize.Height > 0);
            Assert.True(control.PermissionCatalog.Keys.Width > 0);
            Assert.True(control.PermissionCatalog.CopyKey.Visible);
            Assert.True(control.PermissionCatalog.CopyKey.Bottom <= control.PermissionCatalog.ClientSize.Height);
            Assert.True(control.PermissionCatalog.Details.Bottom <= control.PermissionCatalog.ClientSize.Height);
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Surface, control.PermissionCatalog.Keys.BackgroundColor);
            Assert.False(control.HorizontalScroll.Visible);
        }
        finally { BootstrapThemeManager.CurrentTheme = theme; }
    });
    [Fact]
    public void Filters_preserve_selected_owner_and_show_catalog_lifecycle() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        var owner = control.ViewModel.SelectedUserId;
        control.UserSearch.Text = "inactive";
        Assert.Single(control.Users.Items);
        Assert.Equal(owner, control.ViewModel.SelectedUserId);
        control.UserSearch.Clear();
        Assert.Equal(owner, ((MyDmsVn.Contracts.Security.SecurityUserDto)control.Users.SelectedItem).UserId);
        control.PermissionCatalog.Lifecycle.SelectedIndex = 2;
        Assert.Single(control.PermissionCatalog.Keys.Rows);
        Assert.Equal("Legacy.Deprecated", control.PermissionCatalog.Keys.Rows[0].Cells[1].Value);
        Assert.Contains("1 /", control.PermissionCatalog.Count.Text);
        control.PermissionCatalog.Search.Text = "no-match";
        Assert.Empty(control.PermissionCatalog.Keys.Rows);
        Assert.Contains("0 /", control.PermissionCatalog.Count.Text);
        Assert.False(control.PermissionCatalog.CopyKey.Enabled);
    });

    [Fact]
    public void Effective_explanation_is_read_only_and_retains_direct_deny() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        control.UserDetail.Tabs.SelectedIndex = 2;
        control.UserDetail.Result.SelectedIndex = 2;
        Assert.True(control.UserDetail.Effective.ReadOnly);
        Assert.True(control.UserDetail.Effective.Rows.Count > 0);
        var row = System.Linq.Enumerable.Single(control.UserDetail.Effective.Rows.Cast<DataGridViewRow>(), item => Equals(item.Cells[0].Value, "Sales.Receipts.Post"));
        control.UserDetail.Effective.CurrentCell = row.Cells[0];
        Assert.Contains("DirectDeny", control.UserDetail.Explanation.Text);
        Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation.Contains("Mutate"));
    });

    [Fact]
    public void Headers_do_not_duplicate_child_tab_membership() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        Assert.DoesNotContain("Nhân viên bán hàng", control.UserDetail.Header.Text);
        Assert.Equal("Vai trò (1)", control.UserDetail.Tabs.TabPages[1].Text);
        Assert.Equal("Thành viên (1)", control.RoleDetail.Tabs.TabPages[1].Text);
        Assert.DoesNotContain("Thành viên", control.RoleDetail.Header.Text);
    });
    [Fact]
    public void Catalog_has_no_assignment_actions() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        control.Tabs.SelectedIndex = 2;
        Assert.Equal(data.Data.Catalog.Items.Count, control.PermissionCatalog.Keys.Rows.Count);
        Assert.Single(Descendants(control.PermissionCatalog, typeof(Button)));
        control.PermissionCatalog.Keys.CurrentCell = control.PermissionCatalog.Keys.Rows[0].Cells[0];
        Assert.Contains("Sales.Receipts.Read", control.PermissionCatalog.Details.Text);
    });
    [Fact]
    public void Dark_role_grants_remain_readable_and_read_only_until_edit() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        var priorTheme = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(BootstrapThemeMode.Dark);
            control.Tabs.SelectedIndex = 1;
            var grants = Assert.Single(Descendants(control.RoleDetail, typeof(CheckedListBox)).Cast<CheckedListBox>());
            Assert.True(grants.Enabled);
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Text, grants.ForeColor);
            var before = grants.GetItemChecked(0);
            grants.SetItemChecked(0, !before);
            Assert.Equal(before, grants.GetItemChecked(0));
            Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation.Contains("Mutate"));
            var edit = Assert.Single(Descendants(control.RoleDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Sửa quyền");
            edit.PerformClick();
            grants.SetItemChecked(0, !before);
            Assert.Equal(!before, grants.GetItemChecked(0));
        }
        finally { BootstrapThemeManager.CurrentTheme = priorTheme; }
    });
    [Fact]
    public void User_create_dialog_rejects_missing_and_oversize_fields_before_dispatch() => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        var create = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Tạo người dùng…");
        ExceptionDispatchInfo? dialogError = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "Tạo người dùng");
            if (dialog == null) return;
            timer.Stop();
            try
            {
                var save = Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Lưu thông tin");
                save.PerformClick();
                Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                Assert.True(dialog.Visible);
                var name = Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Tên đăng nhập");
                var display = Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Tên hiển thị");
                var password = Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Mật khẩu tạm");
                name.Text = "new-user";
                display.Text = "New User";
                password.Text = new string('x', 73);
                save.PerformClick();
                Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                Assert.Empty(password.Text);
                Assert.True(password.Focused);
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); }
            finally { dialog.Close(); }
        };
        timer.Start();
        create.PerformClick();
        Assert.False(timer.Enabled, "The create dialog did not open within the pumped test window.");
        dialogError?.Throw();
    });
    [Fact]
    public void Password_reset_dialog_rejects_invalid_input_before_dispatch() => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        var reset = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Đặt lại mật khẩu…");
        ExceptionDispatchInfo? dialogError = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text.StartsWith("Đặt lại mật khẩu — "));
            if (dialog == null) return;
            timer.Stop();
            try
            {
                var save = Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Xác nhận");
                var password = Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Mật khẩu mới");
                save.PerformClick();
                Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                Assert.True(dialog.Visible);
                password.Text = new string('x', 73);
                save.PerformClick();
                Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                Assert.Empty(password.Text);
                Assert.True(password.Focused);
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); }
            finally { dialog.Close(); }
        };
        timer.Start();
        reset.PerformClick();
        Assert.False(timer.Enabled, "The password reset dialog did not open within the pumped test window.");
        dialogError?.Throw();
    });
    [Fact]
    public void Members_open_the_authorized_user_roles_tab() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        control.Tabs.SelectedIndex = 1;
        control.RoleDetail.Tabs.SelectedIndex = 1;
        control.RoleDetail.Members.SelectedIndex = 0;
        control.RoleDetail.OpenUser.PerformClick();
        Assert.Equal(0, control.Tabs.SelectedIndex);
        Assert.Equal(1, control.UserDetail.Tabs.SelectedIndex);
        Assert.Equal(101, control.ViewModel.SelectedUserId);
    });
    [Fact]
    public void Unknown_data_is_not_presented_as_an_empty_success() => Run(SecurityScenarioId.SEC_L09, (control, data) =>
    {
        Assert.Equal(SecurityDatasetState.Forbidden, control.ViewModel.Catalog.State);
        Assert.Contains("không khả dụng", control.PermissionCatalog.Details.Text);
        Assert.Equal(3, control.Users.Items.Count);
        Assert.Equal("Vai trò (1)", control.UserDetail.Tabs.TabPages[1].Text);
    });
    [Fact]
    public void Session_replacement_clears_presentation() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        data.Session.Replace();
        Assert.Empty(control.Users.Items);
        Assert.Empty(control.Roles.Items);
        Assert.Empty(control.PermissionCatalog.Keys.Rows);
        Assert.Null(control.ViewModel.SelectedUser);
    });
    static System.Collections.Generic.IEnumerable<Control> Descendants(Control control, Type type)
    {
        foreach (Control child in control.Controls)
        {
            if (type.IsInstanceOfType(child))
                yield return child;
            foreach (var nested in Descendants(child, type))
                yield return nested;
        }
    }

    internal static void Run(SecurityScenarioId id, Action<SecurityWorkspaceControl, SecurityScenario> action)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            ExceptionDispatchInfo? error = null;
            using var loop = new ApplicationContext();
            using var startup = new Control();
            startup.CreateControl();
            startup.BeginInvoke(new Action(() =>
            {
                try
                {
                    using var data = SecurityScenarioCatalog.Create(id);
                    if (id == SecurityScenarioId.SEC_L09)
                        data.Catalog.Error = new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.Forbidden, "Forbidden", "Catalog denied");
                    using var form = new Form
                    {
                        Width = 900,
                        Height = 600
                    };
                    form.Show();
                    var context = SynchronizationContext.Current ?? throw new InvalidOperationException();
                    using var control = new SecurityWorkspaceControl(new SecurityWorkspaceViewModel(data.Administration, data.Catalog, data.Session, new SynchronizationContextUiDispatcher(context), data.Session.Version));
                    form.Controls.Add(control);
                    Wait(control.ActivateAsync(token));
                    Assert.NotEqual(IntPtr.Zero, control.Handle);
                    action(control, data);
                    Assert.Empty(guard.Exceptions);
                }
                catch (Exception e)
                {
                    error = ExceptionDispatchInfo.Capture(e);
                }
                finally
                {
                    loop.ExitThread();
                }
            }));
            System.Windows.Forms.Application.Run(loop);
            error?.Throw();
        }, TimeSpan.FromSeconds(25));
    }

    static void Wait(Task task)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }

        Assert.True(task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    static void Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(6)) { System.Windows.Forms.Application.DoEvents(); Thread.Yield(); }
        Assert.True(condition());
    }
}
