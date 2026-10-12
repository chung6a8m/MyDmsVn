using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    public sealed class StockBalanceControl : UserControl
    {
        private readonly StockBalanceViewModel _viewModel;

        public StockBalanceControl(StockBalanceViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            Dock = DockStyle.Fill;

            SearchTextBox = new TextBox { Width = 180 };
            WarehouseFilter = CreateIdFilter();
            ProductFilter = CreateIdFilter();
            RefreshButton = new Button { Text = "Refresh", AutoSize = true };
            PreviousPageButton = new Button { Text = "Previous", AutoSize = true };
            NextPageButton = new Button { Text = "Next", AutoSize = true };
            PageLabel = new Label { AutoSize = true, Text = "Page 1" };
            TotalLabel = new Label { AutoSize = true, Text = "0.0000" };
            OperationStatusLabel = new Label { AutoSize = true, Text = "Ready" };
            BusyIndicator = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Width = 100,
                Visible = false,
            };
            BalanceGrid = CreateGrid();

            var filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
            };
            filters.Controls.Add(new Label { Text = "Search", AutoSize = true });
            filters.Controls.Add(SearchTextBox);
            filters.Controls.Add(new Label { Text = "Warehouse ID", AutoSize = true });
            filters.Controls.Add(WarehouseFilter);
            filters.Controls.Add(new Label { Text = "Product ID", AutoSize = true });
            filters.Controls.Add(ProductFilter);
            filters.Controls.Add(RefreshButton);
            filters.Controls.Add(PreviousPageButton);
            filters.Controls.Add(NextPageButton);
            filters.Controls.Add(PageLabel);
            filters.Controls.Add(new Label { Text = "Visible total", AutoSize = true });
            filters.Controls.Add(TotalLabel);
            filters.Controls.Add(BusyIndicator);
            filters.Controls.Add(OperationStatusLabel);

            Controls.Add(BalanceGrid);
            Controls.Add(filters);

            SearchTextBox.TextChanged += OnSearchChanged;
            WarehouseFilter.ValueChanged += OnWarehouseChanged;
            ProductFilter.ValueChanged += OnProductChanged;
            RefreshButton.Click += OnRefreshClicked;
            PreviousPageButton.Click += OnPreviousPageClicked;
            NextPageButton.Click += OnNextPageClicked;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Render();
        }

        public DataGridView BalanceGrid { get; }
        public TextBox SearchTextBox { get; }
        public NumericUpDown WarehouseFilter { get; }
        public NumericUpDown ProductFilter { get; }
        public Button RefreshButton { get; }
        public Button PreviousPageButton { get; }
        public Button NextPageButton { get; }
        public Label PageLabel { get; }
        public Label TotalLabel { get; }
        public Label OperationStatusLabel { get; }
        public ProgressBar BusyIndicator { get; }
        public Task LastOperation { get; private set; } = Task.CompletedTask;

        public Task ActivateAsync(CancellationToken cancellationToken)
        {
            LastOperation = _viewModel.ActivateAsync(cancellationToken);
            return LastOperation;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SearchTextBox.TextChanged -= OnSearchChanged;
                WarehouseFilter.ValueChanged -= OnWarehouseChanged;
                ProductFilter.ValueChanged -= OnProductChanged;
                RefreshButton.Click -= OnRefreshClicked;
                PreviousPageButton.Click -= OnPreviousPageClicked;
                NextPageButton.Click -= OnNextPageClicked;
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OnSearchChanged(object? sender, EventArgs eventArgs) =>
            LastOperation = _viewModel.SetSearch(SearchTextBox.Text);

        private void OnWarehouseChanged(object? sender, EventArgs eventArgs) =>
            LastOperation = _viewModel.SetWarehouseFilterAsync(
                ToOptionalId(WarehouseFilter.Value),
                CancellationToken.None);

        private void OnProductChanged(object? sender, EventArgs eventArgs) =>
            LastOperation = _viewModel.SetProductFilterAsync(
                ToOptionalId(ProductFilter.Value),
                CancellationToken.None);

        private void OnRefreshClicked(object? sender, EventArgs eventArgs) =>
            LastOperation = _viewModel.RefreshAsync(CancellationToken.None);

        private void OnPreviousPageClicked(object? sender, EventArgs eventArgs)
        {
            if (_viewModel.PageNumber <= 1)
            {
                return;
            }

            _viewModel.PageNumber--;
            LastOperation = _viewModel.RefreshAsync(CancellationToken.None);
        }

        private void OnNextPageClicked(object? sender, EventArgs eventArgs)
        {
            if ((long)_viewModel.PageNumber * _viewModel.PageSize >= _viewModel.TotalCount)
            {
                return;
            }

            _viewModel.PageNumber++;
            LastOperation = _viewModel.RefreshAsync(CancellationToken.None);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) =>
            Render();

        private void Render()
        {
            BalanceGrid.Rows.Clear();
            foreach (var row in _viewModel.Items)
            {
                BalanceGrid.Rows.Add(
                    FormatCatalog(row.WarehouseCode, row.WarehouseName, row.WarehouseIsActive),
                    FormatCatalog(row.ProductCode, row.ProductName, row.ProductIsActive),
                    row.Quantity.ToString("0.0000"));
            }

            var totalPages = Math.Max(
                1L,
                (_viewModel.TotalCount + _viewModel.PageSize - 1) / _viewModel.PageSize);
            PageLabel.Text = "Page " + _viewModel.PageNumber + " of " + totalPages;
            TotalLabel.Text = _viewModel.VisibleQuantityTotal.ToString("0.0000");
            BusyIndicator.Visible = _viewModel.IsLoading;
            BalanceGrid.Enabled = !_viewModel.IsLoading;
            RefreshButton.Enabled = !_viewModel.IsLoading;
            PreviousPageButton.Enabled = !_viewModel.IsLoading && _viewModel.PageNumber > 1;
            NextPageButton.Enabled = !_viewModel.IsLoading && _viewModel.PageNumber < totalPages;
            OperationStatusLabel.Text = !string.IsNullOrWhiteSpace(_viewModel.ErrorMessage)
                ? _viewModel.ErrorMessage
                : _viewModel.IsEmpty
                    ? "No stock balances."
                    : "Ready";
        }

        private static DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            };
            grid.Columns.Add("warehouse", "Warehouse");
            grid.Columns.Add("product", "Product");
            grid.Columns.Add("quantity", "Quantity");
            return grid;
        }

        private static NumericUpDown CreateIdFilter() =>
            new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Width = 100 };

        private static int? ToOptionalId(decimal value) => value <= 0 ? null : decimal.ToInt32(value);

        private static string FormatCatalog(string code, string name, bool isActive) =>
            code + " - " + name + (isActive ? string.Empty : " (inactive)");
    }
}
