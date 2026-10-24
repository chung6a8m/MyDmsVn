using System;
using System.Linq;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed partial class GoodsReceiptEditorViewModelTests
    {
        [Fact]
        public void NumericDraftPatchTests_goods_ordinary_first_notification_rejects_older_patch()
        {
            using var fixture = Create(new EditorApiClient());
            var editor = fixture.Editor;
            editor.Load(null);
            var line = editor.AddLine();
            var snapshot = editor.CaptureNumericDraft();
            var patch = new NumericDraftPatch<GoodsReceiptLineViewModel>(snapshot,
                new[] { new NumericDraftChange<GoodsReceiptLineViewModel>(line, NumericDraftField.Quantity, 3m) });
            bool? admitted = null;
            line.PropertyChanged += (_, _) =>
            {
                if (!admitted.HasValue) admitted = editor.TryApplyNumericPatch(patch, out _);
            };

            line.Quantity = 2m;

            Assert.False(admitted);
            Assert.Equal(2m, line.Quantity);
            Assert.True(editor.EditRevision > snapshot.Revision);
        }

        [Fact]
        public void NumericDraftPatchTests_goods_batch_is_complete_at_first_notification_and_never_calls_api()
        {
            var api = new EditorApiClient();
            using var fixture = Create(api);
            var editor = fixture.Editor;
            editor.Load(null);
            var first = editor.AddLine(); var second = editor.AddLine();
            var snapshot = editor.CaptureNumericDraft();
            var patch = new NumericDraftPatch<GoodsReceiptLineViewModel>(snapshot, new[]
            {
                new NumericDraftChange<GoodsReceiptLineViewModel>(first, NumericDraftField.Quantity, 2m),
                new NumericDraftChange<GoodsReceiptLineViewModel>(first, NumericDraftField.UnitCost, 5m),
                new NumericDraftChange<GoodsReceiptLineViewModel>(second, NumericDraftField.Quantity, 3m),
                new NumericDraftChange<GoodsReceiptLineViewModel>(second, NumericDraftField.UnitCost, 7m),
            });
            var observed = 0; var totalNotices = 0;
            first.PropertyChanged += (_, _) =>
            {
                if (observed++ != 0) return;
                Assert.Equal((2m, 5m, 3m, 7m), (first.Quantity, first.UnitCost, second.Quantity, second.UnitCost));
                first.Quantity = 99m;
                Assert.Equal(2m, first.Quantity);
                Assert.False(editor.TryApplyNumericPatch(patch, out _));
            };
            editor.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(editor.Total)) totalNotices++; };

            Assert.True(editor.TryApplyNumericPatch(patch, out var error), error);
            Assert.Equal(1, totalNotices);
            Assert.Equal(31m, editor.Total);
            Assert.Null(api.LastCreate); Assert.Null(api.LastUpdate); Assert.Null(api.LastPost);
        }

        [Fact]
        public void NumericDraftPatchTests_goods_direct_collection_event_advances_revision_before_callback()
        {
            using var fixture = Create(new EditorApiClient());
            var editor = fixture.Editor;
            editor.Load(null);
            var line = editor.AddLine();
            var snapshot = editor.CaptureNumericDraft();
            var patch = new NumericDraftPatch<GoodsReceiptLineViewModel>(snapshot,
                new[] { new NumericDraftChange<GoodsReceiptLineViewModel>(line, NumericDraftField.Quantity, 3m) });
            long revisionAtFirstEvent = -1;
            editor.Lines.CollectionChanged += (_, _) =>
            {
                if (revisionAtFirstEvent < 0) revisionAtFirstEvent = editor.EditRevision;
                Assert.False(editor.TryApplyNumericPatch(patch, out _));
            };
            editor.Lines.Remove(line);

            Assert.True(revisionAtFirstEvent > snapshot.Revision);
            Assert.False(editor.TryApplyNumericPatch(patch, out _));
            Assert.Empty(editor.Lines);
        }

        [Fact]
        public void NumericDraftPatchTests_goods_invalid_cost_rejects_all_cells_without_notifications()
        {
            using var fixture = Create(new EditorApiClient());
            var editor = fixture.Editor;
            editor.Load(null);
            var line = editor.AddLine();
            var before = (line.Quantity, line.UnitCost, editor.EditRevision);
            var patch = new NumericDraftPatch<GoodsReceiptLineViewModel>(editor.CaptureNumericDraft(), new[]
            {
                new NumericDraftChange<GoodsReceiptLineViewModel>(line, NumericDraftField.Quantity, 2m),
                new NumericDraftChange<GoodsReceiptLineViewModel>(line, NumericDraftField.UnitCost, -1m),
            });
            var notifications = 0;
            line.PropertyChanged += (_, _) => notifications++;

            Assert.False(editor.TryApplyNumericPatch(patch, out _));
            Assert.Equal(before, (line.Quantity, line.UnitCost, editor.EditRevision));
            Assert.Equal(0, notifications);
        }

        [Fact]
        public void NumericDraftPatchTests_goods_posted_receipt_rejects_numeric_patch()
        {
            using var fixture = Create(new EditorApiClient());
            var editor = fixture.Editor;
            editor.Load(Receipt("posted", "AQIDBAUGBwg=", "Posted"));
            var line = Assert.Single(editor.Lines);
            var patch = new NumericDraftPatch<GoodsReceiptLineViewModel>(editor.CaptureNumericDraft(),
                new[] { new NumericDraftChange<GoodsReceiptLineViewModel>(line, NumericDraftField.Quantity, 3m) });
            Assert.False(editor.TryApplyNumericPatch(patch, out _));
            Assert.Equal(2.5m, line.Quantity);
        }
    }
}
