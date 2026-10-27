using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Icons;
using MyDmsVn.Bootstrap5WinFormUI.Theme;

namespace MyDmsVn.Desktop.WinForms;

// App-owned vectors use the pinned provider contract; no bitmap/font dependency or vendor patch.
internal sealed class BusinessCompactIcons : IIconProvider
{
    internal static readonly IIconRenderer Renderer = new BootstrapIconRenderer(new IIconProvider[]
        { new BusinessCompactIcons(), new SegoeMdl2IconProvider(), new FrameworkVectorIconProvider() });
    internal static IconDescriptor For(string key) => IconDescriptor.External("business-compact", key);
    public bool CanRender(IconDescriptor descriptor) => descriptor.SourceKind == IconSourceKind.External && descriptor.SourceId == "business-compact";
    public bool TryRender(Graphics graphics, IconDescriptor descriptor, Rectangle bounds, Color color)
    {
        if (!CanRender(descriptor) || bounds.Width <= 0 || bounds.Height <= 0) return false;
        var state = graphics.Save();
        try
        {
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(color, 1.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            switch (descriptor.Value)
            {
                case "brand":
                case "product":
                case "balance":
                    var cube = new[] { new Point(12, 2), new Point(21, 7), new Point(21, 17), new Point(12, 22), new Point(3, 17), new Point(3, 7) };
                    if (descriptor.Value == "brand") { using var fill = new SolidBrush(Color.FromArgb(40, 151, 255)); graphics.FillPolygon(fill, cube); }
                    graphics.DrawPolygon(pen, cube);
                    graphics.DrawLines(pen, new[] { new Point(3, 7), new Point(12, 12), new Point(21, 7) });
                    graphics.DrawLine(pen, 12, 12, 12, 22);
                    if (descriptor.Value == "balance") graphics.DrawLine(pen, 7, 4, 17, 10);
                    break;
                case "warehouse":
                    graphics.DrawLines(pen, new[] { new Point(2, 10), new Point(12, 2), new Point(22, 10) });
                    graphics.DrawRectangle(pen, 4, 10, 16, 12); graphics.DrawRectangle(pen, 9, 14, 6, 8); break;
                case "employee":
                case "customer":
                    graphics.DrawEllipse(pen, 8, 3, 8, 8); graphics.DrawArc(pen, 4, 13, 16, 16, 180, 180);
                    graphics.DrawLine(pen, 4, 21, 20, 21);
                    if (descriptor.Value == "customer") { graphics.DrawArc(pen, 16, 5, 6, 6, 270, 180); graphics.DrawArc(pen, 16, 14, 7, 10, 270, 100); }
                    break;
                case "receipt":
                case "card":
                    graphics.DrawRectangle(pen, 5, 2, 14, 20);
                    for (var y = 8; y <= 17; y += 4) graphics.DrawLine(pen, descriptor.Value == "card" ? 10 : 8, y, 16, y);
                    if (descriptor.Value == "card") for (var y = 8; y <= 17; y += 4) graphics.DrawRectangle(pen, 7, y, 1, 1);
                    break;
                case "security":
                    graphics.DrawPolygon(pen, new[] { new Point(12, 2), new Point(21, 6), new Point(19, 16), new Point(12, 22), new Point(5, 16), new Point(3, 6) });
                    graphics.DrawLines(pen, new[] { new Point(8, 12), new Point(11, 15), new Point(16, 9) }); break;
                case "theme":
                    graphics.DrawEllipse(pen, 7, 7, 10, 10);
                    for (var i = 0; i < 8; i++)
                    {
                        var angle = i * Math.PI / 4;
                        graphics.DrawLine(pen, 12 + 8 * (float)Math.Cos(angle), 12 + 8 * (float)Math.Sin(angle),
                            12 + 11 * (float)Math.Cos(angle), 12 + 11 * (float)Math.Sin(angle));
                    }
                    break;
                case "signout":
                    graphics.DrawLines(pen, new[] { new Point(11, 3), new Point(4, 3), new Point(4, 21), new Point(11, 21) });
                    graphics.DrawLine(pen, 10, 12, 22, 12); graphics.DrawLines(pen, new[] { new Point(17, 7), new Point(22, 12), new Point(17, 17) }); break;
                default:
                    graphics.DrawRectangle(pen, 3, 3, 18, 18); graphics.DrawLine(pen, 3, 9, 21, 9); graphics.DrawLine(pen, 9, 9, 9, 21); break;
            }
            return true;
        }
        finally { graphics.Restore(state); }
    }
}

internal class BusinessCompactIconButton : Button
{
    private readonly string _icon;
    internal BusinessCompactIconButton(string icon)
    {
        _icon = icon;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        TextAlign = ContentAlignment.MiddleLeft;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var extent = BusinessCompactAppearance.Scale(this, 20);
        var left = string.IsNullOrEmpty(Text) ? (Width - extent) / 2 : BusinessCompactAppearance.Scale(this, 12);
        BusinessCompactIcons.Renderer.TryRender(e.Graphics, BusinessCompactIcons.For(_icon), new Rectangle(left, (Height - extent) / 2, extent, extent), Enabled ? ForeColor : BootstrapThemeManager.CurrentTheme.Colors.Disabled);
        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(BootstrapThemeManager.CurrentTheme.Colors.Focus, BusinessCompactAppearance.Scale(this, 2));
            e.Graphics.DrawRectangle(focus, 3, 3, Width - 7, Height - 7);
        }
    }
}
