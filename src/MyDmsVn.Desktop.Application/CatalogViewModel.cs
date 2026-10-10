using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public abstract class CatalogViewModel<TDto> : DesktopViewModelBase, IDisposable
    {
        public static readonly TimeSpan DefaultDebounceInterval = TimeSpan.FromMilliseconds(300);

        private readonly object _sync = new object();
        private readonly IUiDispatcher _dispatcher;
        private readonly DebouncedAsyncAction _searchReload;
        private IReadOnlyList<TDto> _items = Array.Empty<TDto>();
        private CancellationTokenSource? _activeLoad;
        private string? _search;
        private int _pageNumber = 1;
        private readonly int _pageSize = 25;
        private long _totalCount;
        private int? _selectedId;
        private bool _isLoading;
        private bool _disposed;
        private long _generation;

        protected CatalogViewModel(
            CatalogKind kind,
            IMessenger messenger,
            IUiDispatcher dispatcher,
            IDesktopNotificationService notifications,
            IAsyncDelay delay,
            TimeSpan? debounceInterval = null)
            : base(notifications)
        {
            Kind = kind;
            Messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _searchReload = new DebouncedAsyncAction(
                delay ?? throw new ArgumentNullException(nameof(delay)),
                debounceInterval ?? DefaultDebounceInterval);
        }

        public CatalogKind Kind { get; }

        protected IMessenger Messenger { get; }

        public IReadOnlyList<TDto> Items
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

        public string? Search => _search;

        public int PageNumber
        {
            get => _pageNumber;
            private set => SetProperty(ref _pageNumber, value);
        }

        public int PageSize => _pageSize;

        public long TotalCount
        {
            get => _totalCount;
            private set => SetProperty(ref _totalCount, value);
        }

        public int? SelectedId
        {
            get => _selectedId;
            protected set => SetProperty(ref _selectedId, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public Task ActivateAsync(CancellationToken cancellationToken) =>
            RefreshAsync(cancellationToken);

        public Task SetSearch(string? search)
        {
            ThrowIfDisposed();
            _search = search;
            OnPropertyChanged(nameof(Search));
            PageNumber = 1;
            return _searchReload.Schedule(LoadAsync);
        }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            _searchReload.Cancel();
            return LoadAsync(cancellationToken);
        }

        public Task MoveToPageAsync(int pageNumber, CancellationToken cancellationToken)
        {
            if (pageNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            PageNumber = pageNumber;
            return RefreshAsync(cancellationToken);
        }

        public void BeginCreate()
        {
            ThrowIfDisposed();
            SelectedId = null;
            ClearEditor();
        }

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return ExecuteBusyAsync(SaveCoreAsync, cancellationToken);
        }

        public Task SetActiveAsync(
            int id,
            bool isActive,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return ExecuteBusyAsync(
                token => SetActiveCoreAndRefreshAsync(id, isActive, token),
                cancellationToken);
        }

        public async Task SelectAsync(int id, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var response = await GetAsync(id, cancellationToken).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(
                () =>
                {
                    if (response.IsSuccess)
                    {
                        SelectedId = GetId(response.Data!);
                        PopulateEditor(response.Data!);
                    }
                    else
                    {
                        ApplyApiError(response.Error!);
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            CancellationTokenSource? activeLoad;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _generation++;
                activeLoad = _activeLoad;
                _activeLoad = null;
            }

            activeLoad?.Cancel();
            _searchReload.Dispose();
        }

        protected abstract Task<ApiResponse<PagedResult<TDto>>> ListAsync(
            CatalogListRequest request,
            CancellationToken cancellationToken);

        protected abstract Task<ApiResponse<TDto>> GetAsync(
            int id,
            CancellationToken cancellationToken);

        protected abstract Task<ApiResponse<TDto>> CreateAsync(
            CancellationToken cancellationToken);

        protected abstract Task<ApiResponse<TDto>> UpdateAsync(
            int id,
            CancellationToken cancellationToken);

        protected abstract Task<ApiResponse<UnitResponse>> SetActiveCoreAsync(
            int id,
            bool isActive,
            CancellationToken cancellationToken);

        protected abstract int GetId(TDto item);

        protected abstract void PopulateEditor(TDto item);

        protected abstract void ClearEditor();

        private async Task SaveCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var operation = SelectedId.HasValue
                    ? CatalogChangeOperation.Updated
                    : CatalogChangeOperation.Created;
                var response = SelectedId.HasValue
                    ? await UpdateAsync(SelectedId.Value, cancellationToken).ConfigureAwait(false)
                    : await CreateAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccess)
                {
                    await _dispatcher.InvokeAsync(
                        () => ApplyApiError(response.Error!),
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                var entityId = GetId(response.Data!);
                await _dispatcher.InvokeAsync(
                    () =>
                    {
                        SelectedId = entityId;
                        PopulateEditor(response.Data!);
                    },
                    cancellationToken).ConfigureAwait(false);
                Messenger.Send(new CatalogChangedMessage(Kind, entityId, operation));
                await LoadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await _dispatcher.InvokeAsync(
                    () => PublishNotification(DesktopNotificationKind.Error, exception.Message),
                    CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task SetActiveCoreAndRefreshAsync(
            int id,
            bool isActive,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await SetActiveCoreAsync(id, isActive, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccess)
                {
                    await _dispatcher.InvokeAsync(
                        () => ApplyApiError(response.Error!),
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                Messenger.Send(
                    new CatalogChangedMessage(
                        Kind,
                        id,
                        CatalogChangeOperation.ActiveStatusChanged));
                await LoadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await _dispatcher.InvokeAsync(
                    () => PublishNotification(DesktopNotificationKind.Error, exception.Message),
                    CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource loadCancellation;
            long generation;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _activeLoad?.Cancel();
                loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeLoad = loadCancellation;
                generation = ++_generation;
            }

            try
            {
                await _dispatcher.InvokeAsync(
                    () => IsLoading = true,
                    loadCancellation.Token).ConfigureAwait(false);
                var response = await ListAsync(
                    new CatalogListRequest(_search, PageNumber, PageSize, includeInactive: true),
                    loadCancellation.Token).ConfigureAwait(false);
                if (!IsCurrent(generation))
                {
                    return;
                }

                await _dispatcher.InvokeAsync(
                    () => ApplyListResponse(response, generation),
                    loadCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (loadCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (IsCurrent(generation))
                {
                    await _dispatcher.InvokeAsync(
                        () => PublishNotification(DesktopNotificationKind.Error, exception.Message),
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                if (IsCurrent(generation))
                {
                    await _dispatcher.InvokeAsync(
                        () => IsLoading = false,
                        CancellationToken.None).ConfigureAwait(false);
                }

                lock (_sync)
                {
                    if (ReferenceEquals(_activeLoad, loadCancellation))
                    {
                        _activeLoad = null;
                    }
                }

                loadCancellation.Dispose();
            }
        }

        private void ApplyListResponse(ApiResponse<PagedResult<TDto>> response, long generation)
        {
            if (!IsCurrent(generation))
            {
                return;
            }

            if (response.IsSuccess)
            {
                Items = response.Data!.Items;
                TotalCount = response.Data.TotalCount;
            }
            else
            {
                Items = Array.Empty<TDto>();
                TotalCount = 0;
                ApplyApiError(response.Error!);
            }
        }

        private bool IsCurrent(long generation)
        {
            lock (_sync)
            {
                return !_disposed && _generation == generation;
            }
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
