using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class GoodsReceiptProductOptionsViewModelTests
    {
        [Fact]
        public async Task Product_message_bursts_coalesce_and_unrelated_messages_are_ignored()
        {
            var source = new ProductSource();
            var messenger = new WeakReferenceMessenger();
            var delay = new ControllableDelay();
            using (var viewModel = Create(source, messenger, delay))
            {
                messenger.Send(new CatalogChangedMessage(CatalogKind.Warehouse, 1, CatalogChangeOperation.Updated));
                messenger.Send(new CatalogChangedMessage(CatalogKind.Product, 1, CatalogChangeOperation.Updated));
                messenger.Send(new CatalogChangedMessage(CatalogKind.Product, 2, CatalogChangeOperation.ActiveStatusChanged));

                Assert.Equal(2, delay.PendingCount);
                delay.ReleaseLatest();
                await WaitUntilAsync(() => viewModel.Items.Count == 1);
                Assert.Equal(new[] { 1 }, viewModel.Items.Select(item => item.Id));
            }
        }

        [Fact]
        public async Task Search_is_debounced_activation_is_immediate_and_disposal_stops_messages()
        {
            var source = new ProductSource();
            var messenger = new WeakReferenceMessenger();
            var delay = new ControllableDelay();
            var viewModel = Create(source, messenger, delay);

            await viewModel.ActivateAsync(CancellationToken.None);
            Assert.Equal(1, source.Calls);
            var first = viewModel.SetSearch("P");
            var second = viewModel.SetSearch("P1");
            delay.ReleaseLatest();
            await Task.WhenAll(first, second);
            Assert.Equal(2, source.Calls);
            Assert.Equal("P1", source.Requests.Last().Search);

            viewModel.Dispose();
            messenger.Send(new CatalogChangedMessage(CatalogKind.Product, 1, CatalogChangeOperation.Updated));
            Assert.Equal(2, source.Calls);
        }

        [Fact]
        public async Task Search_change_invalidates_an_old_product_response_before_debounce_starts()
        {
            var source = new ControlledProductSource();
            var delay = new ControllableDelay();
            using (var viewModel = new GoodsReceiptProductOptionsViewModel(
                source,
                new WeakReferenceMessenger(),
                new IgnoringCancellationDispatcher(),
                new DesktopNotificationCenter(),
                delay,
                TimeSpan.FromMilliseconds(300)))
            {
                var oldLoad = viewModel.ActivateAsync(CancellationToken.None);
                var search = viewModel.SetSearch("NEW");

                source.Complete(0, new CatalogLookupDto(1, "OLD", "Old", true));
                await oldLoad;

                Assert.Empty(viewModel.Items);

                delay.ReleaseLatest();
                await WaitUntilAsync(() => source.Calls == 2);
                source.Complete(1, new CatalogLookupDto(2, "NEW", "New", true));
                await search;
                Assert.Equal(2, Assert.Single(viewModel.Items).Id);
            }
        }

        [Fact]
        public async Task Deactivation_message_marks_tracked_line_unavailable_without_losing_its_label()
        {
            var source = new ProductSource();
            var messenger = new WeakReferenceMessenger();
            var delay = new ControllableDelay();
            using (var viewModel = Create(source, messenger, delay))
            {
                var line = new GoodsReceiptLineViewModel();
                line.SetProduct(1, "P1 - Historical label", true);
                viewModel.TrackLines(new[] { line });
                source.ProductIsActive = false;

                messenger.Send(new CatalogChangedMessage(
                    CatalogKind.Product,
                    1,
                    CatalogChangeOperation.ActiveStatusChanged));
                delay.ReleaseLatest();
                await WaitUntilAsync(() => !line.ProductIsAvailable);

                Assert.Equal(1, line.ProductId);
                Assert.Equal("P1 - Historical label", line.ProductDisplayName);
            }
        }

        [Fact]
        public async Task Tracked_product_outside_the_active_page_remains_a_historical_option()
        {
            var source = new ProductSource();
            var messenger = new WeakReferenceMessenger();
            using (var viewModel = Create(source, messenger, new ImmediateDelay()))
            {
                await viewModel.ActivateAsync(CancellationToken.None);
                var line = new GoodsReceiptLineViewModel();
                line.SetProduct(99, "P99 - Historical product", false);

                viewModel.TrackLines(new[] { line });

                var option = Assert.Single(viewModel.Items, item => item.Id == 99);
                Assert.Equal("P99 - Historical product", option.DisplayName);
                Assert.False(option.IsAvailableForNewSelection);
            }
        }

        [Fact]
        public async Task Message_triggered_lookup_exception_surfaces_without_faulting_fire_and_forget_work()
        {
            var source = new ProductSource
            {
                LookupException = new InvalidOperationException("Product lookup failed."),
            };
            var messenger = new WeakReferenceMessenger();
            using (var viewModel = Create(source, messenger, new ImmediateDelay()))
            {
                messenger.Send(new CatalogChangedMessage(
                    CatalogKind.Product,
                    1,
                    CatalogChangeOperation.Updated));

                await WaitUntilAsync(() => viewModel.ErrorMessage != null);
                Assert.Equal("Product lookup failed.", viewModel.ErrorMessage);
            }
        }

        private static GoodsReceiptProductOptionsViewModel Create(
            ICatalogLookupSource source,
            IMessenger messenger,
            IAsyncDelay delay) =>
            new GoodsReceiptProductOptionsViewModel(
                source,
                messenger,
                new ImmediateUiDispatcher(),
                new DesktopNotificationCenter(),
                delay,
                TimeSpan.FromMilliseconds(300));

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

        private sealed class ProductSource : ICatalogLookupSource
        {
            public CatalogKind Kind => CatalogKind.Product;
            public int Calls { get; private set; }
            public bool ProductIsActive { get; set; } = true;
            public Exception? LookupException { get; set; }
            public IList<CatalogLookupRequest> Requests { get; } = new List<CatalogLookupRequest>();

            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(
                CatalogLookupRequest request,
                CancellationToken cancellationToken)
            {
                Calls++;
                Requests.Add(request);
                if (LookupException != null)
                {
                    throw LookupException;
                }
                return Task.FromResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(
                    new[]
                    {
                        new CatalogLookupDto(1, "P1", "Active", true),
                        new CatalogLookupDto(2, "P2", "Inactive", false),
                    }));
            }

            public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken cancellationToken) =>
                Task.FromResult(ApiResponse<CatalogLookupDto>.Success(
                    new CatalogLookupDto(id, "P" + id, "Current", ProductIsActive)));
        }

        private sealed class ControlledProductSource : ICatalogLookupSource
        {
            private readonly List<TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>> _loads =
                new List<TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>>();

            public CatalogKind Kind => CatalogKind.Product;
            public int Calls => _loads.Count;

            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(
                CatalogLookupRequest request,
                CancellationToken cancellationToken)
            {
                var completion = new TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _loads.Add(completion);
                return completion.Task;
            }

            public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken cancellationToken) =>
                Task.FromResult(ApiResponse<CatalogLookupDto>.Success(
                    new CatalogLookupDto(id, "P" + id, "Product", true)));

            public void Complete(int index, CatalogLookupDto item) =>
                _loads[index].TrySetResult(
                    ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(new[] { item }));
        }

        private sealed class ControllableDelay : IAsyncDelay
        {
            private readonly List<TaskCompletionSource<bool>> _pending = new List<TaskCompletionSource<bool>>();
            public int PendingCount => _pending.Count;
            public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken)
            {
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => completion.TrySetCanceled());
                _pending.Add(completion);
                return completion.Task;
            }
            public void ReleaseLatest() => _pending[_pending.Count - 1].TrySetResult(true);
        }

        private sealed class ImmediateDelay : IAsyncDelay
        {
            public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken) =>
                Task.CompletedTask;
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
