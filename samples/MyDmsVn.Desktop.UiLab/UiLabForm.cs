using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms;

namespace MyDmsVn.Desktop.UiLab;

internal sealed class UiLabForm : Form
{
    private readonly ComboBox _scenario = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly Button _reset = new Button { Text = "Reset", AutoSize = true };
    private readonly Button _closeWorkspace = new Button { Text = "Close workspace", AutoSize = true };
    private readonly Button _replaceSession = new Button { Text = "Replace session", AutoSize = true };
    private readonly Button _failSave = new Button { Text = "Fail next Save", AutoSize = true };
    private readonly Button _armRace = new Button { Text = "Arm lookup A/B", AutoSize = true };
    private readonly Button _completeB = new Button { Text = "Complete B", AutoSize = true };
    private readonly Button _completeA = new Button { Text = "Complete A", AutoSize = true };
    private readonly ComboBox _permissionVariant = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly Button _applyPermission = new Button { Text = "Apply access", AutoSize = true };
    private readonly Button _holdReload = new Button { Text = "Hold next Reload", AutoSize = true };
    private readonly Button _releaseReload = new Button { Text = "Release Reload", AutoSize = true };
    private readonly Label _status = new Label { AutoSize = true, Text = "Starting...", Padding = new Padding(8) };
    private readonly TextBox _journal = new TextBox { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = 110 };
    private readonly Panel _workspace = new Panel { Dock = DockStyle.Fill };
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

    internal SalesScenarioData? CurrentScenarioData => _data;
    internal SalesReceiptControl? CurrentSalesControl => _control;
    internal bool IsResetting => _resetting;
    internal bool IsPoisoned => _poisoned;
    internal Task ResetForTestAsync() => ResetSafelyAsync();

    public UiLabForm()
    {
        Text = "MyDmsVn Sales UI Lab";
        Size = new Size(1250, 850);
        StartPosition = FormStartPosition.CenterScreen;
        _closeCoordinator = new WorkspaceCloseCoordinator(PromptCloseAsync);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 68, WrapContents = true, Padding = new Padding(6) };
        toolbar.Controls.AddRange(new Control[] { _scenario, _reset, _closeWorkspace, _replaceSession,
            _failSave, _armRace, _completeB, _completeA, _permissionVariant, _applyPermission,
            _holdReload, _releaseReload, _status });
        Controls.Add(_workspace);
        Controls.Add(_journal);
        Controls.Add(toolbar);
        _scenario.Items.AddRange(Enum.GetValues(typeof(SalesScenarioId)).Cast<object>().ToArray());
        _scenario.SelectedIndex = 0;
        _permissionVariant.Items.AddRange(new object[] { "Allow all", "Deny Read", "Deny Write", "Deny Post" });
        _permissionVariant.SelectedIndex = 0;
        _reset.Click += async (_, __) => await ResetSafelyAsync();
        _scenario.SelectedIndexChanged += async (_, __) => { if (IsHandleCreated) await ResetSafelyAsync(); };
        _closeWorkspace.Click += async (_, __) => await CloseWorkspaceSafelyAsync();
        _replaceSession.Click += (_, __) => { _data?.Session.Replace(); RefreshDiagnostics(); };
        _failSave.Click += (_, __) => ArmSaveError();
        _armRace.Click += (_, __) => ArmLookupRace();
        _completeB.Click += (_, __) => CompleteRace(false);
        _completeA.Click += (_, __) => CompleteRace(true);
        _applyPermission.Click += async (_, __) => await ApplyPermissionAsync();
        _holdReload.Click += (_, __) => ArmBusyReload();
        _releaseReload.Click += (_, __) => ReleaseBusyReload();
        _diagnostics.Tick += (_, __) => RefreshDiagnostics();
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
            var context = SynchronizationContext.Current as WindowsFormsSynchronizationContext
                ?? throw new InvalidOperationException("UI Lab requires a running Windows Forms synchronization context.");
            _lifetime = new CancellationTokenSource();
            _race = null; _reloadGate = null;
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
        if (_resetting || _closingWorkspace || _control == null) return;
        _closingWorkspace = true;
        SetCommands(false);
        try
        {
            var decision = await _closeCoordinator.RequestCloseAsync(_control, _lifetime!.Token);
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
            "Sales UI Lab", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return Task.FromResult(answer == DialogResult.Yes ? WorkspaceCloseChoice.Save :
            answer == DialogResult.No ? WorkspaceCloseChoice.Discard : WorkspaceCloseChoice.Cancel);
    }

    private void RefreshDiagnostics()
    {
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
        var keys = new[] { "Sales.Receipts.Read", "Sales.Receipts.Write", "Sales.Receipts.Post" };
        foreach (var key in keys) _data.Permissions.Set(key, true);
        if (_permissionVariant.SelectedIndex > 0) _data.Permissions.Set(keys[_permissionVariant.SelectedIndex - 1], false);
        try { await _control.ActivateAsync(_lifetime.Token); _status.Text = _permissionVariant.Text + " applied."; }
        catch (Exception exception) { _status.Text = "Access refresh failed: " + exception.Message; }
    }

    private void ArmBusyReload()
    {
        if (_data == null) return;
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

    private void SetCommands(bool enabled)
    {
        _scenario.Enabled = enabled;
        _reset.Enabled = enabled;
        _closeWorkspace.Enabled = enabled && _control != null;
        _replaceSession.Enabled = enabled && _control != null;
        _failSave.Enabled = enabled && _control != null;
        _armRace.Enabled = enabled && _control != null && _data?.Id == SalesScenarioId.S03LookupRace;
        _completeA.Enabled = enabled && _race != null && !_race.A.IsCompleted;
        _completeB.Enabled = enabled && _race != null && !_race.B.IsCompleted;
        _permissionVariant.Enabled = enabled && _control != null;
        _applyPermission.Enabled = enabled && _control != null;
        _holdReload.Enabled = enabled && _control != null;
        _releaseReload.Enabled = enabled && _reloadGate != null && !_reloadGate.IsCompleted;
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
        if (_control != null)
        {
            await CloseWorkspaceSafelyAsync();
            if (_control != null) return;
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
