using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    public sealed class StockCardControl : UserControl
    {
        private readonly StockCardViewModel _viewModel;

        public StockCardControl(StockCardViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            Dock = DockStyle.Fill;

            WarehouseFilter = CreateIdFilter();
            WarehouseFilter.AccessibleName = "Warehouse ID";
            ProductFilter = CreateIdFilter();
            ProductFilter.AccessibleName = "Product ID";
            FromUtcFilter = CreateDateFilter();
            FromUtcFilter.AccessibleName = "From UTC";
            ToUtcFilter = CreateDateFilter();
            ToUtcFilter.AccessibleName = "To UTC";
            RefreshButton = new Button { Text = "Refresh", AutoSize = true };
            PreviousPageButton = new Button { Text = "Previous", AutoSize = true };
            NextPageButton = new Button { Text = "Next", AutoSize = true };
            PageLabel = new Label { AutoSize = true, Text = "Page 1" };
            TotalLabel = new Label { AutoSize = true, Text = "0.0000" };
            OperationStatusLabel = new Label { AutoSize = true, Text = "Select warehouse and product." };
            BusyIndicator = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Width = 100,
                Visible = false,
            };
            StockCardGrid = CreateGrid();

            var filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
            };
            filters.Controls.Add(new Label { Text = "Warehouse ID", AutoSize = true });
            filters.Controls.Add(WarehouseFilter);
            filters.Controls.Add(new Label { Text = "Product ID", AutoSize = true });
            filters.Controls.Add(ProductFilter);
            filters.Controls.Add(new Label { Text = "From UTC", AutoSize = true });
            filters.Controls.Add(FromUtcFilter);
            filters.Controls.Add(new Label { Text = "To UTC", AutoSize = true });
            filters.Controls.Add(ToUtcFilter);
            filters.Controls.Add(RefreshButton);
            filters.Controls.Add(PreviousPageButton);
            filters.Controls.Add(NextPageButton);
            filters.Controls.Add(PageLabel);
            filters.Controls.Add(new Label { Text = "Visible delta", AutoSize = true });
            filters.Controls.Add(TotalLabel);
            filters.Controls.Add(BusyIndicator);
            filters.Controls.Add(OperationStatusLabel);

            Controls.Add(StockCardGrid);
            Controls.Add(filters);

            WarehouseFilter.ValueChanged += OnSelectionChanged;
            ProductFilter.ValueChanged += OnSelectionChanged;
            FromUtcFilter.ValueChanged += OnDateRangeChanged;
            ToUtcFilter.ValueChanged += OnDateRangeChanged;
            RefreshButton.Click += OnRefreshClicked;
            PreviousPageButton.Click += OnPreviousPageClicked;
            NextPageButton.Click += OnNextPageClicked;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Render();
        }

        public DataGridView StockCardGrid { get; }
        public NumericUpDown WarehouseFilter { get; }
        public NumericUpDown ProductFilter { get; }
        public DateTimePicker FromUtcFilter { get; }
        public DateTimePicker ToUtcFilter { get; }
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
                WarehouseFilter.ValueChanged -= OnSelectionChanged;
                ProductFilter.ValueChanged -= OnSelectionChanged;
                FromUtcFilter.ValueChanged -= OnDateRangeChanged;
                ToUtcFilter.ValueChanged -= OnDateRangeChanged;
                RefreshButton.Click -= OnRefreshClicked;
                PreviousPageButton.Click -= OnPreviousPageClicked;
                NextPageButton.Click -= OnNextPageClicked;
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OnSelectionChanged(object? sender, EventArgs eventArgs)
        {
            LastOperation = _viewModel.SetFiltersAsync(
                decimal.ToInt32(WarehouseFilter.Value),
                decimal.ToInt32(ProductFilter.Value),
                ToUtc(FromUtcFilter),
                ToUtc(ToUtcFilter, includeDisplayedSecond: true),
                CancellationToken.None);
        }

        private void OnDateRangeChanged(object? sender, EventArgs eventArgs)
        {
            if (WarehouseFilter.Value <= 0 || ProductFilter.Value <= 0)
            {
                return;
            }

            LastOperation = _viewModel.SetDateRangeAsync(
                ToUtc(FromUtcFilter),
                ToUtc(ToUtcFilter, includeDisplayedSecond: true),
                CancellationToken.None);
        }

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
            StockCardGrid.Rows.Clear();
            foreach (var row in _viewModel.Items)
            {
                StockCardGrid.Rows.Add(
                    row.PostedAtUtc.ToString("u"),
                    row.DocumentNumber,
                    row.DocumentType,
                    FormatCatalog(row.WarehouseCode, row.WarehouseName, row.WarehouseIsActive),
                    FormatCatalog(row.ProductCode, row.ProductName, row.ProductIsActive),
                    row.QuantityChange.ToString("0.0000"));
            }

            var totalPages = Math.Max(
                1L,
                (_viewModel.TotalCount + _viewModel.PageSize - 1) / _viewModel.PageSize);
            PageLabel.Text = "Page " + _viewModel.PageNumber + " of " + totalPages;
            TotalLabel.Text = _viewModel.VisibleQuantityChangeTotal.ToString("0.0000");
            BusyIndicator.Visible = _viewModel.IsLoading;
            StockCardGrid.Enabled = !_viewModel.IsLoading;
            RefreshButton.Enabled = !_viewModel.IsLoading;
            PreviousPageButton.Enabled = !_viewModel.IsLoading && _viewModel.PageNumber > 1;
            NextPageButton.Enabled = !_viewModel.IsLoading && _viewModel.PageNumber < totalPages;
            OperationStatusLabel.Text = !string.IsNullOrWhiteSpace(_viewModel.ErrorMessage)
                ? _viewModel.ErrorMessage
                : _viewModel.WarehouseId <= 0 || _viewModel.ProductId <= 0
                    ? "Select warehouse and product."
                    : _viewModel.IsEmpty
                        ? "No stock movements."
                        : "Ready";
        }

        private static DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                AccessibleName = "Stock card results",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            };
            grid.Columns.Add("postedAtUtc", "Posted UTC");
            grid.Columns.Add("documentNumber", "Document");
            grid.Columns.Add("documentType", "Type");
            grid.Columns.Add("warehouse", "Warehouse");
            grid.Columns.Add("product", "Product");
            grid.Columns.Add("quantityChange", "Quantity change");
            return grid;
        }

        private static NumericUpDown CreateIdFilter() =>
            new AccessibleIdFilter { Minimum = 0, Maximum = int.MaxValue, Width = 100 };

        private static DateTimePicker CreateDateFilter() =>
            new DateTimePicker
            {
                ShowCheckBox = true,
                Checked = false,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm:ss",
                Width = 150,
            };

        private static DateTime? ToUtc(
            DateTimePicker picker,
            bool includeDisplayedSecond = false)
        {
            if (!picker.Checked)
            {
                return null;
            }

            var value = picker.Value;
            var displayedSecond = new DateTime(
                value.Year,
                value.Month,
                value.Day,
                value.Hour,
                value.Minute,
                value.Second,
                DateTimeKind.Utc);
            return includeDisplayedSecond
                ? displayedSecond.AddSeconds(1).AddTicks(-1)
                : displayedSecond;
        }

        private static string FormatCatalog(string code, string name, bool isActive) =>
            code + " - " + name + (isActive ? string.Empty : " (inactive)");
    }
}
