using System;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Desktop.Application.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

public sealed class SecurityUserDetailView : UserControl
{
    public Label Header { get; } = new BootstrapLabel { Dock = DockStyle.Top, Height = 27, AutoEllipsis = true, AccessibleName = "Người dùng đã chọn" };
    public Label HeaderMetadata { get; } = new BootstrapLabel { Dock = DockStyle.Top, Height = 23, AutoEllipsis = true, TextRole = BootstrapTextRole.Muted };
    public Label HeaderState { get; } = new BootstrapLabel { Dock = DockStyle.Top, Height = 24, AutoEllipsis = true };
    public FlowLayoutPanel HeaderActions { get; } = new() { Dock = DockStyle.Top, Height = 32, AutoScroll = false, WrapContents = true };
    public Label ActionFeedback { get; } = new BootstrapLabel { Dock = DockStyle.Top, Height = 26, Visible = false, AutoEllipsis = true,
        Padding = new Padding(8, 4, 8, 0), AccessibleName = "Thông báo đổi trạng thái" };
    public TabControl Tabs { get; } = new BusinessCompactWorkspaceTabs { Dock = DockStyle.Fill };
    public SecurityDetailListView Memberships { get; } = new() { AccessibleName = "Vai trò của người dùng" };
    public DataGridView Effective { get; } = new BootstrapDataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, StandardTab = true, AccessibleName = "Quyền hiệu lực đã tải", EmptyStateText = "Không có quyền phù hợp bộ lọc." };
    // The pinned BootstrapTextBox owns a single-line EDIT and exposes no Multiline/ScrollBars contract.
    // Native read-only EDIT retains multiline selection, copy, vertical scroll and keyboard accessibility.
    public TextBox Explanation { get; } = new() { Dock = DockStyle.Bottom, Height = 88, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, AccessibleName = "Giải thích quyền hiệu lực đã tải" };
    public BootstrapTextBox Search { get; } = new() { Width = 150, AccessibleName = "Tìm quyền hiệu lực", PlaceholderText = "Tìm quyền…" };
    public ComboBox Module { get; } = new BootstrapComboBox { Width = 135, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Phân hệ quyền hiệu lực" };
    public ComboBox Result { get; } = new BootstrapComboBox { Width = 145, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Kết quả quyền hiệu lực" };
    public Button RefreshButton { get; } = new BootstrapButton { Text = "Làm mới", AutoSize = true, Outline = true, Variant = BootstrapVariant.Secondary, ButtonSize = BootstrapButtonSize.Small };
    public Button OpenDirectPermission { get; } = new BootstrapButton { Text = "Mở quyền riêng", AutoSize = true, Outline = true, ButtonSize = BootstrapButtonSize.Small };
    public SecurityDetailListView DirectDecisions { get; } = new() { AccessibleName = "Quyền riêng của người dùng" };
    public event Action<string>? DirectPermissionRequested;
    readonly Label _profile = new BootstrapLabel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(12, 8, 12, 0), TextRole = BootstrapTextRole.Muted, Text = "THÔNG TIN TÀI KHOẢN" };
    readonly BootstrapLabel[] _profileValues = Enumerable.Range(0, 4).Select(_ => new BootstrapLabel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(4, 6, 4, 6) }).ToArray();
    SecurityWorkspaceViewModel? _viewModel;
    int? _renderedDirectUserId;
    int? _renderedMembershipUserId;
    int[] _renderedMembershipRoleIds = Array.Empty<int>();
    bool _rendering;

    public SecurityUserDetailView()
    {
        Dock = DockStyle.Fill;
        foreach (var title in new[] { "Thông tin", "Vai trò", "Quyền hiệu lực", "Quyền riêng" }) Tabs.TabPages.Add(title);
        var profileSurface = new BootstrapPanel { Dock = DockStyle.Fill, Padding = new Padding(12), SurfaceRole = BootstrapSurfaceRole.Surface, AutoScroll = true };
        var profileFields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        profileFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        profileFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var captions = new[] { "Tên hiển thị", "Tên đăng nhập", "Email", "Trạng thái" };
        for (var index = 0; index < captions.Length; index++)
        {
            profileFields.Controls.Add(new BootstrapLabel { Text = captions[index], AutoSize = true, Padding = new Padding(4, 6, 12, 6), TextRole = BootstrapTextRole.Muted }, 0, index);
            _profileValues[index].AccessibleName = captions[index];
            profileFields.Controls.Add(_profileValues[index], 1, index);
        }
        profileSurface.Controls.Add(profileFields);
        Tabs.TabPages[0].Controls.Add(profileSurface);
        Tabs.TabPages[0].Controls.Add(_profile);
        Tabs.TabPages[1].Controls.Add(Memberships);
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
        filters.Controls.AddRange(new Control[] { Search, Module, Result, RefreshButton });
        Module.Items.Add("Tất cả phân hệ");
        Result.Items.AddRange(new object[] { "Tất cả kết quả", "Cho phép", "Từ chối", "Chưa được cấp", "Chưa xác định" });
        Module.SelectedIndex = Result.SelectedIndex = 0;
        foreach (var name in new[] { "Quyền", "Kết quả", "Nguồn quyết định" }) Effective.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
        Effective.Columns[0].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        Tabs.TabPages[2].Controls.Add(Effective);
        Tabs.TabPages[2].Controls.Add(Explanation);
        var effectiveActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 35, WrapContents = false };
        effectiveActions.Controls.Add(OpenDirectPermission);
        Tabs.TabPages[2].Controls.Add(effectiveActions);
        Tabs.TabPages[2].Controls.Add(filters);
        Tabs.TabPages[2].Controls.Add(new BootstrapLabel { Text = "Theo dữ liệu phân quyền đã tải", TextRole = BootstrapTextRole.Muted, Dock = DockStyle.Top, Height = 24 });
        Tabs.TabPages[3].Controls.Add(DirectDecisions);
        Controls.Add(Tabs);
        Controls.Add(ActionFeedback);
        var identity = new BootstrapPanel { Dock = DockStyle.Top, Height = 115, Padding = new Padding(10, 5, 10, 4), SurfaceRole = BootstrapSurfaceRole.Surface };
        identity.Controls.Add(HeaderActions);
        identity.Controls.Add(HeaderState);
        identity.Controls.Add(HeaderMetadata);
        identity.Controls.Add(Header);
        _ = new SecurityHeaderLayout(identity, HeaderActions);
        Controls.Add(identity);
        Effective.SelectionChanged += (_, _) => Explain();
        Effective.CurrentCellChanged += (_, _) => Explain();
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
        var values = user == null ? new[] { "Chưa chọn người dùng", "—", "—", "—" } :
            new[] { user.DisplayName, user.Username, string.IsNullOrWhiteSpace(user.Email) ? "—" : user.Email, user.IsActive ? "Đang hoạt động" : "Ngừng hoạt động" };
        for (var index = 0; index < values.Length; index++) _profileValues[index].Text = values[index];
        Tabs.TabPages[1].Text = "Vai trò (" + (user?.RoleIds.Count.ToString() ?? "—") + ")";
        Tabs.TabPages[3].Text = "Quyền riêng (" + (user?.DirectDecisions.Count.ToString() ?? "—") + ")";
        var retainMembership = _renderedMembershipUserId == user?.UserId &&
            viewModel.Users.IsCurrent(viewModel.OwnerVersion) && viewModel.Roles.IsCurrent(viewModel.OwnerVersion);
        var selectedMembershipIndex = Memberships.SelectedIndex;
        var selectedRoleId = retainMembership && selectedMembershipIndex >= 0 && selectedMembershipIndex < _renderedMembershipRoleIds.Length
            ? _renderedMembershipRoleIds[selectedMembershipIndex] : (int?)null;
        var firstVisibleMembership = retainMembership ? Memberships.TopIndex : 0;
        var membershipRows = new System.Collections.Generic.List<object>();
        if (user != null)
        {
            foreach (var id in user.RoleIds)
            {
                var role = viewModel.Roles.Items.FirstOrDefault(item => item.RoleId == id);
                membershipRows.Add(role == null ? "#" + id + " — Chi tiết không khả dụng" : role.Name + (role.IsActive ? "" : " — Vai trò ngừng hoạt động — không cấp quyền"));
            }
        }
        Memberships.Rebind(membershipRows.ToArray(), retainMembership && _renderedMembershipRoleIds.SequenceEqual(user?.RoleIds ?? Array.Empty<int>()));
        if (selectedRoleId.HasValue && user != null)
        {
            var index = user.RoleIds.ToList().IndexOf(selectedRoleId.Value);
            if (index >= 0) Memberships.SelectedIndex = index;
        }
        if (Memberships.Items.Count > 0)
            Memberships.TopIndex = Math.Min(firstVisibleMembership, Memberships.Items.Count - 1);
        _renderedMembershipUserId = user?.UserId;
        _renderedMembershipRoleIds = user?.RoleIds.ToArray() ?? Array.Empty<int>();
        _rendering = true;
        var selectedModule = Module.SelectedItem as string;
        Module.Items.Clear();
        Module.Items.Add("Tất cả phân hệ");
        foreach (var module in viewModel.Catalog.Items.Select(key => key.Module).Distinct().OrderBy(value => value)) Module.Items.Add(module);
        Module.SelectedIndex = selectedModule != null && Module.Items.Contains(selectedModule) ? Module.Items.IndexOf(selectedModule) : 0;
        _rendering = false;
        Filter();
        var selectedDirectKey = _renderedDirectUserId == user?.UserId
            ? (DirectDecisions.SelectedItem as DirectDecisionItem)?.Key : null;
        var firstVisibleDirect = _renderedDirectUserId == user?.UserId ? DirectDecisions.TopIndex : 0;
        _rendering = true;
        try
        {
            var directRows = new System.Collections.Generic.List<object>();
            if (user != null)
                foreach (var decision in user.DirectDecisions)
                {
                    var key = viewModel.Catalog.Items.FirstOrDefault(item => item.Key == decision.PermissionKey);
                    var label = key == null ? decision.PermissionKey : SecurityPresentation.Name(key) + " · " + decision.PermissionKey;
                    var history = !viewModel.Catalog.IsCurrent(viewModel.OwnerVersion) ? " · Danh mục chưa khả dụng" : key == null ? " · Không còn trong danh mục" : key.Status == "Active" ? "" : " · Ngừng dùng";
                    directRows.Add(new DirectDecisionItem(decision.PermissionKey, label + " · " + (decision.Decision == true ? "Cho phép" : decision.Decision == false ? "Từ chối" : "Theo vai trò") + history, key?.Status == "Active"));
                }
            DirectDecisions.Rebind(directRows.ToArray(), _renderedDirectUserId == user?.UserId);
            if (selectedDirectKey != null)
            {
                var selectedIndex = DirectDecisions.Items.Cast<ListViewItem>().ToList()
                    .FindIndex(item => ((DirectDecisionItem)item.Tag).Key == selectedDirectKey);
                if (selectedIndex >= 0) DirectDecisions.SelectedIndex = selectedIndex;
            }
            if (DirectDecisions.Items.Count > 0)
                DirectDecisions.TopIndex = Math.Min(firstVisibleDirect, DirectDecisions.Items.Count - 1);
            _renderedDirectUserId = user?.UserId;
        }
        finally { _rendering = false; }
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
            if (Search.Text.Length > 0 && (key.Key + " " + SecurityPresentation.Name(key) + " " + key.Description).IndexOf(Search.Text, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
            var explanation = SecurityPermissionProjection.Evaluate(_viewModel.Snapshot, key.Key);
            var result = ResultText(explanation.Result);
            if (Result.SelectedIndex > 0 && !result.EndsWith(Result.Text, StringComparison.Ordinal)) continue;
            var index = Effective.Rows.Add(SecurityPresentation.Name(key) + Environment.NewLine + key.Key, result, SecurityPresentation.Source(explanation));
            Effective.Rows[index].Height = Math.Max(BusinessCompactAppearance.Scale(Effective, 45), TextRenderer.MeasureText("A" + Environment.NewLine + "A", Effective.Font).Height + 8);
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

// App-owned Security row content only. Selection, scroll, focus and rendering remain native/toolkit-owned.
public sealed class SecurityDetailListView : BootstrapListView
{
    public SecurityDetailListView()
    {
        Dock = DockStyle.Fill;
        View = View.Details;
        HeaderStyle = ColumnHeaderStyle.None;
        FullRowSelect = true;
        MultiSelect = false;
        HideSelection = false;
        ShowItemToolTips = true;
        BorderStyle = BorderStyle.None;
        Columns.Add("Thông tin");
        SizeChanged += (_, _) => FitColumn();
        DpiChangedAfterParent += (_, _) => FitColumn();
    }

    internal void Rebind(object[] values, bool sameOwner)
    {
        // Opening a direct draft can synchronously render inside native SelectedIndexChanged.
        // Keep unchanged native items alive until that selection message has completed.
        if (sameOwner && values.Length == Items.Count && values.Where((value, index) => value.ToString() != Items[index].Text).Any() == false) return;
        BeginUpdate();
        try
        {
            Items.Clear();
            AccessibleDescription = "";
            foreach (var value in values)
            {
                var text = value.ToString() ?? "";
                Items.Add(new ListViewItem(text) { Tag = value, ToolTipText = text });
            }
            FitColumn();
        }
        finally { EndUpdate(); }
    }

    void FitColumn()
    {
        if (Columns.Count > 0) Columns[0].Width = Math.Max(1, ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
    }

    public int SelectedIndex
    {
        get => SelectedIndices.Count == 0 ? -1 : SelectedIndices[0];
        set
        {
            if (value == SelectedIndex) return;
            foreach (var item in SelectedItems.Cast<ListViewItem>().ToArray()) item.Selected = false;
            if (value < 0 || value >= Items.Count) return;
            Items[value].Selected = true;
            Items[value].Focused = true;
        }
    }

    public object? SelectedItem => SelectedItems.Count == 0 ? null : SelectedItems[0].Tag;
    public int TopIndex
    {
        get => TopItem?.Index ?? 0;
        set { if (Items.Count > 0) TopItem = Items[Math.Max(0, Math.Min(value, Items.Count - 1))]; }
    }
    // A conservative complete-row budget is also valid while native items are being rebuilt.
    public int ItemHeight => Math.Max(Font.Height + 8, BusinessCompactAppearance.Scale(this, 24));

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        AccessibleDescription = SelectedItem?.ToString() ?? "";
        base.OnSelectedIndexChanged(e);
    }
}
