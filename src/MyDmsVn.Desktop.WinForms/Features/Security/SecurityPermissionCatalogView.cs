using System;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

public sealed class SecurityPermissionCatalogView : UserControl
{
    public DataGridView Keys { get; } = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    public Button CopyKey { get; } = new() { Text = "Sao chép mã quyền", Dock = DockStyle.Bottom, Height = 32 };
    public Button RefreshButton { get; } = new() { Text = "Làm mới", AutoSize = true };
    public Label Count { get; } = new() { Dock = DockStyle.Bottom, Height = 24 };
    public TextBox Details { get; } = new() { Dock = DockStyle.Bottom, Multiline = true, ReadOnly = true, Height = 116, ScrollBars = ScrollBars.Vertical };
    public TextBox Search { get; } = new() { Width = 210, AccessibleName = "Tìm khóa quyền" };
    public ComboBox Module { get; } = new() { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Phân hệ quyền" };
    public ListBox ModuleGroups { get; } = new() { Dock = DockStyle.Fill, AccessibleName = "Nhóm quyền" };
    public ComboBox Lifecycle { get; } = new() { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Vòng đời quyền" };
    readonly Label _status = new() { Dock = DockStyle.Top, Height = 37, Text = "Tra cứu quyền của hệ thống. Gán quyền tại Người dùng hoặc Vai trò." };
    readonly Label _heading = new() { Dock = DockStyle.Top, Height = 42, Text = "Danh mục quyền   ·   Chỉ xem" };
    public Label Heading => _heading;
    readonly Label _groupHeading = new() { Dock = DockStyle.Top, Height = 36, Text = "Nhóm quyền" };
    SecurityWorkspaceViewModel? _viewModel;
    bool _rendering;

    public SecurityPermissionCatalogView()
    {
        Dock = DockStyle.Fill;
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 62, WrapContents = true, AutoScroll = true };
        filters.Controls.AddRange(new Control[] { Search, Lifecycle, RefreshButton });
        Module.Items.Add("Tất cả phân hệ");
        Lifecycle.Items.AddRange(new object[] { "Tất cả vòng đời", "Đang dùng", "Ngừng dùng" });
        Module.SelectedIndex = Lifecycle.SelectedIndex = 0;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 77));
        var groups = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        groups.Controls.Add(ModuleGroups);
        groups.Controls.Add(_groupHeading);
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        content.Controls.Add(Keys);
        content.Controls.Add(Details);
        content.Controls.Add(CopyKey);
        content.Controls.Add(Count);
        content.Controls.Add(filters);
        content.Controls.Add(_status);
        content.Controls.Add(_heading);
        layout.Controls.Add(groups, 0, 0);
        layout.Controls.Add(content, 1, 0);
        Controls.Add(layout);
        Search.TextChanged += (_, _) => Filter();
        Module.SelectedIndexChanged += (_, _) => Filter();
        ModuleGroups.SelectedIndexChanged += (_, _) =>
        {
            if (!_rendering && ModuleGroups.SelectedIndex >= 0 && ModuleGroups.SelectedIndex < Module.Items.Count)
                Module.SelectedIndex = ModuleGroups.SelectedIndex;
        };
        Lifecycle.SelectedIndexChanged += (_, _) => Filter();
        foreach (var title in new[] { "Tên quyền", "Mã quyền", "Nhóm", "Trạng thái" }) Keys.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        CopyKey.Click += (_, _) => { if (Keys.CurrentRow?.Tag is PermissionCatalogItemDto selected) Clipboard.SetText(selected.Key); };
        Keys.CurrentCellChanged += (_, _) =>
        {
            if (Keys.CurrentRow?.Tag is PermissionCatalogItemDto key)
                Details.Text = DetailText(key);
        };
    }

    public void Render(SecurityWorkspaceViewModel viewModel)
    {
        _viewModel = viewModel;
        _rendering = true;
        var selected = Module.SelectedItem as string;
        Module.Items.Clear();
        Module.Items.Add("Tất cả phân hệ");
        foreach (var module in viewModel.Catalog.Items.Select(key => key.Module).Distinct().OrderBy(value => value)) Module.Items.Add(module);
        Module.SelectedIndex = selected != null && Module.Items.Contains(selected) ? Module.Items.IndexOf(selected) : 0;
        ModuleGroups.Items.Clear();
        if (viewModel.Catalog.IsCurrent(viewModel.OwnerVersion))
        {
            ModuleGroups.Items.Add("Tất cả  (" + viewModel.Catalog.Items.Count + ")");
            foreach (var module in Module.Items.Cast<string>().Skip(1))
                ModuleGroups.Items.Add(module + "  (" + viewModel.Catalog.Items.Count(key => key.Module == module) + ")");
            ModuleGroups.SelectedIndex = Module.SelectedIndex;
        }
        else ModuleGroups.Items.Add("Nhóm quyền chưa khả dụng");
        _rendering = false;
        Filter();
    }

    void Filter()
    {
        if (_rendering || _viewModel == null) return;
        var selectedKey = (Keys.CurrentRow?.Tag as PermissionCatalogItemDto)?.Key;
        Keys.Rows.Clear();
        Details.Clear();
        CopyKey.Enabled = false;
        Count.Text = "Số quyền: chưa khả dụng";
        if (!_viewModel.Catalog.IsCurrent(_viewModel.OwnerVersion))
        {
            Details.Text = "Danh mục quyền: " + SecurityPresentation.Dataset(_viewModel.Catalog.State) + " — dữ liệu không khả dụng";
            return;
        }
        foreach (var key in _viewModel.Catalog.Items)
        {
            if (Module.SelectedIndex > 0 && key.Module != Module.Text) continue;
            if (Lifecycle.SelectedIndex == 1 && key.Status != "Active") continue;
            if (Lifecycle.SelectedIndex == 2 && key.Status != "Deprecated") continue;
            if (Search.Text.Length > 0 && (key.Key + " " + SecurityPresentation.Name(key) + " " + key.Description).IndexOf(Search.Text, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
            var index = Keys.Rows.Add(SecurityPresentation.Name(key), key.Key, key.Module, key.Status == "Active" ? "Đang dùng" : "Ngừng dùng");
            Keys.Rows[index].Tag = key;
            if (key.Key == selectedKey) Keys.CurrentCell = Keys.Rows[index].Cells[0];
        }
        Count.Text = Keys.Rows.Count + " / " + _viewModel.Catalog.Items.Count + " quyền · Chỉ xem";
        CopyKey.Enabled = Keys.Rows.Count > 0;
        if (Keys.CurrentRow?.Tag is PermissionCatalogItemDto current)
            Details.Text = DetailText(current);
    }

    static string DetailText(PermissionCatalogItemDto key) => "CHI TIẾT QUYỀN" + Environment.NewLine + "Tên  " + SecurityPresentation.Name(key) + Environment.NewLine + "Mã quyền  " + key.Key + Environment.NewLine + "Mô tả  " + key.Description + Environment.NewLine + "Nhóm  " + key.Module + " · Trạng thái  " + (key.Status == "Active" ? "Đang dùng" : "Ngừng dùng");
}
