using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.WinForms.Features.Security;

namespace MyDmsVn.Desktop.UiLab;

internal sealed class SecurityLabForm : UiLabHostForm
{
    private SecurityScenario? _securityData;
    private SecurityWorkspaceControl? _securityControl;
    private readonly Button _securityReviewToggle = new Button { Text = "Security review tools", AutoSize = true };
    private readonly FlowLayoutPanel _securityReviewPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 76, WrapContents = true, AutoScroll = true, Padding = new Padding(4), Visible = false };
    private readonly ComboBox _securityReadSource = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly ComboBox _securityReadOutcome = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
    private readonly CheckBox _securityHoldRead = new CheckBox { Text = "Hold read", AutoSize = true };
    private readonly CheckBox _securityIgnoreCancellation = new CheckBox { Text = "Ignore cancel", AutoSize = true };
    private readonly CheckBox _securityIgnoreOwnerRelease = new CheckBox { Text = "Ignore owner release", AutoSize = true };
    private readonly Button _securityArmRead = new Button { Text = "Arm read", AutoSize = true };
    private readonly Button _securityReload = new Button { Text = "Reload Security", AutoSize = true };
    private readonly Button _securityReleaseRead = new Button { Text = "Release read", AutoSize = true };
    private readonly ComboBox _securityMutationOutcome = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
    private readonly Button _securityArmMutation = new Button { Text = "Arm mutation", AutoSize = true };
    private readonly Button _securityReleaseMutation = new Button { Text = "Release mutation", AutoSize = true };
    private readonly Button _securityAdvanceMembershipUserRevision = new Button { Text = "Advance U101 revision", AutoSize = true };
    private readonly Button _securityAdvanceMembershipRoleRevision = new Button { Text = "Advance R202 revision", AutoSize = true };
    private readonly CheckBox _securityDenyMembership = new CheckBox { Text = "Deny membership writes", AutoSize = true };
    private readonly Button _securityHoldImportPreview = new Button { Text = "Hold next Preview", AutoSize = true };
    private readonly Button _securityReleaseImportPreview = new Button { Text = "Release Preview", AutoSize = true };
    private readonly Button _securityDenyImportPreview = new Button { Text = "Deny next Preview", AutoSize = true };
    private Action? _releaseSecurityRead;
    private Action? _releaseSecurityMutation;
    private ScenarioGate<bool>? _securityImportPreviewGate;

    internal SecurityScenario? CurrentSecurityScenarioData => _securityData;
    internal SecurityWorkspaceControl? CurrentSecurityControl => _securityControl;
    internal ComboBox SecurityReadSourceSelector => _securityReadSource;
    internal ComboBox SecurityReadOutcomeSelector => _securityReadOutcome;
    internal ComboBox SecurityMutationOutcomeSelector => _securityMutationOutcome;
    internal CheckBox SecurityHoldReadCheckBox => _securityHoldRead;
    internal CheckBox SecurityIgnoreCancellationCheckBox => _securityIgnoreCancellation;
    internal CheckBox SecurityIgnoreOwnerReleaseCheckBox => _securityIgnoreOwnerRelease;
    internal Button SecurityArmReadButton => _securityArmRead;
    internal Button SecurityReloadButton => _securityReload;
    internal Button SecurityReleaseReadButton => _securityReleaseRead;
    internal Button SecurityArmMutationButton => _securityArmMutation;
    internal Button SecurityReleaseMutationButton => _securityReleaseMutation;
    internal Button SecurityAdvanceMembershipUserRevisionButton => _securityAdvanceMembershipUserRevision;
    internal Button SecurityAdvanceMembershipRoleRevisionButton => _securityAdvanceMembershipRoleRevision;
    internal CheckBox SecurityDenyMembershipCheckBox => _securityDenyMembership;
    internal Button SecurityHoldImportPreviewButton => _securityHoldImportPreview;
    internal Button SecurityReleaseImportPreviewButton => _securityReleaseImportPreview;
    internal Button SecurityDenyImportPreviewButton => _securityDenyImportPreview;
    internal Button SecurityReviewToggleButton => _securityReviewToggle;
    public SecurityLabForm() : this(PromptCloseAsync) { }
    internal SecurityLabForm(Func<WorkspaceCloseState, CancellationToken, Task<WorkspaceCloseChoice>> prompt) : base(prompt)
    {
        Text = "MyDmsVn Security UI Lab";
        _journal.Height = 70;
        _securityReviewPanel.Controls.AddRange(new Control[] { _securityReadSource, _securityReadOutcome, _securityHoldRead, _securityIgnoreCancellation, _securityIgnoreOwnerRelease, _securityArmRead, _securityReload, _securityReleaseRead, _securityMutationOutcome, _securityArmMutation, _securityReleaseMutation, _securityHoldImportPreview, _securityReleaseImportPreview, _securityDenyImportPreview, _securityAdvanceMembershipUserRevision, _securityAdvanceMembershipRoleRevision, _securityDenyMembership });
        _toolbarHost.Controls.Add(_securityReviewPanel);
        _securityReadSource.Items.AddRange(new object[] { "Users", "Roles", "Catalog" }); _securityReadSource.SelectedIndex = 0;
        _securityReadOutcome.Items.AddRange(new object[] { "Success", "Empty", "Forbidden", "Failure" }); _securityReadOutcome.SelectedIndex = 0;
        _securityMutationOutcome.Items.AddRange(new object[] { "Success", "No-op grant", "Validation", "Forbidden", "Conflict", "Hold success", "Hold failure", "Uncertain" }); _securityMutationOutcome.SelectedIndex = 0;
        ConfigureScenarios(typeof(SecurityScenarioId), _securityReviewToggle);
        _securityReviewToggle.Click += (_, __) => ToggleSecurityReview();
        _securityArmRead.Click += (_, __) => ArmSecurityRead();
        _securityReload.Click += async (_, __) => await ReloadSecurityAsync();
        _securityReleaseRead.Click += (_, __) => ReleaseSecurityRead();
        _securityArmMutation.Click += (_, __) => ArmSecurityMutation();
        _securityReleaseMutation.Click += (_, __) => ReleaseSecurityMutation();
        _securityAdvanceMembershipUserRevision.Click += (_, __) => AdvanceSecurityMembershipRevision(PermissionImportTargetTypeDto.User, 101);
        _securityAdvanceMembershipRoleRevision.Click += (_, __) => AdvanceSecurityMembershipRevision(PermissionImportTargetTypeDto.Role, 202);
        _securityDenyMembership.CheckedChanged += (_, __) => { if (_securityData?.Id == SecurityScenarioId.SEC_L04) _securityData.Administration.DenyMembershipMutations = _securityDenyMembership.Checked; };
        _securityHoldImportPreview.Click += (_, __) => ArmSecurityImportPreview();
        _securityReleaseImportPreview.Click += (_, __) => ReleaseSecurityImportPreview();
        _securityDenyImportPreview.Click += (_, __) => ArmSecurityImportPreviewForbidden();
    }
    protected override IWorkspaceCloseParticipant? Participant => _securityControl;
    protected override bool HasGraph => _securityData != null || _securityControl != null;
    protected override async Task CreateModuleAsync(SynchronizationContext context, CancellationToken token)
    {
        _releaseSecurityRead = null; _releaseSecurityMutation = null; _securityImportPreviewGate = null; _securityDenyMembership.Checked = false;
        _securityData = LabScenarioFactory.CreateSecurity((SecurityScenarioId)_scenario.SelectedItem!);
        _securityControl = new SecurityWorkspaceControl(new SecurityWorkspaceViewModel(_securityData.Administration, _securityData.Catalog, _securityData.Session, new SynchronizationContextUiDispatcher(context), _securityData.Session.Version));
        _securityControl.SetImportDialogs(SelectSecurityImportJson, ConfirmSecurityImport);
        _workspace.Controls.Add(_securityControl);
        await _securityControl.ActivateAsync(token);
        _status.Text = _scenario.SelectedItem + " — ready (SQL-free Security)";
    }
    protected override async Task TearDownModuleAsync()
    {
        if (_securityData == null) return;
        _securityData.Dispose();
        if (_securityControl != null)
        {
            var pending = _securityControl.LastOperation;
            if (await Task.WhenAny(pending, Task.Delay(5000)) != pending) throw new TimeoutException("Security workspace did not drain.");
            try { await pending; } catch (OperationCanceledException) { }
        }
        if (!await _securityData.DrainAsync(5000)) throw new TimeoutException("Security fixture did not drain.");
        if (_securityControl != null) { _workspace.Controls.Remove(_securityControl); _securityControl.Dispose(); _securityControl = null; }
        _securityData = null;
        _releaseSecurityRead = null; _releaseSecurityMutation = null; _securityImportPreviewGate = null;
    }
    protected override void ReplaceSession() => _securityData?.Session.Replace();
    protected override void RefreshDiagnostics()
    {
        if (_securityData == null) { _journal.Clear(); return; }
        _journal.Lines = _securityData.Journal.Entries.Select(entry => entry.Sequence + " " + entry.Operation +
            (entry.Request is SecurityMutationJournalEntry || entry.Request is SecurityResponseJournalEntry ? " " + entry.Request : "")).ToArray();
    }
    protected override void TickModule() => UpdateModalMutationRelease();
    protected override void UpdateModuleCommands(bool enabled)
    {
        var pendingSecurityRelease = _releaseSecurityRead != null || _releaseSecurityMutation != null ||
            _securityImportPreviewGate != null && !_securityImportPreviewGate.IsCompleted;
        _securityReviewToggle.Enabled = (enabled || _poisoned || _closingWorkspace && pendingSecurityRelease) && _securityData != null;
        _securityReviewPanel.Enabled = _securityData != null && (!_resetting || _poisoned);
        _securityArmRead.Enabled = enabled && _securityControl != null && _releaseSecurityRead == null;
        _securityReload.Enabled = enabled && _securityControl != null;
        _securityReleaseRead.Enabled = _releaseSecurityRead != null;
        _securityArmMutation.Enabled = enabled && _securityControl != null && _releaseSecurityMutation == null;
        _securityReleaseMutation.Enabled = _releaseSecurityMutation != null;
        var membershipRevisionReview = _securityData?.Id == SecurityScenarioId.SEC_L04;
        _securityAdvanceMembershipUserRevision.Visible = membershipRevisionReview;
        _securityAdvanceMembershipRoleRevision.Visible = membershipRevisionReview;
        _securityDenyMembership.Visible = membershipRevisionReview;
        _securityAdvanceMembershipUserRevision.Enabled = enabled && membershipRevisionReview && _securityControl?.ViewModel.IsBusy != true;
        _securityAdvanceMembershipRoleRevision.Enabled = enabled && membershipRevisionReview && _securityControl?.ViewModel.IsBusy != true;
        _securityDenyMembership.Enabled = enabled && membershipRevisionReview && _securityControl?.ViewModel.IsBusy != true;
        _securityHoldImportPreview.Enabled = enabled && _securityControl != null && _securityImportPreviewGate == null &&
            _securityData?.Administration.DenyNextImportPreview != true;
        _securityReleaseImportPreview.Enabled = _securityImportPreviewGate != null && !_securityImportPreviewGate.IsCompleted;
        var importPreviewDenialReview = _securityData?.Id == SecurityScenarioId.SEC_L09;
        _securityDenyImportPreview.Visible = importPreviewDenialReview;
        _securityDenyImportPreview.Enabled = enabled && importPreviewDenialReview &&
            _securityImportPreviewGate == null && _securityData?.Administration.DenyNextImportPreview != true;
    }
    private void ToggleSecurityReview()
    {
        if (_securityData == null) return;
        _securityReviewPanel.Visible = !_securityReviewPanel.Visible;
        _toolbarHost.Height = _securityReviewPanel.Visible ? 168 : 68;
    }

    private static string? SelectSecurityImportJson()
    {
        using var dialog = new OpenFileDialog { Title = "Nhập quyền từ JSON", Filter = "JSON (*.json)|*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog() != DialogResult.OK) return null;
        if (new FileInfo(dialog.FileName).Length > PermissionAssignmentImportLimits.MaximumUtf8Bytes)
            throw new InvalidDataException("JSON exceeds the supported limit.");
        return File.ReadAllText(dialog.FileName, new UTF8Encoding(false, true));
    }

    private bool ConfirmSecurityImport(string summary)
    {
        var scene = _securityData;
        var preview = _securityControl?.ViewModel.ImportPreview;
        if (scene == null || preview == null) return false;
        var reviewSummary = summary + Environment.NewLine +
            "Preview revision: " + preview.TargetRevision + Environment.NewLine +
            "Preview catalog digest: " + preview.CatalogDigest;
        using var dialog = new SecurityImportReviewDialog(reviewSummary,
            () => scene.AdvanceImportTargetRevision(preview.Import.TargetType, preview.Import.TargetId),
            scene.RotateCatalogDigest,
            () =>
            {
                var targetType = preview.Import.TargetType;
                var alternateId = targetType == PermissionImportTargetTypeDto.Role ?
                    (preview.Import.TargetId == 201 ? 202 : 201) :
                    (preview.Import.TargetId == 101 ? 102 : 101);
                if (targetType == PermissionImportTargetTypeDto.Role)
                    _securityControl?.ViewModel.SelectRole(alternateId);
                else
                    _securityControl?.ViewModel.SelectUser(alternateId);
                scene.Journal.Record("Security.ReviewTargetSwitch", new
                    { Target = targetType, OriginalTargetId = preview.Import.TargetId, AlternateTargetId = alternateId });
            });
        BusinessCompactAppearance.Apply(dialog);
        return dialog.ShowDialog(this) == DialogResult.OK;
    }

    private static ApiError ReviewError(ApiStatusCode status) => new ApiError(status,
        status == ApiStatusCode.Conflict ? "Security.RevisionConflict" : status == ApiStatusCode.BadRequest ? "ValidationError" :
        status == ApiStatusCode.Forbidden ? "Forbidden" : "ReviewFailure", "UI Lab controlled response.");

    private ApiResponse<T> ReviewReadResult<T>(T success, T empty)
    {
        return _securityReadOutcome.Text switch
        {
            "Empty" => ApiResponse<T>.Success(empty),
            "Forbidden" => ApiResponse<T>.Failure(ReviewError(ApiStatusCode.Forbidden)),
            "Failure" => ApiResponse<T>.Failure(ReviewError(ApiStatusCode.InternalServerError)),
            _ => ApiResponse<T>.Success(success),
        };
    }

    private void ArmSecurityRead()
    {
        var scene = _securityData;
        if (scene == null || _releaseSecurityRead != null) return;
        var sourceAlreadyArmed = _securityReadSource.Text switch
        {
            "Users" => scene.Administration.UsersGate != null,
            "Roles" => scene.Administration.RolesGate != null,
            "Catalog" => scene.Catalog.Gate != null,
            _ => false,
        };
        if (sourceAlreadyArmed)
        {
            _status.Text = "This Security source is already armed. Reload or Reset before arming it again.";
            return;
        }
        bool hold = _securityHoldRead.Checked;
        bool ignoreCancellation = _securityIgnoreCancellation.Checked;
        bool ignoreOwnerRelease = _securityIgnoreOwnerRelease.Checked;
        switch (_securityReadSource.Text)
        {
            case "Users":
            {
                scene.Administration.UsersError = null;
                var gate = new ScenarioGate<ApiResponse<IReadOnlyList<SecurityUserDto>>> { IgnoreCancellation = ignoreCancellation, IgnoreOwnerRelease = ignoreOwnerRelease };
                scene.Administration.UsersGate = gate;
                var result = ReviewReadResult<IReadOnlyList<SecurityUserDto>>(scene.Data.Users.ToArray(), Array.Empty<SecurityUserDto>());
                if (hold) _releaseSecurityRead = () => gate.Complete(result);
                else gate.Complete(result);
                break;
            }
            case "Roles":
            {
                scene.Administration.RolesError = null;
                var gate = new ScenarioGate<ApiResponse<IReadOnlyList<SecurityRoleDto>>> { IgnoreCancellation = ignoreCancellation, IgnoreOwnerRelease = ignoreOwnerRelease };
                scene.Administration.RolesGate = gate;
                var result = ReviewReadResult<IReadOnlyList<SecurityRoleDto>>(scene.Data.Roles.ToArray(), Array.Empty<SecurityRoleDto>());
                if (hold) _releaseSecurityRead = () => gate.Complete(result);
                else gate.Complete(result);
                break;
            }
            case "Catalog":
            {
                scene.Catalog.Error = null;
                var gate = new ScenarioGate<ApiResponse<PermissionCatalogDto>> { IgnoreCancellation = ignoreCancellation, IgnoreOwnerRelease = ignoreOwnerRelease };
                scene.Catalog.Gate = gate;
                var empty = new PermissionCatalogDto(scene.Data.Catalog.ApplicationVersion, "empty-review-catalog", Array.Empty<PermissionCatalogItemDto>());
                var result = ReviewReadResult(scene.Data.Catalog, empty);
                if (hold) _releaseSecurityRead = () => gate.Complete(result);
                else gate.Complete(result);
                break;
            }
        }
        _status.Text = $"Security {_securityReadSource.Text}: next read {_securityReadOutcome.Text}{(hold ? " held" : "")}.";
        SetCommands(!_poisoned);
    }

    private async Task ReloadSecurityAsync()
    {
        if (_securityControl == null || _lifetime == null) return;
        var origin = _securityControl;
        try
        {
            if (!origin.ViewModel.CanNavigate)
            {
                if (!_poisoned && ReferenceEquals(origin, _securityControl)) _status.Text = "Security reload blocked; current state retained.";
                return;
            }
            await origin.ActivateAsync(_lifetime.Token);
            if (!_poisoned && ReferenceEquals(origin, _securityControl)) _status.Text = "Security reload completed.";
        }
        catch (OperationCanceledException) { if (!_poisoned && ReferenceEquals(origin, _securityControl)) _status.Text = "Security reload canceled."; }
        catch (Exception exception) { if (!_poisoned && ReferenceEquals(origin, _securityControl)) _status.Text = "Security reload failed: " + exception.Message; }
        finally { RefreshDiagnostics(); SetCommands(!_poisoned); }
    }

    private void ReleaseSecurityRead()
    {
        var release = _releaseSecurityRead;
        if (release == null) return;
        _releaseSecurityRead = null;
        release();
        if (!_poisoned) _status.Text = "Held Security read released.";
        SetCommands(!_poisoned);
    }

    private void ArmSecurityMutation()
    {
        var scene = _securityData;
        if (scene == null || _releaseSecurityMutation != null) return;
        // An explicit reviewer choice replaces the scenario's pending preset response.
        scene.Administration.MutationErrors.Clear();
        scene.Administration.NoOpNextRolePermission = _securityMutationOutcome.Text == "No-op grant";
        switch (_securityMutationOutcome.Text)
        {
            case "Validation": scene.Administration.MutationErrors.Enqueue(ReviewError(ApiStatusCode.BadRequest)); break;
            case "Forbidden": scene.Administration.MutationErrors.Enqueue(ReviewError(ApiStatusCode.Forbidden)); break;
            case "Conflict": scene.Administration.MutationErrors.Enqueue(ReviewError(ApiStatusCode.Conflict)); break;
            case "Hold success":
            case "Hold failure":
            case "Uncertain":
            {
                var gate = scene.Administration.HoldNextMutation(_securityIgnoreCancellation.Checked || _securityMutationOutcome.Text == "Uncertain");
                gate.IgnoreOwnerRelease = _securityMutationOutcome.Text != "Uncertain" && _securityIgnoreOwnerRelease.Checked;
                _releaseSecurityMutation = _securityMutationOutcome.Text switch
                {
                    "Hold failure" => () => gate.Complete(ApiResponse<SecurityAdministrationResultDto>.Failure(ReviewError(ApiStatusCode.Conflict))),
                    "Uncertain" => () => gate.Cancel(),
                    _ => () => gate.Complete(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(0, true))),
                };
                break;
            }
            default: scene.Administration.MutationErrors.Enqueue(null); break;
        }
        _status.Text = "Security next mutation: " + _securityMutationOutcome.Text + ".";
        SetCommands(!_poisoned);
    }

    private void ReleaseSecurityMutation()
    {
        var release = _releaseSecurityMutation;
        if (release == null) return;
        _releaseSecurityMutation = null;
        release();
        if (!_poisoned) _status.Text = "Held Security mutation released.";
        SetCommands(!_poisoned);
    }

    private void UpdateModalMutationRelease()
    {
        if (_releaseSecurityMutation == null && _securityData == null) return;
        var originControl = _securityControl;
        var originScene = _securityData;
        if (originControl == null || originScene == null || !ReferenceEquals(originControl.FindForm(), this)) return;
        var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form =>
            form.Modal && ReferenceEquals(form.Owner, this) && form.Text.StartsWith("Đặt lại mật khẩu — ", StringComparison.Ordinal));
        var fields = (dialog as MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapModal)?.FooterPanel;
        if (fields == null) return;
        var release = fields.Controls.OfType<Button>().FirstOrDefault(button => button.Name == "SecurityLabReleaseMutation");
        if (release == null && _releaseSecurityMutation != null)
        {
            release = new Button
            {
                Name = "SecurityLabReleaseMutation",
                Text = "Release mutation (Lab)",
                AccessibleName = "Release held Security mutation (Lab)",
                AutoSize = true,
                CausesValidation = false
            };
            var binding = new ModalReleaseBinding(originScene, originControl, originScene.Session.Version, _releaseSecurityMutation);
            release.Tag = binding;
            release.Click += (_, _) =>
            {
                if (ReferenceEquals(binding.Scene, _securityData) && ReferenceEquals(binding.Control, _securityControl) &&
                    ReferenceEquals(binding.Release, _releaseSecurityMutation)) ReleaseSecurityMutation();
            };
            fields.Controls.Add(release);
        }
        if (release?.Tag is ModalReleaseBinding current && ReferenceEquals(current.Scene, _securityData) &&
            ReferenceEquals(current.Control, _securityControl))
        {
            // Rebind a later gate only for this same graph and session. After session
            // replacement, the old button may release its old gate, never a new one.
            if (current.SessionVersion == originScene.Session.Version) current.Release = _releaseSecurityMutation;
            release.Enabled = current.Release != null && ReferenceEquals(current.Release, _releaseSecurityMutation) &&
                _securityControl?.ViewModel.IsBusy == true;
        }
    }

    private sealed class ModalReleaseBinding
    {
        internal ModalReleaseBinding(SecurityScenario scene, SecurityWorkspaceControl control, long sessionVersion, Action? release)
        { Scene = scene; Control = control; SessionVersion = sessionVersion; Release = release; }
        internal SecurityScenario Scene { get; }
        internal SecurityWorkspaceControl Control { get; }
        internal long SessionVersion { get; }
        internal Action? Release { get; set; }
    }

    private void ArmSecurityImportPreview()
    {
        if (_securityData == null || _securityImportPreviewGate != null ||
            _securityData.Administration.DenyNextImportPreview) return;
        _securityImportPreviewGate = _securityData.Administration.HoldNextImportPreview();
        _status.Text = "Next Security import Preview is held. Release it after replacing the session.";
        SetCommands(!_poisoned);
    }

    private void ArmSecurityImportPreviewForbidden()
    {
        if (_securityData?.Id != SecurityScenarioId.SEC_L09 || _securityControl == null ||
            _securityImportPreviewGate != null || _securityData.Administration.DenyNextImportPreview ||
            _resetting || _poisoned) return;
        _securityData.Administration.DenyNextImportPreview = true;
        _securityData.Journal.Record("Security.ReviewImportPreviewFault", new { Outcome = "Forbidden" });
        _status.Text = "Next Security import Preview: Forbidden.";
        SetCommands(!_poisoned);
    }

    private void ReleaseSecurityImportPreview()
    {
        var gate = _securityImportPreviewGate;
        if (gate == null) return;
        _securityImportPreviewGate = null;
        gate.Complete(true);
        if (!_poisoned) _status.Text = "Held Security import Preview released.";
        SetCommands(!_poisoned);
    }

    private void AdvanceSecurityMembershipRevision(PermissionImportTargetTypeDto type, int id)
    {
        if (_securityData?.Id != SecurityScenarioId.SEC_L04 || _securityControl == null ||
            _securityControl.ViewModel.IsBusy || _resetting || _poisoned) return;
        _securityData.AdvanceImportTargetRevision(type, id);
        _status.Text = $"Security {type} #{id}: fixture revision advanced.";
        RefreshDiagnostics();
    }

}
