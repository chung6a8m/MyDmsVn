using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed partial class SalesReceiptEditorViewModelTests
{
    [Fact]
    public async Task Save_for_close_reports_unknown_create_after_transport_failure_and_keeps_review_lock()
    {
        var api = new SalesApi { Create = _ => throw new InvalidOperationException("ambiguous") };
        using var vm = Editor(api);
        Valid(vm);
        var row = vm.AddLine()!;
        row.ProductId = 7;

        var outcome = await vm.SaveForCloseAsync();

        Assert.Equal(WorkspaceSaveStatus.Unknown, outcome.Status);
        Assert.True(vm.RequiresCreateOutcomeReview);
        Assert.Equal(vm.EditorIdentity, outcome.EditorIdentity);
        Assert.Single(api.Creates);
    }

    [Fact]
    public async Task Server_line_details_follow_submitted_reordered_references_and_leave_unknown_fields_unbound()
    {
        var api = new SalesApi();
        using var vm = Editor(api);
        await vm.LoadAsync(41);
        var first = vm.Lines[0];
        var second = vm.AddLine()!;
        second.ProductId = first.ProductId;
        vm.MoveLine(second, 0);
        api.Update = _ => Task.FromResult(ApiResponse<SalesReceiptDto>.Failure(
            new ApiError(ApiStatusCode.BadRequest, "SalesReceipt.Invalid", "Correct the sale.", new[]
            {
                new ApiErrorDetail("Validation.PrecisionScale", "Second row quantity is invalid.", "Request.Lines[0].Qty"),
                new ApiErrorDetail("Validation.PrecisionScale", "First row price is invalid.", "request.lines[1].unitPrice"),
                new ApiErrorDetail("Validation.Unknown", "Unknown row.", "Request.Lines[9].Qty"),
                new ApiErrorDetail("SalesReceipt.AmountOutOfRange", "Line amount is invalid.", "Request.Lines[1]"),
            })));

        await vm.SaveAsync();

        Assert.Same(second, vm.ValidationIssues[0].Line);
        Assert.Equal("qty", vm.ValidationIssues[0].Field);
        Assert.Same(first, vm.ValidationIssues[1].Line);
        Assert.Equal("unitPrice", vm.ValidationIssues[1].Field);
        Assert.Null(vm.ValidationIssues[2].Line);
        Assert.Equal("Request.Lines[9].Qty", vm.ValidationIssues[2].Field);
        Assert.Same(first, vm.ValidationIssues[3].Line);
        Assert.Equal("line", vm.ValidationIssues[3].Field);
        Assert.All(vm.ValidationIssues, issue => Assert.Equal("server", issue.Source));
        second.Quantity = 2m;
        Assert.DoesNotContain(vm.ValidationIssues, issue => ReferenceEquals(issue.Line, second) && issue.Field == "qty");
        Assert.Contains(vm.ValidationIssues, issue => ReferenceEquals(issue.Line, first) && issue.Field == "unitPrice");
        Assert.Contains(vm.ValidationIssues, issue => ReferenceEquals(issue.Line, first) && issue.Field == "line");
        Assert.Contains(vm.ValidationIssues, issue => issue.Line == null);
        Assert.True(first.SetProduct(first.ProductId, "Updated caption", true, first.Unit));
        first.Quantity = 3m;
        Assert.Contains(vm.ValidationIssues, issue => ReferenceEquals(issue.Line, first) && issue.Field == "line");
    }

    [Fact]
    public async Task Post_line_error_targets_current_reference_without_reusing_a_saved_line_id()
    {
        var api = new SalesApi();
        using var vm = Editor(api);
        await vm.LoadAsync(41);
        var row = vm.Lines.Single();
        api.Post = _ => Task.FromResult(ApiResponse<PostSalesReceiptResponse>.Failure(
            new ApiError(ApiStatusCode.BadRequest, "SalesReceipt.Invalid", "Review product.", new[]
            {
                new ApiErrorDetail("SalesReceipt.ProductInactive", "Product is inactive.", "Request.Lines[0].ProductId"),
            })));

        await vm.PostAsync();

        var issue = Assert.Single(vm.ValidationIssues);
        Assert.Same(row, issue.Line);
        Assert.Equal("productId", issue.Field);
        Assert.Equal("Product is inactive.", issue.Message);
        Assert.True(row.SetProduct(row.ProductId, "Refreshed caption", true, row.Unit));
        Assert.Single(vm.ValidationIssues);
        row.ProductId = row.ProductId + 1;
        Assert.Empty(vm.ValidationIssues);
    }

    [Fact]
    public async Task Queued_server_validation_from_old_session_does_not_target_new_document()
    {
        var api = new SalesApi(); var session = new SalesSession(); var ui = new SalesQueuedUi();
        using var vm = Editor(api, session, ui);
        await vm.LoadAsync(41);
        vm.Lines.Single().Quantity = 2m;
        api.Update = _ =>
        {
            ui.Queue = true;
            return Task.FromResult(ApiResponse<SalesReceiptDto>.Failure(
                new ApiError(ApiStatusCode.BadRequest, "SalesReceipt.Invalid", "Old error.", new[]
                {
                    new ApiErrorDetail("Validation.PrecisionScale", "Old quantity.", "Request.Lines[0].Qty"),
                })));
        };

        var pending = vm.SaveAsync();
        session.Replace();
        ui.Queue = false; ui.Drain();
        await LocalSalesReceiptApiClientTests.Within(pending);

        Assert.Empty(vm.ValidationIssues);
        Assert.Empty(vm.Lines);
    }

    [Fact]
    public async Task Duplicate_product_zero_id_lines_keep_distinct_server_targets_and_document_replacement_clears_them()
    {
        var api = new SalesApi();
        using var vm = Editor(api);
        Valid(vm);
        var first = vm.AddLine()!; first.ProductId = 7;
        var second = vm.AddLine()!; second.ProductId = 7;
        api.Create = _ => Task.FromResult(ApiResponse<SalesReceiptDto>.Failure(
            new ApiError(ApiStatusCode.BadRequest, "SalesReceipt.Invalid", "Correct the sale.", new[]
            {
                new ApiErrorDetail("Validation.PrecisionScale", "First quantity.", "Request.Lines[0].Qty"),
                new ApiErrorDetail("Validation.PrecisionScale", "Second quantity.", "Request.Lines[1].Qty"),
            })));

        await vm.SaveAsync();

        Assert.Same(first, vm.ValidationIssues[0].Line);
        Assert.Same(second, vm.ValidationIssues[1].Line);
        Assert.NotSame(vm.ValidationIssues[0].Line, vm.ValidationIssues[1].Line);
        Assert.True(vm.NewDraft());
        Assert.Empty(vm.ValidationIssues);
    }
    [Fact]
    public async Task Confirmed_post_publishes_success_before_failed_detail_refresh_and_keeps_recovery()
    {
        var api = new SalesApi();
        var feedback = new RecordedFeedback();
        using var vm = Editor(api);
        await vm.LoadAsync(41);
        api.Get = _ => Task.FromResult(SalesApi.Error<SalesReceiptDto>());

        await vm.PostAsync(feedback: feedback);

        Assert.Equal(new[] { DesktopNotificationKind.Success, DesktopNotificationKind.Error },
            feedback.Notices.Select(notice => notice.Kind));
        Assert.Equal("Posted", vm.Status);
        Assert.True(vm.RequiresPostOutcomeReload);
        Assert.Single(api.Posts);
    }

    [Fact]
    public async Task Ambiguous_create_publishes_no_success_and_keeps_review_lock()
    {
        var api = new SalesApi { Create = _ => throw new InvalidOperationException("ambiguous") };
        var feedback = new RecordedFeedback();
        using var vm = Editor(api);
        Valid(vm);
        var row = vm.AddLine()!;
        row.ProductId = 7;

        await vm.SaveAsync(feedback: feedback);

        Assert.DoesNotContain(feedback.Notices, notice => notice.Kind == DesktopNotificationKind.Success);
        Assert.Contains(feedback.Notices, notice => notice.Kind == DesktopNotificationKind.Error);
        Assert.True(vm.RequiresCreateOutcomeReview);
        Assert.Single(api.Creates);
    }

    [Fact]
    public async Task Changing_product_identity_clears_old_unit_and_keeps_manual_price()
    {
        using var vm = Editor(); await vm.LoadAsync(41); var row = vm.Lines.Single();
        Assert.Equal("box", row.Unit); row.ProductId = 8;
        Assert.Equal("", row.Unit); Assert.Equal(4m, row.UnitPrice); Assert.True(vm.IsDirty);
        Assert.True(row.SetProduct(9, "Other", true, "each")); Assert.Equal("each", row.Unit); Assert.Equal(4m, row.UnitPrice);
        vm.SetAccess(true, false, false); Assert.False(row.SetProduct(99, "Denied", true, "wrong")); Assert.Equal("each", row.Unit);
    }

    [Fact]
    public async Task Canceled_ignored_create_transport_cannot_apply_queued_success_or_allow_duplicate_retry()
    {
        var api = new SalesApi(); var ui = new SalesQueuedUi(); using var vm = Editor(api, ui: ui); Valid(vm);
        var row = vm.AddLine()!; row.ProductId = 7; row.Quantity = 2; row.UnitPrice = 4;
        api.Create = _ => { ui.Queue = true; return Task.FromResult(SalesApi.Ok(SalesTestData.Receipt())); };
        using var cancellation = new CancellationTokenSource(); var pending = vm.SaveAsync(cancellation.Token);
        cancellation.Cancel(); ui.Queue = false; ui.Drain(); await LocalSalesReceiptApiClientTests.Within(pending);
        Assert.True(vm.RequiresCreateOutcomeReview); Assert.Equal(0, vm.SaleId); Assert.Same(row, vm.Lines.Single());
        await vm.SaveAsync(); Assert.Single(api.Creates);
    }

    [Fact]
    public async Task New_document_fences_queued_previous_detail_and_disposal_scrubs()
    {
        var api = new SalesApi(); var ui = new SalesQueuedUi(); var vm = Editor(api, ui: ui);
        ui.Queue = true; var pending = vm.LoadAsync(41); Assert.True(vm.NewDraft()); vm.Note = "new document";
        ui.Queue = false; ui.Drain(); await LocalSalesReceiptApiClientTests.Within(pending);
        Assert.Equal(0, vm.SaleId); Assert.Equal("new document", vm.Note); Assert.Empty(vm.Lines); Assert.True(vm.CanEdit);
        vm.Dispose(); Assert.Null(vm.Note); Assert.False(vm.CanEdit);
    }

    [Fact]
    public async Task Queued_known_post_after_replacement_never_requests_detail_or_publishes_status()
    {
        var api = new SalesApi(); var session = new SalesSession(); var ui = new SalesQueuedUi(); using var vm = Editor(api, session, ui);
        await vm.LoadAsync(41);
        api.Post = _ => { ui.Queue = true; return Task.FromResult(SalesApi.Ok(new PostSalesReceiptResponse(41, "Posted", DateTime.UtcNow, "new-version"))); };
        var pending = vm.PostAsync(); session.Replace();
        ui.Queue = false; ui.Drain(); await LocalSalesReceiptApiClientTests.Within(pending);
        Assert.Single(api.Gets); Assert.Equal(0, vm.SaleId); Assert.Empty(vm.Lines); Assert.False(vm.CanEdit);
    }

    [Fact]
    public async Task Persisted_duplicate_rows_remain_independent_after_reorder_update_and_same_id_reload()
    {
        var api = new SalesApi { Get = _ => Task.FromResult(SalesApi.Ok(SalesTestData.Receipt(41, false, SalesTestData.Line(91, 1), SalesTestData.Line(92, 2)))) };
        using var vm = Editor(api); await vm.LoadAsync(41); var a = vm.Lines[0]; var b = vm.Lines[1]; vm.SelectLine(b); vm.MoveLine(b, 0);
        api.Update = _ => Task.FromResult(SalesApi.Ok(SalesTestData.Receipt(41, false, SalesTestData.Line(92, 1), SalesTestData.Line(91, 2))));
        await vm.SaveAsync(); Assert.Equal(new long[] { 92, 91 }, api.Updates.Single().Lines.Select(row => row.SaleLineId));
        Assert.Same(b, vm.SelectedLine); Assert.Same(b, vm.Lines[0]); Assert.Same(a, vm.Lines[1]);
        await vm.LoadAsync(41); Assert.Same(a, vm.Lines[0]); Assert.Same(b, vm.Lines[1]); Assert.Same(b, vm.SelectedLine);
    }

    [Theory]
    [InlineData("500")]
    [InlineData("cancel")]
    [InlineData("invalid")]
    public async Task Ambiguous_create_envelope_or_cancellation_never_synthesizes_identity_or_retries(string mode)
    {
        var api = new SalesApi(); using var vm = Editor(api); Valid(vm); var row = vm.AddLine()!; row.ProductId = 7;
        api.Create = _ => mode == "cancel" ? throw new OperationCanceledException() : mode == "500" ? Task.FromResult(SalesApi.Error<SalesReceiptDto>()) : Task.FromResult(SalesApi.Ok(SalesTestData.Receipt(41, false, SalesTestData.Line(92), SalesTestData.Line(93, 2))));
        await vm.SaveAsync(); Assert.True(vm.RequiresCreateOutcomeReview); Assert.Equal(0, vm.SaleId); Assert.Same(row, vm.Lines.Single()); Assert.False(vm.CanEdit);
        await vm.SaveAsync(); Assert.Single(api.Creates);
    }

    [Fact]
    public async Task Read_write_and_post_access_are_independent_and_invalid_buffers_cannot_send()
    {
        var api = new SalesApi(); var buffers = new SalesBuffers(); using var vm = Editor(api, buffers: buffers); await vm.LoadAsync(41);
        vm.SetAccess(true, true, false); await vm.PostAsync(); Assert.Empty(api.Posts); Assert.True(vm.CanEdit);
        buffers.Commit = _ => { vm.Lines[0].Quantity = .00001m; return Task.FromResult(false); }; await vm.SaveAsync(); Assert.Empty(api.Updates);
        vm.SetAccess(false, true, true); Assert.Empty(vm.Lines); await vm.LoadAsync(41); await vm.SaveAsync(); Assert.Single(api.Gets); Assert.False(vm.CanEdit);
    }

    [Fact]
    public void Preview_uses_exact_fixed_point_and_validates_even_gifts()
    {
        var math = SalesApi.Calculator();
        Assert.Equal(12345678901234567800000m, math.CalculateRevenue(12345678901234.5678m, 1000000000.0000m, false));
        Assert.Equal(.0001m, math.CalculateRevenue(.0001m, .5m, false));
        Assert.ThrowsAny<ArgumentException>(() => math.CalculateRevenue(1, .00001m, true));
        Assert.Throws<OverflowException>(() => math.SumAmounts(new[] { SalesContractLimits.MaximumAmount, .0001m }));
    }

    [Fact]
    public void Gift_retains_manual_price_and_invalid_amount_or_sum_is_visible()
    {
        using var vm = Editor(); var row = vm.AddLine()!; row.ProductId = 7; row.Quantity = 2m; row.UnitPrice = 4m;
        Assert.Equal(8m, row.LineAmount); row.IsGift = true; Assert.Equal(0m, row.LineAmount); Assert.Equal(4m, row.UnitPrice);
        row.IsGift = false; Assert.Equal(8m, row.LineAmount); row.Quantity = .00001m; Assert.Null(row.LineAmount); Assert.Null(vm.TotalAmount);
    }

    [Fact]
    public async Task Duplicate_zero_rows_keep_keys_and_objects_through_save_move_and_remove()
    {
        var api = new SalesApi(); using var vm = Editor(api); Valid(vm);
        var a = vm.AddLine()!; var b = vm.AddLine()!; a.ProductId = b.ProductId = 7; a.UnitPrice = b.UnitPrice = 4; a.Quantity = b.Quantity = 2;
        Assert.NotEqual(a.ClientKey, b.ClientKey);
        vm.MoveLine(b, 0);
        api.Create = r => Task.FromResult(SalesApi.Ok(SalesTestData.Receipt(41, false, SalesTestData.Line(92, 1), SalesTestData.Line(91, 2))));
        await vm.SaveAsync();
        Assert.Same(b, vm.Lines[0]); Assert.Equal(92, b.SaleLineId); Assert.Equal(91, a.SaleLineId);
        Assert.Equal("box", b.Unit); Assert.False(vm.IsDirty); Assert.Equal(2, api.Creates.Single().Lines.Count);
        Assert.True(vm.RemoveLine(a)); a.Quantity = 19; Assert.Equal(2m, a.Quantity);
        Assert.Same(b, Assert.Single(vm.Lines)); Assert.Null(vm.TotalCost); Assert.Null(vm.GrossProfit);
    }

    [Fact]
    public async Task Raw_buffers_commit_before_save_snapshot_and_before_post_dirty_decision()
    {
        var api = new SalesApi(); var buffers = new SalesBuffers(); using var vm = Editor(api, buffers: buffers);
        await vm.LoadAsync(41);
        buffers.Commit = _ => { vm.Note = "raw valid edit"; return Task.FromResult(true); };
        await vm.PostAsync(); Assert.Empty(api.Posts); Assert.True(vm.IsDirty);
        await vm.SaveAsync(); Assert.Equal("raw valid edit", api.Updates.Single().Note);
        buffers.Commit = _ => Task.FromResult(false); await vm.SaveAsync(); await vm.PostAsync();
        Assert.Single(api.Updates); Assert.Empty(api.Posts);
    }

    [Fact]
    public async Task Overlapping_admission_and_busy_block_every_mutation()
    {
        var api = new SalesApi(); var buffers = new SalesBuffers(); using var vm = Editor(api, buffers: buffers); await vm.LoadAsync(41);
        var release = new TaskCompletionSource<bool>(); buffers.Commit = _ => release.Task;
        var save = vm.SaveAsync(); await vm.PostAsync(); Assert.Empty(api.Posts);
        var result = new TaskCompletionSource<ApiResponse<SalesReceiptDto>>(); api.Update = _ => result.Task;
        release.SetResult(true);
        // A clean Save does not dispatch; make admission commit a dirty model before continuing.
        await save;
        vm.Note = "dirty"; buffers.Commit = _ => Task.FromResult(true);
        var pending = vm.SaveAsync(); Assert.True(vm.IsBusy); var row = vm.Lines.Single(); var qty = row.Quantity;
        row.Quantity = 99; row.UnitPrice = 99; row.IsGift = true; row.ProductId = 99; vm.Note = "rejected";
        Assert.Equal(qty, row.Quantity); Assert.Equal(4m, row.UnitPrice); Assert.False(row.IsGift); Assert.Equal(7, row.ProductId);
        Assert.False(row.SetProduct(99, "Rejected", true, "unit"));
        Assert.False(vm.SetCustomer(99, "Rejected", true)); Assert.False(vm.SetWarehouse(99, "Rejected", true)); Assert.False(vm.SetEmployee(99, "Rejected", true));
        Assert.Null(vm.AddLine()); Assert.False(vm.RemoveLine(row)); Assert.False(vm.MoveLine(row, 0));
        Assert.Equal("dirty", vm.Note);
        result.SetResult(SalesApi.Ok(SalesTestData.Receipt())); await pending;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Posted_and_denied_guard_headers_rows_and_actions(bool posted)
    {
        var api = new SalesApi { Get = _ => Task.FromResult(SalesApi.Ok(SalesTestData.Receipt(41, posted))) };
        using var vm = Editor(api); await vm.LoadAsync(41); if (!posted) vm.SetAccess(true, false, false);
        var row = vm.Lines.Single(); row.Quantity = 99; row.UnitPrice = 99; row.ProductId = 99; row.IsGift = true; vm.CustomerId = 99;
        Assert.False(vm.CanEdit); Assert.Equal(2m, row.Quantity); Assert.Equal(4m, row.UnitPrice); Assert.Equal(7, row.ProductId);
        Assert.Equal(1, vm.CustomerId); Assert.Null(vm.AddLine()); Assert.False(vm.RemoveLine(row)); await vm.SaveAsync(); await vm.PostAsync();
        Assert.Empty(api.Creates); Assert.Empty(api.Updates); Assert.Empty(api.Posts);
        Assert.Equal(posted ? 5m : (decimal?)null, vm.TotalCost);
    }

    [Fact]
    public async Task Unknown_create_retains_draft_locks_no_replay_and_requires_authorized_identified_resolution()
    {
        var api = new SalesApi { Create = _ => throw new InvalidOperationException("ambiguous") };
        using var vm = Editor(api); Valid(vm); var row = vm.AddLine()!; row.ProductId = 7; vm.Note = "raw draft";
        await vm.SaveAsync(); Assert.True(vm.RequiresCreateOutcomeReview); Assert.Same(row, vm.Lines.Single()); Assert.Equal("raw draft", vm.Note);
        await vm.SaveAsync(); Assert.Single(api.Creates); Assert.False(vm.NewDraft()); await vm.LoadAsync(99); Assert.Empty(api.Gets);
        api.Get = _ => Task.FromResult(SalesApi.Error<SalesReceiptDto>()); await vm.ResolveCreateOutcomeAsync(41); Assert.True(vm.RequiresCreateOutcomeReview);
        api.Get = id => Task.FromResult(SalesApi.Ok(SalesTestData.Receipt(id))); await vm.ResolveCreateOutcomeAsync(41);
        Assert.False(vm.RequiresCreateOutcomeReview); Assert.Equal(41, vm.SaleId); Assert.True(vm.CanEdit);
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("cancel")]
    [InlineData("SalesReceipt.ConcurrencyConflict")]
    [InlineData("SalesReceipt.AlreadyPosted")]
    public async Task Unknown_or_conflicted_post_stays_locked_until_successful_same_id_reload(string mode)
    {
        var api = new SalesApi(); using var vm = Editor(api); await vm.LoadAsync(41);
        api.Post = _ => mode == "throw" ? throw new InvalidOperationException() : mode == "cancel" ? throw new OperationCanceledException() :
            Task.FromResult(SalesApi.Error<PostSalesReceiptResponse>(ApiStatusCode.Conflict, mode));
        await vm.PostAsync(); Assert.True(vm.RequiresPostOutcomeReload); Assert.False(vm.CanEdit); await vm.PostAsync(); Assert.Single(api.Posts);
        api.Get = _ => Task.FromResult(SalesApi.Error<SalesReceiptDto>()); await vm.ReloadOutcomeAsync(); Assert.True(vm.RequiresPostOutcomeReload);
        api.Get = _ => Task.FromResult(SalesApi.Ok(SalesTestData.Receipt(99))); await vm.ReloadOutcomeAsync(); Assert.True(vm.RequiresPostOutcomeReload); Assert.Equal(41, vm.SaleId);
        api.Get = _ => Task.FromResult(SalesApi.Ok(SalesTestData.Receipt(41, true))); await vm.ReloadOutcomeAsync();
        Assert.False(vm.RequiresPostOutcomeReload); Assert.Equal("Posted", vm.Status); Assert.Equal(5m, vm.TotalCost);
    }

    [Fact]
    public async Task Known_post_retains_status_version_when_cost_reload_fails_without_second_post()
    {
        var api = new SalesApi(); using var vm = Editor(api); await vm.LoadAsync(41);
        api.Get = _ => Task.FromResult(SalesApi.Error<SalesReceiptDto>()); await vm.PostAsync();
        Assert.Equal("Posted", vm.Status); Assert.Equal("new-version", vm.Version); Assert.False(vm.IsDirty);
        Assert.Null(vm.TotalCost); Assert.Null(vm.GrossProfit); Assert.True(vm.RequiresPostOutcomeReload); Assert.False(vm.CanEdit);
        await vm.PostAsync(); Assert.Single(api.Posts);
    }

    [Fact]
    public async Task Session_replacement_while_post_blocked_scrubs_and_prevents_follow_on_get()
    {
        var api = new SalesApi(); var session = new SalesSession(); using var vm = Editor(api, session); await vm.LoadAsync(41);
        var completion = new TaskCompletionSource<ApiResponse<PostSalesReceiptResponse>>(); api.Post = _ => completion.Task;
        var pending = vm.PostAsync(); session.Replace();
        completion.SetResult(SalesApi.Ok(new PostSalesReceiptResponse(41, "Posted", DateTime.UtcNow, "late"))); await pending;
        Assert.Empty(vm.Lines); Assert.Equal(0, vm.SaleId); Assert.Null(vm.Note); Assert.False(vm.CanEdit); Assert.Single(api.Gets);
    }

    [Fact]
    public async Task Queued_old_document_or_session_load_cannot_publish()
    {
        var api = new SalesApi(); var session = new SalesSession(); var ui = new SalesQueuedUi(); using var vm = Editor(api, session, ui);
        ui.Queue = true; var pending = vm.LoadAsync(41); session.Replace(); ui.Queue = false; ui.Drain(); await LocalSalesReceiptApiClientTests.Within(pending);
        Assert.Empty(vm.Lines); Assert.Equal(0, vm.SaleId); Assert.False(vm.CanEdit);
    }

    private static SalesReceiptEditorViewModel Editor(SalesApi? api = null, SalesSession? session = null, IUiDispatcher? ui = null, SalesBuffers? buffers = null) =>
        CreateEditor(api, session, ui, buffers);
    private static SalesReceiptEditorViewModel CreateEditor(SalesApi? api, SalesSession? session, IUiDispatcher? ui, SalesBuffers? buffers)
    {
        var vm = new SalesReceiptEditorViewModel(api ?? new SalesApi(), session ?? new SalesSession(), ui ?? new ImmediateUiDispatcher(), SalesApi.Calculator(), buffers ?? new SalesBuffers());
        vm.SetAccess(true, true, true); return vm;
    }
    private static void Valid(SalesReceiptEditorViewModel vm) { vm.CustomerId = 1; vm.WarehouseId = 2; vm.EmployeeId = 3; }

    private sealed class RecordedFeedback : IWorkspaceFeedbackSink
    {
        public List<DesktopNotification> Notices { get; } = new List<DesktopNotification>();

        public void Publish(DesktopNotification notice) => Notices.Add(notice);
        public void ClearActionable() { }
    }
}
