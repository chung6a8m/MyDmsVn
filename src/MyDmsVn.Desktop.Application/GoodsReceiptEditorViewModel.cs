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
        private readonly IUiDispatcher _dispatcher;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();
        private long? _id;
        private string? _receiptNumber;
        private DateTime _receiptDate = DateTime.Today;
        private int? _warehouseId;
        private int? _employeeId;
        private string? _note;
        private string _status = "Draft";
        private string? _version;
        private int _saveInFlight;
        private bool _isApplyingReceipt;
        private bool _isDirty;
        private bool _disposed;

        public GoodsReceiptEditorViewModel(
            IGoodsReceiptApiClient apiClient,
            CatalogLookupViewModel warehouseLookup,
            CatalogLookupViewModel employeeLookup,
            GoodsReceiptProductOptionsViewModel productOptions,
            IUiDispatcher dispatcher,
            IDesktopNotificationService notifications,
            IDesktopExceptionReporter? exceptionReporter = null)
            : base(notifications, exceptionReporter)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            WarehouseLookup = warehouseLookup ?? throw new ArgumentNullException(nameof(warehouseLookup));
            EmployeeLookup = employeeLookup ?? throw new ArgumentNullException(nameof(employeeLookup));
            ProductOptions = productOptions ?? throw new ArgumentNullException(nameof(productOptions));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
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
            set
            {
                if (SetProperty(ref _receiptDate, value.Date))
                {
                    MarkDirty();
                }
            }
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
            set
            {
                if (SetProperty(ref _note, value))
                {
                    MarkDirty();
                }
            }
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
        public bool IsDirty
        {
            get => _isDirty;
            private set => SetProperty(ref _isDirty, value);
        }
        public decimal? Total
        {
            get
            {
                var total = 0m;
                foreach (var line in Lines)
                {
                    var lineTotal = line.LineTotal;
                    if (!lineTotal.HasValue)
                    {
                        return null;
                    }

                    try
                    {
                        total = checked(total + lineTotal.Value);
                    }
                    catch (OverflowException)
                    {
                        return null;
                    }
                }

                return total;
            }
        }

        public void Load(GoodsReceiptDto? receipt)
        {
            ThrowIfDisposed();
            _isApplyingReceipt = true;
            try
            {
                LoadCore(receipt);
            }
            finally
            {
                _isApplyingReceipt = false;
                IsDirty = false;
            }
        }

        private void LoadCore(GoodsReceiptDto? receipt)
        {
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
            var changed = WarehouseId != id;
            WarehouseId = id;
            WarehouseLookup.SetSelection(id, displayName, isAvailable);
            if (changed)
            {
                MarkDirty();
            }
        }

        public void SetEmployee(int id, string? displayName, bool isAvailable)
        {
            var changed = EmployeeId != id;
            EmployeeId = id;
            EmployeeLookup.SetSelection(id, displayName, isAvailable);
            if (changed)
            {
                MarkDirty();
            }
        }

        public GoodsReceiptLineViewModel AddLine()
        {
            ThrowIfReadOnly();
            var line = new GoodsReceiptLineViewModel();
            AddLineCore(line);
            ProductOptions.TrackLines(Lines);
            OnPropertyChanged(nameof(Total));
            MarkDirty();
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
                MarkDirty();
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
            ThrowIfDisposed();
            if (!Id.HasValue)
            {
                return false;
            }

            var lifetimeToken = _lifetimeCancellation.Token;
            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetimeToken))
            {
                ApiResponse<GoodsReceiptDto> response;
                try
                {
                    response = await _apiClient
                        .GetAsync(Id.Value, linkedCancellation.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (lifetimeToken.IsCancellationRequested)
                {
                    return false;
                }
                catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    if (lifetimeToken.IsCancellationRequested)
                    {
                        return false;
                    }

                    try
                    {
                        await _dispatcher.InvokeAsync(
                            () =>
                            {
                                if (!_disposed)
                                {
                                    ApplyUnexpectedError(exception);
                                }
                            },
                            lifetimeToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (lifetimeToken.IsCancellationRequested)
                    {
                    }
                    return false;
                }

                if (lifetimeToken.IsCancellationRequested)
                {
                    return false;
                }

                var applied = false;
                try
                {
                    await _dispatcher.InvokeAsync(
                        () =>
                        {
                            if (_disposed)
                            {
                                return;
                            }

                            if (!response.IsSuccess)
                            {
                                ApplyApiError(response.Error!);
                                return;
                            }

                            Load(response.Data);
                            applied = true;
                        },
                        linkedCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (lifetimeToken.IsCancellationRequested)
                {
                    return false;
                }

                return applied;
            }
        }

        public async Task<bool> SaveAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (Interlocked.CompareExchange(ref _saveInFlight, 1, 0) != 0)
            {
                return false;
            }

            var lifetimeToken = _lifetimeCancellation.Token;
            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetimeToken))
            {
                var saved = false;
                try
                {
                    await ExecuteBusyAsync(
                        async token => saved = await SaveCoreAsync(token, lifetimeToken).ConfigureAwait(false),
                        linkedCancellation.Token,
                        () => !_disposed).ConfigureAwait(false);
                    return saved;
                }
                finally
                {
                    Volatile.Write(ref _saveInFlight, 0);
                }
            }
        }

        private async Task<bool> SaveCoreAsync(
            CancellationToken cancellationToken,
            CancellationToken lifetimeToken)
        {
            if (IsReadOnly || !ValidateForSave())
            {
                return false;
            }

            var saveLines = Lines.Select(line => new SaveGoodsReceiptLineRequest(
                line.ProductId,
                line.Quantity,
                line.UnitCost)).ToArray();
            ApiResponse<GoodsReceiptDto> response;
            try
            {
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
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await _dispatcher.InvokeAsync(
                    () => ApplyUnexpectedError(exception),
                    lifetimeToken).ConfigureAwait(false);
                return false;
            }

            if (!response.IsSuccess)
            {
                await _dispatcher.InvokeAsync(
                    () => ApplyApiError(response.Error!),
                    cancellationToken).ConfigureAwait(false);
                return false;
            }

            var persisted = response.Data!;
            var identityApplied = false;
            await _dispatcher.InvokeAsync(
                () =>
                {
                    if (_disposed)
                    {
                        return;
                    }

                    ApplyPersistedIdentity(persisted);
                    IsDirty = false;
                    identityApplied = true;
                },
                lifetimeToken).ConfigureAwait(false);
            if (!identityApplied)
            {
                return false;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                await PublishSavedWithoutRefreshAsync(lifetimeToken).ConfigureAwait(false);
                return true;
            }

            ApiResponse<GoodsReceiptDto> refresh;
            try
            {
                refresh = await _apiClient
                    .GetAsync(persisted.Id, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await PublishSavedWithoutRefreshAsync(lifetimeToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception exception)
            {
                await _dispatcher.InvokeAsync(
                    () =>
                    {
                        ApplyUnexpectedError(exception);
                        PublishNotification(
                            DesktopNotificationKind.Warning,
                            "Goods receipt was saved, but its latest details could not be refreshed.");
                    },
                    lifetimeToken).ConfigureAwait(false);
                return true;
            }

            if (!refresh.IsSuccess)
            {
                await _dispatcher.InvokeAsync(
                    () =>
                    {
                        ApplyApiError(refresh.Error!);
                        PublishNotification(
                            DesktopNotificationKind.Warning,
                            "Goods receipt was saved, but its latest details could not be refreshed.");
                    },
                    lifetimeToken).ConfigureAwait(false);
                return true;
            }

            await _dispatcher.InvokeAsync(
                () =>
                {
                    Load(refresh.Data);
                    PublishNotification(DesktopNotificationKind.Success, "Goods receipt saved.");
                },
                lifetimeToken).ConfigureAwait(false);
            return true;
        }

        private Task PublishSavedWithoutRefreshAsync(CancellationToken cancellationToken) =>
            _dispatcher.InvokeAsync(
                () => PublishNotification(
                    DesktopNotificationKind.Warning,
                    "Goods receipt was saved, but its latest details were not refreshed because the operation was canceled."),
                cancellationToken);

        private void ApplyPersistedIdentity(GoodsReceiptDto receipt)
        {
            Id = receipt.Id;
            ReceiptNumber = receipt.ReceiptNumber;
            Status = receipt.Status;
            Version = receipt.Version;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetimeCancellation.Cancel();
            ClearLines();
            WarehouseLookup.Dispose();
            EmployeeLookup.Dispose();
            ProductOptions.Dispose();
            _lifetimeCancellation.Dispose();
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
                if (!line.LineTotal.HasValue)
                {
                    details.Add(new ApiErrorDetail(
                        "Validation.AmountOverflow",
                        "Line total exceeds the supported amount.",
                        $"lines[{index}].unitCost"));
                }
            }
            if (Lines.All(line => line.LineTotal.HasValue) && !Total.HasValue)
            {
                details.Add(new ApiErrorDetail(
                    "Validation.AmountOverflow",
                    "Receipt total exceeds the supported amount.",
                    "lines"));
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
            if (eventArgs.PropertyName == nameof(GoodsReceiptLineViewModel.ProductId) ||
                eventArgs.PropertyName == nameof(GoodsReceiptLineViewModel.Quantity) ||
                eventArgs.PropertyName == nameof(GoodsReceiptLineViewModel.UnitCost))
            {
                MarkDirty();
            }

            if (eventArgs.PropertyName == nameof(GoodsReceiptLineViewModel.LineTotal) ||
                eventArgs.PropertyName == nameof(GoodsReceiptLineViewModel.Quantity) ||
                eventArgs.PropertyName == nameof(GoodsReceiptLineViewModel.UnitCost))
            {
                OnPropertyChanged(nameof(Total));
            }
        }

        private void MarkDirty()
        {
            if (!_isApplyingReceipt)
            {
                IsDirty = true;
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
