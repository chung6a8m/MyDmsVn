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
        public async Task Search_change_invalidates_an_old_response_before_the_debounce_starts()
        {
            var api = new ControlledApiClient();
            var delay = new ControllableDelay();
            using (var viewModel = new GoodsReceiptListViewModel(
                api,
                new IgnoringCancellationDispatcher(),
                new DesktopNotificationCenter(),
                delay,
                TimeSpan.FromMilliseconds(300)))
            {
                var oldLoad = viewModel.RefreshAsync(CancellationToken.None);
                var search = viewModel.SetSearch("NEW");

                api.CompleteList(0, Receipt(1, "OLD"));
                await oldLoad;

                Assert.Empty(viewModel.Items);

                delay.ReleaseLatest();
                await WaitUntilAsync(() => api.ListCalls == 2);
                api.CompleteList(1, Receipt(2, "NEW"));
                await search;
                Assert.Equal(2, Assert.Single(viewModel.Items).Id);
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

        [Fact]
        public async Task Authorization_failures_clear_cached_list_and_detail_data()
        {
            var api = new ScriptedApiClient
            {
                ListResponse = ApiResponse<PagedResult<GoodsReceiptDto>>.Success(
                    new PagedResult<GoodsReceiptDto>(new[] { Receipt(41, "GR41") }, 1, 25, 1)),
            };
            using (var viewModel = Create(api, new ImmediateDelay()))
            {
                await viewModel.ActivateAsync(CancellationToken.None);
                await viewModel.SelectAsync(41, CancellationToken.None);
                Assert.NotEmpty(viewModel.Items);
                Assert.NotNull(viewModel.SelectedReceipt);

                api.GetResponse = ApiResponse<GoodsReceiptDto>.Failure(
                    new ApiError(ApiStatusCode.Unauthorized, "Auth.Unauthorized", "Sign in again."));
                await viewModel.SelectAsync(99, CancellationToken.None);
                Assert.Null(viewModel.SelectedReceipt);

                await viewModel.SelectAsync(41, CancellationToken.None);
                api.GetResponse = ApiResponse<GoodsReceiptDto>.Success(Receipt(41, "GR41"));
                await viewModel.SelectAsync(41, CancellationToken.None);
                api.ListResponse = ApiResponse<PagedResult<GoodsReceiptDto>>.Failure(
                    new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Denied."));
                await viewModel.RefreshAsync(CancellationToken.None);

                Assert.Empty(viewModel.Items);
                Assert.Equal(0, viewModel.TotalCount);
                Assert.Null(viewModel.SelectedReceipt);
            }
        }

        [Fact]
        public async Task Unexpected_list_and_detail_exceptions_surface_without_faulting_ui_tasks()
        {
            var api = new ScriptedApiClient
            {
                ListException = new InvalidOperationException("List connection failed."),
            };
            using (var viewModel = Create(api, new ImmediateDelay()))
            {
                await viewModel.ActivateAsync(CancellationToken.None);
                Assert.Matches(
                    "^An unexpected error occurred\\. Support ID: [a-f0-9]{32}$",
                    viewModel.ErrorMessage!);

                api.ListException = null;
                api.GetException = new InvalidOperationException("Detail connection failed.");
                var selected = await viewModel.SelectAsync(41, CancellationToken.None);

                Assert.Null(selected);
                Assert.Matches(
                    "^An unexpected error occurred\\. Support ID: [a-f0-9]{32}$",
                    viewModel.ErrorMessage!);
            }
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

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow >= timeout)
                {
                    throw new TimeoutException();
                }

                await Task.Delay(10);
            }
        }

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
            public ApiResponse<GoodsReceiptDto>? GetResponse { get; set; }
            public Exception? ListException { get; set; }
            public Exception? GetException { get; set; }

            public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(
                GoodsReceiptListRequest request,
                CancellationToken cancellationToken)
            {
                ListRequests.Add(request);
                if (ListException != null)
                {
                    throw ListException;
                }
                return Task.FromResult(ListResponse);
            }

            public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken token)
            {
                if (GetException != null)
                {
                    throw GetException;
                }

                return Task.FromResult(
                    GetResponse ?? ApiResponse<GoodsReceiptDto>.Success(Receipt(id, "GR")));
            }
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

            public int ListCalls => _lists.Count;

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

        private sealed class IgnoringCancellationDispatcher : IUiDispatcher
        {
            public Task InvokeAsync(Action action, CancellationToken cancellationToken)
            {
                action();
                return Task.CompletedTask;
            }
        }
    }
}
