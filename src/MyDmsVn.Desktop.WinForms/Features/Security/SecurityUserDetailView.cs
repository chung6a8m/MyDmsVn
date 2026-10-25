using System;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

public sealed class SecurityUserDetailView : UserControl
{
    public Label Header { get; } = new() { Dock = DockStyle.Top, Height = 27, AutoEllipsis = true };
    public Label HeaderMetadata { get; } = new() { Dock = DockStyle.Top, Height = 23, AutoEllipsis = true };
    public Label HeaderState { get; } = new() { Dock = DockStyle.Top, Height = 24, AutoEllipsis = true };
    public FlowLayoutPanel HeaderActions { get; } = new() { Dock = DockStyle.Top, Height = 42, AutoScroll = true, WrapContents = false };
    public TabControl Tabs { get; } = new() { Dock = DockStyle.Fill };
    public ListBox Memberships { get; } = new() { Dock = DockStyle.Fill };
    public DataGridView Effective { get; } = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    public TextBox Explanation { get; } = new() { Dock = DockStyle.Bottom, Height = 80, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    public TextBox Search { get; } = new() { Width = 150, AccessibleName = "Tìm quyền hiệu lực" };
    public ComboBox Module { get; } = new() { Width = 115, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Phân hệ quyền hiệu lực" };
    public ComboBox Result { get; } = new() { Width = 115, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Kết quả quyền hiệu lực" };
    public Button RefreshButton { get; } = new() { Text = "Làm mới", AutoSize = true };
    public Button OpenDirectPermission { get; } = new() { Text = "Mở quyền riêng", AutoSize = true };
    public ListBox DirectDecisions { get; } = new() { Dock = DockStyle.Fill, AccessibleName = "Quyền riêng của người dùng" };
    public event Action<string>? DirectPermissionRequested;
    readonly Label _profile = new() { Dock = DockStyle.Fill, Padding = new Padding(12), AutoEllipsis = true };
    SecurityWorkspaceViewModel? _viewModel;
    bool _rendering;

    public SecurityUserDetailView()
    {
        Dock = DockStyle.Fill;
        foreach (var title in new[] { "Thông tin", "Vai trò", "Quyền hiệu lực", "Quyền riêng" }) Tabs.TabPages.Add(title);
        Tabs.TabPages[0].Controls.Add(_profile);
        Tabs.TabPages[1].Controls.Add(Memberships);
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 62, WrapContents = true, AutoScroll = true };
        filters.Controls.AddRange(new Control[] { Search, Module, Result, RefreshButton });
        Module.Items.Add("Tất cả phân hệ");
        Result.Items.AddRange(new object[] { "Tất cả kết quả", "Cho phép", "Từ chối", "Chưa được cấp", "Chưa xác định" });
        Module.SelectedIndex = Result.SelectedIndex = 0;
        foreach (var name in new[] { "Quyền", "Kết quả", "Nguồn quyết định" }) Effective.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        Effective.Columns[0].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        Tabs.TabPages[2].Controls.Add(Effective);
        Tabs.TabPages[2].Controls.Add(Explanation);
        var effectiveActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 35, WrapContents = false };
        effectiveActions.Controls.Add(OpenDirectPermission);
        Tabs.TabPages[2].Controls.Add(effectiveActions);
        Tabs.TabPages[2].Controls.Add(filters);
        Tabs.TabPages[3].Controls.Add(DirectDecisions);
        Controls.Add(Tabs);
        var identity = new Panel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(10, 5, 10, 4) };
        identity.Controls.Add(HeaderActions);
        identity.Controls.Add(HeaderState);
        identity.Controls.Add(HeaderMetadata);
        identity.Controls.Add(Header);
        Controls.Add(identity);
        Effective.SelectionChanged += (_, _) => Explain();
        Search.TextChanged += (_, _) => Filter();
        Module.SelectedIndexChanged += (_, _) => Filter();
        Result.SelectedIndexChanged += (_, _) => Filter();
        OpenDirectPermission.Click += (_, _) =>
        {
            if (Effective.CurrentRow?.Tag is string key) DirectPermissionRequested?.Invoke(key);
        };
        DirectDecisions.SelectedIndexChanged += (_, _) =>
        {
            if (!_rendering && DirectDecisions.SelectedItem is DirectDecisionItem item && item.Editable)
                DirectPermissionRequested?.Invoke(item.Key);
        };
    }

