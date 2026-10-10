using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public sealed class GoodsReceiptListViewModel : DesktopViewModelBase, IDisposable
    {
        private readonly object _sync = new object();
        private readonly IGoodsReceiptApiClient _apiClient;
        private readonly IUiDispatcher _dispatcher;
        private readonly DebouncedAsyncAction _searchDebouncer;
        private IReadOnlyList<GoodsReceiptDto> _items = Array.Empty<GoodsReceiptDto>();
        private GoodsReceiptDto? _selectedReceipt;
        private CancellationTokenSource? _activeLoad;
        private string? _search;
        private int? _warehouseId;
        private string? _status;
        private int _pageNumber = 1;
        private int _pageSize = 25;
        private long _totalCount;
        private long _generation;
        private bool _isLoading;
        private bool _isAccessDenied;
        private bool _disposed;

        public GoodsReceiptListViewModel(
            IGoodsReceiptApiClient apiClient,
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

        public IReadOnlyList<GoodsReceiptDto> Items
        {
            get => _items;
            private set
            {
                if (SetProperty(ref _items, value))
                {
                    IsEmpty = value.Count == 0;
                }
            }
        }

        public GoodsReceiptDto? SelectedReceipt
        {
            get => _selectedReceipt;
            private set => SetProperty(ref _selectedReceipt, value);
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
                return LoadListAsync(cancellationToken);
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
                return _searchDebouncer.Schedule(LoadListAsync);
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
                return LoadListAsync(cancellationToken);
            }
        }

        public Task SetStatusFilterAsync(string? status, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _searchDebouncer.Cancel();
                _status = status;
                PageNumber = 1;
                return LoadListAsync(cancellationToken);
            }
        }

        public Task<GoodsReceiptDto?> SelectAsync(long id, CancellationToken cancellationToken) =>
            LoadDetailAsync(id, cancellationToken);

        public void NewDraft() => SelectedReceipt = null;

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

        private async Task LoadListAsync(CancellationToken cancellationToken)
        {
            var operation = BeginLoad(cancellationToken);
            try
            {
                var request = new GoodsReceiptListRequest(
                    PageNumber,
                    PageSize,
                    _warehouseId,
                    _status,
                    _search);
                var response = await _apiClient
                    .ListAsync(request, operation.Cancellation.Token)
                    .ConfigureAwait(false);
                if (!IsCurrent(operation.Generation))
                {
                    return;
                }

                await _dispatcher.InvokeAsync(
                    () =>
                    {
                        if (!IsCurrent(operation.Generation))
                        {
                            return;
                        }

                        if (response.IsSuccess)
                        {
                            ResetValidation();
                            IsAccessDenied = false;
                            Items = response.Data!.Items;
                            TotalCount = response.Data.TotalCount;
                        }
                        else
                        {
                            IsAccessDenied = IsAuthorizationFailure(response.Error!);
                            if (IsAccessDenied)
                            {
                                Items = Array.Empty<GoodsReceiptDto>();
                                TotalCount = 0;
                                SelectedReceipt = null;
                            }
                            ApplyApiError(response.Error!);
                        }
                    },
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
                EndLoad(operation);
            }
        }

        private async Task<GoodsReceiptDto?> LoadDetailAsync(long id, CancellationToken cancellationToken)
        {
            var operation = BeginLoad(cancellationToken);
            try
            {
                var response = await _apiClient
                    .GetAsync(id, operation.Cancellation.Token)
                    .ConfigureAwait(false);
                if (!IsCurrent(operation.Generation))
                {
                    return null;
                }

                GoodsReceiptDto? selected = null;
                await _dispatcher.InvokeAsync(
                    () =>
                    {
                        if (IsCurrent(operation.Generation))
                        {
                            if (response.IsSuccess)
                            {
                                ResetValidation();
                                IsAccessDenied = false;
                                SelectedReceipt = response.Data;
                                selected = response.Data;
                            }
                            else
                            {
                                IsAccessDenied = IsAuthorizationFailure(response.Error!);
                                if (IsAccessDenied)
                                {
                                    SelectedReceipt = null;
                                }
                                ApplyApiError(response.Error!);
                            }
                        }
                    },
                    operation.Cancellation.Token).ConfigureAwait(false);
                return selected;
            }
            catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested)
            {
                return null;
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

                return null;
            }
            finally
            {
                EndLoad(operation);
            }
        }

        private LoadOperation BeginLoad(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                CancelActiveLoadLocked();
                var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeLoad = cancellation;
                var operation = new LoadOperation(++_generation, cancellation);
                IsLoading = true;
                return operation;
            }
        }

        private void EndLoad(LoadOperation operation)
        {
            lock (_sync)
            {
                if (ReferenceEquals(_activeLoad, operation.Cancellation))
                {
                    _activeLoad = null;
                    IsLoading = false;
                }
            }

            operation.Cancellation.Dispose();
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
                throw new ObjectDisposedException(nameof(GoodsReceiptListViewModel));
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
