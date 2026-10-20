using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using MyDmsVn.Bootstrap5WinFormUI.Formatting;
using MyDmsVn.BootstrapSourceGrid.Editors;
using MyDmsVn.Desktop.Application;
using SourceGrid;
using SourceGrid.Cells;
using SourceGrid.Cells.Controllers;
using SourceGrid.Cells.Editors;

namespace MyDmsVn.Desktop.WinForms;

internal sealed class GoodsReceiptLinesGridAdapter : IDisposable
{
    private readonly GoodsReceiptLinesGrid _grid;
    private readonly GoodsReceiptEditorViewModel _editor;
    private readonly Func<bool> _canEdit;
    private readonly BootstrapLookupBoxEditor _product;
    private readonly ReceiptNumberEditor _quantity;
    private readonly ReceiptNumberEditor _cost;
    private readonly List<GoodsReceiptLineViewModel> _lines = new List<GoodsReceiptLineViewModel>();
    private readonly List<(Cell Cell, LineController Controller)> _controllers = new List<(Cell, LineController)>();
    private CultureInfo _culture = CultureInfo.ReadOnly((CultureInfo)CultureInfo.CurrentCulture.Clone());
    private Session? _session;
    private long _generation;
    private long? _receiptId;
    private long? _selectionId;
    private bool _selectionPending;
    private int _selectionRow = 1;
    private int _selectionColumn = 1;
    private bool _projecting;
    private bool _resetting;
    private bool _structurePending;
    private bool _disposed;
    private bool _bindingOptions;

