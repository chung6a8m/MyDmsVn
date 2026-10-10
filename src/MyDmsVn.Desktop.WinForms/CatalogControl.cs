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
using MyDmsVn.Desktop.Application;
using BootstrapGrid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

namespace MyDmsVn.Desktop.WinForms
{
    public interface ICatalogControlFactory
    {
        Control Create(CatalogKind kind);
    }

    public sealed class CatalogControl : UserControl
    {
        private readonly CatalogBinding _binding;
        private readonly ErrorProvider _errors;
        private readonly Dictionary<string, BootstrapTextBox> _editorControls =
            new Dictionary<string, BootstrapTextBox>(StringComparer.Ordinal);
        private readonly SplitContainer _split;
        private readonly int _uiThreadId;
        private bool _applyingState;

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
                Dock = DockStyle.Fill,
                TabIndex = 0,
            };
            Grid = new BootstrapGrid
            {
                Dock = DockStyle.Fill,
                TabIndex = 1,
                TabStop = true,
            };
            Grid.DoubleClick += (_, __) => SelectActiveRow();

            RefreshButton = CreateButton("Refresh", 20, () => SetOperation(_binding.RefreshAsync(CancellationToken.None)));
            PreviousPageButton = CreateButton("Previous", 18, () => MovePage(-1));
            NextPageButton = CreateButton("Next", 19, () => MovePage(1));
            PageLabel = new Label { AutoSize = true, TextAlign = ContentAlignment.MiddleCenter };
            NewButton = CreateButton("New", 21, BeginCreate);
            SaveButton = CreateButton("Save", 22, () => SetOperation(_binding.SaveAsync(CancellationToken.None)));
            ActivateButton = CreateButton("Activate", 23, () => SetActive(true));
            DeactivateButton = CreateButton("Deactivate", 24, () => SetActive(false));
            CancelButton = CreateButton("Cancel", 25, _binding.Cancel);

