using System;
using System.Threading;
using System.Linq;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Bootstrap5WinFormUI.Controls;

namespace MyDmsVn.Desktop.WinForms.Features.Security;
public sealed partial class SecurityWorkspaceControl : UserControl, IActivatableDesktopControl
{
    public SecurityWorkspaceViewModel ViewModel { get; }
    public TabControl Tabs { get; } = new BusinessCompactWorkspaceTabs
    {
        Dock = DockStyle.Fill
    };
    public BootstrapListView Users { get; } = new SecurityMasterListView { AccessibleName = "Người dùng" };
    public BootstrapListView Roles { get; } = new SecurityMasterListView { AccessibleName = "Vai trò" };
    public SecurityUserDetailView UserDetail { get; } = new();
    public SecurityRoleDetailView RoleDetail { get; } = new();
    public SecurityPermissionCatalogView PermissionCatalog { get; } = new();
    public BootstrapLabel DatasetStatus { get; } = new()
    {
        Dock = DockStyle.Top,
        Height = 30,
        TextRole = BootstrapTextRole.Warning,
        AccessibleRole = AccessibleRole.StatusBar
    };
    public BootstrapLabel UserPaneStatus { get; } = new() { Dock = DockStyle.Top, Height = 36, AutoEllipsis = true, AccessibleRole = AccessibleRole.StatusBar };
    public BootstrapLabel RolePaneStatus { get; } = new() { Dock = DockStyle.Top, Height = 36, AutoEllipsis = true, AccessibleRole = AccessibleRole.StatusBar };
    public Task LastOperation { get; private set; } = Task.CompletedTask;
    internal Task TrackOperation(Task operation) => LastOperation = operation;

    public BootstrapTextBox UserSearch { get; } = new() { Dock = DockStyle.Top, AccessibleName = "Tìm người dùng", PlaceholderText = "Tìm người dùng" };
    public BootstrapTextBox RoleSearch { get; } = new() { Dock = DockStyle.Top, AccessibleName = "Tìm vai trò", PlaceholderText = "Tìm vai trò" };
    public BootstrapComboBox UserActivity { get; } = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Trạng thái người dùng" };
    public BootstrapComboBox RoleActivity { get; } = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Trạng thái vai trò" };
    Font? _identityFont;
    Font? _catalogFont;
    bool _rendering;
    public SecurityWorkspaceControl(SecurityWorkspaceViewModel viewModel)
    {
        ViewModel = viewModel;
        Dock = DockStyle.Fill;
        Tabs.TabPages.Add("Người dùng");
        Tabs.TabPages.Add("Vai trò");
        Tabs.TabPages.Add("Danh mục quyền");
        Compose(Tabs.TabPages[0], Master(Users, UserSearch, UserActivity, UserPaneStatus), UserDetail);
        Compose(Tabs.TabPages[1], Master(Roles, RoleSearch, RoleActivity, RolePaneStatus), RoleDetail);
        Tabs.TabPages[2].Controls.Add(PermissionCatalog);
        Controls.Add(Tabs);
        Controls.Add(DatasetStatus);
        Users.SelectedIndexChanged += async (_, __) =>
        {
            if (!_rendering && Users.SelectedItems.Count == 1 && Users.SelectedItems[0].Tag is SecurityUserDto u)
            {
                if (ViewModel.SelectedUserId != u.UserId && !await AdmitNavigationAsync()) { Render(); return; }
                ViewModel.SelectUser(u.UserId);
            }
        };
        Roles.SelectedIndexChanged += async (_, __) =>
        {
            if (!_rendering && Roles.SelectedItems.Count == 1 && Roles.SelectedItems[0].Tag is SecurityRoleDto r)
            {
                if (ViewModel.SelectedRoleId != r.RoleId && !await AdmitNavigationAsync()) { Render(); return; }
                ViewModel.SelectRole(r.RoleId);
            }
        };
        RoleDetail.OpenUserRequested += id => TrackOperation(OpenRoleMemberAsync(id));
        UserSearch.TextChanged += (_, _) => FilterMasters();
        RoleSearch.TextChanged += (_, _) => FilterMasters();
        UserActivity.SelectedIndexChanged += (_, _) => FilterMasters();
        RoleActivity.SelectedIndexChanged += (_, _) => FilterMasters();
        UserDetail.RefreshButton.Click += async (_, _) => { if (await AdmitNavigationAsync()) await ActivateAsync(CancellationToken.None); };
        PermissionCatalog.RefreshButton.Click += async (_, _) => { if (await AdmitNavigationAsync()) await ActivateAsync(CancellationToken.None); };
        BusinessCompactAppearance.Apply(this);
        BootstrapThemeManager.ThemeChanged += ThemeChanged;
        ApplyTheme(this);
        ViewModel.Changed += Changed;
        _ = new SecurityWorkspaceEditorBindings(this);
        InitializeImportBindings();
        BusinessCompactAppearance.Apply(this);
        ApplyTheme(this);
        StyleHeadings();
        Render();
    }

