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
    private int _paintedSelectedIndex = -1;
    private Rectangle _paintedSelectedBounds;
    internal BusinessCompactWorkspaceTabs() { TabStyle = BootstrapTabStyle.Underline; BorderRadius = 0; }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index == SelectedIndex)
        {
            _paintedSelectedIndex = e.Index;
            _paintedSelectedBounds = e.Bounds;
        }
        base.OnDrawItem(e);
        // The shell has text-only Underline headers. The pinned renderer measures a
        // padded string, then constrains DrawText to that measured width; DrawText's
        // padding/ellipsis can still shorten it. Use the available app header width
        // and explicit NoPadding, without changing native item sizing or selection.
        if (e.Index < 0 || e.Index >= TabCount || ImageList != null ||
            TabStyle != BootstrapTabStyle.Underline || Variant != BootstrapVariant.Primary) return;
        var colors = BootstrapThemeManager.CurrentTheme.Colors;
        var enabled = Enabled && TabPages[e.Index].Enabled;
        var hovered = HotTrack && e.Bounds.Contains(PointToClient(MousePosition)) && e.Index != SelectedIndex;
        var background = hovered && enabled ? colors.Hover : colors.Surface;
        var foreground = !enabled ? colors.Disabled : e.Index == SelectedIndex ? colors.Primary : hovered ? colors.Text : colors.MutedText;
        // The vendor already painted its inset focus rectangle. Keep its complete
        // pen extent outside our caption fill, including both horizontal edges.
        var verticalInset = Math.Max(2, BootstrapThemeManager.CurrentTheme.Metrics.FocusBorderWidth * 2 + 1);
        var text = Rectangle.Inflate(e.Bounds, -BusinessCompactAppearance.Scale(this, BootstrapThemeManager.CurrentTheme.Metrics.SpacingMD), -BusinessCompactAppearance.Scale(this, verticalInset));
        if (text.Width <= 0 || text.Height <= 0) return;
        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, text);
        TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, text, foreground,
            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }
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
        // Native top and inter-header bevels sit outside every GetTabRect. Complete
        // that whole gap, excluding the headers so their glyphs/focus/underline survive.
        using var gaps = new Region(new Rectangle(0, 0, ClientSize.Width, Math.Max(0, display.Top)));
        for (var index = 0; index < TabCount; index++)
            gaps.Exclude(index == SelectedIndex && index == _paintedSelectedIndex && !_paintedSelectedBounds.IsEmpty
                ? _paintedSelectedBounds : GetTabRect(index));
        graphics.FillRegion(surface, gaps);
        for (var index = 0; index < TabCount; index++)
        {
            // Native DrawItem expands the selected bounds beyond GetTabRect. Its
            // complete surface and focus are already painted; never trim that frame.
            if (index == SelectedIndex)
            {
                // The outermost native bevel is outside the inset focus pen.
                // Use the actual expanded DrawItem bounds, not GetTabRect.
                var selected = index == _paintedSelectedIndex && !_paintedSelectedBounds.IsEmpty
                    ? _paintedSelectedBounds : GetTabRect(index);
                var bevel = Focused && ShowFocusCues ? 1 : 2;
                graphics.FillRectangle(surface, selected.Left, selected.Top, selected.Width, bevel);
                graphics.FillRectangle(surface, selected.Left, selected.Top, bevel, selected.Height);
                graphics.FillRectangle(surface, selected.Right - bevel, selected.Top, bevel, selected.Height);
                continue;
            }
            var header = GetTabRect(index);
            // comctl32 draws a light bevel outside the vendor's header surface.
            // Cover only those two-pixel edges, leaving text/focus and hit regions intact.
            graphics.FillRectangle(surface, header.Left, header.Top, header.Width, 2);
            graphics.FillRectangle(surface, header.Left, header.Top, 2, header.Height);
            graphics.FillRectangle(surface, header.Right - 2, header.Top, 2, header.Height);
            graphics.FillRectangle(surface, header.Left, header.Bottom - 2, header.Width, 2);
        }
        graphics.FillRectangle(surface, 0, display.Top, Math.Max(0, display.Left), Math.Max(0, ClientSize.Height - display.Top));
        graphics.FillRectangle(surface, display.Right, display.Top, Math.Max(0, ClientSize.Width - display.Right), Math.Max(0, ClientSize.Height - display.Top));
        graphics.FillRectangle(surface, 0, display.Bottom, ClientSize.Width, Math.Max(0, ClientSize.Height - display.Bottom));
        using var border = new Pen(colors.Border);
        graphics.DrawRectangle(border, 0, Math.Max(0, display.Top - 1), Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - display.Top));
    }
}
