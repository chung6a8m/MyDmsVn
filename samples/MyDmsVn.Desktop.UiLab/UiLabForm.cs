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

internal sealed class UiLabForm : Form
{
    private readonly ComboBox _module = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private SecurityScenario? _securityData;
    private SecurityWorkspaceControl? _securityControl;
    private readonly ComboBox _scenario = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly Button _theme = new Button { Text = "Light / Dark", AutoSize = true };
    private readonly Button _reset = new Button { Text = "Reset", AutoSize = true };
    private readonly ReceiptCommandButton _closeWorkspace = new ReceiptCommandButton
        { Text = "Close workspace", AutoSize = true, CausesValidation = false };
    private readonly Button _replaceSession = new Button { Text = "Replace session", AutoSize = true };
    private readonly Button _failSave = new Button { Text = "Fail next Save", AutoSize = true };
    private readonly Button _armRace = new Button { Text = "Arm lookup A/B", AutoSize = true };
    private readonly Button _completeB = new Button { Text = "Complete B", AutoSize = true };
    private readonly Button _completeA = new Button { Text = "Complete A", AutoSize = true };
    private readonly ComboBox _permissionVariant = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly Button _applyPermission = new Button { Text = "Apply access", AutoSize = true };
    private readonly Button _holdReload = new Button { Text = "Hold next Reload", AutoSize = true };
    private readonly Button _releaseReload = new Button { Text = "Release Reload", AutoSize = true };
    private readonly Button _securityReviewToggle = new Button { Text = "Security review tools", AutoSize = true };
    private readonly FlowLayoutPanel _securityReviewPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 76, WrapContents = true, AutoScroll = true, Padding = new Padding(4), Visible = false };
    private readonly Panel _toolbarHost = new Panel { Dock = DockStyle.Top, Height = 68 };
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
    private readonly Label _status = new Label { AutoSize = true, Text = "Starting...", Padding = new Padding(8) };
    private readonly TextBox _journal = new TextBox { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = 110 };
    private readonly Panel _workspace = new Panel { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel _viewportControls = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, WrapContents = false };
    private readonly Button _reviewMode = new Button { Name = "ReviewModeToggle", Text = "Review mode (F11)", AutoSize = true };
    private bool _reviewModeActive;
    private bool _restoreFitAfterReview;
    private readonly Panel _viewportHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly NumericUpDown _viewportWidth = new NumericUpDown { Name = "ReviewViewportWidth", Minimum = 1, Maximum = 10000, Value = 952, Width = 65, AccessibleName = "Measured content width" };
    private readonly NumericUpDown _viewportHeight = new NumericUpDown { Name = "ReviewViewportHeight", Minimum = 1, Maximum = 10000, Value = 657, Width = 65, AccessibleName = "Measured content height" };
    private readonly Button _viewportApply = new Button { Name = "ReviewViewportApply", Text = "Fixed viewport", AutoSize = true };
    private readonly Button _viewportFree = new Button { Text = "Fit Lab", AutoSize = true };
    private readonly Label _viewportDiagnostics = new Label { Name = "ViewportDiagnostics", Dock = DockStyle.Fill, AccessibleName = "Viewport diagnostics" };
    private bool _fixedViewport;
    private readonly System.Windows.Forms.Timer _diagnostics = new System.Windows.Forms.Timer { Interval = 300 };
    private readonly WorkspaceCloseCoordinator _closeCoordinator;
    private SalesScenarioData? _data;
    private SalesReceiptControl? _control;
    private CancellationTokenSource? _lifetime;
    private bool _resetting;
    private bool _closingWorkspace;
    private bool _admittedClose;
    private bool _poisoned;
    private ScenarioLookupRace? _race;
    private ScenarioGate<ApiResponse<SalesReceiptDto>>? _reloadGate;
    private Action? _releaseSecurityRead;
    private Action? _releaseSecurityMutation;
    private ScenarioGate<bool>? _securityImportPreviewGate;

    internal ComboBox ModuleSelector => _module;
    internal SecurityScenario? CurrentSecurityScenarioData => _securityData;
    internal SecurityWorkspaceControl? CurrentSecurityControl => _securityControl;
    internal SalesScenarioData? CurrentScenarioData => _data;
    internal SalesReceiptControl? CurrentSalesControl => _control;
    internal bool IsResetting => _resetting;
    internal bool IsPoisoned => _poisoned;
    internal Task ResetForTestAsync() => ResetSafelyAsync();
    internal ComboBox ScenarioSelector => _scenario;
    internal ReceiptCommandButton CloseWorkspaceButton => _closeWorkspace;
    internal Button HoldReloadButton => _holdReload;
    internal Button ReleaseReloadButton => _releaseReload;
    internal Button ArmRaceButton => _armRace;
    internal Button CompleteAButton => _completeA;
    internal Button CompleteBButton => _completeB;
    internal Button ApplyAccessButton => _applyPermission;
    internal ComboBox PermissionVariantSelector => _permissionVariant;
    internal string StatusText => _status.Text;
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
    internal string JournalText => _journal.Text;

    public UiLabForm() : this(PromptCloseAsync) { }

    internal UiLabForm(Func<WorkspaceCloseState, CancellationToken, Task<WorkspaceCloseChoice>> closePrompt)
    {
        BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(BootstrapThemeManager.CurrentTheme.Mode);
        Text = "MyDmsVn UI Lab";
        Size = new Size(1250, 850);
        StartPosition = FormStartPosition.CenterScreen;
        _closeCoordinator = new WorkspaceCloseCoordinator(closePrompt);
        _closeWorkspace.RetainEditorFocus = () => _control?.CaptureCloseState().IsDirty == true;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 68, WrapContents = true, Padding = new Padding(6) };
        toolbar.Controls.AddRange(new Control[] { _module, _scenario, _theme, _reset, _closeWorkspace, _replaceSession,
            _failSave, _armRace, _completeB, _completeA, _permissionVariant, _applyPermission,
            _holdReload, _releaseReload, _securityReviewToggle, _status });
        _securityReviewPanel.Controls.AddRange(new Control[] { _securityReadSource, _securityReadOutcome,
            _securityHoldRead, _securityIgnoreCancellation, _securityIgnoreOwnerRelease,
            _securityArmRead, _securityReload, _securityReleaseRead, _securityMutationOutcome,
            _securityArmMutation, _securityReleaseMutation, _securityHoldImportPreview, _securityReleaseImportPreview,
            _securityDenyImportPreview,
            _securityAdvanceMembershipUserRevision, _securityAdvanceMembershipRoleRevision, _securityDenyMembership });
        _toolbarHost.Controls.Add(_securityReviewPanel);
        _toolbarHost.Controls.Add(toolbar);
        _viewportControls.Controls.AddRange(new Control[] { new Label { Text = "Shell content W × H (measure):", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, _viewportWidth, _viewportHeight, _viewportApply, _viewportFree, _reviewMode });
        _viewportHost.Controls.Add(_workspace);
        Controls.Add(_viewportHost);
        Controls.Add(_journal);
        var diagnosticsPanel = new Panel { Dock = DockStyle.Bottom, Height = 42 };
        diagnosticsPanel.Controls.Add(_viewportDiagnostics);
        Controls.Add(diagnosticsPanel);
        Controls.Add(_viewportControls);
        Controls.Add(_toolbarHost);
        _reviewMode.Click += (_, __) => ToggleReviewMode();
        _viewportApply.Click += (_, __) => SetReviewViewport(true);
        _viewportFree.Click += (_, __) => SetReviewViewport(false);
        _viewportHost.Resize += (_, __) => UpdateViewportDiagnostics();
        _module.Items.AddRange(new object[] { "Sales", "Security" });
        _module.SelectedIndex = 0;
        _scenario.Items.AddRange(Enum.GetValues(typeof(SalesScenarioId)).Cast<object>().ToArray());
        _scenario.SelectedIndex = 0;
        _permissionVariant.Items.AddRange(new object[] { "Allow all", "Deny Read", "Deny Write", "Deny Post" });
        _permissionVariant.SelectedIndex = 0;
        _securityReadSource.Items.AddRange(new object[] { "Users", "Roles", "Catalog" });
        _securityReadSource.SelectedIndex = 0;
        _securityReadOutcome.Items.AddRange(new object[] { "Success", "Empty", "Forbidden", "Failure" });
        _securityReadOutcome.SelectedIndex = 0;
        _securityMutationOutcome.Items.AddRange(new object[] { "Success", "No-op grant", "Validation", "Forbidden", "Conflict", "Hold success", "Hold failure", "Uncertain" });
        _securityMutationOutcome.SelectedIndex = 0;
        _module.SelectedIndexChanged += async (_, __) => { if (_resetting) return; _scenario.Items.Clear(); _scenario.Items.AddRange(Enum.GetValues(_module.SelectedIndex == 1 ? typeof(SecurityScenarioId) : typeof(SalesScenarioId)).Cast<object>().ToArray()); _scenario.SelectedIndex = 0; if (IsHandleCreated) await ResetSafelyAsync(); };
        _theme.Click += (_, __) => BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(BootstrapThemeManager.CurrentTheme.Mode == BootstrapThemeMode.Light ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
        _reset.Click += async (_, __) => await ResetSafelyAsync();
        _scenario.SelectedIndexChanged += async (_, __) => { if (IsHandleCreated) await ResetSafelyAsync(); };
        _closeWorkspace.Click += async (_, __) => await CloseWorkspaceSafelyAsync();
        _replaceSession.Click += (_, __) => { _data?.Session.Replace(); _securityData?.Session.Replace(); RefreshDiagnostics(); };
        _failSave.Click += (_, __) => ArmSaveError();
        _armRace.Click += (_, __) => ArmLookupRace();
        _completeB.Click += (_, __) => CompleteRace(false);
        _completeA.Click += (_, __) => CompleteRace(true);
        _applyPermission.Click += async (_, __) => await ApplyPermissionAsync();
        _holdReload.Click += (_, __) => ArmBusyReload();
        _releaseReload.Click += (_, __) => ReleaseBusyReload();
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
        _diagnostics.Tick += (_, __) =>
        {
            RefreshDiagnostics();
            UpdateViewportDiagnostics();
            SetCommands(!_resetting && !_closingWorkspace && !_poisoned);
            UpdateModalMutationRelease();
        };
        Shown += async (_, __) => { _diagnostics.Start(); await ResetSafelyAsync(); };
        FormClosing += OnFormClosing;
    }

    private async Task ResetSafelyAsync()
    {
        if (_resetting || _poisoned) return;
        _resetting = true;
        SetCommands(false);
        try
        {
            await TearDownOrPoisonAsync();
            _journal.Height = _module.SelectedIndex == 1 ? 70 : 110;
            _releaseSecurityRead = null;
            _releaseSecurityMutation = null;
            _securityImportPreviewGate = null;
            _securityDenyMembership.Checked = false;
            var context = SynchronizationContext.Current as WindowsFormsSynchronizationContext
                ?? throw new InvalidOperationException("UI Lab requires a running Windows Forms synchronization context.");
            _lifetime = new CancellationTokenSource();
            _race = null; _reloadGate = null;
            if (_module.SelectedIndex == 1)
            {
                _securityData = SecurityScenarioCatalog.Create((SecurityScenarioId)_scenario.SelectedItem!);
                _securityControl = new SecurityWorkspaceControl(new SecurityWorkspaceViewModel(_securityData.Administration, _securityData.Catalog, _securityData.Session, new SynchronizationContextUiDispatcher(context), _securityData.Session.Version));
                _securityControl.SetImportDialogs(SelectSecurityImportJson, ConfirmSecurityImport);
                _workspace.Controls.Add(_securityControl);
                await _securityControl.ActivateAsync(_lifetime.Token);
                _status.Text = _scenario.SelectedItem + " — ready (SQL-free Security)";
                RefreshDiagnostics();
                return;
            }
            _securityReviewPanel.Visible = false;
            _toolbarHost.Height = 68;
            _data = SalesScenarioData.Create((SalesScenarioId)_scenario.SelectedItem!);
            var ui = new SynchronizationContextUiDispatcher(context);
            var data = _data;
            _control = new SalesReceiptControl(data.SalesApi, data.Session, ui,
                data.Preview, kind => data.CreateLookup(kind, ui), data.Permissions, data.ProductApi);
            _control.SaleDateEditor.ReferenceDateProvider = () => SalesScenarioData.ReferenceDate;
            _control.FromDateEditor.ReferenceDateProvider = () => SalesScenarioData.ReferenceDate;
            _control.ToDateEditor.ReferenceDateProvider = () => SalesScenarioData.ReferenceDate;
            _workspace.Controls.Add(_control);
            await _control.ActivateAsync(_lifetime.Token);
            if (data.InitialSaleId.HasValue) await _control.SelectReceiptAsync(data.InitialSaleId.Value, _lifetime.Token);
            else _control.NewButton.PerformClick();
            _status.Text = _scenario.SelectedItem + " — ready";
            RefreshDiagnostics();
        }
        catch (Exception exception)
        {
            if (!_poisoned)
            {
                try { await TearDownOrPoisonAsync(); }
                catch (Exception cleanup)
                {
                    _status.Text = "Reset cleanup unresolved: " + exception.Message + "; " + cleanup.Message;
                    return;
                }
            }
            _status.Text = "Reset failed: " + exception.Message;
        }
        finally { _resetting = false; SetCommands(!_poisoned); }
    }

    private async Task TearDownAsync()
    {
        _lifetime?.Cancel();
        _data?.CancelPending();
        if (_securityData != null)
        {
            _securityData.Dispose();
            if (_securityControl != null) {
                var pending = _securityControl.LastOperation;
                if (await Task.WhenAny(pending, Task.Delay(5000)) != pending) throw new TimeoutException("Security workspace did not drain.");
                try { await pending; } catch (OperationCanceledException) { }
            }
            if (!await _securityData.DrainAsync(5000)) throw new TimeoutException("Security fixture did not drain.");
            if (_securityControl != null)
            {
                _workspace.Controls.Remove(_securityControl);
                _securityControl.Dispose();
                _securityControl = null;
            }
            _securityData = null;
        }
        if (_control != null)
        {
            var pending = _control.LastOperation;
            var finished = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(5)));
            if (finished != pending) throw new TimeoutException("The previous workspace did not drain; reset stopped.");
            try { await pending; } catch (OperationCanceledException) { }
            if (_data != null) await _data.DrainAsync(TimeSpan.FromSeconds(5));
            _workspace.Controls.Remove(_control);
            _control.Dispose();
            _control = null;
        }
        _lifetime?.Dispose();
        _lifetime = null;
        _data?.Dispose();
        _data = null;
    }

    private async Task TearDownOrPoisonAsync()
    {
        try { await TearDownAsync(); }
        catch { _poisoned = true; throw; }
    }

    private async Task CloseWorkspaceSafelyAsync()
    {
        if (_resetting || _closingWorkspace || (_control == null && _securityControl == null)) return;
        _closingWorkspace = true;
        SetCommands(false);
        try
        {
            var decision = await _closeCoordinator.RequestCloseAsync((IWorkspaceCloseParticipant?)_securityControl ?? _control!, _lifetime!.Token);
            if (decision != WorkspaceCloseDecision.Admitted) { _status.Text = "Close rejected; edits retained."; return; }
            await TearDownOrPoisonAsync();
            _status.Text = "Workspace closed. Reset to reopen.";
        }
        catch (Exception exception) { _status.Text = "Close failed: " + exception.Message; }
        finally { _closingWorkspace = false; SetCommands(!_poisoned); }
    }

    private static Task<WorkspaceCloseChoice> PromptCloseAsync(WorkspaceCloseState _, CancellationToken token)
    {
        if (token.IsCancellationRequested) return Task.FromResult(WorkspaceCloseChoice.Cancel);
        var answer = MessageBox.Show("Save changes before closing? Yes = Save, No = Discard, Cancel = keep open.",
            "UI Lab", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return Task.FromResult(answer == DialogResult.Yes ? WorkspaceCloseChoice.Save :
            answer == DialogResult.No ? WorkspaceCloseChoice.Discard : WorkspaceCloseChoice.Cancel);
    }

    private void RefreshDiagnostics()
    {
        if (_securityData != null)
        {
            _journal.Lines = _securityData.Journal.Entries.Select(entry => entry.Sequence + " " + entry.Operation +
                (entry.Request is SecurityMutationJournalEntry || entry.Request is SecurityResponseJournalEntry
                    ? " " + entry.Request : "")).ToArray();
            return;
        }
        if (_data == null) { _journal.Clear(); return; }
        _journal.Lines = _data.Journal.Entries.Select(entry => entry.Sequence + " " + entry.Operation).ToArray();
    }

    private void ArmSaveError()
    {
        if (_data == null) return;
        _data.SalesApi.RejectNextSaveField("Request.Lines[0].Qty");
        _status.Text = "Next locally valid Save will receive a field error.";
    }

    private void ArmLookupRace()
    {
        if (_data == null) return;
        if (_race != null) { _status.Text = "Lookup A/B is already armed. Reset to arm it again."; return; }
        _race = _data.ArmLookupRace();
        _status.Text = "Type product search A, then B. Complete B before A.";
        SetCommands(true);
    }

    private void CompleteRace(bool first)
    {
        if (_race == null) return;
        if (first) _race.CompleteA(); else _race.CompleteB();
        _status.Text = first ? "A completed after B." : "B completed; now complete A.";
        SetCommands(true);
        RefreshDiagnostics();
    }

    private async Task ApplyPermissionAsync()
    {
        if (_data == null || _control == null || _lifetime == null) return;
        if (!_control.CanRefreshPermissions)
        {
            _status.Text = "Access not applied while the workspace is busy. Release the operation and apply again.";
            return;
        }
        var keys = new[] { "Sales.Receipts.Read", "Sales.Receipts.Write", "Sales.Receipts.Post" };
        foreach (var key in keys) _data.Permissions.Set(key, true);
        if (_permissionVariant.SelectedIndex > 0) _data.Permissions.Set(keys[_permissionVariant.SelectedIndex - 1], false);
        try { await _control.ActivateAsync(_lifetime.Token); _status.Text = _permissionVariant.Text + " applied."; }
        catch (Exception exception) { _status.Text = "Access refresh failed: " + exception.Message; }
    }

    private void ArmBusyReload()
    {
        if (_data == null) return;
        if (_reloadGate != null) { _status.Text = "Reload is already held. Reset to hold it again."; return; }
        _reloadGate = new ScenarioGate<ApiResponse<SalesReceiptDto>>();
        _data.SalesApi.EnqueueGetGate(_reloadGate);
        _status.Text = "Next Reload is held. Press Release Reload to return its response.";
        SetCommands(true);
    }

    private void ReleaseBusyReload()
    {
        if (_reloadGate == null || _data?.SalesApi.CurrentSale == null) return;
        _reloadGate.Complete(ApiResponse<SalesReceiptDto>.Success(_data.SalesApi.CurrentSale));
        _status.Text = "Held Reload released.";
        SetCommands(true);
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == Keys.F11) { ToggleReviewMode(); return true; }
        return base.ProcessCmdKey(ref message, keyData);
    }

    private void ToggleReviewMode()
    {
        _reviewModeActive = !_reviewModeActive;
        if (_reviewModeActive && !_fixedViewport)
        {
            _restoreFitAfterReview = true;
            var currentSize = _workspace.ClientSize;
            _workspace.Dock = DockStyle.None;
            _workspace.Location = Point.Empty;
            _workspace.ClientSize = currentSize;
        }
        else if (!_reviewModeActive && _restoreFitAfterReview)
        {
            _restoreFitAfterReview = false;
            _workspace.Dock = DockStyle.Fill;
        }
        _toolbarHost.Visible = !_reviewModeActive;
        _viewportControls.Visible = !_reviewModeActive;
        _journal.Visible = !_reviewModeActive;
        PerformLayout();
        UpdateViewportDiagnostics();
    }

    private void SetReviewViewport(bool fixedViewport)
    {
        _fixedViewport = fixedViewport;
        if (fixedViewport)
        {
            _workspace.Dock = DockStyle.None;
            _workspace.Location = Point.Empty;
            _workspace.ClientSize = new Size((int)_viewportWidth.Value, (int)_viewportHeight.Value);
        }
        else
        {
            _workspace.Dock = DockStyle.Fill;
        }
        _viewportHost.PerformLayout();
        UpdateViewportDiagnostics();
    }

    private void UpdateViewportDiagnostics()
    {
        var workArea = Screen.FromControl(this).WorkingArea;
        var clipped = (_fixedViewport || _restoreFitAfterReview) && (_workspace.Width > _viewportHost.ClientSize.Width || _workspace.Height > _viewportHost.ClientSize.Height);
        _viewportDiagnostics.Text = $"Outer: {Width}×{Height}; Client: {ClientSize.Width}×{ClientSize.Height}; Content: {_workspace.ClientSize.Width}×{_workspace.ClientSize.Height}; DeviceDpi: {_workspace.DeviceDpi}; Physical scale: unverified;\nWork area: {workArea.Width}×{workArea.Height}; " + (clipped ? "viewport clipped; scroll to inspect" : "viewport visible") + (_reviewModeActive ? "; F11 restores tools" : "");
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
        var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form =>
            form.Modal && form.Text.StartsWith("Đặt lại mật khẩu — ", StringComparison.Ordinal));
        var fields = dialog?.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
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
            release.Click += (_, _) => ReleaseSecurityMutation();
            fields.Controls.Add(release);
        }
        if (release != null) release.Enabled = _releaseSecurityMutation != null && _securityControl?.ViewModel.IsBusy == true;
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

    private void SetCommands(bool enabled)
    {
        _module.Enabled = enabled;
        _scenario.Enabled = enabled;
        _reset.Enabled = enabled;
        _closeWorkspace.Enabled = enabled && (_control != null || _securityControl != null);
        _replaceSession.Enabled = enabled && (_control != null || _securityControl != null);
        _failSave.Enabled = enabled && _control != null;
        _armRace.Enabled = enabled && _control != null && _data?.Id == SalesScenarioId.S03LookupRace && _race == null;
        _completeA.Enabled = enabled && _race != null && !_race.A.IsCompleted;
        _completeB.Enabled = enabled && _race != null && !_race.B.IsCompleted;
        _permissionVariant.Enabled = enabled && _control != null;
        _applyPermission.Enabled = enabled && _control?.CanRefreshPermissions == true;
        _holdReload.Enabled = enabled && _control != null && _reloadGate == null;
        _releaseReload.Enabled = enabled && _reloadGate != null && !_reloadGate.IsCompleted;
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

    private async void OnFormClosing(object? sender, FormClosingEventArgs args)
    {
        if (_admittedClose) return;
        args.Cancel = true;
        if (_resetting || _closingWorkspace) return;
        if (_poisoned)
        {
            _resetting = true;
            try
            {
                await TearDownOrPoisonAsync();
                _poisoned = false;
                _admittedClose = true;
                Close();
            }
            catch (Exception exception) { _status.Text = "Close cleanup unresolved: " + exception.Message; }
            finally { _resetting = false; }
            return;
        }
        if (_control != null || _securityControl != null)
        {
            await CloseWorkspaceSafelyAsync();
            if (_control != null || _securityControl != null) return;
        }
        else if (_data != null || _lifetime != null)
        {
            try { await TearDownOrPoisonAsync(); }
            catch (Exception exception) { _status.Text = "Close cleanup unresolved: " + exception.Message; return; }
        }
        _diagnostics.Stop();
        _admittedClose = true;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _diagnostics.Dispose();
        base.Dispose(disposing);
    }
}
