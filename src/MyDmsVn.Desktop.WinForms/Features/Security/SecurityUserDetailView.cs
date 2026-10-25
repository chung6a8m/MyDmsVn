using System;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

public sealed class SecurityUserDetailView : UserControl
{
    public Label Header { get; } = new() { Dock = DockStyle.Top, Height = 55, AutoEllipsis = true };
    public TabControl Tabs { get; } = new() { Dock = DockStyle.Fill };
    public ListBox Memberships { get; } = new() { Dock = DockStyle.Fill };
    public DataGridView Effective { get; } = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    public TextBox Explanation { get; } = new() { Dock = DockStyle.Bottom, Height = 80, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    public TextBox Search { get; } = new() { Width = 150, AccessibleName = "Tìm quyền hiệu lực" };
    public ComboBox Module { get; } = new() { Width = 115, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Phân hệ quyền hiệu lực" };
    public ComboBox Result { get; } = new() { Width = 115, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Kết quả quyền hiệu lực" };
    public Button RefreshButton { get; } = new() { Text = "Làm mới", AutoSize = true };
    readonly Label _profile = new() { Dock = DockStyle.Fill };
    readonly ListBox _direct = new() { Dock = DockStyle.Fill };
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
        Result.Items.AddRange(new object[] { "Tất cả kết quả", "Cho phép", "Từ chối", "Chưa gán", "Chưa đủ dữ liệu" });
        Module.SelectedIndex = Result.SelectedIndex = 0;
        foreach (var name in new[] { "Quyền", "Kết quả", "Nguồn quyết định" }) Effective.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        Tabs.TabPages[2].Controls.Add(Effective);
        Tabs.TabPages[2].Controls.Add(Explanation);
        Tabs.TabPages[2].Controls.Add(filters);
        Tabs.TabPages[3].Controls.Add(_direct);
        Controls.Add(Tabs);
        Controls.Add(Header);
        Effective.SelectionChanged += (_, _) => Explain();
        Search.TextChanged += (_, _) => Filter();
        Module.SelectedIndexChanged += (_, _) => Filter();
        Result.SelectedIndexChanged += (_, _) => Filter();
    }

    public void Render(SecurityWorkspaceViewModel viewModel)
    {
        _viewModel = viewModel;
        var user = viewModel.SelectedUser;
        Header.Text = user == null ? "Chọn người dùng" : user.DisplayName + " · " + user.Username + " · " + (user.IsActive ? "Đang hoạt động" : "Ngừng hoạt động") + (user.IsProtectedAccount ? " · Tài khoản được bảo vệ" : "");
        _profile.Text = user == null ? "Chưa tải người dùng" : user.Username + Environment.NewLine + user.DisplayName + Environment.NewLine + user.Email;
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
        _direct.Items.Clear();
        if (user != null)
            foreach (var decision in user.DirectDecisions)
                _direct.Items.Add(decision.PermissionKey + " · " + (decision.Decision == true ? "Cho phép" : decision.Decision == false ? "Từ chối" : "Theo vai trò"));
    }

    void Filter()
    {
        if (_rendering || _viewModel == null) return;
        var selectedKey = Effective.CurrentRow?.Tag as string;
        Effective.Rows.Clear();
        Explanation.Text = "Theo dữ liệu phân quyền đã tải";
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
            if (Result.SelectedIndex > 0 && result != Result.Text) continue;
            var index = Effective.Rows.Add(key.Key, result, explanation.Reason.ToString());
            Effective.Rows[index].Tag = key.Key;
            if (key.Key == selectedKey) Effective.CurrentCell = Effective.Rows[index].Cells[0];
        }
        Explain();
    }

    void Explain()
    {
        if (_viewModel == null || Effective.CurrentRow?.Tag is not string key) return;
        var explanation = SecurityPermissionProjection.Evaluate(_viewModel.Snapshot, key);
        Explanation.Text = key + Environment.NewLine + ResultText(explanation.Result) + " · " + explanation.Reason + Environment.NewLine + (explanation.IsProvenanceComplete ? "Nguồn đầy đủ" : "Nguồn chưa đầy đủ") + " · " + string.Join(", ", explanation.RoleSources.Select(role => role.Name));
    }

    static string ResultText(SecurityPermissionResult result) => result == SecurityPermissionResult.Allowed ? "Cho phép" : result == SecurityPermissionResult.Denied ? "Từ chối" : result == SecurityPermissionResult.Unassigned ? "Chưa gán" : "Chưa đủ dữ liệu";
}
