using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using MyDmsVn.Bootstrap5WinFormUI.Theme;

namespace MyDmsVn.Desktop.UiLab;

internal abstract class UiLabHostForm : Form
{
    protected readonly ComboBox _scenario = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly Button _theme = new Button { Text = "Light / Dark", AutoSize = true };
    private readonly Button _reset = new Button { Text = "Reset", AutoSize = true };
    protected readonly ReceiptCommandButton _closeWorkspace = new ReceiptCommandButton { Text = "Close workspace", AutoSize = true, CausesValidation = false };
    private readonly Button _replaceSession = new Button { Text = "Replace session", AutoSize = true };
    protected readonly Panel _toolbarHost = new Panel { Dock = DockStyle.Top, Height = 68 };
    private readonly Panel _toolbarScroll = new Panel { Dock = DockStyle.Top, Height = 68, AutoScroll = true };
    protected readonly FlowLayoutPanel _toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(6) };
    protected readonly Label _status = new Label { AutoSize = true, Text = "Starting...", Padding = new Padding(8) };
    protected readonly TextBox _journal = new TextBox { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = 110 };
    protected readonly Panel _workspace = new Panel { Dock = DockStyle.Fill };
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
    protected CancellationTokenSource? _lifetime;
    protected bool _resetting;
    protected bool _closingWorkspace;
    private bool _admittedClose;
    protected bool _poisoned;
    internal bool IsResetting => _resetting;
    internal bool IsPoisoned => _poisoned;
    internal Task ResetForTestAsync() => ResetSafelyAsync();
    internal ComboBox ScenarioSelector => _scenario;
    internal ReceiptCommandButton CloseWorkspaceButton => _closeWorkspace;
    internal string StatusText => _status.Text;
    internal string JournalText => _journal.Text;

    protected UiLabHostForm(Func<WorkspaceCloseState, CancellationToken, Task<WorkspaceCloseChoice>> closePrompt)
    {
        BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(BootstrapThemeManager.CurrentTheme.Mode);
        Text = "MyDmsVn UI Lab";
        Size = new Size(1250, 850);
        StartPosition = FormStartPosition.CenterScreen;
        _closeCoordinator = new WorkspaceCloseCoordinator(closePrompt);
        _closeWorkspace.RetainEditorFocus = () => Participant?.CaptureCloseState().IsDirty == true;
        _toolbar.Controls.AddRange(new Control[] { _scenario, _theme, _reset, _closeWorkspace, _replaceSession, _status });
        _toolbarScroll.Controls.Add(_toolbar);
        _toolbarHost.Controls.Add(_toolbarScroll);
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
        _theme.Click += (_, __) => BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(BootstrapThemeManager.CurrentTheme.Mode == BootstrapThemeMode.Light ? BootstrapThemeMode.Dark : BootstrapThemeMode.Light);
        _reset.Click += async (_, __) => await ResetSafelyAsync();
        _closeWorkspace.Click += async (_, __) => await CloseWorkspaceSafelyAsync();
        _replaceSession.Click += (_, __) => { ReplaceSession(); RefreshDiagnostics(); };
        _diagnostics.Tick += (_, __) => { RefreshDiagnostics(); UpdateViewportDiagnostics(); SetCommands(!_resetting && !_closingWorkspace && !_poisoned); TickModule(); };
        Shown += async (_, __) => { _diagnostics.Start(); await ResetSafelyAsync(); };
        FormClosing += OnFormClosing;
    }

    protected void ConfigureScenarios(Type type, params Control[] moduleTools)
    {
        _scenario.Items.AddRange(Enum.GetValues(type).Cast<object>().ToArray());
        _scenario.SelectedIndex = 0;
        foreach (var tool in moduleTools) _toolbar.Controls.Add(tool);
        _scenario.SelectedIndexChanged += async (_, __) => { if (IsHandleCreated) await ResetSafelyAsync(); };
    }
    protected abstract IWorkspaceCloseParticipant? Participant { get; }
    protected abstract bool HasGraph { get; }
    protected abstract Task CreateModuleAsync(SynchronizationContext context, CancellationToken token);
    protected abstract Task TearDownModuleAsync();
    protected abstract void ReplaceSession();
    protected abstract void RefreshDiagnostics();
    protected abstract void UpdateModuleCommands(bool enabled);
    protected virtual void TickModule() { }

    private async Task ResetSafelyAsync()
    {
        if (_resetting || _closingWorkspace || _poisoned) return;
        _resetting = true;
        SetCommands(false);
        try
        {
            await TearDownOrPoisonAsync();
            var context = SynchronizationContext.Current as WindowsFormsSynchronizationContext
                ?? throw new InvalidOperationException("UI Lab requires a running Windows Forms synchronization context.");
            _lifetime = new CancellationTokenSource();
            await CreateModuleAsync(context, _lifetime.Token);
            RefreshDiagnostics();
        }
        catch (Exception exception)
        {
            if (!_poisoned)
            {
                try { await TearDownOrPoisonAsync(); }
                catch (Exception cleanup) { _status.Text = "Reset cleanup unresolved: " + exception.Message + "; " + cleanup.Message; return; }
            }
            _status.Text = "Reset failed: " + exception.Message;
        }
        finally { _resetting = false; SetCommands(!_poisoned); }
    }
    private async Task TearDownOrPoisonAsync()
    {
        try
        {
            _lifetime?.Cancel();
            await TearDownModuleAsync();
            _lifetime?.Dispose();
            _lifetime = null;
        }
        catch { _poisoned = true; throw; }
    }
    private async Task CloseWorkspaceSafelyAsync()
    {
        if (_resetting || _closingWorkspace || Participant == null) return;
        _closingWorkspace = true;
        SetCommands(false);
        try
        {
            var decision = await _closeCoordinator.RequestCloseAsync(Participant, _lifetime!.Token);
            if (decision != WorkspaceCloseDecision.Admitted) { _status.Text = "Close rejected; edits retained."; return; }
            await TearDownOrPoisonAsync();
            _status.Text = "Workspace closed. Reset to reopen.";
        }
        catch (Exception exception) { _status.Text = "Close failed: " + exception.Message; }
        finally { _closingWorkspace = false; SetCommands(!_poisoned); }
    }
    protected static Task<WorkspaceCloseChoice> PromptCloseAsync(WorkspaceCloseState _, CancellationToken token)
    {
        if (token.IsCancellationRequested) return Task.FromResult(WorkspaceCloseChoice.Cancel);
        var answer = MessageBox.Show("Save changes before closing? Yes = Save, No = Discard, Cancel = keep open.",
            "UI Lab", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return Task.FromResult(answer == DialogResult.Yes ? WorkspaceCloseChoice.Save :
            answer == DialogResult.No ? WorkspaceCloseChoice.Discard : WorkspaceCloseChoice.Cancel);
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

    protected void SetCommands(bool enabled)
    {
        _scenario.Enabled = enabled;
        _reset.Enabled = enabled;
        _closeWorkspace.Enabled = enabled && Participant != null;
        _replaceSession.Enabled = enabled && Participant != null;
        UpdateModuleCommands(enabled);
    }
    private async void OnFormClosing(object? sender, FormClosingEventArgs args)
    {
        if (_admittedClose) return;
        args.Cancel = true;
        if (_resetting || _closingWorkspace) return;
        if (_poisoned)
        {
            _resetting = true;
            try { await TearDownOrPoisonAsync(); _poisoned = false; _admittedClose = true; Close(); }
            catch (Exception exception) { _status.Text = "Close cleanup unresolved: " + exception.Message; }
            finally { _resetting = false; }
            return;
        }
        if (Participant != null)
        {
            await CloseWorkspaceSafelyAsync();
            if (Participant != null) return;
        }
        else if (HasGraph || _lifetime != null)
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
