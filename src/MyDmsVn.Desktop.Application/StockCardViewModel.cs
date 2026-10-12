using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public sealed class StockCardViewModel : DesktopViewModelBase, IDisposable
    {
        private readonly object _sync = new object();
        private readonly IInventoryApiClient _apiClient;
        private readonly IUiDispatcher _dispatcher;
        private IReadOnlyList<StockCardRow> _items = Array.Empty<StockCardRow>();
        private CancellationTokenSource? _activeLoad;
        private int _warehouseId;
        private int _productId;
        private DateTime? _fromUtc;
        private DateTime? _toUtc;
        private int _pageNumber = 1;
        private int _pageSize = 25;
        private long _totalCount;
        private decimal _visibleQuantityChangeTotal;
        private long _generation;
        private bool _isLoading;
        private bool _isAccessDenied;
        private bool _disposed;

        public StockCardViewModel(
            IInventoryApiClient apiClient,
            IUiDispatcher dispatcher,
            IDesktopNotificationService notifications)
            : base(notifications)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public IReadOnlyList<StockCardRow> Items
        {
            get => _items;
            private set
            {
                if (SetProperty(ref _items, value))
                {
                    IsEmpty = value.Count == 0;
                    VisibleQuantityChangeTotal = value.Sum(row => row.QuantityChange);
                }
            }
        }

        public int WarehouseId
        {
            get => _warehouseId;
            private set => SetProperty(ref _warehouseId, value);
        }

        public int ProductId
        {
            get => _productId;
            private set => SetProperty(ref _productId, value);
        }

        public DateTime? FromUtc
        {
            get => _fromUtc;
            private set => SetProperty(ref _fromUtc, value);
        }

        public DateTime? ToUtc
        {
            get => _toUtc;
            private set => SetProperty(ref _toUtc, value);
        }

        public int PageNumber
        {
            get => _pageNumber;
            set => SetProperty(ref _pageNumber, value);
        }

        public int PageSize
        {
            get => _pageSize;
            set => SetProperty(ref _pageSize, value);
        }

        public long TotalCount
        {
            get => _totalCount;
            private set => SetProperty(ref _totalCount, value);
        }

        public decimal VisibleQuantityChangeTotal
        {
            get => _visibleQuantityChangeTotal;
            private set => SetProperty(ref _visibleQuantityChangeTotal, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public bool IsAccessDenied
        {
            get => _isAccessDenied;
            private set => SetProperty(ref _isAccessDenied, value);
        }

        public Task ActivateAsync(CancellationToken cancellationToken) =>
            RefreshAsync(cancellationToken);

        public Task SetSelectionAsync(
            int warehouseId,
            int productId,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                return SetFiltersLocked(
                    warehouseId,
                    productId,
                    FromUtc,
                    ToUtc,
                    cancellationToken);
            }
        }

        public Task SetFiltersAsync(
            int warehouseId,
            int productId,
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                return SetFiltersLocked(
                    warehouseId,
                    productId,
                    fromUtc,
                    toUtc,
                    cancellationToken);
            }
        }

        public Task SetDateRangeAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                return SetFiltersLocked(
                    WarehouseId,
                    ProductId,
                    fromUtc,
                    toUtc,
                    cancellationToken);
            }
        }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                if (WarehouseId <= 0 || ProductId <= 0)
                {
                    return ClearWithoutQueryLocked();
                }

                return LoadAsync(cancellationToken);
            }
        }

        public void Dispose()
        {
            CancellationTokenSource? active;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _generation++;
                active = _activeLoad;
                _activeLoad = null;
            }

            active?.Cancel();
            active?.Dispose();
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            var operation = BeginLoad(cancellationToken);
            try
            {
                await SetLoadingAsync(operation).ConfigureAwait(false);
                if (!IsCurrent(operation.Generation))
                {
                    return;
                }

                var request = new StockCardQuery(
                    WarehouseId,
                    ProductId,
                    FromUtc,
                    ToUtc,
                    PageNumber,
                    PageSize);
                var response = await _apiClient
                    .GetStockCardAsync(request, operation.Cancellation.Token)
                    .ConfigureAwait(false);
                if (!IsCurrent(operation.Generation))
                {
                    return;
                }

                await _dispatcher.InvokeAsync(
                    () => ApplyResponse(operation.Generation, response),
                    operation.Cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (IsCurrent(operation.Generation))
                {
                    await _dispatcher.InvokeAsync(
                        () =>
                        {
                            if (IsCurrent(operation.Generation))
                            {
                                ApplyUnexpectedError(exception);
                            }
                        },
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                await EndLoadAsync(operation).ConfigureAwait(false);
            }
        }

        private Task SetFiltersLocked(
            int warehouseId,
            int productId,
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken cancellationToken)
        {
            WarehouseId = warehouseId;
            ProductId = productId;
            FromUtc = fromUtc;
            ToUtc = toUtc;
            PageNumber = 1;
            return warehouseId <= 0 || productId <= 0
                ? ClearWithoutQueryLocked()
                : LoadAsync(cancellationToken);
        }

        private Task ClearWithoutQueryLocked()
        {
            CancelActiveLoadLocked();
            ResetValidation();
            IsAccessDenied = false;
            Items = Array.Empty<StockCardRow>();
            TotalCount = 0;
            return _dispatcher.InvokeAsync(
                () => IsLoading = false,
                CancellationToken.None);
        }

        private void ApplyResponse(
            long generation,
            ApiResponse<PagedResult<StockCardRow>> response)
        {
            if (!IsCurrent(generation))
            {
                return;
            }

            if (response.IsSuccess)
            {
                ResetValidation();
                IsAccessDenied = false;
                Items = response.Data!.Items;
                TotalCount = response.Data.TotalCount;
                return;
            }

            IsAccessDenied = IsAuthorizationFailure(response.Error!);
            Items = Array.Empty<StockCardRow>();
            TotalCount = 0;
            ApplyApiError(response.Error!);
        }

        private LoadOperation BeginLoad(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                CancelActiveLoadLocked();
                var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeLoad = cancellation;
                return new LoadOperation(++_generation, cancellation);
            }
        }

        private Task SetLoadingAsync(LoadOperation operation) =>
            _dispatcher.InvokeAsync(
                () =>
                {
                    if (IsCurrent(operation.Generation))
                    {
                        IsLoading = true;
                    }
                },
                operation.Cancellation.Token);

        private async Task EndLoadAsync(LoadOperation operation)
        {
            var isCompletedOperation = false;
            lock (_sync)
            {
                if (ReferenceEquals(_activeLoad, operation.Cancellation))
                {
                    _activeLoad = null;
                    isCompletedOperation = true;
                }
            }

            try
            {
                if (isCompletedOperation)
                {
                    await _dispatcher.InvokeAsync(
                        () =>
                        {
                            if (IsCurrent(operation.Generation))
                            {
                                IsLoading = false;
                            }
                        },
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                operation.Cancellation.Dispose();
            }
        }

        private bool IsCurrent(long generation)
        {
            lock (_sync)
            {
                return !_disposed && generation == _generation;
            }
        }

        private void CancelActiveLoadLocked()
        {
            _generation++;
            _activeLoad?.Cancel();
            _activeLoad = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(StockCardViewModel));
            }
        }

        private static bool IsAuthorizationFailure(ApiError error) =>
            error.Status == ApiStatusCode.Unauthorized ||
            error.Status == ApiStatusCode.Forbidden;

        private sealed class LoadOperation
        {
            public LoadOperation(long generation, CancellationTokenSource cancellation)
            {
                Generation = generation;
                Cancellation = cancellation;
            }

            public long Generation { get; }
            public CancellationTokenSource Cancellation { get; }
        }
    }
}
