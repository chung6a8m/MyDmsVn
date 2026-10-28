using System;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

public sealed class SecurityPermissionCatalogView : UserControl
{
    public DataGridView Keys { get; } = new BootstrapDataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, StandardTab = true, AccessibleName = "Định nghĩa quyền — chỉ xem" };
    public Button CopyKey { get; } = new BootstrapButton { Text = "Sao chép mã quyền", Dock = DockStyle.Bottom, Height = 34, Variant = BootstrapVariant.Secondary, Outline = true };
    public Button RefreshButton { get; } = new BootstrapButton { Text = "Làm mới", AutoSize = true, Variant = BootstrapVariant.Secondary, Outline = true };
    public Label Count { get; } = new BootstrapLabel { Dock = DockStyle.Bottom, Height = 24, TextRole = BootstrapTextRole.Muted };
    // The pinned BootstrapTextBox exposes only a single-line editor; selected long definitions
    // require native multiline text selection/copy and vertical scrolling through public APIs.
    public TextBox Details { get; } = new() { Dock = DockStyle.Bottom, Multiline = true, ReadOnly = true, Height = 116, ScrollBars = ScrollBars.Vertical, AccessibleName = "Chi tiết định nghĩa quyền đã chọn" };
    public BootstrapTextBox Search { get; } = new() { Width = 210, AccessibleName = "Tìm khóa quyền", PlaceholderText = "Tìm tên hoặc mã quyền" };
    public ComboBox Module { get; } = new BootstrapComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Phân hệ quyền" };
    public SecurityDetailListView ModuleGroups { get; } = new() { Dock = DockStyle.Fill, AccessibleName = "Nhóm quyền" };
    public ComboBox Lifecycle { get; } = new BootstrapComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Vòng đời quyền" };
    readonly Label _status = new BootstrapLabel { Dock = DockStyle.Top, Height = 37, Text = "Tra cứu quyền của hệ thống. Gán quyền tại Người dùng hoặc Vai trò.", TextRole = BootstrapTextRole.Muted };
    readonly Label _heading = new BootstrapLabel { Dock = DockStyle.Top, Height = 42, Text = "Danh mục quyền   ·   Chỉ xem" };
    public Label Heading => _heading;
    readonly Label _groupHeading = new BootstrapLabel { Dock = DockStyle.Top, Height = 36, Text = "Nhóm quyền" };
    SecurityWorkspaceViewModel? _viewModel;
    bool _rendering;

    public SecurityPermissionCatalogView()
    {
        Dock = DockStyle.Fill;
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
        filters.Controls.AddRange(new Control[] { Search, Lifecycle, RefreshButton });
        Module.Items.Add("Tất cả phân hệ");
        Lifecycle.Items.AddRange(new object[] { "Tất cả vòng đời", "Đang dùng", "Ngừng dùng" });
        Module.SelectedIndex = Lifecycle.SelectedIndex = 0;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 77));
        var groups = new BootstrapPanel { Dock = DockStyle.Fill, Padding = new Padding(8), SurfaceRole = BootstrapSurfaceRole.Surface };
        groups.Controls.Add(ModuleGroups);
        groups.Controls.Add(_groupHeading);
        var content = new BootstrapPanel { Dock = DockStyle.Fill, Padding = new Padding(8), SurfaceRole = BootstrapSurfaceRole.Surface };
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
        Module.SelectedIndexChanged += (_, _) =>
        {
            if (!_rendering && ModuleGroups.SelectedIndex != Module.SelectedIndex)
                ModuleGroups.SelectedIndex = Module.SelectedIndex;
            Filter();
        };
        ModuleGroups.SelectedIndexChanged += (_, _) =>
        {
            if (!_rendering && ModuleGroups.SelectedIndex >= 0 && ModuleGroups.SelectedIndex < Module.Items.Count)
                Module.SelectedIndex = ModuleGroups.SelectedIndex;
        };
        Lifecycle.SelectedIndexChanged += (_, _) => Filter();
        foreach (var title in new[] { "Tên quyền", "Mã quyền", "Nhóm", "Trạng thái" }) Keys.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
        Keys.RowTemplate.Height = Math.Max(BusinessCompactAppearance.Scale(Keys, 30), Keys.Font.Height + 8);
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
        if (viewModel.Catalog.IsCurrent(viewModel.OwnerVersion))
        {
            var groups = new object[] { "Tất cả  (" + viewModel.Catalog.Items.Count + ")" }.Concat(Module.Items.Cast<string>().Skip(1)
                .Select(module => (object)(module + "  (" + viewModel.Catalog.Items.Count(key => key.Module == module) + ")"))).ToArray();
            ModuleGroups.Rebind(groups, true);
            ModuleGroups.SelectedIndex = Module.SelectedIndex;
        }
        else ModuleGroups.Rebind(new object[] { "Nhóm quyền chưa khả dụng" }, false);
        _rendering = false;
        Filter();
    }

    void Filter()
    {
        if (_rendering || _viewModel == null) return;
        if (!_viewModel.Catalog.IsCurrent(_viewModel.OwnerVersion))
        {
            Keys.Rows.Clear();
            CopyKey.Enabled = false;
            Count.Text = "Số quyền: chưa khả dụng";
            Details.Text = "Danh mục quyền: " + SecurityPresentation.Dataset(_viewModel.Catalog.State) + " — dữ liệu không khả dụng";
            ((BootstrapDataGridView)Keys).EmptyStateText = Details.Text;
            return;
        }
        var filtered = _viewModel.Catalog.Items.Where(key =>
            (Module.SelectedIndex <= 0 || key.Module == Module.Text) &&
            (Lifecycle.SelectedIndex != 1 || key.Status == "Active") &&
            (Lifecycle.SelectedIndex != 2 || key.Status == "Deprecated") &&
            (Search.Text.Length == 0 || (key.Key + " " + SecurityPresentation.Name(key) + " " + key.Description).IndexOf(Search.Text, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToArray();
        var sameRows = filtered.Length == Keys.Rows.Count && filtered.Select((key, index) => ReferenceEquals(key, Keys.Rows[index].Tag)).All(same => same);
        if (!sameRows)
        {
            var selectedKey = (Keys.CurrentRow?.Tag as PermissionCatalogItemDto)?.Key;
            var firstVisibleKey = Keys.FirstDisplayedScrollingRowIndex >= 0
                ? (Keys.Rows[Keys.FirstDisplayedScrollingRowIndex].Tag as PermissionCatalogItemDto)?.Key : null;
            Keys.Rows.Clear();
            foreach (var key in filtered)
            {
                var index = Keys.Rows.Add(SecurityPresentation.Name(key), key.Key, key.Module, key.Status == "Active" ? "Đang dùng" : "Ngừng dùng");
                var row = Keys.Rows[index];
                row.Tag = key;
                foreach (DataGridViewCell cell in row.Cells) cell.ToolTipText = cell.Value?.ToString() ?? "";
                if (key.Key == selectedKey) Keys.CurrentCell = row.Cells[0];
            }
            var anchor = Keys.Rows.Cast<DataGridViewRow>().FirstOrDefault(row => (row.Tag as PermissionCatalogItemDto)?.Key == firstVisibleKey);
            if (anchor != null) Keys.FirstDisplayedScrollingRowIndex = anchor.Index;
        }
        Count.Text = Keys.Rows.Count + " / " + _viewModel.Catalog.Items.Count + " quyền · Chỉ xem";
        CopyKey.Enabled = Keys.Rows.Count > 0;
        if (Keys.Rows.Count == 0)
            Details.Text = _viewModel.Catalog.Items.Count == 0 ? "Danh mục quyền chưa có mục nào." : "Không có quyền phù hợp bộ lọc.";
        else if (Keys.CurrentRow?.Tag is PermissionCatalogItemDto current)
            Details.Text = DetailText(current);
        ((BootstrapDataGridView)Keys).EmptyStateText = Keys.Rows.Count == 0 ? Details.Text : "Không có quyền phù hợp bộ lọc.";
    }

    static string DetailText(PermissionCatalogItemDto key) => "CHI TIẾT QUYỀN" + Environment.NewLine + "Tên  " + SecurityPresentation.Name(key) + Environment.NewLine + "Mã quyền  " + key.Key + Environment.NewLine + "Mô tả  " + key.Description + Environment.NewLine + "Nhóm  " + key.Module + " · Trạng thái  " + (key.Status == "Active" ? "Đang dùng" : "Ngừng dùng");

    protected override void Dispose(bool disposing)
    {
        // Module is the existing canonical selector behind the visible group list and has no parent.
        if (disposing) Module.Dispose();
        base.Dispose(disposing);
    }
}
