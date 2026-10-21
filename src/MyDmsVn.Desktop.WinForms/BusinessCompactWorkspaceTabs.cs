using System;
using System.Drawing;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;

namespace MyDmsVn.Desktop.WinForms;

// Keep the pinned native tab control's selection, overflow, keyboard and page ownership.
// Owner-drawn headers do not paint the remaining native frame/header gap. Complete only
// that app-owned chrome through WinForms' protected paint-message seam, including WM_PRINT.
internal sealed class BusinessCompactWorkspaceTabs : BootstrapTabControl
{
    internal BusinessCompactWorkspaceTabs() { TabStyle = BootstrapTabStyle.Underline; BorderRadius = 0; }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (!IsHandleCreated || IsDisposed || (message.Msg != 0x000F && message.Msg != 0x0317 && message.Msg != 0x0318) ||
            (message.Msg != 0x000F && message.WParam == IntPtr.Zero)) return;
        using var graphics = message.Msg == 0x000F ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(message.WParam);
        var colors = BootstrapThemeManager.CurrentTheme.Colors;
        using var surface = new SolidBrush(colors.Surface);
        if (TabCount == 0) { graphics.FillRectangle(surface, ClientRectangle); return; }
        var display = DisplayRectangle;
        var lastRight = 0;
        for (var index = 0; index < TabCount; index++)
        {
            var header = GetTabRect(index);
            lastRight = Math.Max(lastRight, header.Right);
            // comctl32 draws a light bevel outside the vendor's header surface.
            // Cover only those two-pixel edges, leaving text/focus and hit regions intact.
            graphics.FillRectangle(surface, header.Left, header.Top, header.Width, 2);
            graphics.FillRectangle(surface, header.Left, header.Top, 2, header.Height);
            graphics.FillRectangle(surface, header.Right - 2, header.Top, 2, header.Height);
        }
        if (lastRight < ClientSize.Width)
            graphics.FillRectangle(surface, lastRight, 0, ClientSize.Width - lastRight, Math.Max(0, display.Top));
        graphics.FillRectangle(surface, 0, display.Top, Math.Max(0, display.Left), Math.Max(0, ClientSize.Height - display.Top));
        graphics.FillRectangle(surface, display.Right, display.Top, Math.Max(0, ClientSize.Width - display.Right), Math.Max(0, ClientSize.Height - display.Top));
        graphics.FillRectangle(surface, 0, display.Bottom, ClientSize.Width, Math.Max(0, ClientSize.Height - display.Bottom));
        using var border = new Pen(colors.Border);
        graphics.DrawRectangle(border, 0, Math.Max(0, display.Top - 1), Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - display.Top));
    }
}
