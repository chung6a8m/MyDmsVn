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
        private int _postInFlight;
        private bool _isApplyingReceipt;
        private bool _isDirty;
        private bool _requiresPostOutcomeReload;
        private long? _uncertainPostReceiptId;
        private bool _disposed;
        private long _documentGeneration;
        private Guid _editorIdentity = Guid.NewGuid();
        private long _editRevision;
        private WorkspaceSaveStatus _lastSaveStatus = WorkspaceSaveStatus.CanceledBeforeMutation;
        private bool _lastSaveRefreshFailed;
        private bool _preserveCloseIdentityForSaveRefresh;

        public Guid EditorIdentity => _editorIdentity;
        public long EditRevision => Interlocked.Read(ref _editRevision);
        public bool IsMutationBusy => Volatile.Read(ref _saveInFlight) != 0 || Volatile.Read(ref _postInFlight) != 0;

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
        public bool RequiresPostOutcomeReload
        {
            get => _requiresPostOutcomeReload;
            private set => SetProperty(ref _requiresPostOutcomeReload, value);
        }
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
            if (!_preserveCloseIdentityForSaveRefresh)
            {
                _editorIdentity = Guid.NewGuid();
                Interlocked.Increment(ref _editRevision);
            }
            Interlocked.Increment(ref _documentGeneration);
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
            var document = new DocumentDispatch(this);
            ThrowIfDisposed();
            var receiptId = _uncertainPostReceiptId ?? Id;
            if (!receiptId.HasValue)
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
                        .GetAsync(receiptId.Value, linkedCancellation.Token)
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
                        await document.InvokeAsync(
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

                if (lifetimeToken.IsCancellationRequested || !document.IsCurrent)
                {
                    return false;
                }

                var applied = false;
                try
                {
                    await document.InvokeAsync(
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
                            if (response.Data!.Id == _uncertainPostReceiptId)
                            {
                                _uncertainPostReceiptId = null;
                                RequiresPostOutcomeReload = false;
                            }
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
            if (RequiresPostOutcomeReload)
            {
                return false;
            }
            if (Interlocked.CompareExchange(ref _saveInFlight, 1, 0) != 0)
            {
                return false;
            }
            _lastSaveStatus = WorkspaceSaveStatus.CanceledBeforeMutation;
            _lastSaveRefreshFailed = false;

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

        public async Task<WorkspaceSaveOutcome> SaveForCloseAsync(CancellationToken cancellationToken)
        {
            var identity = EditorIdentity;
            var revision = EditRevision;
            try { await SaveAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            return new WorkspaceSaveOutcome(_lastSaveStatus, identity, revision, _lastSaveRefreshFailed);
        }

        public async Task<bool> PostAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (RequiresPostOutcomeReload)
            {
                return false;
            }
            if (Interlocked.CompareExchange(ref _postInFlight, 1, 0) != 0)
            {
                return false;
            }

            var lifetimeToken = _lifetimeCancellation.Token;
            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetimeToken))
            {
                var posted = false;
                try
                {
                    await ExecuteBusyAsync(
                        async token => posted = await PostCoreAsync(token, lifetimeToken).ConfigureAwait(false),
                        linkedCancellation.Token,
                        () => !_disposed).ConfigureAwait(false);
                    return posted;
                }
                finally
                {
                    Volatile.Write(ref _postInFlight, 0);
                }
            }
        }

        private async Task<bool> PostCoreAsync(
            CancellationToken cancellationToken,
            CancellationToken lifetimeToken)
        {
            var document = new DocumentDispatch(this);
            if (!Id.HasValue ||
                string.IsNullOrWhiteSpace(Version) ||
                IsReadOnly ||
                IsDirty)
            {
                return false;
            }

            var receiptId = Id.Value;
            ApiResponse<PostGoodsReceiptResponse> response;
            try
            {
                response = await _apiClient.PostAsync(
                    new PostGoodsReceiptRequest(receiptId, Version!),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (!lifetimeToken.IsCancellationRequested)
                {
                    await document.InvokeAsync(
                        () => RequirePostOutcomeReload(receiptId),
                        lifetimeToken).ConfigureAwait(false);
                }
                throw;
            }
            catch (Exception exception)
            {
                await document.InvokeAsync(
                    () =>
                    {
                        RequirePostOutcomeReload(receiptId);
                        ApplyUnexpectedError(exception,
                            "The posting outcome could not be confirmed. Reload the receipt before making further changes.");
                    },
                    lifetimeToken).ConfigureAwait(false);
                return false;
            }

            if (!document.IsCurrent) return false;
            if (!response.IsSuccess)
            {
                await document.InvokeAsync(
                    () =>
                    {
                        var error = response.Error!;
                        if (error.Status == ApiStatusCode.InternalServerError ||
                            error.Status == ApiStatusCode.Unspecified)
                        {
                            RequirePostOutcomeReload(receiptId);
                        }
                        ApplyApiError(error);
                    },
                    lifetimeToken).ConfigureAwait(false);
                return false;
            }

            await document.InvokeAsync(
                () =>
                {
                    Status = response.Data!.Status;
                    IsDirty = false;
                },
                lifetimeToken).ConfigureAwait(false);

            if (!document.IsCurrent) return false;
            if (cancellationToken.IsCancellationRequested)
            {
                await document.InvokeAsync(() => PublishPostedWithoutRefresh(), lifetimeToken).ConfigureAwait(false);
                return true;
            }

            ApiResponse<GoodsReceiptDto> refresh;
            try
            {
                refresh = await _apiClient
                    .GetAsync(receiptId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await document.InvokeAsync(() => PublishPostedWithoutRefresh(), lifetimeToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception exception)
            {
                await document.InvokeAsync(
                    () =>
                    {
                        ApplyUnexpectedError(exception);
                        PublishNotification(
                            DesktopNotificationKind.Warning,
                            "Goods receipt was posted, but its latest details could not be refreshed.");
                    },
                    lifetimeToken).ConfigureAwait(false);
                return true;
            }

            if (!document.IsCurrent) return false;
            if (!refresh.IsSuccess)
            {
                await document.InvokeAsync(
                    () =>
                    {
                        ApplyApiError(refresh.Error!);
                        PublishNotification(
                            DesktopNotificationKind.Warning,
                            "Goods receipt was posted, but its latest details could not be refreshed.");
                    },
                    lifetimeToken).ConfigureAwait(false);
                return true;
            }

            var loaded = false;
            await document.InvokeAsync(
                () =>
                {
                    Load(refresh.Data);
                    loaded = true;
                    PublishNotification(DesktopNotificationKind.Success, "Goods receipt posted.");
                },
                lifetimeToken).ConfigureAwait(false);
            return loaded;
        }

        private void PublishPostedWithoutRefresh() =>
            PublishNotification(DesktopNotificationKind.Warning,
                "Goods receipt was posted, but its latest details were not refreshed because the operation was canceled.");

        private void RequirePostOutcomeReload(long receiptId)
        {
            _uncertainPostReceiptId = receiptId;
            RequiresPostOutcomeReload = true;
        }

        private async Task<bool> SaveCoreAsync(
            CancellationToken cancellationToken,
            CancellationToken lifetimeToken)
        {
            var document = new DocumentDispatch(this);
            if (IsReadOnly || !ValidateForSave())
            {
                _lastSaveStatus = WorkspaceSaveStatus.Rejected;
                return false;
            }

            var saveLines = Lines.Select(line => new SaveGoodsReceiptLineRequest(
                line.ProductId,
                line.Quantity,
                line.UnitCost)).ToArray();
            var submittedRevision = EditRevision;
            ApiResponse<GoodsReceiptDto> response;
            try
            {
                _lastSaveStatus = WorkspaceSaveStatus.Unknown;
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
                await document.InvokeAsync(
                    () => ApplyUnexpectedError(exception),
                    lifetimeToken).ConfigureAwait(false);
                return false;
            }

            if (!document.IsCurrent) return false;
            if (!response.IsSuccess)
            {
                _lastSaveStatus = response.Error!.Status == ApiStatusCode.InternalServerError
                    ? WorkspaceSaveStatus.Unknown : WorkspaceSaveStatus.Rejected;
                await document.InvokeAsync(
                    () => ApplyApiError(response.Error!),
                    cancellationToken).ConfigureAwait(false);
                return false;
            }

            var persisted = response.Data!;
            var identityApplied = false;
            await document.InvokeAsync(
                () =>
                {
                    if (_disposed)
                    {
                        return;
                    }

                    ApplyPersistedIdentity(persisted);
                    IsDirty = EditRevision != submittedRevision;
                    _lastSaveStatus = WorkspaceSaveStatus.Saved;
                    identityApplied = true;
                },
                lifetimeToken).ConfigureAwait(false);
            if (!identityApplied)
            {
                return false;
            }

            if (IsDirty)
            {
                _lastSaveRefreshFailed = true;
                await document.InvokeAsync(() => PublishNotification(DesktopNotificationKind.Warning,
                    "Goods receipt was saved. Newer edits remain unsaved; refresh was deferred."), lifetimeToken).ConfigureAwait(false);
                return true;
            }

            if (!document.IsCurrent) return false;
            if (cancellationToken.IsCancellationRequested)
            {
                _lastSaveRefreshFailed = true;
                await document.InvokeAsync(() => PublishSavedWithoutRefresh(), lifetimeToken).ConfigureAwait(false);
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
                _lastSaveRefreshFailed = true;
                await document.InvokeAsync(() => PublishSavedWithoutRefresh(), lifetimeToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception exception)
            {
                _lastSaveRefreshFailed = true;
                await document.InvokeAsync(
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

            if (!document.IsCurrent) return false;
            if (!refresh.IsSuccess)
            {
                _lastSaveRefreshFailed = true;
                await document.InvokeAsync(
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

            var loaded = false;
            await document.InvokeAsync(
                () =>
                {
                    _preserveCloseIdentityForSaveRefresh = true;
                    try { Load(refresh.Data); }
                    finally { _preserveCloseIdentityForSaveRefresh = false; }
                    loaded = true;
                    PublishNotification(DesktopNotificationKind.Success, "Goods receipt saved.");
                },
                lifetimeToken).ConfigureAwait(false);
            return loaded;
        }

        private void PublishSavedWithoutRefresh() =>
            PublishNotification(DesktopNotificationKind.Warning,
                "Goods receipt was saved, but its latest details were not refreshed because the operation was canceled.");

        // A caller may deliberately replace the document while a provider ignores cancellation.
        // Recheck inside the dispatcher, not only before queueing work on the UI thread.
        private sealed class DocumentDispatch
        {
            private readonly GoodsReceiptEditorViewModel _owner;
            private readonly long _generation;
            internal DocumentDispatch(GoodsReceiptEditorViewModel owner)
            {
                _owner = owner;
                _generation = Interlocked.Read(ref owner._documentGeneration);
            }
            internal bool IsCurrent => !_owner._disposed && _generation == Interlocked.Read(ref _owner._documentGeneration);
            internal Task InvokeAsync(Action action, CancellationToken token) =>
                _owner._dispatcher.InvokeAsync(() => { if (IsCurrent) action(); }, token);
        }

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
                Interlocked.Increment(ref _editRevision);
                IsDirty = true;
            }
        }

        private void ThrowIfReadOnly()
        {
            if (RequiresPostOutcomeReload)
            {
                throw new InvalidOperationException("Reload the receipt to confirm the posting outcome before making further changes.");
            }
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
