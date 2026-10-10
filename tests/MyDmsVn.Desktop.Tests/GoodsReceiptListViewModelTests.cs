using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class GoodsReceiptListViewModelTests
    {
        [Fact]
        public async Task Search_is_debounced_while_filters_refresh_immediately_and_reset_page()
        {
            var api = new ScriptedApiClient();
            var delay = new ControllableDelay();
            using (var viewModel = Create(api, delay))
            {
                var first = viewModel.SetSearch("G");
                var second = viewModel.SetSearch("GR");
                Assert.Empty(api.ListRequests);

                delay.ReleaseLatest();
                await Task.WhenAll(first, second);
                Assert.Single(api.ListRequests);
                Assert.Equal("GR", api.ListRequests[0].Search);

                viewModel.PageNumber = 3;
                await viewModel.SetWarehouseFilterAsync(7, CancellationToken.None);
                await viewModel.SetStatusFilterAsync("Draft", CancellationToken.None);
                await viewModel.RefreshAsync(CancellationToken.None);

                Assert.Equal(4, api.ListRequests.Count);
                Assert.Equal(0, viewModel.TotalCount);
                Assert.Equal(1, viewModel.PageNumber);
                Assert.Equal(7, api.ListRequests[1].WarehouseId);
                Assert.Equal("Draft", api.ListRequests[2].Status);
            }
        }

        [Fact]
        public async Task Late_old_list_and_get_responses_cannot_replace_newer_state()
        {
            var api = new ControlledApiClient();
            using (var viewModel = Create(api, new ImmediateDelay()))
            {
                var oldList = viewModel.RefreshAsync(CancellationToken.None);
                var newList = viewModel.RefreshAsync(CancellationToken.None);
                api.CompleteList(1, Receipt(2, "NEW"));
                await newList;
                api.CompleteList(0, Receipt(1, "OLD"));
                await oldList;
                Assert.Equal(2, Assert.Single(viewModel.Items).Id);

                var oldGet = viewModel.SelectAsync(1, CancellationToken.None);
                var newGet = viewModel.SelectAsync(2, CancellationToken.None);
                api.CompleteGet(1, Receipt(2, "NEW"));
                await newGet;
                api.CompleteGet(0, Receipt(1, "OLD"));
                await oldGet;
                Assert.Equal(2, viewModel.SelectedReceipt!.Id);
            }
        }

        [Fact]
        public async Task Activation_fetches_fresh_data_errors_surface_and_disposal_ignores_late_work()
        {
            var notifications = new DesktopNotificationCenter();
            var api = new ScriptedApiClient
            {
                ListResponse = ApiResponse<PagedResult<GoodsReceiptDto>>.Failure(
                    new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Denied.")),
            };
            var viewModel = new GoodsReceiptListViewModel(
                api,
                new ImmediateUiDispatcher(),
                notifications,
                new ImmediateDelay());

            await viewModel.ActivateAsync(CancellationToken.None);
            Assert.Equal("Denied.", viewModel.ErrorMessage);
            Assert.Equal("Denied.", notifications.LastNotification!.Message);

            var controlled = new ControlledApiClient();
            var disposed = Create(controlled, new ImmediateDelay());
            var pending = disposed.RefreshAsync(CancellationToken.None);
            disposed.Dispose();
            controlled.CompleteList(0, Receipt(3, "LATE"));
            await pending;
            Assert.Empty(disposed.Items);
            viewModel.Dispose();
        }

        private static GoodsReceiptListViewModel Create(
            IGoodsReceiptApiClient api,
            IAsyncDelay delay) =>
            new GoodsReceiptListViewModel(
                api,
                new ImmediateUiDispatcher(),
                new DesktopNotificationCenter(),
                delay,
                TimeSpan.FromMilliseconds(300));

        private static GoodsReceiptDto Receipt(long id, string number) =>
            new GoodsReceiptDto(
                id,
                number,
                new DateTime(2026, 10, 10),
                3,
                5,
                "Draft",
                null,
                null,
                Convert.ToBase64String(new byte[8]),
                Array.Empty<GoodsReceiptLineDto>());

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
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => completion.TrySetCanceled());
                _pending.Add(completion);
                return completion.Task;
            }

            public void ReleaseLatest() => _pending[_pending.Count - 1].TrySetResult(true);
        }

        private sealed class ScriptedApiClient : IGoodsReceiptApiClient
        {
            public IList<GoodsReceiptListRequest> ListRequests { get; } =
                new List<GoodsReceiptListRequest>();

            public ApiResponse<PagedResult<GoodsReceiptDto>> ListResponse { get; set; } =
                ApiResponse<PagedResult<GoodsReceiptDto>>.Success(
                    new PagedResult<GoodsReceiptDto>(Array.Empty<GoodsReceiptDto>(), 1, 25, 0));

            public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(
                GoodsReceiptListRequest request,
                CancellationToken cancellationToken)
            {
                ListRequests.Add(request);
                return Task.FromResult(ListResponse);
            }

            public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken token) =>
                Task.FromResult(ApiResponse<GoodsReceiptDto>.Success(Receipt(id, "GR")));
            public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(SaveGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(UpdateGoodsReceiptDraftRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(PostGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
        }

        private sealed class ControlledApiClient : IGoodsReceiptApiClient
        {
            private readonly List<TaskCompletionSource<ApiResponse<PagedResult<GoodsReceiptDto>>>> _lists =
                new List<TaskCompletionSource<ApiResponse<PagedResult<GoodsReceiptDto>>>>();
            private readonly List<TaskCompletionSource<ApiResponse<GoodsReceiptDto>>> _gets =
                new List<TaskCompletionSource<ApiResponse<GoodsReceiptDto>>>();

            public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(GoodsReceiptListRequest request, CancellationToken token)
            {
                var completion = new TaskCompletionSource<ApiResponse<PagedResult<GoodsReceiptDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
                _lists.Add(completion);
                return completion.Task;
            }

            public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken token)
            {
                var completion = new TaskCompletionSource<ApiResponse<GoodsReceiptDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
                _gets.Add(completion);
                return completion.Task;
            }

            public void CompleteList(int index, GoodsReceiptDto receipt) =>
                _lists[index].TrySetResult(ApiResponse<PagedResult<GoodsReceiptDto>>.Success(
                    new PagedResult<GoodsReceiptDto>(new[] { receipt }, 1, 25, 1)));
            public void CompleteGet(int index, GoodsReceiptDto receipt) =>
                _gets[index].TrySetResult(ApiResponse<GoodsReceiptDto>.Success(receipt));
            public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(SaveGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(UpdateGoodsReceiptDraftRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(PostGoodsReceiptRequest request, CancellationToken token) => throw new NotSupportedException();
        }
    }
}
