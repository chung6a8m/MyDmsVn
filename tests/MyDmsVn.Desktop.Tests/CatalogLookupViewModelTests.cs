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
    public sealed class CatalogLookupViewModelTests
    {
        [Fact]
        public async Task Relevant_message_bursts_refresh_two_recipients_and_disposal_stops_one()
        {
            var messenger = new WeakReferenceMessenger();
            var firstDelay = new ControllableDelay();
            var secondDelay = new ControllableDelay();
            var firstSource = ScriptedSource.For(CatalogKind.Product, "P1");
            var secondSource = ScriptedSource.For(CatalogKind.Product, "P2");
            using (var first = Create(firstSource, messenger, firstDelay))
            using (var second = Create(secondSource, messenger, secondDelay))
            {
                messenger.Send(new CatalogChangedMessage(
                    CatalogKind.Warehouse,
                    1,
                    CatalogChangeOperation.Updated));
                Assert.Equal(0, firstDelay.PendingCount);
                Assert.Equal(0, secondDelay.PendingCount);

                messenger.Send(new CatalogChangedMessage(
                    CatalogKind.Product,
                    1,
                    CatalogChangeOperation.Updated));
                messenger.Send(new CatalogChangedMessage(
                    CatalogKind.Product,
                    2,
                    CatalogChangeOperation.ActiveStatusChanged));

                Assert.Equal(2, firstDelay.PendingCount);
                Assert.Equal(2, secondDelay.PendingCount);
                firstDelay.ReleaseLatest();
                secondDelay.ReleaseLatest();
                await WaitUntilAsync(() => firstSource.CallCount == 1 && secondSource.CallCount == 1);

                first.Dispose();
                messenger.Send(new CatalogChangedMessage(
                    CatalogKind.Product,
                    3,
                    CatalogChangeOperation.Created));
                secondDelay.ReleaseLatest();
                await WaitUntilAsync(() => secondSource.CallCount == 2);

                Assert.Equal(1, firstSource.CallCount);
                Assert.Equal(2, secondSource.CallCount);
            }
        }

        [Fact]
        public async Task Activation_refreshes_immediately_and_late_old_response_cannot_replace_new_items()
        {
            var source = new ControlledSource(CatalogKind.Product);
            using (var viewModel = Create(source, new WeakReferenceMessenger(), new ControllableDelay()))
            {
                var oldRefresh = viewModel.ActivateAsync(CancellationToken.None);
                var newRefresh = viewModel.RefreshAsync(CancellationToken.None);

                source.Complete(
                    1,
                    new CatalogLookupDto(2, "NEW", "New item", true));
                await newRefresh;
                source.Complete(
                    0,
                    new CatalogLookupDto(1, "OLD", "Old item", true));
                await oldRefresh;

                Assert.Equal(2, Assert.Single(viewModel.Items).Id);
                Assert.Equal(2, source.CallCount);
            }
        }

        [Fact]
        public async Task Search_is_debounced_but_explicit_refresh_is_immediate()
        {
            var delay = new ControllableDelay();
            var source = ScriptedSource.For(CatalogKind.Customer, "C1");
            using (var viewModel = Create(source, new WeakReferenceMessenger(), delay))
            {
                var first = viewModel.SetSearch("a");
                var second = viewModel.SetSearch("ab");
                Assert.Equal(0, source.CallCount);

                delay.ReleaseLatest();
                await Task.WhenAll(first, second);
                Assert.Equal(1, source.CallCount);

                await viewModel.RefreshAsync(CancellationToken.None);
                Assert.Equal(2, source.CallCount);
            }
        }

        [Fact]
        public async Task Refresh_supersedes_a_message_reload_callback_already_queued_for_ui_dispatch()
        {
            var messenger = new WeakReferenceMessenger();
            var delay = new ControllableDelay();
            var source = ScriptedSource.For(CatalogKind.Product, "P1");
            var dispatcher = new SwitchableUiDispatcher { Defer = true };
            using (var viewModel = Create(source, messenger, delay, dispatcher))
            {
                messenger.Send(new CatalogChangedMessage(
                    CatalogKind.Product,
                    1,
                    CatalogChangeOperation.Updated));

                dispatcher.Defer = false;
                await viewModel.RefreshAsync(CancellationToken.None);
                dispatcher.RunNext();

                Assert.Equal(0, delay.PendingCount);
                Assert.Equal(1, source.CallCount);
            }
        }

        [Fact]
        public async Task Search_supersedes_a_message_reload_callback_already_queued_for_ui_dispatch()
        {
            var messenger = new WeakReferenceMessenger();
            var delay = new ControllableDelay();
            var source = ScriptedSource.For(CatalogKind.Product, "P1");
            var dispatcher = new SwitchableUiDispatcher { Defer = true };
            using (var viewModel = Create(source, messenger, delay, dispatcher))
            {
                messenger.Send(new CatalogChangedMessage(
                    CatalogKind.Product,
                    1,
                    CatalogChangeOperation.Updated));
                var search = viewModel.SetSearch("new");

                dispatcher.Defer = false;
                dispatcher.RunNext();
                Assert.Equal(1, delay.PendingCount);
                delay.ReleaseAll();
                await search;

                var request = Assert.Single(source.Requests);
                Assert.Equal("new", request.Search);
            }
        }

        [Fact]
        public void Cancel_supersedes_a_message_reload_callback_already_queued_for_ui_dispatch()
        {
            var messenger = new WeakReferenceMessenger();
            var delay = new ControllableDelay();
            var source = ScriptedSource.For(CatalogKind.Product, "P1");
            var dispatcher = new SwitchableUiDispatcher { Defer = true };
            using (var viewModel = Create(source, messenger, delay, dispatcher))
            {
                messenger.Send(new CatalogChangedMessage(
                    CatalogKind.Product,
                    1,
                    CatalogChangeOperation.Updated));

                dispatcher.Defer = false;
                viewModel.Cancel();
                dispatcher.RunNext();

                Assert.Equal(0, delay.PendingCount);
                Assert.Equal(0, source.CallCount);
            }
        }

        [Fact]
        public async Task Message_reload_supersedes_a_pending_search_instead_of_querying_twice()
        {
            var messenger = new WeakReferenceMessenger();
            var delay = new ControllableDelay();
            var source = ScriptedSource.For(CatalogKind.Product, "P1");
            using (var viewModel = Create(source, messenger, delay))
            {
                var search = viewModel.SetSearch("new");
                messenger.Send(new CatalogChangedMessage(
                    CatalogKind.Product,
                    1,
                    CatalogChangeOperation.Updated));

                Assert.Equal(1, delay.IncompletePendingCount);
                delay.ReleaseAll();
                await search;
                await WaitUntilAsync(() => source.CallCount == 1);

                Assert.Single(source.Requests);
            }
        }

        [Fact]
        public async Task Inactive_historical_selection_remains_visible_but_inactive_results_are_not_new_options()
        {
            var source = new ScriptedSource(
                CatalogKind.Employee,
                (_, __) => Task.FromResult(
                    ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(
                        new[]
                        {
                            new CatalogLookupDto(1, "E1", "Active employee", true),
                            new CatalogLookupDto(2, "E2", "Inactive employee", false),
                        })));
            using (var viewModel = Create(source, new WeakReferenceMessenger(), new ControllableDelay()))
            {
                viewModel.SetSelection(7, "E7 - Historical employee", isActive: false);
                await viewModel.RefreshAsync(CancellationToken.None);

                Assert.Equal(new[] { 1, 7 }, viewModel.Items.Select(item => item.Id));
                var historical = viewModel.Items.Single(item => item.Id == 7);
                Assert.Equal("E7 - Historical employee", historical.DisplayName);
                Assert.False(historical.IsAvailableForNewSelection);
                Assert.Equal(7, viewModel.SelectedId);
            }
        }

        [Fact]
        public async Task Selected_active_item_becomes_unavailable_after_deactivation_message()
        {
            var messenger = new WeakReferenceMessenger();
            var delay = new ControllableDelay();
            var active = true;
            var source = new ScriptedSource(
                CatalogKind.Product,
                (_, __) => Task.FromResult(
                    ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(
                        new[] { new CatalogLookupDto(7, "P7", "Current renamed product", true) })),
                (_, __) => Task.FromResult(
                    ApiResponse<CatalogLookupDto>.Success(
                        new CatalogLookupDto(7, "P7", "Current renamed product", active))));
            using (var viewModel = Create(source, messenger, delay))
            {
                viewModel.SetSelection(7, "P7 - Selected product", isActive: true);
                await viewModel.RefreshAsync(CancellationToken.None);
                Assert.True(viewModel.Items.Single(item => item.Id == 7).IsAvailableForNewSelection);

                active = false;
                messenger.Send(new CatalogChangedMessage(
                    CatalogKind.Product,
                    7,
                    CatalogChangeOperation.ActiveStatusChanged));
                delay.ReleaseLatest();
                await WaitUntilAsync(() => viewModel.Items.Any(
                    item => item.Id == 7 && !item.IsAvailableForNewSelection));

                Assert.False(viewModel.Items.Single(item => item.Id == 7).IsAvailableForNewSelection);
                Assert.Equal(
                    "P7 - Selected product",
                    viewModel.Items.Single(item => item.Id == 7).DisplayName);
            }
        }

        [Fact]
        public async Task Dispose_during_request_prevents_late_state_updates()
        {
            var source = new ControlledSource(CatalogKind.Warehouse);
            var viewModel = Create(source, new WeakReferenceMessenger(), new ControllableDelay());
            var refresh = viewModel.RefreshAsync(CancellationToken.None);

            viewModel.Dispose();
            source.Complete(0, new CatalogLookupDto(1, "W1", "Warehouse", true));
            await refresh;

            Assert.Empty(viewModel.Items);
        }

        [Fact]
        public async Task Late_selected_status_response_cannot_change_a_newer_historical_selection()
        {
            var source = new ControlledSelectionSource(CatalogKind.Product);
            using (var viewModel = Create(source, new WeakReferenceMessenger(), new ControllableDelay()))
            {
                viewModel.SetSelection(1, "P1 - Original label", isActive: true);
                var refresh = viewModel.RefreshAsync(CancellationToken.None);
                viewModel.SetSelection(2, "P2 - Historical label", isActive: false);

                source.CompleteLookup();
                source.CompleteGet(new CatalogLookupDto(1, "P1", "Changed label", false));
                await refresh;

                var selected = viewModel.Items.Single(item => item.Id == 2);
                Assert.Equal("P2 - Historical label", selected.DisplayName);
                Assert.False(selected.IsAvailableForNewSelection);
            }
        }

        private static CatalogLookupViewModel Create(
            ICatalogLookupSource source,
            IMessenger messenger,
            IAsyncDelay delay,
            IUiDispatcher? dispatcher = null)
        {
            return new CatalogLookupViewModel(
                source,
                messenger,
                dispatcher ?? new ImmediateUiDispatcher(),
                new DesktopNotificationCenter(),
                delay);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow >= timeout)
                {
                    throw new TimeoutException("The expected asynchronous condition was not reached.");
                }

                await Task.Delay(10);
            }
        }

        private sealed class ControllableDelay : IAsyncDelay
        {
            private readonly List<TaskCompletionSource<bool>> _pending =
                new List<TaskCompletionSource<bool>>();

            public int PendingCount => _pending.Count;

            public int IncompletePendingCount =>
                _pending.Count(completion => !completion.Task.IsCompleted);

            public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken)
            {
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => completion.TrySetCanceled());
                _pending.Add(completion);
                return completion.Task;
            }

            public void ReleaseLatest()
            {
                _pending[_pending.Count - 1].TrySetResult(true);
            }

            public void ReleaseAll()
            {
                foreach (var completion in _pending)
                {
                    completion.TrySetResult(true);
                }
            }
        }

        private sealed class ScriptedSource : ICatalogLookupSource
        {
            private readonly Func<CatalogLookupRequest, CancellationToken,
                Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>>> _lookup;
            private readonly Func<int, CancellationToken, Task<ApiResponse<CatalogLookupDto>>>? _get;

            public ScriptedSource(
                CatalogKind kind,
                Func<CatalogLookupRequest, CancellationToken,
                    Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>>> lookup,
                Func<int, CancellationToken, Task<ApiResponse<CatalogLookupDto>>>? get = null)
            {
                Kind = kind;
                _lookup = lookup;
                _get = get;
            }

            public CatalogKind Kind { get; }

            public int CallCount { get; private set; }

            public IList<CatalogLookupRequest> Requests { get; } =
                new List<CatalogLookupRequest>();

            public static ScriptedSource For(CatalogKind kind, string code)
            {
                return new ScriptedSource(
                    kind,
                    (_, __) => Task.FromResult(
                        ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(
                            new[] { new CatalogLookupDto(1, code, "Item", true) })));
            }

            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(
                CatalogLookupRequest request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                Requests.Add(request);
                return _lookup(request, cancellationToken);
            }

            public async Task<ApiResponse<CatalogLookupDto>> GetAsync(
                int id,
                CancellationToken cancellationToken)
            {
                if (_get != null)
                {
                    return await _get(id, cancellationToken);
                }

                var response = await _lookup(
                    new CatalogLookupRequest(null, 25),
                    cancellationToken);
                var item = response.Data?.SingleOrDefault(candidate => candidate.Id == id);
                return item != null
                    ? ApiResponse<CatalogLookupDto>.Success(item)
                    : ApiResponse<CatalogLookupDto>.Failure(
                        new ApiError(ApiStatusCode.NotFound, "Catalog.NotFound", "Not found."));
            }
        }

        private sealed class SwitchableUiDispatcher : IUiDispatcher
        {
            private readonly Queue<Action> _pending = new Queue<Action>();

            public bool Defer { get; set; }

            public Task InvokeAsync(Action action, CancellationToken cancellationToken)
            {
                if (!Defer)
                {
                    action();
                    return Task.CompletedTask;
                }

                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _pending.Enqueue(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        action();
                    }

                    completion.TrySetResult(true);
                });
                return completion.Task;
            }

            public void RunNext() => _pending.Dequeue()();
        }

        private sealed class ControlledSource : ICatalogLookupSource
        {
            private readonly List<TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>>
                _responses = new List<TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>>();

            public ControlledSource(CatalogKind kind)
            {
                Kind = kind;
            }

            public CatalogKind Kind { get; }

            public int CallCount => _responses.Count;

            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(
                CatalogLookupRequest request,
                CancellationToken cancellationToken)
            {
                var response = new TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _responses.Add(response);
                return response.Task;
            }

            public void Complete(int index, params CatalogLookupDto[] items)
            {
                _responses[index].TrySetResult(
                    ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(items));
            }

            public Task<ApiResponse<CatalogLookupDto>> GetAsync(
                int id,
                CancellationToken cancellationToken) =>
                Task.FromResult(
                    ApiResponse<CatalogLookupDto>.Success(
                        new CatalogLookupDto(id, id.ToString(), "Selected", true)));
        }

        private sealed class ControlledSelectionSource : ICatalogLookupSource
        {
            private readonly TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>> _lookup =
                new TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<ApiResponse<CatalogLookupDto>> _get =
                new TaskCompletionSource<ApiResponse<CatalogLookupDto>>(TaskCreationOptions.RunContinuationsAsynchronously);

            public ControlledSelectionSource(CatalogKind kind) => Kind = kind;
            public CatalogKind Kind { get; }
            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken) => _lookup.Task;
            public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken cancellationToken) => _get.Task;
            public void CompleteLookup() => _lookup.TrySetResult(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(Array.Empty<CatalogLookupDto>()));
            public void CompleteGet(CatalogLookupDto item) => _get.TrySetResult(ApiResponse<CatalogLookupDto>.Success(item));
        }
    }
}
