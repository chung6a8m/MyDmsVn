using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.UiLab.Scenarios;

public sealed class ScenarioSalesApiClient : ISalesReceiptApiClient
{
    private readonly ScenarioDesktopSession _session;
    private readonly ScenarioJournal _journal;
    private readonly ScenarioPreviewCalculator _preview;
    private readonly object _sync = new object();
    private readonly Queue<Func<CancellationToken, Task<ApiResponse<SalesReceiptDto>>>> _getOverrides =
        new Queue<Func<CancellationToken, Task<ApiResponse<SalesReceiptDto>>>>();
    private readonly List<ScenarioGate<ApiResponse<SalesReceiptDto>>> _getGates = new List<ScenarioGate<ApiResponse<SalesReceiptDto>>>();
    private readonly List<ScenarioGate<ApiResponse<PostSalesReceiptResponse>>> _postGates = new List<ScenarioGate<ApiResponse<PostSalesReceiptResponse>>>();
    private SalesReceiptDto? _sale;
    private long _nextLineId = 100;
    private long _version = 1;
    private readonly HashSet<Task> _outstanding = new HashSet<Task>();

    public ScenarioSalesApiClient(ScenarioDesktopSession session, ScenarioJournal journal, ScenarioPreviewCalculator preview, SalesReceiptDto? seed)
    {
        _session = session;
        _journal = journal;
        _preview = preview;
        _sale = seed;
        _nextLineId = Math.Max(100, seed?.Lines.Max(line => line.SaleLineId) ?? 0) + 1;
    }

    public SalesReceiptDto? CurrentSale { get { lock (_sync) return _sale; } }
    private ScenarioGate<ApiResponse<PostSalesReceiptResponse>>? _postGate;
    public ScenarioGate<ApiResponse<PostSalesReceiptResponse>>? PostGate
    {
        get => _postGate;
        set { _postGate = value; if (value != null) _postGates.Add(value); }
    }
    public bool UnknownPostOutcome { get; set; }
    public ApiResponse<SalesReceiptDto>? NextSaveResponse { get; set; }

    public void RejectNextSaveField(string field, string code = "UiLab.FieldInvalid")
    {
        NextSaveResponse = ApiResponse<SalesReceiptDto>.Failure(new ApiError(ApiStatusCode.BadRequest,
            "SalesReceipt.InvalidDraft", "The UI Lab server rejected this field.",
            new[] { new ApiErrorDetail(code, "Correct this field.", field) }));
    }

    public void EnqueueGetResponse(ApiResponse<SalesReceiptDto> response) =>
        EnqueueGet(_ => Task.FromResult(response));

    public void EnqueueGetGate(ScenarioGate<ApiResponse<SalesReceiptDto>> gate)
    {
        lock (_sync) _getGates.Add(gate);
        EnqueueGet(gate.WaitAsync);
    }

    public void CancelGates()
    {
        lock (_sync)
        {
            foreach (var gate in _getGates) gate.Cancel();
            foreach (var gate in _postGates) gate.Cancel();
        }
    }

    private void EnqueueGet(Func<CancellationToken, Task<ApiResponse<SalesReceiptDto>>> operation)
    {
        lock (_sync) _getOverrides.Enqueue(operation);
    }

    public IReadOnlyList<Task> OutstandingOperations
    {
        get { lock (_sync) return _outstanding.ToArray(); }
    }

