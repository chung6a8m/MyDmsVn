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
    public Label Count { get; } = new() { Dock = DockStyle.Bottom, Height = 24 };
    public TextBox Details { get; } = new() { Dock = DockStyle.Bottom, Multiline = true, ReadOnly = true, Height = 100, ScrollBars = ScrollBars.Vertical };
    public TextBox Search { get; } = new() { Width = 210, AccessibleName = "Tìm khóa quyền" };
    public ComboBox Module { get; } = new() { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Phân hệ quyền" };
    public ComboBox Lifecycle { get; } = new() { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Vòng đời quyền" };
    readonly Label _status = new() { Dock = DockStyle.Top, Height = 48, Text = "Tra cứu quyền của hệ thống. Gán quyền tại Người dùng hoặc Vai trò." };
    SecurityWorkspaceViewModel? _viewModel;
    bool _rendering;

    public SecurityPermissionCatalogView()
    {
        Dock = DockStyle.Fill;
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 62, WrapContents = true, AutoScroll = true };
        filters.Controls.AddRange(new Control[] { Search, Module, Lifecycle });
        Module.Items.Add("Tất cả phân hệ");
        Lifecycle.Items.AddRange(new object[] { "Tất cả vòng đời", "Đang dùng", "Ngừng dùng" });
        Module.SelectedIndex = Lifecycle.SelectedIndex = 0;
        Controls.Add(Keys);
        Controls.Add(Details);
        Controls.Add(CopyKey);
        Controls.Add(Count);
        Controls.Add(filters);
        Controls.Add(_status);
        Search.TextChanged += (_, _) => Filter();
        Module.SelectedIndexChanged += (_, _) => Filter();
        Lifecycle.SelectedIndexChanged += (_, _) => Filter();
        foreach (var title in new[] { "Tên quyền", "Mã quyền", "Nhóm", "Trạng thái" }) Keys.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        CopyKey.Click += (_, _) => { if (Keys.CurrentRow?.Tag is PermissionCatalogItemDto selected) Clipboard.SetText(selected.Key); };
        Keys.CurrentCellChanged += (_, _) =>
        {
            if (Keys.CurrentRow?.Tag is PermissionCatalogItemDto key)
                Details.Text = key.Key + Environment.NewLine + key.Description + Environment.NewLine + key.Module + " · " + (key.Status == "Active" ? "Đang dùng" : "Ngừng dùng");
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
            Details.Text = "Danh mục quyền: " + _viewModel.Catalog.State + " — dữ liệu không khả dụng";
            return;
        }
        foreach (var key in _viewModel.Catalog.Items)
        {
            if (Module.SelectedIndex > 0 && key.Module != Module.Text) continue;
            if (Lifecycle.SelectedIndex == 1 && key.Status != "Active") continue;
            if (Lifecycle.SelectedIndex == 2 && key.Status != "Deprecated") continue;
            if (Search.Text.Length > 0 && (key.Key + " " + key.Description).IndexOf(Search.Text, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
            var index = Keys.Rows.Add(FriendlyName(key), key.Key, key.Module, key.Status == "Active" ? "Đang dùng" : "Ngừng dùng");
            Keys.Rows[index].Tag = key;
            if (key.Key == selectedKey) Keys.CurrentCell = Keys.Rows[index].Cells[0];
        }
        Count.Text = Keys.Rows.Count + " / " + _viewModel.Catalog.Items.Count + " quyền · Chỉ xem";
        CopyKey.Enabled = Keys.Rows.Count > 0;
        if (Keys.CurrentRow?.Tag is PermissionCatalogItemDto current)
            Details.Text = current.Key + Environment.NewLine + current.Description + Environment.NewLine + current.Module + " · " + (current.Status == "Active" ? "Đang dùng" : "Ngừng dùng");
    }

    static string FriendlyName(PermissionCatalogItemDto key) => key.Key switch
    {
        "Sales.Receipts.Read" => "Xem phiếu bán hàng",
        "Sales.Receipts.Write" => "Sửa phiếu bán hàng",
        "Sales.Receipts.Post" => "Ghi sổ bán hàng",
        _ => string.IsNullOrWhiteSpace(key.Description) ? key.Key : key.Description
    };
}
