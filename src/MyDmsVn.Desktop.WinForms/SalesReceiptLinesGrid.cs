using BootstrapGrid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms;

internal sealed class SalesReceiptLinesGrid : BootstrapGrid
{
    internal SalesReceiptLinesGrid()
    {
        AccessibleName = "Sales receipt lines";
        AccessibleDescription = "Select existing quantity and unit price cells, then press Ctrl+Shift+V to paste tab-separated numbers.";
        BusinessCompactAppearance.Apply(this);
    }
    internal SalesReceiptLinesGridAdapter? Adapter { get; set; }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Shift | Keys.V))
        {
            string text;
            try { text = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty; }
            catch (ExternalException) { text = string.Empty; }
            Adapter?.TryPasteNumeric(text);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new ReceiptAccessibleObject(this);

    private Rectangle CellBounds(int row, int column)
    {
        if (IsDisposed) return Rectangle.Empty;
        if (InvokeRequired) return (Rectangle)Invoke(new Func<Rectangle>(() => CellBounds(row, column)));
        if (!Visible || row < 0 || row >= RowsCount || column < 0 || column >= ColumnsCount) return Rectangle.Empty;
        var bounds = Rectangle.Intersect(ClientRectangle, PositionToRectangle(new SourceGrid.Position(row, column)));
        return bounds.IsEmpty ? Rectangle.Empty : PhysicalScreenBounds(this, bounds);
    }

    // MSAA accLocation/accHitTest use physical screen coordinates even when the shipped
    // net48 window remains DPI-unaware. Do not change the process's awareness to fix a provider.
    private static Rectangle PhysicalScreenBounds(Control control, Rectangle clientBounds)
    {
        var screen = control.RectangleToScreen(clientBounds);
        var first = screen.Location;
        var last = new Point(screen.Right, screen.Bottom);
        if (!ToPhysical(control.Handle, ref first) || !ToPhysical(control.Handle, ref last)) return Rectangle.Empty;
        return Rectangle.FromLTRB(first.X, first.Y, last.X, last.Y);
    }
    private static bool ToPhysical(IntPtr window, ref Point point)
    {
        try { return LogicalToPhysicalPointForPerMonitorDPI(window, ref point); }
        catch (EntryPointNotFoundException) { return LogicalToPhysicalPoint(window, ref point); }
    }
    [DllImport("user32.dll")] private static extern bool LogicalToPhysicalPointForPerMonitorDPI(IntPtr window, ref Point point);
    // Compatibility path for pre-8.1 Windows; runtime certification remains a release prerequisite.
    [DllImport("user32.dll")] private static extern bool LogicalToPhysicalPoint(IntPtr window, ref Point point);

    private sealed class ReceiptAccessibleObject : ControlAccessibleObject
    {
        private readonly SalesReceiptLinesGrid _grid;
        internal ReceiptAccessibleObject(SalesReceiptLinesGrid grid) : base(grid) { _grid = grid; }
        public override AccessibleRole Role => AccessibleRole.Table;
        public override string? Name { get => _grid.AccessibleName; set { } }
        public override int GetChildCount() => _grid.Adapter == null ? 0 : _grid.RowsCount + (_grid.Adapter.ActiveAccessibleEditor == null ? 0 : 1);
        public override AccessibleObject? GetChild(int index)
        {
            if (_grid.IsDisposed) return null;
            if (_grid.InvokeRequired) return (AccessibleObject?)_grid.Invoke(new Func<AccessibleObject?>(() => GetChild(index)));
            var adapter = _grid.Adapter;
            if (adapter == null || _grid.IsDisposed || index < 0) return null;
            if (index == _grid.RowsCount) return adapter.ActiveAccessibleEditor?.AccessibilityObject;
            if (index >= _grid.RowsCount) return null;
            var line = index == 0 ? null : adapter.AccessibleLine(index - 1);
            return index > 0 && line == null ? null : new RowAccessibleObject(_grid, this, adapter.AccessibleGeneration, line, index);
        }
        public override AccessibleObject? GetFocused() => _grid.Adapter?.ActiveAccessibleEditor?.AccessibilityObject.GetFocused()
            ?? _grid.Adapter?.ActiveAccessibleEditor?.AccessibilityObject;
        public override AccessibleObject? HitTest(int x, int y)
        {
            var input = _grid.Adapter?.ActiveAccessibleEditor;
            if (input != null && PhysicalScreenBounds(input, input.ClientRectangle).Contains(x, y)) return input.AccessibilityObject.HitTest(x, y) ?? input.AccessibilityObject;
            for (var row = 0; row < _grid.RowsCount; row++)
                for (var column = 0; column < _grid.ColumnsCount; column++)
                    if (_grid.CellBounds(row, column).Contains(x, y)) return GetChild(row)?.GetChild(column);
            return base.HitTest(x, y);
        }
    }

    private sealed class RowAccessibleObject : AccessibleObject
    {
        internal readonly SalesReceiptLinesGrid Grid;
        internal readonly long Generation;
        internal readonly SalesReceiptLineViewModel? Line;
        internal readonly int OriginalRow;
        private readonly AccessibleObject _parent;
        internal RowAccessibleObject(SalesReceiptLinesGrid grid, AccessibleObject parent, long generation, SalesReceiptLineViewModel? line, int row)
        { Grid = grid; _parent = parent; Generation = generation; Line = line; OriginalRow = row; }
        internal int CurrentRow => Grid.IsDisposed || Grid.Adapter == null ? -1 : Line == null ? 0 : Grid.Adapter.AccessibleRow(Generation, Line);
        public override AccessibleObject Parent => _parent;
        public override AccessibleRole Role => AccessibleRole.Row;
        public override string? Name
        {
            get
            {
                if (CurrentRow < 0) return "Unavailable receipt line";
                if (Line == null) return "Columns";
                var issue = Grid.Adapter?.AccessibleRowValidation(Generation, Line);
                return "Line " + CurrentRow + ": " + (Line.ProductDisplayName ?? "New line") +
                    (string.IsNullOrEmpty(issue) ? "" : "; Validation error: " + issue);
            }
            set { }
        }
        public override AccessibleStates State => CurrentRow < 0 ? AccessibleStates.Unavailable : AccessibleStates.None;
        public override Rectangle Bounds
        {
            get
            {
                var bounds = Rectangle.Empty;
                for (var column = 0; column < 7; column++)
                {
                    var cell = Grid.CellBounds(CurrentRow, column);
                    if (!cell.IsEmpty) bounds = bounds.IsEmpty ? cell : Rectangle.Union(bounds, cell);
                }
                return bounds;
            }
        }
        public override int GetChildCount() => 7;
        public override AccessibleObject? GetChild(int index) => index < 0 || index >= 7 ? null : new CellAccessibleObject(this, index);
    }

    private sealed class CellAccessibleObject : AccessibleObject
    {
        private readonly RowAccessibleObject _row;
        private readonly int _column;
        private static readonly string[] Columns = { "STT", "Product", "Unit", "Quantity", "Unit price", "Gift", "Line amount" };
        internal CellAccessibleObject(RowAccessibleObject row, int column) { _row = row; _column = column; }
        private bool Editable => _row.Line != null && _row.Grid.Adapter?.CanAccessEdit(_row.Generation, _row.Line, _column) == true;
        public override AccessibleObject Parent => _row;
        public override AccessibleRole Role => _row.Line == null ? AccessibleRole.ColumnHeader : _column == 5 ? AccessibleRole.CheckButton : AccessibleRole.Cell;
        public override string? Name { get => Columns[_column] + (_row.Line == null ? string.Empty : " row " + _row.CurrentRow); set { } }
        public override string? Description => _row.CurrentRow < 0 ? null : Columns[_column];
        public override string? Help => _row.CurrentRow < 0 ? "This receipt line is no longer available." : _row.Line == null ? Description :
            _row.Grid.Adapter?.AccessibleHelp(_row.Generation, _row.Line, _column);
        public override string? Value
        {
            get => _row.CurrentRow < 0 ? string.Empty : _row.Line == null ? Columns[_column] : _row.Grid.Adapter?.AccessibleValue(_row.Generation, _row.Line, _column);
            set
            {
                if (!Editable || _row.Line == null) throw new InvalidOperationException("This receipt cell is not editable.");
                _row.Grid.Adapter!.SetAccessibleValue(_row.Generation, _row.Line, _column, value ?? string.Empty);
            }
        }
        public override AccessibleStates State => (_row.CurrentRow < 0 ? AccessibleStates.Unavailable : 0) |
            (Editable ? AccessibleStates.Focusable : AccessibleStates.ReadOnly) |
            (_column == 5 && _row.Line?.IsGift == true ? AccessibleStates.Checked : 0) |
            (Bounds.IsEmpty ? AccessibleStates.Offscreen : 0);
        public override Rectangle Bounds => _row.Grid.CellBounds(_row.CurrentRow, _column);
        public override string DefaultAction => Editable ? (_column == 5 ? "Toggle" : "Edit") : string.Empty;
        public override void DoDefaultAction()
        {
            if (!Editable || _row.Line == null) throw new InvalidOperationException("This receipt cell is not editable.");
            _row.Grid.Adapter!.BeginAccessibleEdit(_row.Generation, _row.Line, _column);
        }
        public override void Select(AccessibleSelection flags)
        {
            if ((flags & (AccessibleSelection.TakeFocus | AccessibleSelection.TakeSelection)) != 0) DoDefaultAction();
        }
    }
}
