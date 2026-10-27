using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

internal static class SecurityPresentation
{
    internal static string Name(PermissionCatalogItemDto key) => key.Key switch
    {
        "Sales.Receipts.Read" => "Xem phiếu bán hàng",
        "Sales.Receipts.Write" => "Sửa phiếu bán hàng",
        "Sales.Receipts.Post" => "Ghi sổ bán hàng",
        _ => string.IsNullOrWhiteSpace(key.Description) ? key.Key : key.Description
    };

    internal static string Dataset(SecurityDatasetState state) => state switch
    {
        SecurityDatasetState.Ready => "Đã tải",
        SecurityDatasetState.Loading => "Đang tải…",
        SecurityDatasetState.Forbidden => "Không có quyền xem",
        SecurityDatasetState.Failed => "Tải dữ liệu thất bại",
        _ => "Chưa tải"
    };

    internal static string Result(SecurityPermissionResult result) => result switch
    {
        SecurityPermissionResult.Allowed => "✓ Cho phép",
        SecurityPermissionResult.Denied => "✕ Từ chối",
        SecurityPermissionResult.Unassigned => "— Chưa được cấp",
        _ => "? Chưa xác định"
    };

    internal static string Source(SecurityPermissionExplanation explanation) => explanation.Reason switch
    {
        SecurityPermissionReason.DirectAllow => "Quyền riêng: Cho phép",
        SecurityPermissionReason.DirectDeny => "Quyền riêng: Từ chối",
        SecurityPermissionReason.ActiveRoleGrant => "Vai trò: " + string.Join(", ", explanation.RoleSources.Select(role => role.Name)),
        SecurityPermissionReason.InactiveUser => "Tài khoản ngừng hoạt động",
        SecurityPermissionReason.InactivePermission => "Quyền đã ngừng dùng",
        SecurityPermissionReason.UnknownPermission => "Mã quyền không còn trong danh mục",
        SecurityPermissionReason.CatalogUnavailable => "Danh mục quyền chưa khả dụng",
        SecurityPermissionReason.UserUnavailable => "Thông tin người dùng chưa khả dụng",
        SecurityPermissionReason.RoleDataUnavailable => "Dữ liệu vai trò chưa đầy đủ",
        SecurityPermissionReason.ProtectedAccountUnverified => "Tài khoản được bảo vệ; chưa xác minh quyền thực tế",
        _ => "Chưa có quyền riêng hoặc vai trò cấp quyền"
    };
}

// Business content and identity only; native selection, rendering and theme lifetime stay in WinFormsKit.
internal sealed class SecurityMasterListView : MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapListView
{
    readonly ToolTip _itemTip = new();
    string _tipText = "";

    internal SecurityMasterListView()
    {
        Dock = DockStyle.Fill;
        View = View.Tile;
        MultiSelect = false;
        HideSelection = false;
        FullRowSelect = true;
        BorderStyle = BorderStyle.None;
        // Tile projects the primary label plus ordinal subitems 1..Columns.Count at this pin.
        Columns.Add("Thông tin");
        SizeChanged += (_, _) => UpdateTileSize();
        DpiChangedAfterParent += (_, _) => UpdateTileSize();
        UpdateTileSize();
    }

    void UpdateTileSize() => TileSize = new Size(Math.Max(1, ClientSize.Width - SystemInformation.VerticalScrollBarWidth),
        BusinessCompactAppearance.Scale(this, 64));

    internal void Rebind(System.Collections.Generic.IEnumerable<object> source, object? selected)
    {
        var values = source.ToArray();
        var sameRows = values.Length == Items.Count &&
            values.Select((value, index) => ReferenceEquals(value, Items[index].Tag)).All(same => same);
        if (!sameRows)
        {
            ClearHoverDescription();
            var focused = FocusedItem?.Tag;
            var visible = Items.Cast<ListViewItem>().FirstOrDefault(item => item.Bounds.Bottom > 0)?.Tag;
            BeginUpdate();
            try
            {
                Items.Clear();
                foreach (var value in values)
                {
                    var title = value is SecurityUserDto user ? user.DisplayName : ((SecurityRoleDto)value).Name;
                    var row = new ListViewItem(new[] { title, Metadata(value) }) { Tag = value, UseItemStyleForSubItems = false };
                    Items.Add(row);
                }
                var anchor = Items.Cast<ListViewItem>().FirstOrDefault(item => SameIdentity(item.Tag, visible));
                anchor?.EnsureVisible();
                var focus = Items.Cast<ListViewItem>().FirstOrDefault(item => SameIdentity(item.Tag, focused)) ??
                    Items.Cast<ListViewItem>().FirstOrDefault(item => SameIdentity(item.Tag, selected));
                if (focus != null) focus.Focused = true;
            }
            finally { EndUpdate(); }
        }
        foreach (ListViewItem item in Items)
        {
            var shouldSelect = SameIdentity(item.Tag, selected);
            if (item.Selected != shouldSelect) item.Selected = shouldSelect;
        }
        UpdateAccessibleIdentity();
    }

    void UpdateAccessibleIdentity() => AccessibleDescription = SelectedItems.Count == 1
        ? FullText(SelectedItems[0].Tag) : _tipText;

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        UpdateAccessibleIdentity();
    }

    static bool SameIdentity(object? left, object? right) =>
        left is SecurityUserDto user && right is SecurityUserDto otherUser ? user.UserId == otherUser.UserId :
        left is SecurityRoleDto role && right is SecurityRoleDto otherRole && role.RoleId == otherRole.RoleId;

    static string Metadata(object item) => item is SecurityUserDto user
        ? "@" + user.Username + " · " + (user.IsActive ? "Hoạt động" : "Ngừng hoạt động") +
            (user.IsProtectedAccount ? " · Được bảo vệ" : "")
        : item is SecurityRoleDto role
            ? role.MemberUserIds.Count + " thành viên · " + (role.IsActive ? "Hoạt động" : "Ngừng hoạt động") : "";

    internal static string FullText(object item) =>
        (item is SecurityUserDto user ? user.DisplayName : item is SecurityRoleDto role ? role.Name : "") + " · " + Metadata(item);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var item = GetItemAt(e.X, e.Y);
        var text = item == null ? "" : FullText(item.Tag);
        if (_tipText == text) return;
        _tipText = text;
        UpdateAccessibleIdentity();
        _itemTip.SetToolTip(this, text);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        ClearHoverDescription();
        UpdateAccessibleIdentity();
        base.OnMouseLeave(e);
    }

    internal void ClearHoverDescription()
    {
        _tipText = "";
        AccessibleDescription = "";
        _itemTip.Hide(this);
        _itemTip.SetToolTip(this, "");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { ClearHoverDescription(); _itemTip.Dispose(); }
        base.Dispose(disposing);
    }
}
