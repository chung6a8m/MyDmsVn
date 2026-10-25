using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Bootstrap5WinFormUI.Theme;

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

internal sealed class SecurityMasterListBox : ListBox
{
    internal SecurityMasterListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 58;
        IntegralHeight = false;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var theme = BootstrapThemeManager.CurrentTheme.Colors;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? theme.SurfaceSecondary : theme.Surface);
        e.Graphics.FillRectangle(background, e.Bounds);
        var item = Items[e.Index];
        var title = item is SecurityUserDto user ? user.DisplayName : item is SecurityRoleDto role ? role.Name : item.ToString() ?? "";
        var sub = item is SecurityUserDto u ? u.Username : item is SecurityRoleDto r ? r.MemberUserIds.Count + " thành viên" : "";
        var active = item is SecurityUserDto uu ? uu.IsActive : item is SecurityRoleDto rr && rr.IsActive;
        var status = active ? "Hoạt động" : "Ngừng hoạt động";
        var titleRect = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + 6, Math.Max(0, e.Bounds.Width - 20), 21);
        var subRect = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + 30, Math.Max(0, e.Bounds.Width - 145), 19);
        var badgeRect = new Rectangle(Math.Max(e.Bounds.Left + 10, e.Bounds.Right - 138), e.Bounds.Top + 30, 128, 19);
        using var badgeBackground = new SolidBrush(theme.SurfaceSecondary);
        using var badgeBorder = new Pen(theme.MutedText);
        e.Graphics.FillRectangle(badgeBackground, badgeRect);
        e.Graphics.DrawRectangle(badgeBorder, badgeRect);
        TextRenderer.DrawText(e.Graphics, title, Font, titleRect, theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, sub, Font, subRect, theme.MutedText, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, status, Font, badgeRect, theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }
}
