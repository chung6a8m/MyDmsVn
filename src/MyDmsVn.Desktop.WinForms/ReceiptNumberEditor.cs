using System;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Formatting;
using SourceGrid.Cells.Editors;

namespace MyDmsVn.Desktop.WinForms;

// App-owned None-mode equivalent of the pinned stock adapter. Protected Editor is the
// public subclass seam; no traversal/reflection of vendor-private controls is involved.
internal sealed class ReceiptNumberEditor : EditorControlBase
{
    internal ReceiptNumberEditor() : base(typeof(decimal)) { UseCellViewProperties = false; }
    internal ReceiptNumberInput BootstrapControl => (ReceiptNumberInput)Control;
    protected override Control CreateControl() => new ReceiptNumberInput();
    public override void SetEditValue(object value)
    {
        BootstrapControl.RawValue = IsStringConversionSupported() ? ValueToString(value) : ValueToDisplayString(value);
        BootstrapControl.SelectAllForEdit();
    }
    public override object GetEditedValue() => BootstrapControl.RawValue;
    protected override void OnSendCharToEditor(char key) => BootstrapControl.ReplaceFirstCharacter(key);
}

internal sealed class ReceiptNumberInput : BootstrapFormattedTextBox
{
    internal ReceiptNumberInput() { FormatMode = BootstrapInputFormatMode.None; Editor.TextAlign = HorizontalAlignment.Right; Editor.AutoSize = false; }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (Editor == null) return;
        var height = Math.Max(Editor.PreferredHeight, TextRenderer.MeasureText("123", Editor.Font).Height);
        Editor.SetBounds(Editor.Left, Math.Max(0, (ClientSize.Height - height) / 2), Editor.Width, height);
    }
    internal void SelectAllForEdit() => Editor.SelectAll();
    internal void ReplaceFirstCharacter(char key) => Editor.SelectedText = key.ToString();
}
