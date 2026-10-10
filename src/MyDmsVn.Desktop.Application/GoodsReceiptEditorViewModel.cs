using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public sealed class GoodsReceiptEditorViewModel : DesktopViewModelBase, IDisposable
    {
        private readonly IGoodsReceiptApiClient _apiClient;
        private long? _id;
        private string? _receiptNumber;
        private DateTime _receiptDate = DateTime.Today;
        private int? _warehouseId;
        private int? _employeeId;
        private string? _note;
        private string _status = "Draft";
        private string? _version;
        private bool _disposed;

        public GoodsReceiptEditorViewModel(
            IGoodsReceiptApiClient apiClient,
            CatalogLookupViewModel warehouseLookup,
            CatalogLookupViewModel employeeLookup,
            GoodsReceiptProductOptionsViewModel productOptions,
            IDesktopNotificationService notifications)
            : base(notifications)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            WarehouseLookup = warehouseLookup ?? throw new ArgumentNullException(nameof(warehouseLookup));
            EmployeeLookup = employeeLookup ?? throw new ArgumentNullException(nameof(employeeLookup));
            ProductOptions = productOptions ?? throw new ArgumentNullException(nameof(productOptions));
        }

        public ObservableCollection<GoodsReceiptLineViewModel> Lines { get; } =
            new ObservableCollection<GoodsReceiptLineViewModel>();

        public CatalogLookupViewModel WarehouseLookup { get; }
        public CatalogLookupViewModel EmployeeLookup { get; }
        public GoodsReceiptProductOptionsViewModel ProductOptions { get; }

        public long? Id
        {
            get => _id;
            private set => SetProperty(ref _id, value);
        }

        public string? ReceiptNumber
        {
            get => _receiptNumber;
            private set => SetProperty(ref _receiptNumber, value);
        }

        public DateTime ReceiptDate
        {
            get => _receiptDate;
            set => SetProperty(ref _receiptDate, value.Date);
        }

        public int? WarehouseId
        {
            get => _warehouseId;
            private set => SetProperty(ref _warehouseId, value);
        }

        public int? EmployeeId
        {
            get => _employeeId;
            private set => SetProperty(ref _employeeId, value);
        }

        public string? Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
        }

        public string Status
        {
            get => _status;
            private set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(IsReadOnly));
                }
            }
        }

        public string? Version
        {
            get => _version;
            private set => SetProperty(ref _version, value);
        }

        public bool IsReadOnly => string.Equals(Status, "Posted", StringComparison.Ordinal);
        public decimal Total => Lines.Sum(line => line.LineTotal);

        public void Load(GoodsReceiptDto? receipt)
        {
            ThrowIfDisposed();
            ResetValidation();
            ClearLines();
            if (receipt == null)
            {
                Id = null;
                ReceiptNumber = null;
                ReceiptDate = DateTime.Today;
                WarehouseId = null;
                EmployeeId = null;
                Note = null;
                Status = "Draft";
                Version = null;
                WarehouseLookup.SetSelection(null, null, true);
                EmployeeLookup.SetSelection(null, null, true);
                ProductOptions.TrackLines(Lines);
                OnPropertyChanged(nameof(Total));
                return;
            }

            Id = receipt.Id;
            ReceiptNumber = receipt.ReceiptNumber;
            ReceiptDate = receipt.ReceiptDate;
            WarehouseId = receipt.WarehouseId;
            EmployeeId = receipt.EmployeeId;
            Note = receipt.Note;
            Status = receipt.Status;
            Version = receipt.Version;
            WarehouseLookup.SetSelection(
                receipt.WarehouseId,
                receipt.WarehouseDisplayName,
                receipt.WarehouseIsActive);
            EmployeeLookup.SetSelection(
                receipt.EmployeeId,
                receipt.EmployeeDisplayName,
                receipt.EmployeeIsActive);
            foreach (var item in receipt.Lines.OrderBy(line => line.LineNumber))
            {
                var line = new GoodsReceiptLineViewModel
                {
                    Id = item.Id,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost,
                };
                line.SetProduct(item.ProductId, item.ProductDisplayName, item.ProductIsActive);
                AddLineCore(line);
            }

            ProductOptions.TrackLines(Lines);

            OnPropertyChanged(nameof(Total));
        }

        public void SetWarehouse(int id, string? displayName, bool isAvailable)
        {
            WarehouseId = id;
            WarehouseLookup.SetSelection(id, displayName, isAvailable);
        }

        public void SetEmployee(int id, string? displayName, bool isAvailable)
        {
            EmployeeId = id;
            EmployeeLookup.SetSelection(id, displayName, isAvailable);
        }

        public GoodsReceiptLineViewModel AddLine()
        {
            ThrowIfReadOnly();
            var line = new GoodsReceiptLineViewModel();
            AddLineCore(line);
            ProductOptions.TrackLines(Lines);
            OnPropertyChanged(nameof(Total));
            return line;
        }

        public void RemoveLine(GoodsReceiptLineViewModel line)
        {
            ThrowIfReadOnly();
            if (Lines.Remove(line))
            {
                line.PropertyChanged -= OnLinePropertyChanged;
                ProductOptions.TrackLines(Lines);
                OnPropertyChanged(nameof(Total));
            }
        }

        public async Task ActivateAsync(CancellationToken cancellationToken)
        {
            await WarehouseLookup.ActivateAsync(cancellationToken).ConfigureAwait(false);
            await EmployeeLookup.ActivateAsync(cancellationToken).ConfigureAwait(false);
            await ProductOptions.ActivateAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
        {
            if (!Id.HasValue)
            {
                return false;
            }

            var response = await _apiClient.GetAsync(Id.Value, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess)
            {
                ApplyApiError(response.Error!);
                return false;
            }

            Load(response.Data);
            return true;
        }

        public async Task<bool> SaveAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (IsReadOnly || !ValidateForSave())
            {
                return false;
            }

            ApiResponse<GoodsReceiptDto> response;
            var saveLines = Lines.Select(line => new SaveGoodsReceiptLineRequest(
                line.ProductId,
                line.Quantity,
                line.UnitCost)).ToArray();
            if (Id.HasValue)
            {
                response = await _apiClient.UpdateDraftAsync(
                    new UpdateGoodsReceiptDraftRequest(
                        Id.Value,
                        Version!,
                        ReceiptDate,
                        WarehouseId!.Value,
                        EmployeeId!.Value,
                        Note,
                        saveLines),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                response = await _apiClient.CreateDraftAsync(
                    new SaveGoodsReceiptRequest(
                        ReceiptDate,
                        WarehouseId!.Value,
                        EmployeeId!.Value,
                        Note,
                        saveLines),
                    cancellationToken).ConfigureAwait(false);
            }

            if (!response.IsSuccess)
            {
                ApplyApiError(response.Error!);
                return false;
            }

            var refresh = await _apiClient
                .GetAsync(response.Data!.Id, cancellationToken)
                .ConfigureAwait(false);
            if (!refresh.IsSuccess)
            {
                ApplyApiError(refresh.Error!);
                return false;
            }

            Load(refresh.Data);
            PublishNotification(DesktopNotificationKind.Success, "Goods receipt saved.");
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ClearLines();
            WarehouseLookup.Dispose();
            EmployeeLookup.Dispose();
            ProductOptions.Dispose();
        }

        private bool ValidateForSave()
        {
            var details = new List<ApiErrorDetail>();
            if (ReceiptDate == default(DateTime))
            {
                details.Add(new ApiErrorDetail("Validation.Required", "Receipt date is required.", "receiptDate"));
            }
            if (!WarehouseId.HasValue || WarehouseId.Value <= 0)
            {
                details.Add(new ApiErrorDetail("Validation.Required", "Warehouse is required.", "warehouseId"));
            }
            if (!EmployeeId.HasValue || EmployeeId.Value <= 0)
            {
                details.Add(new ApiErrorDetail("Validation.Required", "Employee is required.", "employeeId"));
            }
            if (Lines.Count == 0)
            {
                details.Add(new ApiErrorDetail("Validation.Required", "At least one line is required.", "lines"));
            }
            for (var index = 0; index < Lines.Count; index++)
            {
                var line = Lines[index];
                if (line.ProductId <= 0)
                {
                    details.Add(new ApiErrorDetail("Validation.Required", "Product is required.", $"lines[{index}].productId"));
                }
                if (line.Quantity <= 0)
                {
                    details.Add(new ApiErrorDetail("Validation.GreaterThan", "Quantity must be greater than zero.", $"lines[{index}].quantity"));
                }
                if (line.UnitCost < 0)
                {
                    details.Add(new ApiErrorDetail("Validation.GreaterThanOrEqual", "Unit cost cannot be negative.", $"lines[{index}].unitCost"));
                }
            }

            if (details.Count == 0)
            {
                ResetValidation();
                return true;
            }

            ApplyApiError(new ApiError(
                ApiStatusCode.BadRequest,
                "ValidationError",
                "The request is invalid.",
                details));
            return false;
        }

        private void AddLineCore(GoodsReceiptLineViewModel line)
        {
            line.PropertyChanged += OnLinePropertyChanged;
            Lines.Add(line);
        }

        private void ClearLines()
        {
            foreach (var line in Lines)
            {
                line.PropertyChanged -= OnLinePropertyChanged;
            }
            Lines.Clear();
        }

        private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName == nameof(GoodsReceiptLineViewModel.LineTotal) ||
                eventArgs.PropertyName == nameof(GoodsReceiptLineViewModel.Quantity) ||
                eventArgs.PropertyName == nameof(GoodsReceiptLineViewModel.UnitCost))
            {
                OnPropertyChanged(nameof(Total));
            }
        }

        private void ThrowIfReadOnly()
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Posted goods receipts are read-only.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GoodsReceiptEditorViewModel));
            }
        }
    }
}