    public void Render(SecurityWorkspaceViewModel viewModel)
    {
        _viewModel = viewModel;
        var user = viewModel.SelectedUser;
        Header.Text = user == null ? "Chọn người dùng" : user.DisplayName;
        HeaderMetadata.Text = user == null ? "" : "@" + user.Username + (string.IsNullOrWhiteSpace(user.Email) ? "" : "  ·  " + user.Email);
        HeaderState.Text = user == null ? "" : (user.IsActive ? "● Đang hoạt động" : "● Ngừng hoạt động") + (user.IsProtectedAccount ? "  ·  Tài khoản được bảo vệ" : "");
        _profile.Text = user == null ? "Chưa chọn người dùng" : "THÔNG TIN TÀI KHOẢN" + Environment.NewLine + Environment.NewLine + "Tên hiển thị   " + user.DisplayName + Environment.NewLine + "Tên đăng nhập   " + user.Username + Environment.NewLine + "Email   " + (string.IsNullOrWhiteSpace(user.Email) ? "—" : user.Email) + Environment.NewLine + "Trạng thái   " + (user.IsActive ? "Đang hoạt động" : "Ngừng hoạt động");
        Tabs.TabPages[1].Text = "Vai trò (" + (user?.RoleIds.Count.ToString() ?? "—") + ")";
        Tabs.TabPages[3].Text = "Quyền riêng (" + (user?.DirectDecisions.Count.ToString() ?? "—") + ")";
        Memberships.Items.Clear();
        if (user != null)
        {
            foreach (var id in user.RoleIds)
            {
                var role = viewModel.Roles.Items.FirstOrDefault(item => item.RoleId == id);
                Memberships.Items.Add(role == null ? "#" + id + " — Chi tiết không khả dụng" : role.Name + (role.IsActive ? "" : " — Vai trò ngừng hoạt động — không cấp quyền"));
            }
        }
        _rendering = true;
        var selectedModule = Module.SelectedItem as string;
        Module.Items.Clear();
        Module.Items.Add("Tất cả phân hệ");
        foreach (var module in viewModel.Catalog.Items.Select(key => key.Module).Distinct().OrderBy(value => value)) Module.Items.Add(module);
        Module.SelectedIndex = selectedModule != null && Module.Items.Contains(selectedModule) ? Module.Items.IndexOf(selectedModule) : 0;
        _rendering = false;
        Filter();
        _rendering = true;
        DirectDecisions.Items.Clear();
        if (user != null)
            foreach (var decision in user.DirectDecisions)
            {
                var key = viewModel.Catalog.Items.FirstOrDefault(item => item.Key == decision.PermissionKey);
                var label = key == null ? decision.PermissionKey : SecurityPresentation.Name(key) + " · " + decision.PermissionKey;
                var history = !viewModel.Catalog.IsCurrent(viewModel.OwnerVersion) ? " · Danh mục chưa khả dụng" : key == null ? " · Không còn trong danh mục" : key.Status == "Active" ? "" : " · Ngừng dùng";
                DirectDecisions.Items.Add(new DirectDecisionItem(decision.PermissionKey, label + " · " + (decision.Decision == true ? "Cho phép" : decision.Decision == false ? "Từ chối" : "Theo vai trò") + history, key?.Status == "Active"));
            }
        _rendering = false;
    }

    void Filter()
    {
        if (_rendering || _viewModel == null) return;
        var selectedKey = Effective.CurrentRow?.Tag as string;
        Effective.Rows.Clear();
        Explanation.Text = "Theo dữ liệu phân quyền đã tải";
        OpenDirectPermission.Enabled = false;
        if (!_viewModel.Catalog.IsCurrent(_viewModel.OwnerVersion))
        {
            Explanation.Text = "Danh mục quyền chưa khả dụng — chưa thể kết luận";
            return;
        }
        foreach (var key in _viewModel.Catalog.Items)
        {
            if (Module.SelectedIndex > 0 && key.Module != Module.Text) continue;
            if (Search.Text.Length > 0 && (key.Key + " " + key.Description).IndexOf(Search.Text, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
            var explanation = SecurityPermissionProjection.Evaluate(_viewModel.Snapshot, key.Key);
            var result = ResultText(explanation.Result);
            if (Result.SelectedIndex > 0 && !result.EndsWith(Result.Text, StringComparison.Ordinal)) continue;
            var index = Effective.Rows.Add(SecurityPresentation.Name(key) + Environment.NewLine + key.Key, result, SecurityPresentation.Source(explanation));
            Effective.Rows[index].Height = 45;
            Effective.Rows[index].Tag = key.Key;
            if (key.Key == selectedKey) Effective.CurrentCell = Effective.Rows[index].Cells[0];
        }
        Explain();
    }

    void Explain()
    {
        if (_viewModel == null || Effective.CurrentRow?.Tag is not string key) return;
        var explanation = SecurityPermissionProjection.Evaluate(_viewModel.Snapshot, key);
        Explanation.Text = "QUYỀN  " + key + Environment.NewLine + "KẾT QUẢ  " + ResultText(explanation.Result) + Environment.NewLine + "NGUỒN  " + SecurityPresentation.Source(explanation) + Environment.NewLine + (explanation.IsProvenanceComplete ? "Đã tải đủ nguồn vai trò" : "Nguồn vai trò chưa đầy đủ");
        OpenDirectPermission.Enabled = _viewModel.SelectedUser is { IsProtectedAccount: false } && _viewModel.Catalog.IsCurrent(_viewModel.OwnerVersion) && _viewModel.Catalog.Items.Any(item => item.Key == key && item.Status == "Active");
    }

    static string ResultText(SecurityPermissionResult result) => SecurityPresentation.Result(result);

    sealed class DirectDecisionItem
    {
        internal DirectDecisionItem(string key, string text, bool editable) { Key = key; Text = text; Editable = editable; }
        internal string Key { get; }
        internal bool Editable { get; }
        string Text { get; }
        public override string ToString() => Text;
    }
}
