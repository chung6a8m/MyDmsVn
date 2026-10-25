using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

internal sealed class SecurityWorkspaceEditorBindings
{
    readonly SecurityWorkspaceControl _control;
    readonly CheckedListBox _grants = new() { Dock = DockStyle.Fill, CheckOnClick = true, AccessibleName = "Cấp quyền cho vai trò" };
    readonly ComboBox _directKey = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Quyền riêng" };
    readonly RadioButton _inherit = new() { Text = "Theo vai trò", AutoSize = true };
    readonly RadioButton _allow = new() { Text = "Cho phép", AutoSize = true };
    readonly RadioButton _deny = new() { Text = "Từ chối", AutoSize = true };
    readonly Label _status = new() { AutoSize = true };
    bool _rendering;
    SecurityWorkspaceViewModel Vm => _control.ViewModel;

    internal SecurityWorkspaceEditorBindings(SecurityWorkspaceControl control)
    {
        _control = control;
        var roleCommands = Commands();
        Add(roleCommands, "Tạo vai trò…", () => IdentityDialogAsync(false, true));
        Add(roleCommands, "Sửa tên…", () => IdentityDialogAsync(false, false));
        Add(roleCommands, "Đổi trạng thái…", () => ActivationAsync(false));
        Add(roleCommands, "Sửa quyền", () => { Vm.BeginRolePermissions(); return Task.CompletedTask; });
        Add(roleCommands, "Lưu quyền vai trò", () => Vm.SaveEditorAsync(default));
        Add(roleCommands, "Hủy thay đổi", () => { Vm.DiscardEditor(); return Task.CompletedTask; });
        control.RoleDetail.Tabs.TabPages[0].Controls.Add(_grants);
        _grants.BringToFront();
        control.RoleDetail.Tabs.TabPages[0].Controls.Add(roleCommands);
        _grants.ItemCheck += (_, e) =>
        {
            if (_rendering) return;
            if (Vm.RoleDraft == null)
            {
                e.NewValue = e.CurrentValue;
                return;
            }
            if (_grants.Items[e.Index] is not string key) return;
            // ItemCheck precedes the native list update; render only after that update.
            _control.BeginInvoke(new Action(() => Vm.SetRoleGranted(key, e.NewValue == CheckState.Checked)));
        };
        var direct = Commands();
        direct.Height = 115;
        direct.Controls.AddRange(new Control[] { _directKey, _inherit, _allow, _deny });
        Add(direct, "Lưu quyền này", () => Vm.SaveEditorAsync(default));
        Add(direct, "Hủy", () => { Vm.DiscardEditor(); return Task.CompletedTask; });
        control.UserDetail.Tabs.TabPages[3].Controls.Add(direct);
        _directKey.SelectedIndexChanged += async (_, _) =>
        {
            if (_rendering || _directKey.SelectedItem is not string key) return;
            if (await AdmitAsync()) Vm.BeginDirectPermission(key);
            Render();
        };
        foreach (var radio in new[] { _inherit, _allow, _deny }) radio.CheckedChanged += (_, _) =>
        {
            if (!_rendering && radio.Checked) Vm.SetDirectDecision(_inherit.Checked ? null : _allow.Checked);
        };
        var membership = Commands();
        Add(membership, "Gán vai trò…", async () =>
        {
            if (!await AdmitAsync() || Vm.SelectedUser == null) return;
            using var dialog = new Form { Text = "Gán vai trò — " + Vm.SelectedUser.DisplayName, Width = 450, Height = 180, StartPosition = FormStartPosition.CenterParent };
            var roles = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
            foreach (var role in Vm.Roles.Items) roles.Items.Add(role);
            var ok = new Button { Text = "Xác nhận gán", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };
            dialog.Controls.Add(roles); dialog.Controls.Add(ok);
            if (dialog.ShowDialog(control) == DialogResult.OK && roles.SelectedItem is SecurityRoleDto selected) await Vm.SetMembershipAsync(selected.RoleId, true, default);
        });
        Add(membership, "Gỡ vai trò", async () =>
        {
            var user = Vm.SelectedUser;
            var index = control.UserDetail.Memberships.SelectedIndex;
            if (user == null || index < 0 || index >= user.RoleIds.Count || !await AdmitAsync()) return;
            var roleId = user.RoleIds[index];
            var name = Vm.Roles.Items.FirstOrDefault(r => r.RoleId == roleId)?.Name ?? "#" + roleId;
            if (MessageBox.Show(control, "Gỡ " + name + " khỏi " + user.DisplayName + "?", "Gỡ vai trò", MessageBoxButtons.OKCancel) == DialogResult.OK) await Vm.SetMembershipAsync(roleId, false, default);
        });
        control.UserDetail.Tabs.TabPages[1].Controls.Add(membership);
        var profile = Commands();
        Add(profile, "Tạo người dùng…", () => IdentityDialogAsync(true, true));
        Add(profile, "Sửa thông tin…", () => IdentityDialogAsync(true, false));
        Add(profile, "Đặt lại mật khẩu…", () => PasswordAsync());
        Add(profile, "Đổi trạng thái…", () => ActivationAsync(true));
        control.UserDetail.Tabs.TabPages[0].Controls.Add(profile);
        var footer = Commands();
        footer.Controls.Add(_status);
        Add(footer, "Tải lại để đối chiếu", async () => { await Vm.ReloadForComparisonAsync(default); });
        Add(footer, "Đã đối chiếu — lập diff mới", () =>
        {
            if (control.RecoveryConfirmationOverride?.Invoke() ??
                MessageBox.Show(control, "Giữ ý định và tính lại thay đổi từ dữ liệu vừa tải? Không xác nhận kết quả lệnh chưa rõ.",
                    "Đối chiếu", MessageBoxButtons.OKCancel) == DialogResult.OK)
                Vm.ReviewRecoveryAndRebase();
            return Task.CompletedTask;
        });
        control.Controls.Add(footer); footer.Dock = DockStyle.Bottom;
        GuardTabs(control.Tabs);
        GuardTabs(control.UserDetail.Tabs);
        GuardTabs(control.RoleDetail.Tabs);
        Vm.Changed += Changed;
        control.Disposed += (_, _) => Vm.Changed -= Changed;
        Render();
    }

