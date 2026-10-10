using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public interface ICatalogLookupSource
    {
        CatalogKind Kind { get; }

        Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(
            CatalogLookupRequest request,
            CancellationToken cancellationToken);

        Task<ApiResponse<CatalogLookupDto>> GetAsync(
            int id,
            CancellationToken cancellationToken);
    }

    public sealed class CatalogLookupOption
    {
        public CatalogLookupOption(
            int id,
            string code,
            string name,
            bool isAvailableForNewSelection,
            string? historicalDisplayName = null)
        {
            Id = id;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsAvailableForNewSelection = isAvailableForNewSelection;
            DisplayName = string.IsNullOrWhiteSpace(historicalDisplayName)
                ? $"{code} - {name}"
                : historicalDisplayName!;
        }

        public int Id { get; }
        public string Code { get; }
        public string Name { get; }
        public string DisplayName { get; }
        public bool IsAvailableForNewSelection { get; }
    }

    public sealed class CatalogLookupViewModel : DesktopViewModelBase, IDisposable
    {
        public static readonly TimeSpan DefaultDebounceInterval = TimeSpan.FromMilliseconds(300);

        private readonly object _sync = new object();
        private readonly ICatalogLookupSource _source;
        private readonly IMessenger _messenger;
        private readonly IUiDispatcher _dispatcher;
        private readonly DebouncedAsyncAction _messageReload;
        private readonly DebouncedAsyncAction _searchReload;
        private IReadOnlyList<CatalogLookupOption> _items = Array.Empty<CatalogLookupOption>();
        private CancellationTokenSource? _activeLoad;
        private string? _search;
        private int? _selectedId;
        private string? _selectedHistoricalLabel;
        private bool _selectedIsActive;
        private long _selectionGeneration;
        private long _generation;
        private long _messageReloadGeneration;
        private bool _disposed;

        public CatalogLookupViewModel(
            ICatalogLookupSource source,
            IMessenger messenger,
            IUiDispatcher dispatcher,
            IDesktopNotificationService notifications,
            IAsyncDelay delay,
            TimeSpan? debounceInterval = null)
            : base(notifications)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            var interval = debounceInterval ?? DefaultDebounceInterval;
            _messageReload = new DebouncedAsyncAction(
                delay ?? throw new ArgumentNullException(nameof(delay)),
                interval);
            _searchReload = new DebouncedAsyncAction(delay, interval);
            _messenger.Register<CatalogLookupViewModel, CatalogChangedMessage>(
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

        public int? SelectedId
        {
            get => _selectedId;
            private set => SetProperty(ref _selectedId, value);
        }

        public Task ActivateAsync(CancellationToken cancellationToken) =>
            RefreshAsync(cancellationToken);

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _searchReload.Cancel();
                CancelMessageReloadLocked();
                return LoadAsync(cancellationToken);
            }
        }

        public Task SetSearch(string? search)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                CancelMessageReloadLocked();
                CancelActiveLoadLocked();
                _search = search;
                return _searchReload.Schedule(LoadAsync);
            }
        }

        public void Cancel()
        {
            lock (_sync)
            {
                _searchReload.Cancel();
                CancelMessageReloadLocked();
                CancelActiveLoadLocked();
            }

            CancelCurrentOperation();
        }

        public void SetSelection(int? id, string? historicalDisplayName, bool isActive)
        {
            ThrowIfDisposed();
            SelectedId = id;
            _selectedHistoricalLabel = historicalDisplayName;
            _selectedIsActive = isActive;
            _selectionGeneration++;
            Items = BuildOptions(
                Items.Where(item => item.IsAvailableForNewSelection)
                    .Select(item => new CatalogLookupDto(
                        item.Id,
                        item.Code,
                        item.Name,
                        true)));
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
                _messageReloadGeneration++;
                activeLoad = _activeLoad;
                _activeLoad = null;
            }

            activeLoad?.Cancel();
            _searchReload.Dispose();
            _messageReload.Dispose();
            _messenger.UnregisterAll(this);
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource loadCancellation;
            long generation;
            long selectionGeneration;
            int? selectedId;
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
                selectionGeneration = _selectionGeneration;
                selectedId = _selectedId;
            }

            try
            {
                var response = await _source.LookupAsync(
                    new CatalogLookupRequest(_search, 25),
                    loadCancellation.Token).ConfigureAwait(false);
                ApiResponse<CatalogLookupDto>? selectedResponse = null;
                if (selectedId.HasValue)
                {
                    selectedResponse = await _source.GetAsync(
                        selectedId.Value,
                        loadCancellation.Token).ConfigureAwait(false);
                }
                if (!IsCurrent(generation))
                {
                    return;
                }

                await _dispatcher.InvokeAsync(
                    () => ApplyResponse(
                        response,
                        selectedResponse,
                        selectedId,
                        selectionGeneration,
                        generation),
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

        private void ApplyResponse(
            ApiResponse<IReadOnlyList<CatalogLookupDto>> response,
            ApiResponse<CatalogLookupDto>? selectedResponse,
            int? requestedSelectedId,
            long selectionGeneration,
            long generation)
        {
            if (!IsCurrent(generation))
            {
                return;
            }

            if (response.IsSuccess)
            {
                if (selectedResponse != null &&
                    selectedResponse.IsSuccess &&
                    requestedSelectedId == SelectedId &&
                    selectionGeneration == _selectionGeneration)
                {
                    _selectedIsActive = selectedResponse.Data!.IsActive;
                    if (string.IsNullOrWhiteSpace(_selectedHistoricalLabel))
                    {
                        _selectedHistoricalLabel =
                            $"{selectedResponse.Data.Code} - {selectedResponse.Data.Name}";
                    }
                }

                Items = BuildOptions(response.Data!);
            }
            else
            {
                ApplyApiError(response.Error!);
            }
        }

        private IReadOnlyList<CatalogLookupOption> BuildOptions(
            IEnumerable<CatalogLookupDto> sourceItems)
        {
            var options = sourceItems
                .Where(item => item.IsActive)
                .Select(item => new CatalogLookupOption(
                    item.Id,
                    item.Code,
                    item.Name,
                    isAvailableForNewSelection: true))
                .ToList();
            if (SelectedId.HasValue)
            {
                var selected = options.FirstOrDefault(item => item.Id == SelectedId.Value);
                if (selected != null && !_selectedIsActive)
                {
                    options.Remove(selected);
                }

                if (selected == null || !_selectedIsActive)
                {
                    options.Add(
                        new CatalogLookupOption(
                            SelectedId.Value,
                            string.Empty,
                            string.Empty,
                            _selectedIsActive,
                            _selectedHistoricalLabel ?? SelectedId.Value.ToString()));
                }
            }

            return options;
        }

        private bool IsCurrent(long generation)
        {
            lock (_sync)
            {
                return !_disposed && _generation == generation;
            }
        }

        private void CancelActiveLoadLocked()
        {
            _generation++;
            _activeLoad?.Cancel();
        }

        private void Receive(CatalogChangedMessage message)
        {
            if (message.CatalogKind != _source.Kind)
            {
                return;
            }

            long messageReloadGeneration;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _searchReload.Cancel();
                _messageReload.Cancel();
                messageReloadGeneration = ++_messageReloadGeneration;
                CancelActiveLoadLocked();
            }

            _ = ScheduleMessageReloadAsync(messageReloadGeneration);
        }

        private async Task ScheduleMessageReloadAsync(long messageReloadGeneration)
        {
            try
            {
                Task scheduled = Task.CompletedTask;
                await _dispatcher.InvokeAsync(
                    () =>
                    {
                        lock (_sync)
                        {
                            if (!_disposed &&
                                _messageReloadGeneration == messageReloadGeneration)
                            {
                                scheduled = _messageReload.Schedule(LoadAsync);
                            }
                        }
                    },
                    CancellationToken.None).ConfigureAwait(false);
                await scheduled.ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void CancelMessageReloadLocked()
        {
            _messageReloadGeneration++;
            _messageReload.Cancel();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CatalogLookupViewModel));
            }
        }
    }

    public sealed class ProductCatalogLookupSource : ICatalogLookupSource
    {
        private readonly IProductApiClient _client;
        public ProductCatalogLookupSource(IProductApiClient client) => _client = client;
        public CatalogKind Kind => CatalogKind.Product;
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken) => _client.LookupAsync(request, cancellationToken);
        public async Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken cancellationToken) => Map(await _client.GetAsync(id, cancellationToken).ConfigureAwait(false));
        private static ApiResponse<CatalogLookupDto> Map(ApiResponse<ProductDto> response) => response.IsSuccess ? ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(response.Data!.Id, response.Data.Code, response.Data.Name, response.Data.IsActive)) : ApiResponse<CatalogLookupDto>.Failure(response.Error!);
    }

    public sealed class WarehouseCatalogLookupSource : ICatalogLookupSource
    {
        private readonly IWarehouseApiClient _client;
        public WarehouseCatalogLookupSource(IWarehouseApiClient client) => _client = client;
        public CatalogKind Kind => CatalogKind.Warehouse;
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken) => _client.LookupAsync(request, cancellationToken);
        public async Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken cancellationToken) => Map(await _client.GetAsync(id, cancellationToken).ConfigureAwait(false));
        private static ApiResponse<CatalogLookupDto> Map(ApiResponse<WarehouseDto> response) => response.IsSuccess ? ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(response.Data!.Id, response.Data.Code, response.Data.Name, response.Data.IsActive)) : ApiResponse<CatalogLookupDto>.Failure(response.Error!);
    }

    public sealed class EmployeeCatalogLookupSource : ICatalogLookupSource
    {
        private readonly IEmployeeApiClient _client;
        public EmployeeCatalogLookupSource(IEmployeeApiClient client) => _client = client;
        public CatalogKind Kind => CatalogKind.Employee;
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken) => _client.LookupAsync(request, cancellationToken);
        public async Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken cancellationToken) => Map(await _client.GetAsync(id, cancellationToken).ConfigureAwait(false));
        private static ApiResponse<CatalogLookupDto> Map(ApiResponse<EmployeeDto> response) => response.IsSuccess ? ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(response.Data!.Id, response.Data.Code, response.Data.Name, response.Data.IsActive)) : ApiResponse<CatalogLookupDto>.Failure(response.Error!);
    }

    public sealed class CustomerCatalogLookupSource : ICatalogLookupSource
    {
        private readonly ICustomerApiClient _client;
        public CustomerCatalogLookupSource(ICustomerApiClient client) => _client = client;
        public CatalogKind Kind => CatalogKind.Customer;
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken) => _client.LookupAsync(request, cancellationToken);
        public async Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken cancellationToken) => Map(await _client.GetAsync(id, cancellationToken).ConfigureAwait(false));
        private static ApiResponse<CatalogLookupDto> Map(ApiResponse<CustomerDto> response) => response.IsSuccess ? ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(response.Data!.Id, response.Data.Code, response.Data.Name, response.Data.IsActive)) : ApiResponse<CatalogLookupDto>.Failure(response.Error!);
    }
}
