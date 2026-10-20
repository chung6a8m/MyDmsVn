using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MyDmsVn.Desktop.WinForms;
using SourceGrid.Cells.Editors;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

internal static class InlineReceiptTestDriver
{
    internal static void ShiftTab()
    {
        var original = new byte[256];
        Assert.True(GetKeyboardState(original));
        var shifted = (byte[])original.Clone();
        shifted[(int)Keys.ShiftKey] |= 0x80;
        try
        {
            Assert.True(SetKeyboardState(shifted));
            GoodsReceiptInlineEditorTests.Key(Keys.Tab);
        }
        finally { Assert.True(SetKeyboardState(original)); }
    }
    [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] keys);
    [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] keys);
    internal static void MouseDown(Control control)
    {
        Assert.True(PostMessage(control.Handle, 0x0201, new IntPtr(1), new IntPtr(0x00080008)));
        System.Windows.Forms.Application.DoEvents();
    }
    internal static void MouseUp(Control control, bool inside = true)
    {
        Assert.True(PostMessage(control.Handle, 0x0202, IntPtr.Zero, inside ? new IntPtr(0x00080008) : new IntPtr(-1)));
        System.Windows.Forms.Application.DoEvents();
    }
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    internal static void Click(Control control, Point point, Form? owner = null)
    {
        var form = owner ?? control.FindForm()!;
        var topmost = form.TopMost;
        var cursor = Cursor.Position;
        try
        {
            form.TopMost = true;
            form.Activate();
            var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
            var currentThread = GetCurrentThreadId();
            var attached = foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
            try { SetForegroundWindow(form.Handle); }
            finally { if (attached) Assert.True(AttachThreadInput(currentThread, foregroundThread, false)); }
            Assert.Equal(form.Handle, GetForegroundWindow());
            System.Windows.Forms.Application.DoEvents();
            Cursor.Position = control.PointToScreen(point);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (WindowFromPoint(Cursor.Position) != control.Handle && watch.Elapsed < TimeSpan.FromSeconds(2))
            {
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Yield();
            }
            var hit = Control.FromHandle(WindowFromPoint(Cursor.Position));
            Assert.True(ReferenceEquals(hit, control) || (hit != null && control.Contains(hit)), "hit=" + hit?.GetType().Name + "; target=" + control.GetType().Name + "; bounds=" + control.RectangleToScreen(control.ClientRectangle));
            mouse_event(2, 0, 0, 0, UIntPtr.Zero);
            mouse_event(4, 0, 0, 0, UIntPtr.Zero);
            System.Windows.Forms.Application.DoEvents();
        }
        finally { form.TopMost = topmost; Cursor.Position = cursor; }
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint first, uint second, bool attach);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    internal static decimal Value(GoodsReceiptControl control, int column) => control.LineGrid.RowsCount > 1 ? (decimal)control.LineGrid[SelectedRow(control), column].Value : 0m;
    private static int SelectedRow(GoodsReceiptControl control) => Math.Max(1, control.LineGrid.Selection.ActivePosition.Row);
    internal static void SetValue(GoodsReceiptControl control, int column, decimal value)
    {
        Form? host = null;
        if (control.FindForm() == null)
        {
            host = new Form { Size = new Size(1500, 850) };
            host.Controls.Add(control);
            host.Show();
        }
        try
        {
            var position = new SourceGrid.Position(SelectedRow(control), column);
            control.LineGrid.Focus();
            control.LineGrid.Selection.Focus(position, true);
            var context = new SourceGrid.CellContext(control.LineGrid, position);
            context.StartEdit();
            Assert.True(context.IsEditing());
            ((EditorControlBase)context.Cell.Editor).Control.Text = value.ToString(CultureInfo.CurrentCulture);
            Assert.True(context.EndEdit(false));
        }
        finally
        {
            if (host != null) { host.Controls.Remove(control); host.Dispose(); }
        }
    }
}
