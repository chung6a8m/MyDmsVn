using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using BootstrapGrid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

namespace MyDmsVn.Desktop.WinForms
{
    public interface IGoodsReceiptControlFactory
    {
        Control Create();
    }

    public sealed class GoodsReceiptControl : UserControl
    {
        private readonly GoodsReceiptListViewModel _list;
        private readonly GoodsReceiptEditorViewModel _editor;
        private readonly ErrorProvider _errors;
        private readonly Dictionary<string, Control> _errorControls;
        private readonly List<GoodsReceiptLineViewModel> _subscribedLines =
            new List<GoodsReceiptLineViewModel>();
        private readonly int _uiThreadId;
        private IReadOnlyList<long> _receiptIds = Array.Empty<long>();
        private GoodsReceiptLineViewModel? _selectedLine;
        private int? _warehouseFilterId;
        private string? _statusFilter;
        private string? _workflowMessage;
        private bool _isRefreshing;
        private bool _isAccessDeniedEditorLocked;
        private bool _applyingState;

        public GoodsReceiptControl(
            GoodsReceiptListViewModel list,
            GoodsReceiptEditorViewModel editor)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
            Dock = DockStyle.Fill;

            SearchBox = new BootstrapTextBox { Dock = DockStyle.Fill, AccessibleName = "Search goods receipts" };
            WarehouseFilterLookup = CreateLookup("Filter by warehouse");
            StatusFilter = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = "Filter by status",
            };
            StatusFilter.Items.AddRange(new object[] { "All", "Draft", "Posted" });
            StatusFilter.SelectedIndex = 0;
            RefreshButton = CreateButton("Refresh", () => SetOperation(RefreshAsync()));
            NewButton = CreateButton("New", BeginNew);
            PreviousPageButton = CreateButton("Previous", () => MovePage(-1));
            NextPageButton = CreateButton("Next", () => MovePage(1));
            PageLabel = new Label { AutoSize = true, TextAlign = ContentAlignment.MiddleCenter };
            ReceiptGrid = new BootstrapGrid { Dock = DockStyle.Fill, TabStop = true };
            ReceiptGrid.DoubleClick += (_, __) => SelectActiveReceipt();
            ReceiptGrid.KeyDown += OnReceiptGridKeyDown;

            ReceiptDateEditor = new BootstrapDatePicker { Dock = DockStyle.Top, AccessibleName = "Receipt date" };
            WarehouseLookup = CreateLookup("Warehouse");
            EmployeeLookup = CreateLookup("Employee");
            NoteEditor = new BootstrapTextBox { Dock = DockStyle.Top, AccessibleName = "Note" };
            StatusLabel = new Label { AutoSize = true };
            VersionLabel = new Label { AutoSize = true };
            TotalLabel = new Label { AutoSize = true, TextAlign = ContentAlignment.MiddleRight };
            OperationStatusLabel = new Label { AutoSize = true, Text = "Ready" };
            BusyIndicator = new BootstrapProgressBar
            {
                Width = 100,
                Height = 20,
                Indeterminate = true,
                Animated = true,
                ShowText = false,
                Visible = false,
            };
            SaveButton = CreateButton("Save", () => SetOperation(SaveAsync()));
            PostButton = CreateButton("Post", () => SetOperation(PostAsync()));

            LineGrid = new BootstrapGrid { Dock = DockStyle.Fill, TabStop = true };
            LineGrid.Click += (_, __) => SelectActiveLine();
            LineGrid.KeyDown += OnLineGridKeyDown;
            ProductLookup = CreateLookup("Product");
            QuantityEditor = new BootstrapNumericBox
            {
                Dock = DockStyle.Top,
                DecimalPlaces = GoodsReceiptContractLimits.DecimalPlaces,
                Minimum = 0,
                Maximum = GoodsReceiptContractLimits.MaximumQuantity,
                AccessibleName = "Quantity",
            };
            UnitCostEditor = new BootstrapNumericBox
            {
                Dock = DockStyle.Top,
                DecimalPlaces = GoodsReceiptContractLimits.DecimalPlaces,
                Minimum = 0,
                Maximum = GoodsReceiptContractLimits.MaximumUnitCost,
                AccessibleName = "Unit cost",
            };
            AddLineButton = CreateButton("Add line", AddLine);
            RemoveLineButton = CreateButton("Remove line", RemoveSelectedLine);

