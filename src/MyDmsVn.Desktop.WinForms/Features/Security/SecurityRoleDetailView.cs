using System;
using System.Linq;
using System.Windows.Forms;
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
    public FlowLayoutPanel HeaderActions { get; } = new() { Dock = DockStyle.Top, Height = 42, AutoScroll = true, WrapContents = false };
    public Panel GrantArea { get; } = new() { Dock = DockStyle.Fill };
    public Label HistoryTitle { get; } = new() { Dock = DockStyle.Bottom, Height = 22, Text = "Quyền cũ / không còn trong danh mục — chỉ xem" };
    public TabControl Tabs { get; } = new()
    {
        Dock = DockStyle.Fill
    };
    public ListBox Permissions { get; } = new()
    {
        Dock = DockStyle.Bottom,
        Height = 64,
        AccessibleName = "Quyền cũ hoặc không còn trong danh mục"
    };
    public ListBox Members { get; } = new()
    {
        Dock = DockStyle.Fill,
        DisplayMember = "Text"
    };
    public Button OpenUser { get; } = new()
    {
        Dock = DockStyle.Bottom,
        Text = "Mở người dùng",
        Height = 34
    };

    public event Action<int>? OpenUserRequested;
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
        var identity = new Panel { Dock = DockStyle.Top, Height = 102, Padding = new Padding(10, 5, 10, 4) };
        identity.Controls.Add(HeaderActions);
        identity.Controls.Add(HeaderState);
        identity.Controls.Add(Header);
        Controls.Add(identity);
        OpenUser.Click += (_, __) =>
        {
            if (Members.SelectedItem is SecurityMemberItem member)
                OpenUserRequested?.Invoke(member.Id);
        };
    }

    public void Render(SecurityWorkspaceViewModel vm)
    {
        var role = vm.SelectedRole;
        Header.Text = role == null ? "Chọn vai trò" : role.Name;
        HeaderState.Text = role == null ? "" : role.IsActive ? "● Đang hoạt động" : "● Ngừng hoạt động";
        Tabs.TabPages[1].Text = "Thành viên (" + (role?.MemberUserIds.Count.ToString() ?? "—") + ")";
        Permissions.Items.Clear();
        Members.Items.Clear();
        if (role != null)
        {
            var catalogReady = vm.Catalog.IsCurrent(vm.OwnerVersion);
            HistoryTitle.Text = catalogReady ? "Quyền cũ / không còn trong danh mục — chỉ xem" : "Quyền đã gán — danh mục chưa khả dụng";
            foreach (var key in role.PermissionKeys.Where(key => !catalogReady || !vm.Catalog.Items.Any(item => item.Key == key && item.Status == "Active")))
                Permissions.Items.Add(key + " · " + (!catalogReady ? "Chưa xác định vòng đời" : vm.Catalog.Items.Any(item => item.Key == key) ? "Ngừng dùng" : "Không còn trong danh mục"));
            foreach (var id in role.MemberUserIds)
            {
                var user = vm.Users.Items.FirstOrDefault(u => u.UserId == id);
                Members.Items.Add(new SecurityMemberItem(id, user == null ? "#" + id + " — Chi tiết không khả dụng" : user.DisplayName));
            }
        }

        OpenUser.Enabled = vm.Users.IsCurrent(vm.OwnerVersion) && role != null;
        Permissions.Visible = HistoryTitle.Visible = Permissions.Items.Count > 0;
    }

    sealed class SecurityMemberItem
    {
        public SecurityMemberItem(int id, string text)
        {
            Id = id;
            Text = text;
        }

        public int Id { get; }
        public string Text { get; }
    }
}
