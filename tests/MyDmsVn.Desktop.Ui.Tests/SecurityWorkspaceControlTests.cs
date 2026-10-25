using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms;
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
            Assert.True(control.PermissionCatalog.ModuleGroups.Width >= 150);
            Assert.True(control.PermissionCatalog.CopyKey.Visible);
            Assert.True(control.PermissionCatalog.CopyKey.Bottom <= control.PermissionCatalog.ClientSize.Height);
            Assert.True(control.PermissionCatalog.Details.Bottom <= control.PermissionCatalog.ClientSize.Height);
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Surface, control.PermissionCatalog.Keys.BackgroundColor);
            Assert.False(control.HorizontalScroll.Visible);
            control.Tabs.SelectedIndex = 0;
            System.Windows.Forms.Application.DoEvents();
            Assert.InRange(control.Users.Width, 175, 285);
            Assert.True(control.UserDetail.HeaderActions.Controls.Cast<Control>().Last().Right <= control.UserDetail.HeaderActions.ClientSize.Width);
            control.Tabs.SelectedIndex = 1;
            System.Windows.Forms.Application.DoEvents();
            Assert.True(control.RoleDetail.GrantArea.Width > 0);
            Assert.True(control.RoleDetail.HeaderActions.Controls.Cast<Control>().Last().Right <= control.RoleDetail.HeaderActions.ClientSize.Width);
        }
        finally { BootstrapThemeManager.CurrentTheme = theme; }
    });
    [Theory]
    [InlineData(900, 600)]
    [InlineData(1200, 760)]
    public void Expanded_shell_keeps_master_metadata_and_actions_reachable(int width, int height) => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        using var shellViewModel = new FoundationViewModel(new UnusedFoundationApi());
        using var shell = new FoundationShellForm(shellViewModel, data.Session, new DesktopNotificationCenter())
        {
            Size = new System.Drawing.Size(width, height)
        };
        shell.Show();
        Assert.True(shell.Navigation.Expanded);
        Assert.NotNull(shell.OpenWorkspace("security-ui-lab-geometry", "Security", control, data.Session.Version));
        shell.PerformLayout();
        System.Windows.Forms.Application.DoEvents();
        control.Tabs.SelectedIndex = 0;
        System.Windows.Forms.Application.DoEvents();
        Console.WriteLine("SHELL_GEOMETRY=" + shell.ClientSize.Width + "x" + shell.ClientSize.Height +
            ";NAV=" + shell.Navigation.Width + ";MASTER=" + control.Users.Parent!.Width +
            ";LIST=" + control.Users.Width + ";DETAIL=" + control.UserDetail.Width +
            ";ACTIONS=" + control.UserDetail.HeaderActions.ClientSize.Width);
        Assert.InRange(control.Users.Parent!.Width, width == 900 ? 180 : 240, width == 900 ? 220 : 280);
        Assert.True(control.Users.ClientSize.Width - 20 >= 150, "Master secondary text needs a visible line in the expanded shell.");
        Assert.True(control.Users.ItemHeight >= 69);
        Assert.Contains("lan", control.Users.GetItemText(control.Users.Items[0]));
        AssertActionReachable(control.UserDetail.HeaderActions);
        control.Tabs.SelectedIndex = 1;
        System.Windows.Forms.Application.DoEvents();
        Assert.InRange(control.Roles.Parent!.Width, width == 900 ? 180 : 240, width == 900 ? 220 : 280);
        Assert.True(control.Roles.ClientSize.Width - 20 >= 150);
        Assert.Contains("thành viên", control.Roles.GetItemText(control.Roles.Items[0]));
        AssertActionReachable(control.RoleDetail.HeaderActions);
    });
    [Fact]
    public void Master_tooltip_scrubs_identity_on_rebind_forbidden_and_session_replacement() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        HoverFirstRow(control.Users);
        Assert.Contains("lan", TooltipText(control.Users));
        var original = scene.Data.Users[0];
        scene.Data.Users[0] = new SecurityUserDto(original.UserId, "replacement", "Người dùng mới", original.Email,
            original.IsActive, original.AssignmentRevision, original.RoleIds, original.DirectDecisions, original.IsProtectedAccount);
        Wait(control.ActivateAsync(default));
        Assert.Equal("", TooltipText(control.Users));
        HoverFirstRow(control.Users);
        Assert.Contains("replacement", TooltipText(control.Users));
        Assert.DoesNotContain("lan", TooltipText(control.Users));
        scene.Administration.UsersError = new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.Forbidden, "Denied", "Denied");
        Wait(control.ActivateAsync(default));
        Assert.Empty(control.Users.Items);
        Assert.Equal("", TooltipText(control.Users));
        Assert.Equal("", control.Users.AccessibleDescription);
        HoverFirstRow(control.Roles);
        Assert.NotEmpty(TooltipText(control.Roles));
        scene.Session.Replace();
        Assert.Equal("", TooltipText(control.Roles));
        Assert.Equal("", control.Roles.AccessibleDescription);
    });

    [Fact]
    public void Effective_search_matches_friendly_name_without_changing_owner_or_explanation() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        scene.Data.Catalog = new PermissionCatalogDto("UI Lab", "different-description",
            scene.Data.Catalog.Items.Select(item => item.Key == "Sales.Receipts.Read"
                ? new PermissionCatalogItemDto(item.Key, item.Module, "Other description", item.Status) : item).ToArray());
        Wait(control.ActivateAsync(default));
        var owner = control.ViewModel.SelectedUserId;
        control.UserDetail.Tabs.SelectedIndex = 2;
        control.UserDetail.Search.Text = "Xem phiếu bán hàng";
        var row = Assert.Single(control.UserDetail.Effective.Rows.Cast<DataGridViewRow>());
        Assert.Equal("Sales.Receipts.Read", row.Tag);
        Assert.Contains("Sales.Receipts.Read", control.UserDetail.Explanation.Text);
        Assert.Equal(owner, control.ViewModel.SelectedUserId);
    });

    [Fact]
    public void Protected_user_disables_maintenance_actions_but_keeps_create_available() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        control.ViewModel.SelectUser(103);
        var actions = control.UserDetail.HeaderActions.Controls.Cast<Control>().OfType<Button>().ToArray();
        Assert.True(Assert.Single(actions, button => button.Text == "Tạo người dùng…").Enabled);
        foreach (var name in new[] { "Sửa thông tin…", "Đặt lại mật khẩu…", "Đổi trạng thái…" })
        {
            var action = Assert.Single(actions, button => button.Text == name);
            Assert.False(action.Enabled);
            Assert.Contains("Tài khoản được bảo vệ", action.AccessibleDescription);
            action.PerformClick();
        }
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        control.ViewModel.SelectUser(101);
        Assert.All(actions.Skip(1), action => Assert.True(action.Enabled));
        scene.Session.Replace();
        Assert.All(actions, action => Assert.False(action.Enabled));
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
        var row = System.Linq.Enumerable.Single(control.UserDetail.Effective.Rows.Cast<DataGridViewRow>(), item => Equals(item.Tag, "Sales.Receipts.Post"));
        control.UserDetail.Effective.CurrentCell = row.Cells[0];
        Assert.Contains("Quyền riêng: Từ chối", control.UserDetail.Explanation.Text);
        Assert.DoesNotContain("DirectDeny", control.UserDetail.Explanation.Text);
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
        var actions = Descendants(control.PermissionCatalog, typeof(Button)).Cast<Button>().Select(button => button.Text).ToArray();
        Assert.Equal(2, actions.Length);
        Assert.Contains("Sao chép mã quyền", actions);
        Assert.Contains("Làm mới", actions);
        Assert.Equal(data.Data.Catalog.Items.Select(item => item.Module).Distinct().Count() + 1, control.PermissionCatalog.ModuleGroups.Items.Count);
        Assert.Contains("Tất cả", control.PermissionCatalog.ModuleGroups.Items[0]!.ToString());
        control.PermissionCatalog.ModuleGroups.SelectedIndex = 1;
        Assert.All(control.PermissionCatalog.Keys.Rows.Cast<DataGridViewRow>(), row => Assert.Equal(control.PermissionCatalog.Module.Text, row.Cells[2].Value));
        control.PermissionCatalog.ModuleGroups.SelectedIndex = 0;
        control.PermissionCatalog.Keys.CurrentCell = control.PermissionCatalog.Keys.Rows[0].Cells[0];
        Assert.Contains("Sales.Receipts.Read", control.PermissionCatalog.Details.Text);
        Assert.Contains("CHI TIẾT QUYỀN", control.PermissionCatalog.Details.Text);
    });
    [Fact]
    public void Catalog_search_matches_the_displayed_friendly_name() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        scene.Data.Catalog = new PermissionCatalogDto("UI Lab", "different-description",
            scene.Data.Catalog.Items.Select(item => item.Key == "Sales.Receipts.Read"
                ? new PermissionCatalogItemDto(item.Key, item.Module, "Other description", item.Status) : item).ToArray());
        Wait(control.ActivateAsync(default));
        control.PermissionCatalog.Search.Text = "Xem phiếu bán hàng";
        Assert.Single(control.PermissionCatalog.Keys.Rows);
        Assert.Equal("Sales.Receipts.Read", control.PermissionCatalog.Keys.Rows[0].Cells[1].Value);
    });
    [Fact]
    public void Master_and_identity_views_show_separate_hierarchy() => Run(SecurityScenarioId.SEC_L01, (control, _) =>
    {
        Assert.Equal(DrawMode.OwnerDrawFixed, control.Users.DrawMode);
        Assert.True(control.Users.ItemHeight >= 50);
        Assert.Equal(DrawMode.OwnerDrawFixed, control.Roles.DrawMode);
        Assert.NotEmpty(control.UserDetail.Header.Text);
        Assert.Contains("@", control.UserDetail.HeaderMetadata.Text);
        Assert.Contains("hoạt động", control.UserDetail.HeaderState.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THÔNG TIN TÀI KHOẢN", control.UserDetail.Tabs.TabPages[0].Controls.Cast<Control>().OfType<Label>().First(item => item.Text.Contains("THÔNG TIN TÀI KHOẢN")).Text);
        Assert.DoesNotContain("Vai trò", control.UserDetail.Header.Text);
        Assert.DoesNotContain("Thành viên", control.RoleDetail.Header.Text);
    });
    [Fact]
    public void Direct_row_opens_the_same_key_and_previews_without_mutating_effective_result() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        var key = "Sales.Receipts.Post";
        var committed = Assert.Single(control.UserDetail.Effective.Rows.Cast<DataGridViewRow>(), row => Equals(row.Tag, key)).Cells[1].Value;
        control.UserDetail.Tabs.SelectedIndex = 3;
        control.UserDetail.DirectDecisions.SelectedIndex = 0;
        Until(() => control.ViewModel.DirectPermissionKey == key);
        Assert.Equal(key, control.ViewModel.DirectPermissionKey);
        var preview = control.UserDetail.Tabs.TabPages[3].Controls.Cast<Control>().OfType<Label>().First(item => item.Text.Contains("Đã lưu:"));
        Assert.Contains("Đã lưu:", preview.Text);
        Assert.Single(Descendants(control.UserDetail, typeof(RadioButton)).Cast<RadioButton>(), radio => radio.Text == "Cho phép").Checked = true;
        Assert.True(control.ViewModel.HasDirtyEditor);
        Assert.Contains("Kết quả dự kiến (chưa lưu): ✓ Cho phép", preview.Text);
        Assert.Equal(committed, Assert.Single(control.UserDetail.Effective.Rows.Cast<DataGridViewRow>(), row => Equals(row.Tag, key)).Cells[1].Value);
        control.ViewModel.DiscardEditor();
        Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation.Contains("Mutate"));
    });
    [Fact]
    public void Direct_decision_render_preserves_selected_key_and_scroll_position() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        var original = scene.Data.Users[0];
        var decisions = Enumerable.Range(0, 6).Select(index => new SecurityDecisionDto("Legacy.Key" + index, false))
            .Concat(new[] { new SecurityDecisionDto("Sales.Receipts.Post", false) })
            .Concat(Enumerable.Range(6, 6).Select(index => new SecurityDecisionDto("Legacy.Key" + index, false))).ToArray();
        scene.Data.Users[0] = new SecurityUserDto(original.UserId, original.Username, original.DisplayName,
            original.Email, original.IsActive, original.AssignmentRevision, original.RoleIds, decisions, original.IsProtectedAccount);
        Wait(control.ActivateAsync(default));
        control.UserDetail.Tabs.SelectedIndex = 3;
        var list = control.UserDetail.DirectDecisions;
        list.Height = 66;
        list.TopIndex = 4;
        list.SelectedIndex = 6;
        Until(() => control.ViewModel.DirectPermissionKey == "Sales.Receipts.Post");
        var top = list.TopIndex;
        control.ViewModel.SelectUser(101);
        Assert.Equal(6, list.SelectedIndex);
        Assert.Contains("Sales.Receipts.Post", list.SelectedItem!.ToString());
        Assert.Equal(top, list.TopIndex);
        Assert.True(list.Focus());
        NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Down);
        Assert.Equal(7, list.SelectedIndex);
        NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Up);
        Assert.Equal(6, list.SelectedIndex);
        Assert.Equal(101, control.ViewModel.SelectedUserId);
    });
    [Fact]
    public void Role_editor_has_filters_history_and_contextual_recovery() => Run(SecurityScenarioId.SEC_L01, (control, _) =>
    {
        control.Tabs.SelectedIndex = 1;
        Assert.Equal("", control.RolePaneStatus.Text);
        Assert.Contains("Sửa quyền", control.RoleDetail.HeaderActions.Controls.Cast<Control>().OfType<Button>().Select(button => button.Text));
        Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(CheckedListBox)));
        Assert.NotEmpty(Descendants(control.RoleDetail.GrantArea, typeof(TextBox)));
        Assert.NotEmpty(Descendants(control.RoleDetail.GrantArea, typeof(ComboBox)));
        Assert.NotEmpty(Descendants(control.RoleDetail.GrantArea, typeof(CheckBox)));
        Assert.DoesNotContain(Descendants(control, typeof(FlowLayoutPanel)).Cast<FlowLayoutPanel>(), panel => panel.Visible && panel.Controls.Cast<Control>().OfType<Button>().Any(button => button.Text == "Tải lại để đối chiếu"));
    });
    [Fact]
    public void Role_filter_retains_hidden_draft_and_read_only_orphan() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        control.Tabs.SelectedIndex = 1;
        Assert.Contains("Legacy.Orphan", control.RoleDetail.Permissions.Items[0]!.ToString());
        var edit = Assert.Single(Descendants(control.RoleDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Sửa quyền");
        edit.PerformClick();
        var grants = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(CheckedListBox)).Cast<CheckedListBox>());
        Assert.Contains("Xem phiếu bán hàng", grants.GetItemText(grants.Items[0]));
        var index = grants.Items.IndexOf("Sales.Receipts.Post");
        grants.SetItemChecked(index, true);
        Until(() => control.ViewModel.RoleDraft?.Changes.Count == 1);
        Assert.Contains("Chưa lưu", grants.GetItemText(grants.Items[index]));
        var search = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(TextBox)).Cast<TextBox>());
        search.Text = "Read";
        Assert.Single(grants.Items);
        Assert.Equal("Sales.Receipts.Read", grants.Items[0]);
        Assert.Single(control.ViewModel.RoleDraft!.Changes);
        Assert.Contains("1 thay đổi", control.RoleDetail.GrantArea.Controls.Cast<Control>().OfType<Label>().Single().Text);
        Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation.Contains("Mutate"));
    });
    [Fact]
    public void Deferred_role_check_cannot_edit_a_replacement_draft() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        control.Tabs.SelectedIndex = 1;
        Assert.True(control.ViewModel.BeginRolePermissions());
        var grants = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(CheckedListBox)).Cast<CheckedListBox>());
        var index = grants.Items.IndexOf("Sales.Receipts.Post");
        Assert.True(index >= 0);
        grants.SetItemChecked(index, true);
        control.ViewModel.SelectRole(202);
        Assert.True(control.ViewModel.BeginRolePermissions());
        var replacement = control.ViewModel.RoleDraft;
        System.Windows.Forms.Application.DoEvents();
        Assert.Same(replacement, control.ViewModel.RoleDraft);
        Assert.Empty(replacement!.Changes);
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });

    [Fact]
    public void Role_member_labels_include_username_and_activity() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        control.ViewModel.SelectRole(201);
        Assert.Contains("@lan", control.RoleDetail.Members.Items[0]!.ToString());
        Assert.Contains("Đang hoạt động", control.RoleDetail.Members.Items[0]!.ToString());
        var original = scene.Data.Users[0];
        scene.Data.Users[0] = new SecurityUserDto(original.UserId, original.Username, original.DisplayName,
            original.Email, false, original.AssignmentRevision, original.RoleIds, original.DirectDecisions, original.IsProtectedAccount);
        Wait(control.ActivateAsync(default));
        Assert.Contains("Ngừng hoạt động", control.RoleDetail.Members.Items[0]!.ToString());
    });
    [Fact]
    public void Unmapped_catalog_key_with_no_description_keeps_its_canonical_name() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        var future = new PermissionCatalogItemDto("Future.Module.Read", "Future", "", "Active");
        data.Data.Catalog = new PermissionCatalogDto("UI Lab", "future-key", data.Data.Catalog.Items.Concat(new[] { future }).ToArray());
        Wait(control.ActivateAsync(default));
        control.Tabs.SelectedIndex = 2;
        var row = Assert.Single(control.PermissionCatalog.Keys.Rows.Cast<DataGridViewRow>(), item => (string?)item.Cells[1].Value == future.Key);
        Assert.Equal(future.Key, row.Cells[0].Value);
        control.PermissionCatalog.Keys.CurrentCell = row.Cells[0];
        Assert.Contains(future.Key, control.PermissionCatalog.Details.Text);
        Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });
    [Fact]
    public void Dark_role_grants_remain_readable_and_read_only_until_edit() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        var priorTheme = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(BootstrapThemeMode.Dark);
            control.Tabs.SelectedIndex = 1;
            Assert.True(control.RoleDetail.Header.Font.Bold);
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
    public void User_create_dialog_accepts_valid_fields_by_native_enter_once() => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        var create = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Tạo người dùng…");
        ExceptionDispatchInfo? dialogError = null;
        TextBox? secret = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "Tạo người dùng");
            if (dialog == null) return;
            timer.Stop();
            try
            {
                Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Tên đăng nhập").Text = "keyboard-user";
                Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Tên hiển thị").Text = "Keyboard User";
                secret = Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Mật khẩu tạm");
                secret.Text = "temporary-password";
                Assert.True(secret.Focus());
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
                Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); dialog.Close(); }
        };
        timer.Start();
        create.PerformClick();
        Assert.False(timer.Enabled, "The create dialog did not open within the pumped test window.");
        dialogError?.Throw();
        Assert.NotNull(secret);
        Assert.Empty(secret!.Text);
        Assert.Contains(data.Data.Users, user => user.Username == "keyboard-user");
        Assert.Equal(4, control.Users.Items.Count);
    });
    [Theory]
    [InlineData(MyDmsVn.Contracts.ApiStatusCode.BadRequest)]
    [InlineData(MyDmsVn.Contracts.ApiStatusCode.Conflict)]
    public void User_create_server_rejection_keeps_safe_fields_and_clears_secret(MyDmsVn.Contracts.ApiStatusCode status) => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        data.Administration.MutationErrors.Enqueue(new MyDmsVn.Contracts.ApiError(status, status == MyDmsVn.Contracts.ApiStatusCode.Conflict ? "Security.RevisionConflict" : "ValidationError", "Check the entered fields."));
        var create = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Tạo người dùng…");
        ExceptionDispatchInfo? dialogError = null;
        var phase = 0;
        var ticks = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "Tạo người dùng");
            if (dialog == null) return;
            try
            {
                if (phase == 0)
                {
                    phase = 1;
                    Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Tên đăng nhập").Text = "rejected-user";
                    Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Tên hiển thị").Text = "Rejected User";
                    Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Mật khẩu tạm").Text = "temporary-password";
                    Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Lưu thông tin").PerformClick();
                }
                else if (data.Journal.Entries.Count(entry => entry.Operation == "Security.Mutate") == 1)
                {
                    Assert.True(dialog.Visible);
                    Assert.Equal("rejected-user", Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Tên đăng nhập").Text);
                    Assert.Empty(Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Mật khẩu tạm").Text);
                    timer.Stop();
                    dialog.Close();
                }
                else if (++ticks > 50) throw new Xunit.Sdk.XunitException("The rejected mutation did not reach the fake journal.");
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); timer.Stop(); dialog.Close(); }
        };
        timer.Start();
        create.PerformClick();
        Assert.False(timer.Enabled, "The rejected create dialog did not finish within the pumped test window.");
        dialogError?.Throw();
        Assert.DoesNotContain(data.Data.Users, user => user.Username == "rejected-user");
    });
    [Fact]
    public void User_create_held_save_rejects_duplicate_submission() => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        var gate = data.Administration.HoldNextMutation();
        var create = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Tạo người dùng…");
        ExceptionDispatchInfo? dialogError = null;
        var phase = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "Tạo người dùng");
            if (dialog == null) return;
            try
            {
                var fields = Descendants(dialog, typeof(TextBox)).Cast<TextBox>().ToArray();
                var secret = Assert.Single(fields, field => field.AccessibleName == "Mật khẩu tạm");
                if (phase == 0)
                {
                    phase = 1;
                    Assert.Single(fields, field => field.AccessibleName == "Tên đăng nhập").Text = "held-user";
                    Assert.Single(fields, field => field.AccessibleName == "Tên hiển thị").Text = "Held User";
                    secret.Text = "temporary-password";
                    var save = Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Lưu thông tin");
                    save.PerformClick();
                    Assert.False(save.Enabled);
                    save.PerformClick();
                    Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                    gate.Complete(MyDmsVn.Contracts.ApiResponse<SecurityAdministrationResultDto>.Failure(new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.BadRequest, "ValidationError", "Rejected.")));
                }
                else
                {
                    Assert.True(dialog.Visible);
                    Assert.Empty(secret.Text);
                    timer.Stop();
                    dialog.Close();
                }
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); timer.Stop(); dialog.Close(); }
        };
        timer.Start();
        create.PerformClick();
        Assert.False(timer.Enabled, "The held create dialog did not finish within the pumped test window.");
        dialogError?.Throw();
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
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
    public void Password_reset_dialog_accepts_native_enter_once_and_clears_secret() => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        var reset = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Đặt lại mật khẩu…");
        ExceptionDispatchInfo? dialogError = null;
        TextBox? secret = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text.StartsWith("Đặt lại mật khẩu — "));
            if (dialog == null) return;
            timer.Stop();
            try
            {
                secret = Assert.Single(Descendants(dialog, typeof(TextBox)).Cast<TextBox>(), field => field.AccessibleName == "Mật khẩu mới");
                secret.Text = "new-temporary-password";
                Assert.True(secret.Focus());
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
                Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); dialog.Close(); }
        };
        timer.Start();
        reset.PerformClick();
        Assert.False(timer.Enabled, "The password reset dialog did not open within the pumped test window.");
        dialogError?.Throw();
        Assert.NotNull(secret);
        Assert.Empty(secret!.Text);
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

    static void AssertActionReachable(FlowLayoutPanel actions)
    {
        Assert.False(actions.AutoScroll);
        Assert.True(actions.WrapContents);
        foreach (var action in actions.Controls.Cast<Control>().OfType<Button>())
        {
            Assert.InRange(action.Left, 0, actions.ClientSize.Width);
            Assert.InRange(action.Right, 1, actions.ClientSize.Width);
            Assert.InRange(action.Bottom, 1, actions.ClientSize.Height);
        }
    }

    static void HoverFirstRow(ListBox list)
    {
        var method = list.GetType().GetMethod("OnMouseMove", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        method.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.None, 0, 5, list.ItemHeight / 2, 0) });
    }

    static string TooltipText(ListBox list)
    {
        var field = list.GetType().GetField("_itemTip", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return ((ToolTip)field.GetValue(list)!).GetToolTip(list);
    }

    sealed class UnusedFoundationApi : IFoundationApiClient
    {
        public Task<MyDmsVn.Contracts.ApiResponse<MyDmsVn.Contracts.FoundationStatus>> GetStatusAsync(
            MyDmsVn.Contracts.FoundationStatusRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Shell geometry must not request foundation data.");
    }
}
