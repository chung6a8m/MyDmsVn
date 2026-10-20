using System;
using System.Drawing;
using System.Windows.Forms;
using BootstrapGrid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

namespace MyDmsVn.Desktop.WinForms;

// Read-only result cells retain their table semantics. A virtual Open button exposes
// the same row activation as mouse/keyboard, without editing cells or vendor code.
internal class AccessibleActionGrid : BootstrapGrid
{
    private Func<int, object?>? _rowKey;
    private Func<int, string>? _rowName;
    private Action<int>? _openRow;

    internal void ConfigureRowActions(Func<int, object?> key, Func<int, string> name, Action<int> openRow)
    {
        _rowKey = key;
        _rowName = name;
        _openRow = openRow;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ResultAccessibleObject(this);

    private int FindRow(object? key)
    {
        if (key == null || _rowKey == null) return -1;
        for (var row = FixedRows; row < RowsCount; row++)
            if (Equals(key, _rowKey(row))) return row;
        return -1;
    }

    private void Open(object? key)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Open(key)));
            return;
        }
        var row = FindRow(key);
        if (row >= FixedRows && Enabled && Visible) _openRow?.Invoke(row);
    }

    private Rectangle CellBounds(int row, int column)
    {
        if (!Visible || row < 0 || row >= RowsCount || column < 0 || column >= ColumnsCount) return Rectangle.Empty;
        var bounds = Rectangle.Intersect(ClientRectangle, PositionToRectangle(new SourceGrid.Position(row, column)));
        return bounds.IsEmpty ? Rectangle.Empty : RectangleToScreen(bounds);
    }

    private sealed class ResultAccessibleObject : Control.ControlAccessibleObject
    {
        private readonly AccessibleActionGrid _grid;
        internal ResultAccessibleObject(AccessibleActionGrid grid) : base(grid) { _grid = grid; }
        public override AccessibleRole Role => AccessibleRole.Table;
        public override string? Name { get => _grid.AccessibleName ?? _grid.Name; set { } }
        public override int GetChildCount() => _grid.RowsCount;
        public override AccessibleObject? GetChild(int index) => index < 0 || index >= _grid.RowsCount ? null : new RowAccessibleObject(_grid, index, this);
    }

    private sealed class RowAccessibleObject : AccessibleObject
    {
        internal readonly AccessibleActionGrid Grid;
        private readonly AccessibleObject _parent;
        private readonly bool _header;
        internal readonly object? Key;
        internal readonly string OpenName;
        internal RowAccessibleObject(AccessibleActionGrid grid, int row, AccessibleObject parent)
        {
            Grid = grid;
            _parent = parent;
            _header = row < grid.FixedRows;
            Key = _header ? null : grid._rowKey?.Invoke(row);
            OpenName = "Open " + (grid._rowName?.Invoke(row) ?? string.Empty);
        }
        internal int CurrentRow => _header ? 0 : Grid.FindRow(Key);
        public override AccessibleObject Parent => _parent;
        public override AccessibleRole Role => AccessibleRole.Row;
        public override string? Name { get => _header ? "Columns" : "Result " + OpenName.Substring(5); set { } }
        public override Rectangle Bounds => Grid.CellBounds(CurrentRow, 0);
        public override int GetChildCount() => Grid.ColumnsCount + (!_header && Key != null && Grid._openRow != null ? 1 : 0);
        public override AccessibleObject? GetChild(int index)
        {
            if (index == Grid.ColumnsCount && !_header && Key != null) return new OpenAccessibleObject(this);
            var row = CurrentRow;
            if (index < 0 || index >= Grid.ColumnsCount || row < 0 || row >= Grid.RowsCount) return null;
            return new CellAccessibleObject(this, index, Convert.ToString(Grid[row, index]?.Value) ?? string.Empty, _header);
        }
    }

    private sealed class CellAccessibleObject : AccessibleObject
    {
        public override string? Help => Description;
        private readonly RowAccessibleObject _row;
        private readonly int _column;
        private readonly string _value;
        private readonly bool _header;
        internal CellAccessibleObject(RowAccessibleObject row, int column, string value, bool header)
        { _row = row; _column = column; _value = value; _header = header; }
        public override AccessibleObject Parent => _row;
        public override AccessibleRole Role => _header ? AccessibleRole.ColumnHeader : AccessibleRole.Cell;
        public override string? Name { get => _value; set { } }
        public override string? Description => !_row.Grid.IsDisposed && _row.Grid.RowsCount > 0 && _column < _row.Grid.ColumnsCount
            ? Convert.ToString(_row.Grid[0, _column]?.Value) : null;
        public override string? Value { get => _value; set => throw new InvalidOperationException("Result cells are read-only."); }
        public override AccessibleStates State => AccessibleStates.ReadOnly | (_row.CurrentRow < 0 ? AccessibleStates.Unavailable : 0);
        public override Rectangle Bounds => _row.Grid.CellBounds(_row.CurrentRow, _column);
    }

    private sealed class OpenAccessibleObject : AccessibleObject
    {
        private readonly RowAccessibleObject _row;
        internal OpenAccessibleObject(RowAccessibleObject row) { _row = row; }
        public override AccessibleObject Parent => _row;
        public override AccessibleRole Role => AccessibleRole.PushButton;
        public override string? Name { get => _row.OpenName; set { } }
        public override string DefaultAction => "Open";
        public override Rectangle Bounds => _row.Bounds;
        public override AccessibleStates State => _row.CurrentRow >= _row.Grid.FixedRows && _row.Grid.Enabled
            ? AccessibleStates.Focusable : AccessibleStates.Unavailable;
        public override void DoDefaultAction() => _row.Grid.Open(_row.Key);
    }
}