            _errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            _errorControls = new Dictionary<string, Control>(StringComparer.Ordinal)
            {
                ["receiptDate"] = ReceiptDateEditor,
                ["warehouseId"] = WarehouseLookup,
                ["employeeId"] = EmployeeLookup,
                ["lines"] = LineGrid,
            };

            Controls.Add(CreateLayout());
            SearchBox.TextChanged += OnSearchChanged;
            WarehouseFilterLookup.TextChanged += OnWarehouseLookupSearchChanged;
            WarehouseFilterLookup.SelectedValueChanged += OnWarehouseFilterChanged;
            StatusFilter.SelectedValueChanged += OnStatusFilterChanged;
            ReceiptDateEditor.ValueChanged += OnReceiptDateChanged;
            WarehouseLookup.SelectedValueChanged += OnWarehouseChanged;
            WarehouseLookup.TextChanged += OnWarehouseLookupSearchChanged;
            EmployeeLookup.SelectedValueChanged += OnEmployeeChanged;
            EmployeeLookup.TextChanged += OnEmployeeLookupSearchChanged;
            NoteEditor.TextChanged += OnNoteChanged;
            ProductLookup.SelectedValueChanged += OnProductChanged;
            ProductLookup.TextChanged += OnProductLookupSearchChanged;
            QuantityEditor.ValueChanged += OnQuantityChanged;
            UnitCostEditor.ValueChanged += OnUnitCostChanged;
            _list.PropertyChanged += OnListViewModelPropertyChanged;
            _editor.PropertyChanged += OnViewModelPropertyChanged;
            _editor.WarehouseLookup.PropertyChanged += OnViewModelPropertyChanged;
            _editor.EmployeeLookup.PropertyChanged += OnViewModelPropertyChanged;
            _editor.ProductOptions.PropertyChanged += OnViewModelPropertyChanged;
            _editor.ErrorsChanged += OnErrorsChanged;
            _editor.Lines.CollectionChanged += OnLinesChanged;
            SynchronizeLineSubscriptions();
            ApplyState();
        }

        public BootstrapTextBox SearchBox { get; }
        public BootstrapLookupBox WarehouseFilterLookup { get; }
        public ComboBox StatusFilter { get; }
        public BootstrapGrid ReceiptGrid { get; }
        public BootstrapGrid LineGrid { get; }
        public BootstrapDatePicker ReceiptDateEditor { get; }
        public BootstrapLookupBox WarehouseLookup { get; }
        public BootstrapLookupBox EmployeeLookup { get; }
        public BootstrapLookupBox ProductLookup { get; }
        public BootstrapTextBox NoteEditor { get; }
        public BootstrapNumericBox QuantityEditor { get; }
        public BootstrapNumericBox UnitCostEditor { get; }
        public BootstrapButton RefreshButton { get; }
        public BootstrapButton PreviousPageButton { get; }
        public BootstrapButton NextPageButton { get; }
        public Label PageLabel { get; }
        public BootstrapButton NewButton { get; }
        public BootstrapButton SaveButton { get; }
        public BootstrapButton PostButton { get; }
        public BootstrapButton AddLineButton { get; }
        public BootstrapButton RemoveLineButton { get; }
        public Label StatusLabel { get; }
        public Label VersionLabel { get; }
        public Label TotalLabel { get; }
        public Label OperationStatusLabel { get; }
        public BootstrapProgressBar BusyIndicator { get; }
        public Task LastOperation { get; private set; } = Task.CompletedTask;

        public Task ActivateAsync(CancellationToken cancellationToken)
        {
            LastOperation = ActivateCoreAsync(cancellationToken);
            return LastOperation;
        }

        public Task SelectReceiptAsync(long id, CancellationToken cancellationToken)
        {
            LastOperation = SelectReceiptCoreAsync(id, cancellationToken);
            return LastOperation;
        }

        public string GetFieldError(string field)
        {
            if (_errorControls.TryGetValue(field, out var control))
            {
                return _errors.GetError(control);
            }

            if (TryGetLineErrorControl(field, out control))
            {
                return _errors.GetError(control);
            }

            return string.Empty;
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
            ApplyState();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SearchBox.TextChanged -= OnSearchChanged;
                WarehouseFilterLookup.TextChanged -= OnWarehouseLookupSearchChanged;
                WarehouseFilterLookup.SelectedValueChanged -= OnWarehouseFilterChanged;
                StatusFilter.SelectedValueChanged -= OnStatusFilterChanged;
                ReceiptDateEditor.ValueChanged -= OnReceiptDateChanged;
                WarehouseLookup.SelectedValueChanged -= OnWarehouseChanged;
                WarehouseLookup.TextChanged -= OnWarehouseLookupSearchChanged;
                EmployeeLookup.SelectedValueChanged -= OnEmployeeChanged;
                EmployeeLookup.TextChanged -= OnEmployeeLookupSearchChanged;
                NoteEditor.TextChanged -= OnNoteChanged;
                ProductLookup.SelectedValueChanged -= OnProductChanged;
                ProductLookup.TextChanged -= OnProductLookupSearchChanged;
                QuantityEditor.ValueChanged -= OnQuantityChanged;
                UnitCostEditor.ValueChanged -= OnUnitCostChanged;
                _list.PropertyChanged -= OnListViewModelPropertyChanged;
                _editor.PropertyChanged -= OnViewModelPropertyChanged;
                _editor.WarehouseLookup.PropertyChanged -= OnViewModelPropertyChanged;
                _editor.EmployeeLookup.PropertyChanged -= OnViewModelPropertyChanged;
                _editor.ProductOptions.PropertyChanged -= OnViewModelPropertyChanged;
                _editor.ErrorsChanged -= OnErrorsChanged;
                _editor.Lines.CollectionChanged -= OnLinesChanged;
                UnsubscribeLines();
                _errors.Dispose();
                _list.Dispose();
                _editor.Dispose();
            }

            base.Dispose(disposing);
        }

        private async Task ActivateCoreAsync(CancellationToken cancellationToken)
        {
            await _list.ActivateAsync(cancellationToken).ConfigureAwait(true);
            await _editor.ActivateAsync(cancellationToken).ConfigureAwait(true);
            ApplyListViewModelState();
        }

        private async Task RefreshAsync()
        {
            if (!CanAbandonEditor())
            {
                return;
            }

            var selectedLineId = _selectedLine?.Id;
            var selectedLineIndex = _selectedLine == null
                ? -1
                : _editor.Lines.IndexOf(_selectedLine);
            _isRefreshing = true;
            ApplyState();
            try
            {
                await _list.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
                if (_list.IsAccessDenied)
                {
                    EnterAccessDeniedEditorState(false);
                }
                else if (_editor.Id.HasValue)
                {
                    if (await _editor.RefreshAsync(CancellationToken.None).ConfigureAwait(true))
                    {
                        RebindSelectedLine(selectedLineId, selectedLineIndex);
                    }
                }
            }
            finally
            {
                _isRefreshing = false;
                ApplyState();
            }
        }

        private async Task SelectReceiptCoreAsync(long id, CancellationToken cancellationToken)
        {
            if (!CanAbandonEditor())
            {
                return;
            }

            var selected = await _list.SelectAsync(id, cancellationToken).ConfigureAwait(true);
            if (selected == null)
            {
                if (_list.IsAccessDenied)
                {
                    EnterAccessDeniedEditorState(false);
                }
                ApplyState();
                return;
            }

            _workflowMessage = null;
            _editor.Load(selected);
            _selectedLine = _editor.Lines.FirstOrDefault();
            ApplyState();
        }

        private async Task SaveAsync()
        {
            if (ApplyEditorsToViewModel())
            {
                var selectedLineId = _selectedLine?.Id;
                var selectedLineIndex = _selectedLine == null
                    ? -1
                    : _editor.Lines.IndexOf(_selectedLine);
                var saved = await _editor.SaveAsync(CancellationToken.None).ConfigureAwait(true);
                if (saved)
                {
                    RebindSelectedLine(selectedLineId, selectedLineIndex);
                    _workflowMessage = null;
                    await _list.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
                    if (_list.IsAccessDenied)
                    {
                        EnterAccessDeniedEditorState(true);
                    }
                }
            }
            ApplyState();
        }

        private async Task PostAsync()
        {
            var posted = await _editor.PostAsync(CancellationToken.None).ConfigureAwait(true);
            if (posted)
            {
                _workflowMessage = null;
                await _list.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
                if (_list.IsAccessDenied)
                {
                    EnterAccessDeniedEditorState(true);
                }
            }
            ApplyState();
        }

        private void SetOperation(Task operation)
        {
            LastOperation = CompleteOperationAsync(operation);
        }

        private async Task CompleteOperationAsync(Task operation)
        {
            await operation.ConfigureAwait(true);
            ApplyState();
        }

        private void BeginNew()
        {
            if (!CanAbandonEditor())
            {
                return;
            }

            _workflowMessage = null;
            _isAccessDeniedEditorLocked = false;
            _list.NewDraft();
            _editor.Load(null);
            _selectedLine = null;
            ApplyState();
        }

        private bool CanAbandonEditor()
        {
            if (!_editor.IsDirty)
            {
                return true;
            }

            _workflowMessage = "Save the current draft before leaving it.";
            ApplyState();
            return false;
        }

        private void ClearPersistedEditor()
        {
            if (_editor.Id.HasValue)
            {
                _editor.Load(null);
                _selectedLine = null;
            }
        }

        private void EnterAccessDeniedEditorState(bool saved)
        {
            if (saved)
            {
                _isAccessDeniedEditorLocked = true;
                _workflowMessage =
                    "Goods receipt was saved. Read access is required to view or edit it.";
            }

            if (!_editor.Id.HasValue)
            {
                return;
            }

            _isAccessDeniedEditorLocked = true;
            ClearPersistedEditor();
        }

        private void RebindSelectedLine(long? selectedLineId, int selectedLineIndex)
        {
            var persistedId = selectedLineId.GetValueOrDefault();
            _selectedLine = persistedId > 0
                ? _editor.Lines.FirstOrDefault(line => line.Id == persistedId)
                : null;
            if (_selectedLine == null &&
                selectedLineIndex >= 0 &&
                selectedLineIndex < _editor.Lines.Count)
            {
                _selectedLine = _editor.Lines[selectedLineIndex];
            }

            if (_selectedLine == null)
            {
                _selectedLine = _editor.Lines.FirstOrDefault();
            }
        }

        private void MovePage(int offset)
        {
            var target = _list.PageNumber + offset;
            var pageCount = Math.Max(1, (int)Math.Ceiling(_list.TotalCount / (double)_list.PageSize));
            if (target >= 1 && target <= pageCount)
            {
                _list.PageNumber = target;
                SetOperation(_list.RefreshAsync(CancellationToken.None));
            }
        }

        private void AddLine()
        {
            if (_editor.IsReadOnly)
            {
                return;
            }
            _selectedLine = _editor.AddLine();
            ApplyState();
        }

        private void RemoveSelectedLine()
        {
            if (_selectedLine == null || _editor.IsReadOnly)
            {
                return;
            }
            _editor.RemoveLine(_selectedLine);
            _selectedLine = _editor.Lines.FirstOrDefault();
            ApplyState();
        }

        private bool ApplyEditorsToViewModel()
        {
            if (_editor.IsReadOnly)
            {
                return false;
            }
            _editor.ReceiptDate = ReceiptDateEditor.Value;
            _editor.Note = NoteEditor.Text;
            ApplySelectedLineEditors();
            return true;
        }

        private void ApplySelectedLineEditors()
        {
            if (_selectedLine == null || _editor.IsReadOnly)
            {
                return;
            }
            _selectedLine.Quantity = QuantityEditor.Value;
            _selectedLine.UnitCost = UnitCostEditor.Value;
        }

        private void SelectActiveReceipt()
        {
            var row = ReceiptGrid.Selection.ActivePosition.Row;
            if (row > 0 && row - 1 < _receiptIds.Count)
            {
                SetOperation(SelectReceiptCoreAsync(_receiptIds[row - 1], CancellationToken.None));
            }
        }

        private void SelectActiveLine()
        {
            var row = LineGrid.Selection.ActivePosition.Row;
            if (row > 0 && row - 1 < _editor.Lines.Count)
            {
                ApplySelectedLineEditors();
                _selectedLine = _editor.Lines[row - 1];
                ApplyState();
            }
        }

        private void OnReceiptGridKeyDown(object? sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode == Keys.Enter && eventArgs.Modifiers == Keys.None)
            {
                SelectActiveReceipt();
                eventArgs.Handled = true;
                eventArgs.SuppressKeyPress = true;
            }
        }

        private void OnLineGridKeyDown(object? sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode == Keys.Enter && eventArgs.Modifiers == Keys.None)
            {
                SelectActiveLine();
                eventArgs.Handled = true;
                eventArgs.SuppressKeyPress = true;
            }
        }

        private void OnSearchChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState)
            {
                SetOperation(_list.SetSearch(SearchBox.Text));
            }
        }

        private void OnWarehouseLookupSearchChanged(object? sender, EventArgs eventArgs)
        {
            var lookup = (BootstrapLookupBox)sender!;
            if (!_applyingState && lookup.HasPendingText)
            {
                SetOperation(_editor.WarehouseLookup.SetSearch(lookup.Text));
            }
        }

        private void OnEmployeeLookupSearchChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && EmployeeLookup.HasPendingText)
            {
                SetOperation(_editor.EmployeeLookup.SetSearch(EmployeeLookup.Text));
            }
        }

        private void OnProductLookupSearchChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && ProductLookup.HasPendingText)
            {
                SetOperation(_editor.ProductOptions.SetSearch(ProductLookup.Text));
            }
        }

        private void OnWarehouseFilterChanged(object? sender, EventArgs eventArgs)
        {
            if (_applyingState)
            {
                return;
            }

            _warehouseFilterId = WarehouseFilterLookup.SelectedValue as int?;
            SetOperation(
                _list.SetWarehouseFilterAsync(
                    _warehouseFilterId,
                    CancellationToken.None));
        }

        private void OnStatusFilterChanged(object? sender, EventArgs eventArgs)
        {
            if (_applyingState)
            {
                return;
            }

            _statusFilter = string.Equals(StatusFilter.SelectedItem as string, "All", StringComparison.Ordinal)
                ? null
                : StatusFilter.SelectedItem as string;
            SetOperation(
                _list.SetStatusFilterAsync(
                    _statusFilter,
                    CancellationToken.None));
        }

        private void OnReceiptDateChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && !_editor.IsReadOnly)
            {
                _editor.ReceiptDate = ReceiptDateEditor.Value;
            }
        }

        private void OnWarehouseChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && TrySelectedOption(WarehouseLookup, out var option))
            {
                _editor.SetWarehouse(option.Id, option.DisplayName, option.IsAvailableForNewSelection);
            }
        }

        private void OnEmployeeChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && TrySelectedOption(EmployeeLookup, out var option))
            {
                _editor.SetEmployee(option.Id, option.DisplayName, option.IsAvailableForNewSelection);
            }
        }

        private void OnProductChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && _selectedLine != null && TrySelectedOption(ProductLookup, out var option))
            {
                _selectedLine.SetProduct(option.Id, option.DisplayName, option.IsAvailableForNewSelection);
            }
        }

        private void OnNoteChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && !_editor.IsReadOnly)
            {
                _editor.Note = NoteEditor.Text;
            }
        }

        private void OnQuantityChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState)
            {
                ApplySelectedLineEditors();
            }
        }

        private void OnUnitCostChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState)
            {
                ApplySelectedLineEditors();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) =>
            DispatchToUi(ApplyState);

        private void OnListViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) =>
            DispatchToUi(
                eventArgs.PropertyName == nameof(GoodsReceiptListViewModel.IsAccessDenied)
                    ? (Action)ApplyListViewModelState
                    : ApplyState);

        private void ApplyListViewModelState()
        {
            if (_list.IsAccessDenied)
            {
                EnterAccessDeniedEditorState(false);
            }
            else
            {
                _isAccessDeniedEditorLocked = false;
            }

            ApplyState();
        }

        private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs eventArgs) =>
            DispatchToUi(ApplyErrors);

        private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            DispatchToUi(
                () =>
                {
                    SynchronizeLineSubscriptions();
                    ApplyState();
                });
        }

        private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) =>
            DispatchToUi(ApplyState);

        private void DispatchToUi(Action action)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId == _uiThreadId)
            {
                action();
                return;
            }

            if (!IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (!IsDisposed && !Disposing)
                    {
                        action();
                    }
                }));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void SynchronizeLineSubscriptions()
        {
            UnsubscribeLines();
            foreach (var line in _editor.Lines)
            {
                line.PropertyChanged += OnLinePropertyChanged;
                _subscribedLines.Add(line);
            }
        }

        private void UnsubscribeLines()
        {
            foreach (var line in _subscribedLines)
            {
                line.PropertyChanged -= OnLinePropertyChanged;
            }
            _subscribedLines.Clear();
        }

        private void ApplyState()
        {
            if (_applyingState || IsDisposed || Disposing)
            {
                return;
            }

            _applyingState = true;
            try
            {
                SetReceiptRows();
                SetLineRows();
                WarehouseFilterLookup.DataSource = _editor.WarehouseLookup.Items
                    .Where(item => item.IsAvailableForNewSelection)
                    .ToArray();
                WarehouseLookup.DataSource = _editor.WarehouseLookup.Items.ToArray();
                EmployeeLookup.DataSource = _editor.EmployeeLookup.Items.ToArray();
                ProductLookup.DataSource = _editor.ProductOptions.Items.ToArray();
                WarehouseFilterLookup.SelectValue(_warehouseFilterId);
                StatusFilter.SelectedItem = _statusFilter ?? "All";
                WarehouseLookup.SelectValue(_editor.WarehouseId);
                EmployeeLookup.SelectValue(_editor.EmployeeId);
                ProductLookup.SelectValue(_selectedLine?.ProductId);
                ReceiptDateEditor.Value = _editor.ReceiptDate;
                NoteEditor.Text = _editor.Note ?? string.Empty;
                StatusLabel.Text = _editor.Status;
                VersionLabel.Text = _editor.Version ?? string.Empty;
                TotalLabel.Text = _editor.Total.HasValue
                    ? _editor.Total.Value.ToString("0.00")
                    : "Amount too large";
                QuantityEditor.Value = Clamp(QuantityEditor, _selectedLine?.Quantity ?? 0m);
                UnitCostEditor.Value = Clamp(UnitCostEditor, _selectedLine?.UnitCost ?? 0m);

                var busy = _isRefreshing || _list.IsLoading || _list.IsBusy || _editor.IsBusy;
                BusyIndicator.Visible = busy;
                OperationStatusLabel.Text = _workflowMessage ??
                    _editor.ErrorMessage ??
                    _list.ErrorMessage ??
                    (busy ? "Loading..." : "Ready");
                ReceiptGrid.Enabled = !busy;
                RefreshButton.Enabled = !busy;
                NewButton.Enabled = !busy;
                WarehouseFilterLookup.Enabled = !busy;
                StatusFilter.Enabled = !busy;
                var pageCount = Math.Max(1, (int)Math.Ceiling(
                    _list.TotalCount / (double)_list.PageSize));
                PageLabel.Text = $"Page {_list.PageNumber} of {pageCount}";
                PreviousPageButton.Enabled = !busy && _list.PageNumber > 1;
                NextPageButton.Enabled = !busy && _list.PageNumber < pageCount;
                var canEdit = !busy && !_editor.IsReadOnly && !_isAccessDeniedEditorLocked;
                ReceiptDateEditor.Enabled = canEdit;
                WarehouseLookup.Enabled = canEdit;
                EmployeeLookup.Enabled = canEdit;
                NoteEditor.Enabled = canEdit;
                SaveButton.Enabled = canEdit;
                PostButton.Enabled = canEdit &&
                    _editor.Id.HasValue &&
                    !string.IsNullOrWhiteSpace(_editor.Version) &&
                    !_editor.IsDirty;
                AddLineButton.Enabled = canEdit;
                RemoveLineButton.Enabled = canEdit && _selectedLine != null;
                ProductLookup.Enabled = canEdit && _selectedLine != null;
                QuantityEditor.Enabled = canEdit && _selectedLine != null;
                UnitCostEditor.Enabled = canEdit && _selectedLine != null;
                ApplyErrors();
            }
            finally
            {
                _applyingState = false;
            }
        }

        private void SetReceiptRows()
        {
            _receiptIds = _list.Items.Select(item => item.Id).ToArray();
            ReceiptGrid.Redim(_list.Items.Count + 1, 6);
            ReceiptGrid.FixedRows = 1;
            SetHeaders(ReceiptGrid, "ID", "Number", "Date", "Warehouse", "Employee", "Status");
            for (var index = 0; index < _list.Items.Count; index++)
            {
                var item = _list.Items[index];
                var row = index + 1;
                ReceiptGrid[row, 0] = new SourceGrid.Cells.Cell(item.Id);
                ReceiptGrid[row, 1] = new SourceGrid.Cells.Cell(item.ReceiptNumber);
                ReceiptGrid[row, 2] = new SourceGrid.Cells.Cell(item.ReceiptDate.ToString("yyyy-MM-dd"));
                ReceiptGrid[row, 3] = new SourceGrid.Cells.Cell(item.WarehouseDisplayName ?? item.WarehouseId.ToString());
                ReceiptGrid[row, 4] = new SourceGrid.Cells.Cell(item.EmployeeDisplayName ?? item.EmployeeId.ToString());
                ReceiptGrid[row, 5] = new SourceGrid.Cells.Cell(item.Status);
            }
            ReceiptGrid.AutoSizeCells();
        }

        private void SetLineRows()
        {
            LineGrid.Redim(_editor.Lines.Count + 1, 5);
            LineGrid.FixedRows = 1;
            SetHeaders(LineGrid, "#", "Product", "Quantity", "Unit cost", "Total");
            for (var index = 0; index < _editor.Lines.Count; index++)
            {
                var item = _editor.Lines[index];
                var row = index + 1;
                LineGrid[row, 0] = new SourceGrid.Cells.Cell(index + 1);
                LineGrid[row, 1] = new SourceGrid.Cells.Cell(item.ProductDisplayName ?? string.Empty);
                LineGrid[row, 2] = new SourceGrid.Cells.Cell(item.Quantity);
                LineGrid[row, 3] = new SourceGrid.Cells.Cell(item.UnitCost);
                LineGrid[row, 4] = new SourceGrid.Cells.Cell(
                    item.LineTotal.HasValue
                        ? (object)item.LineTotal.Value
                        : "Amount too large");
            }
            LineGrid.AutoSizeCells();
        }

        private void ApplyErrors()
        {
            foreach (var control in _errorControls.Values.Concat(
                new Control[] { ProductLookup, QuantityEditor, UnitCostEditor }))
            {
                _errors.SetError(control, string.Empty);
            }

            foreach (var field in _errorControls.Keys)
            {
                SetError(field, _errorControls[field]);
            }

            if (_selectedLine != null)
            {
                var index = _editor.Lines.IndexOf(_selectedLine);
                if (index >= 0)
                {
                    SetError($"lines[{index}].productId", ProductLookup);
                    SetError($"lines[{index}].quantity", QuantityEditor);
                    SetError($"lines[{index}].unitCost", UnitCostEditor);
                }
            }
        }

        private void SetError(string field, Control control)
        {
            var messages = ((IEnumerable)_editor.GetErrors(field)).Cast<string>();
            _errors.SetError(control, string.Join(Environment.NewLine, messages));
        }

        private bool TryGetLineErrorControl(string field, out Control control)
        {
            if (field.EndsWith(".productId", StringComparison.Ordinal))
            {
                control = ProductLookup;
                return true;
            }
            if (field.EndsWith(".quantity", StringComparison.Ordinal))
            {
                control = QuantityEditor;
                return true;
            }
            if (field.EndsWith(".unitCost", StringComparison.Ordinal))
            {
                control = UnitCostEditor;
                return true;
            }
            control = null!;
            return false;
        }

        private Control CreateLayout()
        {
            var master = new Panel { Dock = DockStyle.Fill };
            var masterBar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 5, Padding = new Padding(8) };
            masterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            masterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            masterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            masterBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            masterBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            masterBar.Controls.Add(SearchBox, 0, 0);
            masterBar.Controls.Add(WarehouseFilterLookup, 1, 0);
            masterBar.Controls.Add(StatusFilter, 2, 0);
            masterBar.Controls.Add(RefreshButton, 3, 0);
            masterBar.Controls.Add(NewButton, 4, 0);
            var pagingBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8),
            };
            pagingBar.Controls.Add(PreviousPageButton);
            pagingBar.Controls.Add(PageLabel);
            pagingBar.Controls.Add(NextPageButton);
            master.Controls.Add(ReceiptGrid);
            master.Controls.Add(pagingBar);
            master.Controls.Add(masterBar);

            var detail = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
            detail.Panel1MinSize = 300;
            detail.Panel1.Controls.Add(CreateHeaderEditor());
            detail.Panel2.Controls.Add(CreateLineEditor());

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            split.Panel1.Controls.Add(master);
            split.Panel2.Controls.Add(detail);
            return split;
        }

        private Control CreateHeaderEditor()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1, Padding = new Padding(8) };
            AddField(panel, "Receipt date", ReceiptDateEditor);
            AddField(panel, "Warehouse", WarehouseLookup);
            AddField(panel, "Employee", EmployeeLookup);
            AddField(panel, "Note", NoteEditor);
            AddField(panel, "Status", StatusLabel);
            AddField(panel, "Version", VersionLabel);
            var commands = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            commands.Controls.Add(SaveButton);
            commands.Controls.Add(PostButton);
            commands.Controls.Add(new Label { Text = "Total", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
            commands.Controls.Add(TotalLabel);
            commands.Controls.Add(BusyIndicator);
            panel.Controls.Add(commands);
            panel.Controls.Add(OperationStatusLabel);
            return panel;
        }

        private Control CreateLineEditor()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var fields = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 3, Padding = new Padding(8) };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            fields.Controls.Add(ProductLookup, 0, 0);
            fields.Controls.Add(QuantityEditor, 1, 0);
            fields.Controls.Add(UnitCostEditor, 2, 0);
            var commands = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
            commands.Controls.Add(AddLineButton);
            commands.Controls.Add(RemoveLineButton);
            panel.Controls.Add(LineGrid);
            panel.Controls.Add(fields);
            panel.Controls.Add(commands);
            return panel;
        }

        private static void AddField(TableLayoutPanel panel, string label, Control editor)
        {
            panel.Controls.Add(new Label { Text = label, AutoSize = true });
            panel.Controls.Add(editor);
        }

        private static BootstrapLookupBox CreateLookup(string accessibleName)
        {
            var lookup = new BootstrapLookupBox
            {
                Dock = DockStyle.Top,
                AccessibleName = accessibleName,
                DisplayMember = nameof(CatalogLookupOption.DisplayName),
                ValueMember = nameof(CatalogLookupOption.Id),
            };
            lookup.SearchMembers.Add(nameof(CatalogLookupOption.DisplayName));
            return lookup;
        }

        private static BootstrapButton CreateButton(string text, Action action)
        {
            var button = new BootstrapButton { Text = text, AutoSize = true };
            button.Click += (_, __) => action();
            return button;
        }

        private static bool TrySelectedOption(BootstrapLookupBox lookup, out CatalogLookupOption option)
        {
            var selected = lookup.SelectedItem as CatalogLookupOption;
            if (selected == null)
            {
                option = null!;
                return false;
            }

            option = selected;
            return true;
        }

        private static decimal Clamp(BootstrapNumericBox editor, decimal value) =>
            Math.Min(editor.Maximum, Math.Max(editor.Minimum, value));

        private static void SetHeaders(BootstrapGrid grid, params string[] headers)
        {
            for (var index = 0; index < headers.Length; index++)
            {
                grid[0, index] = new SourceGrid.Cells.ColumnHeader(headers[index]);
            }
        }
    }
}
