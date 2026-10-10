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
                await WaitUntilAsync(() => source.Calls == 1);
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
            public IList<CatalogLookupRequest> Requests { get; } = new List<CatalogLookupRequest>();

            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(
                CatalogLookupRequest request,
                CancellationToken cancellationToken)
            {
                Calls++;
                Requests.Add(request);
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
    }
}