    private Task<T> Track<T>(Task<T> task)
    {
        lock (_sync) _outstanding.Add(task);
        _ = task.ContinueWith(completed => { lock (_sync) _outstanding.Remove(completed); },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }

    public ISalesReceiptApiClient ForSession(long expectedSessionVersion) => new Bound(this, expectedSessionVersion);
    private bool Admitted(long? expected) => _session.IsAuthenticated && (!expected.HasValue || _session.Version == expected.Value);

    private Task<ApiResponse<T>> Reject<T>() => Task.FromResult(ScenarioErrors.SessionChanged<T>());

    public Task<ApiResponse<SalesReceiptListPage>> ListAsync(SalesReceiptListRequest request, CancellationToken cancellationToken) => List(request, cancellationToken, null);
    private Task<ApiResponse<SalesReceiptListPage>> List(SalesReceiptListRequest request, CancellationToken token, long? expected)
    {
        if (!Admitted(expected)) return Reject<SalesReceiptListPage>();
        token.ThrowIfCancellationRequested();
        _journal.Record("List.request", request);
        if (request.PageSize < 1 || request.PageSize > 200 || request.Cursor != null)
            return Task.FromResult(ScenarioErrors.Unsupported<SalesReceiptListPage>("Sales list page"));
        var sale = CurrentSale;
        var matched = sale != null && (!request.FromDate.HasValue || sale.SaleDate >= request.FromDate.Value) &&
            (!request.ToDate.HasValue || sale.SaleDate <= request.ToDate.Value) &&
            (!request.CustomerId.HasValue || sale.CustomerId == request.CustomerId.Value) &&
            (!request.WarehouseId.HasValue || sale.WarehouseId == request.WarehouseId.Value) &&
            (!request.EmployeeId.HasValue || sale.EmployeeId == request.EmployeeId.Value) &&
            (request.Status == null || sale.Status == request.Status) &&
            (!request.ProductId.HasValue || sale.Lines.Any(line => line.ProductId == request.ProductId.Value)) &&
            (string.IsNullOrWhiteSpace(request.Search) || sale.SaleNumber.IndexOf(request.Search, StringComparison.OrdinalIgnoreCase) >= 0);
        var rows = matched ? new[] { new SalesReceiptListRow(sale!.SaleId, sale.SaleNumber, sale.SaleDate,
            sale.CustomerId, sale.CustomerDisplayName ?? "Customer", sale.WarehouseId, sale.WarehouseDisplayName ?? "Warehouse",
            sale.EmployeeId, sale.EmployeeDisplayName ?? "Employee", sale.Status, sale.Lines.Count, sale.TotalAmount) }
            : Array.Empty<SalesReceiptListRow>();
        var response = ApiResponse<SalesReceiptListPage>.Success(new SalesReceiptListPage(rows, null, null));
        _journal.Record("List.response", response);
        return Task.FromResult(response);
    }

    public Task<ApiResponse<SalesReceiptDto>> GetAsync(long saleId, CancellationToken cancellationToken) => Get(saleId, cancellationToken, null);
    private Task<ApiResponse<SalesReceiptDto>> Get(long saleId, CancellationToken token, long? expected)
    {
        if (!Admitted(expected)) return Reject<SalesReceiptDto>();
        _journal.Record("Get.request", saleId);
        Func<CancellationToken, Task<ApiResponse<SalesReceiptDto>>>? queued = null;
        lock (_sync) if (_getOverrides.Count > 0) queued = _getOverrides.Dequeue();
        return Track(GetCore(saleId, token, queued));
    }

    private async Task<ApiResponse<SalesReceiptDto>> GetCore(long saleId, CancellationToken token,
        Func<CancellationToken, Task<ApiResponse<SalesReceiptDto>>>? queued)
    {
        ApiResponse<SalesReceiptDto> response;
        if (queued != null) response = await queued(token).ConfigureAwait(false);
        else
        {
            token.ThrowIfCancellationRequested();
            var sale = CurrentSale;
            response = sale != null && sale.SaleId == saleId
                ? ApiResponse<SalesReceiptDto>.Success(sale)
                : ApiResponse<SalesReceiptDto>.Failure(new ApiError(ApiStatusCode.NotFound, "SalesReceipt.NotFound", "Sale is not in the UI Lab fixture."));
        }
        _journal.Record("Get.response", response);
        return response;
    }

    public Task<ApiResponse<SalesReceiptDto>> CreateDraftAsync(SaveSalesReceiptRequest request, CancellationToken cancellationToken) =>
        Save(request, null, cancellationToken, null);
    public Task<ApiResponse<SalesReceiptDto>> UpdateDraftAsync(UpdateSalesReceiptDraftRequest request, CancellationToken cancellationToken) =>
        Save(new SaveSalesReceiptRequest(request.SaleDate, request.CustomerId, request.WarehouseId, request.EmployeeId, request.Note, request.Lines), request, cancellationToken, null);

    private Task<ApiResponse<SalesReceiptDto>> Save(SaveSalesReceiptRequest request, UpdateSalesReceiptDraftRequest? update, CancellationToken token, long? expected)
    {
        if (!Admitted(expected)) return Reject<SalesReceiptDto>();
        token.ThrowIfCancellationRequested();
        _journal.Record(update == null ? "Create.request" : "Update.request", (object?)update ?? request);
        ApiResponse<SalesReceiptDto> response;
        lock (_sync)
        {
            if (NextSaveResponse != null) { response = NextSaveResponse; NextSaveResponse = null; }
            else if (update == null && _sale != null) response = ScenarioErrors.Unsupported<SalesReceiptDto>("Second sales create");
            else if (update != null && (_sale == null || _sale.SaleId != update.SaleId || _sale.Status != "Draft" || _sale.Version != update.ExpectedVersion))
                response = ApiResponse<SalesReceiptDto>.Failure(new ApiError(ApiStatusCode.Conflict, "SalesReceipt.Version", "Reload this sale."));
            else if (request.CustomerId != 1 || request.WarehouseId != 2 || request.EmployeeId != 3 ||
                     (request.Note?.Length ?? 0) > SalesContractLimits.MaximumNoteLength ||
                     request.Lines.Count == 0 || request.Lines.Count > SalesContractLimits.MaximumLines ||
                     request.Lines.Where(line => line.SaleLineId > 0).Select(line => line.SaleLineId).Distinct().Count() != request.Lines.Count(line => line.SaleLineId > 0) ||
                     request.Lines.Any(line => (line.ProductId != 11 && line.ProductId != 12) || line.Qty <= 0 || line.Qty > 10000 ||
                     decimal.Round(line.Qty, 4) != line.Qty || line.UnitPrice < 0 || line.UnitPrice > 1000000 ||
                     decimal.Round(line.UnitPrice, 4) != line.UnitPrice || line.SaleLineId < 0 ||
                     (line.SaleLineId > 0 && (_sale == null || _sale.Lines.All(old => old.SaleLineId != line.SaleLineId)))))
                response = ApiResponse<SalesReceiptDto>.Failure(new ApiError(ApiStatusCode.BadRequest, "SalesReceipt.InvalidDraft", "Outside the UI Lab fixture range."));
            else
            {
                var lines = request.Lines.Select((line, index) => new SalesReceiptLineDto(
                    line.SaleLineId == 0 ? _nextLineId++ : line.SaleLineId, index + 1, line.ProductId,
                    line.ProductId == 11 ? "pcs" : "box", line.Qty, line.UnitPrice, line.IsGift,
                    _preview.CalculateRevenue(line.Qty, line.UnitPrice, line.IsGift), null, null,
                    line.ProductId == 11 ? "Demo Product A" : "Demo Product B", true)).ToArray();
                _sale = new SalesReceiptDto(41, "SA0000000041", request.SaleDate, request.CustomerId, request.WarehouseId, request.EmployeeId,
                    "Draft", request.Note, null, "lab-v" + ++_version, lines, _preview.SumAmounts(lines.Select(line => line.LineAmount)), null, null,
                    "Demo Customer", true, "Demo Warehouse", true, "Demo Employee", true);
                response = ApiResponse<SalesReceiptDto>.Success(_sale);
            }
        }
        _journal.Record(update == null ? "Create.response" : "Update.response", response);
        return Task.FromResult(response);
    }

    public Task<ApiResponse<PostSalesReceiptResponse>> PostAsync(PostSalesReceiptRequest request, CancellationToken cancellationToken) => Post(request, cancellationToken, null);
    private Task<ApiResponse<PostSalesReceiptResponse>> Post(PostSalesReceiptRequest request, CancellationToken token, long? expected)
    {
        if (!Admitted(expected)) return Reject<PostSalesReceiptResponse>();
        token.ThrowIfCancellationRequested();
        _journal.Record("Post.request", request);
        return Track(PostCore(request, token, expected));
    }

    private async Task<ApiResponse<PostSalesReceiptResponse>> PostCore(PostSalesReceiptRequest request, CancellationToken token, long? expected)
    {
        ApiResponse<PostSalesReceiptResponse> response;
        var gate = PostGate;
        // A successful gate releases the fixture transition; it cannot supply an unrelated authoritative version.
        var gated = gate == null ? null : await gate.WaitAsync(token).ConfigureAwait(false);
        if (!Admitted(expected)) response = ScenarioErrors.SessionChanged<PostSalesReceiptResponse>();
        else if (gated != null && !gated.IsSuccess) response = gated;
        else if (gated != null && (gated.Data!.SaleId != request.SaleId || gated.Data.Status != "Posted"))
            response = ScenarioErrors.Unsupported<PostSalesReceiptResponse>("Post gate result");
        else
        {
            lock (_sync)
            {
                if (!Admitted(expected))
                    response = ScenarioErrors.SessionChanged<PostSalesReceiptResponse>();
                else if (_sale == null || _sale.SaleId != request.SaleId || _sale.Status != "Draft" || _sale.Version != request.ExpectedVersion)
                    response = ApiResponse<PostSalesReceiptResponse>.Failure(new ApiError(ApiStatusCode.Conflict, "SalesReceipt.Version", "Reload this sale."));
                else
                {
                    var postedAt = new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc);
                    var version = "lab-v" + ++_version;
                    var lines = _sale.Lines.Select(line => new SalesReceiptLineDto(line.SaleLineId, line.LineNo, line.ProductId, line.Unit,
                        line.Qty, line.UnitPrice, line.IsGift, line.LineAmount, 2m, line.Qty * 2m,
                        line.ProductDisplayName, line.ProductIsActive)).ToArray();
                    var cost = lines.Sum(line => line.CostAmount!.Value);
                    _sale = new SalesReceiptDto(_sale.SaleId, _sale.SaleNumber, _sale.SaleDate, _sale.CustomerId, _sale.WarehouseId,
                        _sale.EmployeeId, "Posted", _sale.Note, postedAt, version, lines, _sale.TotalAmount, cost, _sale.TotalAmount - cost,
                        _sale.CustomerDisplayName, true, _sale.WarehouseDisplayName, true, _sale.EmployeeDisplayName, true);
                    response = UnknownPostOutcome
                        ? ApiResponse<PostSalesReceiptResponse>.Failure(new ApiError(ApiStatusCode.InternalServerError, "UiLab.UnknownPost", "The post outcome is unknown; reload the sale."))
                        : ApiResponse<PostSalesReceiptResponse>.Success(new PostSalesReceiptResponse(_sale.SaleId, "Posted", postedAt, version));
                    if (UnknownPostOutcome)
                        _getOverrides.Enqueue(_ => Task.FromResult(ApiResponse<SalesReceiptDto>.Failure(
                            new ApiError(ApiStatusCode.InternalServerError, "UiLab.ReloadFailed", "The first recovery reload fails."))));
                }
            }
        }
        _journal.Record("Post.response", response);
        return response;
    }

