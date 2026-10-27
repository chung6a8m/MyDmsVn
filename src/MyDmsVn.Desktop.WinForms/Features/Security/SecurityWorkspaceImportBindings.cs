using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

public sealed partial class SecurityWorkspaceControl
{
    private Func<string?> _selectImportJson = null!;
    private Func<string, bool> _confirmImport = null!;
    private PermissionImportTargetTypeDto? _importTarget;
    private int? _importTargetId;
    private readonly Label _userImportStatus = new BootstrapLabel { Dock = DockStyle.Bottom, Height = 28, AccessibleName = "Trạng thái nhập quyền riêng", TextRole = BootstrapTextRole.Muted };
    private readonly Label _roleImportStatus = new BootstrapLabel { Dock = DockStyle.Bottom, Height = 28, AccessibleName = "Trạng thái nhập quyền vai trò", TextRole = BootstrapTextRole.Muted };

    public Button UserImportButton { get; } = new BootstrapButton { Text = "Nhập quyền từ JSON…", AutoSize = true, Outline = true, Variant = BootstrapVariant.Secondary, ButtonSize = BootstrapButtonSize.Small };
    public Button RoleImportButton { get; } = new BootstrapButton { Text = "Nhập quyền từ JSON…", AutoSize = true, Outline = true, Variant = BootstrapVariant.Secondary, ButtonSize = BootstrapButtonSize.Small };

    public void SetImportDialogs(Func<string?> selectImportJson, Func<string, bool> confirmImport)
    {
        _selectImportJson = selectImportJson ?? throw new ArgumentNullException(nameof(selectImportJson));
        _confirmImport = confirmImport ?? throw new ArgumentNullException(nameof(confirmImport));
    }

    private void InitializeImportBindings()
    {
        _selectImportJson = SelectImportJson;
        _confirmImport = ConfirmImport;
        var userPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false };
        userPanel.Controls.Add(UserImportButton);
        UserDetail.Tabs.TabPages[3].Controls.Add(userPanel);
        UserDetail.Tabs.TabPages[3].Controls.Add(_userImportStatus);

        var rolePanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false };
        rolePanel.Controls.Add(RoleImportButton);
        RoleDetail.Tabs.TabPages[0].Controls.Add(rolePanel);
        RoleDetail.Tabs.TabPages[0].Controls.Add(_roleImportStatus);

        UserImportButton.Click += (_, _) => TrackOperation(RunImportAsync(PermissionImportTargetTypeDto.User));
        RoleImportButton.Click += (_, _) => TrackOperation(RunImportAsync(PermissionImportTargetTypeDto.Role));
        ViewModel.Changed += ImportChanged;
        Disposed += (_, _) => ViewModel.Changed -= ImportChanged;
        ImportChanged(this, EventArgs.Empty);
    }

    private void ImportChanged(object? sender, EventArgs args)
    {
        var status = ViewModel.ImportStatus;
        // Direct ViewModel-driven Lab/review calls have no picker context. Present
        // their feedback in the visible object context, never in both detail panes.
        var target = _importTarget ?? (Tabs.SelectedIndex == 1 ? PermissionImportTargetTypeDto.Role : PermissionImportTargetTypeDto.User);
        _userImportStatus.Text = target == PermissionImportTargetTypeDto.User &&
            (_importTarget == null ||
            ViewModel.SelectedUserId == _importTargetId || ViewModel.SelectedUserId == null) ? status : "";
        _roleImportStatus.Text = target == PermissionImportTargetTypeDto.Role &&
            (_importTarget == null || ViewModel.SelectedRoleId == _importTargetId || ViewModel.SelectedRoleId == null) ? status : "";
        _userImportStatus.Visible = !string.IsNullOrWhiteSpace(_userImportStatus.Text);
        _roleImportStatus.Visible = !string.IsNullOrWhiteSpace(_roleImportStatus.Text);
        UserImportButton.Enabled = !ViewModel.IsBusy && !ViewModel.RequiresRecovery &&
            ViewModel.SelectedUser is { IsActive: true, IsProtectedAccount: false } &&
            ViewModel.Catalog.IsCurrent(ViewModel.OwnerVersion);
        RoleImportButton.Enabled = !ViewModel.IsBusy && !ViewModel.RequiresRecovery &&
            ViewModel.SelectedRole is { IsActive: true } &&
            ViewModel.Catalog.IsCurrent(ViewModel.OwnerVersion);
    }

    private async Task RunImportAsync(PermissionImportTargetTypeDto targetType)
    {
        var targetId = targetType == PermissionImportTargetTypeDto.User ? ViewModel.SelectedUserId : ViewModel.SelectedRoleId;
        if (!await AdmitNavigationAsync() || !ViewModel.IsCurrent || targetId == null ||
            (targetType == PermissionImportTargetTypeDto.User ? ViewModel.SelectedUserId : ViewModel.SelectedRoleId) != targetId) return;
        _importTarget = targetType;
        _importTargetId = targetId;
        string? json;
        try { json = _selectImportJson(); }
        catch
        {
            var status = targetType == PermissionImportTargetTypeDto.User ? _userImportStatus : _roleImportStatus;
            status.Text = "Không đọc được tệp JSON";
            status.Visible = true;
            return;
        }
        if (json == null) return;
        if (!ViewModel.IsCurrent || IsDisposed ||
            (targetType == PermissionImportTargetTypeDto.User ? ViewModel.SelectedUserId : ViewModel.SelectedRoleId) != targetId) return;
        if (!await ViewModel.PreviewImportAsync(targetType, json, CancellationToken.None)) return;

        var preview = ViewModel.ImportPreview;
        if (preview == null) return;
        var additions = preview.Additions.Take(10).Select(item => item.PermissionKey).ToArray();
        var summary = "Đối tượng: " + preview.TargetName + " (#" + preview.Import.TargetId + ")" + Environment.NewLine +
            "Quyền sẽ thêm: " + preview.Additions.Count + Environment.NewLine +
            "Revision: " + preview.TargetRevision + Environment.NewLine +
            "Catalog digest: " + preview.CatalogDigest + Environment.NewLine +
            string.Join(Environment.NewLine, additions);
        if (preview.Additions.Count > additions.Length)
            summary += Environment.NewLine + "… và " + (preview.Additions.Count - additions.Length) + " quyền khác";

        var confirmed = _confirmImport(summary);
        await ViewModel.ApplyImportPreviewAsync(targetType, json, confirmed, CancellationToken.None);
    }

    private string? SelectImportJson()
    {
        using var dialog = new OpenFileDialog { Title = "Nhập quyền từ JSON", Filter = "JSON (*.json)|*.json|All files (*.*)|*.*" };
        // BC-08: OS file-system navigation, shell integration and file selection are
        // not supplied by the pinned toolkit. Owner is this originating workspace.
        if (dialog.ShowDialog(this) != DialogResult.OK) return null;
        var file = new FileInfo(dialog.FileName);
        if (file.Length > PermissionAssignmentImportLimits.MaximumUtf8Bytes)
            throw new InvalidDataException("JSON exceeds the supported limit.");
        return File.ReadAllText(dialog.FileName, new UTF8Encoding(false, true));
    }

    private bool ConfirmImport(string summary)
    {
        using var dialog = new SecurityDecisionDialog("Xem trước nhập quyền", summary, "Áp dụng các quyền thêm");
        return dialog.ShowOwned(this) == DialogResult.OK;
    }
}
