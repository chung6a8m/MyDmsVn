using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Contracts;
using BootstrapGrid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

namespace MyDmsVn.Desktop.WinForms
{
    public interface ICatalogControlFactory
    {
        Control Create(CatalogKind kind);
    }

    public sealed class CatalogControl : UserControl, IWorkspaceCloseParticipant
    {
        private readonly CatalogBinding _binding;
        private WorkspaceFeedbackNotificationService? _feedbackNotifications;
        public WorkspaceFeedbackScope FeedbackScope { get; private set; } = new WorkspaceFeedbackScope();
        internal void AttachFeedback(WorkspaceFeedbackScope scope, WorkspaceFeedbackNotificationService notifications)
        {
            _busyBinder.ReplaceScope(scope);
            FeedbackScope.Dispose();
            FeedbackScope = scope;
            _feedbackNotifications = notifications;
        }
        private readonly ErrorProvider _errors;
        private readonly FieldValidationBinder _validationBinder;
        private readonly List<IDisposable> _validationBindings = new List<IDisposable>();
        private readonly Dictionary<string, BootstrapTextBox> _editorControls =
            new Dictionary<string, BootstrapTextBox>(StringComparer.Ordinal);
        private readonly Dictionary<string, Label> _fieldHelp =
            new Dictionary<string, Label>(StringComparer.Ordinal);
        private IReadOnlyList<int> _rowIds = Array.Empty<int>();
        private readonly SplitContainer _split;
        private readonly BootstrapBusyHost _busyHost;
        private readonly WorkspaceBusyBinder _busyBinder;
        private readonly Action _cancelBusyQuery;
        private readonly int _uiThreadId;
        private bool _applyingState;
        private bool _arranging;
        private bool _creating;
        private int? _displayedSelectedId;
        private IReadOnlyList<CatalogDisplayRow> _rows = Array.Empty<CatalogDisplayRow>();
        private readonly TableLayoutPanel _toolbar;
        private readonly Label _title;
        private readonly Label _count;
        private readonly Label _heading;
        private readonly Label _selectedCode;
        private readonly Label _status;
        private readonly Label _resultRange;
        private readonly Label _resultsState;
        private readonly Label _error;
        private readonly Label _validationSummary;
        private readonly BootstrapButton _firstErrorButton;

        WorkspaceCloseState IWorkspaceCloseParticipant.CaptureCloseState()
        {
            // Native text is the submitted buffer. TextChanged normally synchronizes it
            // immediately; this also covers a pending native edit at close admission.
            foreach (var field in _binding.Fields)
            {
                var raw = _editorControls[field.Key].Text;
                if (!string.Equals(raw, field.GetValue(), StringComparison.Ordinal))
                    field.SetValue(raw);
            }
            var snapshot = _binding.CaptureEditSnapshot();
            return new WorkspaceCloseState(snapshot.EditorIdentity, snapshot.Revision,
                snapshot.IsDirty, _binding.IsBusy, false,
                !IsDisposed && !Disposing && !_binding.IsBusy && !_binding.IsEditorLoading);
        }

        Task<WorkspaceSaveOutcome> IWorkspaceCloseParticipant.SaveForCloseAsync(
            CancellationToken cancellationToken) => _binding.SaveForCloseAsync(cancellationToken);

        public CatalogControl(ProductCatalogViewModel viewModel)
            : this(CreateBinding(viewModel))
        {
        }

        public CatalogControl(WarehouseCatalogViewModel viewModel)
            : this(CreateBinding(viewModel))
        {
        }

        public CatalogControl(EmployeeCatalogViewModel viewModel)
            : this(CreateBinding(viewModel))
        {
        }

        public CatalogControl(CustomerCatalogViewModel viewModel)
            : this(CreateBinding(viewModel))
        {
        }

