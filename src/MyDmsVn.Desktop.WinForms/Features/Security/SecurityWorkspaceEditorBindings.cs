using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Rendering;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

internal sealed class SecurityWorkspaceEditorBindings
{
    readonly SecurityWorkspaceControl _control;
    readonly RoleGrantGrid _grants = new()
    {
        Dock = DockStyle.Fill, AccessibleName = "Cấp quyền cho vai trò", ReadOnly = true,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
        AutoGenerateColumns = false, RowHeadersVisible = false, MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, StandardTab = true,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, EmptyStateText = "Không có quyền phù hợp bộ lọc."
    };
    readonly BootstrapTextBox _grantSearch = new() { Width = 145, AccessibleName = "Tìm quyền vai trò", PlaceholderText = "Tìm quyền…" };
    readonly BootstrapComboBox _grantModule = new() { Width = 115, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Nhóm quyền vai trò" };
    readonly BootstrapCheckBox _grantedOnly = new() { Text = "Chỉ quyền đã cấp", AutoSize = true };
    readonly Label _grantSummary = new() { Dock = DockStyle.Bottom, Height = 20 };
    readonly ComboBox _directKey = new BootstrapComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Quyền riêng" };
    readonly RadioButton _inherit = new BootstrapRadioButton { Text = "Theo vai trò", AutoSize = true };
    readonly RadioButton _allow = new BootstrapRadioButton { Text = "Cho phép", AutoSize = true };
    readonly RadioButton _deny = new BootstrapRadioButton { Text = "Từ chối", AutoSize = true };
    readonly Label _status = new() { AutoSize = true };
    readonly Label _directPreview = new BootstrapLabel { Dock = DockStyle.Bottom, Height = 58, AutoEllipsis = true, AccessibleName = "Kết quả dự kiến và quyết định đã lưu" };
    readonly FlowLayoutPanel _recovery = Commands();
    readonly Button _recoveryReview;
    readonly ToolTip _maintenanceTip = new();
    readonly Button _createUser;
    readonly Button _editUser;
    readonly Button _resetPassword;
    readonly Button _changeUserState;
    readonly Button _editRolePermissions;
    readonly Button _grantDiscard;
    readonly Button _grantSave;
    readonly FlowLayoutPanel _grantActions;
    readonly Panel _grantContent = new() { Dock = DockStyle.Top };
    readonly Button _assignRole;
    readonly Button _removeRole;
    readonly Label _membershipHint = new BootstrapLabel { AutoSize = true, AccessibleName = "Thông báo gán vai trò", TextRole = BootstrapTextRole.Muted };
    SecurityDataset<PermissionCatalogItemDto>? _grantCatalogSnapshot;
    GrantCatalogEntry[] _activeGrantEntries = Array.Empty<GrantCatalogEntry>();
    Dictionary<string, GrantCatalogEntry> _activeGrantByKey = new(StringComparer.Ordinal);
    string[] _grantModuleOptions = { "Tất cả nhóm" };
    string[] _directGrantKeys = Array.Empty<string>();
    SecurityRolePermissionDraft? _grantDraftSnapshot;
    long _grantDraftRevision = -1;
    IReadOnlyList<SecurityPermissionChange> _grantChanges = Array.Empty<SecurityPermissionChange>();
    HashSet<string> _draftGrantedKeys = new(StringComparer.Ordinal);
    HashSet<string> _changedGrantKeys = new(StringComparer.Ordinal);
    SecurityRoleDto? _grantRoleSnapshot;
    HashSet<string> _roleGrantedKeys = new(StringComparer.Ordinal);
    bool _rendering;
    SecurityWorkspaceViewModel Vm => _control.ViewModel;

    sealed class GrantCatalogEntry
    {
        internal GrantCatalogEntry(PermissionCatalogItemDto item)
        {
            Item = item;
            var name = SecurityPresentation.Name(item);
            SearchText = item.Key + " " + name + " " + item.Description;
            Display = name + Environment.NewLine + item.Key;
            PendingDisplay = Display + " · Chưa lưu";
            ToolTip = name + " · " + item.Key + " · " + item.Module;
            PendingToolTip = ToolTip + " · Chưa lưu";
        }

        internal PermissionCatalogItemDto Item { get; }
        internal string SearchText { get; }
        internal string Display { get; }
        internal string PendingDisplay { get; }
        internal string ToolTip { get; }
        internal string PendingToolTip { get; }
    }

    internal SecurityWorkspaceEditorBindings(SecurityWorkspaceControl control)
    {
        _control = control;
        var roleCommands = control.RoleDetail.HeaderActions;
        Add(roleCommands, "Tạo vai trò…", () => IdentityDialogAsync(false, true));
        Add(roleCommands, "Sửa tên…", () => IdentityDialogAsync(false, false));
        Add(roleCommands, "Đổi trạng thái…", () => ActivationAsync(false));
        _editRolePermissions = Add(roleCommands, "Sửa quyền", () =>
        {
            if (Vm.BeginRolePermissions())
            {
                control.RoleDetail.Tabs.SelectedIndex = 0;
                _grantSearch.Focus();
            }
            return Task.CompletedTask;
        });
        var grantFilters = Commands();
        grantFilters.AutoScroll = false;
        grantFilters.Layout += (_, _) =>
        {
            var height = grantFilters.Controls.Cast<Control>().Where(child => child.Visible)
                .Select(child => child.Bottom + child.Margin.Bottom).DefaultIfEmpty(0).Max();
            if (height > 0 && grantFilters.Height != height) grantFilters.Height = height;
            LayoutGrantArea();
        };
        grantFilters.Controls.AddRange(new Control[] { _grantSearch, _grantModule, _grantedOnly });
        var grantActions = _grantActions = Commands();
        grantActions.Dock = DockStyle.Bottom;
        grantActions.Height = 42;
        _grantDiscard = Add(grantActions, "Hủy thay đổi", () => { Vm.DiscardEditor(); return Task.CompletedTask; });
        _grantSave = Add(grantActions, "Lưu quyền vai trò", () => Vm.SaveEditorAsync(default));
        ((BootstrapButton)_grantSave).Outline = false;
        ((BootstrapButton)_grantDiscard).Variant = BootstrapVariant.Secondary;
        control.RoleDetail.GrantArea.Controls.Add(_grantContent);
        control.RoleDetail.GrantArea.SizeChanged += (_, _) => LayoutGrantArea();
        _grantContent.Controls.Add(_grants);
        _grantContent.Controls.Add(_grantSummary);
        _grantContent.Controls.Add(grantActions);
        _grantContent.Controls.Add(grantFilters);
        _grantModule.Items.Add("Tất cả nhóm");
        _grantModule.SelectedIndex = 0;
        _grantSearch.TextChanged += (_, _) => { if (!_rendering) Render(); };
        _grantModule.SelectedIndexChanged += (_, _) => { if (!_rendering) Render(); };
        _grantedOnly.CheckedChanged += (_, _) => { if (!_rendering) Render(); };
        _grants.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Cấp", Width = 52, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grants.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Quyền", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 80,
            DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True, Padding = new Padding(8, 4, 8, 4) },
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grants.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nhóm", Width = 100, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grants.CellContentClick += (_, e) =>
        {
            if (e.ColumnIndex == 0 && e.RowIndex >= 0) ToggleGrant(e.RowIndex);
        };
        _grants.KeyDown += (_, e) =>
        {
            // Checkbox cells already raise CellContentClick for native Space. Other cells act on their selected row.
            if (e.KeyCode != Keys.Space || _grants.CurrentCell == null || _grants.CurrentCell.ColumnIndex == 0) return;
            e.Handled = e.SuppressKeyPress = true;
            ToggleGrant(_grants.CurrentCell.RowIndex);
        };
        var direct = Commands();
        direct.AutoScroll = false;
        direct.AutoSize = true;
        direct.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        direct.Controls.AddRange(new Control[] { _directKey, _inherit, _allow, _deny });
        var directArea = control.UserDetail.Tabs.TabPages[3];
        directArea.AutoScroll = true;
        directArea.Layout += (_, _) => LayoutDirectArea();
        directArea.Controls.Add(direct);
        var directActions = Commands();
        directActions.Dock = DockStyle.Bottom;
        directActions.AutoScroll = false;
        directActions.AutoSize = true;
        directActions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ((BootstrapButton)Add(directActions, "Hủy", () => { Vm.DiscardEditor(); return Task.CompletedTask; })).Variant = BootstrapVariant.Secondary;
        ((BootstrapButton)Add(directActions, "Lưu quyền này", () => Vm.SaveEditorAsync(default))).Outline = false;
        directArea.Controls.Add(_directPreview);
        directArea.Controls.Add(directActions);
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
        membership.AutoScroll = false;
        membership.AutoSize = true;
        membership.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _assignRole = Add(membership, "Gán vai trò…", async () =>
        {
            var userId = Vm.SelectedUserId;
            if (!CanManageMembership() || !await AdmitAsync() || Vm.SelectedUserId != userId || !CanManageMembership()) return;
            using var dialog = new Form { Text = "Gán vai trò — " + Vm.SelectedUser!.DisplayName, Width = 450, Height = 210, StartPosition = FormStartPosition.CenterParent };
            var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            var roles = new ComboBox { Width = 390, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", AccessibleName = "Vai trò" };
            foreach (var role in Vm.Roles.Items) roles.Items.Add(role);
            var ok = new Button { Text = "Xác nhận gán", AutoSize = true, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
            fields.Controls.Add(new Label { Text = "Vai trò", AutoSize = true, Margin = new Padding(3, 6, 3, 0) });
            fields.Controls.Add(roles); fields.Controls.Add(ok); fields.Controls.Add(cancel);
            dialog.Controls.Add(fields);
            dialog.AcceptButton = ok;
            dialog.CancelButton = cancel;
            BusinessCompactAppearance.Apply(dialog);
            if (dialog.ShowDialog(control) == DialogResult.OK && roles.SelectedItem is SecurityRoleDto selected) await Vm.SetMembershipAsync(selected.RoleId, true, default);
        });
        _removeRole = Add(membership, "Gỡ vai trò", async () =>
        {
            var user = Vm.SelectedUser;
            var index = control.UserDetail.Memberships.SelectedIndex;
            if (!CanManageMembership() || user == null || index < 0 || index >= user.RoleIds.Count || !await AdmitAsync() ||
                Vm.SelectedUserId != user.UserId || !CanManageMembership()) return;
            var roleId = user.RoleIds[index];
            var role = Vm.Roles.Items.FirstOrDefault(r => r.RoleId == roleId);
            if (role == null) return;
            var name = role.Name;
            if (MessageBox.Show(control, "Gỡ " + name + " khỏi " + user.DisplayName + "?", "Gỡ vai trò", MessageBoxButtons.OKCancel) == DialogResult.OK) await Vm.SetMembershipAsync(roleId, false, default);
        });
        ((BootstrapButton)_assignRole).Outline = false;
        ((BootstrapButton)_removeRole).Variant = BootstrapVariant.Danger;
        membership.Controls.Add(_membershipHint);
        control.UserDetail.Tabs.TabPages[1].Controls.Add(membership);
        control.UserDetail.Memberships.SelectedIndexChanged += (_, _) => RenderMembershipActions();
        var profile = control.UserDetail.HeaderActions;
        _createUser = Add(profile, "Tạo người dùng…", () => IdentityDialogAsync(true, true));
        _editUser = Add(profile, "Sửa thông tin…", () => IdentityDialogAsync(true, false));
        _resetPassword = Add(profile, "Đặt lại mật khẩu…", () => PasswordAsync());
        _changeUserState = Add(profile, "Đổi trạng thái…", () => ActivationAsync(true));
        _recovery.Dock = DockStyle.Bottom;
        _recovery.Height = 54;
        _recovery.Controls.Add(_status);
        Add(_recovery, "Tải lại để đối chiếu", async () => { await Vm.ReloadForComparisonAsync(default); }, themed: false);
        _recoveryReview = Add(_recovery, "Đã đối chiếu — lập diff mới", () =>
        {
            if (control.RecoveryConfirmationOverride?.Invoke() ??
                MessageBox.Show(control, "Giữ ý định và tính lại thay đổi từ dữ liệu vừa tải? Không xác nhận kết quả lệnh chưa rõ.",
                    "Đối chiếu", MessageBoxButtons.OKCancel) == DialogResult.OK)
                Vm.ReviewRecoveryAndRebase();
            return Task.CompletedTask;
        }, themed: false);
        control.UserDetail.Controls.Add(_recovery);
        GuardTabs(control.Tabs);
        GuardTabs(control.UserDetail.Tabs);
        GuardTabs(control.RoleDetail.Tabs);
        Vm.Changed += Changed;
        control.Disposed += (_, _) => { Vm.Changed -= Changed; _maintenanceTip.Dispose(); };
        Render();
    }

    void Changed(object? sender, EventArgs e) => Render();
    void GuardTabs(TabControl tabs)
    {
        var navigationVersion = 0;
        tabs.Selecting += async (_, e) =>
        {
            var requestVersion = ++navigationVersion;
            if (Vm.CanNavigate) return;
            e.Cancel = true;
            var destination = e.TabPage;
            var ownerVersion = Vm.OwnerVersion;
            if (!await AdmitAsync() || destination == null || tabs.IsDisposed || !tabs.IsHandleCreated) return;
            // A synchronous Discard can finish inside Selecting. Resume selection after WinForms finishes canceling it.
            tabs.BeginInvoke(new Action(() =>
            {
                if (!_control.IsDisposed && !tabs.IsDisposed && Vm.IsCurrent && Vm.OwnerVersion == ownerVersion &&
                    requestVersion == navigationVersion &&
                    Vm.CanNavigate && tabs.TabPages.Contains(destination))
                    tabs.SelectedTab = destination;
            }));
        };
    }
    void RefreshGrantCatalogSnapshot()
    {
        if (ReferenceEquals(_grantCatalogSnapshot, Vm.Catalog)) return;
        _grantCatalogSnapshot = Vm.Catalog;
        _activeGrantByKey = new Dictionary<string, GrantCatalogEntry>(StringComparer.Ordinal);
        var entries = new List<GrantCatalogEntry>();
        foreach (var item in Vm.Catalog.Items.Where(item => item.Status == "Active"))
        {
            if (_activeGrantByKey.ContainsKey(item.Key)) continue;
            var entry = new GrantCatalogEntry(item);
            _activeGrantByKey.Add(item.Key, entry);
            entries.Add(entry);
        }
        _activeGrantEntries = entries.ToArray();
        _grantModuleOptions = new[] { "Tất cả nhóm" }.Concat(_activeGrantEntries.Select(entry => entry.Item.Module).Distinct().OrderBy(value => value)).ToArray();
        _directGrantKeys = _activeGrantEntries.Select(entry => entry.Item.Key).ToArray();
    }

    void RefreshGrantMembershipSnapshot(SecurityRolePermissionDraft? draft, SecurityRoleDto? role)
    {
        if (draft == null)
        {
            _grantDraftSnapshot = null;
            _grantDraftRevision = -1;
            _grantChanges = Array.Empty<SecurityPermissionChange>();
            _draftGrantedKeys.Clear();
            _changedGrantKeys.Clear();
        }
        else if (!ReferenceEquals(_grantDraftSnapshot, draft) || _grantDraftRevision != draft.LocalEditRevision)
        {
            _grantDraftSnapshot = draft;
            _grantDraftRevision = draft.LocalEditRevision;
            _grantChanges = draft.Changes;
            _draftGrantedKeys = new HashSet<string>(draft.DesiredKeys, StringComparer.Ordinal);
            _changedGrantKeys = new HashSet<string>(_grantChanges.Select(change => change.PermissionKey), StringComparer.Ordinal);
        }
        if (ReferenceEquals(_grantRoleSnapshot, role)) return;
        _grantRoleSnapshot = role;
        _roleGrantedKeys = new HashSet<string>(role?.PermissionKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    void Render()
    {
        _rendering = true;
        try
        {
            _status.Text = Vm.EditingStatus;
            var actionFeedback = Vm.LastMutationOperation == SecurityAdministrationOperationDto.SetActive
                ? Vm.LastMutationFeedback?.Message : null;
            _control.UserDetail.ActionFeedback.Text = Vm.LastMutationTarget == SecurityAdministrationTargetDto.User &&
                Vm.LastMutationTargetId == Vm.SelectedUserId ? actionFeedback ?? "" : "";
            _control.UserDetail.ActionFeedback.Visible = _control.UserDetail.ActionFeedback.Text.Length > 0;
            _control.RoleDetail.ActionFeedback.Text = Vm.LastMutationTarget == SecurityAdministrationTargetDto.Role &&
                Vm.LastMutationTargetId == Vm.SelectedRoleId ? actionFeedback ?? "" : "";
            _control.RoleDetail.ActionFeedback.Visible = _control.RoleDetail.ActionFeedback.Text.Length > 0;
            var user = Vm.SelectedUser;
            var usersAvailable = Vm.IsCurrent && Vm.Users.IsCurrent(Vm.OwnerVersion) && !Vm.IsBusy && !Vm.RequiresRecovery;
            _createUser.Enabled = usersAvailable;
            var canMaintainUser = usersAvailable && user is { IsProtectedAccount: false };
            var maintenanceHint = user?.IsProtectedAccount == true ? "Tài khoản được bảo vệ — bảo trì bên ngoài màn này." :
                user == null ? "Chọn người dùng để thực hiện." : "";
            foreach (var action in new[] { _editUser, _resetPassword, _changeUserState })
            {
                action.Enabled = canMaintainUser;
                action.AccessibleDescription = maintenanceHint;
                _maintenanceTip.SetToolTip(action, maintenanceHint);
            }
            RenderMembershipActions();
            var partialForbidden = Vm.LastSaveOutcome?.Completion == SecuritySaveCompletion.Partial &&
                Vm.LastSaveOutcome.StopReason == SecuritySaveStopReason.Forbidden &&
                (Vm.LastSaveTarget == SecurityAdministrationTargetDto.Role ? Vm.Roles.State == SecurityDatasetState.Forbidden :
                    Vm.Users.State == SecurityDatasetState.Forbidden);
            var recoveryOwner = (Vm.RequiresRecovery ? Vm.RecoveryTarget : Vm.LastSaveTarget) == SecurityAdministrationTargetDto.Role
                ? (Control)_control.RoleDetail : _control.UserDetail;
            if (_recovery.Parent != recoveryOwner)
            {
                recoveryOwner.Controls.Add(_recovery);
                _recovery.BringToFront();
            }
            _recovery.Visible = Vm.RequiresRecovery || partialForbidden;
            foreach (var button in _recovery.Controls.OfType<Button>()) button.Visible = Vm.RequiresRecovery;
            _recoveryReview.Enabled = Vm.CanReviewRecovery;
            var roleDraft = Vm.RoleDraft;
            var role = Vm.SelectedRole;
            RefreshGrantCatalogSnapshot();
            RefreshGrantMembershipSnapshot(roleDraft, role);
            var roleChanges = roleDraft == null ? null : _grantChanges;
            _grantSummary.Text = roleDraft == null ? "Chọn Sửa quyền để bắt đầu bản nháp" : roleChanges!.Count + " thay đổi chưa lưu trong toàn bộ danh mục";
            var roleEditorAvailable = Vm.IsCurrent && Vm.Roles.IsCurrent(Vm.OwnerVersion) && Vm.Catalog.IsCurrent(Vm.OwnerVersion) &&
                Vm.SelectedRole != null && !Vm.IsBusy && !Vm.RequiresRecovery;
            _editRolePermissions.Enabled = roleEditorAvailable && roleDraft == null;
            _grantDiscard.Enabled = roleEditorAvailable && roleDraft != null;
            _grantSave.Enabled = roleEditorAvailable && roleChanges?.Count > 0;
            _grantActions.Visible = roleDraft != null;
            _grantSummary.Visible = roleDraft != null;
            var selectedGrant = _grants.CurrentRow?.Tag as string;
            var draftOwnsRole = roleDraft != null && roleDraft.RoleId == role?.RoleId;
            var grantedKeys = draftOwnsRole ? _draftGrantedKeys : _roleGrantedKeys;
            var changedKeys = roleDraft == null ? null : _changedGrantKeys;
            var selectedModule = _grantModule.SelectedItem as string;
            if (_grantModule.Items.Count != _grantModuleOptions.Length || _grantModuleOptions.Where((value, index) => !string.Equals(_grantModule.Items[index] as string, value, StringComparison.Ordinal)).Any())
            {
                _grantModule.Items.Clear();
                _grantModule.Items.AddRange(_grantModuleOptions.Cast<object>().ToArray());
            }
            _grantModule.SelectedIndex = selectedModule != null && _grantModule.Items.Contains(selectedModule) ? _grantModule.Items.IndexOf(selectedModule) : 0;
            var activeKeys = _activeGrantEntries
                .Where(entry => _grantModule.SelectedIndex == 0 || entry.Item.Module == _grantModule.Text)
                .Where(entry => _grantSearch.Text.Length == 0 || entry.SearchText.IndexOf(_grantSearch.Text, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .Where(entry => !_grantedOnly.Checked || grantedKeys.Contains(entry.Item.Key))
                .Select(entry => entry.Item.Key).ToArray();
            var grantPresentationChanged = _grants.Rows.Count != activeKeys.Length || activeKeys.Where((key, index) => !string.Equals(_grants.Rows[index].Tag as string, key, StringComparison.Ordinal)).Any();
            if (grantPresentationChanged)
            {
                _grants.Rows.Clear();
                foreach (var key in activeKeys)
                {
                    var index = _grants.Rows.Add(false, "", "");
                    _grants.Rows[index].Tag = key;
                    if (key == selectedGrant) _grants.CurrentCell = _grants.Rows[index].Cells[0];
                }
            }
            for (var index = 0; index < activeKeys.Length; index++)
            {
                var key = activeKeys[index];
                var entry = _activeGrantByKey[key];
                var item = entry.Item;
                var granted = grantedKeys.Contains(key);
                var pending = changedKeys?.Contains(key) == true;
                var row = _grants.Rows[index];
                var display = pending ? entry.PendingDisplay : entry.Display;
                var tooltip = pending ? entry.PendingToolTip : entry.ToolTip;
                if (!Equals(row.Cells[0].Value, granted)) { row.Cells[0].Value = granted; grantPresentationChanged = true; }
                if (!Equals(row.Cells[1].Value, display)) { row.Cells[1].Value = display; grantPresentationChanged = true; }
                if (!Equals(row.Cells[2].Value, item.Module)) { row.Cells[2].Value = item.Module; grantPresentationChanged = true; }
                foreach (DataGridViewCell cell in row.Cells)
                    if (!string.Equals(cell.ToolTipText, tooltip, StringComparison.Ordinal)) { cell.ToolTipText = tooltip; grantPresentationChanged = true; }
            }
            _grants.Enabled = role != null && Vm.Catalog.IsCurrent(Vm.OwnerVersion) && !Vm.IsBusy && !Vm.RequiresRecovery;
            LayoutGrantArea();
            if (grantPresentationChanged) _grants.Refresh();
            _grants.AccessibleDescription = roleDraft == null ? "Chỉ xem. Chọn Sửa quyền để thay đổi." : "Đang sửa quyền của vai trò.";
            var selected = Vm.DirectPermissionKey;
            if (_directKey.Items.Count != _directGrantKeys.Length || _directGrantKeys.Where((key, index) => !string.Equals(_directKey.Items[index] as string, key, StringComparison.Ordinal)).Any())
            {
                _directKey.Items.Clear();
                _directKey.Items.AddRange(_directGrantKeys.Cast<object>().ToArray());
            }
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

    void LayoutGrantArea()
    {
        // Keep at least a complete permission row under its header. At constrained product bounds,
        // this local viewport scrolls the filters/footer instead of collapsing the native table.
        var rowHeight = _grants.Rows.Cast<DataGridViewRow>().Select(row => row.Height).DefaultIfEmpty(_grants.RowTemplate.Height).Max();
        var minimum = _grants.ColumnHeadersHeight + rowHeight + 4 +
            _grantContent.Controls.Cast<Control>().Where(child => child != _grants && child.Visible).Sum(child => child.Height);
        _grantContent.Height = Math.Max(_control.RoleDetail.GrantArea.ClientSize.Height, minimum);
    }

    void LayoutDirectArea()
    {
        // Dock against a local minimum display area, including contextual import/status controls
        // added by their own binding. Optional feedback must not consume the native list's rows.
        var area = _control.UserDetail.Tabs.TabPages[3];
        var list = _control.UserDetail.DirectDecisions;
        var minimum = list.ItemHeight * 2 + list.Height - list.ClientSize.Height +
            area.Controls.Cast<Control>().Where(child => child != list && child.Visible).Sum(child => child.Height);
        area.AutoScrollMinSize = new System.Drawing.Size(0, minimum);
    }

    void ToggleGrant(int index)
    {
        var draft = Vm.RoleDraft;
        if (_rendering || draft == null || !_grants.Enabled || !Vm.IsCurrent || Vm.IsBusy || Vm.RequiresRecovery ||
            !Vm.Roles.IsCurrent(Vm.OwnerVersion) || !Vm.Catalog.IsCurrent(Vm.OwnerVersion) ||
            Vm.SelectedRoleId != draft.RoleId || Vm.OwnerVersion != draft.SessionVersion ||
            index < 0 || index >= _grants.Rows.Count || _grants.Rows[index].Tag is not string key ||
            !Vm.Catalog.Items.Any(item => item.Key == key && item.Status == "Active")) return;
        // Native grid editing is read-only: one mouse/Space gesture changes only this canonical draft key.
        // No deferred callback can carry the row index into a replacement role, draft or session.
        Vm.SetRoleGranted(key, !draft.DesiredKeys.Contains(key));
    }

    bool CanManageMembership() => Vm.IsCurrent && Vm.Users.IsCurrent(Vm.OwnerVersion) && Vm.Roles.IsCurrent(Vm.OwnerVersion) &&
        Vm.SelectedUser is { IsProtectedAccount: false } && !Vm.IsBusy && !Vm.RequiresRecovery;

    void RenderMembershipActions()
    {
        var user = Vm.SelectedUser;
        var hint = Vm.Users.State == SecurityDatasetState.Forbidden ? "Không đủ quyền quản lý người dùng." :
            !Vm.IsCurrent || !Vm.Users.IsCurrent(Vm.OwnerVersion) ? "Người dùng chưa khả dụng." :
            user == null ? "Chọn người dùng để quản lý vai trò." :
            user.IsProtectedAccount ? "Tài khoản được bảo vệ — bảo trì bên ngoài màn này." :
            !Vm.Roles.IsCurrent(Vm.OwnerVersion) ? "Vai trò chưa khả dụng — tải lại dữ liệu trước khi thay đổi." :
            Vm.IsBusy ? "Đang xử lý thay đổi." :
            Vm.RequiresRecovery ? "Cần đối chiếu kết quả trước khi thay đổi." : "";
        if (hint.Length == 0 && Vm.LastMutationOperation == SecurityAdministrationOperationDto.SetMembership &&
            Vm.LastMutationTarget == SecurityAdministrationTargetDto.User && Vm.LastMutationTargetId == user?.UserId)
            hint = Vm.LastMutationFeedback?.Message ?? "";
        var canManage = CanManageMembership();
        _assignRole.Enabled = canManage;
        var index = _control.UserDetail.Memberships.SelectedIndex;
        var canRemove = canManage && index >= 0 && index < user!.RoleIds.Count &&
            Vm.Roles.Items.Any(role => role.RoleId == user.RoleIds[index]);
        _removeRole.Enabled = canRemove;
        _assignRole.AccessibleDescription = hint;
        _removeRole.AccessibleDescription = canRemove ? "" : hint.Length > 0 ? hint : "Chọn vai trò để gỡ.";
        _membershipHint.Text = hint;
        _membershipHint.Visible = hint.Length > 0;
        _maintenanceTip.SetToolTip(_assignRole, hint);
        _maintenanceTip.SetToolTip(_removeRole, _removeRole.AccessibleDescription);
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

    Task<bool> AdmitAsync() => _control.AdmitNavigationAsync();

    async Task IdentityDialogAsync(bool userTarget, bool create)
    {
        if (!await AdmitAsync()) return;
        if (userTarget && create) { await CreateUserDialogAsync(); return; }
        var user = Vm.SelectedUser;
        var role = Vm.SelectedRole;
        if (!create && (userTarget ? user == null || user.IsProtectedAccount : role == null)) return;
        using var dialog = new MutationDialog { Text = create ? "Tạo " + (userTarget ? "người dùng" : "vai trò") : "Sửa — " + (userTarget ? user!.DisplayName : role!.Name), Width = 440, Height = userTarget ? (create ? 430 : 380) : 240, StartPosition = FormStartPosition.CenterParent };
        var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        var name = new TextBox { Width = 380, AccessibleName = userTarget ? "Tên đăng nhập" : "Tên vai trò", Text = create ? "" : userTarget ? user!.Username : role!.Name };
        var display = new TextBox { Width = 380, AccessibleName = "Tên hiển thị", Text = create ? "" : user?.DisplayName ?? "" };
        var email = new TextBox { Width = 380, AccessibleName = "Email", Text = create ? "" : user?.Email ?? "" };
        var password = new TextBox { Width = 380, AccessibleName = "Mật khẩu tạm", UseSystemPasswordChar = true };
        var identityFields = (Name: (Control)name, Display: (Control)display, Email: (Control)email, Password: (Control)password);
        void AddField(string caption, TextBox field)
        {
            fields.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(3, 6, 3, 0) });
            fields.Controls.Add(field);
        }
        AddField(userTarget ? "Tên đăng nhập" : "Tên vai trò", name);
        if (userTarget)
        {
            AddField("Tên hiển thị", display);
            AddField("Email", email);
            if (create) AddField("Mật khẩu tạm", password);
        }
        var validation = new Label { AutoSize = true, AccessibleName = "Lỗi thông tin" };
        fields.Controls.Add(validation);
        var reload = new Button { Text = "Tải lại để đối chiếu", AutoSize = true, Visible = false };
        fields.Controls.Add(reload);
        var save = new Button { Text = "Lưu thông tin", AutoSize = true };
        var cancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
        fields.Controls.Add(save); fields.Controls.Add(cancel); dialog.Controls.Add(fields);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        var closeGuard = new MutationDialogCloseGuard(dialog, cancel, validation, _control, Vm);
        reload.Click += async (_, _) => await ReloadDialogAsync(dialog, validation, reload);
        save.Click += async (_, _) =>
        {
            save.Enabled = false;
            try
            {
                var invalid = ValidateIdentityFields(userTarget, create, identityFields);
                if (invalid.HasValue)
                {
                    validation.Text = invalid.Value.Message;
                    invalid.Value.Field.Focus();
                    return;
                }
                var saved = await ExecuteIdentityMutationAsync(userTarget, create,
                    create ? 0 : userTarget ? user!.UserId : role!.RoleId,
                    create ? null : userTarget ? user!.AssignmentRevision : role!.AssignmentRevision, identityFields, closeGuard);
                if (saved) dialog.Close();
                else if (!dialog.IsDisposed)
                {
                    var field = PresentIdentityFeedback(validation, reload, identityFields);
                    (field ?? (Vm.RequiresRecovery ? reload : name)).Focus();
                }
            }
            finally { password.Clear(); if (!dialog.IsDisposed) save.Enabled = !Vm.RequiresRecovery; }
        };
        BusinessCompactAppearance.Apply(dialog);
        dialog.ShowDialog(_control);
        password.Clear();
    }

    Task CreateUserDialogAsync()
    {
        using var dialog = new MutationDialog
        {
            Text = "Tạo người dùng", Width = 480, Height = 470, StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false
        };
        var body = new BootstrapPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12), SurfaceRole = BootstrapSurfaceRole.Surface };
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, TabIndex = 0 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.Controls.Add(fields);
        dialog.Controls.Add(body);
        BootstrapTextBox AddField(string caption, int index, bool secret = false)
        {
            var label = new BootstrapLabel { Text = caption, AutoSize = true, Margin = new Padding(0, 8, 0, 4), TabStop = false };
            var input = new BootstrapTextBox
            {
                Dock = DockStyle.Top, AccessibleName = caption, TabIndex = index,
                UseSystemPasswordChar = secret, Margin = new Padding(0, 0, 0, 4)
            };
            fields.Controls.Add(label);
            fields.Controls.Add(input);
            return input;
        }
        var name = AddField("Tên đăng nhập", 0);
        var display = AddField("Tên hiển thị", 1);
        var email = AddField("Email", 2);
        var password = AddField("Mật khẩu tạm", 3, true);
        var inputs = new[] { name, display, email, password };
        var identityFields = (Name: (Control)name, Display: (Control)display, Email: (Control)email, Password: (Control)password);
        var validation = new BootstrapLabel
        {
            AutoSize = true, Dock = DockStyle.Top, MaximumSize = new System.Drawing.Size(400, 0),
            AccessibleName = "Lỗi thông tin", AccessibleRole = AccessibleRole.Alert,
            TextRole = BootstrapTextRole.Danger, Margin = new Padding(0, 8, 0, 0)
        };
        fields.Controls.Add(validation);
        BootstrapTextBox? invalidFeedbackInput = null;
        var clearingSecret = false;
        void ShowFieldFeedback(Control field, string message)
        {
            validation.Text = message;
            invalidFeedbackInput = field as BootstrapTextBox;
            if (invalidFeedbackInput != null) invalidFeedbackInput.ValidationState = BootstrapValidationState.Invalid;
        }
        void ClearSecret()
        {
            clearingSecret = true;
            try { password.Clear(); }
            finally { clearingSecret = false; }
        }
        foreach (var input in inputs) input.TextChanged += (_, _) =>
        {
            if (clearingSecret || !ReferenceEquals(input, invalidFeedbackInput)) return;
            var invalid = ValidateIdentityFields(true, true, identityFields);
            if (invalid.HasValue && ReferenceEquals(invalid.Value.Field, input))
            {
                validation.Text = invalid.Value.Message;
                return;
            }
            input.ValidationState = BootstrapValidationState.None;
            invalidFeedbackInput = null;
            validation.Text = "";
        };
        var save = new BootstrapButton { Text = "Lưu thông tin", AutoSize = true, Variant = BootstrapVariant.Primary, TabIndex = 0 };
        var cancel = new BootstrapButton { Text = "Hủy", AutoSize = true, Variant = BootstrapVariant.Secondary, Outline = true, DialogResult = DialogResult.Cancel, TabIndex = 1 };
        var reload = new BootstrapButton { Text = "Tải lại để đối chiếu", AutoSize = true, Variant = BootstrapVariant.Secondary, Outline = true, Visible = false, TabIndex = 2 };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8), WrapContents = false, TabIndex = 1 };
        footer.Controls.AddRange(new Control[] { save, cancel, reload });
        dialog.Controls.Add(footer);
        body.TabIndex = 0;
        dialog.Shown += (_, _) => name.Focus();
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        var closeGuard = new MutationDialogCloseGuard(dialog, cancel, validation, _control, Vm);
        reload.Click += async (_, _) => await ReloadDialogAsync(dialog, validation, reload);
        save.Click += async (_, _) =>
        {
            save.Enabled = false;
            try
            {
                invalidFeedbackInput = null;
                validation.Text = "";
                foreach (var input in inputs) input.ValidationState = BootstrapValidationState.None;
                var invalid = ValidateIdentityFields(true, true, identityFields);
                if (invalid.HasValue)
                {
                    ShowFieldFeedback(invalid.Value.Field, invalid.Value.Message);
                    invalid.Value.Field.Focus();
                    return;
                }
                var saved = await ExecuteIdentityMutationAsync(true, true, 0, null, identityFields, closeGuard);
                if (saved) dialog.Close();
                else if (!dialog.IsDisposed)
                {
                    var field = PresentIdentityFeedback(validation, reload, identityFields);
                    if (field != null) ShowFieldFeedback(field, validation.Text);
                    (field ?? (Vm.RequiresRecovery ? reload : name)).Focus();
                }
            }
            finally { ClearSecret(); if (!dialog.IsDisposed) save.Enabled = !Vm.RequiresRecovery; }
        };
        // A separate presentation owner; toolkit composites retain ownership of native editor internals.
        BusinessCompactAppearance.Apply(dialog);
        try { dialog.ShowDialog(_control); }
        finally { ClearSecret(); }
        return Task.CompletedTask;
    }

    static (Control Field, string Message)? ValidateIdentityFields(bool userTarget, bool create,
        (Control Name, Control Display, Control Email, Control Password) fields)
    {
        if (string.IsNullOrWhiteSpace(fields.Name.Text) || fields.Name.Text.Length > 100)
            return (fields.Name, "Tên phải có từ 1 đến 100 ký tự.");
        if (userTarget && (string.IsNullOrWhiteSpace(fields.Display.Text) || fields.Display.Text.Length > 100))
            return (fields.Display, "Tên hiển thị phải có từ 1 đến 100 ký tự.");
        if (userTarget && fields.Email.Text.Length > 254)
            return (fields.Email, "Email không được vượt quá 254 ký tự.");
        if (userTarget && create && (fields.Password.Text.Length == 0 || Encoding.UTF8.GetByteCount(fields.Password.Text) > 72))
            return (fields.Password, "Mật khẩu tạm phải có từ 1 đến 72 byte UTF-8.");
        return null;
    }

    async Task<bool> ExecuteIdentityMutationAsync(bool userTarget, bool create, int targetId, long? expectedRevision,
        (Control Name, Control Display, Control Email, Control Password) fields, MutationDialogCloseGuard closeGuard)
    {
        closeGuard.Begin();
        try
        {
            return await Vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(
                userTarget ? SecurityAdministrationTargetDto.User : SecurityAdministrationTargetDto.Role,
                create ? SecurityAdministrationOperationDto.Create : SecurityAdministrationOperationDto.Update,
                targetId, name: fields.Name.Text, displayName: userTarget ? fields.Display.Text : null,
                email: userTarget ? fields.Email.Text : null, password: userTarget && create ? fields.Password.Text : null,
                expectedRevision: expectedRevision), default);
        }
        finally { closeGuard.End(); }
    }

    Control? PresentIdentityFeedback(Label validation, Button reload,
        (Control Name, Control Display, Control Email, Control Password) fields)
    {
        var feedback = Vm.LastMutationFeedback;
        validation.Text = feedback?.Message ?? "Không thể lưu thay đổi trong trạng thái hiện tại.";
        reload.Visible = Vm.RequiresRecovery;
        return feedback?.Field switch
        {
            "name" => fields.Name, "displayName" => fields.Display, "email" => fields.Email,
            "password" => fields.Password, _ => null
        };
    }

    async Task PasswordAsync()
    {
        if (!await AdmitAsync() || Vm.SelectedUser is not { IsProtectedAccount: false } user) return;
        using var dialog = new MutationDialog { Text = "Đặt lại mật khẩu — " + user.DisplayName, Width = 420, Height = 230, StartPosition = FormStartPosition.CenterParent };
        var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        var password = new TextBox { Width = 360, UseSystemPasswordChar = true, AccessibleName = "Mật khẩu mới" };
        var validation = new Label { AutoSize = true, AccessibleName = "Lỗi mật khẩu" };
        var reload = new Button { Text = "Tải lại để đối chiếu", AutoSize = true, Visible = false };
        var save = new Button { Text = "Xác nhận", AutoSize = true };
        var cancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
        fields.Controls.Add(new Label { Text = "Mật khẩu mới", AutoSize = true, Margin = new Padding(3, 6, 3, 0) });
        fields.Controls.Add(password); fields.Controls.Add(validation); fields.Controls.Add(reload); fields.Controls.Add(save); fields.Controls.Add(cancel);
        dialog.Controls.Add(fields);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        var closeGuard = new MutationDialogCloseGuard(dialog, cancel, validation, _control, Vm);
        reload.Click += async (_, _) => await ReloadDialogAsync(dialog, validation, reload);
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
                closeGuard.Begin();
                bool saved;
                try
                {
                    saved = await Vm.ExecuteMutationAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetPassword, user.UserId, password: password.Text, expectedRevision: user.AssignmentRevision), default);
                }
                finally { closeGuard.End(); }
                if (saved) dialog.Close();
                else if (!dialog.IsDisposed)
                {
                    validation.Text = Vm.LastMutationFeedback?.Message ?? "Không thể lưu thay đổi trong trạng thái hiện tại.";
                    reload.Visible = Vm.RequiresRecovery;
                    (Vm.RequiresRecovery ? (Control)reload : password).Focus();
                }
            }
            finally { password.Clear(); if (!dialog.IsDisposed) save.Enabled = !Vm.RequiresRecovery; }
        };
        BusinessCompactAppearance.Apply(dialog);
        try { dialog.ShowDialog(_control); }
        finally { password.Clear(); }
    }

    sealed class MutationDialog : Form
    {
        internal Func<bool>? ShouldBlockClose { get; set; }

        protected override void WndProc(ref Message message)
        {
            // Block native X / Alt+F4 before WinForms can set a modal Cancel result.
            if (ShouldBlockClose?.Invoke() == true && (message.Msg == 0x0010 ||
                message.Msg == 0x0112 && (message.WParam.ToInt64() & 0xFFF0) == 0xF060)) return;
            base.WndProc(ref message);
        }
    }

    sealed class MutationDialogCloseGuard
    {
        readonly MutationDialog _dialog;
        readonly Button _cancel;
        readonly Label _status;
        readonly SecurityWorkspaceControl _owner;
        readonly SecurityWorkspaceViewModel _viewModel;
        bool _pending;

        internal MutationDialogCloseGuard(MutationDialog dialog, Button cancel, Label status, SecurityWorkspaceControl owner, SecurityWorkspaceViewModel viewModel)
        {
            _dialog = dialog;
            _cancel = cancel;
            _status = status;
            _owner = owner;
            _viewModel = viewModel;
            dialog.ShouldBlockClose = () => _pending && !_owner.IsDisposed && _viewModel.IsCurrent;
            dialog.FormClosing += (_, args) =>
            {
                if (_pending && !_owner.IsDisposed && _viewModel.IsCurrent)
                {
                    args.Cancel = true;
                    _dialog.DialogResult = DialogResult.None;
                }
            };
        }

        internal void Begin()
        {
            _pending = true;
            _dialog.ControlBox = false;
            _status.Text = "Đang lưu…";
            _cancel.Enabled = false;
            _dialog.CancelButton = null;
        }

        internal void End()
        {
            _pending = false;
            if (_dialog.IsDisposed) return;
            _dialog.ControlBox = true;
            _cancel.Enabled = true;
            _dialog.CancelButton = _cancel;
        }
    }

    async Task ReloadDialogAsync(Form dialog, Label validation, Button reload)
    {
        reload.Enabled = false;
        try
        {
            if (await Vm.ReloadForComparisonAsync(default) && Vm.CanReviewRecovery)
                dialog.Close();
            else if (!dialog.IsDisposed)
                validation.Text = "Chưa tải lại được dữ liệu có quyền truy cập. Hãy thử lại hoặc hủy.";
        }
        finally { if (!dialog.IsDisposed) reload.Enabled = true; }
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
    Button Add(FlowLayoutPanel panel, string text, Func<Task> action, bool themed = true)
    {
        Button button = themed
            ? new BootstrapButton { Text = text, AutoSize = true, Variant = BootstrapVariant.Primary, Outline = true, ButtonSize = BootstrapButtonSize.Small }
            : new Button { Text = text, AutoSize = true };
        panel.Controls.Add(button);
        button.Click += async (_, _) => await _control.TrackOperation(action());
        return button;
    }
}

internal sealed class RoleGrantGrid : BootstrapDataGridView
{
    protected override void WndProc(ref Message message)
    {
        // Only the app-owned text-cell fallback needs a repeat guard. Keep native checkbox messages intact.
        if (message.Msg == 0x0100 && message.WParam.ToInt32() == (int)Keys.Space &&
            (message.LParam.ToInt64() & (1L << 30)) != 0 && CurrentCell?.ColumnIndex > 0) return;
        base.WndProc(ref message);
    }
}
