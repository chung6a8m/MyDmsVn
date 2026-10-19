using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class InventoryAcceptanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Paging_renders_response_order_and_inactive_history_and_filter_change_returns_to_first_page(bool card)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture(card);
            guard.Attach(fixture.Grid);
            fixture.Activate(token);

            Assert.Equal("Page 1 of 2", fixture.PageLabel.Text);
            Assert.False(fixture.Previous.Enabled);
            Assert.True(fixture.Next.Enabled);
            Assert.Equal(2, fixture.Grid.Rows.Count);
            Assert.Equal(card ? "GR0000000042" : "P42 - Historical product (inactive)", fixture.Grid.Rows[0].Cells[1].Value);
            Assert.Equal(card ? "GR0000000041" : "P41 - Historical product (inactive)", fixture.Grid.Rows[1].Cells[1].Value);
            Assert.Equal("W2 - Historical warehouse (inactive)", fixture.Grid.Rows[0].Cells[card ? 3 : 0].Value);
            if (card)
                Assert.Equal("P7 - Historical product (inactive)", fixture.Grid.Rows[0].Cells[4].Value);

            fixture.Next.PerformClick();
            Drain(fixture.LastOperation, token);
            Assert.Equal(2, fixture.LastPage);
            Assert.Equal("Page 2 of 2", fixture.PageLabel.Text);
            Assert.Single(fixture.Grid.Rows);
            Assert.True(fixture.Previous.Enabled);
            Assert.False(fixture.Next.Enabled);
            var requestsBefore = fixture.RequestCount;
            fixture.Next.PerformClick();
            Assert.Equal(requestsBefore, fixture.RequestCount);

            fixture.Previous.PerformClick();
            Drain(fixture.LastOperation, token);
            Assert.Equal(1, fixture.LastPage);
            fixture.Next.PerformClick();
            Drain(fixture.LastOperation, token);
            fixture.ProductFilter.Value = 8;
            Drain(fixture.LastOperation, token);
            Assert.Equal(1, fixture.LastPage);
            Assert.False(fixture.Previous.Enabled);
            Assert.True(fixture.Grid.ReadOnly);
            Assert.False(fixture.Grid.AllowUserToAddRows);
            Assert.False(fixture.Grid.AllowUserToDeleteRows);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_query_disables_grid_refresh_and_paging_then_recovers_after_completion(bool card)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture(card);
            guard.Attach(fixture.Grid);
            fixture.Activate(token);
            fixture.Api.HoldResponses = true;

            fixture.Refresh.PerformClick();
            var pending = fixture.LastOperation;
            Assert.False(pending.IsCompleted);
            Assert.False(fixture.Grid.Enabled);
            Assert.False(fixture.Refresh.Enabled);
            Assert.False(fixture.Next.Enabled);
            Assert.False(fixture.Previous.Enabled);
            var requestsBefore = fixture.RequestCount;
            fixture.Refresh.PerformClick();
            fixture.Next.PerformClick();
            Assert.Equal(requestsBefore, fixture.RequestCount);

            fixture.Complete(0);
            Drain(pending, token);
            Assert.True(fixture.Refresh.Enabled);
            Assert.True(fixture.Grid.Enabled);
            Assert.True(fixture.Next.Enabled);
            Assert.Equal("Ready", fixture.Status.Text);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false, ApiStatusCode.Forbidden)]
    [InlineData(true, ApiStatusCode.Forbidden)]
    [InlineData(false, ApiStatusCode.Unauthorized)]
    [InlineData(true, ApiStatusCode.Unauthorized)]
    public void Revoked_or_expired_read_clears_rows_totals_and_paging_and_can_refresh_after_access_returns(
        bool card, ApiStatusCode status)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture(card);
            guard.Attach(fixture.Grid);
            fixture.Activate(token);
            fixture.ProductFilter.Value = 7;
            Drain(fixture.LastOperation, token);
            Assert.NotEmpty(fixture.Grid.Rows);
            fixture.Api.Error = new ApiError(status, "Auth.AccessUnavailable", "Current identity cannot read inventory.");

            fixture.Refresh.PerformClick();
            Drain(fixture.LastOperation, token);

            Assert.Empty(fixture.Grid.Rows);
            Assert.Equal("0.0000", fixture.Total.Text);
            Assert.Equal("Current identity cannot read inventory.", fixture.Status.Text);
            Assert.False(fixture.Next.Enabled);
            Assert.True(fixture.IsAccessDenied);
            fixture.Api.Error = null;
            fixture.Refresh.PerformClick();
            Drain(fixture.LastOperation, token);
            Assert.False(fixture.IsAccessDenied);
            Assert.Equal("Ready", fixture.Status.Text);
            Assert.NotEmpty(fixture.Grid.Rows);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Late_success_from_canceled_filter_cannot_repopulate_inventory_after_newer_denial(bool card)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture(card);
            guard.Attach(fixture.Grid);
            fixture.Activate(token);
            fixture.Api.HoldResponses = true;
            fixture.Refresh.PerformClick();
            var oldOperation = fixture.LastOperation;
            fixture.ProductFilter.Value = 8;
            var newOperation = fixture.LastOperation;
            Assert.True(fixture.PendingToken(0).IsCancellationRequested);

            fixture.Complete(1, new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Read revoked."));
            Drain(newOperation, token);
            Assert.Empty(fixture.Grid.Rows);
            fixture.Complete(0);
            Drain(oldOperation, token);

            Assert.Empty(fixture.Grid.Rows);
            Assert.Equal("Read revoked.", fixture.Status.Text);
            Assert.True(fixture.IsAccessDenied);
            Assert.True(fixture.Refresh.Enabled);
            Assert.False(fixture.Next.Enabled);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disposed_inventory_control_cancels_query_and_ignores_late_rows_and_notifications(bool card)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture(card);
            guard.Attach(fixture.Grid);
            fixture.Activate(token);
            fixture.Api.HoldResponses = true;
            fixture.Refresh.PerformClick();
            var pending = fixture.LastOperation;
            var notification = fixture.Notifications.LastNotification;
            var changes = 0;
            fixture.ObserveChanges(() => changes++);

            fixture.DisposeControl();
            Assert.True(fixture.PendingToken(0).IsCancellationRequested);
            fixture.Complete(0, new ApiError(ApiStatusCode.InternalServerError, "Inventory.LateFailure", "Late failure."));
            Drain(pending, token);

            Assert.Equal(0, changes);
            Assert.Same(notification, fixture.Notifications.LastNotification);
            Assert.True(fixture.ControlIsDisposed);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validation_error_is_inline_clears_stale_rows_and_explicit_refresh_recovers(bool card)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture(card);
            guard.Attach(fixture.Grid);
            fixture.Activate(token);
            fixture.Api.Error = new ApiError(ApiStatusCode.BadRequest, "Inventory.InvalidRange", "Invalid inventory filter.");
            fixture.Refresh.PerformClick();
            Drain(fixture.LastOperation, token);

            Assert.Empty(fixture.Grid.Rows);
            Assert.Equal("Invalid inventory filter.", fixture.Status.Text);
            Assert.False(fixture.IsAccessDenied);
            Assert.True(fixture.Refresh.Enabled);
            fixture.Api.Error = null;
            fixture.Refresh.PerformClick();
            Drain(fixture.LastOperation, token);
            Assert.NotEmpty(fixture.Grid.Rows);
            Assert.Equal("Ready", fixture.Status.Text);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    private static void Drain(Task task, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(4))
        {
            token.ThrowIfCancellationRequested();
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }
        Assert.True(task.IsCompleted, "Controlled inventory operation failed to drain within four seconds.");
        task.GetAwaiter().GetResult();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly StockBalanceViewModel? _balances;
        private readonly StockCardViewModel? _card;
        private readonly StockBalanceControl? _balanceControl;
        private readonly StockCardControl? _cardControl;

        public Fixture(bool card)
        {
            if (card)
            {
                _card = new StockCardViewModel(Api, new ImmediateUiDispatcher(), Notifications) { PageSize = 2 };
                _cardControl = new StockCardControl(_card);
            }
            else
            {
                _balances = new StockBalanceViewModel(Api, new ImmediateUiDispatcher(), Notifications,
                    new ImmediateDelay()) { PageSize = 2 };
                _balanceControl = new StockBalanceControl(_balances);
            }
        }

        public ControlledApi Api { get; } = new ControlledApi();
        public DesktopNotificationCenter Notifications { get; } = new DesktopNotificationCenter();
        public DataGridView Grid => _cardControl?.StockCardGrid ?? _balanceControl!.BalanceGrid;
        public Button Refresh => _cardControl?.RefreshButton ?? _balanceControl!.RefreshButton;
        public Button Previous => _cardControl?.PreviousPageButton ?? _balanceControl!.PreviousPageButton;
        public Button Next => _cardControl?.NextPageButton ?? _balanceControl!.NextPageButton;
        public Label PageLabel => _cardControl?.PageLabel ?? _balanceControl!.PageLabel;
        public Label Total => _cardControl?.TotalLabel ?? _balanceControl!.TotalLabel;
        public Label Status => _cardControl?.OperationStatusLabel ?? _balanceControl!.OperationStatusLabel;
        public NumericUpDown ProductFilter => _cardControl?.ProductFilter ?? _balanceControl!.ProductFilter;
        public Task LastOperation => _cardControl?.LastOperation ?? _balanceControl!.LastOperation;
        public bool IsAccessDenied => _card?.IsAccessDenied ?? _balances!.IsAccessDenied;
        public int RequestCount => _card != null ? Api.CardRequests.Count : Api.BalanceRequests.Count;
        public int LastPage => _card != null ? Api.CardRequests[RequestCount - 1].PageNumber : Api.BalanceRequests[RequestCount - 1].PageNumber;
        public bool ControlIsDisposed => _cardControl?.IsDisposed ?? _balanceControl!.IsDisposed;

        public void Activate(CancellationToken token)
        {
            if (_cardControl != null)
            {
                _cardControl.WarehouseFilter.Value = 2;
                _cardControl.ProductFilter.Value = 7;
                Drain(_cardControl.LastOperation, token);
            }
            else Drain(_balanceControl!.ActivateAsync(token), token);
        }
        public void ObserveChanges(Action action)
        {
            if (_card != null) _card.PropertyChanged += (_, __) => action();
            else _balances!.PropertyChanged += (_, __) => action();
        }
        public CancellationToken PendingToken(int index) => _card != null
            ? Api.PendingCards[index].Token : Api.PendingBalances[index].Token;
        public void Complete(int index, ApiError? error = null)
        {
            if (_card != null)
            {
                var operation = Api.PendingCards[index];
                operation.Completion.SetResult(error == null ? CardResponse(operation.Page)
                    : ApiResponse<PagedResult<StockCardRow>>.Failure(error));
            }
            else
            {
                var operation = Api.PendingBalances[index];
                operation.Completion.SetResult(error == null ? BalanceResponse(operation.Page)
                    : ApiResponse<PagedResult<StockBalanceRow>>.Failure(error));
            }
        }
        public void DisposeControl()
        {
            _cardControl?.Dispose();
            _balanceControl?.Dispose();
        }
        public void Dispose()
        {
            DisposeControl();
            foreach (var pending in Api.PendingBalances) pending.Completion.TrySetCanceled();
            foreach (var pending in Api.PendingCards) pending.Completion.TrySetCanceled();
        }
    }

    private static ApiResponse<PagedResult<StockBalanceRow>> BalanceResponse(int page)
    {
        var rows = page == 1
            ? new[] { BalanceRow(42), BalanceRow(41) }
            : new[] { BalanceRow(43) };
        return ApiResponse<PagedResult<StockBalanceRow>>.Success(new PagedResult<StockBalanceRow>(rows, page, 2, 3));
    }
    private static StockBalanceRow BalanceRow(int product) => new StockBalanceRow(
        2, "W2", "Historical warehouse", false, product, "P" + product, "Historical product", false, product);
    private static ApiResponse<PagedResult<StockCardRow>> CardResponse(int page)
    {
        var rows = page == 1 ? new[] { CardRow(42), CardRow(41) } : new[] { CardRow(43) };
        return ApiResponse<PagedResult<StockCardRow>>.Success(new PagedResult<StockCardRow>(rows, page, 2, 3));
    }
    private static StockCardRow CardRow(int document) => new StockCardRow(
        document, 2, "W2", "Historical warehouse", false, 7, "P7", "Historical product", false,
        new DateTime(2026, 10, 19, 1, 2, 3, DateTimeKind.Utc), "GoodsReceipt", document,
        "GR" + document.ToString("D10"), document);

    private sealed class Pending<T>
    {
        public Pending(int page, CancellationToken token) { Page = page; Token = token; }
        public int Page { get; }
        public CancellationToken Token { get; }
        // Completed by the owning STA to exercise timing without background control access.
        public TaskCompletionSource<ApiResponse<PagedResult<T>>> Completion { get; } =
            new TaskCompletionSource<ApiResponse<PagedResult<T>>>();
    }
    private sealed class ImmediateDelay : IAsyncDelay
    {
        public Task DelayAsync(TimeSpan interval, CancellationToken token) => Task.CompletedTask;
    }
    private sealed class ControlledApi : IInventoryApiClient
    {
        public bool HoldResponses { get; set; }
        public ApiError? Error { get; set; }
        public List<StockBalanceQuery> BalanceRequests { get; } = new List<StockBalanceQuery>();
        public List<StockCardQuery> CardRequests { get; } = new List<StockCardQuery>();
        public List<Pending<StockBalanceRow>> PendingBalances { get; } = new List<Pending<StockBalanceRow>>();
        public List<Pending<StockCardRow>> PendingCards { get; } = new List<Pending<StockCardRow>>();
        public Task<ApiResponse<PagedResult<StockBalanceRow>>> GetStockBalancesAsync(StockBalanceQuery query, CancellationToken token)
        {
            BalanceRequests.Add(query);
            if (!HoldResponses) return Task.FromResult(Error == null ? BalanceResponse(query.PageNumber)
                : ApiResponse<PagedResult<StockBalanceRow>>.Failure(Error));
            var pending = new Pending<StockBalanceRow>(query.PageNumber, token);
            PendingBalances.Add(pending);
            return pending.Completion.Task;
        }
        public Task<ApiResponse<PagedResult<StockCardRow>>> GetStockCardAsync(StockCardQuery query, CancellationToken token)
        {
            CardRequests.Add(query);
            if (!HoldResponses) return Task.FromResult(Error == null ? CardResponse(query.PageNumber)
                : ApiResponse<PagedResult<StockCardRow>>.Failure(Error));
            var pending = new Pending<StockCardRow>(query.PageNumber, token);
            PendingCards.Add(pending);
            return pending.Completion.Task;
        }
    }
}