        private CatalogControl(CatalogBinding binding)
        {
            _binding = binding ?? throw new ArgumentNullException(nameof(binding));
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
            Dock = DockStyle.Fill;

            SearchBox = new BootstrapTextBox
            {
                AccessibleName = "Search catalog",
                PlaceholderText = "Search code or name…",
                Dock = DockStyle.Fill,
                TabIndex = 0,
            };
            Grid = new CatalogResultsGrid
            {
                Dock = DockStyle.Fill,
                TabIndex = 1,
                TabStop = true,
                AccessibleName = "Catalog results",
                SelectionMode = SourceGrid.GridSelectionMode.Row,
            };
            ((AccessibleActionGrid)Grid).ConfigureRowActions(
                row => row > 0 && row <= _rowIds.Count ? (object)_rowIds[row - 1] : null,
                row => row > 0 && row < Grid.RowsCount ? Convert.ToString(Grid[row, _binding.Kind == CatalogKind.Product ? 0 : 1]?.Value) ?? string.Empty : string.Empty,
                row =>
                {
                    Grid.Selection.Focus(new SourceGrid.Position(row, 1), true);
                    SelectActiveRow();
                });
            Grid.DoubleClick += (_, __) => SelectActiveRow();
            Grid.KeyDown += OnGridKeyDown;

            RefreshButton = CreateButton("Refresh", 20, () => SetOperation(_binding.RefreshAsync(CancellationToken.None)));
            PreviousPageButton = CreateButton("Previous", 18, () => MovePage(-1));
            NextPageButton = CreateButton("Next", 19, () => MovePage(1));
            PageLabel = new Label { AutoSize = true, TextAlign = ContentAlignment.MiddleCenter };
            NewButton = CreateButton(binding.Kind == CatalogKind.Product ? "Add product" : "New", 21, BeginCreate);
            SaveButton = CreateButton("Save", 22, () => SetOperation(_binding.SaveAsync(CancellationToken.None)));
            ActivateButton = CreateButton("Activate", 23, () => SetActive(true));
            DeactivateButton = CreateButton("Deactivate", 24, () => SetActive(false));
            CancelButton = CreateButton("Cancel", 25, _binding.Cancel);

            _errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            _title = TextLabel("CatalogTitle", PluralName, "CompactHeading");
            _count = TextLabel("CatalogResultCount", string.Empty, "CompactSecondary");
            _heading = TextLabel("CatalogInspectorHeading", SingularName + " details", "CompactHeading");
            _selectedCode = TextLabel("CatalogSelectedCode", string.Empty, "CompactSecondary");
            _status = TextLabel("CatalogStatus", string.Empty);
            _status.BorderStyle = BorderStyle.FixedSingle;
            _status.Padding = new Padding(6, 3, 6, 3);
            _resultRange = TextLabel("CatalogResultRange", string.Empty, "CompactSecondary");
            _resultsState = TextLabel("CatalogResultsState", string.Empty);
            _resultsState.Dock = DockStyle.Top;
            _error = TextLabel("CatalogError", string.Empty);
            _error.AccessibleRole = AccessibleRole.Alert;
            _error.Dock = DockStyle.Bottom;
            _error.Padding = new Padding(8);
            _validationSummary = TextLabel("CatalogValidationSummary", string.Empty);
            _validationSummary.AccessibleRole = AccessibleRole.Alert;
            _validationSummary.AutoSize = true;
            _firstErrorButton = new BootstrapButton { Name = "CatalogValidationNavigation", Text = "Go to first error", AccessibleName = "Go to first validation error", AutoSize = true, Outline = true };
            _firstErrorButton.Click += (_, __) => FocusFirstValidationError();
            var editor = CreateEditorPanel();
            _validationBinder = new FieldValidationBinder(_binding.ErrorSource, DispatchToUi);
            foreach (var field in _binding.Fields)
            {
                var key = field.Key;
                var control = _editorControls[key];
                _validationBindings.Add(_validationBinder.Bind(key, control,
                    state => control.ValidationState = state,
                    message =>
                    {
                        _fieldHelp[key].Text = message;
                        if (IsHandleCreated) _fieldHelp[key].Visible = message.Length != 0;
                        _errors.SetError(control, message);
                    }));
            }
            _validationBinder.Changed += OnValidationChanged;
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2,
            };
            _split.Panel1.Controls.Add(Grid);
            _split.Panel1.Controls.Add(_resultsState);
            _split.Panel2.Controls.Add(editor);
            _split.SizeChanged += (_, __) => UpdateEditorPanelWidth();
            _busyHost = new BootstrapBusyHost(_split) { Dock = DockStyle.Fill };
            _busyBinder = new WorkspaceBusyBinder(FeedbackScope, _busyHost.Begin, DispatchToUi);
            _cancelBusyQuery = () => _binding.Cancel();

