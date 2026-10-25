using System;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Bootstrap5WinFormUI.Theme;

namespace MyDmsVn.Desktop.WinForms.Features.Security;
public sealed partial class SecurityWorkspaceControl : UserControl, IActivatableDesktopControl
{
    public SecurityWorkspaceViewModel ViewModel { get; }
    public TabControl Tabs { get; } = new()
    {
        Dock = DockStyle.Fill
    };
    public ListBox Users { get; } = new()
    {
        Dock = DockStyle.Fill,
        DisplayMember = "DisplayName"
    };
    public ListBox Roles { get; } = new()
    {
        Dock = DockStyle.Fill,
        DisplayMember = "Name"
    };
    public SecurityUserDetailView UserDetail { get; } = new();
    public SecurityRoleDetailView RoleDetail { get; } = new();
    public SecurityPermissionCatalogView PermissionCatalog { get; } = new();
    public Label DatasetStatus { get; } = new()
    {
        Dock = DockStyle.Top,
        Height = 30
    };
    public Task LastOperation { get; private set; } = Task.CompletedTask;
    internal Task TrackOperation(Task operation) => LastOperation = operation;

    public TextBox UserSearch { get; } = new() { Dock = DockStyle.Top, AccessibleName = "Tìm người dùng" };
    public TextBox RoleSearch { get; } = new() { Dock = DockStyle.Top, AccessibleName = "Tìm vai trò" };
    public ComboBox UserActivity { get; } = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Trạng thái người dùng" };
    public ComboBox RoleActivity { get; } = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Trạng thái vai trò" };
    bool _rendering;
    public SecurityWorkspaceControl(SecurityWorkspaceViewModel viewModel)
    {
        ViewModel = viewModel;
        Dock = DockStyle.Fill;
        Tabs.TabPages.Add("Người dùng");
        Tabs.TabPages.Add("Vai trò");
        Tabs.TabPages.Add("Danh mục quyền");
        Compose(Tabs.TabPages[0], Master(Users, UserSearch, UserActivity), UserDetail);
        Compose(Tabs.TabPages[1], Master(Roles, RoleSearch, RoleActivity), RoleDetail);
        Tabs.TabPages[2].Controls.Add(PermissionCatalog);
        Controls.Add(Tabs);
        Controls.Add(DatasetStatus);
        Users.FormattingEnabled = Roles.FormattingEnabled = true;
        Users.Format += (_, args) =>
        {
            if (args.ListItem is SecurityUserDto user) args.Value = user.DisplayName + " · " + user.Username + (user.IsActive ? " · Đang hoạt động" : " · Ngừng hoạt động") + (user.IsProtectedAccount ? " · Bảo vệ" : "");
        };
        Roles.Format += (_, args) =>
        {
            if (args.ListItem is SecurityRoleDto role) args.Value = role.Name + (role.IsActive ? " · Đang hoạt động" : " · Ngừng hoạt động");
        };
        Users.SelectedIndexChanged += async (_, __) =>
        {
            if (!_rendering && Users.SelectedItem is SecurityUserDto u)
            {
                if (ViewModel.SelectedUserId != u.UserId && !await AdmitNavigationAsync()) { Render(); return; }
                ViewModel.SelectUser(u.UserId);
            }
        };
        Roles.SelectedIndexChanged += async (_, __) =>
        {
            if (!_rendering && Roles.SelectedItem is SecurityRoleDto r)
            {
                if (ViewModel.SelectedRoleId != r.RoleId && !await AdmitNavigationAsync()) { Render(); return; }
                ViewModel.SelectRole(r.RoleId);
            }
        };
        RoleDetail.OpenUserRequested += id =>
        {
            if (!ViewModel.Users.IsCurrent(ViewModel.OwnerVersion))
                return;
            ViewModel.SelectUser(id);
            Tabs.SelectedIndex = 0;
            UserDetail.Tabs.SelectedIndex = 1;
        };
        UserSearch.TextChanged += (_, _) => FilterMasters();
        RoleSearch.TextChanged += (_, _) => FilterMasters();
        UserActivity.SelectedIndexChanged += (_, _) => FilterMasters();
        RoleActivity.SelectedIndexChanged += (_, _) => FilterMasters();
        UserDetail.RefreshButton.Click += async (_, _) => { if (await AdmitNavigationAsync()) await ActivateAsync(CancellationToken.None); };
        BusinessCompactAppearance.Apply(this);
        BootstrapThemeManager.ThemeChanged += ThemeChanged;
        ApplyTheme(this);
        ViewModel.Changed += Changed;
        _ = new SecurityWorkspaceEditorBindings(this);
        InitializeImportBindings();
        BusinessCompactAppearance.Apply(this);
        ApplyTheme(this);
        Render();
    }

    static Control Master(Control list, TextBox search, ComboBox activity)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        search.TabIndex = 0;
        activity.TabIndex = 1;
        list.TabIndex = 2;
        activity.Items.AddRange(new object[] { "Tất cả trạng thái", "Đang hoạt động", "Ngừng hoạt động" });
        activity.SelectedIndex = 0;
        panel.Controls.Add(list);
        panel.Controls.Add(activity);
        panel.Controls.Add(search);
        return panel;
    }

    void FilterMasters()
    {
        var rendering = _rendering;
        _rendering = true;
        try
        {
            Users.Items.Clear();
            Roles.Items.Clear();
            foreach (var user in ViewModel.Users.Items.Where(user => (UserActivity.SelectedIndex < 1 || user.IsActive == (UserActivity.SelectedIndex == 1)) && (user.DisplayName + " " + user.Username).IndexOf(UserSearch.Text, StringComparison.CurrentCultureIgnoreCase) >= 0)) Users.Items.Add(user);
            foreach (var role in ViewModel.Roles.Items.Where(role => (RoleActivity.SelectedIndex < 1 || role.IsActive == (RoleActivity.SelectedIndex == 1)) && role.Name.IndexOf(RoleSearch.Text, StringComparison.CurrentCultureIgnoreCase) >= 0)) Roles.Items.Add(role);
            Users.SelectedItem = ViewModel.SelectedUser;
            Roles.SelectedItem = ViewModel.SelectedRole;
        }
        finally { _rendering = rendering; }
    }

    static void Compose(TabPage page, Control list, Control detail)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 73));
        layout.Controls.Add(list, 0, 0);
        layout.Controls.Add(detail, 1, 0);
        page.Controls.Add(layout);
    }

    public Task ActivateAsync(CancellationToken token) => LastOperation = ViewModel.LoadAsync(token);
    void ThemeChanged(object? sender, BootstrapThemeChangedEventArgs args)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(new Action(() => ApplyTheme(this))); }
        else ApplyTheme(this);
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
        foreach (Control child in root.Controls) ApplyTheme(child);
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
            DatasetStatus.Text = "Người dùng: " + ViewModel.Users.State + " · Vai trò: " + ViewModel.Roles.State + " · Danh mục: " + ViewModel.Catalog.State;
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
        }

        base.Dispose(disposing);
    }
}
