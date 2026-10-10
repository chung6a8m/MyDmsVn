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
        private readonly DebouncedAsyncAction _messageReload;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();
        private IReadOnlyList<TDto> _items = Array.Empty<TDto>();
        private CancellationTokenSource? _activeLoad;
        private CancellationTokenSource? _activeEditorLoad;
        private string? _search;
        private int _pageNumber = 1;
        private readonly int _pageSize = 25;
        private long _totalCount;
        private int? _selectedId;
        private bool _isLoading;
        private bool _disposed;
        private long _generation;
        private long _editorGeneration;
        private bool _ignoreNextRelevantMessage;

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
            _messageReload = new DebouncedAsyncAction(
                delay,
                debounceInterval ?? DefaultDebounceInterval);
            Messenger.Register<CatalogViewModel<TDto>, CatalogChangedMessage>(
                this,
                static (recipient, message) => recipient.Receive(message));
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
            _messageReload.Cancel();
            CancelListLoad();
            _search = search;
            OnPropertyChanged(nameof(Search));
            PageNumber = 1;
            return _searchReload.Schedule(LoadAsync);
        }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            _searchReload.Cancel();
            _messageReload.Cancel();
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
            CancelEditorLoad();
            SelectedId = null;
            ResetValidation();
            ClearEditor();
        }

        public async Task SaveAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token))
            {
                await ExecuteBusyAsync(
                    SaveCoreAsync,
                    linked.Token,
                    () => !IsDisposed()).ConfigureAwait(false);
            }
        }

        public async Task SetActiveAsync(
            int id,
            bool isActive,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token))
            {
                await ExecuteBusyAsync(
                    token => SetActiveCoreAndRefreshAsync(id, isActive, token),
                    linked.Token,
                    () => !IsDisposed()).ConfigureAwait(false);
            }
        }

        public async Task SelectAsync(int id, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            CancellationTokenSource editorCancellation;
            long editorGeneration;
            lock (_sync)
            {
                _activeEditorLoad?.Cancel();
                editorCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _lifetimeCancellation.Token);
                _activeEditorLoad = editorCancellation;
                editorGeneration = ++_editorGeneration;
            }

            try
            {
                var response = await GetAsync(id, editorCancellation.Token).ConfigureAwait(false);
                if (!IsEditorCurrent(editorGeneration))
                {
                    return;
                }

                await _dispatcher.InvokeAsync(
                    () =>
                    {
                        if (!IsEditorCurrent(editorGeneration))
                        {
                            return;
                        }

                        if (response.IsSuccess)
                        {
                            ResetValidation();
                            SelectedId = GetId(response.Data!);
                            PopulateEditor(response.Data!);
                        }
                        else
                        {
                            ApplyApiError(response.Error!);
                        }
                    },
                    editorCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (editorCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (!editorCancellation.IsCancellationRequested && IsEditorCurrent(editorGeneration))
                {
                    await _dispatcher.InvokeAsync(
                        () =>
                        {
                            if (IsEditorCurrent(editorGeneration))
                            {
                                PublishNotification(
                                    DesktopNotificationKind.Error,
                                    exception.Message);
                            }
                        },
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_activeEditorLoad, editorCancellation))
                    {
                        _activeEditorLoad = null;
                    }
                }

                editorCancellation.Dispose();
            }
        }

        public void Cancel()
        {
            _searchReload.Cancel();
            _messageReload.Cancel();
            CancelListLoad();
            CancelEditorLoad();
            CancelBusyOperation();
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
            _lifetimeCancellation.Cancel();
            CancelEditorLoad();
            CancelBusyOperation();
            _searchReload.Dispose();
            _messageReload.Dispose();
            Messenger.UnregisterAll(this);
            _lifetimeCancellation.Dispose();
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
                long editorGeneration;
                lock (_sync)
                {
                    editorGeneration = _editorGeneration;
                }

                var selectedId = SelectedId;
                var operation = selectedId.HasValue
                    ? CatalogChangeOperation.Updated
                    : CatalogChangeOperation.Created;
                var response = selectedId.HasValue
                    ? await UpdateAsync(selectedId.Value, cancellationToken).ConfigureAwait(false)
                    : await CreateAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!response.IsSuccess)
                {
                    await _dispatcher.InvokeAsync(
                        () => ApplyApiError(response.Error!),
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                var entityId = GetId(response.Data!);
                if (IsEditorCurrent(editorGeneration))
                {
                    await _dispatcher.InvokeAsync(
                        () =>
                        {
                            if (IsEditorCurrent(editorGeneration))
                            {
                                SelectedId = entityId;
                                PopulateEditor(response.Data!);
                            }
                        },
                        cancellationToken).ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
                _ignoreNextRelevantMessage = true;
                Messenger.Send(new CatalogChangedMessage(Kind, entityId, operation));
                await LoadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested || IsDisposed())
                {
                    return;
                }

                await _dispatcher.InvokeAsync(
                    () =>
                    {
                        if (!IsDisposed())
                        {
                            PublishNotification(DesktopNotificationKind.Error, exception.Message);
                        }
                    },
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
                cancellationToken.ThrowIfCancellationRequested();
                if (!response.IsSuccess)
                {
                    await _dispatcher.InvokeAsync(
                        () => ApplyApiError(response.Error!),
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                _ignoreNextRelevantMessage = true;
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
                if (cancellationToken.IsCancellationRequested || IsDisposed())
                {
                    return;
                }

                await _dispatcher.InvokeAsync(
                    () =>
                    {
                        if (!IsDisposed())
                        {
                            PublishNotification(DesktopNotificationKind.Error, exception.Message);
                        }
                    },
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
                        () =>
                        {
                            if (IsCurrent(generation))
                            {
                                PublishNotification(DesktopNotificationKind.Error, exception.Message);
                            }
                        },
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                var wasActiveLoad = false;
                lock (_sync)
                {
                    if (ReferenceEquals(_activeLoad, loadCancellation))
                    {
                        _activeLoad = null;
                        wasActiveLoad = true;
                    }
                }

                if (wasActiveLoad)
                {
                    await _dispatcher.InvokeAsync(
                        () =>
                        {
                            if (!IsDisposed())
                            {
                                IsLoading = false;
                            }
                        },
                        CancellationToken.None).ConfigureAwait(false);
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

        private bool IsEditorCurrent(long generation)
        {
            lock (_sync)
            {
                return !_disposed && _editorGeneration == generation;
            }
        }

        private bool IsDisposed()
        {
            lock (_sync)
            {
                return _disposed;
            }
        }

        private void CancelListLoad()
        {
            lock (_sync)
            {
                _generation++;
                _activeLoad?.Cancel();
            }
        }

        private void CancelEditorLoad()
        {
            lock (_sync)
            {
                _editorGeneration++;
                _activeEditorLoad?.Cancel();
                _activeEditorLoad = null;
            }
        }

        private void Receive(CatalogChangedMessage message)
        {
            if (message.CatalogKind != Kind)
            {
                return;
            }

            if (_ignoreNextRelevantMessage)
            {
                _ignoreNextRelevantMessage = false;
                return;
            }

            CancelListLoad();
            _ = ScheduleMessageReloadAsync();
        }

        private async Task ScheduleMessageReloadAsync()
        {
            try
            {
                Task scheduled = Task.CompletedTask;
                await _dispatcher.InvokeAsync(
                    () => scheduled = _messageReload.Schedule(LoadAsync),
                    _lifetimeCancellation.Token).ConfigureAwait(false);
                await scheduled.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
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
