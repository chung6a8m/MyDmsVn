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

internal sealed class SalesLabForm : UiLabHostForm
{
    private readonly Button _failSave = new Button { Text = "Fail next Save", AutoSize = true };
    private readonly Button _armRace = new Button { Text = "Arm lookup A/B", AutoSize = true };
    private readonly Button _completeB = new Button { Text = "Complete B", AutoSize = true };
    private readonly Button _completeA = new Button { Text = "Complete A", AutoSize = true };
    private readonly ComboBox _permissionVariant = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly Button _applyPermission = new Button { Text = "Apply access", AutoSize = true };
    private readonly Button _holdReload = new Button { Text = "Hold next Reload", AutoSize = true };
    private readonly Button _releaseReload = new Button { Text = "Release Reload", AutoSize = true };
    private readonly HashSet<Task> _permissionRefreshes = new HashSet<Task>();
    private long _permissionRefreshGeneration;
    private SalesScenarioData? _data;
    private SalesReceiptControl? _control;
    private ScenarioLookupRace? _race;
    private ScenarioGate<ApiResponse<SalesReceiptDto>>? _reloadGate;
    internal SalesScenarioData? CurrentScenarioData => _data;
    internal SalesReceiptControl? CurrentSalesControl => _control;
    internal Button HoldReloadButton => _holdReload;
    internal Button ReleaseReloadButton => _releaseReload;
    internal Button ArmRaceButton => _armRace;
    internal Button CompleteAButton => _completeA;
    internal Button CompleteBButton => _completeB;
    internal Button ApplyAccessButton => _applyPermission;
    internal ComboBox PermissionVariantSelector => _permissionVariant;
    public SalesLabForm() : this(PromptCloseAsync) { }
    internal SalesLabForm(Func<WorkspaceCloseState, CancellationToken, Task<WorkspaceCloseChoice>> prompt) : base(prompt)
    {
        Text = "MyDmsVn Sales UI Lab";
        _permissionVariant.Items.AddRange(new object[] { "Allow all", "Deny Read", "Deny Write", "Deny Post" });
        _permissionVariant.SelectedIndex = 0;
        ConfigureScenarios(typeof(SalesScenarioId), _failSave, _armRace, _completeB, _completeA, _permissionVariant, _applyPermission, _holdReload, _releaseReload);
        _failSave.Click += (_, __) => ArmSaveError();
        _armRace.Click += (_, __) => ArmLookupRace();
        _completeB.Click += (_, __) => CompleteRace(false);
        _completeA.Click += (_, __) => CompleteRace(true);
        _applyPermission.Click += async (_, __) =>
        {
            var operation = ApplyPermissionAsync();
            _permissionRefreshes.Add(operation);
            try { await operation; }
            finally { _permissionRefreshes.Remove(operation); }
        };
        _holdReload.Click += (_, __) => ArmBusyReload();
        _releaseReload.Click += (_, __) => ReleaseBusyReload();
    }
    protected override IWorkspaceCloseParticipant? Participant => _control;
    protected override bool HasGraph => _data != null || _control != null;
    protected override async Task CreateModuleAsync(SynchronizationContext context, CancellationToken token)
    {
        _race = null; _reloadGate = null;
        _data = LabScenarioFactory.CreateSales((SalesScenarioId)_scenario.SelectedItem!);
        var ui = new SynchronizationContextUiDispatcher(context);
        var data = _data;
        _control = new SalesReceiptControl(data.SalesApi, data.Session, ui, data.Preview, kind => data.CreateLookup(kind, ui), data.Permissions, data.ProductApi);
        _control.SaleDateEditor.ReferenceDateProvider = () => SalesScenarioData.ReferenceDate;
        _control.FromDateEditor.ReferenceDateProvider = () => SalesScenarioData.ReferenceDate;
        _control.ToDateEditor.ReferenceDateProvider = () => SalesScenarioData.ReferenceDate;
        _workspace.Controls.Add(_control);
        await _control.ActivateAsync(token);
        if (data.InitialSaleId.HasValue) await _control.SelectReceiptAsync(data.InitialSaleId.Value, token);
        else _control.NewButton.PerformClick();
        _status.Text = _scenario.SelectedItem + " — ready";
    }
    protected override async Task TearDownModuleAsync()
    {
        _data?.CancelPending();
        var refreshes = Task.WhenAll(_permissionRefreshes.ToArray());
        if (await Task.WhenAny(refreshes, Task.Delay(TimeSpan.FromSeconds(5))) != refreshes)
            throw new TimeoutException("Permission refresh did not drain; reset stopped.");
        await refreshes;
        if (_control != null)
        {
            var pending = _control.LastOperation;
            if (await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(5))) != pending) throw new TimeoutException("The previous workspace did not drain; reset stopped.");
            try { await pending; } catch (OperationCanceledException) { }
            if (_data != null) await _data.DrainAsync(TimeSpan.FromSeconds(5));
            _workspace.Controls.Remove(_control);
            _control.Dispose();
            _control = null;
        }
        _data?.Dispose(); _data = null;
        _race = null; _reloadGate = null;
    }
    protected override void ReplaceSession() => _data?.Session.Replace();
    protected override void RefreshDiagnostics()
    {
        if (_data == null) { _journal.Clear(); return; }
        _journal.Lines = _data.Journal.Entries.Select(entry => entry.Sequence + " " + entry.Operation).ToArray();
    }
    protected override void UpdateModuleCommands(bool enabled)
    {
        _failSave.Enabled = enabled && _control != null;
        _armRace.Enabled = enabled && _control != null && _data?.Id == SalesScenarioId.S03LookupRace && _race == null;
        _completeA.Enabled = enabled && _race != null && !_race.A.IsCompleted;
        _completeB.Enabled = enabled && _race != null && !_race.B.IsCompleted;
        _permissionVariant.Enabled = enabled && _control != null;
        _applyPermission.Enabled = enabled && _control?.CanRefreshPermissions == true;
        _holdReload.Enabled = enabled && _control != null && _reloadGate == null;
        _releaseReload.Enabled = enabled && _reloadGate != null && !_reloadGate.IsCompleted;
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
        var data = _data;
        var control = _control;
        var lifetime = _lifetime;
        var version = data.Session.Version;
        var generation = ++_permissionRefreshGeneration;
        var variant = _permissionVariant.Text;
        bool Current() => generation == _permissionRefreshGeneration && !_poisoned && !lifetime.IsCancellationRequested && !IsDisposed &&
            ReferenceEquals(data, _data) && ReferenceEquals(control, _control) &&
            ReferenceEquals(lifetime, _lifetime) && data.Session.Version == version;
        var keys = new[] { "Sales.Receipts.Read", "Sales.Receipts.Write", "Sales.Receipts.Post" };
        foreach (var key in keys) data.Permissions.Set(key, true);
        if (_permissionVariant.SelectedIndex > 0) data.Permissions.Set(keys[_permissionVariant.SelectedIndex - 1], false);
        try { await control.ActivateAsync(lifetime.Token); if (Current()) _status.Text = variant + " applied."; }
        catch (Exception exception) { if (Current()) _status.Text = "Access refresh failed: " + exception.Message; }
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

}