    void Changed(object? sender, EventArgs e) => Render();
    void GuardTabs(TabControl tabs)
    {
        tabs.Selecting += async (_, e) =>
        {
            if (Vm.CanNavigate) return;
            e.Cancel = true;
            var destination = e.TabPage;
            if (await AdmitAsync() && destination != null && !tabs.IsDisposed) tabs.SelectedTab = destination;
        };
    }
    void Render()
    {
        _rendering = true;
        try
        {
            _status.Text = Vm.EditingStatus + (Vm.HasDirtyEditor ? " · Có thay đổi chưa lưu" : "") + (Vm.RoleDraft != null ? " · " + Vm.RoleDraft.Changes.Count + " thay đổi" : "");
            _grants.Items.Clear();
            var role = Vm.SelectedRole;
            foreach (var key in Vm.Catalog.Items.Where(k => k.Status == "Active")) _grants.Items.Add(key.Key,
                Vm.RoleDraft != null && Vm.RoleDraft.RoleId == role?.RoleId ? Vm.RoleDraft.DesiredKeys.Contains(key.Key) : role?.PermissionKeys.Contains(key.Key) == true);
            _grants.Enabled = role != null && Vm.Catalog.IsCurrent(Vm.OwnerVersion) && !Vm.IsBusy && !Vm.RequiresRecovery;
            _grants.AccessibleDescription = Vm.RoleDraft == null ? "Chỉ xem. Chọn Sửa quyền để thay đổi." : "Đang sửa quyền của vai trò.";
            var selected = Vm.DirectPermissionKey;
            _directKey.Items.Clear();
            foreach (var key in Vm.Catalog.Items.Where(k => k.Status == "Active")) _directKey.Items.Add(key.Key);
            _directKey.SelectedItem = selected;
            _directKey.Enabled = !Vm.IsBusy && !Vm.RequiresRecovery;
            _inherit.Checked = Vm.DirectDecision == null;
            _allow.Checked = Vm.DirectDecision == true;
            _deny.Checked = Vm.DirectDecision == false;
            foreach (var radio in new[] { _inherit, _allow, _deny }) radio.Enabled = selected != null && !Vm.IsBusy && !Vm.RequiresRecovery;
        }
        finally { _rendering = false; }
    }

