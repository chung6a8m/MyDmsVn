using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.UiLab.Scenarios;

public sealed class ScenarioPermissionClient : IPermissionApiClient
{
    private readonly Dictionary<string, bool> _decisions = new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["Sales.Receipts.Read"] = true,
        ["Sales.Receipts.Write"] = true,
        ["Sales.Receipts.Post"] = true,
    };

    private readonly ScenarioTaskTracker _permissionTasks = new ScenarioTaskTracker();
    public ScenarioGate<bool>? NextCheckGate { get; set; }
    public IReadOnlyList<Task> OutstandingChecks => _permissionTasks.Outstanding;

    public void Set(string permissionKey, bool allowed)
    {
        if (!_decisions.ContainsKey(permissionKey)) throw new ArgumentException("Unknown UI Lab permission key.", nameof(permissionKey));
        _decisions[permissionKey] = allowed;
    }

    public Task<ApiResponse<PermissionDecisionDto>> CheckAsync(string permissionKey, CancellationToken cancellationToken)
    {
        var gate = NextCheckGate;
        NextCheckGate = null;
        return _permissionTasks.Track(CheckCoreAsync(permissionKey, cancellationToken, gate));
    }

    private async Task<ApiResponse<PermissionDecisionDto>> CheckCoreAsync(string permissionKey, CancellationToken cancellationToken, ScenarioGate<bool>? gate)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (gate != null) await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return ApiResponse<PermissionDecisionDto>.Success(
            new PermissionDecisionDto(permissionKey, _decisions.TryGetValue(permissionKey, out var allowed) && allowed));
    }
}

public sealed class ScenarioProductApiClient : IProductApiClient
{
    private readonly IReadOnlyList<ProductDto> _products;
    private readonly List<int> _getCalls = new List<int>();
    private readonly ScenarioTaskTracker _tasks = new ScenarioTaskTracker();
    private readonly List<ScenarioGate<ApiResponse<ProductDto>>> _gates = new List<ScenarioGate<ApiResponse<ProductDto>>>();
    public IReadOnlyList<int> GetCalls { get { lock (_getCalls) return _getCalls.ToArray(); } }
    public IReadOnlyList<Task> OutstandingOperations => _tasks.Outstanding;
    private ScenarioGate<ApiResponse<ProductDto>>? _getGate;
    public ScenarioGate<ApiResponse<ProductDto>>? GetGate
    {
        get => _getGate;
        set { _getGate = value; if (value != null) _gates.Add(value); }
    }
    public void CancelGates() { foreach (var gate in _gates) gate.Cancel(); }

    public ScenarioProductApiClient(IReadOnlyList<ProductDto> products)
    {
        _products = products;
    }

    public Task<ApiResponse<ProductDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        lock (_getCalls) _getCalls.Add(id);
        if (GetGate != null) return _tasks.Track(GetGate.WaitAsync(cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        var product = _products.FirstOrDefault(item => item.Id == id);
        return Task.FromResult(product == null
            ? ApiResponse<ProductDto>.Failure(new ApiError(ApiStatusCode.NotFound, "Product.NotFound", "Product is not in the UI Lab fixture."))
            : ApiResponse<ProductDto>.Success(product));
    }

    public Task<ApiResponse<PagedResult<ProductDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.PageNumber < 1 || request.PageNumber > 100 || request.PageSize < 1 || request.PageSize > 200)
            return Task.FromResult(ScenarioErrors.Unsupported<PagedResult<ProductDto>>("Product page"));
        var matches = _products.Where(item => (request.IncludeInactive || item.IsActive) &&
            (string.IsNullOrWhiteSpace(request.Search) || item.Code.IndexOf(request.Search, StringComparison.OrdinalIgnoreCase) >= 0 ||
             item.Name.IndexOf(request.Search, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
        return Task.FromResult(ApiResponse<PagedResult<ProductDto>>.Success(new PagedResult<ProductDto>(
            matches.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize), request.PageNumber, request.PageSize, matches.Length)));
    }

    public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Limit < 1 || request.Limit > 200)
            return Task.FromResult(ScenarioErrors.Unsupported<IReadOnlyList<CatalogLookupDto>>("Product lookup limit"));
        var matches = _products.Where(item => item.IsActive && (string.IsNullOrWhiteSpace(request.Search) ||
            item.Code.IndexOf(request.Search, StringComparison.OrdinalIgnoreCase) >= 0 || item.Name.IndexOf(request.Search, StringComparison.OrdinalIgnoreCase) >= 0))
            .Take(Math.Max(0, request.Limit)).Select(item => new CatalogLookupDto(item.Id, item.Code, item.Name, item.IsActive)).ToArray();
        return Task.FromResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(matches));
    }

    public Task<ApiResponse<ProductDto>> CreateAsync(SaveProductRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(ScenarioErrors.Unsupported<ProductDto>("Product create"));
    public Task<ApiResponse<ProductDto>> UpdateAsync(int id, SaveProductRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(ScenarioErrors.Unsupported<ProductDto>("Product update"));
    public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken) =>
        Task.FromResult(ScenarioErrors.Unsupported<UnitResponse>("Product active status"));
}

