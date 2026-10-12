using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public sealed class StockBalanceViewModel : DesktopViewModelBase, IDisposable
    {
        private readonly object _sync = new object();
        private readonly IInventoryApiClient _apiClient;
        private readonly IUiDispatcher _dispatcher;
        private readonly DebouncedAsyncAction _searchDebouncer;
        private IReadOnlyList<StockBalanceRow> _items = Array.Empty<StockBalanceRow>();
        private CancellationTokenSource? _activeLoad;
        private string? _search;
        private int? _warehouseId;
        private int? _productId;
        private int _pageNumber = 1;
        private int _pageSize = 25;
        private long _totalCount;
        private decimal _visibleQuantityTotal;
        private long _generation;
        private bool _isLoading;
        private bool _isAccessDenied;
        private bool _disposed;

        public StockBalanceViewModel(
            IInventoryApiClient apiClient,
            IUiDispatcher dispatcher,
            IDesktopNotificationService notifications,
            IAsyncDelay delay,
            TimeSpan? debounceInterval = null)
            : base(notifications)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _searchDebouncer = new DebouncedAsyncAction(
                delay ?? throw new ArgumentNullException(nameof(delay)),
                debounceInterval ?? CatalogLookupViewModel.DefaultDebounceInterval);
        }

        public IReadOnlyList<StockBalanceRow> Items
        {
            get => _items;
            private set
            {
                if (SetProperty(ref _items, value))
                {
                    IsEmpty = value.Count == 0;
                    VisibleQuantityTotal = value.Sum(row => row.Quantity);
                }
            }
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

        public decimal VisibleQuantityTotal
        {
            get => _visibleQuantityTotal;
            private set => SetProperty(ref _visibleQuantityTotal, value);
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

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _searchDebouncer.Cancel();
                return LoadAsync(cancellationToken);
            }
        }

        public Task SetSearch(string? search)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                CancelActiveLoadLocked();
                _search = search;
                PageNumber = 1;
                return _searchDebouncer.Schedule(LoadAsync);
            }
        }

        public Task SetWarehouseFilterAsync(int? warehouseId, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _searchDebouncer.Cancel();
                _warehouseId = warehouseId;
                PageNumber = 1;
                return LoadAsync(cancellationToken);
            }
        }

        public Task SetProductFilterAsync(int? productId, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _searchDebouncer.Cancel();
                _productId = productId;
                PageNumber = 1;
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
            _searchDebouncer.Dispose();
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

                var request = new StockBalanceQuery(
                    _warehouseId,
                    _productId,
                    _search,
                    PageNumber,
                    PageSize);
                var response = await _apiClient
                    .GetStockBalancesAsync(request, operation.Cancellation.Token)
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

        private void ApplyResponse(
            long generation,
            ApiResponse<PagedResult<StockBalanceRow>> response)
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
            if (IsAccessDenied)
            {
                Items = Array.Empty<StockBalanceRow>();
                TotalCount = 0;
            }

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
                throw new ObjectDisposedException(nameof(StockBalanceViewModel));
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
