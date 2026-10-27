using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
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
using MyDmsVn.Bootstrap5WinFormUI.Controls;

namespace MyDmsVn.Desktop.Tests;
[Trait("Module", "Security")]
public sealed class SecurityWorkspaceControlTests
{
    [Fact]
    public void Role_catalog_render_preserves_selected_definition_native_row_and_filter_identity() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        control.Tabs.SelectedIndex = 2;
        var catalog = control.PermissionCatalog;
        catalog.ModuleGroups.SelectedIndex = catalog.Module.Items.IndexOf("Sales");
        var row = catalog.Keys.Rows.Cast<DataGridViewRow>().Single(item => (string)item.Cells[1].Value == "Sales.Receipts.Post");
        catalog.Keys.CurrentCell = row.Cells[0];
        catalog.Keys.Focus();
        control.ViewModel.SelectRole(201);
        Assert.Same(row, catalog.Keys.CurrentRow);
        Assert.True(catalog.Keys.ContainsFocus);
        Assert.Equal("Sales", catalog.Module.Text);
        Assert.Equal(catalog.Module.SelectedIndex, catalog.ModuleGroups.SelectedIndex);
        Assert.Contains("Sales.Receipts.Post", catalog.Details.Text);
        catalog.Search.Text = "Sales.";
        Assert.Same(row, catalog.Keys.CurrentRow);
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Role_catalog_toolkit_surfaces_keep_read_only_theme_focus_and_copy(bool dark) => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        var prior = BootstrapThemeManager.CurrentTheme;
        var clipboard = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        try
        {
            BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
            control.Tabs.SelectedIndex = 1;
            control.RoleDetail.Tabs.SelectedIndex = 1;
            Assert.IsAssignableFrom<BootstrapListView>((object)control.RoleDetail.Members);
            Assert.IsAssignableFrom<BootstrapListView>((object)control.RoleDetail.Permissions);
            Assert.IsType<BootstrapButton>(control.RoleDetail.OpenUser);
            Assert.IsType<BootstrapLabel>(control.RoleDetail.HistoryTitle);
            control.RoleDetail.Members.SelectedIndex = 1;
            Assert.True(control.RoleDetail.Members.Focus());
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Surface, control.RoleDetail.Members.BackColor);
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Home);
            Assert.Equal(0, control.RoleDetail.Members.SelectedIndex);
            Assert.DoesNotContain(control.RoleDetail.HeaderState.Text, "thành viên", StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Thành viên (2)", control.RoleDetail.Tabs.TabPages[1].Text);
            var catalog = control.PermissionCatalog;
            control.Tabs.SelectedIndex = 2;
            Assert.IsType<BootstrapDataGridView>(catalog.Keys);
            Assert.IsType<BootstrapTextBox>((object)catalog.Search);
            Assert.IsType<BootstrapComboBox>(catalog.Module);
            Assert.IsType<BootstrapComboBox>(catalog.Lifecycle);
            Assert.IsAssignableFrom<BootstrapListView>((object)catalog.ModuleGroups);
            Assert.IsType<BootstrapButton>(catalog.CopyKey);
            Assert.IsType<BootstrapButton>(catalog.RefreshButton);
            Assert.IsType<BootstrapLabel>(catalog.Heading);
            Assert.IsType<BootstrapPanel>(catalog.Keys.Parent);
            Assert.True(catalog.Keys.ReadOnly && !catalog.Keys.AllowUserToAddRows && !catalog.Keys.AllowUserToDeleteRows);
            Assert.True(catalog.Details.Multiline && catalog.Details.ReadOnly);
            Assert.Equal("Chi tiết định nghĩa quyền đã chọn", catalog.Details.AccessibleName);
            var row = catalog.Keys.Rows.Cast<DataGridViewRow>().Single(item => (string)item.Cells[1].Value == "Sales.Receipts.Post");
            catalog.Keys.CurrentCell = row.Cells[0];
            Assert.Contains("Sales.Receipts.Post", catalog.Details.Text);
            Assert.Equal("Sales.Receipts.Post", row.Cells[1].ToolTipText);
            Assert.True(catalog.Details.Focus());
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Surface, catalog.Details.BackColor);
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Text, catalog.Details.ForeColor);
            catalog.CopyKey.PerformClick();
            Assert.Equal("Sales.Receipts.Post", Clipboard.GetText());
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        }
        finally
        {
            if (clipboard != null) Clipboard.SetText(clipboard); else Clipboard.Clear();
            BootstrapThemeManager.CurrentTheme = prior;
        }
    });

    [Fact]
    public void Role_catalog_same_owner_render_keeps_native_member_and_catalog_rows_scroll_and_identity() => Run(SecurityScenarioId.SEC_L01, scene =>
    {
        var user = scene.Data.Users[0];
        var ids = Enumerable.Range(1000, 45).ToArray();
        foreach (var id in ids)
            scene.Data.Users.Add(new SecurityUserDto(id, "member-" + id, "Thành viên " + id, user.Email, true,
                user.AssignmentRevision, user.RoleIds, user.DirectDecisions, false));
        var role = scene.Data.Roles[0];
        scene.Data.Roles[0] = new SecurityRoleDto(role.RoleId, role.Name, role.IsActive, role.AssignmentRevision, ids, role.PermissionKeys);
        var catalog = scene.Data.Catalog;
        scene.Data.Catalog = new PermissionCatalogDto(catalog.ApplicationVersion, catalog.CatalogDigest, catalog.Items.Concat(
            ids.Select(id => new PermissionCatalogItemDto("Long.Canonical.Key." + id, "Long", "Tên quyền " + id, "Active"))).ToArray());
    }, (control, scene) =>
    {
        control.Tabs.SelectedIndex = 1;
        control.RoleDetail.Tabs.SelectedIndex = 1;
        var members = Assert.IsAssignableFrom<BootstrapListView>((object)control.RoleDetail.Members);
        control.RoleDetail.Members.SelectedIndex = 30;
        members.Items[30].Focused = true;
        members.Items[30].EnsureVisible();
        members.Focus();
        var selected = members.Items[30];
        var top = members.TopItem;
        control.ViewModel.SelectRole(201);
        Assert.Same(selected, Assert.Single(members.SelectedItems.Cast<ListViewItem>()));
        Assert.Same(selected, members.FocusedItem);
        Assert.Same(top, members.TopItem);
        Assert.True(members.ContainsFocus);
        control.Tabs.SelectedIndex = 2;
        var catalog = control.PermissionCatalog;
        catalog.ModuleGroups.SelectedIndex = catalog.Module.Items.IndexOf("Long");
        catalog.Keys.CurrentCell = catalog.Keys.Rows[30].Cells[0];
        catalog.Keys.FirstDisplayedScrollingRowIndex = 26;
        catalog.Keys.Focus();
        var row = catalog.Keys.CurrentRow;
        var scroll = catalog.Keys.FirstDisplayedScrollingRowIndex;
        control.ViewModel.SelectRole(201);
        Assert.Same(row, catalog.Keys.CurrentRow);
        Assert.Equal(scroll, catalog.Keys.FirstDisplayedScrollingRowIndex);
        Assert.True(catalog.Keys.ContainsFocus);
        Assert.Equal("Long", catalog.Module.Text);
        Assert.Equal(catalog.Module.SelectedIndex, catalog.ModuleGroups.SelectedIndex);
        Assert.Contains("Long.Canonical.Key.1030", catalog.Details.Text);
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });

    [Fact]
    public void Role_catalog_unavailable_users_scrub_profile_text_and_prevent_member_navigation() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        control.Tabs.SelectedIndex = 1;
        control.RoleDetail.Tabs.SelectedIndex = 1;
        control.RoleDetail.Members.SelectedIndex = 0;
        scene.Administration.UsersError = new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.Forbidden, "Forbidden", "Unavailable");
        Wait(control.ActivateAsync(default));
        Assert.Equal(2, control.RoleDetail.Members.Items.Count);
        control.RoleDetail.Members.SelectedIndex = 0;
        Assert.Contains("Chi tiết không khả dụng", control.RoleDetail.Members.SelectedItem!.ToString());
        Assert.DoesNotContain("@lan", control.RoleDetail.Members.SelectedItem!.ToString());
        Assert.False(control.RoleDetail.OpenUser.Enabled);
        control.RoleDetail.OpenUser.PerformClick();
        Assert.Equal(1, control.Tabs.SelectedIndex);
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void User_detail_toolkit_surfaces_preserve_identity_read_only_explanation_and_theme(bool dark) => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        var prior = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
            var detail = control.UserDetail;
            Assert.IsType<BootstrapLabel>(detail.Header);
            Assert.IsType<BootstrapLabel>(detail.HeaderMetadata);
            Assert.IsType<BootstrapLabel>(detail.HeaderState);
            Assert.IsType<BootstrapPanel>(detail.Header.Parent);
            Assert.Contains(Descendants(detail.Tabs.TabPages[0], typeof(BootstrapLabel)).Cast<BootstrapLabel>(),
                label => label.Text == control.ViewModel.SelectedUser!.DisplayName);
            Assert.IsAssignableFrom<BootstrapListView>((object)detail.Memberships);
            Assert.IsAssignableFrom<BootstrapListView>((object)detail.DirectDecisions);
            Assert.IsType<BootstrapDataGridView>(detail.Effective);
            Assert.IsType<BootstrapTextBox>((object)detail.Search);
            Assert.IsType<BootstrapComboBox>(detail.Module);
            Assert.IsType<BootstrapComboBox>(detail.Result);
            Assert.IsType<BootstrapButton>(detail.RefreshButton);
            Assert.IsType<BootstrapButton>(detail.OpenDirectPermission);
            Assert.IsType<BootstrapButton>(control.UserImportButton);
            detail.Tabs.SelectedIndex = 2;
            var row = Assert.Single(detail.Effective.Rows.Cast<DataGridViewRow>(), item => Equals(item.Tag, "Sales.Receipts.Post"));
            detail.Effective.CurrentCell = row.Cells[0];
            Assert.Contains(Environment.NewLine + "Sales.Receipts.Post", row.Cells[0].Value.ToString());
            Assert.True(row.Height >= TextRenderer.MeasureText("A" + Environment.NewLine + "A", detail.Effective.Font).Height);
            Assert.True(detail.Explanation.Multiline && detail.Explanation.ReadOnly);
            Assert.Equal("Giải thích quyền hiệu lực đã tải", detail.Explanation.AccessibleName);
            Assert.Contains("Quyền riêng: Từ chối", detail.Explanation.Text);
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Surface, detail.Explanation.BackColor);
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Text, detail.Explanation.ForeColor);
            Assert.True(detail.Explanation.Focus());
            Assert.True(detail.Explanation.ContainsFocus);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        }
        finally { BootstrapThemeManager.CurrentTheme = prior; }
    });

    [Fact]
    public void User_detail_membership_actions_keep_native_selection_and_contextual_hierarchy() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        var detail = control.UserDetail;
        detail.Tabs.SelectedIndex = 1;
        Assert.IsAssignableFrom<BootstrapListView>((object)detail.Memberships);
        detail.Memberships.SelectedIndex = 1;
        Assert.True(detail.Memberships.Focus());
        control.ViewModel.SelectUser(101);
        Assert.Equal(1, detail.Memberships.SelectedIndex);
        Assert.Contains("không cấp quyền", detail.Memberships.SelectedItem!.ToString());
        NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Up);
        Assert.Equal(0, detail.Memberships.SelectedIndex);
        var actions = Descendants(detail.Tabs.TabPages[1], typeof(BootstrapButton)).Cast<BootstrapButton>().ToArray();
        Assert.False(Assert.Single(actions, button => button.Text == "Gán vai trò…").Outline);
        var remove = Assert.Single(actions, button => button.Text == "Gỡ vai trò");
        Assert.True(remove.Outline);
        Assert.Equal(BootstrapVariant.Danger, remove.Variant);
        foreach (var action in actions)
        {
            detail.Tabs.TabPages[1].ScrollControlIntoView(action);
            Assert.True(action.Focus());
            Assert.True(detail.Tabs.TabPages[1].RectangleToScreen(detail.Tabs.TabPages[1].ClientRectangle)
                .Contains(action.RectangleToScreen(action.ClientRectangle)));
        }
        Assert.Equal(101, control.ViewModel.SelectedUserId);
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });

    [Fact]
    public void User_detail_direct_selector_uses_toolkit_without_changing_nullable_draft_or_committed_projection() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        var detail = control.UserDetail;
        detail.Tabs.SelectedIndex = 3;
        var selector = Assert.IsType<BootstrapComboBox>(Assert.Single(Descendants(detail.Tabs.TabPages[3], typeof(ComboBox))));
        selector.SelectedItem = "Sales.Receipts.Post";
        var committed = Assert.Single(detail.Effective.Rows.Cast<DataGridViewRow>(), row => Equals(row.Tag, "Sales.Receipts.Post")).Cells[1].Value;
        foreach (var choice in new[] { "Theo vai trò", "Cho phép", "Từ chối" })
        {
            Assert.Single(Descendants(detail, typeof(RadioButton)).Cast<RadioButton>(), radio => radio.Text == choice).Checked = true;
            Assert.Equal(choice == "Theo vai trò" ? (bool?)null : choice == "Cho phép", control.ViewModel.DirectDecision);
            Assert.Equal("Sales.Receipts.Post", control.ViewModel.DirectPermissionKey);
            Assert.Equal(committed, Assert.Single(detail.Effective.Rows.Cast<DataGridViewRow>(), row => Equals(row.Tag, "Sales.Receipts.Post")).Cells[1].Value);
        }
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Toolkit_master_tiles_preserve_owner_native_selection_and_theme(bool roles) => Run(SecurityScenarioId.SEC_L01, (control, _) =>
    {
        if (roles) control.Tabs.SelectedIndex = 1;
        var list = Assert.IsAssignableFrom<BootstrapListView>((object)(roles ? control.Roles : control.Users));
        Assert.IsType<BootstrapTextBox>((object)(roles ? control.RoleSearch : control.UserSearch));
        Assert.IsType<BootstrapComboBox>(roles ? control.RoleActivity : control.UserActivity);
        Assert.IsType<BootstrapLabel>(roles ? control.RolePaneStatus : control.UserPaneStatus);
        Assert.IsType<BootstrapPanel>(list.Parent);
        Assert.Equal(View.Tile, list.View);
        Assert.False(list.MultiSelect);
        Assert.False(list.HideSelection);
        Assert.Equal(2, list.Items[0].SubItems.Count);
        var item = list.Items[1];
        item.Selected = true;
        item.Focused = true;
        Assert.True(list.Focus());
        var owner = roles ? control.ViewModel.SelectedRoleId : control.ViewModel.SelectedUserId;
        Assert.Equal(roles ? 202 : 102, owner);
        var prior = BootstrapThemeManager.CurrentTheme;
        try
        {
            foreach (var mode in new[] { BootstrapThemeMode.Dark, BootstrapThemeMode.Light })
            {
                BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(mode);
                list.Refresh();
                Assert.Same(item, Assert.Single(list.SelectedItems.Cast<ListViewItem>()));
                Assert.Same(item, list.FocusedItem);
                Assert.True(list.ContainsFocus);
                Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Surface, list.BackColor);
                Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Text, list.ForeColor);
                Assert.NotEqual(list.BackColor, list.ForeColor);
                Assert.Equal(owner, roles ? control.ViewModel.SelectedRoleId : control.ViewModel.SelectedUserId);
            }
        }
        finally { BootstrapThemeManager.CurrentTheme = prior; }
    });

    [Fact]
    public void Toolkit_master_render_keeps_native_item_focus_scroll_and_full_identity() => Run(SecurityScenarioId.SEC_L01, scene =>
    {
        var original = scene.Data.Users[0];
        for (var i = 0; i < 40; i++)
            scene.Data.Users.Add(new SecurityUserDto(1000 + i, "scroll-" + i, "Người dùng " + i, original.Email,
                true, original.AssignmentRevision, original.RoleIds, original.DirectDecisions, false));
    }, (control, _) =>
    {
        var list = control.Users;
        var selected = Assert.Single(list.SelectedItems.Cast<ListViewItem>());
        selected.Focused = true;
        list.Focus();
        list.Items[list.Items.Count - 1].EnsureVisible();
        System.Windows.Forms.Application.DoEvents();
        var before = list.Items.Cast<ListViewItem>().Select(item => item.Bounds).ToArray();
        control.ViewModel.SelectUser(control.ViewModel.SelectedUserId!.Value);
        Assert.Same(selected, Assert.Single(list.SelectedItems.Cast<ListViewItem>()));
        Assert.Same(selected, list.FocusedItem);
        Assert.True(list.ContainsFocus);
        Assert.Equal(before, list.Items.Cast<ListViewItem>().Select(item => item.Bounds).ToArray());
        Assert.Contains("lan", list.AccessibleDescription);
        Assert.Contains(selected.Text, list.AccessibleDescription);
    });

    [Fact]
    public void Master_hover_does_not_replace_selected_accessible_identity() => Run(SecurityScenarioId.SEC_L01, (control, _) =>
    {
        control.ViewModel.SelectUser(102);
        var selected = control.ViewModel.SelectedUser!;
        HoverFirstRow(control.Users);
        Assert.Contains("lan", TooltipText(control.Users));
        Assert.Contains(selected.DisplayName, control.Users.AccessibleDescription);
        Assert.Contains(selected.Username, control.Users.AccessibleDescription);
        Assert.Equal(selected.UserId, control.ViewModel.SelectedUserId);
    });

    [Fact]
    public void Security_tabs_use_themed_native_headers_at_every_level() => Run(SecurityScenarioId.SEC_L01, (control, _) =>
    {
        foreach (var tabs in new[] { control.Tabs, control.UserDetail.Tabs, control.RoleDetail.Tabs })
        {
            var themed = Assert.IsType<BusinessCompactWorkspaceTabs>(tabs);
            Assert.Equal(TabDrawMode.OwnerDrawFixed, themed.DrawMode);
            Assert.Equal(BootstrapTabStyle.Underline, themed.TabStyle);
        }
    });

    [Fact]
    [Trait("Module", "Common")]
    public void Module_hosts_mount_and_close_their_own_workspace_directly() => Run(SecurityScenarioId.SEC_L01, (_, _) =>
    {
        using (var securityHost = new MyDmsVn.Desktop.UiLab.SecurityLabForm())
        {
            securityHost.Show();
            Until(() => securityHost.CurrentSecurityControl != null && !securityHost.IsResetting);
            var security = securityHost.CurrentSecurityControl!;
            Assert.IsType<SecurityWorkspaceControl>(security);
            Assert.Equal(SecurityDatasetState.Ready, security.ViewModel.Users.State);
            securityHost.CloseWorkspaceButton.PerformClick();
            Until(() => securityHost.CurrentSecurityControl == null);
            Assert.True(security.IsDisposed);
        }
        using (var salesHost = new MyDmsVn.Desktop.UiLab.SalesLabForm())
        {
            salesHost.Show();
            Until(() => salesHost.CurrentSalesControl != null && !salesHost.IsResetting);
            var sales = salesHost.CurrentSalesControl!;
            salesHost.CloseWorkspaceButton.PerformClick();
            Until(() => salesHost.CurrentSalesControl == null);
            Assert.True(sales.IsDisposed);
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Minimum_user_direct_editor_keeps_commands_and_feedback_reachable(bool dark) => Run(SecurityScenarioId.SEC_L01, (_, _) =>
    {
        var theme = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
            using var lab = new MyDmsVn.Desktop.UiLab.SecurityLabForm { ClientSize = new System.Drawing.Size(900, 600) };
            lab.Show();
            Until(() => lab.CurrentSecurityControl is { IsDisposed: false } && !lab.IsResetting && lab.StatusText.Contains("ready"));
            var workspace = lab.CurrentSecurityControl!;
            workspace.UserDetail.Tabs.SelectedIndex = 3;
            Assert.True(workspace.ViewModel.BeginDirectPermission("Sales.Receipts.Post"));
            workspace.SetImportDialogs(() => "invalid JSON", _ => false);
            workspace.UserImportButton.PerformClick();
            Wait(workspace.LastOperation);
            Assert.False(string.IsNullOrWhiteSpace(workspace.ViewModel.ImportStatus));
            var tab = workspace.UserDetail.Tabs.TabPages[3];
            var selector = Assert.Single(Descendants(tab, typeof(ComboBox)).Cast<ComboBox>());
            var buttons = Descendants(tab, typeof(Button)).Cast<Button>().ToArray();
            var save = Assert.Single(buttons, button => button.Text == "Lưu quyền này");
            var cancel = Assert.Single(buttons, button => button.Text == "Hủy");
            var labels = Descendants(tab, typeof(Label)).Cast<Label>().ToArray();
            var preview = Assert.Single(labels, label => label.Text.Contains("Kết quả dự kiến"));
            var status = Assert.Single(labels, label => label.Text == workspace.ViewModel.ImportStatus);
            // Regression: fixed docked regions can clip commands/feedback even if the list has a minimum height.
            foreach (var item in new Control[] { status, selector }.Concat(Descendants(tab, typeof(RadioButton)))
                .Concat(new Control[] { workspace.UserImportButton, preview, save, cancel }))
            {
                tab.ScrollControlIntoView(item);
                if (item.CanSelect) Assert.True(item.Focus());
                System.Windows.Forms.Application.DoEvents();
                var viewport = tab.RectangleToScreen(tab.ClientRectangle);
                var bounds = item.RectangleToScreen(item.ClientRectangle);
                Assert.True(item.Visible && viewport.Contains(bounds), $"{item.AccessibleName ?? item.Text}: {bounds}; Direct viewport {viewport}");
                var region = item;
                while (region.Parent != tab) region = region.Parent!;
                var center = tab.PointToClient(new System.Drawing.Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
                Assert.Same(region, tab.GetChildAtPoint(center));
            }
            Assert.False(tab.HorizontalScroll.Visible);
            Assert.False(workspace.HorizontalScroll.Visible);
            workspace.ViewModel.SetDirectDecision(true);
            selector.Focus();
            NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
            Assert.True(workspace.ViewModel.HasDirtyEditor);
            cancel.PerformClick();
            Assert.False(workspace.ViewModel.HasDirtyEditor);
            Assert.False(Assert.Single(workspace.ViewModel.SelectedUser!.DirectDecisions, decision => decision.PermissionKey == "Sales.Receipts.Post").Decision);
            lab.CloseWorkspaceButton.PerformClick();
        }
        finally { BootstrapThemeManager.CurrentTheme = theme; }
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
            AssertHeaderUsesActionRows(control.UserDetail.HeaderActions);
            control.Tabs.SelectedIndex = 1;
            System.Windows.Forms.Application.DoEvents();
            Assert.True(control.RoleDetail.GrantArea.Width > 0);
            Assert.True(control.RoleDetail.HeaderActions.Controls.Cast<Control>().Last().Right <= control.RoleDetail.HeaderActions.ClientSize.Width);
            AssertHeaderUsesActionRows(control.RoleDetail.HeaderActions);
        }
        finally { BootstrapThemeManager.CurrentTheme = theme; }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Minimum_role_view_keeps_grant_choices_visible(bool dark) => Run(SecurityScenarioId.SEC_L01, (_, _) =>
    {
        var theme = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
            using var lab = new MyDmsVn.Desktop.UiLab.SecurityLabForm { ClientSize = new System.Drawing.Size(900, 600) };
            lab.Show();
            Until(() => lab.CurrentSecurityControl is { IsDisposed: false } && !lab.IsResetting && lab.StatusText.Contains("ready"));
            var workspace = lab.CurrentSecurityControl!;
            workspace.Tabs.SelectedIndex = 1;
            workspace.RoleDetail.Tabs.SelectedIndex = 0;
            lab.PerformLayout();
            System.Windows.Forms.Application.DoEvents();
            var grants = Descendants(workspace.RoleDetail.GrantArea, typeof(BootstrapDataGridView)).Cast<BootstrapDataGridView>().Single();
            Assert.True(grants.Visible);
            Assert.True(grants.Height >= 60,
                $"Grant list height {grants.Height}; area {workspace.RoleDetail.GrantArea.Height}; tab {workspace.RoleDetail.Tabs.Height}");
            Assert.True(workspace.ViewModel.BeginRolePermissions());
            workspace.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
            Assert.True(grants.Height >= grants.ColumnHeadersHeight + grants.Rows[0].Height,
                $"Editing grant grid height {grants.Height}; header {grants.ColumnHeadersHeight}; row {grants.Rows[0].Height}");
            var search = Assert.Single(Descendants(workspace.RoleDetail.GrantArea, typeof(BootstrapTextBox)).Cast<BootstrapTextBox>());
            foreach (Control filter in search.Parent!.Controls)
            {
                Assert.True(filter.Bottom <= search.Parent.ClientSize.Height, $"Filter {filter.AccessibleName ?? filter.Text} bottom {filter.Bottom}; parent {search.Parent.ClientSize}");
                Assert.True(filter.Right <= search.Parent.ClientSize.Width);
            }
            var save = Assert.Single(Descendants(workspace.RoleDetail.GrantArea, typeof(Button)).Cast<Button>(), button => button.Text == "Lưu quyền vai trò");
            Assert.True(save.Focus());
            var viewport = workspace.RoleDetail.GrantArea.RectangleToScreen(workspace.RoleDetail.GrantArea.ClientRectangle);
            var saveBounds = save.RectangleToScreen(save.ClientRectangle);
            Assert.True(viewport.Contains(saveBounds), $"Save {saveBounds}; grant viewport {viewport}");
            workspace.ViewModel.DiscardEditor();
            lab.CloseWorkspaceButton.PerformClick();
        }
        finally { BootstrapThemeManager.CurrentTheme = theme; }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Minimum_user_view_shows_direct_permission_rows(bool dark) => Run(SecurityScenarioId.SEC_L01, (_, _) =>
    {
        var theme = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
            using var lab = new MyDmsVn.Desktop.UiLab.SecurityLabForm { ClientSize = new System.Drawing.Size(900, 600) };
            lab.Show();
            Until(() => lab.CurrentSecurityControl is { IsDisposed: false } && !lab.IsResetting && lab.StatusText.Contains("ready"));
            var workspace = lab.CurrentSecurityControl!;
            workspace.Tabs.SelectedIndex = 0;
            workspace.UserDetail.Tabs.SelectedIndex = 3;
            lab.PerformLayout();
            System.Windows.Forms.Application.DoEvents();
            var direct = workspace.UserDetail.DirectDecisions;
            Assert.Equal(2, direct.Items.Count);
            Assert.True(direct.Visible);
            Assert.True(direct.ClientSize.Height >= direct.ItemHeight * 2,
                $"Direct list client height {direct.ClientSize.Height}; item height {direct.ItemHeight}; tab height {workspace.UserDetail.Tabs.Height}");
            lab.CloseWorkspaceButton.PerformClick();
        }
        finally { BootstrapThemeManager.CurrentTheme = theme; }
    });

    [Theory]
    [InlineData(900, 600, false)]
    [InlineData(900, 600, true)]
    [InlineData(1200, 760, false)]
    [InlineData(1200, 760, true)]
    public void Partial_save_recovery_actions_fit_at_review_sizes(int width, int height, bool dark) => Run(SecurityScenarioId.SEC_L08, (control, _) =>
    {
        var theme = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(dark ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
            var form = control.FindForm()!;
            form.Size = new System.Drawing.Size(width, height);
            control.Tabs.SelectedIndex = 1;
            control.ViewModel.BeginRolePermissions();
            control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
            control.ViewModel.SetRoleGranted("Sales.Receipts.Read", false);
            control.ViewModel.SetRoleGranted("Sales.Receipts.Write", false);
            Wait(control.ViewModel.SaveEditorAsync(default));
            form.PerformLayout();
            System.Windows.Forms.Application.DoEvents();

            var status = Descendants(control.RoleDetail, typeof(Label)).Cast<Label>()
                .Single(label => label.Text.StartsWith("Đã lưu 1;", StringComparison.Ordinal));
            var recovery = status.Parent!;
            Assert.True(recovery.Visible);
            Assert.All(recovery.Controls.Cast<Control>().Where(item => item.Visible), item =>
            {
                Assert.InRange(item.Right, 1, recovery.ClientSize.Width);
                Assert.InRange(item.Bottom, 1, recovery.ClientSize.Height);
            });
        }
        finally { BootstrapThemeManager.CurrentTheme = theme; }
    });

    [Fact]
    public void Partial_forbidden_notice_remains_visible_after_role_data_is_revoked() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        control.Tabs.SelectedIndex = 1;
        scene.Administration.MutationErrors.Enqueue(null);
        scene.Administration.MutationErrors.Enqueue(new MyDmsVn.Contracts.ApiError(
            MyDmsVn.Contracts.ApiStatusCode.Forbidden, "Forbidden", "Review response."));
        control.ViewModel.BeginRolePermissions();
        control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
        control.ViewModel.SetRoleGranted("Sales.Receipts.Read", false);
        control.ViewModel.SetRoleGranted("Sales.Receipts.Write", false);
        Wait(control.ViewModel.SaveEditorAsync(default));
        System.Windows.Forms.Application.DoEvents();

        Assert.Equal(SecurityDatasetState.Forbidden, control.ViewModel.Roles.State);
        Assert.Equal(SecuritySaveCompletion.Partial, control.ViewModel.LastSaveOutcome!.Completion);
        Assert.Single(control.ViewModel.LastSaveOutcome.ConfirmedKeys);
        Assert.Single(control.ViewModel.LastSaveOutcome.RejectedKeys);
        Assert.Single(control.ViewModel.LastSaveOutcome.UnattemptedKeys);
        Assert.Empty(control.ViewModel.Roles.Items);
        Assert.Null(control.ViewModel.SelectedRole);
        Assert.True(control.ViewModel.RequiresRecovery);
        Assert.False(control.ViewModel.CanNavigate);
        Assert.True(control.CaptureCloseState().RequiresOutcomeReview);
        var admission = control.AdmitNavigationAsync();
        Wait(admission);
        Assert.False(admission.Result);
        var notice = Descendants(control.RoleDetail, typeof(Label)).Cast<Label>()
            .Single(label => label.Text.Contains("Đã lưu 1") && label.Text.Contains("Không đủ quyền"));
        Assert.True(notice.Visible);
        Assert.Contains(notice.Parent!.Controls.OfType<Button>(), button => button.Visible && button.Text == "Tải lại để đối chiếu");
        Wait(control.ViewModel.LoadAsync(default));
        System.Windows.Forms.Application.DoEvents();
        Assert.True(notice.Parent.Visible);
        Assert.Equal(SecurityDatasetState.Forbidden, control.ViewModel.Roles.State);
        Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate.Result" &&
            entry.Request is SecurityResponseJournalEntry result && result.Outcome == "Forbidden");
        var reload = control.ViewModel.ReloadForComparisonAsync(default);
        Wait(reload);
        Assert.True(reload.Result);
        Assert.True(control.ViewModel.CanReviewRecovery);
        Assert.True(control.ViewModel.ReviewRecoveryAndRebase());
        Assert.False(control.ViewModel.RequiresRecovery);
        Assert.Contains(control.ViewModel.RoleDraft!.Changes, change => change.PermissionKey == "Sales.Receipts.Read");
        Assert.Contains(control.ViewModel.RoleDraft.Changes, change => change.PermissionKey == "Sales.Receipts.Write");
        Assert.DoesNotContain(control.ViewModel.RoleDraft.Changes, change => change.PermissionKey == "Sales.Receipts.Post");
        Assert.False(control.ViewModel.CanNavigate);
        Assert.Collection(scene.Journal.Entries.Where(entry => entry.Operation == "Security.Mutate"), _ => { }, _ => { });
    });
    [Fact]
    public void Rejected_membership_shows_scoped_sanitized_feedback_without_changing_membership() => Run(SecurityScenarioId.SEC_L04, (control, scene) =>
    {
        control.UserDetail.Tabs.SelectedIndex = 1;
        var originalRoles = control.ViewModel.SelectedUser!.RoleIds.ToArray();
        scene.Administration.MutationErrors.Enqueue(new MyDmsVn.Contracts.ApiError(
            MyDmsVn.Contracts.ApiStatusCode.BadRequest, "ValidationError", "raw server secret"));

        var mutation = control.ViewModel.SetMembershipAsync(202, true, default);
        Wait(mutation);
        Assert.False(mutation.Result);
        Assert.Equal(originalRoles, control.ViewModel.SelectedUser!.RoleIds);
        Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Users.State);
        Assert.Equal(SecurityDatasetState.Ready, control.ViewModel.Roles.State);
        Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        var feedback = Assert.Single(Descendants(control.UserDetail, typeof(Label)).Cast<Label>(),
            label => label.AccessibleName == "Thông báo gán vai trò");
        Assert.True(feedback.Visible);
        Assert.NotEmpty(feedback.Text);
        Assert.DoesNotContain("raw", feedback.Text);
        control.ViewModel.SelectUser(102);
        Assert.False(feedback.Visible);
    });
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rejected_status_change_shows_scoped_sanitized_feedback(bool roleTarget) => Run(SecurityScenarioId.SEC_L03, (control, scene) =>
    {
        if (roleTarget) control.Tabs.SelectedIndex = 1;
        var target = roleTarget ? SecurityAdministrationTargetDto.Role : SecurityAdministrationTargetDto.User;
        var targetId = roleTarget ? 201 : 101;
        var revision = roleTarget ? control.ViewModel.SelectedRole!.AssignmentRevision : control.ViewModel.SelectedUser!.AssignmentRevision;
        scene.Administration.MutationErrors.Enqueue(new MyDmsVn.Contracts.ApiError(
            MyDmsVn.Contracts.ApiStatusCode.BadRequest, "ValidationError", "raw server secret"));

        var mutation = control.ViewModel.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(
            target, SecurityAdministrationOperationDto.SetActive, targetId, isActive: false, expectedRevision: revision), default);
        Wait(mutation);
        Assert.False(mutation.Result);
        Assert.True(roleTarget ? control.ViewModel.SelectedRole!.IsActive : control.ViewModel.SelectedUser!.IsActive);
        Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        var pane = roleTarget ? (Control)control.RoleDetail : control.UserDetail;
        var feedback = Assert.Single(Descendants(pane, typeof(Label)).Cast<Label>(),
            label => label.AccessibleName == "Thông báo đổi trạng thái");
        Assert.True(feedback.Visible);
        Assert.NotEmpty(feedback.Text);
        Assert.DoesNotContain("raw", feedback.Text);
        if (roleTarget) control.ViewModel.SelectRole(202);
        else control.ViewModel.SelectUser(102);
        Assert.False(feedback.Visible);
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
        Assert.InRange(control.Users.TileSize.Height, 56, 80);
        Assert.Contains("lan", control.Users.Items[0].SubItems[1].Text);
        AssertActionReachable(control.UserDetail.HeaderActions);
        control.Tabs.SelectedIndex = 1;
        System.Windows.Forms.Application.DoEvents();
        Assert.InRange(control.Roles.Parent!.Width, width == 900 ? 180 : 240, width == 900 ? 220 : 280);
        Assert.True(control.Roles.ClientSize.Width - 20 >= 150);
        Assert.Contains("thành viên", control.Roles.Items[0].SubItems[1].Text);
        AssertActionReachable(control.RoleDetail.HeaderActions);
    });
    [Theory]
    [InlineData(MyDmsVn.Contracts.ApiStatusCode.Forbidden)]
    [InlineData(MyDmsVn.Contracts.ApiStatusCode.InternalServerError)]
    public void Master_tooltip_scrubs_identity_on_rebind_forbidden_and_session_replacement(MyDmsVn.Contracts.ApiStatusCode status) => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
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
        scene.Administration.UsersError = new MyDmsVn.Contracts.ApiError(status, "Unavailable", "Unavailable");
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
        Assert.Equal(owner, ((SecurityUserDto)Assert.Single(control.Users.SelectedItems.Cast<ListViewItem>()).Tag).UserId);
        control.PermissionCatalog.Lifecycle.SelectedIndex = 2;
        Assert.Single(control.PermissionCatalog.Keys.Rows);
        Assert.Equal("Legacy.Deprecated", control.PermissionCatalog.Keys.Rows[0].Cells[1].Value);
        Assert.Contains("1 /", control.PermissionCatalog.Count.Text);
        control.PermissionCatalog.Search.Text = "no-match";
        Assert.Empty(control.PermissionCatalog.Keys.Rows);
        Assert.Contains("0 /", control.PermissionCatalog.Count.Text);
        Assert.Contains("Không có quyền phù hợp bộ lọc", control.PermissionCatalog.Details.Text);
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
        Assert.Equal("Vai trò (2)", control.UserDetail.Tabs.TabPages[1].Text);
        Assert.Equal("Thành viên (2)", control.RoleDetail.Tabs.TabPages[1].Text);
        Assert.DoesNotContain("Thành viên", control.RoleDetail.Header.Text);
    });
    [Fact]
    public void Catalog_has_no_assignment_actions() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        control.Tabs.SelectedIndex = 2;
        Assert.Equal(data.Data.Catalog.Items.Count, control.PermissionCatalog.Keys.Rows.Count);
        // The toolkit search composite owns its native clear button; it is not a catalog command.
        var actions = Descendants(control.PermissionCatalog, typeof(Button)).Cast<Button>()
            .Where(button => !control.PermissionCatalog.Search.Contains(button)).Select(button => button.Text).ToArray();
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
        Assert.True(control.Users.OwnerDraw);
        Assert.True(control.Users.TileSize.Height >= 50);
        Assert.True(control.Roles.OwnerDraw);
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
    public void Role_grant_table_keeps_friendly_name_canonical_target_and_group_visible() => Run(SecurityScenarioId.SEC_L01, (control, _) =>
    {
        control.Tabs.SelectedIndex = 1;
        var grants = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapDataGridView)).Cast<BootstrapDataGridView>());
        Assert.Equal(new[] { "Cấp", "Quyền", "Nhóm" }, grants.Columns.Cast<DataGridViewColumn>().Select(column => column.HeaderText));
        var index = GrantIndex(grants, "Sales.Receipts.Read");
        Assert.True(index >= 0);
        Assert.Contains("Xem phiếu bán hàng", grants.Rows[index].Cells[1].Value!.ToString());
        Assert.Contains("Sales.Receipts.Read", grants.Rows[index].Cells[1].Value!.ToString());
        Assert.Equal("Sales", grants.Rows[index].Cells[2].Value);
        Assert.DoesNotContain(grants.Rows.Cast<DataGridViewRow>(), row => (string?)row.Tag == "Legacy.Orphan");
    });

    [Fact]
    public void Role_grant_review_fixture_exposes_long_text_through_active_group_and_search() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        const string key = "UiLab.ReviewOnly.LongCanonicalPermissionKeyForNativeRoleGridOverflowAndTooltipInspection";
        const string label = "Chỉ dùng để duyệt giao diện UI Lab: kiểm tra nhãn quyền tiếng Việt rất dài, xuống dòng và khả năng đọc đầy đủ trong bảng quyền của vai trò";
        control.Tabs.SelectedIndex = 1;
        var grants = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapDataGridView)).Cast<BootstrapDataGridView>());
        var modules = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapComboBox)).Cast<BootstrapComboBox>());
        var search = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapTextBox)).Cast<BootstrapTextBox>());
        Assert.Contains("UiLab.ReviewOnly", modules.Items.Cast<string>());
        Assert.DoesNotContain("Legacy", modules.Items.Cast<string>());
        modules.SelectedItem = "UiLab.ReviewOnly";
        var row = Assert.Single(grants.Rows.Cast<DataGridViewRow>());
        Assert.Equal(key, row.Tag);
        Assert.Equal(label + Environment.NewLine + key, row.Cells[1].Value);
        Assert.Equal("UiLab.ReviewOnly", row.Cells[2].Value);
        Assert.Contains(label, row.Cells[1].ToolTipText);
        Assert.Contains(key, row.Cells[1].ToolTipText);
        Assert.False(Granted(grants, 0));
        foreach (var query in new[] { "nhãn quyền tiếng Việt rất dài", "LongCanonicalPermissionKeyForNativeRoleGrid" })
        {
            search.Text = query;
            Assert.Equal(key, Assert.Single(grants.Rows.Cast<DataGridViewRow>()).Tag);
        }
        search.Text = "Sales.Receipts.Read";
        Assert.Empty(grants.Rows.Cast<DataGridViewRow>());
        modules.SelectedItem = "Sales";
        Assert.Equal("Sales.Receipts.Read", Assert.Single(grants.Rows.Cast<DataGridViewRow>()).Tag);
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });

    [Fact]
    public void Role_grant_large_catalog_native_search_reuses_unchanged_rows_without_dispatch() =>
        Run(SecurityScenarioId.SEC_L01, scene =>
        {
            var catalog = scene.Data.Catalog;
            var extras = Enumerable.Range(0, 600)
                .Select(index => new PermissionCatalogItemDto("Zp.Review." + index.ToString("D4"), "Zp.Review",
                    "Zp review permission " + index.ToString("D4"), "Active"));
            scene.Data.Catalog = new PermissionCatalogDto(catalog.ApplicationVersion, catalog.CatalogDigest,
                catalog.Items.Concat(extras).ToArray());
        }, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            NativeLookupTypingDriver.Activate(control.FindForm()!);
            var search = Assert.Single(Descendants(control.RoleDetail, typeof(BootstrapTextBox)).Cast<BootstrapTextBox>(),
                input => input.AccessibleName == "Tìm quyền vai trò");
            var grants = Assert.Single(Descendants(control.RoleDetail, typeof(BootstrapDataGridView)).Cast<BootstrapDataGridView>());
            var driver = new NativeLookupTypingDriver(search);
            driver.TypeCharacter('Z');
            Assert.Equal(600, grants.Rows.Count);
            var row = grants.Rows[0];
            var display = row.Cells[1].Value;
            var tooltip = row.Cells[1].ToolTipText;

            driver.TypeCharacter('p');

            Assert.Equal("Zp", driver.Text);
            Assert.Equal(2, driver.SelectionStart);
            Assert.Equal(0, driver.SelectionLength);
            Assert.True(search.ContainsFocus);
            Assert.Equal(600, grants.Rows.Count);
            Assert.Same(row, grants.Rows[0]);
            Assert.Same(display, grants.Rows[0].Cells[1].Value);
            Assert.Same(tooltip, grants.Rows[0].Cells[1].ToolTipText);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Role_grant_duplicate_active_key_preserves_first_definition_without_render_failure() =>
        Run(SecurityScenarioId.SEC_L01, scene =>
        {
            var catalog = scene.Data.Catalog;
            var first = catalog.Items.First(item => item.Key == "Sales.Receipts.Read");
            var duplicate = new PermissionCatalogItemDto(first.Key, "Duplicate.Module", "Duplicate definition", "Active");
            scene.Data.Catalog = new PermissionCatalogDto(catalog.ApplicationVersion, catalog.CatalogDigest,
                catalog.Items.Concat(new[] { duplicate }).ToArray());
        }, (control, scene) =>
        {
            control.Tabs.SelectedIndex = 1;
            var search = Assert.Single(Descendants(control.RoleDetail, typeof(BootstrapTextBox)).Cast<BootstrapTextBox>(),
                input => input.AccessibleName == "Tìm quyền vai trò");
            search.Text = "Sales.Receipts.Read";
            var grants = Assert.Single(Descendants(control.RoleDetail, typeof(BootstrapDataGridView)).Cast<BootstrapDataGridView>());

            var row = Assert.Single(grants.Rows.Cast<DataGridViewRow>());
            Assert.Equal("Sales", row.Cells[2].Value);
            search.Text = "Duplicate definition";
            Assert.Empty(grants.Rows.Cast<DataGridViewRow>());
            var directKey = Assert.Single(Descendants(control.UserDetail, typeof(ComboBox)).Cast<ComboBox>(),
                combo => combo.AccessibleName == "Quyền riêng");
            Assert.Equal(directKey.Items.Cast<string>().Distinct(StringComparer.Ordinal).Count(), directKey.Items.Count);
            Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Fact]
    public void Role_editor_has_filters_history_and_contextual_recovery() => Run(SecurityScenarioId.SEC_L01, (control, _) =>
    {
        control.Tabs.SelectedIndex = 1;
        Assert.Equal("", control.RolePaneStatus.Text);
        Assert.Contains("Sửa quyền", control.RoleDetail.HeaderActions.Controls.Cast<Control>().OfType<Button>().Select(button => button.Text));
        Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapDataGridView)));
        Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapTextBox)));
        Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapComboBox)));
        Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapCheckBox)));
        Assert.DoesNotContain(Descendants(control, typeof(FlowLayoutPanel)).Cast<FlowLayoutPanel>(), panel => panel.Visible && panel.Controls.Cast<Control>().OfType<Button>().Any(button => button.Text == "Tải lại để đối chiếu"));
    });
    [Fact]
    public void Role_grant_actions_follow_draft_and_busy_state() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        control.Tabs.SelectedIndex = 1;
        var buttons = Descendants(control.RoleDetail.GrantArea, typeof(Button)).Cast<Button>().ToArray();
        var save = Assert.Single(buttons, button => button.Text == "Lưu quyền vai trò");
        var discard = Assert.Single(buttons, button => button.Text == "Hủy thay đổi");
        Assert.False(save.Enabled);
        Assert.False(save.Visible);
        Assert.False(discard.Visible);

        Assert.True(control.ViewModel.BeginRolePermissions());
        Assert.False(save.Enabled);
        Assert.True(discard.Enabled);
        Assert.True(save.Visible);
        Assert.False(Assert.IsType<BootstrapButton>(save).Outline);
        Assert.True(Assert.IsType<BootstrapButton>(discard).Outline);
        control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
        Assert.True(save.Enabled);
        var gate = scene.Administration.HoldNextMutation(true);
        save.PerformClick();
        Assert.True(control.ViewModel.IsBusy);
        Assert.False(save.Enabled);
        Assert.False(discard.Enabled);
        gate.Complete(MyDmsVn.Contracts.ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(22, true)));
        Wait(control.LastOperation);
        Assert.False(save.Enabled);
        Assert.False(discard.Enabled);

        Assert.True(control.ViewModel.BeginRolePermissions());
        control.ViewModel.SetRoleGranted("Inventory.Stock.Read", true);
        Assert.True(discard.Enabled);
        discard.PerformClick();
        Assert.False(save.Enabled);
        Assert.False(discard.Enabled);
    });

    [Theory]
    [InlineData(MyDmsVn.Contracts.ApiStatusCode.Forbidden)]
    [InlineData(MyDmsVn.Contracts.ApiStatusCode.InternalServerError)]
    public void Membership_actions_disable_when_roles_are_unavailable_or_user_is_protected(MyDmsVn.Contracts.ApiStatusCode unavailable) => Run(SecurityScenarioId.SEC_L04, (control, scene) =>
    {
        control.UserDetail.Tabs.SelectedIndex = 1;
        var buttons = Descendants(control.UserDetail.Tabs.TabPages[1], typeof(Button)).Cast<Button>().ToArray();
        var assign = Assert.Single(buttons, button => button.Text == "Gán vai trò…");
        var remove = Assert.Single(buttons, button => button.Text == "Gỡ vai trò");
        Assert.True(assign.Enabled);
        Assert.False(remove.Enabled);
        control.UserDetail.Memberships.SelectedIndex = 0;
        Assert.True(remove.Enabled);

        control.ViewModel.SelectUser(103);
        Assert.False(assign.Enabled);
        Assert.False(remove.Enabled);
        Assert.Contains("bảo vệ", assign.AccessibleDescription);
        assign.PerformClick();
        remove.PerformClick();
        control.ViewModel.SelectUser(101);

        scene.Administration.RolesError = new MyDmsVn.Contracts.ApiError(unavailable, "RolesUnavailable", "Review response.");
        Wait(control.ActivateAsync(default));
        Assert.False(assign.Enabled);
        Assert.False(remove.Enabled);
        Assert.Contains("Vai trò", assign.AccessibleDescription);
        assign.PerformClick();
        remove.PerformClick();
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });
    [Fact]
    public void Membership_forbidden_response_disables_actions_with_permission_context() => Run(SecurityScenarioId.SEC_L04, (control, scene) =>
    {
        control.UserDetail.Tabs.SelectedIndex = 1;
        var buttons = Descendants(control.UserDetail.Tabs.TabPages[1], typeof(Button)).Cast<Button>().ToArray();
        var assign = Assert.Single(buttons, button => button.Text == "Gán vai trò…");
        var remove = Assert.Single(buttons, button => button.Text == "Gỡ vai trò");
        control.UserDetail.Memberships.SelectedIndex = 0;
        scene.Administration.MutationErrors.Enqueue(new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.Forbidden, "Forbidden", "Review response."));

        Wait(control.ViewModel.SetMembershipAsync(203, false, default));
        Assert.Equal(SecurityDatasetState.Forbidden, control.ViewModel.Users.State);
        Assert.False(assign.Enabled);
        Assert.False(remove.Enabled);
        Assert.Contains("Không đủ quyền", assign.AccessibleDescription);
        Assert.Contains("Không đủ quyền", control.ViewModel.EditingStatus);
    });
    [Fact]
    public void Role_filter_retains_hidden_draft_and_read_only_orphan() => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        control.Tabs.SelectedIndex = 1;
        Assert.Contains("Legacy.Orphan", control.RoleDetail.Permissions.Items[0]!.ToString());
        var edit = Assert.Single(Descendants(control.RoleDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Sửa quyền");
        edit.PerformClick();
        var grants = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapDataGridView)).Cast<BootstrapDataGridView>());
        Assert.Contains("Xem phiếu bán hàng", grants.Rows[0].Cells[1].Value!.ToString());
        var index = GrantIndex(grants, "Sales.Receipts.Post");
        ClickGrant(grants, index);
        Until(() => control.ViewModel.RoleDraft?.Changes.Count == 1);
        Assert.Contains("Chưa lưu", grants.Rows[index].Cells[1].Value!.ToString());
        var search = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapTextBox)).Cast<BootstrapTextBox>());
        search.Text = "Receipts.Read";
        Assert.Single(grants.Rows.Cast<DataGridViewRow>());
        Assert.Equal("Sales.Receipts.Read", grants.Rows[0].Tag);
        Assert.Single(control.ViewModel.RoleDraft!.Changes);
        Assert.Contains(Descendants(control.RoleDetail.GrantArea, typeof(Label)).Cast<Label>(), label => label.Text.StartsWith("1 thay đổi", StringComparison.Ordinal));
        Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation.Contains("Mutate"));
    });
    [Fact]
    public void Role_filters_preserve_selected_key_hidden_changes_and_full_draft_count() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        control.Tabs.SelectedIndex = 1;
        Assert.True(control.ViewModel.BeginRolePermissions());
        var grants = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapDataGridView)).Cast<BootstrapDataGridView>());
        var modules = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapComboBox)).Cast<BootstrapComboBox>());
        var grantedOnly = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapCheckBox)).Cast<BootstrapCheckBox>());
        ClickGrant(grants, GrantIndex(grants, "Sales.Receipts.Post"));
        Assert.Equal("Sales.Receipts.Post", grants.CurrentRow!.Tag);
        modules.SelectedItem = "Sales";
        Assert.Equal("Sales.Receipts.Post", grants.CurrentRow!.Tag);
        grantedOnly.Checked = true;
        Assert.Equal("Sales.Receipts.Post", grants.CurrentRow!.Tag);
        ClickGrant(grants, GrantIndex(grants, "Sales.Receipts.Read"));
        Assert.Equal(-1, GrantIndex(grants, "Sales.Receipts.Read"));
        modules.SelectedItem = "Inventory";
        Assert.Empty(grants.Rows.Cast<DataGridViewRow>());
        Assert.Equal(2, control.ViewModel.RoleDraft!.Changes.Count);
        Assert.Contains(Descendants(control.RoleDetail.GrantArea, typeof(Label)).Cast<Label>(), label => label.Text.StartsWith("2 thay đổi", StringComparison.Ordinal));
        modules.SelectedIndex = 0;
        grantedOnly.Checked = false;
        Assert.False(Granted(grants, GrantIndex(grants, "Sales.Receipts.Read")));
        Assert.True(Granted(grants, GrantIndex(grants, "Sales.Receipts.Post")));
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });

    [Fact]
    public void Edit_role_permissions_from_members_opens_grants_only_after_editor_admission() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        control.Tabs.SelectedIndex = 1;
        control.RoleDetail.Tabs.SelectedIndex = 1;
        control.RoleDetail.Members.SelectedIndex = 1;
        var edit = Assert.Single(control.RoleDetail.HeaderActions.Controls.Cast<Control>().OfType<Button>(), button => button.Text == "Sửa quyền");
        var search = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapTextBox)).Cast<BootstrapTextBox>());

        edit.PerformClick();
        Assert.Equal(0, control.RoleDetail.Tabs.SelectedIndex);
        Assert.Equal(201, control.ViewModel.RoleDraft?.RoleId);
        Assert.True(search.ContainsFocus);
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");

        control.ViewModel.DiscardEditor();
        control.RoleDetail.Tabs.SelectedIndex = 1;
        Assert.True(control.ViewModel.BeginDirectPermission("Sales.Receipts.Post"));
        control.ViewModel.SetDirectDecision(true);
        edit.PerformClick();
        Assert.Equal(1, control.RoleDetail.Tabs.SelectedIndex);
        Assert.Null(control.ViewModel.RoleDraft);
        Assert.True(control.ViewModel.HasDirtyEditor);
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });
    [Fact]
    public void Role_member_selection_survives_unrelated_render_and_opens_the_same_user() => Run(SecurityScenarioId.SEC_L01, (control, _) =>
    {
        control.Tabs.SelectedIndex = 1;
        control.RoleDetail.Tabs.SelectedIndex = 1;
        var members = control.RoleDetail.Members;
        members.Height = 22;
        members.SelectedIndex = 1;
        members.TopIndex = 1;
        var top = members.TopIndex;

        Assert.True(control.ViewModel.BeginRolePermissions());
        Assert.Equal(1, members.SelectedIndex);
        Assert.Contains("@inactive", members.SelectedItem!.ToString());
        Assert.Equal(top, members.TopIndex);
        Assert.True(control.RoleDetail.OpenUser.Enabled);
        control.RoleDetail.OpenUser.PerformClick();
        Assert.Equal(102, control.ViewModel.SelectedUserId);
    });
    [Fact]
    public void User_membership_selection_survives_same_owner_render_and_clears_on_owner_change() => Run(SecurityScenarioId.SEC_L01, (control, _) =>
    {
        var memberships = control.UserDetail.Memberships;
        memberships.Height = 22;
        memberships.SelectedIndex = 1;
        memberships.TopIndex = 1;
        var top = memberships.TopIndex;

        control.ViewModel.SelectUser(101);
        Assert.Equal(1, memberships.SelectedIndex);
        Assert.Contains("Vai trò ngừng hoạt động", memberships.SelectedItem!.ToString());
        Assert.Equal(top, memberships.TopIndex);
        control.ViewModel.SelectUser(102);
        Assert.Equal(-1, memberships.SelectedIndex);
    });
    [Fact]
    public void Role_checkbox_does_not_replay_into_replacement_draft() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        control.Tabs.SelectedIndex = 1;
        Assert.True(control.ViewModel.BeginRolePermissions());
        var grants = Assert.Single(Descendants(control.RoleDetail.GrantArea, typeof(BootstrapDataGridView)).Cast<BootstrapDataGridView>());
        var index = GrantIndex(grants, "Sales.Receipts.Post");
        Assert.True(index >= 0);
        ClickGrant(grants, index);
        Assert.Single(control.ViewModel.RoleDraft!.Changes);
        control.ViewModel.DiscardEditor();
        control.ViewModel.SelectRole(202);
        Assert.Equal(202, control.ViewModel.SelectedRoleId);
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
            var grants = Assert.Single(Descendants(control.RoleDetail, typeof(BootstrapDataGridView)).Cast<BootstrapDataGridView>());
            Assert.True(grants.Enabled);
            Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Text, grants.ForeColor);
            var before = Granted(grants, 0);
            ClickGrant(grants, 0);
            Assert.Equal(before, Granted(grants, 0));
            Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation.Contains("Mutate"));
            var edit = Assert.Single(Descendants(control.RoleDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Sửa quyền");
            edit.PerformClick();
            ClickGrant(grants, 0);
            Assert.Equal(!before, Granted(grants, 0));
        }
        finally { BootstrapThemeManager.CurrentTheme = priorTheme; }
    });
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Secret_dialog_closes_and_clears_input_on_owner_boundary(bool create, bool disposeOwner) => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        var command = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == (create ? "Tạo người dùng…" : "Đặt lại mật khẩu…"));
        ExceptionDispatchInfo? failure = null;
        BootstrapTextBox? secret = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => create ? form.Text == "Tạo người dùng" : form.Text.StartsWith("Đặt lại mật khẩu — "));
            if (dialog == null) return;
            timer.Stop();
            try
            {
                secret = Assert.Single(DialogInputs(dialog).OfType<BootstrapTextBox>(), input => input.UseSystemPasswordChar);
                secret.Text = "session-bound-fixture";
                if (disposeOwner) control.Dispose();
                else data.Session.Replace();
                Assert.Empty(secret.Text);
                Assert.NotEqual(DialogResult.None, dialog.DialogResult);
                Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            finally { dialog.Close(); }
        };
        timer.Start(); command.PerformClick();
        Assert.False(timer.Enabled);
        failure?.Throw();
        Assert.NotNull(secret);
        Assert.True(secret!.IsDisposed);
        Assert.Empty(secret.Text);
    });

    [Theory]
    [InlineData("edit-user")]
    [InlineData("create-role")]
    [InlineData("edit-role")]
    [InlineData("password")]
    public void Remaining_identity_dialogs_keep_toolkit_validation_native_input_and_open_theme(string kind) => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        var roles = kind.Contains("role");
        if (roles) control.Tabs.SelectedIndex = 1;
        var caption = kind == "edit-user" ? "Sửa thông tin…" : kind == "create-role" ? "Tạo vai trò…" : kind == "edit-role" ? "Sửa tên…" : "Đặt lại mật khẩu…";
        var command = Assert.Single(Descendants(roles ? (Control)control.RoleDetail : control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == caption);
        ExceptionDispatchInfo? failure = null;
        var prior = BootstrapThemeManager.CurrentTheme;
        BootstrapTextBox? secret = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "Tạo vai trò" || form.Text.StartsWith("Sửa — ") || form.Text.StartsWith("Đặt lại mật khẩu — "));
            if (dialog == null) return;
            timer.Stop();
            try
            {
                var inputs = DialogInputs(dialog).Select(input => Assert.IsType<BootstrapTextBox>(input)).OrderBy(input => input.TabIndex).ToArray();
                var first = inputs[0];
                Assert.True(first.ContainsFocus);
                Assert.Equal(kind == "password" ? "Mật khẩu mới" : roles ? "Tên vai trò" : "Tên đăng nhập", first.AccessibleName);
                if (kind != "create-role") Assert.Contains(roles ? control.ViewModel.SelectedRole!.Name : control.ViewModel.SelectedUser!.DisplayName, dialog.Text);
                var save = Assert.IsType<BootstrapButton>(dialog.AcceptButton);
                var cancel = Assert.IsType<BootstrapButton>(dialog.CancelButton);
                Assert.Equal(BootstrapVariant.Primary, save.Variant);
                Assert.False(save.Outline);
                Assert.Equal(BootstrapVariant.Secondary, cancel.Variant);
                first.Clear();
                save.PerformClick();
                Assert.True(first.ContainsFocus);
                Assert.Equal(BootstrapValidationState.Invalid, first.ValidationState);
                var validation = Assert.Single(Descendants(dialog, typeof(BootstrapLabel)).Cast<BootstrapLabel>(), label => label.AccessibleRole == AccessibleRole.Alert);
                Assert.False(string.IsNullOrEmpty(validation.Text));
                Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                var driver = new NativeLookupTypingDriver(first);
                foreach (var character in "pilot") driver.TypeCharacter(character);
                Assert.Equal(BootstrapValidationState.None, first.ValidationState);
                Assert.Empty(validation.Text);
                driver.SelectRange(1, 2);
                foreach (var mode in new[] { BootstrapThemeMode.Dark, BootstrapThemeMode.Light })
                {
                    BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(mode);
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal("pilot", driver.Text);
                    Assert.Equal(1, driver.SelectionStart);
                    Assert.Equal(2, driver.SelectionLength);
                    driver.AssertFocused();
                    Assert.Equal(10f, first.Font.SizeInPoints);
                    Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Surface, first.BackColor);
                    Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Danger, validation.ForeColor);
                    Assert.False(save.Font.GetHeight() <= 0);
                }
                driver.TypeCharacter('X');
                Assert.Equal("pXot", driver.Text);
                if (kind == "password") { secret = first; Assert.True(first.UseSystemPasswordChar); }
                for (var index = 1; index < inputs.Length; index++)
                {
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
                    Assert.True(inputs[index].ContainsFocus);
                }
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
                Assert.True(save.ContainsFocus);
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
                Assert.True(cancel.ContainsFocus);
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                Assert.Equal(DialogResult.Cancel, dialog.DialogResult);
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            finally { dialog.Close(); }
        };
        try
        {
            timer.Start(); command.PerformClick();
            Assert.False(timer.Enabled);
            failure?.Throw();
            if (secret != null) { Assert.Empty(secret.Text); Assert.True(secret.IsDisposed); }
            Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        }
        finally { BootstrapThemeManager.CurrentTheme = prior; }
    });

    [Fact]
    public void User_create_public_inputs_keep_native_tab_typing_and_theme_state() => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        var create = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Tạo người dùng…");
        ExceptionDispatchInfo? dialogError = null;
        var prior = BootstrapThemeManager.CurrentTheme;
        BootstrapTextBox? secret = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var form = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(item => item.Text == "Tạo người dùng");
            if (form == null) return;
            timer.Stop();
            try
            {
                var modal = form;
                var inputs = Descendants(modal, typeof(BootstrapTextBox)).Cast<BootstrapTextBox>().OrderBy(input => input.TabIndex).ToArray();
                Assert.Equal(new[] { "Tên đăng nhập", "Tên hiển thị", "Email", "Mật khẩu tạm" }, inputs.Select(input => input.AccessibleName));
                Assert.True(inputs[0].ContainsFocus);
                Assert.True(inputs[3].UseSystemPasswordChar);
                secret = inputs[3];
                var save = Assert.IsType<BootstrapButton>(modal.AcceptButton);
                var cancel = Assert.IsType<BootstrapButton>(modal.CancelButton);
                Assert.Same(save.Parent, cancel.Parent);
                Assert.True(save.Visible && cancel.Visible);
                Assert.True(modal.Modal);
                Assert.Equal(BootstrapVariant.Primary, save.Variant);
                Assert.False(save.Outline);
                Assert.Equal(BootstrapVariant.Secondary, cancel.Variant);
                var driver = new NativeLookupTypingDriver(inputs[0]);
                foreach (var letter in "pilot") driver.TypeCharacter(letter);
                Assert.Equal("pilot", driver.Text);
                driver.SelectRange(1, 2);
                foreach (var mode in new[] { BootstrapThemeMode.Dark, BootstrapThemeMode.Light })
                {
                    BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(mode);
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal("pilot", driver.Text);
                    Assert.Equal(1, driver.SelectionStart);
                    Assert.Equal(2, driver.SelectionLength);
                    Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Body, modal.BackColor);
                    Assert.Equal(BootstrapThemeManager.CurrentTheme.Colors.Surface, inputs[0].Parent.Parent.BackColor);
                }
                driver.TypeCharacter('X');
                Assert.Equal("pXot", driver.Text);
                for (var index = 1; index < inputs.Length; index++)
                {
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
                    Assert.True(inputs[index].ContainsFocus, "Native Tab must reach " + inputs[index].AccessibleName);
                }
                secret.Text = "cancel-only-secret";
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
                Assert.True(save.ContainsFocus);
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Tab);
                Assert.True(cancel.ContainsFocus);
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                Assert.Equal(DialogResult.Cancel, modal.DialogResult);
                Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); }
            finally { form.Close(); }
        };
        try
        {
            timer.Start();
            create.PerformClick();
            Assert.False(timer.Enabled);
            dialogError?.Throw();
            Assert.NotNull(secret);
            Assert.Empty(secret!.Text);
            Assert.True(secret.IsDisposed);
        }
        finally { BootstrapThemeManager.CurrentTheme = prior; }
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
                foreach (var caption in new[] { "Tên đăng nhập", "Tên hiển thị", "Email", "Mật khẩu tạm" })
                    Assert.Contains(Descendants(dialog, typeof(Label)).Cast<Label>(), label => label.Visible && label.Text == caption);
                save.PerformClick();
                Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                Assert.True(dialog.Visible);
                var name = Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên đăng nhập");
                var display = Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên hiển thị");
                var password = Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Mật khẩu tạm");
                Assert.Equal(BootstrapValidationState.Invalid, Assert.IsType<BootstrapTextBox>(name).ValidationState);
                Assert.True(name.ContainsFocus);
                var error = Assert.Single(Descendants(dialog, typeof(Label)).Cast<Label>(), label => label.AccessibleName == "Lỗi thông tin");
                Assert.NotEmpty(error.Text);
                var nameDriver = new NativeLookupTypingDriver(name);
                nameDriver.TypeCharacter('n');
                Assert.Equal("n", nameDriver.Text);
                Assert.Equal(1, nameDriver.SelectionStart);
                Assert.Equal(0, nameDriver.SelectionLength);
                Assert.True(name.ContainsFocus);
                Assert.Equal(BootstrapValidationState.None, Assert.IsType<BootstrapTextBox>(name).ValidationState);
                Assert.Empty(error.Text);
                Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                var email = Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Email");
                foreach (var invalid in new[]
                {
                    (Field: name, Text: " "), (Field: name, Text: new string('n', 101)),
                    (Field: display, Text: ""), (Field: display, Text: " "), (Field: display, Text: new string('d', 101)),
                    (Field: email, Text: new string('e', 255)), (Field: password, Text: ""),
                    (Field: password, Text: new string('x', 73)), (Field: password, Text: new string('漢', 25))
                })
                {
                    name.Text = "new-user"; display.Text = "New User"; email.Text = "safe@example.test";
                    password.Text = "fixture-only-password";
                    invalid.Field.Text = invalid.Text;
                    var safeValues = new[] { name.Text, display.Text, email.Text };
                    save.PerformClick();
                    Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                    Assert.Empty(password.Text);
                    Assert.True(invalid.Field.ContainsFocus);
                    Assert.Equal(BootstrapValidationState.Invalid, ((BootstrapTextBox)invalid.Field).ValidationState);
                    Assert.Equal(safeValues, new[] { name.Text, display.Text, email.Text });
                    Assert.All(DialogInputs(dialog).Where(field => field != invalid.Field), field =>
                        Assert.Equal(BootstrapValidationState.None, ((BootstrapTextBox)field).ValidationState));
                }
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); }
            finally { dialog.Close(); }
        };
        timer.Start();
        create.PerformClick();
        Assert.False(timer.Enabled, "The create dialog did not open within the pumped test window.");
        dialogError?.Throw();
    });
    [Theory]
    [InlineData(72, 'x')]
    [InlineData(24, '漢')]
    [InlineData(36, 'é')]
    public void User_create_dialog_accepts_valid_fields_by_native_enter_once(int passwordLength, char passwordCharacter) => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        var create = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Tạo người dùng…");
        ExceptionDispatchInfo? dialogError = null;
        Control? secret = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "Tạo người dùng");
            if (dialog == null) return;
            timer.Stop();
            try
            {
                Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên đăng nhập").Text = new string('u', 100);
                Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên hiển thị").Text = new string('d', 100);
                Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Email").Text = new string('e', 254);
                secret = Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Mật khẩu tạm");
                secret.Text = new string(passwordCharacter, passwordLength);
                secret.Focus();
                Assert.True(secret.ContainsFocus);
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
        Assert.Contains(data.Data.Users, user => user.Username == new string('u', 100) && user.DisplayName == new string('d', 100) && user.Email == new string('e', 254));
        Assert.Equal(4, control.Users.Items.Count);
    });
    [Theory]
    [InlineData(MyDmsVn.Contracts.ApiStatusCode.BadRequest)]
    [InlineData(MyDmsVn.Contracts.ApiStatusCode.Conflict)]
    public void User_create_server_rejection_keeps_safe_fields_and_clears_secret(MyDmsVn.Contracts.ApiStatusCode status) => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        data.Administration.MutationErrors.Enqueue(new MyDmsVn.Contracts.ApiError(status, status == MyDmsVn.Contracts.ApiStatusCode.Conflict ? "Security.RevisionConflict" : "ValidationError", "raw server secret",
            new[] { new MyDmsVn.Contracts.ApiErrorDetail("PredicateValidator", "raw detail secret", "name") }));
        var create = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Tạo người dùng…");
        ExceptionDispatchInfo? dialogError = null;
        var phase = 0;
        var ticks = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "Tạo người dùng");
            if (dialog == null) { if (phase == 2) timer.Stop(); return; }
            try
            {
                if (phase == 0)
                {
                    phase = 1;
                    Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên đăng nhập").Text = "rejected-user";
                    Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên hiển thị").Text = "Rejected User";
                    Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Mật khẩu tạm").Text = "temporary-password";
                    Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Lưu thông tin").PerformClick();
                }
                else if (phase == 2)
                {
                    if (++ticks > 50) throw new Xunit.Sdk.XunitException("The conflict reload did not close the dialog.");
                }
                else if (data.Journal.Entries.Count(entry => entry.Operation == "Security.Mutate") == 1)
                {
                    Assert.True(dialog.Visible);
                    Assert.Equal("rejected-user", Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên đăng nhập").Text);
                    Assert.Empty(Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Mật khẩu tạm").Text);
                    var error = Assert.Single(Descendants(dialog, typeof(Label)).Cast<Label>(), label => label.AccessibleName == "Lỗi thông tin");
                    Assert.True(error.Visible);
                    Assert.NotEmpty(error.Text);
                    Assert.DoesNotContain("raw", error.Text);
                    Assert.True(status == MyDmsVn.Contracts.ApiStatusCode.Conflict
                        ? error.Text.Contains("Xung đột")
                        : error.Text.Contains("tên"));
                    if (status == MyDmsVn.Contracts.ApiStatusCode.Conflict)
                    {
                        var reload = Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Tải lại để đối chiếu");
                        Assert.True(reload.Visible);
                        Assert.False(Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Lưu thông tin").Enabled);
                        reload.PerformClick();
                        phase = 2;
                        return;
                    }
                    else
                    {
                        var name = Assert.IsType<BootstrapTextBox>(Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên đăng nhập"));
                        Assert.True(name.ContainsFocus);
                        Assert.Equal(BootstrapValidationState.Invalid, name.ValidationState);
                        Assert.True(Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Lưu thông tin").Enabled);
                        timer.Stop();
                        var driver = new NativeLookupTypingDriver(name);
                        driver.SelectRange(0, driver.Text.Length);
                        driver.TypeCharacter('r');
                        Assert.Equal("r", driver.Text);
                        Assert.Equal(1, driver.SelectionStart);
                        Assert.Equal(0, driver.SelectionLength);
                        Assert.True(name.ContainsFocus);
                        Assert.Equal(BootstrapValidationState.None, name.ValidationState);
                        Assert.Empty(error.Text);
                        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                    }
                    timer.Stop();
                    dialog.Close();
                }
                else if (++ticks > 50) throw new Xunit.Sdk.XunitException("The rejected mutation did not reach the fake journal.");
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); timer.Stop(); dialog.Close(); }
        };
        timer.Start();
        create.PerformClick();
        if (phase == 2 && !System.Windows.Forms.Application.OpenForms.Cast<Form>().Any(form => form.Text == "Tạo người dùng")) timer.Stop();
        Assert.False(timer.Enabled, "The rejected create dialog did not finish within the pumped test window.");
        dialogError?.Throw();
        Assert.DoesNotContain(data.Data.Users, user => user.Username == "rejected-user");
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        if (status == MyDmsVn.Contracts.ApiStatusCode.Conflict)
        {
            Assert.True(control.ViewModel.RequiresRecovery);
            Assert.True(control.ViewModel.CanReviewRecovery);
            Assert.False(control.ViewModel.CanNavigate);
            var review = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(),
                button => button.Text == "Đã đối chiếu — lập diff mới");
            Assert.True(review.Visible);
            control.RecoveryConfirmationOverride = () => true;
            review.PerformClick();
            Assert.False(control.ViewModel.RequiresRecovery);
        }
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
                var fields = DialogInputs(dialog).ToArray();
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
    public void Held_password_mutation_closes_and_scrubs_its_modal_on_session_replacement_before_late_completion() =>
        Run(SecurityScenarioId.SEC_L03, (control, data) =>
        {
            var gate = data.Administration.HoldNextMutation(true);
            var reset = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(),
                button => button.Text == "Đặt lại mật khẩu…");
            ExceptionDispatchInfo? dialogError = null;
            BootstrapTextBox? secret = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, _) =>
            {
                var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(form => form.Text.StartsWith("Đặt lại mật khẩu — "));
                if (dialog == null) return;
                timer.Stop();
                try
                {
                    secret = Assert.IsType<BootstrapTextBox>(Assert.Single(DialogInputs(dialog),
                        field => field.AccessibleName == "Mật khẩu mới"));
                    secret.Text = "replacement-session-secret";
                    Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(),
                        button => button.Text == "Xác nhận").PerformClick();
                    Assert.True(control.ViewModel.IsBusy);
                    Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");

                    data.Session.Replace();
                    Assert.Empty(secret.Text);
                    Assert.False(control.ViewModel.IsCurrent);
                    gate.Complete(MyDmsVn.Contracts.ApiResponse<SecurityAdministrationResultDto>.Failure(
                        new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.BadRequest,
                            "ValidationError", "Late fixture outcome.")));
                }
                catch (Exception error)
                {
                    dialogError = ExceptionDispatchInfo.Capture(error);
                    gate.Cancel();
                    if (!dialog.IsDisposed) dialog.Close();
                }
            };
            timer.Start();
            reset.PerformClick();
            Assert.False(timer.Enabled, "The held password dialog did not reach session replacement.");
            dialogError?.Throw();
            Until(() => data.Administration.OutstandingOperations.All(operation => operation.IsCompleted));
            System.Windows.Forms.Application.DoEvents();

            Assert.NotNull(secret);
            Assert.Empty(secret!.Text);
            Assert.DoesNotContain(System.Windows.Forms.Application.OpenForms.Cast<Form>(),
                form => form.Text.StartsWith("Đặt lại mật khẩu — "));
            Assert.Empty(control.Users.Items);
            Assert.Empty(control.Roles.Items);
            Assert.Null(control.ViewModel.LastMutationFeedback);
            Assert.DoesNotContain("replacement-session-secret",
                string.Join(" ", data.Journal.Entries.Select(entry => entry.ToString())));
            Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });
    [Theory]
    [InlineData("create-user", "success")]
    [InlineData("edit-user", "success")]
    [InlineData("create-role", "success")]
    [InlineData("edit-role", "success")]
    [InlineData("password", "success")]
    [InlineData("create-user", "conflict")]
    [InlineData("edit-user", "conflict")]
    [InlineData("create-role", "conflict")]
    [InlineData("edit-role", "conflict")]
    [InlineData("password", "conflict")]
    [InlineData("create-user", "unknown")]
    [InlineData("edit-user", "unknown")]
    [InlineData("create-role", "unknown")]
    [InlineData("edit-role", "unknown")]
    [InlineData("password", "unknown")]
    public void Identity_and_password_dialogs_cannot_cancel_a_dispatched_mutation(string kind, string outcome) => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        var gate = data.Administration.HoldNextMutation(true);
        if (kind == "edit-role" || kind == "create-role") control.Tabs.SelectedIndex = 1;
        var host = kind == "edit-role" || kind == "create-role" ? (Control)control.RoleDetail : control.UserDetail;
        var caption = kind switch
        {
            "create-user" => "Tạo người dùng…",
            "edit-user" => "Sửa thông tin…",
            "edit-role" => "Sửa tên…",
            "create-role" => "Tạo vai trò…",
            _ => "Đặt lại mật khẩu…"
        };
        var command = Assert.Single(Descendants(host, typeof(Button)).Cast<Button>(), button => button.Text == caption);
        ExceptionDispatchInfo? dialogError = null;
        var phase = 0;
        var ticks = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form =>
                form.Text == "Tạo người dùng" || form.Text == "Tạo vai trò" || form.Text.StartsWith("Sửa — ") || form.Text.StartsWith("Đặt lại mật khẩu — "));
            try
            {
                if (dialog == null)
                {
                    if (phase == 2 && outcome == "success") { phase = 3; timer.Stop(); }
                    else if (++ticks > 50) throw new Xunit.Sdk.XunitException("Modal closed before outcome or did not open.");
                    return;
                }
                var save = Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == (kind == "password" ? "Xác nhận" : "Lưu thông tin"));
                var cancel = Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Hủy");
                var secret = DialogInputs(dialog).FirstOrDefault(field => field is BootstrapTextBox themed ? themed.UseSystemPasswordChar : ((TextBox)field).UseSystemPasswordChar);
                if (phase == 0)
                {
                    if (kind == "create-user")
                    {
                        Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên đăng nhập").Text = "held-review-user";
                        Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên hiển thị").Text = "Held Review";
                    }
                    else if (kind == "create-role" || kind == "edit-role")
                        Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên vai trò").Text = "Held Review Role";
                    else if (kind == "edit-user")
                        Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Tên hiển thị").Text = "Held Review User";
                    if (secret != null) secret.Text = "fixture-only-password";
                    phase = 1;
                    timer.Stop(); // Native input pumps messages; do not re-enter the initial dispatch.
                    DialogInputs(dialog).First().Focus();
                    Assert.True(DialogInputs(dialog).First().ContainsFocus);
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
                    timer.Start();
                    var request = Assert.IsType<SecurityMutationJournalEntry>(Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate").Request);
                    var expected = kind switch
                    {
                        "create-user" => (SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.Create, 0, (long?)null),
                        "edit-user" => (SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.Update, 101, (long?)11),
                        "create-role" => (SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.Create, 0, (long?)null),
                        "edit-role" => (SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.Update, 201, (long?)21),
                        _ => (SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetPassword, 101, (long?)11)
                    };
                    Assert.Equal(expected, (request.Target, request.Operation, request.TargetId, request.ExpectedRevision));
                }
                else if (phase == 1)
                {
                    timer.Stop(); // Native drivers pump messages: do not re-enter this outcome phase.
                    Assert.True(dialog.Visible);
                    Assert.False(save.Enabled);
                    Assert.False(cancel.Enabled);
                    Assert.False(dialog.ControlBox);
                    Assert.Contains(Descendants(dialog, typeof(Label)).Cast<Label>(), label => label.Text == "Đang lưu…");
                    cancel.PerformClick();
                    Assert.True(dialog.Visible);
                    var focusTarget = (Control?)secret ?? DialogInputs(dialog).First();
                    focusTarget.Focus();
                    Assert.True(focusTarget.ContainsFocus);
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Enter);
                    Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                    Assert.True(dialog.Visible);
                    Assert.Equal(DialogResult.None, dialog.DialogResult);
                    dialog.Close(); // Public native close path must retain the pending modal.
                    Assert.True(dialog.Visible);
                    Assert.Equal(DialogResult.None, dialog.DialogResult);
                    SendMessage(dialog.Handle, 0x0010, IntPtr.Zero, IntPtr.Zero); // Native window close (X/Alt+F4).
                    Assert.True(dialog.Visible);
                    Assert.Equal(DialogResult.None, dialog.DialogResult);
                    SendMessage(dialog.Handle, 0x0112, new IntPtr(0xF060), IntPtr.Zero); // SC_CLOSE, also used by native caption / Alt+F4.
                    Assert.True(dialog.Visible);
                    Assert.Equal(DialogResult.None, dialog.DialogResult);
                    save.PerformClick();
                    Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                    phase = 2;
                    if (outcome == "success") gate.Complete(MyDmsVn.Contracts.ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(22, true)));
                    else if (outcome == "conflict") gate.Complete(MyDmsVn.Contracts.ApiResponse<SecurityAdministrationResultDto>.Failure(new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.Conflict, "Security.RevisionConflict", "Conflict.")));
                    else Assert.True(gate.Cancel());
                    timer.Start();
                }
                else if (phase == 2 && outcome != "success" && !control.ViewModel.IsBusy)
                {
                    Assert.True(dialog.Visible);
                    Assert.True(cancel.Enabled);
                    Assert.True(dialog.ControlBox);
                    Assert.True(control.ViewModel.RequiresRecovery);
                    if (secret != null) Assert.Empty(secret.Text);
                    Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                    phase = 3;
                    timer.Stop();
                    cancel.PerformClick();
                }
                else if (++ticks > 50) throw new Xunit.Sdk.XunitException("Held mutation did not settle.");
            }
            catch (Exception error)
            {
                dialogError = ExceptionDispatchInfo.Capture(error);
                gate.Cancel();
                Until(() => !control.ViewModel.IsBusy);
                dialog?.Close();
                timer.Stop();
            }
        };
        timer.Start();
        command.PerformClick();
        if (phase == 2 && outcome == "success" && !System.Windows.Forms.Application.OpenForms.Cast<Form>().Any(form =>
            form.Text == "Tạo người dùng" || form.Text == "Tạo vai trò" || form.Text.StartsWith("Sửa — ") || form.Text.StartsWith("Đặt lại mật khẩu — ")))
        {
            phase = 3;
            timer.Stop();
        }
        Assert.False(timer.Enabled, "The modal scenario did not finish within the pumped test window.");
        dialogError?.Throw();
        Assert.Equal(3, phase);
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
                Assert.Contains(Descendants(dialog, typeof(Label)).Cast<Label>(), label => label.Visible && label.Text == "Mật khẩu mới");
                var password = Assert.IsType<BootstrapTextBox>(Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Mật khẩu mới"));
                save.PerformClick();
                Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                Assert.True(dialog.Visible);
                password.Text = new string('x', 73);
                save.PerformClick();
                Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                Assert.Empty(password.Text);
                Assert.True(password.ContainsFocus);
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
        BootstrapTextBox? secret = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text.StartsWith("Đặt lại mật khẩu — "));
            if (dialog == null) return;
            timer.Stop();
            try
            {
                secret = Assert.IsType<BootstrapTextBox>(Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Mật khẩu mới"));
                secret.Text = "new-temporary-password";
                secret.Focus();
                Assert.True(secret.ContainsFocus);
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

    [Theory]
    [InlineData(MyDmsVn.Contracts.ApiStatusCode.BadRequest)]
    [InlineData(MyDmsVn.Contracts.ApiStatusCode.Conflict)]
    public void Password_reset_server_rejection_is_visible_and_never_replays_secret(MyDmsVn.Contracts.ApiStatusCode status) => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        data.Administration.MutationErrors.Enqueue(new MyDmsVn.Contracts.ApiError(status,
            status == MyDmsVn.Contracts.ApiStatusCode.Conflict ? "Security.RevisionConflict" : "ValidationError",
            "raw server secret", new[] { new MyDmsVn.Contracts.ApiErrorDetail("PredicateValidator", "raw detail secret", "password") }));
        var reset = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Đặt lại mật khẩu…");
        ExceptionDispatchInfo? dialogError = null;
        var phase = 0;
        var ticks = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text.StartsWith("Đặt lại mật khẩu — "));
            if (dialog == null) { if (phase == 2) timer.Stop(); return; }
            try
            {
                var password = Assert.IsType<BootstrapTextBox>(Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Mật khẩu mới"));
                if (phase == 0)
                {
                    phase = 1;
                    password.Text = "temporary-password";
                    Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Xác nhận").PerformClick();
                }
                else if (phase == 2)
                {
                    if (++ticks > 50) throw new Xunit.Sdk.XunitException("The conflict reload did not close the password dialog.");
                }
                else if (data.Journal.Entries.Count(entry => entry.Operation == "Security.Mutate") == 1)
                {
                    var error = Assert.Single(Descendants(dialog, typeof(Label)).Cast<Label>(), label => label.AccessibleName == "Lỗi mật khẩu");
                    Assert.True(error.Visible);
                    Assert.Contains(status == MyDmsVn.Contracts.ApiStatusCode.Conflict ? "Xung đột" : "mật khẩu", error.Text);
                    Assert.DoesNotContain("raw", error.Text);
                    Assert.Empty(password.Text);
                    Assert.True(dialog.Visible);
                    Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
                    if (status == MyDmsVn.Contracts.ApiStatusCode.Conflict)
                    {
                        var reload = Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Tải lại để đối chiếu");
                        Assert.True(reload.Visible);
                        Assert.False(Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Xác nhận").Enabled);
                        reload.PerformClick();
                        phase = 2;
                        return;
                    }
                    else Assert.True(password.ContainsFocus);
                    timer.Stop();
                    dialog.Close();
                }
                else if (++ticks > 50) throw new Xunit.Sdk.XunitException("The rejected password mutation did not reach the fake journal.");
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); timer.Stop(); dialog.Close(); }
        };
        timer.Start();
        reset.PerformClick();
        if (phase == 2 && !System.Windows.Forms.Application.OpenForms.Cast<Form>().Any(form => form.Text.StartsWith("Đặt lại mật khẩu — "))) timer.Stop();
        Assert.False(timer.Enabled, "The rejected password dialog did not finish within the pumped test window.");
        dialogError?.Throw();
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        if (status == MyDmsVn.Contracts.ApiStatusCode.Conflict)
        {
            Assert.True(control.ViewModel.RequiresRecovery);
            Assert.True(control.ViewModel.CanReviewRecovery);
            Assert.False(control.ViewModel.CanNavigate);
            var review = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(),
                button => button.Text == "Đã đối chiếu — lập diff mới");
            Assert.True(review.Visible);
            control.RecoveryConfirmationOverride = () => true;
            review.PerformClick();
            Assert.False(control.ViewModel.RequiresRecovery);
        }
    });
    [Fact]
    public void Unknown_password_result_requires_explicit_review_after_failed_then_successful_reload() => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        var gate = data.Administration.HoldNextMutation();
        var reset = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Đặt lại mật khẩu…");
        ExceptionDispatchInfo? dialogError = null;
        var phase = 0;
        var ticks = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text.StartsWith("Đặt lại mật khẩu — "));
            if (dialog == null) { if (phase == 4) timer.Stop(); return; }
            try
            {
                var password = Assert.IsType<BootstrapTextBox>(Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Mật khẩu mới"));
                var reload = Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Tải lại để đối chiếu");
                if (phase == 0)
                {
                    password.Text = "fixture-only-password";
                    Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Xác nhận").PerformClick();
                    phase = 1;
                }
                else if (phase == 1 && data.Journal.Entries.Any(entry => entry.Operation == "Security.Mutate"))
                {
                    Assert.True(gate.Cancel());
                    phase = 2;
                }
                else if (phase == 2 && control.ViewModel.RequiresRecovery && reload.Visible)
                {
                    Assert.Empty(password.Text);
                    data.Catalog.Error = new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.Forbidden, "Forbidden", "raw secret");
                    reload.PerformClick();
                    phase = 3;
                }
                else if (phase == 3 && control.ViewModel.Catalog.State == SecurityDatasetState.Forbidden && !control.ViewModel.IsBusy)
                {
                    Assert.True(dialog.Visible);
                    Assert.True(control.ViewModel.RequiresRecovery);
                    Assert.False(control.ViewModel.CanReviewRecovery);
                    data.Catalog.Error = null;
                    reload.PerformClick();
                    phase = 4;
                }
                else if (++ticks > 100) throw new Xunit.Sdk.XunitException("Unknown password recovery did not finish its reloads.");
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); timer.Stop(); dialog.Close(); gate.Cancel(); }
        };
        timer.Start();
        reset.PerformClick();
        if (phase == 4 && !System.Windows.Forms.Application.OpenForms.Cast<Form>().Any(form => form.Text.StartsWith("Đặt lại mật khẩu — ")))
            timer.Stop();
        dialogError?.Throw();
        Assert.False(timer.Enabled, "Unknown password dialog did not close after the successful reload. Phase: " + phase);
        Assert.True(control.ViewModel.RequiresRecovery);
        Assert.True(control.ViewModel.CanReviewRecovery);
        Assert.False(control.ViewModel.CanNavigate);
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate.Result" &&
            entry.Request is SecurityResponseJournalEntry result && result.Outcome == "OutcomeUnknown");
        var review = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Đã đối chiếu — lập diff mới");
        Assert.True(review.Visible);
        control.RecoveryConfirmationOverride = () => true;
        review.PerformClick();
        Assert.False(control.ViewModel.RequiresRecovery);
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });
    [Fact]
    public void Canceling_conflicted_password_dialog_keeps_recovery_guard() => Run(SecurityScenarioId.SEC_L03, (control, data) =>
    {
        data.Administration.MutationErrors.Enqueue(new MyDmsVn.Contracts.ApiError(
            MyDmsVn.Contracts.ApiStatusCode.Conflict, "Security.RevisionConflict", "raw server secret"));
        var reset = Assert.Single(Descendants(control.UserDetail, typeof(Button)).Cast<Button>(), button => button.Text == "Đặt lại mật khẩu…");
        ExceptionDispatchInfo? dialogError = null;
        var phase = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text.StartsWith("Đặt lại mật khẩu — "));
            if (dialog == null) return;
            try
            {
                if (phase == 0)
                {
                    Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Mật khẩu mới").Text = "fixture-only-password";
                    Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Xác nhận").PerformClick();
                    phase = 1;
                }
                else if (control.ViewModel.RequiresRecovery)
                {
                    Assert.Empty(Assert.Single(DialogInputs(dialog), field => field.AccessibleName == "Mật khẩu mới").Text);
                    Assert.Single(Descendants(dialog, typeof(Button)).Cast<Button>(), button => button.Text == "Hủy").PerformClick();
                    timer.Stop();
                }
            }
            catch (Exception error) { dialogError = ExceptionDispatchInfo.Capture(error); timer.Stop(); dialog.Close(); }
        };
        timer.Start();
        reset.PerformClick();
        dialogError?.Throw();
        Assert.False(timer.Enabled);
        Assert.True(control.ViewModel.RequiresRecovery);
        Assert.False(control.ViewModel.CanNavigate);
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
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
    public void Discarding_dirty_direct_decision_completes_requested_subtab_navigation() => Run(SecurityScenarioId.SEC_L10, (control, data) =>
    {
        control.UserDetail.Tabs.SelectedIndex = 3;
        Assert.True(control.ViewModel.BeginDirectPermission("Sales.Receipts.Post"));
        control.ViewModel.SetDirectDecision(true);
        Assert.True(control.ViewModel.HasDirtyEditor);
        Assert.True(control.UserDetail.Tabs.IsHandleCreated);
        control.NavigationResolutionOverride = () => SecurityDraftResolution.Discard;

        control.UserDetail.Tabs.SelectedIndex = 2;
        Until(() => control.UserDetail.Tabs.SelectedIndex == 2 || !control.UserDetail.Tabs.IsHandleCreated);

        Assert.Equal(2, control.UserDetail.Tabs.SelectedIndex);
        Assert.Null(control.ViewModel.DirectPermissionKey);
        Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });
    [Fact]
    public void Newer_subtab_choice_supersedes_discarded_navigation() => Run(SecurityScenarioId.SEC_L10, (control, data) =>
    {
        control.UserDetail.Tabs.SelectedIndex = 3;
        Assert.True(control.ViewModel.BeginDirectPermission("Sales.Receipts.Post"));
        control.ViewModel.SetDirectDecision(true);
        control.NavigationResolutionOverride = () => SecurityDraftResolution.Discard;

        control.UserDetail.Tabs.SelectedIndex = 2;
        control.UserDetail.Tabs.SelectedIndex = 1;
        System.Windows.Forms.Application.DoEvents();

        Assert.Equal(1, control.UserDetail.Tabs.SelectedIndex);
        Assert.False(control.ViewModel.HasDirtyEditor);
        Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Discarding_dirty_role_grant_completes_requested_tab_navigation(bool topLevel) => Run(SecurityScenarioId.SEC_L10, (control, data) =>
    {
        control.Tabs.SelectedIndex = 1;
        Assert.True(control.ViewModel.BeginRolePermissions());
        control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
        Assert.True(control.ViewModel.HasDirtyEditor);
        control.NavigationResolutionOverride = () => SecurityDraftResolution.Discard;

        if (topLevel) control.Tabs.SelectedIndex = 2;
        else control.RoleDetail.Tabs.SelectedIndex = 1;
        Until(() => topLevel ? control.Tabs.SelectedIndex == 2 : control.RoleDetail.Tabs.SelectedIndex == 1);

        Assert.Null(control.ViewModel.RoleDraft);
        Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");
    });
    [Theory]
    [InlineData(SecurityDraftResolution.Save)]
    [InlineData(SecurityDraftResolution.Discard)]
    [InlineData(SecurityDraftResolution.Stay)]
    public void Member_navigation_resolves_role_draft_before_selecting_requested_user(SecurityDraftResolution resolution) => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        control.Tabs.SelectedIndex = 1;
        control.RoleDetail.Tabs.SelectedIndex = 1;
        Assert.True(control.ViewModel.BeginRolePermissions());
        control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
        control.RoleDetail.Members.SelectedIndex = 1;
        control.UserSearch.Text = "protected";
        control.NavigationResolutionOverride = () => resolution;

        control.RoleDetail.OpenUser.PerformClick();
        Wait(control.LastOperation);

        if (resolution == SecurityDraftResolution.Stay)
        {
            Assert.Equal(1, control.Tabs.SelectedIndex);
            Assert.Equal(101, control.ViewModel.SelectedUserId);
            Assert.True(control.ViewModel.RoleDraft?.IsDirty);
            Assert.Equal("protected", control.UserSearch.Text);
        }
        else
        {
            Assert.Equal(0, control.Tabs.SelectedIndex);
            Assert.Equal(1, control.UserDetail.Tabs.SelectedIndex);
            Assert.Equal(102, control.ViewModel.SelectedUserId);
            Assert.Equal("", control.UserSearch.Text);
            Assert.Same(control.ViewModel.SelectedUser, Assert.Single(control.Users.SelectedItems.Cast<ListViewItem>()).Tag);
            Assert.Null(control.ViewModel.RoleDraft);
        }
        Assert.Equal(resolution == SecurityDraftResolution.Save ? 1 : 0,
            data.Journal.Entries.Count(entry => entry.Operation == "Security.Mutate"));
    });
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Member_navigation_waits_for_role_save_and_keeps_origin_on_failure(bool succeeds) => Run(SecurityScenarioId.SEC_L01, (control, data) =>
    {
        control.Tabs.SelectedIndex = 1;
        control.RoleDetail.Tabs.SelectedIndex = 1;
        Assert.True(control.ViewModel.BeginRolePermissions());
        control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
        control.RoleDetail.Members.SelectedIndex = 1;
        control.NavigationResolutionOverride = () => SecurityDraftResolution.Save;
        var gate = data.Administration.HoldNextMutation(true);

        control.RoleDetail.OpenUser.PerformClick();
        Assert.True(control.ViewModel.IsBusy);
        Assert.Equal(1, control.Tabs.SelectedIndex);
        Assert.Equal(101, control.ViewModel.SelectedUserId);
        Assert.True(control.ViewModel.RoleDraft?.IsDirty);
        Assert.Single(data.Journal.Entries, entry => entry.Operation == "Security.Mutate");

        gate.Complete(succeeds
            ? MyDmsVn.Contracts.ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(22, true))
            : MyDmsVn.Contracts.ApiResponse<SecurityAdministrationResultDto>.Failure(new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.Conflict, "Conflict", "Review response.")));
        Wait(control.LastOperation);
        Assert.Equal(succeeds ? 0 : 1, control.Tabs.SelectedIndex);
        Assert.Equal(succeeds ? 102 : 101, control.ViewModel.SelectedUserId);
        Assert.Equal(succeeds ? null : true, control.ViewModel.RoleDraft?.IsDirty);
    });
    [Fact]
    public void Missing_member_profile_does_not_offer_user_navigation() => Run(SecurityScenarioId.SEC_L01, (control, scene) =>
    {
        scene.Data.Users.Clear();
        Wait(control.ActivateAsync(default));
        control.Tabs.SelectedIndex = 1;
        control.RoleDetail.Tabs.SelectedIndex = 1;
        control.RoleDetail.Members.SelectedIndex = 0;

        Assert.Contains("Chi tiết không khả dụng", control.RoleDetail.Members.SelectedItem!.ToString());
        Assert.False(control.RoleDetail.OpenUser.Enabled);
        control.RoleDetail.OpenUser.PerformClick();
        Assert.Equal(1, control.Tabs.SelectedIndex);
        Assert.Null(control.ViewModel.SelectedUserId);
    });
    [Fact]
    public void Unknown_data_is_not_presented_as_an_empty_success() => Run(SecurityScenarioId.SEC_L09, (control, data) =>
    {
        Assert.Equal(SecurityDatasetState.Forbidden, control.ViewModel.Catalog.State);
        Assert.Contains("không khả dụng", control.PermissionCatalog.Details.Text);
        Assert.Equal(3, control.Users.Items.Count);
        Assert.Equal("Vai trò (2)", control.UserDetail.Tabs.TabPages[1].Text);
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

    internal static int GrantIndex(DataGridView grid, string key) => grid.Rows.Cast<DataGridViewRow>().ToList().FindIndex(row => (string?)row.Tag == key);
    internal static bool Granted(DataGridView grid, int index) => (bool)grid.Rows[index].Cells[0].Value;
    internal static void ClickGrant(DataGridView grid, int index)
    {
        grid.CurrentCell = grid.Rows[index].Cells[0];
        typeof(DataGridView).GetMethod("OnCellContentClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(grid, new object[] { new DataGridViewCellEventArgs(0, index) });
    }

    static void AssertHeaderUsesActionRows(FlowLayoutPanel actions)
    {
        var identity = actions.Parent!;
        var buttons = actions.Controls.Cast<Control>().Where(item => item.Visible).ToArray();
        Assert.NotEmpty(buttons);
        Assert.All(buttons, button => Assert.True(button.Bottom <= actions.ClientSize.Height));
        var unused = identity.ClientSize.Height - actions.Top - buttons.Max(button => button.Bottom);
        Assert.InRange(unused, 0, 16);
    }

    internal static void Run(SecurityScenarioId id, Action<SecurityWorkspaceControl, SecurityScenario> action) =>
        Run(id, _ => { }, action);

    internal static void Run(SecurityScenarioId id, Action<SecurityScenario> configure,
        Action<SecurityWorkspaceControl, SecurityScenario> action)
    {
        StaTest.Run(token =>
        {
            // Match the product/Lab common-controls activation context: native Tile requires v6.
            System.Windows.Forms.Application.EnableVisualStyles();
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
                    configure(data);
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

    internal static Control[] DialogInputs(Form dialog)
    {
        var themed = Descendants(dialog, typeof(BootstrapTextBox)).Cast<Control>().ToArray();
        return themed.Length > 0 ? themed : Descendants(dialog, typeof(TextBox)).Cast<Control>().ToArray();
    }

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

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

    static void HoverFirstRow(ListView list)
    {
        for (Control? parent = list.Parent; parent != null; parent = parent.Parent)
            if (parent is TabPage page) ((TabControl)page.Parent!).SelectedTab = page;
        System.Windows.Forms.Application.DoEvents();
        var method = list.GetType().GetMethod("OnMouseMove", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var bounds = list.Items[0].GetBounds(ItemBoundsPortion.Label);
        Assert.False(bounds.IsEmpty);
        method.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.None, 0, bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2, 0) });
    }

    static string TooltipText(ListView list)
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
