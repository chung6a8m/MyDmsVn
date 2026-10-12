using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class InventoryControlTests
{
    [Fact]
    public void Balance_control_renders_history_totals_and_runs_explicit_filters_immediately()
    {
        StaTest.Run(
            cancellationToken =>
            {
                var api = new InventoryApiClient();
                using var guard = new WinFormsTestGuard();
                using var viewModel = new StockBalanceViewModel(
                    api,
                    new ImmediateUiDispatcher(),
                    new DesktopNotificationCenter(),
                    new ImmediateDelay());
                using var control = new StockBalanceControl(viewModel);

                control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();

                Assert.Single(control.BalanceGrid.Rows.Cast<System.Windows.Forms.DataGridViewRow>());
                Assert.Equal("WH-02 - Archived warehouse (inactive)",
                    control.BalanceGrid.Rows[0].Cells[0].Value);
                Assert.Equal("P-007 - Archived product (inactive)",
                    control.BalanceGrid.Rows[0].Cells[1].Value);
                Assert.Equal("12.5000", control.TotalLabel.Text);

                control.WarehouseFilter.Value = 2;
                control.LastOperation.GetAwaiter().GetResult();
                control.ProductFilter.Value = 7;
                control.LastOperation.GetAwaiter().GetResult();
                control.RefreshButton.PerformClick();
                control.LastOperation.GetAwaiter().GetResult();

                Assert.Equal(4, api.BalanceRequests.Count);
                Assert.Equal(2, api.BalanceRequests[1].WarehouseId);
                Assert.Equal(7, api.BalanceRequests[2].ProductId);
                Assert.Empty(guard.Exceptions);
            },
            TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Stock_card_control_filters_without_debounce_and_reports_errors_without_dialogs()
    {
        StaTest.Run(
            _ =>
            {
                var api = new InventoryApiClient();
                var notifications = new DesktopNotificationCenter();
                using var guard = new WinFormsTestGuard();
                using var viewModel = new StockCardViewModel(
                    api,
                    new ImmediateUiDispatcher(),
                    notifications);
                using var control = new StockCardControl(viewModel);

                control.WarehouseFilter.Value = 2;
                control.ProductFilter.Value = 7;
                control.LastOperation.GetAwaiter().GetResult();

                Assert.Single(api.CardRequests);
                Assert.Single(control.StockCardGrid.Rows.Cast<System.Windows.Forms.DataGridViewRow>());
                Assert.Equal("GR0000000041", control.StockCardGrid.Rows[0].Cells[1].Value);
                Assert.Equal("5.0000", control.TotalLabel.Text);

                control.ProductFilter.Value = 0;
                control.LastOperation.GetAwaiter().GetResult();

                Assert.Single(api.CardRequests);
                Assert.Empty(control.StockCardGrid.Rows.Cast<System.Windows.Forms.DataGridViewRow>());
                Assert.Equal("Select warehouse and product.", control.OperationStatusLabel.Text);

                control.ProductFilter.Value = 7;
                control.LastOperation.GetAwaiter().GetResult();

                api.CardError = new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Denied.");
                control.RefreshButton.PerformClick();
                control.LastOperation.GetAwaiter().GetResult();

                Assert.Equal("Denied.", control.OperationStatusLabel.Text);
                Assert.Empty(guard.Exceptions);
            },
            TimeSpan.FromSeconds(10));
    }

    private sealed class ImmediateDelay : IAsyncDelay
    {
        public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class InventoryApiClient : IInventoryApiClient
    {
        public IList<StockBalanceQuery> BalanceRequests { get; } = new List<StockBalanceQuery>();
        public IList<StockCardQuery> CardRequests { get; } = new List<StockCardQuery>();
        public ApiError? CardError { get; set; }

        public Task<ApiResponse<PagedResult<StockBalanceRow>>> GetStockBalancesAsync(
            StockBalanceQuery request,
            CancellationToken cancellationToken)
        {
            BalanceRequests.Add(request);
            var row = new StockBalanceRow(
                2,
                "WH-02",
                "Archived warehouse",
                false,
                7,
                "P-007",
                "Archived product",
                false,
                12.5m);
            return Task.FromResult(ApiResponse<PagedResult<StockBalanceRow>>.Success(
                new PagedResult<StockBalanceRow>(new[] { row }, request.PageNumber, request.PageSize, 1)));
        }

        public Task<ApiResponse<PagedResult<StockCardRow>>> GetStockCardAsync(
            StockCardQuery request,
            CancellationToken cancellationToken)
        {
            CardRequests.Add(request);
            if (CardError != null)
            {
                return Task.FromResult(ApiResponse<PagedResult<StockCardRow>>.Failure(CardError));
            }

            var row = new StockCardRow(
                91,
                2,
                "WH-02",
                "Archived warehouse",
                false,
                7,
                "P-007",
                "Archived product",
                false,
                new DateTime(2026, 10, 12, 1, 2, 3, DateTimeKind.Utc),
                "GoodsReceipt",
                41,
                "GR0000000041",
                5m);
            return Task.FromResult(ApiResponse<PagedResult<StockCardRow>>.Success(
                new PagedResult<StockCardRow>(new[] { row }, request.PageNumber, request.PageSize, 1)));
        }
    }
}