    internal GoodsReceiptLinesGridAdapter(GoodsReceiptLinesGrid grid, GoodsReceiptEditorViewModel editor, Func<bool> canEdit)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _canEdit = canEdit ?? throw new ArgumentNullException(nameof(canEdit));
        _product = grid.BootstrapEditors.CreateLookupBox(typeof(int));
        _quantity = new ReceiptNumberEditor();
        _cost = new ReceiptNumberEditor();
        foreach (var input in Inputs)
        {
            input.EditableMode = EditableMode.F2Key | EditableMode.AnyKey | EditableMode.DoubleClick;
            input.EditException += OnEditException;
            input.ConvertingObjectToValue += ConvertCandidate;
            input.Control.TextChanged += OnCandidateChanged;
        }
        _product.Control.AccessibleName = "Product";
        _quantity.Control.AccessibleName = "Quantity";
        _cost.Control.AccessibleName = "Unit cost";
        _product.BootstrapControl.DisplayMember = nameof(CatalogLookupOption.DisplayName);
        _product.BootstrapControl.ValueMember = nameof(CatalogLookupOption.Id);
        _product.BootstrapControl.SearchDebounceMilliseconds = 0;
        _product.ConvertingValueToDisplayString += DisplayProduct;
        _quantity.BootstrapControl.FormatMode = BootstrapInputFormatMode.None;
        _cost.BootstrapControl.FormatMode = BootstrapInputFormatMode.None;
        _editor.PropertyChanged += OnEditorChanged;
        _editor.Lines.CollectionChanged += OnLinesChanged;
        _editor.ProductOptions.PropertyChanged += OnOptionsChanged;
        _grid.Disposed += OnGridDisposed;
        _grid.SizeChanged += OnAppearanceChanged;
        _grid.FontChanged += OnAppearanceChanged;
        MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.ThemeChanged += OnGridThemeChanged;
        _grid.Adapter = this;
        ResetDocument(editor.Lines);
    }

    private EditorControlBase[] Inputs => new EditorControlBase[] { _product, _quantity, _cost };
    private bool Editable => !_disposed && !_grid.IsDisposed && _grid.Enabled && !_editor.IsReadOnly &&
        !_editor.IsBusy && !_editor.RequiresPostOutcomeReload && _canEdit();
    internal MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapLookupBox ProductControl => _product.BootstrapControl;
    internal MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapFormattedTextBox QuantityControl => _quantity.BootstrapControl;
    internal MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapFormattedTextBox CostControl => _cost.BootstrapControl;
    internal GoodsReceiptLineViewModel? SelectedLine
    {
        get
        {
            var row = _grid.Selection.ActivePosition.Row > 0 ? _grid.Selection.ActivePosition.Row : _selectionRow;
            return row > 0 && row <= _lines.Count ? _lines[row - 1] : null;
        }
    }
    internal long AccessibleGeneration => _generation;
    internal GoodsReceiptLineViewModel? AccessibleLine(int index)
    {
        if (_disposed || _grid.IsDisposed) return null;
        if (_grid.InvokeRequired) return (GoodsReceiptLineViewModel?)_grid.Invoke(new Func<GoodsReceiptLineViewModel?>(() => AccessibleLine(index)));
        return index >= 0 && index < _lines.Count ? _lines[index] : null;
    }
    internal int AccessibleRow(long generation, GoodsReceiptLineViewModel line)
    {
        if (_disposed || _grid.IsDisposed) return -1;
        if (_grid.InvokeRequired) return (int)_grid.Invoke(new Func<int>(() => AccessibleRow(generation, line)));
        if (generation != _generation || !_editor.Lines.Any(current => ReferenceEquals(current, line))) return -1;
        var index = _lines.FindIndex(current => ReferenceEquals(current, line));
        return index < 0 ? -1 : index + 1;
    }
    internal bool CanAccessEdit(long generation, GoodsReceiptLineViewModel line, int column) =>
        Editable && column >= 1 && column <= 3 && AccessibleRow(generation, line) > 0;
    internal System.Windows.Forms.Control? ActiveAccessibleEditor => _session != null && Current(_session) && _session.Input.IsEditing ? _session.Input.Control : null;
    internal string AccessibleValue(long generation, GoodsReceiptLineViewModel line, int column)
    {
        if (_disposed || _grid.IsDisposed) return string.Empty;
        if (_grid.InvokeRequired) return (string)_grid.Invoke(new Func<string>(() => AccessibleValue(generation, line, column)));
        return AccessibleRow(generation, line) <= 0 ? string.Empty : column == 0 ? AccessibleRow(generation, line).ToString(_culture) :
            Convert.ToString(LineValue(line, column), _culture) ?? string.Empty;
    }
    internal string AccessibleHelp(long generation, GoodsReceiptLineViewModel line, int column)
    {
        if (_disposed || _grid.IsDisposed) return "This receipt line is no longer available.";
        if (_grid.InvokeRequired) return (string)_grid.Invoke(new Func<string>(() => AccessibleHelp(generation, line, column)));
        var row = AccessibleRow(generation, line);
        if (row <= 0) return "This receipt line is no longer available.";
        var field = column == 1 ? "productId" : column == 2 ? "quantity" : "unitCost";
        var server = string.Join(" ", _editor.GetErrors($"lines[{row - 1}].{field}").Cast<string>());
        var active = _session != null && ReferenceEquals(_session.Line, line) && _session.Column == column;
        var state = column == 1 ? $"Committed key: {line.ProductId}; Selected: {ProductCaption(line.ProductId)}" : "Committed value: " + AccessibleValue(generation, line, column);
        if (active && column == 1 && _product.BootstrapControl.HasPendingText) state = "Pending selection: " + _product.Control.Text + "; " + state;
        return state + (active && EditError != null ? "; " + EditError : "") + (server.Length > 0 ? "; " + server : "");
    }
    internal void BeginAccessibleEdit(long generation, GoodsReceiptLineViewModel line, int column)
    {
        if (_grid.InvokeRequired) { _grid.Invoke(new Action(() => BeginAccessibleEdit(generation, line, column))); return; }
        if (!CanAccessEdit(generation, line, column)) throw new InvalidOperationException("This receipt cell is not editable.");
        if (_session != null && (!ReferenceEquals(_session.Line, line) || _session.Column != column) &&
            !TryFinishEdit(ReceiptEditIntent.AddLine)) throw new InvalidOperationException(EditError);
        if (_session == null) FocusCell(line, column);
        if (_session == null || !ReferenceEquals(_session.Line, line) || _session.Column != column || !Current(_session))
            throw new InvalidOperationException("The receipt cell could not enter editing.");
        FocusActiveEdit();
    }
    internal void SetAccessibleValue(long generation, GoodsReceiptLineViewModel line, int column, string raw)
    {
        if (_grid.InvokeRequired) { _grid.Invoke(new Action(() => SetAccessibleValue(generation, line, column, raw))); return; }
        BeginAccessibleEdit(generation, line, column);
        if (column == 1 && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var key) && key > 0)
        {
            if (!_product.BootstrapControl.SelectValue(key)) _product.Control.Text = raw;
        }
        else _session!.Input.Control.Text = raw;
        if (!TryFinishEdit(ReceiptEditIntent.Save)) throw new ArgumentException(EditError ?? "Invalid receipt cell value.", nameof(raw));
    }

    internal void FocusCell(GoodsReceiptLineViewModel line, int column)
    {
        if (!Editable || !_grid.IsHandleCreated || column < 1 || column > 3) return;
        var index = _lines.FindIndex(current => ReferenceEquals(current, line));
        if (index < 0) return;
        var position = new Position(index + 1, column);
        _grid.Focus();
        if (_grid.Selection.Focus(position, true)) new CellContext(_grid, position).StartEdit();
    }
    internal void FocusActiveEdit() { if (_session != null && !_session.Input.Control.ContainsFocus) _session.Input.Control.Focus(); }
    private string? _editError;
    internal event EventHandler? EditStateChanged;
    internal string? EditError
    {
        get => _editError;
        private set
        {
            if (_editError == value) return;
            _editError = value;
            EditStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    internal bool HasPendingEdit => _session != null;
    internal bool HasPendingDataEdit => _session != null && (_session.Column == 1
        ? !Equals(_product.BootstrapControl.SelectedValue, _session.OriginalValue)
        : !string.Equals(_session.RawCandidate, _session.OriginalRaw, StringComparison.Ordinal));

    internal bool TryFinishEdit(ReceiptEditIntent intent)
    {
        if (_disposed || _grid.IsDisposed) return false;
        if (_session == null) return true;
        if (!Current(_session)) { CancelActiveEdit(); return false; }
        if (intent == ReceiptEditIntent.RemoveLine) { CancelActiveEdit(); return true; }
        if (intent == ReceiptEditIntent.NewReceipt || intent == ReceiptEditIntent.SelectReceipt || intent == ReceiptEditIntent.Refresh)
        {
            if (HasPendingDataEdit)
            {
                EditError = "Save or cancel the current cell edit before leaving the document.";
                return false;
            }
            CancelActiveEdit(); // A query alone is not a committed product or dirty data.
            return true;
        }
        var success = _session.Context.EndEdit(false);
        if (success && _structurePending) Rebuild(_editor.Lines, false);
        return success;
    }

    internal void CancelActiveEdit()
    {
        var session = _session;
        _session = null; // Invalidate callbacks before native cancellation/focus events.
        if (session != null && !_grid.IsDisposed && session.Input.IsEditing)
            session.Input.EditCellContext.EndEdit(true);
        EditError = null;
        if (session?.Column == 1 && !_disposed) _ = _editor.ProductOptions.SetSearch(null);
        if (!_disposed && !_resetting)
        {
            if (_structurePending) Rebuild(_editor.Lines, false);
            else RefreshProjection();
        }
    }

    internal void ResetDocument(IReadOnlyList<GoodsReceiptLineViewModel> lines)
    {
        Rebuild(lines, true);
        // Explicit document reset is the final mapping boundary after the VM's incremental
        // Clear/Add load notifications. A missing persisted target now settles on the fallback.
        _selectionPending = false;
    }

    private void Rebuild(IReadOnlyList<GoodsReceiptLineViewModel> lines, bool resetCulture)
    {
        if (_disposed || _grid.IsDisposed) return;
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        var snapshot = lines.ToArray();
        if (snapshot.Any(line => !_editor.Lines.Any(current => ReferenceEquals(current, line))) || snapshot.Distinct().Count() != snapshot.Length)
            throw new ArgumentException("Projection requires distinct current receipt line references.", nameof(lines));
        var position = _grid.Selection.ActivePosition;
        var selected = !_selectionPending && position.Row > 0 && position.Row <= _lines.Count ? _lines[position.Row - 1] : null;
        if (selected != null && !_selectionPending)
        {
            _selectionRow = position.Row;
            _selectionColumn = Math.Max(1, Math.Min(3, position.Column));
            _selectionId = selected.Id > 0 ? selected.Id : (long?)null;
        }
        if (_receiptId != _editor.Id) { _selectionId = null; _selectionRow = 1; _selectionPending = false; }
        _receiptId = _editor.Id;
        _resetting = true;
        try
        {
            _generation++;
            CancelActiveEdit();
            _structurePending = false;
            DetachLines();
            _lines.AddRange(snapshot);
            if (resetCulture) _culture = CultureInfo.ReadOnly((CultureInfo)CultureInfo.CurrentCulture.Clone());
            _quantity.CultureInfo = _cost.CultureInfo = _culture;
            _grid.Redim(snapshot.Length + 1, 5);
            _grid.FixedRows = 1;
            var names = new[] { "#", "Product", "Quantity", "Unit cost", "Total" };
            for (var column = 0; column < 5; column++) _grid[0, column] = new SourceGrid.Cells.ColumnHeader(names[column]);
            for (var index = 0; index < snapshot.Length; index++)
            {
                var line = snapshot[index];
                line.PropertyChanged += OnLineChanged;
                var row = index + 1;

                _grid[row, 0] = new Cell(row);
                _grid[row, 4] = new Cell(line.LineTotal.HasValue ? (object)line.LineTotal.Value : "Amount too large");
                _grid[row, 0].AddController(Unselectable.Default);
                _grid[row, 4].AddController(Unselectable.Default);
                for (var column = 1; column <= 3; column++)
                {
                    var input = column == 1 ? (EditorControlBase)_product : column == 2 ? _quantity : _cost;
                    var cell = new Cell(LineValue(line, column), column == 1 ? typeof(int) : typeof(decimal)) { Editor = input };
                    var controller = new LineController(this, line, column, _generation);
                    cell.AddController(controller);
                    _controllers.Add((cell, controller));
                    _grid[row, column] = cell;
                }
            }
            _grid.Columns[0].Width = 45;
            _grid.Columns[1].Width = 290;
            for (var column = 2; column < 5; column++) _grid.Columns[column].Width = 145;
            RefreshAppearance();
            BindProductOptions();
            RefreshEditability();
            _selectionPending = _selectionId.HasValue
                ? !snapshot.Any(line => line.Id > 0 && line.Id == _selectionId)
                : snapshot.Length < _selectionRow;
            if (snapshot.Length > 0)
            {
                var row = selected == null ? -1 : Array.FindIndex(snapshot, line => ReferenceEquals(line, selected));
                if (row < 0 && _selectionId.HasValue) row = Array.FindIndex(snapshot, line => line.Id > 0 && line.Id == _selectionId);
                // New ID-zero rows use their reference, then an explicit nearest-row fallback.
                if (row < 0) row = Math.Max(0, Math.Min(_selectionRow - 1, snapshot.Length - 1));
                _grid.Selection.Focus(new Position(row + 1, _selectionColumn), true);
            }
        }
        finally { _resetting = false; }
    }

    internal void RefreshEditability()
    {
        if (_disposed || _grid.IsDisposed) return;
        var editable = Editable;
        if (!editable) CancelActiveEdit();
        foreach (var input in Inputs) { input.EnableEdit = editable; input.Control.Enabled = editable; }
    }

    private bool Current(Session session) => Editable && session.Generation == _generation &&
        _lines.Any(line => ReferenceEquals(line, session.Line)) && _editor.Lines.Any(line => ReferenceEquals(line, session.Line)) &&
        session.Context.Position.Row > 0 && session.Context.Position.Row < _grid.RowsCount &&
        ReferenceEquals(_grid[session.Context.Position.Row, session.Column], session.Context.Cell);

    private void Begin(LineController controller, CellContext context, CancelEventArgs e)
    {
        if (!Editable || controller.Generation != _generation ||
            !_editor.Lines.Any(line => ReferenceEquals(line, controller.Line))) { e.Cancel = true; return; }
        if (_session != null) { e.Cancel = true; return; }
        if (controller.Column == 1) BindProductOptions();
        var input = (EditorControlBase)context.Cell.Editor;
        _session = new Session(controller.Line, controller.Column, _generation, context, input, LineValue(controller.Line, controller.Column));
        input.Control.AccessibleName = (controller.Column == 1 ? "Product" : controller.Column == 2 ? "Quantity" : "Unit cost") +
            " row " + context.Position.Row + " editor";
        input.Control.AccessibleDescription = AccessibleHelp(_generation, controller.Line, controller.Column);
        EditError = null;
    }

    private void ConvertCandidate(object? sender, DevAge.ComponentModel.ConvertingObjectEventArgs e)
    {
        var session = _session;
        if (session != null) session.CandidateValidated = false;
        if (session == null || !ReferenceEquals(sender, session.Input) || !Current(session))
        {
            EditError = "The edited line is no longer available.";
            e.ConvertingStatus = DevAge.ComponentModel.ConvertingStatus.Error;
            return;
        }
        object? value;
        if (session.Column == 1)
        {
            value = _product.BootstrapControl.SelectedValue;
            // The hosted popup can intentionally retain its presentation snapshot. Admission
            // uses current VM options, so a rebound inactive choice cannot reuse an old grant.
            var option = value is int selectedId ? _editor.ProductOptions.Items.FirstOrDefault(item => item.Id == selectedId) : null;
            if (_product.BootstrapControl.HasPendingText || !(value is int id) || id <= 0 ||
                (id != session.Line.ProductId && (option == null || !option.IsAvailableForNewSelection)))
            {
                EditError = "Choose a product from the current results or cancel the pending query.";
                e.ConvertingStatus = DevAge.ComponentModel.ConvertingStatus.Error;
                return;
            }
        }
        else
        {
            var raw = Convert.ToString(e.Value, _culture) ?? string.Empty;
            if (!ReceiptNumericInput.TryParse(raw, session.Column == 2 ? ReceiptNumericField.Quantity : ReceiptNumericField.UnitCost,
                _culture, out var number, out var error))
            {
                EditError = error;
                e.ConvertingStatus = DevAge.ComponentModel.ConvertingStatus.Error;
                return;
            }
            value = number;
        }
        e.Value = value;
        e.ConvertingStatus = DevAge.ComponentModel.ConvertingStatus.Completed;
        session.CandidateValidated = true;
        session.ValidatedValue = value;
        EditError = null;
    }

    private void Commit(LineController controller, CellContext context)
    {
        var session = _session;
        if (_projecting || session == null || !session.CandidateValidated || !Equals(context.Value, session.ValidatedValue) || controller.Generation != session.Generation ||
            !ReferenceEquals(controller.Line, session.Line) || controller.Column != session.Column ||
            !ReferenceEquals(context.Cell, session.Context.Cell) || !Current(session)) return;
        session.CandidateValidated = false;
        if (controller.Column == 2) session.Line.Quantity = (decimal)context.Value;
        else if (controller.Column == 3) session.Line.UnitCost = (decimal)context.Value;
        else
        {
            var option = _editor.ProductOptions.Items.FirstOrDefault(item => item.Id == (int)context.Value);
            session.Line.SetProduct((int)context.Value, option?.DisplayName ?? session.Line.ProductDisplayName,
                option?.IsAvailableForNewSelection ?? session.Line.ProductIsAvailable);
        }
    }

    private void End(LineController controller, CellContext context)
    {
        if (_session != null && _session.CandidateValidated) Commit(controller, context);
        if (_session != null && controller.Generation == _session.Generation && ReferenceEquals(context.Cell, _session.Context.Cell))
        {
            var wasProduct = _session.Column == 1;
            _session = null;
            EditError = null;
            if (wasProduct && !_disposed) _ = _editor.ProductOptions.SetSearch(null);
        }
        if (_disposed || _resetting) return;
        if (_structurePending)
        {
            // SourceGrid is enumerating this cell's controllers here. Detaching/rebuilding
            // synchronously would invalidate that enumeration. Commands flush after EndEdit;
            // native focus/navigation completes via this generation-fenced callback.
            var generation = _generation;
            _grid.BeginInvoke(new Action(() =>
            {
                if (!_disposed && !_grid.IsDisposed && generation == _generation && _session == null && _structurePending)
                    Rebuild(_editor.Lines, false);
            }));
        }
        else RefreshProjection();
    }

    private void RefreshProjection()
    {
        if (_disposed || _grid.IsDisposed || _resetting) return;
        _projecting = true;
        try
        {
            for (var index = 0; index < _lines.Count; index++)
            {
                var line = _lines[index];
                for (var column = 1; column <= 4; column++)
                {
                    var cell = _grid[index + 1, column];
                    if (_session != null && ReferenceEquals(cell, _session.Context.Cell)) continue;
                    var value = LineValue(line, column);
                    if (!Equals(cell.Value, value)) cell.Value = value;
                }
            }
            _grid.Invalidate(); // Repaint never replaces an editor/cell or resizes the active row.
        }
        finally { _projecting = false; }
    }

    private static object LineValue(GoodsReceiptLineViewModel line, int column) => column == 1 ? (object)line.ProductId :
        column == 2 ? line.Quantity : column == 3 ? line.UnitCost : line.LineTotal.HasValue ? (object)line.LineTotal.Value : "Amount too large";

    private void BindProductOptions()
    {
        if (_bindingOptions) return;
        var options = _editor.ProductOptions.Items.ToList();
        foreach (var line in _lines.Where(line => line.ProductId > 0))
            if (options.All(option => option.Id != line.ProductId))
                options.Add(new CatalogLookupOption(line.ProductId, "", "", line.ProductIsAvailable, line.ProductDisplayName));
        var lookup = _product.BootstrapControl;
        _bindingOptions = true;
        try
        {
            // The pinned lookup reconciles its committed identity on DataSource replacement
            // without touching pending text/caret. Reselecting here would resolve/reset input.
            lookup.DataSource = options.ToArray();
        }
        finally { _bindingOptions = false; }
    }

    private string ProductCaption(int id) => _editor.ProductOptions.Items.FirstOrDefault(option => option.Id == id)?.DisplayName ??
        _lines.FirstOrDefault(line => line.ProductId == id)?.ProductDisplayName ?? (id == 0 ? "" : id.ToString(_culture));

    private bool _sizing;
    private void OnAppearanceChanged(object? sender, EventArgs e) => RefreshAppearance();
    private void OnGridThemeChanged(object? sender, MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeChangedEventArgs e) => Dispatch(RefreshAppearance);
    private void RefreshAppearance()
    {
        if (_disposed || _sizing || _grid.IsDisposed || _grid.ColumnsCount != 5) return;
        _sizing = true;
        try
        {
            var scale = new Func<int, int>(logical => BusinessCompactAppearance.Scale(_grid, logical));
            var height = Math.Max(scale(36), System.Windows.Forms.TextRenderer.MeasureText("123", _grid.Font).Height + scale(12));
            foreach (var input in Inputs)
            {
                input.Control.Font = _grid.Font;
                height = Math.Max(height, Math.Max(input.Control.MinimumSize.Height, input.Control.GetPreferredSize(new System.Drawing.Size(scale(200), 0)).Height) + scale(4));
            }
            var width = Math.Max(scale(145), System.Windows.Forms.TextRenderer.MeasureText("999999999999999.9999", _grid.Font).Width + scale(48));
            _grid.Columns[0].Width = scale(45);
            for (var column = 2; column < 5; column++) _grid.Columns[column].Width = width;
            _grid.Columns[1].Width = Math.Max(scale(240), _grid.ClientSize.Width - scale(45) - width * 3 - System.Windows.Forms.SystemInformation.VerticalScrollBarWidth);
            var dark = MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme.Mode == MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeMode.Dark;
            for (var row = 0; row < _grid.RowsCount; row++)
            {
                _grid.Rows[row].Height = height;
                for (var column = 0; column < 5; column++)
                {
                    var view = new SourceGrid.Cells.Views.Cell
                    {
                        Font = _grid.Font,
                        TextAlignment = column >= 2 ? DevAge.Drawing.ContentAlignment.MiddleRight : DevAge.Drawing.ContentAlignment.MiddleLeft,
                        BackColor = dark ? System.Drawing.Color.FromArgb(34, 45, 60) : System.Drawing.Color.White,
                        ForeColor = dark ? System.Drawing.Color.FromArgb(230, 236, 244) : System.Drawing.Color.FromArgb(31, 45, 61),
                    };
                    _grid[row, column].View = view;
                }
            }
            _grid.ArrangeLinkedControls();
        }
        finally { _sizing = false; }
    }

    private void DisplayProduct(object? sender, DevAge.ComponentModel.ConvertingObjectEventArgs e)
    {
        var id = e.Value is int key ? key : 0;
        e.Value = ProductCaption(id);
        e.ConvertingStatus = DevAge.ComponentModel.ConvertingStatus.Completed;
    }
    private void OnCandidateChanged(object? sender, EventArgs e)
    {
        if (_session != null && _session.Input.IsEditing)
        {
            _session.RawCandidate = _session.Input.Control.Text;
            if (!_bindingOptions && _session.Column == 1 && _product.BootstrapControl.HasPendingText)
                _ = _editor.ProductOptions.SetSearch(_session.RawCandidate);
        }
    }
    private void OnEditException(object sender, ExceptionEventArgs e) => EditError ??= "The cell value could not be committed.";
    private void OnGridDisposed(object? sender, EventArgs e) => Dispose();
    private void OnLineChanged(object? sender, PropertyChangedEventArgs e) => Dispatch(RefreshProjection);
    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e) => Dispatch(() =>
    {
        if (_receiptId != _editor.Id) Rebuild(_editor.Lines, true);
        RefreshEditability();
        RefreshProjection();
    });
    private void OnOptionsChanged(object? sender, PropertyChangedEventArgs e) => Dispatch(() => { BindProductOptions(); RefreshProjection(); });
    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Dispatch(() =>
    {
        if (_session != null && _editor.Lines.IndexOf(_session.Line) == _session.Context.Position.Row - 1)
        {
            _structurePending = true;
            RefreshProjection();
        }
        else Rebuild(_editor.Lines, e.Action == NotifyCollectionChangedAction.Reset);
    });
    private void Dispatch(Action action)
    {
        if (_disposed || _grid.IsDisposed) return;
        var generation = _generation;
        if (_grid.InvokeRequired)
        {
            _grid.BeginInvoke(new Action(() => { if (!_disposed && !_grid.IsDisposed && generation == _generation) action(); }));
        }
        else action();
    }
    private void DetachLines()
    {
        foreach (var line in _lines) line.PropertyChanged -= OnLineChanged;
        foreach (var item in _controllers) item.Cell.RemoveController(item.Controller);
        _controllers.Clear();
        _lines.Clear();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        CancelActiveEdit();
        _editor.PropertyChanged -= OnEditorChanged;
        _editor.Lines.CollectionChanged -= OnLinesChanged;
        _editor.ProductOptions.PropertyChanged -= OnOptionsChanged;
        _grid.Disposed -= OnGridDisposed;
        _grid.SizeChanged -= OnAppearanceChanged;
        _grid.FontChanged -= OnAppearanceChanged;
        MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.ThemeChanged -= OnGridThemeChanged;
        DetachLines();
        foreach (var input in Inputs)
        {
            input.EnableEdit = false;
            input.EditException -= OnEditException;
            input.ConvertingObjectToValue -= ConvertCandidate;
            input.Control.TextChanged -= OnCandidateChanged;
        }
        _product.ConvertingValueToDisplayString -= DisplayProduct;
        _grid.Adapter = null;
        _grid.BootstrapEditors.Dispose(); // Registry owns the lookup. App owns the two numeric adapters.
        foreach (var input in new EditorControlBase[] { _quantity, _cost }) { input.Control.Dispose(); input.Dispose(); }
    }

    private sealed class Session
    {
        internal Session(GoodsReceiptLineViewModel line, int column, long generation, CellContext context, EditorControlBase input, object value)
        { Line = line; Column = column; Generation = generation; Context = context; Input = input; OriginalValue = value; }
        internal readonly GoodsReceiptLineViewModel Line;
        internal readonly int Column;
        internal readonly long Generation;
        internal readonly CellContext Context;
        internal readonly EditorControlBase Input;
        internal readonly object OriginalValue;
        internal string OriginalRaw = "";
        internal string RawCandidate = "";
        internal bool CandidateValidated;
        internal object? ValidatedValue;
    }
    private sealed class LineController : ControllerBase
    {
        private readonly GoodsReceiptLinesGridAdapter _owner;
        internal readonly GoodsReceiptLineViewModel Line;
        internal readonly int Column;
        internal readonly long Generation;
        internal LineController(GoodsReceiptLinesGridAdapter owner, GoodsReceiptLineViewModel line, int column, long generation)
        { _owner = owner; Line = line; Column = column; Generation = generation; }
        public override void OnEditStarting(CellContext sender, CancelEventArgs e) => _owner.Begin(this, sender, e);
        public override void OnEditStarted(CellContext sender, EventArgs e)
        {
            if (_owner._session != null && ReferenceEquals(sender.Cell, _owner._session.Context.Cell))
                _owner._session.OriginalRaw = _owner._session.RawCandidate = _owner._session.Input.Control.Text;
        }
        public override void OnValueChanged(CellContext sender, EventArgs e) => _owner.Commit(this, sender);
        public override void OnEditEnded(CellContext sender, EventArgs e) => _owner.End(this, sender);
    }
}
