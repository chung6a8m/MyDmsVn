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
        Height = 55,
        AutoEllipsis = true
    };
    public TabControl Tabs { get; } = new()
    {
        Dock = DockStyle.Fill
    };
    public ListBox Permissions { get; } = new()
    {
        Dock = DockStyle.Fill
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
        Tabs.TabPages[0].Controls.Add(Permissions);
        Tabs.TabPages[1].Controls.Add(Members);
        Tabs.TabPages[1].Controls.Add(OpenUser);
        Controls.Add(Tabs);
        Controls.Add(Header);
        OpenUser.Click += (_, __) =>
        {
            if (Members.SelectedItem is SecurityMemberItem member)
                OpenUserRequested?.Invoke(member.Id);
        };
    }

    public void Render(SecurityWorkspaceViewModel vm)
    {
        var role = vm.SelectedRole;
        Header.Text = role == null ? "Chọn vai trò" : role.Name + " · " + (role.IsActive ? "Đang hoạt động" : "Ngừng hoạt động");
        Tabs.TabPages[1].Text = "Thành viên (" + (role?.MemberUserIds.Count.ToString() ?? "—") + ")";
        Permissions.Items.Clear();
        Members.Items.Clear();
        if (role != null)
        {
            foreach (var key in role.PermissionKeys)
                Permissions.Items.Add(key);
            foreach (var id in role.MemberUserIds)
            {
                var user = vm.Users.Items.FirstOrDefault(u => u.UserId == id);
                Members.Items.Add(new SecurityMemberItem(id, user == null ? "#" + id + " — Chi tiết không khả dụng" : user.DisplayName));
            }
        }

        OpenUser.Enabled = vm.Users.IsCurrent(vm.OwnerVersion) && role != null;
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
