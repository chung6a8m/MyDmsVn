using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;

namespace MyDmsVn.Desktop.WinForms;

internal sealed class BusinessCompactAppearance : IDisposable
{
    private static readonly ConditionalWeakTable<Control, BusinessCompactAppearance> Owners = new ConditionalWeakTable<Control, BusinessCompactAppearance>();
    internal static readonly Color Navy = Color.FromArgb(23, 42, 70);
    private readonly Control _root;
    private readonly Font _body = new Font("Segoe UI", 10f);
    private readonly Font _heading = new Font("Segoe UI", 14f);
    private readonly Font _secondary = new Font("Segoe UI", 9f);
    private readonly Font _brand = new Font("Segoe UI", 15f, FontStyle.Bold);
    private readonly Font _strong = new Font("Segoe UI", 10f, FontStyle.Bold);
    private readonly HashSet<Control> _attached = new HashSet<Control>();
    private bool _disposed;
    private BusinessCompactAppearance(Control root)
    {
        _root = root;
        root.Disposed += OnDisposed;
        BootstrapThemeManager.ThemeChanged += OnTheme;
        Attach(root);
    }
    internal static void Apply(Control workspace) => Owners.GetValue(workspace, root => new BusinessCompactAppearance(root)).Refresh();
    internal static int Scale(Control control, int logical) => (int)Math.Ceiling(logical * control.DeviceDpi / 96d);
    internal static Color NavigationSurface => BootstrapThemeManager.CurrentTheme.Mode == BootstrapThemeMode.Dark
        ? Color.FromArgb(16, 25, 35) : Color.FromArgb(20, 40, 63);
    // Public immutable vendor tokens own composite/editor palettes; app-owned fonts stay local.
    internal static BootstrapTheme CreateTheme(BootstrapThemeMode mode)
    {
        var dark = mode == BootstrapThemeMode.Dark;
        var basis = BootstrapTheme.CreateDefault(mode);
        var colors = new BootstrapThemeColors(
            dark ? Color.FromArgb(81, 160, 255) : Color.FromArgb(18, 98, 239),
            dark ? Color.FromArgb(170, 189, 211) : Color.FromArgb(82, 107, 144),
            dark ? Color.FromArgb(82, 224, 149) : Color.FromArgb(22, 144, 70), basis.Colors.Danger, basis.Colors.Warning, basis.Colors.Info,
            basis.Colors.Light, basis.Colors.Dark,
            dark ? Color.FromArgb(22, 31, 41) : Color.FromArgb(245, 247, 251),
            dark ? Color.FromArgb(29, 39, 51) : Color.White,
            dark ? Color.FromArgb(37, 49, 65) : Color.FromArgb(237, 242, 248),
            dark ? Color.FromArgb(53, 70, 89) : Color.FromArgb(211, 221, 235),
            dark ? Color.FromArgb(232, 239, 247) : Color.FromArgb(23, 43, 70),
            dark ? Color.FromArgb(170, 189, 211) : Color.FromArgb(82, 107, 144),
            dark ? Color.FromArgb(120, 137, 157) : Color.FromArgb(119, 135, 157),
            dark ? Color.FromArgb(100, 170, 255) : Color.FromArgb(23, 107, 255),
            dark ? Color.FromArgb(37, 59, 84) : Color.FromArgb(232, 241, 255),
            dark ? Color.FromArgb(39, 67, 98) : Color.FromArgb(218, 233, 255));
        return new BootstrapTheme(mode, colors, basis.Metrics, basis.Typography, BootstrapThemeManager.CurrentTheme.ReducedMotion);
    }
    private static bool VendorOwnsChildren(Control control)
    {
        for (var type = control.GetType(); type != null; type = type.BaseType)
            if (type.Namespace == "MyDmsVn.Bootstrap5WinFormUI.Controls") return true;
        return false;
    }
    private void Attach(Control control)
    {
        if (VendorOwnsChildren(control) && !(control is TabControl) && !(control is BootstrapPanel) && !(control is BootstrapBusyHost)) return;
        if (!_attached.Add(control)) return;
        control.ControlAdded += OnControlAdded;
        foreach (var child in OwnedChildren(control)) Attach(child);
    }
    // TabPages, a semantic panel's Controls and a busy host's Content are public app content.
    // Other vendor composite children belong to the component and must remain untouched.
    private static IEnumerable<Control> OwnedChildren(Control control) => control is TabControl tabs
        ? tabs.TabPages.Cast<Control>()
        : control is BootstrapBusyHost busy ? new[] { busy.Content }
        : control is BootstrapPanel panel ? panel.Controls.Cast<Control>()
        : VendorOwnsChildren(control) ? Enumerable.Empty<Control>() : control.Controls.Cast<Control>();
    private void OnControlAdded(object? sender, ControlEventArgs e)
    {
        if (_disposed) return;
        if (sender is BootstrapBusyHost busy && !ReferenceEquals(e.Control, busy.Content)) return;
        Attach(e.Control); Style(e.Control);
    }
    private void OnTheme(object? sender, BootstrapThemeChangedEventArgs e)
    {
        if (_root.IsDisposed) return;
        if (_root.InvokeRequired) { if (_root.IsHandleCreated) _root.BeginInvoke(new Action(Refresh)); }
        else Refresh();
    }
    private void Refresh() { if (!_disposed) Style(_root); }
    private void Style(Control control)
    {
        var dark = BootstrapThemeManager.CurrentTheme.Mode == BootstrapThemeMode.Dark;
        var colors = BootstrapThemeManager.CurrentTheme.Colors;
        var typography = control.Tag as string ?? control.Name;
        var semantic = control is BootstrapPanel || control is BootstrapLabel;
        if (!semantic)
            control.Font = typography == "CompactHeading" ? _heading : control.Name == "CompactBrand" ? _brand : control.Name == "CompactUser" ? _strong : typography == "CompactSecondary" || typography == "CompactSection" ? _secondary : _body;
        if (!semantic && (control is Panel || control is TabPage || control is Form || control is UserControl || control is Label))
        {
            control.BackColor = colors.Body;
            control.ForeColor = colors.Text;
            for (var parent = control; parent != null && !(parent is CatalogControl); parent = parent.Parent)
            {
                if (parent.Name == "CatalogInspector" || parent.Name == "CatalogResultsSurface") { control.BackColor = colors.Surface; break; }
                if (parent is BootstrapPanel surface)
                {
                    control.BackColor = surface.SurfaceRole == BootstrapSurfaceRole.Surface ? colors.Surface :
                        surface.SurfaceRole == BootstrapSurfaceRole.Alternate ? colors.SurfaceSecondary : colors.Body;
                    break;
                }
            }
        }
        if (control is TabControl) { control.BackColor = colors.Surface; control.ForeColor = colors.Text; }
        if (control is TextBox || control is ComboBox || control is DateTimePicker)
        { control.BackColor = colors.Surface; control.ForeColor = colors.Text; }
        if (control is SplitContainer split) split.BackColor = colors.Border;
        if (control is ReceiptResultsGrid receiptResults) StyleReceiptResults(receiptResults);
        if (control.AccessibleRole == AccessibleRole.Alert) control.ForeColor = dark ? Color.FromArgb(255, 180, 171) : Color.FromArgb(180, 35, 24);
        // Theme composite internals remain vendor-owned; use public font/palette APIs only.
        if (control is ToolStrip strip)
            foreach (ToolStripItem item in strip.Items)
                if (item is ToolStripControlHost host) Style(host.Control);
        foreach (var child in OwnedChildren(control)) Style(child);
        if (control is BusinessCompactNavigation navigation) navigation.ApplyPalette();
    }
    internal static void StyleReceiptResults(MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid grid)
    {
        var dark = BootstrapThemeManager.CurrentTheme.Mode == BootstrapThemeMode.Dark;
        for (var row = 0; row < grid.RowsCount; row++)
        {
            grid.Rows[row].Height = Scale(grid, 36);
            for (var column = 0; column < grid.ColumnsCount; column++)
                if (grid[row, column] != null) grid[row, column].View = new SourceGrid.Cells.Views.Cell
                {
                    Font = grid.Font, BackColor = dark ? Color.FromArgb(34, 45, 60) : Color.White,
                    ForeColor = dark ? Color.FromArgb(230, 236, 244) : Color.FromArgb(31, 45, 61),
                    TextAlignment = DevAge.Drawing.ContentAlignment.MiddleLeft,
                };
        }
    }
    private void OnDisposed(object? sender, EventArgs e) => Dispose();
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        BootstrapThemeManager.ThemeChanged -= OnTheme;
        _root.Disposed -= OnDisposed;
        foreach (var control in _attached) control.ControlAdded -= OnControlAdded;
        _attached.Clear();
        Owners.Remove(_root);
        _body.Dispose(); _heading.Dispose(); _secondary.Dispose(); _brand.Dispose(); _strong.Dispose();
    }
}
