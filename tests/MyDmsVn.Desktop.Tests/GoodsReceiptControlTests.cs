using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class GoodsReceiptControlTests
    {
        [Fact]
        public void Activation_and_selection_render_master_detail_lookups_total_and_read_only_state()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var guard = new WinFormsTestGuard())
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();

                        Assert.Equal(2, control.ReceiptGrid.RowsCount);
                        Assert.Equal("GR0000000041", control.ReceiptGrid[1, 1].Value);
                        Assert.Equal(2, control.LineGrid.RowsCount);
                        Assert.Equal("P7 - Product 7", control.LineGrid[1, 1].Value);
                        Assert.Equal("10.00", control.TotalLabel.Text);
                        Assert.Equal("Posted", control.StatusLabel.Text);
                        Assert.Equal("AQIDBAUGBwg=", control.VersionLabel.Text);
                        Assert.Single(
                            Assert.IsAssignableFrom<IEnumerable<CatalogLookupOption>>(
                                control.WarehouseLookup.DataSource));
                        Assert.Single(
                            Assert.IsAssignableFrom<IEnumerable<CatalogLookupOption>>(
                                control.ProductLookup.DataSource));
                        Assert.False(control.SaveButton.Enabled);
                        Assert.False(control.AddLineButton.Enabled);
                        Assert.Empty(guard.Exceptions);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Draft_commands_update_lines_and_validation_errors_stay_inline()
        {
            StaTest.Run(
                _ =>
                {
                    using (var guard = new WinFormsTestGuard())
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        control.NewButton.PerformClick();
                        control.AddLineButton.PerformClick();
                        Assert.Single(fixture.Editor.Lines);
                        Assert.Equal(2, control.LineGrid.RowsCount);
                        control.RemoveLineButton.PerformClick();
                        Assert.Empty(fixture.Editor.Lines);
                        control.AddLineButton.PerformClick();

                        control.SaveButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal("Warehouse is required.", control.GetFieldError("warehouseId"));
                        Assert.Equal("Employee is required.", control.GetFieldError("employeeId"));
                        Assert.Equal("Product is required.", control.GetFieldError("lines[0].productId"));
                        Assert.Empty(guard.Exceptions);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void In_flight_activation_disables_commands_until_the_list_finishes()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        fixture.Api.ControlListResponse = true;

                        var activation = control.ActivateAsync(cancellationToken);

                        Assert.False(control.RefreshButton.Enabled);
                        Assert.False(control.ReceiptGrid.Enabled);
                        Assert.True(control.BusyIndicator.Visible);
                        fixture.Api.CompleteList();
                        PumpMessagesUntil(() => activation.IsCompleted, TimeSpan.FromSeconds(2));
                        activation.GetAwaiter().GetResult();
                        Assert.True(control.RefreshButton.Enabled);
                        Assert.False(control.BusyIndicator.Visible);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Operation_errors_render_in_the_non_modal_status_area()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        fixture.Api.ListError = new ApiError(
                            ApiStatusCode.Forbidden,
                            "Auth.Forbidden",
                            "Denied.");

                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();

                        Assert.Equal("Denied.", control.OperationStatusLabel.Text);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Paging_and_explicit_filters_refresh_immediately()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        Assert.Equal("Page 1 of 2", control.PageLabel.Text);
                        Assert.True(control.NextPageButton.Enabled);

                        control.NextPageButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();
                        Assert.Equal(2, fixture.Api.ListRequests.Last().PageNumber);
                        Assert.Equal("Page 2 of 2", control.PageLabel.Text);

                        control.WarehouseFilterLookup.SelectValue(3);
                        control.LastOperation.GetAwaiter().GetResult();
                        Assert.Equal(3, fixture.Api.ListRequests.Last().WarehouseId);
                        Assert.Equal(1, fixture.Api.ListRequests.Last().PageNumber);

                        control.StatusFilter.SelectedItem = "Posted";
                        control.LastOperation.GetAwaiter().GetResult();
                        Assert.Equal("Posted", fixture.Api.ListRequests.Last().Status);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Repeated_activation_refreshes_and_disposal_owns_both_view_models()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var fixture = new Fixture();
                    var control = new GoodsReceiptControl(fixture.List, fixture.Editor);
                    control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                    control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                    Assert.Equal(2, fixture.Api.ListCalls);

                    control.Dispose();

                    Assert.Throws<ObjectDisposedException>(
                        (Action)(() => _ = fixture.List.RefreshAsync(CancellationToken.None)));
                    Assert.Throws<ObjectDisposedException>(() => fixture.Editor.AddLine());
                },
                TimeSpan.FromSeconds(10));
        }

        private sealed class Fixture : IDisposable
        {
            public Fixture()
            {
                Api = new ReceiptApiClient();
                var messenger = new WeakReferenceMessenger();
                var notifications = new DesktopNotificationCenter();
                var dispatcher = new ImmediateUiDispatcher();
                var delay = new ImmediateDelay();
                List = new GoodsReceiptListViewModel(Api, dispatcher, notifications, delay);
                Editor = new GoodsReceiptEditorViewModel(
                    Api,
                    new CatalogLookupViewModel(new LookupSource(CatalogKind.Warehouse), messenger, dispatcher, notifications, delay),
                    new CatalogLookupViewModel(new LookupSource(CatalogKind.Employee), messenger, dispatcher, notifications, delay),
                    new GoodsReceiptProductOptionsViewModel(new LookupSource(CatalogKind.Product), messenger, dispatcher, notifications, delay),
                    notifications);
            }

            public ReceiptApiClient Api { get; }
            public GoodsReceiptListViewModel List { get; }
            public GoodsReceiptEditorViewModel Editor { get; }

            public void Dispose()
            {
                List.Dispose();
                Editor.Dispose();
            }
        }

        private sealed class ImmediateDelay : IAsyncDelay
        {
            public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken) =>
                Task.CompletedTask;
        }

        private sealed class LookupSource : ICatalogLookupSource
        {
            public LookupSource(CatalogKind kind) => Kind = kind;
            public CatalogKind Kind { get; }

            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(
                CatalogLookupRequest request,
                CancellationToken cancellationToken) =>
                Task.FromResult(
                    ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(
                        new[] { new CatalogLookupDto(Kind == CatalogKind.Product ? 7 : 3, Kind.ToString(), "Choice", true) }));

            public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken cancellationToken) =>
                Task.FromResult(ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(id, "C" + id, "Choice", true)));
        }

        private sealed class ReceiptApiClient : IGoodsReceiptApiClient
        {
            private TaskCompletionSource<ApiResponse<PagedResult<GoodsReceiptDto>>>? _listCompletion;

            public int ListCalls { get; private set; }
            public bool ControlListResponse { get; set; }
            public ApiError? ListError { get; set; }
            public IList<GoodsReceiptListRequest> ListRequests { get; } =
                new List<GoodsReceiptListRequest>();

            public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(
                GoodsReceiptListRequest request,
                CancellationToken cancellationToken)
            {
                ListCalls++;
                ListRequests.Add(request);
                if (ListError != null)
                {
                    return Task.FromResult(
                        ApiResponse<PagedResult<GoodsReceiptDto>>.Failure(ListError));
                }
                var response = ApiResponse<PagedResult<GoodsReceiptDto>>.Success(
                    new PagedResult<GoodsReceiptDto>(new[] { Receipt() }, request.PageNumber, 25, 30));
                if (!ControlListResponse)
                {
                    return Task.FromResult(response);
                }

                _listCompletion = new TaskCompletionSource<ApiResponse<PagedResult<GoodsReceiptDto>>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                return _listCompletion.Task;
            }

            public void CompleteList()
            {
                _listCompletion!.TrySetResult(
                    ApiResponse<PagedResult<GoodsReceiptDto>>.Success(
                        new PagedResult<GoodsReceiptDto>(new[] { Receipt() }, 1, 25, 30)));
            }

            public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken cancellationToken) =>
                Task.FromResult(ApiResponse<GoodsReceiptDto>.Success(Receipt()));

            public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(
                SaveGoodsReceiptRequest request,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(
                UpdateGoodsReceiptDraftRequest request,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(
                PostGoodsReceiptRequest request,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            private static GoodsReceiptDto Receipt() =>
                new GoodsReceiptDto(
                    41,
                    "GR0000000041",
                    new DateTime(2026, 10, 10),
                    3,
                    5,
                    "Posted",
                    "received",
                    new DateTime(2026, 10, 10, 1, 2, 3, DateTimeKind.Utc),
                    "AQIDBAUGBwg=",
                    new[] { new GoodsReceiptLineDto(91, 1, 7, 2.5m, 4m, "P7 - Product 7", true) },
                    "W3 - Main",
                    true,
                    "E5 - Operator",
                    true);
        }

        private static void PumpMessagesUntil(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }

            Assert.True(condition(), "Expected UI update did not arrive before the timeout.");
        }
    }
}
