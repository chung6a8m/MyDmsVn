using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class InventoryViewModelTests
{
    [Fact]
    public async Task Balance_search_is_debounced_while_filters_and_refresh_are_immediate()
    {
        var api = new ScriptedInventoryApiClient();
        var delay = new ControllableDelay();
        using var viewModel = new StockBalanceViewModel(
            api,
            new ImmediateUiDispatcher(),
            new DesktopNotificationCenter(),
            delay,
            TimeSpan.FromMilliseconds(300));

        var first = viewModel.SetSearch("P");
        var second = viewModel.SetSearch("P-0");
        Assert.Empty(api.BalanceRequests);
        delay.ReleaseLatest();
        await Task.WhenAll(first, second);
        Assert.Single(api.BalanceRequests);
        Assert.Equal("P-0", api.BalanceRequests[0].Search);

        viewModel.PageNumber = 3;
        await viewModel.SetWarehouseFilterAsync(2, CancellationToken.None);
        await viewModel.SetProductFilterAsync(7, CancellationToken.None);
        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(4, api.BalanceRequests.Count);
        Assert.Equal(1, viewModel.PageNumber);
        Assert.Equal(2, api.BalanceRequests[1].WarehouseId);
        Assert.Equal(7, api.BalanceRequests[2].ProductId);
    }

    [Fact]
    public async Task Balance_discards_late_responses_surfaces_errors_and_ignores_work_after_dispose()
    {
        var api = new ControlledInventoryApiClient();
        var notifications = new DesktopNotificationCenter();
        var viewModel = new StockBalanceViewModel(
            api,
            new IgnoringCancellationDispatcher(),
            notifications,
            new ImmediateDelay());

        var oldLoad = viewModel.RefreshAsync(CancellationToken.None);
        var newLoad = viewModel.RefreshAsync(CancellationToken.None);
        api.CompleteBalance(1, Balance(2, 8, 9m));
        await newLoad;
        api.CompleteBalance(0, Balance(1, 7, 5m));
        await oldLoad;

        Assert.Equal(8, Assert.Single(viewModel.Items).ProductId);
        Assert.Equal(9m, viewModel.VisibleQuantityTotal);

        api.NextBalanceResponse = ApiResponse<PagedResult<StockBalanceRow>>.Failure(
            new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Denied."));
        await viewModel.RefreshAsync(CancellationToken.None);
        Assert.Empty(viewModel.Items);
        Assert.True(viewModel.IsAccessDenied);
        Assert.Equal("Denied.", viewModel.ErrorMessage);
        Assert.Equal("Denied.", notifications.LastNotification!.Message);

        var pending = viewModel.RefreshAsync(CancellationToken.None);
        viewModel.Dispose();
        api.CompleteLatestBalance(Balance(3, 9, 12m));
        await pending;
        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public async Task Stock_card_selection_date_and_refresh_are_immediate_and_discard_late_responses()
    {
        var api = new ControlledInventoryApiClient();
        using var viewModel = new StockCardViewModel(
            api,
            new IgnoringCancellationDispatcher(),
            new DesktopNotificationCenter());

        var oldSelection = viewModel.SetSelectionAsync(2, 7, CancellationToken.None);
        var newSelection = viewModel.SetSelectionAsync(3, 8, CancellationToken.None);
        Assert.Equal(2, api.CardRequests.Count);
        api.CompleteCard(1, Movement(2, 3, 8, 7m));
        await newSelection;
        api.CompleteCard(0, Movement(1, 2, 7, 5m));
        await oldSelection;
        Assert.Equal(2, Assert.Single(viewModel.Items).LedgerId);

        var fromUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var dateLoad = viewModel.SetDateRangeAsync(fromUtc, toUtc, CancellationToken.None);
        Assert.Equal(3, api.CardRequests.Count);
        api.CompleteCard(2, Movement(3, 3, 8, 2m));
        await dateLoad;
        Assert.Equal(fromUtc, api.CardRequests[2].FromUtc);
        Assert.Equal(toUtc, api.CardRequests[2].ToUtc);

        var refresh = viewModel.RefreshAsync(CancellationToken.None);
        Assert.Equal(4, api.CardRequests.Count);
        api.CompleteCard(3, Movement(4, 3, 8, 4m));
        await refresh;
        Assert.Equal(4m, viewModel.VisibleQuantityChangeTotal);
    }

    [Fact]
    public async Task Stock_card_clearing_selection_cancels_pending_read_and_clears_loading()
    {
        var api = new ControlledInventoryApiClient();
        using var viewModel = new StockCardViewModel(
            api,
            new IgnoringCancellationDispatcher(),
            new DesktopNotificationCenter());

        var pending = viewModel.SetSelectionAsync(2, 7, CancellationToken.None);
        Assert.True(viewModel.IsLoading);

        await viewModel.SetSelectionAsync(2, 0, CancellationToken.None);

        Assert.False(viewModel.IsLoading);
        Assert.Empty(viewModel.Items);
        Assert.Equal(0, viewModel.TotalCount);
        Assert.Single(api.CardRequests);

        api.CompleteCard(0, Movement(1, 2, 7, 5m));
        await pending;
        Assert.Empty(viewModel.Items);
    }

    private static StockBalanceRow Balance(int warehouseId, int productId, decimal quantity) =>
        new StockBalanceRow(
            warehouseId,
            "WH-" + warehouseId,
            "Warehouse " + warehouseId,
            true,
            productId,
            "P-" + productId,
            "Product " + productId,
            true,
            quantity);

    private static StockCardRow Movement(long ledgerId, int warehouseId, int productId, decimal quantity) =>
        new StockCardRow(
            ledgerId,
            warehouseId,
            "WH-" + warehouseId,
            "Warehouse " + warehouseId,
            true,
            productId,
            "P-" + productId,
            "Product " + productId,
            true,
            new DateTime(2026, 10, 12, 1, 2, 3, DateTimeKind.Utc),
            "GoodsReceipt",
            41,
            "GR0000000041",
            quantity);

    private sealed class ImmediateDelay : IAsyncDelay
    {
        public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class ControllableDelay : IAsyncDelay
    {
        private readonly List<TaskCompletionSource<bool>> _pending =
            new List<TaskCompletionSource<bool>>();

        public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => completion.TrySetCanceled());
            _pending.Add(completion);
            return completion.Task;
        }

        public void ReleaseLatest() => _pending[_pending.Count - 1].TrySetResult(true);
    }

    private sealed class ScriptedInventoryApiClient : IInventoryApiClient
    {
        public IList<StockBalanceQuery> BalanceRequests { get; } = new List<StockBalanceQuery>();

        public Task<ApiResponse<PagedResult<StockBalanceRow>>> GetStockBalancesAsync(
            StockBalanceQuery request,
            CancellationToken cancellationToken)
        {
            BalanceRequests.Add(request);
            return Task.FromResult(ApiResponse<PagedResult<StockBalanceRow>>.Success(
                new PagedResult<StockBalanceRow>(Array.Empty<StockBalanceRow>(), request.PageNumber, request.PageSize, 0)));
        }

        public Task<ApiResponse<PagedResult<StockCardRow>>> GetStockCardAsync(
            StockCardQuery request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ControlledInventoryApiClient : IInventoryApiClient
    {
        private readonly List<TaskCompletionSource<ApiResponse<PagedResult<StockBalanceRow>>>> _balances =
            new List<TaskCompletionSource<ApiResponse<PagedResult<StockBalanceRow>>>>();
        private readonly List<TaskCompletionSource<ApiResponse<PagedResult<StockCardRow>>>> _cards =
            new List<TaskCompletionSource<ApiResponse<PagedResult<StockCardRow>>>>();

        public IList<StockCardQuery> CardRequests { get; } = new List<StockCardQuery>();
        public ApiResponse<PagedResult<StockBalanceRow>>? NextBalanceResponse { get; set; }

        public Task<ApiResponse<PagedResult<StockBalanceRow>>> GetStockBalancesAsync(
            StockBalanceQuery request,
            CancellationToken cancellationToken)
        {
            if (NextBalanceResponse != null)
            {
                var response = NextBalanceResponse;
                NextBalanceResponse = null;
                return Task.FromResult(response);
            }

            var completion = new TaskCompletionSource<ApiResponse<PagedResult<StockBalanceRow>>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _balances.Add(completion);
            return completion.Task;
        }

        public Task<ApiResponse<PagedResult<StockCardRow>>> GetStockCardAsync(
            StockCardQuery request,
            CancellationToken cancellationToken)
        {
            CardRequests.Add(request);
            var completion = new TaskCompletionSource<ApiResponse<PagedResult<StockCardRow>>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _cards.Add(completion);
            return completion.Task;
        }

        public void CompleteBalance(int index, StockBalanceRow row) =>
            _balances[index].TrySetResult(ApiResponse<PagedResult<StockBalanceRow>>.Success(
                new PagedResult<StockBalanceRow>(new[] { row }, 1, 25, 1)));

        public void CompleteLatestBalance(StockBalanceRow row) =>
            CompleteBalance(_balances.Count - 1, row);

        public void CompleteCard(int index, StockCardRow row) =>
            _cards[index].TrySetResult(ApiResponse<PagedResult<StockCardRow>>.Success(
                new PagedResult<StockCardRow>(new[] { row }, 1, 25, 1)));
    }

    private sealed class IgnoringCancellationDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action, CancellationToken cancellationToken)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
