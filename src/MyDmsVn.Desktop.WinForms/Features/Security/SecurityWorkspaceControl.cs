using System;
using System.Threading;
using System.Linq;
using System.Drawing;
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
    public ListBox Users { get; } = new SecurityMasterListBox
    {
        Dock = DockStyle.Fill,
        DisplayMember = "DisplayName"
    };
    public ListBox Roles { get; } = new SecurityMasterListBox
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
    public Label UserPaneStatus { get; } = new() { Dock = DockStyle.Top, Height = 28, AutoEllipsis = true };
    public Label RolePaneStatus { get; } = new() { Dock = DockStyle.Top, Height = 28, AutoEllipsis = true };
    public Task LastOperation { get; private set; } = Task.CompletedTask;
    internal Task TrackOperation(Task operation) => LastOperation = operation;

    public TextBox UserSearch { get; } = new() { Dock = DockStyle.Top, AccessibleName = "Tìm người dùng" };
    public TextBox RoleSearch { get; } = new() { Dock = DockStyle.Top, AccessibleName = "Tìm vai trò" };
    public ComboBox UserActivity { get; } = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Trạng thái người dùng" };
    public ComboBox RoleActivity { get; } = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Trạng thái vai trò" };
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
        Users.FormattingEnabled = Roles.FormattingEnabled = true;
        Users.Format += (_, args) =>
        {
            if (args.ListItem is SecurityUserDto user)
                args.Value = user.DisplayName + ", " + user.Username + ", " + (user.IsActive ? "đang hoạt động" : "ngừng hoạt động") + (user.IsProtectedAccount ? ", tài khoản được bảo vệ" : "");
        };
        Roles.Format += (_, args) =>
        {
            if (args.ListItem is SecurityRoleDto role)
                args.Value = role.Name + ", " + role.MemberUserIds.Count + " thành viên, " + (role.IsActive ? "đang hoạt động" : "ngừng hoạt động");
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
        StyleHeadings();
        Render();
    }

    static Control Master(Control list, TextBox search, ComboBox activity, Label status)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        search.TabIndex = 0;
        activity.TabIndex = 1;
        list.TabIndex = 2;
        activity.Items.AddRange(new object[] { "Tất cả trạng thái", "Đang hoạt động", "Ngừng hoạt động" });
        activity.SelectedIndex = 0;
        panel.Controls.Add(list);
        panel.Controls.Add(activity);
        panel.Controls.Add(search);
        panel.Controls.Add(status);
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
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 77));
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
            UserPaneStatus.Text = ViewModel.Users.State == SecurityDatasetState.Ready ? "" : "Người dùng: " + SecurityPresentation.Dataset(ViewModel.Users.State);
            RolePaneStatus.Text = ViewModel.Roles.State == SecurityDatasetState.Ready ? "" : "Vai trò: " + SecurityPresentation.Dataset(ViewModel.Roles.State);
            UserPaneStatus.Visible = ViewModel.Users.State != SecurityDatasetState.Ready;
            RolePaneStatus.Visible = ViewModel.Roles.State != SecurityDatasetState.Ready;
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