            _errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            var editor = CreateEditorPanel();
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2,
            };
            _split.Panel1.Controls.Add(Grid);
            _split.Panel2.Controls.Add(editor);
            _split.SizeChanged += (_, __) => UpdateEditorPanelWidth();

            var searchBar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(8),
            };
            searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchBar.Controls.Add(SearchBox, 0, 0);
            searchBar.Controls.Add(RefreshButton, 1, 0);

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

            Controls.Add(_split);
            Controls.Add(pagingBar);
            Controls.Add(searchBar);
            SearchBox.TextChanged += OnSearchTextChanged;
            _binding.PropertyChanged += OnViewModelPropertyChanged;
            _binding.ErrorsChanged += OnErrorsChanged;
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
            return _editorControls.TryGetValue(field, out var control)
                ? _errors.GetError(control)
                : string.Empty;
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
                _binding.ErrorsChanged -= OnErrorsChanged;
                _binding.Dispose();
                _errors.Dispose();
            }

            base.Dispose(disposing);
        }

        private Control CreateEditorPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 1,
                Padding = new Padding(12),
            };
            var tabIndex = 2;
            foreach (var field in _binding.Fields)
            {
                var editor = new BootstrapTextBox
                {
                    AccessibleName = field.Label,
                    Dock = DockStyle.Top,
                    TabIndex = tabIndex++,
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
            }

            var commands = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                TabIndex = tabIndex,
            };
            commands.Controls.Add(NewButton);
            commands.Controls.Add(SaveButton);
            commands.Controls.Add(ActivateButton);
            commands.Controls.Add(DeactivateButton);
            commands.Controls.Add(CancelButton);
            panel.Controls.Add(commands);
            return panel;
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

                foreach (var field in _binding.Fields)
                {
                    var value = field.GetValue();
                    if (!string.Equals(_editorControls[field.Key].Text, value, StringComparison.Ordinal))
                    {
                        _editorControls[field.Key].Text = value;
                    }
                }

                SetRows(_binding.Rows());
                var busy = _binding.IsBusy || _binding.IsLoading;
                SaveButton.Enabled = !busy;
                RefreshButton.Enabled = !busy;
                NewButton.Enabled = !busy;
                Grid.Enabled = !busy;
                ActivateButton.Enabled = !busy;
                DeactivateButton.Enabled = !busy;
                var pageCount = Math.Max(1, (int)Math.Ceiling(
                    _binding.TotalCount / (double)_binding.PageSize));
                PageLabel.Text = $"Page {_binding.PageNumber} of {pageCount}";
                PreviousPageButton.Enabled = !busy && _binding.PageNumber > 1;
                NextPageButton.Enabled = !busy && _binding.PageNumber < pageCount;
                ApplyErrors();
            }
            finally
            {
                _applyingState = false;
            }
        }

        private void SetRows(IReadOnlyList<CatalogDisplayRow> rows)
        {
            Grid.Redim(rows.Count + 1, 4);
            Grid.FixedRows = 1;
            Grid[0, 0] = new SourceGrid.Cells.ColumnHeader("ID");
            Grid[0, 1] = new SourceGrid.Cells.ColumnHeader("Code");
            Grid[0, 2] = new SourceGrid.Cells.ColumnHeader("Name");
            Grid[0, 3] = new SourceGrid.Cells.ColumnHeader("Status");
            for (var index = 0; index < rows.Count; index++)
            {
                var gridRow = index + 1;
                Grid[gridRow, 0] = new SourceGrid.Cells.Cell(rows[index].Id, typeof(int));
                Grid[gridRow, 1] = new SourceGrid.Cells.Cell(rows[index].Code, typeof(string));
                Grid[gridRow, 2] = new SourceGrid.Cells.Cell(rows[index].Name, typeof(string));
                Grid[gridRow, 3] = new SourceGrid.Cells.Cell(
                    rows[index].IsActive ? "Active" : "Inactive",
                    typeof(string));
            }

            Grid.AutoSizeCells();
        }

        private void BeginCreate()
        {
            _binding.BeginCreate();
            ApplyState();
        }

        private void SelectActiveRow()
        {
            var row = Grid.Selection.ActivePosition.Row;
            if (row <= 0 || row >= Grid.RowsCount)
            {
                return;
            }

            if (Grid[row, 0].Value is int id)
            {
                SetOperation(_binding.SelectAsync(id, CancellationToken.None));
            }
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

        private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs eventArgs)
        {
            DispatchToUi(ApplyErrors);
        }

        private void ApplyErrors()
        {
            foreach (var field in _editorControls)
            {
                var messages = _binding.GetErrors(field.Key).Cast<string>();
                _errors.SetError(field.Value, string.Join(Environment.NewLine, messages));
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
            var available = _split.ClientSize.Width - _split.SplitterWidth;
            if (available >= 560)
            {
                _split.SplitterDistance = available - Math.Min(400, Math.Max(280, available / 3));
            }
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
                () => viewModel.Items.Select(item => new CatalogDisplayRow(item.Id, item.Code, item.Name, item.IsActive)).ToArray(),
                new CatalogField("code", "Code", () => viewModel.Code, value => viewModel.Code = value),
                new CatalogField("name", "Name", () => viewModel.Name, value => viewModel.Name = value),
                new CatalogField("unit", "Unit", () => viewModel.Unit, value => viewModel.Unit = value));

        private static CatalogBinding CreateBinding(WarehouseCatalogViewModel viewModel) =>
            CatalogBinding.Create(
                viewModel,
                () => viewModel.Items.Select(item => new CatalogDisplayRow(item.Id, item.Code, item.Name, item.IsActive)).ToArray(),
                new CatalogField("code", "Code", () => viewModel.Code, value => viewModel.Code = value),
                new CatalogField("name", "Name", () => viewModel.Name, value => viewModel.Name = value),
                new CatalogField("address", "Address", () => viewModel.Address ?? string.Empty, value => viewModel.Address = value));

        private static CatalogBinding CreateBinding(EmployeeCatalogViewModel viewModel) =>
            CatalogBinding.Create(
                viewModel,
                () => viewModel.Items.Select(item => new CatalogDisplayRow(item.Id, item.Code, item.Name, item.IsActive)).ToArray(),
                new CatalogField("code", "Code", () => viewModel.Code, value => viewModel.Code = value),
                new CatalogField("name", "Name", () => viewModel.Name, value => viewModel.Name = value),
                new CatalogField("phone", "Phone", () => viewModel.Phone ?? string.Empty, value => viewModel.Phone = value),
                new CatalogField("userId", "User ID", () => viewModel.UserIdText, value => viewModel.UserIdText = value));

        private static CatalogBinding CreateBinding(CustomerCatalogViewModel viewModel) =>
            CatalogBinding.Create(
                viewModel,
                () => viewModel.Items.Select(item => new CatalogDisplayRow(item.Id, item.Code, item.Name, item.IsActive)).ToArray(),
                new CatalogField("code", "Code", () => viewModel.Code, value => viewModel.Code = value),
                new CatalogField("name", "Name", () => viewModel.Name, value => viewModel.Name = value),
                new CatalogField("address", "Address", () => viewModel.Address ?? string.Empty, value => viewModel.Address = value),
                new CatalogField("phone", "Phone", () => viewModel.Phone ?? string.Empty, value => viewModel.Phone = value),
                new CatalogField("taxCode", "Tax code", () => viewModel.TaxCode ?? string.Empty, value => viewModel.TaxCode = value));

        private sealed class CatalogBinding : IDisposable
        {
            private readonly IDisposable _disposable;
            private readonly Func<Task> _activate;
            private readonly Func<Task> _refresh;
            private readonly Func<string?, Task> _setSearch;
            private readonly Func<int, Task> _select;
            private readonly Func<Task> _save;
            private readonly Func<int, bool, Task> _setActive;
            private readonly Func<int, Task> _moveToPage;
            private readonly Action _beginCreate;
            private readonly Action _cancel;
            private readonly Func<IEnumerable> _getErrors;
            private readonly Func<int?> _selectedId;
            private readonly Func<string?> _search;
            private readonly Func<bool> _isBusy;
            private readonly Func<bool> _isLoading;
            private readonly Func<int> _pageNumber;
            private readonly Func<int> _pageSize;
            private readonly Func<long> _totalCount;

            private CatalogBinding(
                IDisposable disposable,
                INotifyPropertyChanged propertyChanged,
                INotifyDataErrorInfo errors,
                Func<IReadOnlyList<CatalogDisplayRow>> rows,
                IReadOnlyList<CatalogField> fields,
                Func<Task> activate,
                Func<Task> refresh,
                Func<string?, Task> setSearch,
                Func<int, Task> select,
                Func<Task> save,
                Func<int, bool, Task> setActive,
                Func<int, Task> moveToPage,
                Action beginCreate,
                Action cancel,
                Func<int?> selectedId,
                Func<string?> search,
                Func<bool> isBusy,
                Func<bool> isLoading,
                Func<int> pageNumber,
                Func<int> pageSize,
                Func<long> totalCount)
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
                _setActive = setActive;
                _moveToPage = moveToPage;
                _beginCreate = beginCreate;
                _cancel = cancel;
                _selectedId = selectedId;
                _search = search;
                _isBusy = isBusy;
                _isLoading = isLoading;
                _pageNumber = pageNumber;
                _pageSize = pageSize;
                _totalCount = totalCount;
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
            private INotifyDataErrorInfo ErrorSource { get; }
            public Func<IReadOnlyList<CatalogDisplayRow>> Rows { get; }
            public IReadOnlyList<CatalogField> Fields { get; }
            public int? SelectedId => _selectedId();
            public string? Search => _search();
            public bool IsBusy => _isBusy();
            public bool IsLoading => _isLoading();
            public int PageNumber => _pageNumber();
            public int PageSize => _pageSize();
            public long TotalCount => _totalCount();
            public IEnumerable GetErrors(string field) => ErrorSource.GetErrors(field) ?? _getErrors();
            public Task ActivateAsync(CancellationToken token) => _activate();
            public Task RefreshAsync(CancellationToken token) => _refresh();
            public Task SetSearch(string? search) => _setSearch(search);
            public Task SelectAsync(int id, CancellationToken token) => _select(id);
            public Task SaveAsync(CancellationToken token) => _save();
            public Task SetActiveAsync(int id, bool active, CancellationToken token) => _setActive(id, active);
            public Task MoveToPageAsync(int pageNumber, CancellationToken token) => _moveToPage(pageNumber);
            public void BeginCreate() => _beginCreate();
            public void Cancel() => _cancel();
            public void Dispose() => _disposable.Dispose();

            public static CatalogBinding Create<TDto>(
                CatalogViewModel<TDto> viewModel,
                Func<IReadOnlyList<CatalogDisplayRow>> rows,
                params CatalogField[] fields)
            {
                return new CatalogBinding(
                    viewModel,
                    viewModel,
                    viewModel,
                    rows,
                    fields,
                    () => viewModel.ActivateAsync(CancellationToken.None),
                    () => viewModel.RefreshAsync(CancellationToken.None),
                    viewModel.SetSearch,
                    id => viewModel.SelectAsync(id, CancellationToken.None),
                    () => viewModel.SaveAsync(CancellationToken.None),
                    (id, active) => viewModel.SetActiveAsync(id, active, CancellationToken.None),
                    page => viewModel.MoveToPageAsync(page, CancellationToken.None),
                    viewModel.BeginCreate,
                    viewModel.Cancel,
                    () => viewModel.SelectedId,
                    () => viewModel.Search,
                    () => viewModel.IsBusy,
                    () => viewModel.IsLoading,
                    () => viewModel.PageNumber,
                    () => viewModel.PageSize,
                    () => viewModel.TotalCount);
            }
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
            public CatalogDisplayRow(int id, string code, string name, bool isActive)
            {
                Id = id;
                Code = code;
                Name = name;
                IsActive = isActive;
            }

            public int Id { get; }
            public string Code { get; }
            public string Name { get; }
            public bool IsActive { get; }
        }
    }
}