    private sealed class Bound : ISalesReceiptApiClient
    {
        private readonly ScenarioSalesApiClient _owner;
        private readonly long _expected;
        public Bound(ScenarioSalesApiClient owner, long expected) { _owner = owner; _expected = expected; }
        public ISalesReceiptApiClient ForSession(long expectedSessionVersion) => expectedSessionVersion == _expected
            ? this : throw new InvalidOperationException("Cannot rebind a session-bound Sales client.");
        public Task<ApiResponse<SalesReceiptListPage>> ListAsync(SalesReceiptListRequest request, CancellationToken token) => _owner.List(request, token, _expected);
        public Task<ApiResponse<SalesReceiptDto>> GetAsync(long id, CancellationToken token) => _owner.Get(id, token, _expected);
        public Task<ApiResponse<SalesReceiptDto>> CreateDraftAsync(SaveSalesReceiptRequest request, CancellationToken token) => _owner.Save(request, null, token, _expected);
        public Task<ApiResponse<SalesReceiptDto>> UpdateDraftAsync(UpdateSalesReceiptDraftRequest request, CancellationToken token) =>
            _owner.Save(new SaveSalesReceiptRequest(request.SaleDate, request.CustomerId, request.WarehouseId, request.EmployeeId, request.Note, request.Lines), request, token, _expected);
        public Task<ApiResponse<PostSalesReceiptResponse>> PostAsync(PostSalesReceiptRequest request, CancellationToken token) => _owner.Post(request, token, _expected);
    }
}
