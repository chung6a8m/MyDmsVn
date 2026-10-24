using System;
using System.Linq;
using System.Collections.Specialized;
using System.Threading.Tasks;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed partial class SalesReceiptEditorViewModelTests
{
    [Fact]
    public async Task Numeric_patch_clears_only_server_issues_for_changed_fields()
    {
        var api = new SalesApi();
        using var vm = Editor(api);
        await vm.LoadAsync(41);
        var line = vm.Lines.Single();
        vm.Note = "changed";
        api.Update = _ => Task.FromResult(MyDmsVn.Contracts.ApiResponse<MyDmsVn.Contracts.SalesReceiptDto>.Failure(
            new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.BadRequest, "SalesReceipt.Invalid", "Review line.", new[]
            {
                new MyDmsVn.Contracts.ApiErrorDetail("SalesReceipt.ProductInactive", "Product inactive.", "Request.Lines[0].ProductId"),
                new MyDmsVn.Contracts.ApiErrorDetail("Validation.PrecisionScale", "Quantity invalid.", "Request.Lines[0].Qty"),
                new MyDmsVn.Contracts.ApiErrorDetail("Validation.PrecisionScale", "Price invalid.", "Request.Lines[0].UnitPrice"),
            })));
        await vm.SaveAsync();
        Assert.Equal(3, vm.ValidationIssues.Count);

        var patch = new NumericDraftPatch<SalesReceiptLineViewModel>(vm.CaptureNumericDraft(),
            new[] { new NumericDraftChange<SalesReceiptLineViewModel>(line, NumericDraftField.Quantity, line.Quantity + 1m) });
        Assert.True(vm.TryApplyNumericPatch(patch, out var error), error);
        Assert.Contains(vm.ValidationIssues, issue => ReferenceEquals(issue.Line, line) && issue.Field == "productId");
        Assert.Contains(vm.ValidationIssues, issue => ReferenceEquals(issue.Line, line) && issue.Field == "unitPrice");
        Assert.DoesNotContain(vm.ValidationIssues, issue => ReferenceEquals(issue.Line, line) && issue.Field == "qty");
    }

    [Fact]
    public async Task NumericDraftPatchTests_ordinary_line_first_notification_rejects_older_patch()
    {
        using var vm = Editor();
        await vm.LoadAsync(41);
        var line = vm.Lines.Single();
        var snapshot = vm.CaptureNumericDraft();
        var patch = new NumericDraftPatch<SalesReceiptLineViewModel>(snapshot,
            new[] { new NumericDraftChange<SalesReceiptLineViewModel>(line, NumericDraftField.Quantity, 3m) });
        bool? admitted = null;
        line.PropertyChanged += (_, _) =>
        {
            if (!admitted.HasValue) admitted = vm.TryApplyNumericPatch(patch, out _);
        };

        var newer = line.Quantity + 1m;
        line.Quantity = newer;

        Assert.False(admitted);
        Assert.Equal(newer, line.Quantity);
        Assert.True(vm.EditRevision > snapshot.Revision);
    }

    [Fact]
    public async Task NumericDraftPatchTests_batch_publishes_only_after_all_values_and_blocks_nested_writes()
    {
        var api = new SalesApi();
        using var vm = Editor(api);
        await vm.LoadAsync(41);
        var first = vm.Lines.Single();
        var second = vm.AddLine()!;
        second.ProductId = first.ProductId;
        var snapshot = vm.CaptureNumericDraft();
        var patch = new NumericDraftPatch<SalesReceiptLineViewModel>(snapshot, new[]
        {
            new NumericDraftChange<SalesReceiptLineViewModel>(first, NumericDraftField.Quantity, 2m),
            new NumericDraftChange<SalesReceiptLineViewModel>(first, NumericDraftField.UnitPrice, 5m),
            new NumericDraftChange<SalesReceiptLineViewModel>(second, NumericDraftField.Quantity, 3m),
            new NumericDraftChange<SalesReceiptLineViewModel>(second, NumericDraftField.UnitPrice, 7m),
        });
        var observed = 0; var dirtyNotices = 0;
        first.PropertyChanged += (_, _) =>
        {
            if (observed++ != 0) return;
            Assert.Equal((2m, 5m, 3m, 7m), (first.Quantity, first.UnitPrice, second.Quantity, second.UnitPrice));
            Assert.True(vm.EditRevision > snapshot.Revision);
            first.Quantity = 99m;
            Assert.Equal(2m, first.Quantity);
            Assert.False(vm.TryApplyNumericPatch(patch, out _));
        };
        vm.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(vm.IsDirty)) dirtyNotices++; };

        Assert.True(vm.TryApplyNumericPatch(patch, out var error), error);
        Assert.Equal(1, dirtyNotices);
        Assert.Equal(2m, first.Quantity);
        Assert.Equal(3m, second.Quantity);
        Assert.Empty(api.Creates); Assert.Empty(api.Updates); Assert.Empty(api.Posts);
    }

    [Fact]
    public async Task NumericDraftPatchTests_stale_or_removed_line_rejects_without_mutation()
    {
        using var vm = Editor();
        await vm.LoadAsync(41);
        var line = vm.Lines.Single();
        var snapshot = vm.CaptureNumericDraft();
        var patch = new NumericDraftPatch<SalesReceiptLineViewModel>(snapshot,
            new[] { new NumericDraftChange<SalesReceiptLineViewModel>(line, NumericDraftField.Quantity, 3m) });
        var before = line.Quantity;
        vm.RemoveLine(line);

        Assert.False(vm.TryApplyNumericPatch(patch, out _));
        Assert.Equal(before, line.Quantity);
        Assert.Empty(vm.Lines);
    }

    [Fact]
    public async Task NumericDraftPatchTests_header_and_collection_first_callbacks_see_new_revision()
    {
        using var vm = Editor();
        await vm.LoadAsync(41);
        var snapshot = vm.CaptureNumericDraft();
        long headerRevision = -1; long collectionRevision = -1;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.Note) && headerRevision < 0) headerRevision = vm.EditRevision;
        };
        ((INotifyCollectionChanged)vm.Lines).CollectionChanged += (_, _) =>
        { if (collectionRevision < 0) collectionRevision = vm.EditRevision; };

        vm.Note = "changed";
        var afterHeader = vm.EditRevision;
        vm.AddLine();

        Assert.True(headerRevision > snapshot.Revision);
        Assert.True(collectionRevision > afterHeader);
    }

    [Fact]
    public async Task NumericDraftPatchTests_invalid_amount_rejects_entire_sale_without_notifications()
    {
        using var vm = Editor();
        await vm.LoadAsync(41);
        var line = vm.Lines.Single();
        var before = (line.Quantity, line.UnitPrice, vm.EditRevision);
        var snapshot = vm.CaptureNumericDraft();
        var patch = new NumericDraftPatch<SalesReceiptLineViewModel>(snapshot, new[]
        {
            new NumericDraftChange<SalesReceiptLineViewModel>(line, NumericDraftField.Quantity, 2m),
            new NumericDraftChange<SalesReceiptLineViewModel>(line, NumericDraftField.UnitPrice, -1m),
        });
        var notifications = 0;
        line.PropertyChanged += (_, _) => notifications++;

        Assert.False(vm.TryApplyNumericPatch(patch, out _));
        Assert.Equal(before, (line.Quantity, line.UnitPrice, vm.EditRevision));
        Assert.Equal(0, notifications);
    }

    [Fact]
    public async Task NumericDraftPatchTests_sales_permission_loss_rejects_captured_patch()
    {
        using var vm = Editor();
        await vm.LoadAsync(41);
        var line = vm.Lines.Single();
        var patch = new NumericDraftPatch<SalesReceiptLineViewModel>(vm.CaptureNumericDraft(),
            new[] { new NumericDraftChange<SalesReceiptLineViewModel>(line, NumericDraftField.Quantity, 3m) });
        var before = line.Quantity;
        vm.SetAccess(true, false, false);
        Assert.False(vm.TryApplyNumericPatch(patch, out _));
        Assert.Equal(before, line.Quantity);
    }
}
