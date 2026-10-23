using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SalesReceiptControlAdmissionTests
{
    [Fact]
    public void Close_state_tracks_unsaved_receipt_and_clean_posted_inspection()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            var control = (SalesReceiptControl)host.Control;
            Assert.False(control.CaptureCloseState().IsDirty);
            host.Editor.Note = "unsaved";
            var dirty = control.CaptureCloseState();
            Assert.True(dirty.IsDirty);
            Assert.True(dirty.CanSave);
            Assert.False(dirty.IsMutationBusy);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Save_for_close_returns_confirmed_sales_write_with_submitted_editor_revision()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            var control = (SalesReceiptControl)host.Control;
            host.Editor.Note = "changed";
            var task = control.SaveForCloseAsync(CancellationToken.None);
            host.Wait(task);
            var outcome = task.GetAwaiter().GetResult();
            Assert.Equal(WorkspaceSaveStatus.Saved, outcome.Status);
            Assert.Equal(control.CaptureCloseState().Revision, outcome.SubmittedRevision);
            Assert.False(control.CaptureCloseState().IsDirty);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Close_save_commits_native_pending_quantity_before_submitting_and_tracks_committed_revision()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            var control = (SalesReceiptControl)host.Control;
            host.Begin(3);
            var quantity = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapDecimalInput>("QuantityEditor");
            var native = new NativeLookupTypingDriver(quantity);
            native.SelectRange(0, native.Text.Length);
            foreach (var character in "2.125") native.TypeCharacter(character);
            Assert.Equal("2.125", native.Text);
            Assert.Equal(5, native.SelectionStart);
            host.Api.NextUpdate = new SalesReceiptDto(41, "SA0000000041", host.Editor.SaleDate, 1, 2, 3,
                "Draft", "note", null, "AgIDBAUGBwg=",
                new[] { new SalesReceiptLineDto(91, 1, 7, "box", 2.125m, 4m, false, 8.5m,
                    null, null, "P7 - Product", true) }, 8.5m, null, null,
                "C1 - Customer", true, "W2 - Warehouse", true, "E3 - Employee", true);

            var pending = control.SaveForCloseAsync(CancellationToken.None);
            host.Wait(pending);
            var outcome = pending.GetAwaiter().GetResult();

            Assert.Equal(WorkspaceSaveStatus.Saved, outcome.Status);
            Assert.Equal(2.125m, host.Api.Updates.Single().Lines.Single().Qty);
            Assert.Equal(2.125m, host.Editor.Lines.Single().Quantity);
            Assert.Equal(control.CaptureCloseState().Revision, outcome.SubmittedRevision);
            Assert.False(control.CaptureCloseState().IsDirty);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Close_save_rejects_invalid_native_quantity_without_dispatch_or_losing_raw_text()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            var control = (SalesReceiptControl)host.Control;
            host.Begin(3);
            var quantity = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapDecimalInput>("QuantityEditor");
            var native = new NativeLookupTypingDriver(quantity);
            native.SelectRange(0, native.Text.Length);
            foreach (var character in "1.00001") native.TypeCharacter(character);
            Assert.Equal("1.00001", native.Text);
            Assert.Equal(7, native.SelectionStart);

            var pending = control.SaveForCloseAsync(CancellationToken.None);
            host.Wait(pending);
            var outcome = pending.GetAwaiter().GetResult();

            Assert.NotEqual(WorkspaceSaveStatus.Saved, outcome.Status);
            Assert.Empty(host.Api.Updates);
            Assert.Equal("1.00001", quantity.Text);
            Assert.Equal(1m, host.Editor.Lines.Single().Quantity);
            Assert.True(control.CaptureCloseState().IsDirty);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(20));
    }

    [Theory]
    [InlineData(ApiStatusCode.Forbidden, false)]
    [InlineData(ApiStatusCode.Unauthorized, false)]
    [InlineData(ApiStatusCode.Forbidden, true)]
    [InlineData(ApiStatusCode.Unauthorized, true)]
    public void List_authorization_denial_scrubs_retained_detail_and_explicit_read_restore_keeps_original_recovery(ApiStatusCode status, bool recovery)
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            if (recovery) { host.CallTask("PostAsync"); Assert.True(host.Editor.RequiresPostOutcomeReload); }
            else Assert.True((bool)host.Call("TryShowReceiptList")!);
            host.Api.ListHandler = t => Task.FromResult(ApiResponse<SalesReceiptListPage>.Failure(new ApiError(status, "Test.Denied", "Denied")));
            host.Wait(List(host).RefreshAsync());
            Assert.True(host.Editor.IsAccessDenied); Assert.Empty(host.Editor.Lines); Assert.Equal(0, host.Editor.SaleId);
            Assert.Equal(0, host.Editor.CustomerId); Assert.Null(host.Editor.TotalCost); Assert.False((bool)host.Call("TryShowReceiptDetail")!);
            Assert.False(host.Editor.SetCustomer(8, "Denied", true)); host.CallTask("SaveAsync"); host.CallTask("PostAsync");
            Assert.Empty(host.Api.Updates); Assert.Equal(recovery ? 1 : 0, host.Api.Posts.Count);
            Assert.Equal(recovery, host.Editor.RequiresPostOutcomeReload);
            host.Api.ListHandler = null; host.CallTask("ActivateAsync", CancellationToken.None);
            Assert.False(host.Editor.IsAccessDenied); Assert.Equal(recovery, host.Editor.RequiresPostOutcomeReload);
            if (recovery)
            {
                Assert.False(host.Editor.NewDraft());
                host.Api.GetHandler = id => Task.FromResult(ApiResponse<SalesReceiptDto>.Failure(new ApiError(ApiStatusCode.Forbidden, "Test.ReloadDenied", "Denied")));
                host.CallTask("ReloadAsync"); Assert.True(host.Editor.RequiresPostOutcomeReload); Assert.True(host.Editor.IsAccessDenied); Assert.Equal(0, host.Editor.SaleId);
                host.CallTask("ActivateAsync", CancellationToken.None); host.Api.GetHandler = null; host.CallTask("ReloadAsync");
                Assert.Equal(41, host.Api.Gets.Last()); Assert.Equal(41, host.Editor.SaleId); Assert.False(host.Editor.RequiresPostOutcomeReload); Assert.Single(host.Api.Posts);
            }
            else { host.Load(); Assert.Equal(41, host.Editor.SaleId); }
            Assert.False(List(host).IsAccessDenied);
        }, TimeSpan.FromSeconds(25));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Canceled_or_non_authorization_list_failure_does_not_scrub_retained_detail(bool cancel)
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            using var cancellation = new CancellationTokenSource();
            var completion = new TaskCompletionSource<ApiResponse<SalesReceiptListPage>>();
            host.Api.ListHandler = t => completion.Task;
            var pending = List(host).RefreshAsync(cancellation.Token);
            if (cancel) cancellation.Cancel();
            completion.SetResult(ApiResponse<SalesReceiptListPage>.Failure(new ApiError(cancel ? ApiStatusCode.Forbidden : ApiStatusCode.InternalServerError, "Test.Failure", "Failure")));
            host.Wait(pending); Assert.False(host.Editor.IsAccessDenied); Assert.Equal(41, host.Editor.SaleId); Assert.Single(host.Editor.Lines);
        }, TimeSpan.FromSeconds(20));
    }
    [Fact]
    public void Delayed_list_locks_header_search_commit_date_note_and_commands_then_restores_editing()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            var control = (SalesReceiptControl)host.Control; var date = host.Editor.SaleDate; var note = host.Editor.Note;
            var completion = new TaskCompletionSource<ApiResponse<SalesReceiptListPage>>(); host.Api.ListHandler = t => completion.Task;
            var pending = List(host).RefreshAsync();
            try
            {
                var detailHost = Assert.Single(control.Controls.OfType<BootstrapBusyHost>(),
                    item => item.Content.Controls.Contains(control.LineGrid));
                Assert.True(detailHost.Visible);
                var overlay = Assert.Single(detailHost.Controls.OfType<Panel>(),
                    item => !ReferenceEquals(item, detailHost.Content));
                var until = DateTime.UtcNow.AddSeconds(2);
                while (!overlay.Visible && DateTime.UtcNow < until) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
                Assert.True(overlay.Visible);
                Assert.False(detailHost.Contains(control.SaveButton));
                Assert.False(control.CustomerLookup.Enabled); Assert.False(control.WarehouseLookup.Enabled); Assert.False(control.EmployeeLookup.Enabled);
                Assert.False(control.SaleDateEditor.Enabled); Assert.False(control.NoteEditor.Enabled); Assert.False(control.SearchBox.Enabled);
                Assert.False(control.FromDateEditor.Enabled); Assert.False(control.ToDateEditor.Enabled); Assert.False(control.StatusFilter.Enabled); Assert.False(control.PageSizeEditor.Enabled);
                foreach (var lookup in new[] { control.CustomerLookup, control.WarehouseLookup, control.EmployeeLookup }) { lookup.Text = "blocked query"; lookup.SelectValue(8); }
                control.SaleDateEditor.ProjectAcceptedValue(date.AddDays(1)); control.NoteEditor.Text = "blocked note";
                host.CallTask("SaveAsync"); host.CallTask("PostAsync");
                Assert.Equal(1, host.Editor.CustomerId); Assert.Equal(2, host.Editor.WarehouseId); Assert.Equal(3, host.Editor.EmployeeId); Assert.Equal(date, host.Editor.SaleDate); Assert.Equal(note, host.Editor.Note);
                Assert.Empty(host.Api.Updates); Assert.Empty(host.Api.Posts);
            }
            finally { completion.TrySetResult(ApiResponse<SalesReceiptListPage>.Failure(new ApiError(ApiStatusCode.InternalServerError, "Test.End", "End"))); host.Wait(pending); }
            var finishedHost = Assert.Single(control.Controls.OfType<BootstrapBusyHost>(),
                item => item.Content.Controls.Contains(control.LineGrid));
            Assert.False(Assert.Single(finishedHost.Controls.OfType<Panel>(),
                item => !ReferenceEquals(item, finishedHost.Content)).Visible);
            Assert.True(control.NoteEditor.Enabled); control.NoteEditor.Text = "allowed"; Assert.Equal("allowed", host.Editor.Note);
        }, TimeSpan.FromSeconds(25));
    }
    [Fact]
    public void Known_post_then_list_denial_retains_private_same_id_recovery_until_explicit_authorized_reload()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            host.Api.PostHandler = () => Task.FromResult(ApiResponse<PostSalesReceiptResponse>.Success(new PostSalesReceiptResponse(41, "Posted", DateTime.UtcNow, "known-post-version")));
            host.Api.GetHandler = id => Task.FromResult(ApiResponse<SalesReceiptDto>.Failure(new ApiError(ApiStatusCode.InternalServerError, "Test.Detail", "Detail unavailable")));
            host.CallTask("PostAsync"); Assert.Equal("Posted", host.Editor.Status); Assert.Equal("known-post-version", host.Editor.Version); Assert.True(host.Editor.RequiresPostOutcomeReload);
            host.Api.ListHandler = t => Task.FromResult(ApiResponse<SalesReceiptListPage>.Failure(new ApiError(ApiStatusCode.Forbidden, "Test.Denied", "Denied")));
            host.Wait(List(host).RefreshAsync()); Assert.Empty(host.Editor.Lines); Assert.Equal(0, host.Editor.SaleId); Assert.True(host.Editor.RequiresPostOutcomeReload);
            host.Api.ListHandler = null; host.CallTask("ActivateAsync", CancellationToken.None);
            Assert.False(host.Editor.NewDraft()); host.CallTask("SelectReceiptAsync", 99L, CancellationToken.None); Assert.DoesNotContain(99, host.Api.Gets);
            host.Api.GetHandler = id => Task.FromResult(ApiResponse<SalesReceiptDto>.Success(SalesGuiApi.Receipt(id, true)));
            host.CallTask("ReloadAsync"); Assert.Equal(41, host.Api.Gets.Last()); Assert.Equal("Posted", host.Editor.Status); Assert.Equal(2m, host.Editor.TotalCost); Assert.False(host.Editor.RequiresPostOutcomeReload); Assert.Single(host.Api.Posts);
        }, TimeSpan.FromSeconds(25));
    }
    [Fact]
    public void Unit_callback_started_before_list_busy_cannot_mutate_document_during_control_lock()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); var products = new SalesDeferredProducts(); using var host = new SalesControlHost(products); host.Load();
            host.SelectProduct(8); var row = host.Editor.Lines.Single(); Assert.Equal("", row.Unit);
            var completion = new TaskCompletionSource<ApiResponse<SalesReceiptListPage>>(); host.Api.ListHandler = t => completion.Task; var pending = List(host).RefreshAsync();
            try { products.CompleteAll("late-busy-unit"); host.Wait(host.Value<Task>("LastOperation")); Assert.Equal("", row.Unit); Assert.Equal(4m, row.UnitPrice); }
            finally { products.CompleteAll("cleanup"); completion.TrySetResult(ApiResponse<SalesReceiptListPage>.Failure(new ApiError(ApiStatusCode.InternalServerError, "Test.End", "End"))); host.Wait(pending); }
        }, TimeSpan.FromSeconds(25));
    }
    [Fact]
    public void List_denial_fences_inflight_unit_and_older_permission_activation_without_restoring_protected_data()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); var products = new SalesDeferredProducts(); var permissions = new SalesPermissionGate();
            using var host = new SalesControlHost(products, permissions); host.Load(); host.SelectProduct(8); var stale = host.Editor.Lines.Single();
            permissions.Read = new TaskCompletionSource<ApiResponse<PermissionDecisionDto>>(); var activation = (Task)host.Call("ActivateAsync", CancellationToken.None)!;
            host.Api.ListHandler = t => Task.FromResult(ApiResponse<SalesReceiptListPage>.Failure(new ApiError(ApiStatusCode.Unauthorized, "Test.Denied", "Denied")));
            host.Wait(List(host).RefreshAsync());
            products.CompleteAll("obsolete-unit"); permissions.Read.TrySetResult(ApiResponse<PermissionDecisionDto>.Success(new PermissionDecisionDto("Sales.Receipts.Read", true)));
            host.Wait(activation); host.Wait(host.Value<Task>("LastOperation"));
            Assert.True(host.Editor.IsAccessDenied); Assert.Empty(host.Editor.Lines); Assert.Equal("", stale.Unit); Assert.Equal(0, host.Editor.SaleId); Assert.False((bool)host.Call("TryShowReceiptDetail")!);
        }, TimeSpan.FromSeconds(25));
    }
    internal static SalesReceiptListViewModel List(SalesControlHost host) => (SalesReceiptListViewModel)host.Control.GetType().GetField("_list", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host.Control)!;
}
internal sealed class SalesPermissionGate : IPermissionApiClient
{
    internal TaskCompletionSource<ApiResponse<PermissionDecisionDto>>? Read;
    public Task<ApiResponse<PermissionDecisionDto>> CheckAsync(string key, CancellationToken token) =>
        key == "Sales.Receipts.Read" && Read != null ? Read.Task : Task.FromResult(ApiResponse<PermissionDecisionDto>.Success(new PermissionDecisionDto(key, true)));
}
