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
using MyDmsVn.Bootstrap5WinFormUI.Formatting;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using BootstrapGrid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

namespace MyDmsVn.Desktop.WinForms
{
    public interface IGoodsReceiptControlFactory
    {
        Control Create();
    }

    public sealed partial class GoodsReceiptControl : UserControl
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
        private bool _isApplyingWarehouseFilter;
        private bool _isAccessDeniedEditorLocked;
        private bool _applyingState;
        private int _lookupBoundaryGeneration;

        public GoodsReceiptControl(
            GoodsReceiptListViewModel list,
            GoodsReceiptEditorViewModel editor)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
            Dock = DockStyle.Fill;
            AutoScaleMode = AutoScaleMode.Dpi;

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
            ReceiptGrid = new ReceiptResultsGrid { Dock = DockStyle.Fill, TabStop = true, AccessibleName = "Goods receipt results", CausesValidation = false };
            ((AccessibleActionGrid)ReceiptGrid).ConfigureRowActions(
                row => row > 0 && row <= _receiptIds.Count ? (object)_receiptIds[row - 1] : null,
                row => row > 0 && row < ReceiptGrid.RowsCount ? Convert.ToString(ReceiptGrid[row, 1]?.Value) ?? string.Empty : string.Empty,
                row =>
                {
                    if (IsOperationBusy || _editor.RequiresPostOutcomeReload ||
                        !CanAbandonEditor(ReceiptEditIntent.SelectReceipt)) return;
                    ReceiptGrid.Selection.Focus(new SourceGrid.Position(row, 1), true);
                    SelectActiveReceipt();
                });
            ((ReceiptResultsGrid)ReceiptGrid).AdmitPointerNavigation = () =>
                !IsOperationBusy && !_editor.RequiresPostOutcomeReload && CanAbandonEditor(ReceiptEditIntent.SelectReceipt);
            ReceiptGrid.DoubleClick += (_, __) => SelectActiveReceipt();
            ReceiptGrid.KeyDown += OnReceiptGridKeyDown;

            ReceiptDateEditor = new BootstrapDateInput
            {
                Dock = DockStyle.Top, AccessibleName = "Receipt date",
                AccessibleDescription = "Type day, day/month or day/month/four-digit-year.",
                Profile = new DateInputProfile(false, DateTimePicker.MinimumDateTime, DateTimePicker.MaximumDateTime)
            };
            WarehouseLookup = CreateLookup("Warehouse");
            EmployeeLookup = CreateLookup("Employee");
            NoteEditor = new BootstrapTextBox { Dock = DockStyle.Top, AccessibleName = "Note" };
            StatusLabel = new Label { AutoSize = true };
            VersionLabel = new Label { AutoSize = true };
            TotalLabel = new Label { AutoSize = true, TextAlign = ContentAlignment.MiddleRight };
            OperationStatusLabel = new Label { AutoSize = true, Text = "Ready", AccessibleName = "Goods receipt operation status" };
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

