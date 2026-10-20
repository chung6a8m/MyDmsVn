using System.Linq;
using System.Collections;

namespace MyDmsVn.Desktop.WinForms;

public sealed partial class GoodsReceiptControl
{
    private readonly GoodsReceiptLinesGridAdapter _inline;
    private bool IsOperationBusy => IsDisposed || Disposing || _isRefreshing || _list.IsLoading || _list.IsBusy || _editor.IsBusy;
    private bool CanEditLines() => !IsOperationBusy && !_editor.IsReadOnly && !_isAccessDeniedEditorLocked && !_editor.RequiresPostOutcomeReload;

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
    internal System.Func<bool>? AdmitPointerNavigation { get; set; }
    protected override void WndProc(ref System.Windows.Forms.Message message)
    {
        if ((message.Msg == 0x0201 || message.Msg == 0x0203) &&
            AdmitPointerNavigation?.Invoke() == false) return;
        base.WndProc(ref message);
    }
}
