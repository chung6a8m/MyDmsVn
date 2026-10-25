using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

public sealed partial class SecurityWorkspaceControl
{
    private Func<string?> _selectImportJson = SelectImportJson;
    private Func<string, bool> _confirmImport = ConfirmImport;
    private readonly Label _userImportStatus = new() { Dock = DockStyle.Bottom, Height = 28 };
    private readonly Label _roleImportStatus = new() { Dock = DockStyle.Bottom, Height = 28 };

    public Button UserImportButton { get; } = new() { Text = "Nhập quyền từ JSON…", AutoSize = true };
    public Button RoleImportButton { get; } = new() { Text = "Nhập quyền từ JSON…", AutoSize = true };

    public void SetImportDialogs(Func<string?> selectImportJson, Func<string, bool> confirmImport)
    {
        _selectImportJson = selectImportJson ?? throw new ArgumentNullException(nameof(selectImportJson));
        _confirmImport = confirmImport ?? throw new ArgumentNullException(nameof(confirmImport));
    }

    private void InitializeImportBindings()
    {
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
        _userImportStatus.Text = status;
        _roleImportStatus.Text = status;
        _roleImportStatus.Visible = !string.IsNullOrWhiteSpace(status);
        UserImportButton.Enabled = !ViewModel.IsBusy && !ViewModel.RequiresRecovery &&
            ViewModel.SelectedUser is { IsActive: true, IsProtectedAccount: false } &&
            ViewModel.Catalog.IsCurrent(ViewModel.OwnerVersion);
        RoleImportButton.Enabled = !ViewModel.IsBusy && !ViewModel.RequiresRecovery &&
            ViewModel.SelectedRole is { IsActive: true } &&
            ViewModel.Catalog.IsCurrent(ViewModel.OwnerVersion);
    }

    private async Task RunImportAsync(PermissionImportTargetTypeDto targetType)
    {
        if (!await AdmitNavigationAsync()) return;
        string? json;
        try { json = _selectImportJson(); }
        catch { _userImportStatus.Text = _roleImportStatus.Text = "Không đọc được tệp JSON"; return; }
        if (json == null) return;
        if (!await ViewModel.PreviewImportAsync(targetType, json, CancellationToken.None)) return;

        var preview = ViewModel.ImportPreview;
        if (preview == null) return;
        var additions = preview.Additions.Take(10).Select(item => item.PermissionKey).ToArray();
        var summary = "Đối tượng: " + preview.TargetName + " (#" + preview.Import.TargetId + ")" + Environment.NewLine +
            "Quyền sẽ thêm: " + preview.Additions.Count + Environment.NewLine +
            string.Join(Environment.NewLine, additions);
        if (preview.Additions.Count > additions.Length)
            summary += Environment.NewLine + "… và " + (preview.Additions.Count - additions.Length) + " quyền khác";

        var confirmed = _confirmImport(summary);
        await ViewModel.ApplyImportPreviewAsync(targetType, json, confirmed, CancellationToken.None);
    }

    private static string? SelectImportJson()
    {
        using var dialog = new OpenFileDialog { Title = "Nhập quyền từ JSON", Filter = "JSON (*.json)|*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog() != DialogResult.OK) return null;
        var file = new FileInfo(dialog.FileName);
        if (file.Length > PermissionAssignmentImportLimits.MaximumUtf8Bytes)
            throw new InvalidDataException("JSON exceeds the supported limit.");
        return File.ReadAllText(dialog.FileName, new UTF8Encoding(false, true));
    }

    private static bool ConfirmImport(string summary) =>
        MessageBox.Show(summary, "Xem trước nhập quyền", MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question) == DialogResult.OK;
}
