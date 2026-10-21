using System.Linq;
using System.Collections;

namespace MyDmsVn.Desktop.WinForms;

public sealed partial class GoodsReceiptControl
{
    private readonly GoodsReceiptLinesGridAdapter _inline;
    private bool IsOperationBusy => IsDisposed || Disposing || _isRefreshing || _isApplyingWarehouseFilter || _list.IsLoading || _list.IsBusy || _editor.IsBusy;
    private bool CanEditLines() => WorkspaceMode == ReceiptWorkspaceMode.Detail && !IsOperationBusy && !_editor.IsReadOnly && !_isAccessDeniedEditorLocked && !_editor.RequiresPostOutcomeReload;

    private void FocusFirstLineError()
    {
        for (var index = 0; index < _editor.Lines.Count; index++)
        {
            var fields = new[] { "productId", "quantity", "unitCost" };
            for (var field = 0; field < fields.Length; field++)
                if (((IEnumerable)_editor.GetErrors($"lines[{index}].{fields[field]}")).Cast<string>().Any())
                {
                    _inline.FocusCell(_editor.Lines[index], field + 1);
                    return;
                }
        }
    }

    private void OnInlineEditStateChanged(object? sender, System.EventArgs args) => DispatchToUi(ApplyState);

    private bool FinishInline(ReceiptEditIntent intent)
    {
        if (_inline.TryFinishEdit(intent)) return true;
        ApplyState();
        _inline.FocusActiveEdit();
        return false;
    }
}


// Keep the active cell focused until an actual command click is admitted. Mouse-down
// alone (including a drag that abandons the button) must not resolve a lookup.
internal sealed class ReceiptCommandButton : MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapButton
{
    internal System.Func<bool>? RetainEditorFocus { get; set; }
    private bool _held;
    protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!Focused || !ShowFocusCues || !Enabled) return;

        // Supporting outline commands need a text-colored cue; filled commands
        // need contrast against Primary, including Graphite's equal focus/fill tokens.
        var colors = MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme.Colors;
        var primary = colors.Primary;
        var color = Outline ? colors.Text : primary.R * 299 + primary.G * 587 + primary.B * 114 >= 128000 ? colors.Dark : colors.Light;
        var inset = System.Math.Max(3, 4 * DeviceDpi / 96);
        var bounds = new System.Drawing.Rectangle(inset, inset, Width - inset * 2 - 1, Height - inset * 2 - 1);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var pen = new System.Drawing.Pen(color, System.Math.Max(1, DeviceDpi / 96))
        { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
        e.Graphics.DrawRectangle(pen, bounds);
    }
    protected override void WndProc(ref System.Windows.Forms.Message message)
    {
        if (message.Msg == 0x0201 && Enabled && RetainEditorFocus?.Invoke() == true)
        {
            _held = true;
            Capture = true;
            return;
        }
        if (message.Msg == 0x0202 && _held)
        {
            _held = false;
            Capture = false;
            var location = new System.Drawing.Point(unchecked((short)(message.LParam.ToInt64() & 0xffff)),
                unchecked((short)((message.LParam.ToInt64() >> 16) & 0xffff)));
            if (Enabled && ClientRectangle.Contains(location)) OnClick(System.EventArgs.Empty);
            return;
        }
        base.WndProc(ref message);
    }
    protected override void OnMouseCaptureChanged(System.EventArgs e)
    {
        if (!Capture) _held = false;
        base.OnMouseCaptureChanged(e);
    }
}


// The master list must admit navigation before native focus can resolve line lookup text.
internal sealed class ReceiptResultsGrid : AccessibleActionGrid
{
    private bool _synchronizing;
    internal void SynchronizeRow(int row, int column)
    {
        _synchronizing = true;
        try
        {
            if (row > 0 && row < RowsCount)
                Selection.Focus(new SourceGrid.Position(row, System.Math.Max(0, System.Math.Min(ColumnsCount - 1, column))), true);
        }
        finally { _synchronizing = false; }
    }
    public override bool Focus(bool selectFirstCell) => _synchronizing || base.Focus(selectFirstCell);
    internal System.Func<bool>? AdmitPointerNavigation { get; set; }
    protected override void WndProc(ref System.Windows.Forms.Message message)
    {
        if ((message.Msg == 0x0201 || message.Msg == 0x0203) &&
            AdmitPointerNavigation?.Invoke() == false) return;
        base.WndProc(ref message);
    }
}
