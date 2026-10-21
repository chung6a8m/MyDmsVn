using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
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
    private static bool VendorOwnsChildren(Control control)
    {
        for (var type = control.GetType(); type != null; type = type.BaseType)
            if (type.Namespace == "MyDmsVn.Bootstrap5WinFormUI.Controls") return true;
        return false;
    }
    private void Attach(Control control)
    {
        if (VendorOwnsChildren(control)) return;
        control.ControlAdded += OnControlAdded;
        foreach (Control child in control.Controls) Attach(child);
    }
    private void OnControlAdded(object? sender, ControlEventArgs e) { Attach(e.Control); Style(e.Control); }
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
        control.Font = control.Name == "CompactHeading" ? _heading : control.Name == "CompactSecondary" ? _secondary : _body;
        if (control is Panel || control is TabPage || control is Form || control is UserControl || control is Label)
        {
            control.BackColor = dark ? Color.FromArgb(30, 39, 52) : Color.FromArgb(247, 249, 252);
            control.ForeColor = dark ? Color.FromArgb(230, 236, 244) : Color.FromArgb(31, 45, 61);
        }
        if (control is ReceiptResultsGrid receiptResults) StyleReceiptResults(receiptResults);
        if (control.AccessibleRole == AccessibleRole.Alert) control.ForeColor = dark ? Color.FromArgb(255, 180, 171) : Color.FromArgb(180, 35, 24);
        // Theme composite internals remain vendor-owned; use public font/palette APIs only.
        if (control is ToolStrip strip)
            foreach (ToolStripItem item in strip.Items)
                if (item is ToolStripControlHost host) Style(host.Control);
        if (!VendorOwnsChildren(control))
            foreach (Control child in control.Controls) Style(child);
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
        Owners.Remove(_root);
        _body.Dispose(); _heading.Dispose(); _secondary.Dispose();
    }
}