            LineGrid = new GoodsReceiptLinesGrid { Dock = DockStyle.Fill, TabStop = true, CausesValidation = false };
            _inline = new GoodsReceiptLinesGridAdapter((GoodsReceiptLinesGrid)LineGrid, _editor, CanEditLines);
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
            Controls.Add(_workspaceCommands);
            Controls.Add(_workspaceFooter);
            BusinessCompactAppearance.Apply(this);
            _inline.EditStateChanged += OnInlineEditStateChanged;
            foreach (var command in new[] { NewButton, RefreshButton, SaveButton, PostButton, AddLineButton, RemoveLineButton, BackToReceiptsButton })
                ((ReceiptCommandButton)command).RetainEditorFocus = () =>
                    _inline.HasPendingEdit || WarehouseLookup.HasPendingText || EmployeeLookup.HasPendingText;
            SearchBox.TextChanged += OnSearchChanged;
            WarehouseFilterLookup.TextChanged += OnWarehouseLookupSearchChanged;
            WarehouseFilterLookup.SelectedValueChanged += OnWarehouseFilterChanged;
            StatusFilter.SelectedValueChanged += OnStatusFilterChanged;
            ReceiptDateEditor.ValueChanged += OnReceiptDateChanged;
            ReceiptDateEditor.PendingTextChanged += OnPendingReceiptDateChanged;
            WarehouseLookup.SelectedValueChanged += OnWarehouseChanged;
            WarehouseLookup.TextChanged += OnWarehouseLookupSearchChanged;
            EmployeeLookup.SelectedValueChanged += OnEmployeeChanged;
            EmployeeLookup.TextChanged += OnEmployeeLookupSearchChanged;
            NoteEditor.TextChanged += OnNoteChanged;
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
        public BootstrapDateInput ReceiptDateEditor { get; }
        public BootstrapLookupBox WarehouseLookup { get; }
        public BootstrapLookupBox EmployeeLookup { get; }
        public BootstrapLookupBox ProductLookup => _inline.ProductControl;
        public BootstrapTextBox NoteEditor { get; }
        public BootstrapDecimalInput QuantityEditor => _inline.QuantityControl;
        public BootstrapDecimalInput UnitCostEditor => _inline.CostControl;
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
                return string.Join(Environment.NewLine, ((IEnumerable)_editor.GetErrors(field)).Cast<string>());
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
                ReceiptDateEditor.PendingTextChanged -= OnPendingReceiptDateChanged;
                WarehouseLookup.SelectedValueChanged -= OnWarehouseChanged;
                WarehouseLookup.TextChanged -= OnWarehouseLookupSearchChanged;
                EmployeeLookup.SelectedValueChanged -= OnEmployeeChanged;
                EmployeeLookup.TextChanged -= OnEmployeeLookupSearchChanged;
                NoteEditor.TextChanged -= OnNoteChanged;
                _list.PropertyChanged -= OnListViewModelPropertyChanged;
                _editor.PropertyChanged -= OnViewModelPropertyChanged;
                _editor.WarehouseLookup.PropertyChanged -= OnViewModelPropertyChanged;
                _editor.EmployeeLookup.PropertyChanged -= OnViewModelPropertyChanged;
                _editor.ProductOptions.PropertyChanged -= OnViewModelPropertyChanged;
                _editor.ErrorsChanged -= OnErrorsChanged;
                _editor.Lines.CollectionChanged -= OnLinesChanged;
                UnsubscribeLines();
                _inline.EditStateChanged -= OnInlineEditStateChanged;
                _inline.Dispose();
                VersionLabel.Dispose(); // Retained compatibility control is deliberately outside the visible tree.
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
            if (IsOperationBusy || !CanAbandonEditor(ReceiptEditIntent.Refresh))
            {
                return;
            }

            CancelEditorLookupEdits();