public sealed class ScenarioLookupSource : ICatalogLookupSource
{
    private readonly IReadOnlyList<CatalogLookupDto> _items;
    private readonly List<CatalogLookupRequest> _lookupCalls = new List<CatalogLookupRequest>();
    private readonly ScenarioTaskTracker _tasks = new ScenarioTaskTracker();
    private readonly List<ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>>> _allLookupGates =
        new List<ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>>>();
    private readonly List<ScenarioGate<ApiResponse<CatalogLookupDto>>> _allGetGates =
        new List<ScenarioGate<ApiResponse<CatalogLookupDto>>>();
    private readonly Queue<ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>>> _queuedLookupGates =
        new Queue<ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>>>();
    public ScenarioLookupSource(CatalogKind kind, IReadOnlyList<CatalogLookupDto> items)
    {
        Kind = kind;
        _items = items;
    }

    public CatalogKind Kind { get; }
    public IReadOnlyList<CatalogLookupRequest> LookupCalls { get { lock (_lookupCalls) return _lookupCalls.ToArray(); } }
    public IReadOnlyList<Task> OutstandingOperations => _tasks.Outstanding;
    private ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>>? _lookupGate;
    private ScenarioGate<ApiResponse<CatalogLookupDto>>? _getGate;
    public ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>>? LookupGate
    {
        get => _lookupGate;
        set { _lookupGate = value; if (value != null) _allLookupGates.Add(value); }
    }
    public ScenarioGate<ApiResponse<CatalogLookupDto>>? GetGate
    {
        get => _getGate;
        set { _getGate = value; if (value != null) _allGetGates.Add(value); }
    }

    public void EnqueueLookupGate(ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>> gate)
    {
        lock (_queuedLookupGates) { _queuedLookupGates.Enqueue(gate); _allLookupGates.Add(gate); }
    }

    public void CancelQueuedGates()
    {
        lock (_queuedLookupGates)
        {
            foreach (var gate in _allLookupGates) gate.Cancel();
            foreach (var gate in _allGetGates) gate.Cancel();
        }
    }

    public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken)
    {
        lock (_lookupCalls) _lookupCalls.Add(request);
        if (request.Limit < 1 || request.Limit > 200)
            return Task.FromResult(ScenarioErrors.Unsupported<IReadOnlyList<CatalogLookupDto>>("Catalog lookup limit"));
        ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>>? queued = null;
        lock (_queuedLookupGates) if (_queuedLookupGates.Count > 0) queued = _queuedLookupGates.Dequeue();
        if (queued != null) return _tasks.Track(queued.WaitAsync(cancellationToken));
        if (LookupGate != null) return _tasks.Track(LookupGate.WaitAsync(cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        var matches = _items.Where(item => item.IsActive && (string.IsNullOrWhiteSpace(request.Search) ||
            item.Code.IndexOf(request.Search, StringComparison.OrdinalIgnoreCase) >= 0 || item.Name.IndexOf(request.Search, StringComparison.OrdinalIgnoreCase) >= 0))
            .Take(Math.Max(0, request.Limit)).ToArray();
        return Task.FromResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(matches));
    }

    public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        if (GetGate != null) return _tasks.Track(GetGate.WaitAsync(cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        var item = _items.FirstOrDefault(candidate => candidate.Id == id);
        return Task.FromResult(item == null
            ? ApiResponse<CatalogLookupDto>.Failure(new ApiError(ApiStatusCode.NotFound, "Catalog.NotFound", "Catalog item is not in the UI Lab fixture."))
            : ApiResponse<CatalogLookupDto>.Success(item));
    }
}

public sealed class ScenarioLookupSourceFactory
{
    private readonly IReadOnlyDictionary<CatalogKind, IReadOnlyList<CatalogLookupDto>> _items;
    public ScenarioLookupSourceFactory(IReadOnlyDictionary<CatalogKind, IReadOnlyList<CatalogLookupDto>> items)
    {
        _items = items;
    }

    public ScenarioLookupSource Create(CatalogKind kind) => _items.TryGetValue(kind, out var items)
        ? new ScenarioLookupSource(kind, items)
        : throw new ArgumentOutOfRangeException(nameof(kind));
}
