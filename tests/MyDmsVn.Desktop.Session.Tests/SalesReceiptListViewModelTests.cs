using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SalesReceiptListViewModelTests
{
    [Fact]
    public async Task Canceled_ignored_transport_and_queued_ui_cannot_publish_a_page()
    {
        var api = new SalesApi(); var ui = new SalesQueuedUi { Queue = true };
        using var vm = new SalesReceiptListViewModel(api, new SalesSession(), ui);
        using var canceled = new CancellationTokenSource(); var pending = vm.RefreshAsync(canceled.Token);
        canceled.Cancel(); ui.Queue = false; ui.Drain(); await LocalSalesReceiptApiClientTests.Within(pending);
        Assert.Empty(vm.Items); Assert.Null(vm.AppliedCriteria); Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Default_first_and_reverse_navigation_keep_every_filter_in_the_applied_snapshot()
    {
        var api = new SalesApi(); using var vm = new SalesReceiptListViewModel(api, new SalesSession(), new ImmediateUiDispatcher());
        await vm.RefreshAsync(); Assert.Equal(50, api.Lists.Single().PageSize); Assert.Null(api.Lists.Single().Cursor);
        api.List = _ => Task.FromResult(SalesApi.Ok(SalesApi.Page(51, "n", "p")));
        await vm.SetCriteriaAsync(Criteria(1, "all")); await vm.PreviousAsync();
        var request = api.Lists.Last(); Assert.Equal("p", request.Cursor); Assert.Equal(new DateTime(2026, 10, 1), request.FromDate);
        Assert.Equal(new DateTime(2026, 10, 22), request.ToDate); Assert.Equal(1, request.CustomerId); Assert.Equal(2, request.WarehouseId);
        Assert.Equal(3, request.EmployeeId); Assert.Equal(7, request.ProductId); Assert.Equal("Draft", request.Status); Assert.Equal("all", request.Search); Assert.Equal(1, request.PageSize);
    }

    [Fact]
    public async Task Pending_criteria_failure_retains_applied_page_and_disables_old_cursor_navigation()
    {
        var api = new SalesApi();
        using var vm = new SalesReceiptListViewModel(api, new SalesSession(), new ImmediateUiDispatcher());
        await vm.RefreshAsync(); vm.Select(41);
        var applied = vm.AppliedCriteria;
        api.List = _ => Task.FromResult(SalesApi.Error<SalesReceiptListPage>());
        await vm.SetCriteriaAsync(Criteria(1, "new"));
        Assert.Equal(41, Assert.Single(vm.Items).SaleId);
        Assert.Equal(41, vm.SelectedSaleId);
        Assert.Same(applied, vm.AppliedCriteria);
        Assert.Equal("new", vm.PendingCriteria.Search);
        Assert.Equal("next", vm.NextCursor);
        var count = api.Lists.Count; await vm.NextAsync(); Assert.Equal(count, api.Lists.Count);
    }

    [Fact]
    public async Task Successful_page_publishes_all_state_before_any_notification_and_navigation_uses_applied_criteria()
    {
        var api = new SalesApi();
        using var vm = new SalesReceiptListViewModel(api, new SalesSession(), new ImmediateUiDispatcher());
        object? lastItems = null;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Items) && vm.Items.Count > 0 && !ReferenceEquals(lastItems, vm.Items)) {
            lastItems = vm.Items;
            Assert.Equal(vm.PendingCriteria.PageSize, vm.AppliedCriteria!.PageSize); Assert.Equal("next", vm.NextCursor); Assert.Equal(41, vm.Items[0].SaleId); } };
        await vm.SetCriteriaAsync(Criteria(1, "search"));
        await vm.NextAsync(); Assert.Equal("next", api.Lists.Last().Cursor); Assert.Equal("search", api.Lists.Last().Search);
        await vm.FirstAsync(); Assert.Null(api.Lists.Last().Cursor);
        await vm.RefreshAsync(); Assert.Null(api.Lists.Last().Cursor);
        await vm.SetCriteriaAsync(Criteria(10, "other", "untrusted")); Assert.Null(api.Lists.Last().Cursor);
    }

    [Fact]
    public async Task Empty_later_page_recovers_once_per_generation_and_failed_recovery_retains_snapshot()
    {
        var api = new SalesApi();
        using var vm = new SalesReceiptListViewModel(api, new SalesSession(), new ImmediateUiDispatcher());
        await vm.RefreshAsync();
        var call = 0;
        api.List = r => Task.FromResult(++call == 1 ? SalesApi.Ok(SalesApi.Page(0)) : SalesApi.Error<SalesReceiptListPage>());
        await vm.NextAsync(); Assert.Equal(2, call); Assert.Null(api.Lists.Last().Cursor); Assert.Single(vm.Items);
        api.List = _ => { call++; return Task.FromResult(SalesApi.Ok(SalesApi.Page(0))); };
        await vm.NextAsync(); Assert.Equal(3, call); Assert.Single(vm.Items);
        await vm.RefreshAsync(); Assert.Empty(vm.Items); Assert.Null(vm.NextCursor);
    }

    [Theory]
    [InlineData(ApiStatusCode.Unauthorized)]
    [InlineData(ApiStatusCode.Forbidden)]
    public async Task Authorization_loss_clears_protected_items_selection_and_cursors(ApiStatusCode status)
    {
        var api = new SalesApi();
        using var vm = new SalesReceiptListViewModel(api, new SalesSession(), new ImmediateUiDispatcher());
        await vm.RefreshAsync(); vm.Select(41);
        api.List = _ => Task.FromResult(SalesApi.Error<SalesReceiptListPage>(status)); await vm.NextAsync();
        Assert.True(vm.IsAccessDenied); Assert.Empty(vm.Items); Assert.Null(vm.SelectedSaleId); Assert.Null(vm.NextCursor); Assert.Null(vm.AppliedCriteria);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_success_or_error_cannot_replace_newer_filter(bool error)
    {
        var api = new SalesApi(); var old = new TaskCompletionSource<ApiResponse<SalesReceiptListPage>>();
        api.List = _ => old.Task;
        using var vm = new SalesReceiptListViewModel(api, new SalesSession(), new ImmediateUiDispatcher());
        var first = vm.RefreshAsync();
        api.List = _ => Task.FromResult(SalesApi.Ok(SalesApi.Page(42)));
        await vm.SetCriteriaAsync(Criteria(5, "latest"));
        old.SetResult(error ? SalesApi.Error<SalesReceiptListPage>() : SalesApi.Ok(SalesApi.Page(1))); await first;
        Assert.Equal(42, Assert.Single(vm.Items).SaleId); Assert.Null(vm.Error); Assert.False(vm.IsBusy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_change_or_disposal_inside_queued_callback_cannot_publish_old_page(bool dispose)
    {
        var api = new SalesApi(); var session = new SalesSession(); var ui = new SalesQueuedUi();
        using var vm = new SalesReceiptListViewModel(api, session, ui);
        await vm.RefreshAsync(); ui.Queue = true;
        var pending = vm.RefreshAsync();
        if (dispose) vm.Dispose(); else session.Replace();
        ui.Queue = false; ui.Drain(); await LocalSalesReceiptApiClientTests.Within(pending);
        Assert.Empty(vm.Items); Assert.Null(vm.AppliedCriteria); Assert.False(vm.IsBusy);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Explicit_access_revoke_restore_fences_old_transport_and_queued_success_or_error(bool queued, bool error)
    {
        var api = new SalesApi(); var ui = new SalesQueuedUi();
        using var vm = new SalesReceiptListViewModel(api, new SalesSession(), ui);
        await vm.RefreshAsync(); vm.Select(41);
        var completion = new TaskCompletionSource<ApiResponse<SalesReceiptListPage>>();
        api.List = _ => queued ? Task.FromResult(error ? SalesApi.Error<SalesReceiptListPage>(ApiStatusCode.Forbidden) : SalesApi.Ok(SalesApi.Page(1))) : completion.Task;
        ui.Queue = queued; var old = vm.RefreshAsync();
        vm.SetAccess(false); Assert.True(vm.IsAccessDenied); Assert.Empty(vm.Items); Assert.Null(vm.SelectedSaleId); Assert.Null(vm.NextCursor); Assert.Null(vm.PreviousCursor); Assert.Null(vm.AppliedCriteria);
        var calls = api.Lists.Count; await vm.RefreshAsync(); Assert.Equal(calls, api.Lists.Count);
        vm.SetAccess(true); Assert.False(vm.IsAccessDenied); Assert.Empty(vm.Items); Assert.Equal(calls, api.Lists.Count);
        ui.Queue = false; ui.Drain();
        completion.TrySetResult(error ? SalesApi.Error<SalesReceiptListPage>(ApiStatusCode.Forbidden) : SalesApi.Ok(SalesApi.Page(1)));
        await LocalSalesReceiptApiClientTests.Within(old);
        Assert.Empty(vm.Items); Assert.Null(vm.Error); Assert.False(vm.IsBusy);
        api.List = _ => Task.FromResult(SalesApi.Ok(SalesApi.Page(42)));
        await vm.RefreshAsync(); Assert.Equal(42, Assert.Single(vm.Items).SaleId);
    }
    [Fact]
    public async Task Explicit_read_restore_never_rebinds_replacement_session_or_revives_disposed_vm()
    {
        var api = new SalesApi(); var session = new SalesSession();
        using var vm = new SalesReceiptListViewModel(api, session, new ImmediateUiDispatcher());
        await vm.RefreshAsync(); vm.SetAccess(false); session.Replace(); vm.SetAccess(true);
        var calls = api.Lists.Count; await vm.RefreshAsync(); Assert.True(vm.IsAccessDenied); Assert.Empty(vm.Items); Assert.Equal(calls, api.Lists.Count);
        vm.Dispose(); vm.SetAccess(true); await vm.RefreshAsync(); Assert.Empty(vm.Items); Assert.Equal(calls, api.Lists.Count);
    }
    private static SalesReceiptListRequest Criteria(int size, string search, string? cursor = null) =>
        new SalesReceiptListRequest(new DateTime(2026, 10, 1), new DateTime(2026, 10, 22), 1, 2, 3, "Draft", 7, search, size, cursor);
}