            var selectedLineId = _selectedLine?.Id;
            var selectedLineIndex = _selectedLine == null
                ? -1
                : _editor.Lines.IndexOf(_selectedLine);
            _isRefreshing = true;
            ApplyState();
            try
            {
                await _list.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
                if (IsDisposed || Disposing) return;
                if (_list.IsAccessDenied)
                {
                    EnterAccessDeniedEditorState(false);
                }
                else if (_editor.Id.HasValue || _editor.RequiresPostOutcomeReload)
                {
                    if (await _editor.RefreshAsync(CancellationToken.None).ConfigureAwait(true))
                    {
                        BeginReceiptDateSession();
                        RebindSelectedLine(selectedLineId, selectedLineIndex);
                        _inline.ResetDocument(_editor.Lines);
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
            if (_editor.RequiresPostOutcomeReload || IsOperationBusy)
            {
                return;
            }
            if (!CanAbandonEditor(ReceiptEditIntent.SelectReceipt))
            {
                return;
            }

            CancelEditorLookupEdits();
            if (_editor.Id == id && !_list.IsAccessDenied && !_isAccessDeniedEditorLocked && _hasDetail)
            {
                ShowReceiptDetail();
                return;
            }

            var selected = await _list.SelectAsync(id, cancellationToken).ConfigureAwait(true);
            if (IsDisposed || Disposing) return;
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
            CancelEditorLookupEdits();
            _editor.Load(selected);
            BeginReceiptDateSession();
            _selectedLine = _editor.Lines.FirstOrDefault();
            _inline.ResetDocument(_editor.Lines);
            ShowReceiptDetail();
            ApplyState();
        }

        private async Task SaveAsync()
        {
            if (!CanEditLines() || !FinishInline(ReceiptEditIntent.Save) ||
                !await FinishHeaderLookupsAsync(SaveButton).ConfigureAwait(true)) return;
            if (!TryCommitReceiptDate()) return;
            if (ApplyEditorsToViewModel())
            {
                var selectedLineId = _selectedLine?.Id;
                var selectedLineIndex = _selectedLine == null
                    ? -1
                    : _editor.Lines.IndexOf(_selectedLine);
                var saved = await _editor.SaveAsync(CancellationToken.None).ConfigureAwait(true);
                if (IsDisposed || Disposing) return;
                if (saved)
                {
                    RebindSelectedLine(selectedLineId, selectedLineIndex);
                    _inline.ResetDocument(_editor.Lines);
                    _workflowMessage = null;
                    await _list.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
                    if (_list.IsAccessDenied)
                    {
                        EnterAccessDeniedEditorState(true);
                    }
                }
            }
            ApplyState();
            if (_editor.HasErrors) FocusFirstLineError();
        }

        private async Task PostAsync()
        {
            if (!CanEditLines() || !FinishInline(ReceiptEditIntent.Post) ||
                !await FinishHeaderLookupsAsync(PostButton).ConfigureAwait(true)) return;
            if (!TryCommitReceiptDate()) return;
            if (_editor.IsDirty)
            {
                _workflowMessage = "Save the current draft before posting it.";
                ApplyState();
                return;
            }
            var posted = await _editor.PostAsync(CancellationToken.None).ConfigureAwait(true);
            if (IsDisposed || Disposing) return;
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
            if (_editor.RequiresPostOutcomeReload || IsOperationBusy)
            {
                return;
            }
            if (!CanAbandonEditor(ReceiptEditIntent.NewReceipt))
            {
                return;
            }

            _workflowMessage = null;
            _isAccessDeniedEditorLocked = false;
            CancelEditorLookupEdits();
            _list.NewDraft();
            _editor.Load(null);
            BeginReceiptDateSession();
            _selectedLine = null;
            _inline.ResetDocument(_editor.Lines);
            ShowReceiptDetail();
            ApplyState();
        }

        private bool CanAbandonEditor(ReceiptEditIntent intent)
        {
            if (!_editor.IsDirty && !ReceiptDateEditor.HasPendingEdit)
            {
                return FinishInline(intent);
            }

            _workflowMessage = "Save the current draft before leaving it.";
            ApplyState();
            if (ReceiptDateEditor.HasPendingEdit) ReceiptDateEditor.Focus();
            else _inline.FocusActiveEdit();
            return false;
        }

        private void ClearPersistedEditor()
        {
            if (_editor.Id.HasValue)
            {
                CancelEditorLookupEdits();
                _editor.Load(null);
                BeginReceiptDateSession();
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
                BeginReceiptDateSession();
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
            if (_isApplyingWarehouseFilter) return;
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
            if (!CanEditLines() || !FinishInline(ReceiptEditIntent.AddLine)) return;
            _selectedLine = _editor.AddLine();
            LineGrid.Selection.Focus(new SourceGrid.Position(_editor.Lines.IndexOf(_selectedLine) + 1, 1), true);
            ApplyState();
            LineGrid.Focus();
        }

        private void RemoveSelectedLine()
        {
            var target = _inline.SelectedLine;
            if (target == null || !CanEditLines() || !FinishInline(ReceiptEditIntent.RemoveLine)) return;
            _editor.RemoveLine(target);
            _selectedLine = _inline.SelectedLine;
            ApplyState();
        }

        private bool ApplyEditorsToViewModel()
        {
            if (_editor.IsReadOnly || _editor.RequiresPostOutcomeReload)
            {
                return false;
            }
            if (!ReceiptDateEditor.Value.HasValue) return false;
            _editor.ReceiptDate = ReceiptDateEditor.Value.Value;
            _editor.Note = NoteEditor.Text;
            return true;
        }

        private bool TryCommitReceiptDate()
        {
            if (!ReceiptDateEditor.HasPendingEdit) return true;
            if (ReceiptDateEditor.TryCommitPending())
            {
                ReceiptDateEditor.ValidationState = BootstrapValidationState.None;
                if (_workflowMessage == "Enter a valid receipt date (day/month/year).") _workflowMessage = null;
                return true;
            }
            _workflowMessage = "Enter a valid receipt date (day/month/year).";
            ReceiptDateEditor.ValidationState = BootstrapValidationState.Invalid;
            ApplyState();
            ReceiptDateEditor.Focus();
            return false;
        }

        private void BeginReceiptDateSession()
        {
            ReceiptDateEditor.BeginEditSession(_editor.ReceiptDate);
            ReceiptDateEditor.ValidationState = BootstrapValidationState.None;
        }

        private void SelectActiveReceipt()
        {
            var row = ReceiptGrid.Selection.ActivePosition.Row;
            if (row > 0 && row - 1 < _receiptIds.Count)
            {
                SetOperation(SelectReceiptCoreAsync(_receiptIds[row - 1], CancellationToken.None));
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

        private void OnSearchChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && !_isApplyingWarehouseFilter)
            {
                SetOperation(_list.SetSearch(SearchBox.Text));
            }
        }

        private void OnWarehouseLookupSearchChanged(object? sender, EventArgs eventArgs)
        {
            var lookup = (BootstrapLookupBox)sender!;
            var canSearch = ReferenceEquals(lookup, WarehouseFilterLookup) ? !IsOperationBusy : CanEditLines();
            if (!_applyingState && canSearch && lookup.HasPendingText)
            {
                SetOperation(_editor.WarehouseLookup.SetSearch(lookup.Text));
            }
        }

        private void OnEmployeeLookupSearchChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && CanEditLines() && EmployeeLookup.HasPendingText)
            {
                SetOperation(_editor.EmployeeLookup.SetSearch(EmployeeLookup.Text));
            }
        }

        private void OnWarehouseFilterChanged(object? sender, EventArgs eventArgs)
        {
            if (_applyingState || IsOperationBusy)
            {
                return;
            }

            _warehouseFilterId = WarehouseFilterLookup.SelectedValue as int?;
            SetOperation(ApplyWarehouseFilterAsync(_warehouseFilterId));
        }

        private async Task ApplyWarehouseFilterAsync(int? warehouseId)
        {
            // Reserve command admission immediately, but defer disabling the native EDIT
            // until commit/Tab traversal finishes. A later command must not start a load
            // that this already-admitted filter continuation would cancel.
            _isApplyingWarehouseFilter = true;
            try
            {
                if (WarehouseFilterLookup.ContainsFocus) await Task.Yield();
                if (IsDisposed || Disposing) return;
                _isApplyingWarehouseFilter = false; // The list now takes admission synchronously.
                await _list.SetWarehouseFilterAsync(warehouseId, CancellationToken.None).ConfigureAwait(true);
            }
            finally { _isApplyingWarehouseFilter = false; }
        }

        private void OnStatusFilterChanged(object? sender, EventArgs eventArgs)
        {
            if (_applyingState || _isApplyingWarehouseFilter)
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
            if (!_applyingState && !_editor.IsReadOnly && !_editor.RequiresPostOutcomeReload)
            {
                if (ReceiptDateEditor.Value.HasValue) _editor.ReceiptDate = ReceiptDateEditor.Value.Value;
            }
        }

        private void OnPendingReceiptDateChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState) ApplyState();
        }

        private void OnWarehouseChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && CanEditLines() && TrySelectedOption(WarehouseLookup, out var option))
            {
                _editor.SetWarehouse(option.Id, option.DisplayName, option.IsAvailableForNewSelection);
            }
        }

        private void OnEmployeeChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && CanEditLines() && TrySelectedOption(EmployeeLookup, out var option))
            {
                _editor.SetEmployee(option.Id, option.DisplayName, option.IsAvailableForNewSelection);
            }
        }

        private void OnNoteChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState && !_editor.IsReadOnly && !_editor.RequiresPostOutcomeReload)
            {
                _editor.Note = NoteEditor.Text;
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
                _selectedLine = _inline.SelectedLine;
                _inline.RefreshEditability();
                BindEditorLookup(WarehouseFilterLookup, _editor.WarehouseLookup.Items
                    .Where(item => item.IsAvailableForNewSelection), _warehouseFilterId);
                BindEditorLookup(WarehouseLookup, _editor.WarehouseLookup.Items, _editor.WarehouseId);
                BindEditorLookup(EmployeeLookup, _editor.EmployeeLookup.Items, _editor.EmployeeId);
                StatusFilter.SelectedItem = _statusFilter ?? "All";
                if (ReceiptDateEditor.Value != _editor.ReceiptDate)
                    ReceiptDateEditor.ProjectAcceptedValue(_editor.ReceiptDate);
                NoteEditor.Text = _editor.Note ?? string.Empty;
                StatusLabel.Text = _editor.Status;
                if (_compactTitle != null) _compactTitle.Text = _editor.ReceiptNumber ?? "Goods receipt — New";
                VersionLabel.Text = _editor.Version ?? string.Empty;
                TotalLabel.Text = _editor.Total.HasValue
                    ? _editor.Total.Value.ToString("0.00")
                    : "Amount too large";

                var busy = _isRefreshing || _list.IsLoading || _list.IsBusy || _editor.IsBusy;
                var requiresPostReload = _editor.RequiresPostOutcomeReload;
                BusyIndicator.Visible = busy;
                OperationStatusLabel.Text = requiresPostReload
                    ? "Posting outcome unknown. Refresh to reload the receipt before making further changes. " + _editor.ErrorMessage
                    : _inline.EditError ?? _workflowMessage ??
                    _editor.ErrorMessage ??
                    _list.ErrorMessage ??
                    (busy ? "Loading..." : "Ready");
                OperationStatusLabel.AccessibleRole = requiresPostReload || _inline.EditError != null || _workflowMessage != null || _editor.ErrorMessage != null || _list.ErrorMessage != null
                    ? AccessibleRole.Alert : AccessibleRole.StaticText;
                OperationStatusLabel.ForeColor = OperationStatusLabel.AccessibleRole == AccessibleRole.Alert
                    ? MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme.Mode == MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeMode.Dark
                        ? Color.FromArgb(255, 180, 171) : Color.FromArgb(180, 35, 24)
                    : ForeColor;
                ReceiptGrid.Enabled = !busy && !requiresPostReload;
                LineGrid.Enabled = !busy && !requiresPostReload;
                RefreshButton.Enabled = !busy;
                NewButton.Enabled = !busy && !requiresPostReload;
                WarehouseFilterLookup.Enabled = !busy;
                StatusFilter.Enabled = !busy;
                var pageCount = Math.Max(1, (int)Math.Ceiling(
                    _list.TotalCount / (double)_list.PageSize));
                PageLabel.Text = $"Page {_list.PageNumber} of {pageCount}";
                PreviousPageButton.Enabled = !busy && _list.PageNumber > 1;
                NextPageButton.Enabled = !busy && _list.PageNumber < pageCount;
                var canEdit = !busy && !_editor.IsReadOnly && !_isAccessDeniedEditorLocked && !requiresPostReload;
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
                _inline.RefreshEditability();
                ApplyPresentationState();
                ApplyErrors();
            }
            finally
            {
                _applyingState = false;
            }
        }

        private GoodsReceiptDto[] _receiptRows = Array.Empty<GoodsReceiptDto>();
        private void SetReceiptRows()
        {
            var rows = _list.Items;
            if (ReceiptGrid.RowsCount != 0 && _receiptRows.Length == rows.Count &&
                _receiptRows.Zip(rows, (a, b) => a.Id == b.Id && a.ReceiptNumber == b.ReceiptNumber &&
                    a.ReceiptDate == b.ReceiptDate && a.WarehouseId == b.WarehouseId && a.EmployeeId == b.EmployeeId &&
                    a.WarehouseDisplayName == b.WarehouseDisplayName && a.EmployeeDisplayName == b.EmployeeDisplayName && a.Status == b.Status).All(equal => equal)) return;
            var active = ReceiptGrid.Selection.ActivePosition;
            var selectedId = active.Row > 0 && active.Row <= _receiptIds.Count ? (long?)_receiptIds[active.Row - 1] : _list.SelectedReceipt?.Id;
            var scroll = ReceiptGrid.CustomScrollPosition;
            ReceiptGrid.Selection.ResetSelection(false);
            _receiptRows = rows.ToArray();
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
            BusinessCompactAppearance.StyleReceiptResults(ReceiptGrid);
            ReceiptGrid.AutoSizeCells();
            BusinessCompactAppearance.StyleReceiptResults(ReceiptGrid);
            ((ReceiptResultsGrid)ReceiptGrid).SynchronizeRow(Array.FindIndex(_receiptRows, row => row.Id == selectedId) + 1, active.Column);
            ReceiptGrid.CustomScrollPosition = scroll;
        }

        private void ApplyErrors()
        {
            foreach (var field in _errorControls.Keys) SetError(field, _errorControls[field]);
            var messages = Enumerable.Range(0, _editor.Lines.Count)
                .SelectMany(index => new[] { "productId", "quantity", "unitCost" }
                    .SelectMany(field => ((IEnumerable)_editor.GetErrors($"lines[{index}].{field}")).Cast<string>()));
            var error = string.Join(Environment.NewLine, messages);
            if (!string.IsNullOrEmpty(error)) _errors.SetError(LineGrid, error);
        }

        private void SetError(string field, Control control)
        {
            var messages = ((IEnumerable)_editor.GetErrors(field)).Cast<string>();
            var error = string.Join(Environment.NewLine, messages);
            if (field == "receiptDate")
            {
                if (error.Length == 0 && ReceiptDateEditor.ValidationState == BootstrapValidationState.Invalid)
                    error = "Enter a valid receipt date (day/month/year).";
                ReceiptDateEditor.AccessibleDescription = error.Length == 0
                    ? "Type day, day/month or day/month/four-digit-year." : error;
            }
            _errors.SetError(control, error);
        }

        private bool TryGetLineErrorControl(string field, out Control control)
        {
            control = LineGrid;
            return field.StartsWith("lines[", StringComparison.Ordinal);
        }

        private static Control CompactField(string caption, Control editor, int order)
        {
            var group = new Panel { Size = new Size(200, 60), Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 8), TabIndex = order };
            editor.Dock = DockStyle.Top;
            editor.TabIndex = 0;
            group.Controls.Add(editor);
            group.Controls.Add(new Label { Text = caption, Name = "CompactSecondary", Dock = DockStyle.Top, AutoSize = true });
            return group;
        }

        private void CancelEditorLookupEdits()
        {
            _lookupBoundaryGeneration++;
            // Only admitted document boundaries reach this method. Invalidate the shared
            // request generations before clearing presentation so late responses cannot
            // republish a preceding document's query (including the shared filter host).
            _editor.WarehouseLookup.Cancel();
            _editor.EmployeeLookup.Cancel();
            WarehouseLookup.CancelPendingEdit();
            EmployeeLookup.CancelPendingEdit();
            _inline.CancelActiveEdit();
        }

        private async Task<bool> FinishHeaderLookupsAsync(Control command)
        {
            if (!WarehouseLookup.HasPendingText && !EmployeeLookup.HasPendingText) return true;
            var generation = _lookupBoundaryGeneration;
            // Use the pinned lookup's normal exact-match/cancel policy on native leave,
            // including accessible/default-action commands that did not move focus.
            // Do not synthesize Enter: that would commit a merely highlighted result.
            command.Focus();
            await Task.Yield();
            if (IsDisposed || Disposing || generation != _lookupBoundaryGeneration || !CanEditLines()) return false;
            var pending = WarehouseLookup.HasPendingText ? WarehouseLookup :
                EmployeeLookup.HasPendingText ? EmployeeLookup : null;
            if (pending == null) return true;
            _workflowMessage = "Resolve the pending lookup before saving or posting.";
            ApplyState();
            pending.Focus();
            return false;
        }

        private static void BindEditorLookup(BootstrapLookupBox lookup, IEnumerable<CatalogLookupOption> options, int? selectedId)
        {
            var selection = selectedId > 0 ? selectedId : null;
            lookup.DataSource = options.ToArray();
            // The pinned DataSource reconciliation preserves pending native text/caret.
            // Reselect only an actual identity transition; selecting the same key resets it.
            if (!Equals(lookup.SelectedValue, selection)) lookup.SelectValue(selection);
        }

        private static BootstrapLookupBox CreateLookup(string accessibleName)
        {
            var lookup = new AccessibleLookupBox
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
            var button = new ReceiptCommandButton { Text = text, AutoSize = true, CausesValidation = false };
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

        private static void SetHeaders(BootstrapGrid grid, params string[] headers)
        {
            for (var index = 0; index < headers.Length; index++)
            {
                grid[0, index] = new SourceGrid.Cells.ColumnHeader(headers[index]);
            }
        }
    }
}
