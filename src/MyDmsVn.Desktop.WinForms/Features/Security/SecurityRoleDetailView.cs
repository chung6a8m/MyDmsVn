using System;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Desktop.Application.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;
public sealed class SecurityRoleDetailView : UserControl
{
    public Label Header { get; } = new()
    {
        Dock = DockStyle.Top,
        Height = 29,
        AutoEllipsis = true
    };
    public Label HeaderState { get; } = new() { Dock = DockStyle.Top, Height = 25, AutoEllipsis = true };
    public FlowLayoutPanel HeaderActions { get; } = new() { Dock = DockStyle.Top, Height = 32, AutoScroll = false, WrapContents = true };
    public Label ActionFeedback { get; } = new() { Dock = DockStyle.Top, Height = 26, Visible = false, AutoEllipsis = true,
        Padding = new Padding(8, 4, 8, 0), AccessibleName = "Thông báo đổi trạng thái" };
    public Panel GrantArea { get; } = new() { Dock = DockStyle.Fill, AutoScroll = true };
    public Label HistoryTitle { get; } = new BootstrapLabel { Dock = DockStyle.Bottom, Height = 22, Text = "Quyền cũ / không còn trong danh mục — chỉ xem", TextRole = BootstrapTextRole.Muted };
    public TabControl Tabs { get; } = new BusinessCompactWorkspaceTabs
    {
        Dock = DockStyle.Fill
    };
    public SecurityDetailListView Permissions { get; } = new()
    {
        Dock = DockStyle.Bottom,
        Height = 30,
        AccessibleName = "Quyền cũ hoặc không còn trong danh mục"
    };
    public SecurityDetailListView Members { get; } = new()
    {
        Dock = DockStyle.Fill,
        AccessibleName = "Thành viên của vai trò — chỉ xem"
    };
    public Button OpenUser { get; } = new BootstrapButton
    {
        Dock = DockStyle.Bottom,
        Text = "Mở người dùng",
        Height = 34,
        Variant = BootstrapVariant.Secondary,
        Outline = true,
        AccessibleName = "Mở người dùng đã chọn"
    };

    public event Action<int>? OpenUserRequested;
    int? _renderedRoleId;
    int[] _renderedMemberIds = Array.Empty<int>();
    public SecurityRoleDetailView()
    {
        Dock = DockStyle.Fill;
        Tabs.TabPages.Add("Quyền của vai trò");
        Tabs.TabPages.Add("Thành viên");
        Tabs.TabPages[0].Controls.Add(GrantArea);
        Tabs.TabPages[0].Controls.Add(HistoryTitle);
        Tabs.TabPages[0].Controls.Add(Permissions);
        Tabs.TabPages[1].Controls.Add(Members);
        Tabs.TabPages[1].Controls.Add(OpenUser);
        Controls.Add(Tabs);
        Controls.Add(ActionFeedback);
        var identity = new Panel { Dock = DockStyle.Top, Height = 95, Padding = new Padding(10, 5, 10, 4) };
        identity.Controls.Add(HeaderActions);
        identity.Controls.Add(HeaderState);
        identity.Controls.Add(Header);
        _ = new SecurityHeaderLayout(identity, HeaderActions);
        Controls.Add(identity);
        OpenUser.Click += (_, __) =>
        {
            if (Members.SelectedItem is SecurityMemberItem { CanOpen: true } member)
                OpenUserRequested?.Invoke(member.Id);
        };
        Members.SelectedIndexChanged += (_, _) =>
            OpenUser.Enabled = Members.SelectedItem is SecurityMemberItem { CanOpen: true };
    }

    public void Render(SecurityWorkspaceViewModel vm)
    {
        var role = vm.SelectedRole;
        var retainMember = _renderedRoleId == role?.RoleId &&
            vm.Roles.IsCurrent(vm.OwnerVersion) && vm.Users.IsCurrent(vm.OwnerVersion);
        var selectedMemberId = retainMember ? (Members.SelectedItem as SecurityMemberItem)?.Id : null;
        var firstVisibleMember = retainMember ? Members.TopIndex : 0;
        Header.Text = role == null ? "Chọn vai trò" : role.Name;
        HeaderState.Text = role == null ? "" : role.IsActive ? "● Đang hoạt động" : "● Ngừng hoạt động";
        Tabs.TabPages[1].Text = "Thành viên (" + (role?.MemberUserIds.Count.ToString() ?? "—") + ")";
        var historyRows = new System.Collections.Generic.List<object>();
        var memberRows = new System.Collections.Generic.List<object>();
        if (role != null)
        {
            var catalogReady = vm.Catalog.IsCurrent(vm.OwnerVersion);
            HistoryTitle.Text = catalogReady ? "Quyền cũ / không còn trong danh mục — chỉ xem" : "Quyền đã gán — danh mục chưa khả dụng";
            foreach (var key in role.PermissionKeys.Where(key => !catalogReady || !vm.Catalog.Items.Any(item => item.Key == key && item.Status == "Active")))
                historyRows.Add(key + " · " + (!catalogReady ? "Chưa xác định vòng đời" : vm.Catalog.Items.Any(item => item.Key == key) ? "Ngừng dùng" : "Không còn trong danh mục"));
            foreach (var id in role.MemberUserIds)
            {
                var user = vm.Users.IsCurrent(vm.OwnerVersion) ? vm.Users.Items.FirstOrDefault(u => u.UserId == id) : null;
                memberRows.Add(new SecurityMemberItem(id, user == null ? "#" + id + " — Chi tiết không khả dụng" :
                    user.DisplayName + " · @" + user.Username + " · " + (user.IsActive ? "Đang hoạt động" : "Ngừng hoạt động"),
                    user != null && vm.Users.IsCurrent(vm.OwnerVersion)));
            }
        }
        Permissions.Rebind(historyRows.ToArray(), _renderedRoleId == role?.RoleId);
        Members.Rebind(memberRows.ToArray(), retainMember && _renderedMemberIds.SequenceEqual(role?.MemberUserIds ?? Array.Empty<int>()));

        if (selectedMemberId.HasValue)
        {
            var selectedIndex = Members.Items.Cast<ListViewItem>().Select(item => (SecurityMemberItem)item.Tag).ToList()
                .FindIndex(item => item.Id == selectedMemberId.Value && item.CanOpen);
            if (selectedIndex >= 0) Members.SelectedIndex = selectedIndex;
        }
        if (Members.Items.Count > 0)
            Members.TopIndex = Math.Min(firstVisibleMember, Members.Items.Count - 1);
        _renderedRoleId = role?.RoleId;
        _renderedMemberIds = role?.MemberUserIds.ToArray() ?? Array.Empty<int>();

        OpenUser.Enabled = Members.SelectedItem is SecurityMemberItem { CanOpen: true };
        Permissions.Visible = HistoryTitle.Visible = Permissions.Items.Count > 0;
    }

    sealed class SecurityMemberItem
    {
        public SecurityMemberItem(int id, string text, bool canOpen)
        {
            Id = id;
            Text = text;
            CanOpen = canOpen;
        }

        public int Id { get; }
        public string Text { get; }
        public bool CanOpen { get; }
        public override string ToString() => Text;
    }
}