    static Control Master(Control list, BootstrapTextBox search, BootstrapComboBox activity, BootstrapLabel status)
    {
        var panel = new BootstrapPanel { Dock = DockStyle.Fill, Padding = new Padding(8), SurfaceRole = BootstrapSurfaceRole.Surface };
        search.TabIndex = 0;
        activity.TabIndex = 1;
        list.TabIndex = 2;
        activity.Items.AddRange(new object[] { "Tất cả trạng thái", "Đang hoạt động", "Ngừng hoạt động" });
        activity.SelectedIndex = 0;
        panel.Controls.Add(list);
        panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
        panel.Controls.Add(activity);
        panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
        panel.Controls.Add(search);
        panel.Controls.Add(status);
        return panel;
    }

    async Task OpenRoleMemberAsync(int id)
    {
        var roleId = ViewModel.SelectedRoleId;
        if (!ViewModel.IsCurrent || !await AdmitNavigationAsync() || IsDisposed || !ViewModel.IsCurrent ||
            ViewModel.SelectedRoleId != roleId || ViewModel.SelectedRole?.MemberUserIds.Contains(id) != true ||
            !ViewModel.Users.IsCurrent(ViewModel.OwnerVersion) || !ViewModel.Users.Items.Any(user => user.UserId == id))
            return;
        ViewModel.SelectUser(id);
        if (ViewModel.SelectedUserId != id) return;
        UserSearch.Clear();
        UserActivity.SelectedIndex = 0;
        Tabs.SelectedIndex = 0;
        UserDetail.Tabs.SelectedIndex = 1;
    }

    void FilterMasters()
    {
        var rendering = _rendering;
        _rendering = true;
        try
        {
            ((SecurityMasterListView)Users).Rebind(ViewModel.Users.Items.Where(user =>
                (UserActivity.SelectedIndex < 1 || user.IsActive == (UserActivity.SelectedIndex == 1)) &&
                (user.DisplayName + " " + user.Username).IndexOf(UserSearch.Text, StringComparison.CurrentCultureIgnoreCase) >= 0).Cast<object>(), ViewModel.SelectedUser);
            ((SecurityMasterListView)Roles).Rebind(ViewModel.Roles.Items.Where(role =>
                (RoleActivity.SelectedIndex < 1 || role.IsActive == (RoleActivity.SelectedIndex == 1)) &&
                role.Name.IndexOf(RoleSearch.Text, StringComparison.CurrentCultureIgnoreCase) >= 0).Cast<object>(), ViewModel.SelectedRole);
            SetPaneStatus(UserPaneStatus, ViewModel.Users.State, ViewModel.Users.Items.Count, Users.Items.Count, "người dùng");
            SetPaneStatus(RolePaneStatus, ViewModel.Roles.State, ViewModel.Roles.Items.Count, Roles.Items.Count, "vai trò");
        }
        finally { _rendering = rendering; }
    }

