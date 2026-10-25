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
    readonly RoleGrantList _grants = new() { Dock = DockStyle.Fill, CheckOnClick = true, AccessibleName = "Cấp quyền cho vai trò" };
    readonly TextBox _grantSearch = new() { Width = 180, AccessibleName = "Tìm quyền vai trò" };
    readonly ComboBox _grantModule = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Nhóm quyền vai trò" };
    readonly CheckBox _grantedOnly = new() { Text = "Chỉ quyền đã cấp", AutoSize = true };
    readonly Label _grantSummary = new() { Dock = DockStyle.Bottom, Height = 26 };
    readonly ComboBox _directKey = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Quyền riêng" };
    readonly RadioButton _inherit = new() { Text = "Theo vai trò", AutoSize = true };
    readonly RadioButton _allow = new() { Text = "Cho phép", AutoSize = true };
    readonly RadioButton _deny = new() { Text = "Từ chối", AutoSize = true };
    readonly Label _status = new() { AutoSize = true };
    readonly Label _directPreview = new() { Dock = DockStyle.Bottom, Height = 58, AutoEllipsis = true };
    readonly FlowLayoutPanel _recovery = Commands();
    bool _rendering;
    SecurityWorkspaceViewModel Vm => _control.ViewModel;

    internal SecurityWorkspaceEditorBindings(SecurityWorkspaceControl control)
    {
        _control = control;
        var roleCommands = control.RoleDetail.HeaderActions;
        Add(roleCommands, "Tạo vai trò…", () => IdentityDialogAsync(false, true));
        Add(roleCommands, "Sửa tên…", () => IdentityDialogAsync(false, false));
        Add(roleCommands, "Đổi trạng thái…", () => ActivationAsync(false));
        Add(roleCommands, "Sửa quyền", () => { Vm.BeginRolePermissions(); return Task.CompletedTask; });
        var grantFilters = Commands();
        grantFilters.Controls.AddRange(new Control[] { _grantSearch, _grantModule, _grantedOnly });
        var grantActions = Commands();
        grantActions.Dock = DockStyle.Bottom;
        grantActions.Height = 42;
        Add(grantActions, "Hủy thay đổi", () => { Vm.DiscardEditor(); return Task.CompletedTask; });
        Add(grantActions, "Lưu quyền vai trò", () => Vm.SaveEditorAsync(default));
        control.RoleDetail.GrantArea.Controls.Add(_grants);
        control.RoleDetail.GrantArea.Controls.Add(_grantSummary);
        control.RoleDetail.GrantArea.Controls.Add(grantActions);
        control.RoleDetail.GrantArea.Controls.Add(grantFilters);
        _grantModule.Items.Add("Tất cả nhóm");
        _grantModule.SelectedIndex = 0;
        _grantSearch.TextChanged += (_, _) => { if (!_rendering) Render(); };
        _grantModule.SelectedIndexChanged += (_, _) => { if (!_rendering) Render(); };
        _grantedOnly.CheckedChanged += (_, _) => { if (!_rendering) Render(); };
        _grants.FormattingEnabled = true;
        _grants.Format += (_, e) =>
        {
            if (e.ListItem is not string key) return;
            var item = Vm.Catalog.Items.FirstOrDefault(k => k.Key == key);
            var pending = Vm.RoleDraft?.Changes.Any(change => change.PermissionKey == key) == true ? "  ·  Chưa lưu" : "";
            e.Value = item == null ? key : SecurityPresentation.Name(item) + "  ·  " + key + "  ·  " + item.Module + pending;
        };
        _grants.SpacePressed += () =>
        {
            if (Vm.RoleDraft != null && _grants.SelectedItem is string key)
                Vm.SetRoleGranted(key, !Vm.RoleDraft.DesiredKeys.Contains(key));
        };
        _grants.ItemCheck += (_, e) =>
        {
            if (_rendering) return;
            var draft = Vm.RoleDraft;
            if (draft == null)
            {
                e.NewValue = e.CurrentValue;
                return;
            }
            if (_grants.Items[e.Index] is not string key) return;
            var granted = e.NewValue == CheckState.Checked;
            // ItemCheck precedes the native list update; render only after that update.
            _control.BeginInvoke(new Action(() =>
            {
                if (!_control.IsDisposed && Vm.IsCurrent && ReferenceEquals(Vm.RoleDraft, draft) &&
                    Vm.SelectedRoleId == draft.RoleId && Vm.OwnerVersion == draft.SessionVersion)
                    Vm.SetRoleGranted(key, granted);
            }));
        };
        var direct = Commands();
        direct.Height = 115;
        direct.Controls.AddRange(new Control[] { _directKey, _inherit, _allow, _deny });
        control.UserDetail.Tabs.TabPages[3].Controls.Add(direct);
        var directActions = Commands();
        directActions.Dock = DockStyle.Bottom;
        directActions.Height = 42;
        Add(directActions, "Hủy", () => { Vm.DiscardEditor(); return Task.CompletedTask; });
        Add(directActions, "Lưu quyền này", () => Vm.SaveEditorAsync(default));
        control.UserDetail.Tabs.TabPages[3].Controls.Add(_directPreview);
        control.UserDetail.Tabs.TabPages[3].Controls.Add(directActions);
        control.UserDetail.DirectPermissionRequested += key => _ = control.TrackOperation(OpenDirectAsync(key));
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
        var profile = control.UserDetail.HeaderActions;
        Add(profile, "Tạo người dùng…", () => IdentityDialogAsync(true, true));
        Add(profile, "Sửa thông tin…", () => IdentityDialogAsync(true, false));
        Add(profile, "Đặt lại mật khẩu…", () => PasswordAsync());
        Add(profile, "Đổi trạng thái…", () => ActivationAsync(true));
        _recovery.Dock = DockStyle.Bottom;
        _recovery.Height = 54;
        _recovery.Controls.Add(_status);
        Add(_recovery, "Tải lại để đối chiếu", async () => { await Vm.ReloadForComparisonAsync(default); });
        Add(_recovery, "Đã đối chiếu — lập diff mới", () =>
        {
            if (control.RecoveryConfirmationOverride?.Invoke() ??
                MessageBox.Show(control, "Giữ ý định và tính lại thay đổi từ dữ liệu vừa tải? Không xác nhận kết quả lệnh chưa rõ.",
                    "Đối chiếu", MessageBoxButtons.OKCancel) == DialogResult.OK)
                Vm.ReviewRecoveryAndRebase();
            return Task.CompletedTask;
        });
        control.UserDetail.Controls.Add(_recovery);
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
            _status.Text = Vm.EditingStatus;
            var recoveryOwner = Vm.RecoveryTarget == SecurityAdministrationTargetDto.Role
                ? (Control)_control.RoleDetail : _control.UserDetail;
            if (_recovery.Parent != recoveryOwner)
            {
                recoveryOwner.Controls.Add(_recovery);
                _recovery.BringToFront();
            }
            _recovery.Visible = Vm.RequiresRecovery;
            _grantSummary.Text = Vm.RoleDraft == null ? "Chọn Sửa quyền để bắt đầu bản nháp" : Vm.RoleDraft.Changes.Count + " thay đổi chưa lưu trong toàn bộ danh mục";
            var selectedGrant = _grants.SelectedItem as string;
            var role = Vm.SelectedRole;
            var selectedModule = _grantModule.SelectedItem as string;
            var modules = Vm.Catalog.Items.Where(k => k.Status == "Active").Select(k => k.Module).Distinct().OrderBy(value => value).ToArray();
            _grantModule.Items.Clear();
            _grantModule.Items.Add("Tất cả nhóm");
            _grantModule.Items.AddRange(modules.Cast<object>().ToArray());
            _grantModule.SelectedIndex = selectedModule != null && _grantModule.Items.Contains(selectedModule) ? _grantModule.Items.IndexOf(selectedModule) : 0;
            var activeKeys = Vm.Catalog.Items.Where(k => k.Status == "Active")
                .Where(k => _grantModule.SelectedIndex == 0 || k.Module == _grantModule.Text)
                .Where(k => _grantSearch.Text.Length == 0 || (k.Key + " " + SecurityPresentation.Name(k) + " " + k.Description).IndexOf(_grantSearch.Text, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .Where(k => !_grantedOnly.Checked || (Vm.RoleDraft?.DesiredKeys.Contains(k.Key) ?? role?.PermissionKeys.Contains(k.Key) == true))
                .Select(k => k.Key).ToArray();
            if (_grants.Items.Count != activeKeys.Length || activeKeys.Where((key, index) => !string.Equals(_grants.Items[index] as string, key, StringComparison.Ordinal)).Any())
            {
                _grants.Items.Clear();
                foreach (var key in activeKeys) _grants.Items.Add(key);
                if (selectedGrant != null) _grants.SelectedIndex = _grants.Items.IndexOf(selectedGrant);
            }
            for (var index = 0; index < activeKeys.Length; index++)
            {
                var key = activeKeys[index];
                var granted = Vm.RoleDraft != null && Vm.RoleDraft.RoleId == role?.RoleId ? Vm.RoleDraft.DesiredKeys.Contains(key) : role?.PermissionKeys.Contains(key) == true;
                if (_grants.GetItemChecked(index) != granted) _grants.SetItemChecked(index, granted);
            }
            _grants.Enabled = role != null && Vm.Catalog.IsCurrent(Vm.OwnerVersion) && !Vm.IsBusy && !Vm.RequiresRecovery;
            _grants.Refresh();
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
            _directPreview.Text = DirectPreview();
        }
        finally { _rendering = false; }
    }

    async Task OpenDirectAsync(string key)
    {
        if (!await AdmitAsync()) return;
        if (!Vm.BeginDirectPermission(key)) return;
        _control.UserDetail.Tabs.SelectedIndex = 3;
        Render();
    }

    string DirectPreview()
    {
        var key = Vm.DirectPermissionKey;
        var user = Vm.SelectedUser;
        if (key == null || user == null) return "Chọn một mã quyền để xem kết quả dự kiến.";
        var committed = SecurityPermissionProjection.Evaluate(Vm.Snapshot, key);
        var decisions = user.DirectDecisions.Where(item => item.PermissionKey != key).ToList();
        if (Vm.DirectDecision.HasValue) decisions.Add(new SecurityDecisionDto(key, Vm.DirectDecision));
        var draftUser = new SecurityUserDto(user.UserId, user.Username, user.DisplayName, user.Email, user.IsActive,
            user.AssignmentRevision, user.RoleIds, decisions, user.IsProtectedAccount);
        var draftSnapshot = new SecurityProjectionSnapshot(draftUser, Vm.Users, Vm.Roles, Vm.Catalog, Vm.OwnerVersion);
        var preview = SecurityPermissionProjection.Evaluate(draftSnapshot, key);
        return "Đã lưu: " + SecurityPresentation.Result(committed.Result) + " · " + SecurityPresentation.Source(committed) + Environment.NewLine +
            "Kết quả dự kiến (chưa lưu): " + SecurityPresentation.Result(preview.Result) + " · " + SecurityPresentation.Source(preview);
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
        dialog.AcceptButton = save;
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
        dialog.AcceptButton = save;
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

internal sealed class RoleGrantList : CheckedListBox
{
    internal event Action? SpacePressed;

    protected override void WndProc(ref Message message)
    {
        if ((message.Msg == 0x0100 || message.Msg == 0x0101 || message.Msg == 0x0102) && message.WParam.ToInt32() == (int)Keys.Space)
        {
            if (message.Msg == 0x0100 && (message.LParam.ToInt64() & (1L << 30)) == 0)
                SpacePressed?.Invoke();
            return;
        }
        base.WndProc(ref message);
    }
}
