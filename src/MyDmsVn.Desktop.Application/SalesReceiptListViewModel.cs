using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application;

public sealed class SalesReceiptListViewModel : ObservableObject, IDisposable
{
    private readonly object _sync = new object();
    private readonly ISalesReceiptApiClient _api;
    private readonly IDesktopSession _session;
    private readonly IUiDispatcher _ui;
    private readonly long _sessionVersion;
    private CancellationTokenSource? _active;
    private long _operation;
    private bool _recoveryConsumed;
    private bool _disposed;
    private PageState _page = PageState.Empty;

    public SalesReceiptListViewModel(ISalesReceiptApiClient api, IDesktopSession session, IUiDispatcher ui)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _sessionVersion = session.Version;
        _api = (api ?? throw new ArgumentNullException(nameof(api))).ForSession(_sessionVersion);
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        IsAccessDenied = !session.IsAuthenticated;
        _session.SessionChanged += SessionChanged;
    }
    public SalesReceiptListRequest PendingCriteria { get; private set; } = new SalesReceiptListRequest(null, null, null, null, null, null, null, null);
    public SalesReceiptListRequest? AppliedCriteria => _page.Criteria;
    public IReadOnlyList<SalesReceiptListRow> Items => _page.Items;
    public string? NextCursor => _page.Next;
    public string? PreviousCursor => _page.Previous;
    public long? SelectedSaleId => _page.Selected;
    public bool IsBusy { get; private set; }
    public bool IsAccessDenied { get; private set; }
    public ApiError? Error { get; private set; }
    public bool CanNext => Available && !IsBusy && ReferenceEquals(PendingCriteria, AppliedCriteria) && NextCursor != null;
    public bool CanPrevious => Available && !IsBusy && ReferenceEquals(PendingCriteria, AppliedCriteria) && PreviousCursor != null;
    private bool Available => !_disposed && !IsAccessDenied && _session.IsAuthenticated && _session.Version == _sessionVersion;

    // UI admission only. Each request remains bound to the original session and server authorization.
    public void SetAccess(bool canRead)
    {
        lock (_sync)
        {
            if (_disposed || !_session.IsAuthenticated || _session.Version != _sessionVersion) return;
            if (canRead)
            {
                if (!IsAccessDenied) return;
                IsAccessDenied = false; Error = null; Notify();
                return;
            }
            ++_operation; _active?.Cancel(); _active = null;
            IsAccessDenied = true; IsBusy = false; _page = PageState.Empty;
            PendingCriteria = new SalesReceiptListRequest(null, null, null, null, null, null, null, null);
            _recoveryConsumed = false; Notify();
        }
    }

    public Task SetCriteriaAsync(SalesReceiptListRequest criteria, CancellationToken token = default)
    {
        if (criteria == null) throw new ArgumentNullException(nameof(criteria));
        lock (_sync)
        {
            if (!Available) return Task.CompletedTask;
            PendingCriteria = Copy(criteria, null);
            _recoveryConsumed = false;
            OnPropertyChanged(nameof(PendingCriteria));
            return LoadAsync(PendingCriteria, null, token);
        }
    }
    public Task FirstAsync(CancellationToken token = default)
    {
        lock (_sync)
        {
            if (!Available) return Task.CompletedTask;
            _recoveryConsumed = false;
            return LoadAsync(PendingCriteria, null, token);
        }
    }
    public Task RefreshAsync(CancellationToken token = default) => FirstAsync(token);
    public Task NextAsync(CancellationToken token = default)
    { lock (_sync) return CanNext ? LoadAsync(AppliedCriteria!, NextCursor, token) : Task.CompletedTask; }
    public Task PreviousAsync(CancellationToken token = default)
    { lock (_sync) return CanPrevious ? LoadAsync(AppliedCriteria!, PreviousCursor, token) : Task.CompletedTask; }
    public void Select(long? id)
    {
        lock (_sync)
        {
            if (!Available || (id.HasValue && !Items.Any(row => row.SaleId == id))) return;
            _page = new PageState(AppliedCriteria, Items, NextCursor, PreviousCursor, id);
            OnPropertyChanged(nameof(SelectedSaleId));
        }
    }
    private async Task LoadAsync(SalesReceiptListRequest criteria, string? cursor, CancellationToken token)
    {
        _active?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        _active = cancellation;
        var operation = ++_operation;
        IsBusy = true; Notify();
        try
        {
            var response = await _api.ListAsync(Copy(criteria, cursor), cancellation.Token).ConfigureAwait(false);
            if (!Current(operation)) return;
            if (response.IsSuccess && cursor != null && response.Data!.Items.Count == 0)
            {
                bool recover;
                lock (_sync)
                {
                    if (!Current(operation)) return;
                    recover = !_recoveryConsumed;
                    _recoveryConsumed = true;
                }
                if (recover)
                    response = await _api.ListAsync(Copy(criteria, null), cancellation.Token).ConfigureAwait(false);
                else
                    response = ApiResponse<SalesReceiptListPage>.Failure(new ApiError(ApiStatusCode.Conflict,
                        "SalesReceipt.PageChanged", "The page changed. Refresh to start again."));
            }
            await _ui.InvokeAsync(() =>
            {
                lock (_sync)
                {
                    if (!Current(operation)) return;
                    Error = response.Error;
                    if (response.IsSuccess)
                    {
                        var page = response.Data!;
                        var selected = page.Items.Any(row => row.SaleId == SelectedSaleId) ? SelectedSaleId : null;
                        _page = new PageState(criteria, page.Items, page.NextCursor, page.PreviousCursor, selected);
                    }
                    else if (IsAuthorization(response.Error!))
                    { IsAccessDenied = true; _page = PageState.Empty; }
                    Notify();
                }
            }, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            await _ui.InvokeAsync(() =>
            {
                lock (_sync)
                {
                    if (!Current(operation)) return;
                    Error = new ApiError(ApiStatusCode.InternalServerError, "Desktop.UnexpectedError", "Unable to load sales."); Notify();
                }
            }, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await _ui.InvokeAsync(() =>
            {
                lock (_sync)
                {
                    if (!Owns(operation)) return;
                    _active = null; IsBusy = false; Notify();
                }
            }, CancellationToken.None).ConfigureAwait(false);
            cancellation.Dispose();
        }
    }
    private bool Owns(long operation) => !_disposed && operation == _operation && _session.IsAuthenticated && _session.Version == _sessionVersion;
    private bool Current(long operation) => Available && operation == _operation && _active?.IsCancellationRequested == false;
    private void SessionChanged(object? sender, EventArgs args)
    {
        lock (_sync) { ++_operation; _active?.Cancel(); _active = null; IsAccessDenied = true; }
        // Synchronous invalidation precedes UI clearing and fences all already queued callbacks.
        _ = _ui.InvokeAsync(() => { lock (_sync) { Clear(); } }, CancellationToken.None);
    }
    private void Clear()
    {
        _page = PageState.Empty; IsBusy = false; Error = null;
        PendingCriteria = new SalesReceiptListRequest(null, null, null, null, null, null, null, null);
        Notify();
    }
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true; ++_operation; _active?.Cancel(); _active = null;
            _session.SessionChanged -= SessionChanged; Clear();
        }
    }
    private void Notify()
    {
        foreach (var name in new[] { nameof(Items), nameof(AppliedCriteria), nameof(PendingCriteria), nameof(NextCursor), nameof(PreviousCursor), nameof(SelectedSaleId), nameof(IsBusy), nameof(IsAccessDenied), nameof(Error), nameof(CanNext), nameof(CanPrevious) })
            OnPropertyChanged(name);
    }
    internal static bool IsAuthorization(ApiError error) => error.Status == ApiStatusCode.Unauthorized || error.Status == ApiStatusCode.Forbidden;
    private static SalesReceiptListRequest Copy(SalesReceiptListRequest r, string? cursor) =>
        new SalesReceiptListRequest(r.FromDate, r.ToDate, r.CustomerId, r.WarehouseId, r.EmployeeId, r.Status, r.ProductId, r.Search, r.PageSize, cursor);
    private sealed class PageState
    {
        public static PageState Empty { get; } = new PageState(null, Array.Empty<SalesReceiptListRow>(), null, null, null);
        public PageState(SalesReceiptListRequest? criteria, IReadOnlyList<SalesReceiptListRow> items, string? next, string? previous, long? selected)
        { Criteria = criteria; Items = items; Next = next; Previous = previous; Selected = selected; }
        public SalesReceiptListRequest? Criteria { get; }
        public IReadOnlyList<SalesReceiptListRow> Items { get; }
        public string? Next { get; }
        public string? Previous { get; }
        public long? Selected { get; }
    }
}