    static void SetPaneStatus(BootstrapLabel label, SecurityDatasetState state, int count, int visible, string kind)
    {
        label.Text = state == SecurityDatasetState.Ready
            ? count == 0 ? "Chưa có " + kind : visible == 0 ? "Không có kết quả phù hợp" : ""
            : SecurityPresentation.Dataset(state);
        label.TextRole = state == SecurityDatasetState.Forbidden || state == SecurityDatasetState.Failed
            ? BootstrapTextRole.Danger : BootstrapTextRole.Muted;
        label.AccessibleDescription = label.Text;
        label.Visible = label.Text.Length > 0;
    }

    static void Compose(TabPage page, Control list, Control detail)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 196));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.SizeChanged += (_, _) =>
            layout.ColumnStyles[0].Width = Math.Max(196, Math.Min(280, (int)Math.Round(layout.ClientSize.Width * 0.27)));
        layout.Controls.Add(list, 0, 0);
        layout.Controls.Add(detail, 1, 0);
        page.Controls.Add(layout);
    }

    public Task ActivateAsync(CancellationToken token) => LastOperation = ViewModel.LoadAsync(token);
    void ThemeChanged(object? sender, BootstrapThemeChangedEventArgs args)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(new Action(() => { ApplyTheme(this); StyleHeadings(); })); }
        else { ApplyTheme(this); StyleHeadings(); }
    }

    static void ApplyTheme(Control root)
    {
        var colors = BootstrapThemeManager.CurrentTheme.Colors;
        if (root is ListBox list) { list.BackColor = colors.Surface; list.ForeColor = colors.Text; }
        if (root is DataGridView grid)
        {
            grid.BackgroundColor = colors.Surface;
            grid.DefaultCellStyle.BackColor = colors.Surface;
            grid.DefaultCellStyle.ForeColor = colors.Text;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = colors.SurfaceSecondary;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = colors.Text;
        }
        // Public toolkit composites own their private native editors and theme subscriptions.
        if (root is BootstrapBusyHost busy) { ApplyTheme(busy.Content); return; }
        if (root is BootstrapPanel panel) { foreach (Control child in panel.Controls) ApplyTheme(child); return; }
        for (var type = root.GetType(); type != null; type = type.BaseType)
            if (type.Namespace == "MyDmsVn.Bootstrap5WinFormUI.Controls" && root is not TabControl) return;
        foreach (Control child in root.Controls) ApplyTheme(child);
    }
    void StyleHeadings()
    {
        _identityFont ??= new Font(Font.FontFamily, 13, FontStyle.Bold);
        _catalogFont ??= new Font(Font.FontFamily, 12, FontStyle.Bold);
        UserDetail.Header.Font = _identityFont;
        RoleDetail.Header.Font = _identityFont;
        PermissionCatalog.Heading.Font = _catalogFont;
    }
    void Changed(object? sender, EventArgs e)
    {
        if (!IsDisposed)
            Render();
    }

    void Render()
    {
        _rendering = true;
        try
        {
            FilterMasters();
            DatasetStatus.Text = "Người dùng: " + SecurityPresentation.Dataset(ViewModel.Users.State) + " · Vai trò: " + SecurityPresentation.Dataset(ViewModel.Roles.State) + " · Danh mục: " + SecurityPresentation.Dataset(ViewModel.Catalog.State);
            DatasetStatus.Visible = ViewModel.Users.State != SecurityDatasetState.Ready || ViewModel.Roles.State != SecurityDatasetState.Ready || ViewModel.Catalog.State != SecurityDatasetState.Ready;
            UserDetail.Render(ViewModel);
            RoleDetail.Render(ViewModel);
            PermissionCatalog.Render(ViewModel);
        }
        finally
        {
            _rendering = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            BootstrapThemeManager.ThemeChanged -= ThemeChanged;
            ViewModel.Changed -= Changed;
            ViewModel.Dispose();
            _identityFont?.Dispose();
            _catalogFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}
