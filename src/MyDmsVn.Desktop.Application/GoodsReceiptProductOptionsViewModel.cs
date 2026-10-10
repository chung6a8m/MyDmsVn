using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public sealed class GoodsReceiptProductOptionsViewModel : DesktopViewModelBase, IDisposable
    {
        private readonly object _sync = new object();
        private readonly ICatalogLookupSource _source;
        private readonly IMessenger _messenger;
        private readonly IUiDispatcher _dispatcher;
        private readonly DebouncedAsyncAction _searchReload;
        private readonly DebouncedAsyncAction _messageReload;
        private IReadOnlyList<CatalogLookupOption> _items = Array.Empty<CatalogLookupOption>();
        private CancellationTokenSource? _activeLoad;
        private string? _search;
        private long _generation;
        private bool _disposed;

        public GoodsReceiptProductOptionsViewModel(
            ICatalogLookupSource source,
            IMessenger messenger,
            IUiDispatcher dispatcher,
            IDesktopNotificationService notifications,
            IAsyncDelay delay,
            TimeSpan? debounceInterval = null)
            : base(notifications)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            if (_source.Kind != CatalogKind.Product)
            {
                throw new ArgumentException("A product lookup source is required.", nameof(source));
            }

            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            var interval = debounceInterval ?? CatalogLookupViewModel.DefaultDebounceInterval;
            _searchReload = new DebouncedAsyncAction(delay, interval);
            _messageReload = new DebouncedAsyncAction(delay, interval);
            _messenger.Register<GoodsReceiptProductOptionsViewModel, CatalogChangedMessage>(
                this,
                static (recipient, message) => recipient.Receive(message));
        }

        public IReadOnlyList<CatalogLookupOption> Items
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

        public Task ActivateAsync(CancellationToken cancellationToken) =>
            RefreshAsync(cancellationToken);

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _searchReload.Cancel();
                _messageReload.Cancel();
                return LoadAsync(cancellationToken);
            }
        }

        public Task SetSearch(string? search)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _messageReload.Cancel();
                CancelActiveLoadLocked();
                _search = search;
                return _searchReload.Schedule(LoadAsync);
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
            _searchReload.Dispose();
            _messageReload.Dispose();
            _messenger.UnregisterAll(this);
        }

        private void Receive(CatalogChangedMessage message)
        {
            if (message.CatalogKind != CatalogKind.Product)
            {
                return;
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _searchReload.Cancel();
                CancelActiveLoadLocked();
                _ = _messageReload.Schedule(LoadAsync);
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

                CancelActiveLoadLocked();
                loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeLoad = loadCancellation;
                generation = ++_generation;
            }

            try
            {
                var response = await _source.LookupAsync(
                    new CatalogLookupRequest(_search, 200),
                    loadCancellation.Token).ConfigureAwait(false);
                if (!IsCurrent(generation))
                {
                    return;
                }

                await _dispatcher.InvokeAsync(
                    () =>
                    {
                        if (!IsCurrent(generation))
                        {
                            return;
                        }

                        if (response.IsSuccess)
                        {
                            ResetValidation();
                            Items = response.Data!
                                .Where(item => item.IsActive)
                                .Select(item => new CatalogLookupOption(
                                    item.Id,
                                    item.Code,
                                    item.Name,
                                    isAvailableForNewSelection: true))
                                .ToArray();
                        }
                        else
                        {
                            ApplyApiError(response.Error!);
                        }
                    },
                    loadCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (loadCancellation.IsCancellationRequested)
            {
            }
            finally
            {
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

        private bool IsCurrent(long generation)
        {
            lock (_sync)
            {
                return !_disposed && generation == _generation;
            }
        }

        private void CancelActiveLoadLocked()
        {
            _activeLoad?.Cancel();
            _activeLoad = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GoodsReceiptProductOptionsViewModel));
            }
        }
    }
}