    async Task<bool> AdmitAsync()
    {
        if (Vm.CanNavigate) return true;
        if (Vm.IsBusy || Vm.RequiresRecovery) return false;
        var answer = MessageBox.Show(_control, "Lưu thay đổi trước khi tiếp tục?", "Thay đổi chưa lưu", MessageBoxButtons.YesNoCancel);
        return await Vm.ResolveDraftAsync(answer == DialogResult.Yes ? SecurityDraftResolution.Save : answer == DialogResult.No ? SecurityDraftResolution.Discard : SecurityDraftResolution.Stay, CancellationToken.None);
    }

    async Task IdentityDialogAsync(bool userTarget, bool create)
    {
        if (!await AdmitAsync()) return;
        var user = Vm.SelectedUser;
        var role = Vm.SelectedRole;
        if (!create && (userTarget ? user == null || user.IsProtectedAccount : role == null)) return;
        using var dialog = new Form { Text = create ? "Tạo " + (userTarget ? "người dùng" : "vai trò") : "Sửa — " + (userTarget ? user!.DisplayName : role!.Name), Width = 440, Height = userTarget ? 280 : 180, StartPosition = FormStartPosition.CenterParent };
        var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var name = new TextBox { Width = 380, AccessibleName = userTarget ? "Tên đăng nhập" : "Tên vai trò", Text = create ? "" : userTarget ? user!.Username : role!.Name };
        var display = new TextBox { Width = 380, AccessibleName = "Tên hiển thị", Text = create ? "" : user?.DisplayName ?? "" };
        var email = new TextBox { Width = 380, AccessibleName = "Email", Text = create ? "" : user?.Email ?? "" };
        var password = new TextBox { Width = 380, AccessibleName = "Mật khẩu tạm", UseSystemPasswordChar = true };
        fields.Controls.Add(name);
        if (userTarget) { fields.Controls.Add(display); fields.Controls.Add(email); if (create) fields.Controls.Add(password); }
        var validation = new Label { AutoSize = true, AccessibleName = "Lỗi thông tin" };
        fields.Controls.Add(validation);
        var save = new Button { Text = "Lưu thông tin", AutoSize = true };
        var cancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
        fields.Controls.Add(save); fields.Controls.Add(cancel); dialog.Controls.Add(fields);
        dialog.CancelButton = cancel;
        save.Click += async (_, _) =>
        {
            save.Enabled = false;
            try
            {
                TextBox? invalid = null;
                if (string.IsNullOrWhiteSpace(name.Text) || name.Text.Length > 100)
                {
                    invalid = name;
                    validation.Text = "Tên phải có từ 1 đến 100 ký tự.";
                }
                else if (userTarget && (string.IsNullOrWhiteSpace(display.Text) || display.Text.Length > 100))
                {
                    invalid = display;
                    validation.Text = "Tên hiển thị phải có từ 1 đến 100 ký tự.";
                }
                else if (userTarget && email.Text.Length > 254)
                {
                    invalid = email;
                    validation.Text = "Email không được vượt quá 254 ký tự.";
                }
                else if (userTarget && create && (password.Text.Length == 0 || Encoding.UTF8.GetByteCount(password.Text) > 72))
                {
                    invalid = password;
                    validation.Text = "Mật khẩu tạm phải có từ 1 đến 72 byte UTF-8.";
                }
                if (invalid != null)
                {
                    invalid.Focus();
                    return;
                }
                validation.Text = "";
                var saved = await Vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(userTarget ? SecurityAdministrationTargetDto.User : SecurityAdministrationTargetDto.Role,
                    create ? SecurityAdministrationOperationDto.Create : SecurityAdministrationOperationDto.Update,
                    create ? 0 : userTarget ? user!.UserId : role!.RoleId, name: name.Text, displayName: userTarget ? display.Text : null, email: userTarget ? email.Text : null,
                    password: create && userTarget ? password.Text : null, expectedRevision: create ? null : userTarget ? user!.AssignmentRevision : role!.AssignmentRevision), default);
                if (saved) dialog.Close();
            }
            finally { password.Clear(); save.Enabled = true; }
        };
        dialog.ShowDialog(_control);
        password.Clear();
    }

    async Task PasswordAsync()
    {
        if (!await AdmitAsync() || Vm.SelectedUser is not { IsProtectedAccount: false } user) return;
        using var dialog = new Form { Text = "Đặt lại mật khẩu — " + user.DisplayName, Width = 420, Height = 160, StartPosition = FormStartPosition.CenterParent };
        var password = new TextBox { Dock = DockStyle.Top, UseSystemPasswordChar = true, AccessibleName = "Mật khẩu mới" };
        var validation = new Label { Dock = DockStyle.Top, AccessibleName = "Lỗi mật khẩu" };
        var save = new Button { Text = "Xác nhận", Dock = DockStyle.Bottom };
        var cancel = new Button { Text = "Hủy", Dock = DockStyle.Bottom, DialogResult = DialogResult.Cancel };
        dialog.Controls.Add(password); dialog.Controls.Add(validation); dialog.Controls.Add(save); dialog.Controls.Add(cancel);
        dialog.CancelButton = cancel;
        save.Click += async (_, _) =>
        {
            save.Enabled = false;
            try
            {
                if (password.Text.Length == 0 || Encoding.UTF8.GetByteCount(password.Text) > 72)
                {
                    validation.Text = "Mật khẩu phải có từ 1 đến 72 byte UTF-8.";
                    password.Focus();
                    return;
                }
                validation.Text = "";
                if (await Vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetPassword, user.UserId, password: password.Text, expectedRevision: user.AssignmentRevision), default))
                    dialog.Close();
            }
            finally { password.Clear(); save.Enabled = true; }
        };
        try { dialog.ShowDialog(_control); }
        finally { password.Clear(); }
    }

    async Task ActivationAsync(bool userTarget)
    {
        if (!await AdmitAsync()) return;
        var user = Vm.SelectedUser; var role = Vm.SelectedRole;
        if (userTarget ? user == null || user.IsProtectedAccount : role == null) return;
        var name = userTarget ? user!.DisplayName : role!.Name;
        var active = userTarget ? user!.IsActive : role!.IsActive;
        if (MessageBox.Show(_control, (active ? "Ngừng hoạt động " : "Kích hoạt ") + name + (userTarget ? "?" : "? Các thành viên vẫn được giữ; quyền của vai trò chỉ có hiệu lực khi hoạt động."), "Đổi trạng thái", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
        await Vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(userTarget ? SecurityAdministrationTargetDto.User : SecurityAdministrationTargetDto.Role,
            SecurityAdministrationOperationDto.SetActive, userTarget ? user!.UserId : role!.RoleId, isActive: !active,
            expectedRevision: userTarget ? user!.AssignmentRevision : role!.AssignmentRevision), default);
    }

    static FlowLayoutPanel Commands() => new() { Dock = DockStyle.Top, Height = 70, AutoScroll = true, WrapContents = true };
    void Add(FlowLayoutPanel panel, string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true };
        panel.Controls.Add(button);
        button.Click += async (_, _) => await _control.TrackOperation(action());
    }
}