            _toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                Padding = new Padding(16, 12, 16, 12),
                Name = "CatalogToolbar",
            };
            _toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var titleGroup = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 16, 0) };
            titleGroup.Controls.Add(_title);
            titleGroup.Controls.Add(_count);
            _toolbar.Controls.Add(titleGroup, 0, 0);
            _toolbar.Controls.Add(SearchBox, 1, 0);
            _toolbar.Controls.Add(RefreshButton, 2, 0);
            _toolbar.Controls.Add(NewButton, 3, 0);
            NewButton.Variant = BootstrapVariant.Primary;
            SaveButton.Variant = BootstrapVariant.Primary;
            DeactivateButton.Variant = BootstrapVariant.Danger;
            RefreshButton.Outline = true;
            CancelButton.Outline = true;
            ActivateButton.Outline = true;
            DeactivateButton.Outline = true;
            PreviousPageButton.Outline = true;
            NextPageButton.Outline = true;

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
            pagingBar.Controls.Add(_resultRange);

            Controls.Add(_busyHost);
            _split.Panel1.Controls.Add(pagingBar);
            Controls.Add(_error);
            Controls.Add(_toolbar);
            SearchBox.TextChanged += OnSearchTextChanged;
            _binding.PropertyChanged += OnViewModelPropertyChanged;
            BootstrapThemeManager.ThemeChanged += OnThemeChanged;
            BusinessCompactAppearance.Apply(this);
            SizeChanged += (_, __) => UpdateEditorPanelWidth();
            UpdateEditorPanelWidth();
            ApplyState();
        }

        public BootstrapTextBox SearchBox { get; }

        public BootstrapGrid Grid { get; }

        public IReadOnlyDictionary<string, BootstrapTextBox> EditorControls => _editorControls;

        public BootstrapButton RefreshButton { get; }

        public BootstrapButton PreviousPageButton { get; }

        public BootstrapButton NextPageButton { get; }

        public Label PageLabel { get; }

        public BootstrapButton NewButton { get; }

        public BootstrapButton SaveButton { get; }

        public BootstrapButton ActivateButton { get; }

        public BootstrapButton DeactivateButton { get; }

        public BootstrapButton CancelButton { get; }

        public Task LastOperation { get; private set; } = Task.CompletedTask;

        public int EditorPanelWidth => _split.Panel2.Width;

        public Task ActivateAsync(CancellationToken cancellationToken)
        {
            LastOperation = _binding.ActivateAsync(cancellationToken);
            return LastOperation;
        }

        public string GetFieldError(string field)
        {
            // A control used before handle creation cannot BeginInvoke an async error event.
            // The first UI-thread read projects the authoritative source; HandleCreated does likewise.
            if (Thread.CurrentThread.ManagedThreadId == _uiThreadId) _validationBinder.Refresh();
            return _editorControls.TryGetValue(field, out var control)
                ? _errors.GetError(control)
                : string.Empty;
        }

        public string ValidationSummaryText => _validationSummary.Text;

        public void FocusFirstValidationError()
        {
            var target = _validationBinder.Entries.FirstOrDefault(entry => entry.FocusTarget != null)?.FocusTarget;
            if (target != null && target.CanFocus) target.Focus();
            else if (_firstErrorButton.CanFocus) _firstErrorButton.Focus();
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
                SearchBox.TextChanged -= OnSearchTextChanged;
                _binding.PropertyChanged -= OnViewModelPropertyChanged;
                _validationBinder.Changed -= OnValidationChanged;
                foreach (var binding in _validationBindings) binding.Dispose();
                _validationBinder.Dispose();
                BootstrapThemeManager.ThemeChanged -= OnThemeChanged;
                _busyBinder.Dispose();
                FeedbackScope.Dispose();
                _binding.Dispose();
                _errors.Dispose();
            }

            base.Dispose(disposing);
        }

        private Control CreateEditorPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Padding = new Padding(12),
                Name = "CatalogInspectorContent",
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.Controls.Add(_heading);
            panel.Controls.Add(_selectedCode);
            panel.Controls.Add(_status);
            panel.Controls.Add(_validationSummary);
            panel.Controls.Add(_firstErrorButton);
            panel.Controls.Add(TextLabel("CatalogBasicInformation", "BASIC INFORMATION", "CompactSection"));
            var tabIndex = 2;
            foreach (var field in _binding.Fields)
            {
                var editor = new BootstrapTextBox
                {
                    AccessibleName = field.Label,
                    Dock = DockStyle.Top,
                    TabIndex = tabIndex++,
                    Margin = new Padding(3, 3, 24, 12),
                };
                editor.TextChanged += (_, __) =>
                {
                    if (!_applyingState)
                    {
                        field.SetValue(editor.Text);
                    }
                };
                _editorControls.Add(field.Key, editor);
                panel.Controls.Add(new Label { Text = field.Label, AutoSize = true });
                panel.Controls.Add(editor);
                var help = TextLabel("CatalogFieldHelp_" + field.Key, string.Empty);
                help.AccessibleName = field.Label + " validation message";
                help.Visible = false;
                help.AutoSize = true;
                _fieldHelp.Add(field.Key, help);
                panel.Controls.Add(help);
            }

            var commands = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                TabIndex = tabIndex,
            };
            commands.Controls.Add(SaveButton);
            commands.Controls.Add(CancelButton);
            panel.Controls.Add(commands);
            var activation = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 12, 0, 0) };
            activation.Controls.Add(ActivateButton);
            activation.Controls.Add(DeactivateButton);
            panel.Controls.Add(activation);
            foreach (Control child in panel.Controls) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            // A dedicated scroll owner measures the complete preferred content height.
            // A scrolling fill TableLayoutPanel can under-report the final nested command row.
            var scroll = new Panel { Name = "CatalogInspector", Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(panel);
            return scroll;
        }

        private void ApplyState()
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            _applyingState = true;
            try
            {
                if (!string.Equals(SearchBox.Text, _binding.Search ?? string.Empty, StringComparison.Ordinal))
                {
                    SearchBox.Text = _binding.Search ?? string.Empty;
                }

                var busy = _binding.IsBusy || _binding.IsLoading || _binding.IsEditorLoading;
                _busyBinder.Project(_busyBinder.CaptureOwner(), busy,
                    _binding.IsBusy ? "Saving catalog…" : "Loading catalog…",
                    !_binding.IsBusy && (_binding.IsLoading || _binding.IsEditorLoading) ? _cancelBusyQuery : null);
                foreach (var field in _binding.Fields)
                {
                    var value = field.GetValue();
                    var editor = _editorControls[field.Key];
                    editor.Enabled = !busy;
                    if (!string.Equals(editor.Text, value, StringComparison.Ordinal))
                    {
                        editor.Text = value;
                    }
                }

                SetRows(_binding.Rows());
                if (_displayedSelectedId != _binding.SelectedId)
                {
                    _displayedSelectedId = _binding.SelectedId;
                    var selectedIndex = _rowIds.ToList().FindIndex(id => id == _displayedSelectedId);
                    ((CatalogResultsGrid)Grid).SynchronizeRow(selectedIndex + 1);
                }
                var selected = _binding.SelectedRow;
                if (_binding.SelectedId.HasValue) _creating = false;
                _heading.Text = _creating ? "New " + SingularName.ToLowerInvariant() : SingularName + " details";
                _selectedCode.Text = selected?.Code ?? (_creating ? "Enter the basic information below" : "Select a row to view details or choose " + NewButton.Text);
                _status.Text = selected != null ? (selected.IsActive ? "Active" : "Inactive") : (_creating ? "Not saved" : "No selection");
                _count.Text = _binding.TotalCount + " " + ( _binding.Kind == CatalogKind.Product ? (_binding.TotalCount == 1 ? "product" : "products") : "results");
                var first = _rows.Count == 0 ? 0 : (_binding.PageNumber - 1L) * _binding.PageSize + 1;
                var last = _rows.Count == 0 ? 0 : first + _rows.Count - 1;
                _resultRange.Text = first + "–" + last + " / " + _binding.TotalCount;
                _resultsState.Text = _binding.IsLoading ? "Loading results…" : _rows.Count == 0 ? "No results" : string.Empty;
                _resultsState.Visible = _resultsState.Text.Length != 0;
                _error.Text = _binding.ErrorMessage ?? string.Empty;
                _error.Visible = _feedbackNotifications == null && _error.Text.Length != 0;
                SaveButton.Enabled = !busy;
                RefreshButton.Enabled = !busy;
                NewButton.Enabled = !busy;
                Grid.Enabled = !busy;
                ActivateButton.Enabled = !busy && _binding.SelectedId.HasValue;
                DeactivateButton.Enabled = !busy && _binding.SelectedId.HasValue;
                var pageCount = Math.Max(1, (int)Math.Ceiling(
                    _binding.TotalCount / (double)_binding.PageSize));
                PageLabel.Text = $"Page {_binding.PageNumber} of {pageCount}";
                PreviousPageButton.Enabled = !busy && _binding.PageNumber > 1;
                NextPageButton.Enabled = !busy && _binding.PageNumber < pageCount;
                _validationBinder.Refresh();
            }
            finally
            {
                _applyingState = false;
            }
        }

        private void SetRows(IReadOnlyList<CatalogDisplayRow> rows)
        {
            if (_rows.Count == rows.Count && Grid.RowsCount != 0 && _rows.Zip(rows, (a, b) => a.Id == b.Id && a.Code == b.Code && a.Name == b.Name && a.IsActive == b.IsActive && a.Unit == b.Unit).All(equal => equal)) return;
            var active = Grid.Selection.ActivePosition;
            var activeId = active.Row > 0 && active.Row <= _rowIds.Count ? (int?)_rowIds[active.Row - 1] : _binding.SelectedId;
            var scroll = Grid.CustomScrollPosition;
            // SourceGrid selection stores indexes. Never let an old index identify a new entity.
            Grid.Selection.ResetSelection(false);
            _rows = rows;
            _rowIds = rows.Select(row => row.Id).ToArray();
            Grid.Redim(rows.Count + 1, 4);
            Grid.FixedRows = 1;
            var products = _binding.Kind == CatalogKind.Product;
            Grid[0, 0] = new SourceGrid.Cells.ColumnHeader(products ? "Code" : "ID");
            Grid[0, 1] = new SourceGrid.Cells.ColumnHeader(products ? "Name" : "Code");
            Grid[0, 2] = new SourceGrid.Cells.ColumnHeader(products ? "Unit" : "Name");
            Grid[0, 3] = new SourceGrid.Cells.ColumnHeader("Status");
            for (var index = 0; index < rows.Count; index++)
            {
                var gridRow = index + 1;
                Grid[gridRow, 0] = new SourceGrid.Cells.Cell(products ? (object)rows[index].Code : rows[index].Id);
                Grid[gridRow, 1] = new SourceGrid.Cells.Cell(products ? rows[index].Name : rows[index].Code);
                Grid[gridRow, 2] = new SourceGrid.Cells.Cell(products ? rows[index].Unit : rows[index].Name);
                Grid[gridRow, 3] = new SourceGrid.Cells.Cell(
                    rows[index].IsActive ? "Active" : "Inactive");
            }

            ArrangeGrid();
            var rowToFocus = rows.ToList().FindIndex(row => row.Id == activeId);
            ((CatalogResultsGrid)Grid).SynchronizeRow(rowToFocus + 1);
            Grid.CustomScrollPosition = scroll;
        }

        private void BeginCreate()
        {
            _creating = true;
            _binding.BeginCreate();
            ApplyState();
        }

        private bool SelectActiveRow()
        {
            var row = Grid.Selection.ActivePosition.Row;
            if (row <= 0 || row >= Grid.RowsCount)
            {
                return false;
            }

            var rowIndex = row - 1;
            if (rowIndex >= 0 && rowIndex < _rowIds.Count)
            {
                SetOperation(_binding.SelectAsync(_rowIds[rowIndex], CancellationToken.None));
                return true;
            }

            return false;
        }

        private void OnGridKeyDown(object? sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode != Keys.Enter ||
                eventArgs.Modifiers != Keys.None ||
                IsActiveGridCellEditing())
            {
                return;
            }

            if (SelectActiveRow())
            {
                eventArgs.Handled = true;
                eventArgs.SuppressKeyPress = true;
            }
        }

        private bool IsActiveGridCellEditing()
        {
            var position = Grid.Selection.ActivePosition;
            return !position.IsEmpty() &&
                new SourceGrid.CellContext(Grid, position).IsEditing();
        }

        private void SetActive(bool isActive)
        {
            var id = _binding.SelectedId;
            if (id.HasValue)
            {
                SetOperation(_binding.SetActiveAsync(id.Value, isActive, CancellationToken.None));
            }
        }

        private void MovePage(int offset)
        {
            var target = _binding.PageNumber + offset;
            if (target >= 1)
            {
                SetOperation(_binding.MoveToPageAsync(target, CancellationToken.None));
            }
        }

        private void SetOperation(Task operation)
        {
            LastOperation = operation ?? Task.CompletedTask;
        }

        private void OnSearchTextChanged(object? sender, EventArgs eventArgs)
        {
            if (!_applyingState)
            {
                SetOperation(_binding.SetSearch(SearchBox.Text));
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            DispatchToUi(ApplyState);
        }

        private void OnValidationChanged(object? sender, EventArgs eventArgs)
        {
            _validationSummary.Text = string.Join(Environment.NewLine,
                _validationBinder.Entries.Select(entry => entry.FieldKey + ": " + entry.Message));
            if (IsHandleCreated)
            {
                _validationSummary.Visible = _validationSummary.Text.Length != 0;
                _firstErrorButton.Visible = _validationBinder.Entries.Count != 0;
            }
        }

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

        private void UpdateEditorPanelWidth()
        {
            if (_arranging || _split == null || _toolbar == null) return;
            _arranging = true;
            try
            {
                var vertical = ClientSize.Width >= BusinessCompactAppearance.Scale(this, 880);
                _split.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
                var available = (vertical ? _split.ClientSize.Width : _split.ClientSize.Height) - _split.SplitterWidth;
                if (available > 100)
                    _split.SplitterDistance = vertical ? Math.Max(50, available - BusinessCompactAppearance.Scale(this, 340)) : Math.Max(50, available * 2 / 5);
                _toolbar.SuspendLayout();
                var narrow = ClientSize.Width < BusinessCompactAppearance.Scale(this, 820);
                var title = _toolbar.Controls[0];
                _toolbar.SetColumnSpan(title, narrow ? 4 : 1);
                _toolbar.SetCellPosition(SearchBox, new TableLayoutPanelCellPosition(narrow ? 0 : 1, narrow ? 1 : 0));
                _toolbar.SetColumnSpan(SearchBox, narrow ? 2 : 1);
                _toolbar.SetRow(RefreshButton, narrow ? 1 : 0);
                _toolbar.SetRow(NewButton, narrow ? 1 : 0);
                _toolbar.ResumeLayout(true);
                _error.MaximumSize = new Size(Math.Max(1, ClientSize.Width - 16), 0);
                ArrangeGrid();
            }
            finally { _arranging = false; }
        }

        private string SingularName => _binding.Kind.ToString();
        private string PluralName => _binding.Kind == CatalogKind.Product ? "Products" : _binding.Kind == CatalogKind.Warehouse ? "Warehouses" : _binding.Kind == CatalogKind.Employee ? "Employees" : "Customers";
        private static Label TextLabel(string name, string text, string typography = "") => new Label { Name = name, Text = text, AutoSize = true, Tag = typography };
        private void OnThemeChanged(object? sender, BootstrapThemeChangedEventArgs args) => DispatchToUi(() => { ArrangeGrid(); UpdateEditorPanelWidth(); });
        private void ArrangeGrid()
        {
            if (Grid.ColumnsCount == 0) return;
            var products = _binding.Kind == CatalogKind.Product;
            Grid.Columns[0].Width = BusinessCompactAppearance.Scale(this, products ? 94 : 42);
            Grid.Columns[1].Width = products ? Math.Max(BusinessCompactAppearance.Scale(this, 190), Grid.ClientSize.Width - BusinessCompactAppearance.Scale(this, 286)) : BusinessCompactAppearance.Scale(this, 94);
            Grid.Columns[2].Width = products ? BusinessCompactAppearance.Scale(this, 70) : Math.Max(BusinessCompactAppearance.Scale(this, 190), Grid.ClientSize.Width - BusinessCompactAppearance.Scale(this, 258));
            Grid.Columns[3].Width = BusinessCompactAppearance.Scale(this, 102);
            for (var row = 0; row < Grid.RowsCount; row++) Grid.Rows[row].Height = BusinessCompactAppearance.Scale(this, row == 0 ? 36 : 32);
            Grid.Invalidate();
        }

        private static BootstrapButton CreateButton(string text, int tabIndex, Action action)
        {
            var button = new BootstrapButton { Text = text, AutoSize = true, TabIndex = tabIndex };
            button.Click += (_, __) => action();
            return button;
        }

        private static CatalogBinding CreateBinding(ProductCatalogViewModel viewModel) =>
            CatalogBinding.Create(
                viewModel,
                () => viewModel.Items.Select(item => new CatalogDisplayRow(item.Id, item.Code, item.Name, item.IsActive, item.Unit)).ToArray(),
                () => viewModel.SelectedItem is ProductDto selected ? new CatalogDisplayRow(selected.Id, selected.Code, selected.Name, selected.IsActive, selected.Unit) : null,
                new CatalogField("code", "Code", () => viewModel.Code, value => viewModel.Code = value),
                new CatalogField("name", "Name", () => viewModel.Name, value => viewModel.Name = value),
                new CatalogField("unit", "Unit", () => viewModel.Unit, value => viewModel.Unit = value));

        private static CatalogBinding CreateBinding(WarehouseCatalogViewModel viewModel) =>
            CatalogBinding.Create(
                viewModel,
                () => viewModel.Items.Select(item => new CatalogDisplayRow(item.Id, item.Code, item.Name, item.IsActive)).ToArray(),
                () => viewModel.SelectedItem is WarehouseDto selected ? new CatalogDisplayRow(selected.Id, selected.Code, selected.Name, selected.IsActive) : null,
                new CatalogField("code", "Code", () => viewModel.Code, value => viewModel.Code = value),
                new CatalogField("name", "Name", () => viewModel.Name, value => viewModel.Name = value),
                new CatalogField("address", "Address", () => viewModel.Address ?? string.Empty, value => viewModel.Address = value));

        private static CatalogBinding CreateBinding(EmployeeCatalogViewModel viewModel) =>
            CatalogBinding.Create(
                viewModel,
                () => viewModel.Items.Select(item => new CatalogDisplayRow(item.Id, item.Code, item.Name, item.IsActive)).ToArray(),
                () => viewModel.SelectedItem is EmployeeDto selected ? new CatalogDisplayRow(selected.Id, selected.Code, selected.Name, selected.IsActive) : null,
                new CatalogField("code", "Code", () => viewModel.Code, value => viewModel.Code = value),
                new CatalogField("name", "Name", () => viewModel.Name, value => viewModel.Name = value),
                new CatalogField("phone", "Phone", () => viewModel.Phone ?? string.Empty, value => viewModel.Phone = value),
                new CatalogField("userId", "User ID", () => viewModel.UserIdText, value => viewModel.UserIdText = value));

        private static CatalogBinding CreateBinding(CustomerCatalogViewModel viewModel) =>
            CatalogBinding.Create(
                viewModel,
                () => viewModel.Items.Select(item => new CatalogDisplayRow(item.Id, item.Code, item.Name, item.IsActive)).ToArray(),
                () => viewModel.SelectedItem is CustomerDto selected ? new CatalogDisplayRow(selected.Id, selected.Code, selected.Name, selected.IsActive) : null,
                new CatalogField("code", "Code", () => viewModel.Code, value => viewModel.Code = value),
                new CatalogField("name", "Name", () => viewModel.Name, value => viewModel.Name = value),
                new CatalogField("address", "Address", () => viewModel.Address ?? string.Empty, value => viewModel.Address = value),
                new CatalogField("phone", "Phone", () => viewModel.Phone ?? string.Empty, value => viewModel.Phone = value),
                new CatalogField("taxCode", "Tax code", () => viewModel.TaxCode ?? string.Empty, value => viewModel.TaxCode = value));

        private sealed class CatalogBinding : IDisposable
        {
            private readonly IDisposable _disposable;
            private readonly Func<CancellationToken, Task> _activate;
            private readonly Func<CancellationToken, Task> _refresh;
            private readonly Func<string?, Task> _setSearch;
            private readonly Func<int, CancellationToken, Task> _select;
            private readonly Func<CancellationToken, Task> _save;
            private readonly Func<CancellationToken, Task<WorkspaceSaveOutcome>> _saveForClose;
            private readonly Func<CatalogEditSnapshot> _captureEditSnapshot;
            private readonly Func<int, bool, CancellationToken, Task> _setActive;
            private readonly Func<int, CancellationToken, Task> _moveToPage;
            private readonly Action _beginCreate;
            private readonly Action _cancel;
            private readonly Func<IEnumerable> _getErrors;
            private readonly Func<int?> _selectedId;
            private readonly Func<string?> _search;
            private readonly Func<bool> _isBusy;
            private readonly Func<bool> _isLoading;
            private readonly Func<bool> _isEditorLoading;
            private readonly Func<int> _pageNumber;
            private readonly Func<int> _pageSize;
            private readonly Func<long> _totalCount;
            private readonly Func<CatalogDisplayRow?> _selectedRow;

            private CatalogBinding(
                IDisposable disposable,
                INotifyPropertyChanged propertyChanged,
                INotifyDataErrorInfo errors,
                Func<IReadOnlyList<CatalogDisplayRow>> rows,
                IReadOnlyList<CatalogField> fields,
                Func<CancellationToken, Task> activate,
                Func<CancellationToken, Task> refresh,
                Func<string?, Task> setSearch,
                Func<int, CancellationToken, Task> select,
                Func<CancellationToken, Task> save,
                Func<CancellationToken, Task<WorkspaceSaveOutcome>> saveForClose,
                Func<CatalogEditSnapshot> captureEditSnapshot,
                Func<int, bool, CancellationToken, Task> setActive,
                Func<int, CancellationToken, Task> moveToPage,
                Action beginCreate,
                Action cancel,
                Func<int?> selectedId,
                Func<string?> search,
                Func<bool> isBusy,
                Func<bool> isLoading,
                Func<bool> isEditorLoading,
                Func<int> pageNumber,
                Func<int> pageSize,
                Func<long> totalCount, CatalogKind kind, Func<CatalogDisplayRow?> selectedRow)
            {
                _disposable = disposable;
                PropertySource = propertyChanged;
                ErrorSource = errors;
                Rows = rows;
                Fields = fields;
                _activate = activate;
                _refresh = refresh;
                _setSearch = setSearch;
                _select = select;
                _save = save;
                _saveForClose = saveForClose;
                _captureEditSnapshot = captureEditSnapshot;
                _setActive = setActive;
                _moveToPage = moveToPage;
                _beginCreate = beginCreate;
                _cancel = cancel;
                _selectedId = selectedId;
                _search = search;
                _isBusy = isBusy;
                _isLoading = isLoading;
                _isEditorLoading = isEditorLoading;
                _pageNumber = pageNumber;
                _pageSize = pageSize;
                _totalCount = totalCount;
                Kind = kind; _selectedRow = selectedRow;
                _getErrors = () => Array.Empty<string>();
            }

            public event PropertyChangedEventHandler? PropertyChanged
            {
                add => PropertySource.PropertyChanged += value;
                remove => PropertySource.PropertyChanged -= value;
            }

            public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged
            {
                add => ErrorSource.ErrorsChanged += value;
                remove => ErrorSource.ErrorsChanged -= value;
            }

            private INotifyPropertyChanged PropertySource { get; }
            public INotifyDataErrorInfo ErrorSource { get; }
            public Func<IReadOnlyList<CatalogDisplayRow>> Rows { get; }
            public IReadOnlyList<CatalogField> Fields { get; }
            public CatalogKind Kind { get; }
            public CatalogDisplayRow? SelectedRow => _selectedRow();
            public string? ErrorMessage => ((DesktopViewModelBase)_disposable).ErrorMessage;
            public int? SelectedId => _selectedId();
            public string? Search => _search();
            public bool IsBusy => _isBusy();
            public bool IsLoading => _isLoading();
            public bool IsEditorLoading => _isEditorLoading();
            public int PageNumber => _pageNumber();
            public int PageSize => _pageSize();
            public long TotalCount => _totalCount();
            public IEnumerable GetErrors(string field) => ErrorSource.GetErrors(field) ?? _getErrors();
            public Task ActivateAsync(CancellationToken token) => _activate(token);
            public Task RefreshAsync(CancellationToken token) => _refresh(token);
            public Task SetSearch(string? search) => _setSearch(search);
            public Task SelectAsync(int id, CancellationToken token) => _select(id, token);
            public Task SaveAsync(CancellationToken token) => _save(token);
            public Task<WorkspaceSaveOutcome> SaveForCloseAsync(CancellationToken token) =>
                _saveForClose(token);
            public CatalogEditSnapshot CaptureEditSnapshot() => _captureEditSnapshot();
            public Task SetActiveAsync(int id, bool active, CancellationToken token) => _setActive(id, active, token);
            public Task MoveToPageAsync(int pageNumber, CancellationToken token) => _moveToPage(pageNumber, token);
            public void BeginCreate() => _beginCreate();
            public void Cancel() => _cancel();
            public void Dispose() => _disposable.Dispose();

            public static CatalogBinding Create<TDto>(
                CatalogViewModel<TDto> viewModel,
                Func<IReadOnlyList<CatalogDisplayRow>> rows,
                Func<CatalogDisplayRow?> selectedRow,
                params CatalogField[] fields)
            {
                return new CatalogBinding(
                    viewModel,
                    viewModel,
                    viewModel,
                    rows,
                    fields,
                    viewModel.ActivateAsync,
                    viewModel.RefreshAsync,
                    viewModel.SetSearch,
                    viewModel.SelectAsync,
                    viewModel.SaveAsync,
                    viewModel.SaveForCloseAsync,
                    viewModel.CaptureEditSnapshot,
                    viewModel.SetActiveAsync,
                    viewModel.MoveToPageAsync,
                    viewModel.BeginCreate,
                    viewModel.Cancel,
                    () => viewModel.SelectedId,
                    () => viewModel.Search,
                    () => viewModel.IsBusy,
                    () => viewModel.IsLoading,
                    () => viewModel.IsEditorLoading,
                    () => viewModel.PageNumber,
                    () => viewModel.PageSize,
                    () => viewModel.TotalCount, viewModel.Kind, selectedRow);
            }
        }

        // The result cells are read-only. During model projection only, preserve SourceGrid's
        // logical active position without calling Control.Focus on a native inspector editor.
        // All user keyboard/mouse focus still follows the unmodified vendor implementation.
        private sealed class CatalogResultsGrid : AccessibleActionGrid
        {
            private bool _synchronizing;
            internal void SynchronizeRow(int row)
            {
                _synchronizing = true;
                try
                {
                    Selection.ResetSelection(false);
                    if (row > 0 && row < RowsCount) Selection.Focus(new SourceGrid.Position(row, 0), true);
                }
                finally { _synchronizing = false; }
            }
            public override bool Focus(bool selectFirstCell) => _synchronizing || base.Focus(selectFirstCell);
        }

        private sealed class CatalogField
        {
            public CatalogField(string key, string label, Func<string> getValue, Action<string> setValue)
            {
                Key = key;
                Label = label;
                GetValue = getValue;
                SetValue = setValue;
            }

            public string Key { get; }
            public string Label { get; }
            public Func<string> GetValue { get; }
            public Action<string> SetValue { get; }
        }

        private sealed class CatalogDisplayRow
        {
            public CatalogDisplayRow(int id, string code, string name, bool isActive, string unit = "")
            {
                Id = id;
                Code = code;
                Name = name;
                IsActive = isActive; Unit = unit;
            }

            public int Id { get; }
            public string Code { get; }
            public string Name { get; }
            public bool IsActive { get; }
            public string Unit { get; }
        }
    }
}
