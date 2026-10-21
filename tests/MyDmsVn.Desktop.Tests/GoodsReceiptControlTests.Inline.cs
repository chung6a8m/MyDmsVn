using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.BootstrapSourceGrid.Editors;
using MyDmsVn.Desktop.WinForms;
using SourceGrid.Cells.Editors;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed partial class GoodsReceiptControlTests
{
    [Fact]
    public void Native_tab_skips_computed_cells_without_creating_a_line() => Integrated((fixture, control) =>
    {
        control.AddLineButton.PerformClick();
        BeginInline(control, 3).Text = "4.75";
        GoodsReceiptInlineEditorTests.Key(Keys.Tab);
        Assert.Equal(new SourceGrid.Position(2, 1), control.LineGrid.Selection.ActivePosition);
        InlineReceiptTestDriver.ShiftTab();
        Assert.Equal(new SourceGrid.Position(1, 3), control.LineGrid.Selection.ActivePosition);
        BeginInline(control, 3, 2).Text = "2";
        GoodsReceiptInlineEditorTests.Key(Keys.Tab);
        Assert.Equal(4.75m, fixture.Editor.Lines[0].UnitCost);
        Assert.Equal(2, fixture.Editor.Lines.Count);
    });

    [Fact]
    public void Receipt_pointer_navigation_cancels_query_before_native_focus_resolution() => Integrated((fixture, control) =>
    {
        fixture.Editor.Load(ReceiptApiClient.Receipt(status: "Draft", productId: 99,
            productDisplayName: "Historical product", productIsActive: false));
        var line = fixture.Editor.Lines[0];
        BeginInline(control, 1).Text = "Product - Choice";
        InlineReceiptTestDriver.MouseDown(control.ReceiptGrid);
        InlineReceiptTestDriver.MouseUp(control.ReceiptGrid);
        Assert.Equal(99, line.ProductId);
        Assert.False(fixture.Editor.IsDirty);
        Assert.False(control.ProductLookup.HasPendingText);
    });

    [Fact]
    public void Native_commit_errors_are_visible_and_clear_after_cancel() => Integrated((fixture, control) =>
    {
        BeginInline(control, 2).Text = "1.23001";
        GoodsReceiptInlineEditorTests.Key(Keys.Enter);
        Assert.Contains("decimal places", control.OperationStatusLabel.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("1.23001", control.QuantityEditor.Text);
        Assert.Equal(2.5m, fixture.Editor.Lines[0].Quantity);
        Assert.True(control.QuantityEditor.ContainsFocus);
        GoodsReceiptInlineEditorTests.Key(Keys.Tab);
        Assert.Equal(new SourceGrid.Position(1, 2), control.LineGrid.Selection.ActivePosition);
        Assert.Contains("decimal places", control.OperationStatusLabel.Text);
        GoodsReceiptInlineEditorTests.Key(Keys.Escape);
        Assert.DoesNotContain("decimal places", control.OperationStatusLabel.Text, StringComparison.OrdinalIgnoreCase);
    });

    [Fact]
    public void Rejected_navigation_preserves_query_when_the_document_is_already_dirty() => Integrated((fixture, control) =>
    {
        fixture.Editor.Note = "dirty header";
        BeginInline(control, 1).Text = "pending query";
        InlineReceiptTestDriver.MouseDown(control.NewButton);
        Assert.True(control.ProductLookup.HasPendingText);
        InlineReceiptTestDriver.MouseUp(control.NewButton);
        Assert.Equal(41, fixture.Editor.Id);
        Assert.Equal("pending query", control.ProductLookup.Text);
        Assert.True(control.ProductLookup.HasPendingText);
        Assert.True(new SourceGrid.CellContext(control.LineGrid, new SourceGrid.Position(1, 1)).IsEditing());
    });

    [Fact]
    public void Historical_product_display_does_not_enable_inactive_new_selection() => Integrated((fixture, control) =>
    {
        fixture.Editor.Load(ReceiptApiClient.Receipt(status: "Draft", productId: 99,
            productDisplayName: "Historical product", productIsActive: false));
        BeginInline(control, 1);
        Assert.Equal("Historical product", control.ProductLookup.CommittedDisplayText);
        GoodsReceiptInlineEditorTests.Key(Keys.Escape);
        control.AddLineButton.PerformClick();
        var context = new SourceGrid.CellContext(control.LineGrid, new SourceGrid.Position(2, 1));
        context.StartEdit();
        Assert.True(context.IsEditing());
        control.ProductLookup.SelectValue(99);
        control.SaveButton.PerformClick();
        Assert.Null(fixture.Api.LastUpdate);
        Assert.Equal(0, fixture.Editor.Lines[1].ProductId);
        Assert.True(context.IsEditing());
    });

    [Fact]
    public void Lookup_popup_keys_mouse_and_focus_commit_once() => Integrated((fixture, control) =>
    {
        fixture.Editor.Load(ReceiptApiClient.Receipt(status: "Draft", productId: 99,
            productDisplayName: "Historical product", productIsActive: false));
        var commits = 0;
        fixture.Editor.Lines[0].PropertyChanged += (_, e) => { if (e.PropertyName == "ProductId") commits++; };
        BeginInline(control, 1);
        control.ProductLookup.Text = "";
        GoodsReceiptInlineEditorTests.Type("Choice");
        GoodsReceiptInlineEditorTests.Key(Keys.F4);
        Assert.True(control.ProductLookup.IsDropDownOpen);
        GoodsReceiptInlineEditorTests.Key(Keys.PageDown);
        GoodsReceiptInlineEditorTests.Key(Keys.PageUp);
        GoodsReceiptInlineEditorTests.Key(Keys.Down);
        GoodsReceiptInlineEditorTests.Key(Keys.Enter);
        Assert.True(fixture.Editor.Lines[0].ProductId == 7, "text=" + control.ProductLookup.Text + "; selected=" + control.ProductLookup.SelectedValue + "; rows=" + control.ProductLookup.ResultsGrid.Rows.Count + "; input=" + control.OperationStatusLabel.Text);
        Assert.Equal(1, commits);
        BeginInline(control, 1);
        control.ProductLookup.Text = "";
        GoodsReceiptInlineEditorTests.Type("unmatched");
        GoodsReceiptInlineEditorTests.Key(Keys.F4);
        GoodsReceiptInlineEditorTests.Key(Keys.Escape);
        Assert.False(control.ProductLookup.IsDropDownOpen);
        Assert.Equal(7, fixture.Editor.Lines[0].ProductId);
        GoodsReceiptInlineEditorTests.Key(Keys.Escape);
        Assert.Equal(1, commits);
        BeginInline(control, 2);
        control.QuantityEditor.Text = "6.25";
        GoodsReceiptInlineEditorTests.Key(Keys.Tab);
        Assert.Equal(6.25m, fixture.Editor.Lines[0].Quantity);
        Assert.Equal(1, commits);
        fixture.Editor.Load(ReceiptApiClient.Receipt(status: "Draft", productId: 99,
            productDisplayName: "Historical product", productIsActive: false));
        fixture.Editor.Lines[0].PropertyChanged += (_, e) => { if (e.PropertyName == "ProductId") commits++; };
        BeginInline(control, 1);
        control.ProductLookup.Text = "";
        GoodsReceiptInlineEditorTests.Type("Choice");
        GoodsReceiptInlineEditorTests.Key(Keys.F4);
        Assert.True(control.ProductLookup.IsDropDownOpen);
        var results = control.ProductLookup.ResultsGrid;
        var rectangle = results.GetCellDisplayRectangle(0, 0, true);
        Assert.True(rectangle.Width > 0 && rectangle.Height > 0);
        InlineReceiptTestDriver.Click(results, new Point(rectangle.Left + rectangle.Width / 2, rectangle.Top + rectangle.Height / 2), control.FindForm());
        Assert.Equal(7, fixture.Editor.Lines[0].ProductId);
        Assert.Equal(2, commits);
        BeginInline(control, 2).Text = "8.5";
        control.NoteEditor.Focus();
        Assert.True(control.NoteEditor.ContainsFocus);
        System.Windows.Forms.Application.DoEvents();
        Assert.Equal(8.5m, fixture.Editor.Lines[0].Quantity);
        Assert.Equal(2, commits);
    });

    [Fact]
    public void Late_lookup_results_cannot_cross_row_receipt_or_session() => Integrated((fixture, control) =>
    {
        BeginInline(control, 1);
        fixture.ProductSource.HoldQueries = true;
        control.ProductLookup.Text = "old query";
        control.NewButton.PerformClick();
        Assert.Null(fixture.Editor.Id);
        Assert.Empty(fixture.Editor.Lines);
        Assert.Equal(2, fixture.ProductSource.Pending.Count);
        fixture.ProductSource.Pending[1].SetResult(MyDmsVn.Contracts.ApiResponse<System.Collections.Generic.IReadOnlyList<MyDmsVn.Contracts.CatalogLookupDto>>.Success(
            new[] { new MyDmsVn.Contracts.CatalogLookupDto(9, "NEW", "Current result", true) }));
        PumpMessagesUntil(() => System.Linq.Enumerable.Any(fixture.Editor.ProductOptions.Items, item => item.Id == 9), TimeSpan.FromSeconds(2));
        fixture.ProductSource.Pending[0].SetResult(MyDmsVn.Contracts.ApiResponse<System.Collections.Generic.IReadOnlyList<MyDmsVn.Contracts.CatalogLookupDto>>.Success(
            new[] { new MyDmsVn.Contracts.CatalogLookupDto(98, "OLD", "Old result", true) }));
        // Drain the deliberately late provider continuation without a timing-only assertion.
        PumpMessagesUntil(() => fixture.ProductSource.Pending[0].Task.IsCompleted, TimeSpan.FromSeconds(2));
        Assert.False(control.ProductLookup.HasPendingText);
        Assert.Empty(fixture.Editor.Lines);
        Assert.False(fixture.Editor.IsDirty);
        Assert.DoesNotContain(fixture.Editor.ProductOptions.Items, item => item.Id == 98);
        fixture.ProductSource.HoldQueries = false;
    });

    [Fact]
    public void Overflow_preserves_input_and_blocks_commands() => Integrated((fixture, control) =>
    {
        var input = BeginInline(control, 2);
        input.Text = "999999999999999999999999999999999";
        control.SaveButton.PerformClick();
        Assert.Null(fixture.Api.LastUpdate);
        Assert.Equal("999999999999999999999999999999999", input.Text);
        Assert.Equal(2.5m, fixture.Editor.Lines[0].Quantity);
        input.Text = "0";
        control.PostButton.PerformClick();
        Assert.Null(fixture.Api.LastPost);
        Assert.Equal("0", input.Text);
    });

    [Fact]
    public void Denied_or_readonly_state_blocks_active_and_future_edits() => Integrated((fixture, control) =>
    {
        BeginInline(control, 2).Text = "19";
        fixture.Api.ControlGetResponse = true;
        // A guarded navigation must preserve the pending candidate.
        control.RefreshButton.PerformClick();
        Assert.Equal("19", control.QuantityEditor.Text);
        fixture.Editor.Load(ReceiptApiClient.Receipt(status: "Posted"));
        var context = new SourceGrid.CellContext(control.LineGrid, new SourceGrid.Position(1, 2));
        GoodsReceiptInlineEditorTests.Key(Keys.F2);
        Assert.False(context.IsEditing());
        control.QuantityEditor.Text = "20";
        Assert.Equal(2.5m, fixture.Editor.Lines[0].Quantity);
        fixture.Api.ListError = new MyDmsVn.Contracts.ApiError(MyDmsVn.Contracts.ApiStatusCode.Forbidden, "Auth.Forbidden", "Denied");
        fixture.Api.ControlGetResponse = false;
        control.RefreshButton.PerformClick();
        control.LastOperation.GetAwaiter().GetResult();
        Assert.Null(fixture.Editor.Id);
        Assert.Empty(fixture.Editor.Lines);
    });

    [Fact]
    public void Save_flushes_active_edit_or_dispatches_nothing() => Integrated((fixture, control) =>
    {
        var lookup = BeginInline(control, 1);
        lookup.Text = "unresolved product";
        control.SaveButton.PerformClick();
        Assert.Null(fixture.Api.LastUpdate);
        Assert.Equal("unresolved product", lookup.Text);
        Assert.Equal(7, fixture.Editor.Lines[0].ProductId);
        GoodsReceiptInlineEditorTests.Key(Keys.Escape);
        if (new SourceGrid.CellContext(control.LineGrid, new SourceGrid.Position(1, 1)).IsEditing())
            GoodsReceiptInlineEditorTests.Key(Keys.Escape);
        Assert.False(new SourceGrid.CellContext(control.LineGrid, new SourceGrid.Position(1, 1)).IsEditing(), "Escape must cancel the cell");
        var input = BeginInline(control, 2);
        input.Text = "1.23001";
        control.SaveButton.PerformClick();
        Assert.Null(fixture.Api.LastUpdate);
        Assert.Equal("1.23001", input.Text);
        Assert.Equal(2.5m, fixture.Editor.Lines[0].Quantity);
        input.Text = "7.25000";
        control.SaveButton.PerformClick();
        control.LastOperation.GetAwaiter().GetResult();
        Assert.Equal(7.25m, fixture.Api.LastUpdate!.Lines[0].Quantity);
    });

    [Fact]
    public void Post_never_auto_saves_or_uses_pre_edit_values() => Integrated((fixture, control) =>
    {
        BeginInline(control, 2).Text = "9.125";
        control.PostButton.PerformClick();
        control.LastOperation.GetAwaiter().GetResult();
        Assert.Equal(9.125m, fixture.Editor.Lines[0].Quantity);
        Assert.True(fixture.Editor.IsDirty);
        Assert.Null(fixture.Api.LastPost);
        Assert.Null(fixture.Api.LastUpdate);
    });

    [Fact]
    public void All_edit_intents_observe_their_commit_or_cancel_policy() => Integrated((fixture, control) =>
    {
        var input = BeginInline(control, 2);
        input.Text = "12";
        control.NewButton.PerformClick();
        control.RefreshButton.PerformClick();
        control.SelectReceiptAsync(41, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal("12", input.Text);
        Assert.Equal(41, fixture.Editor.Id);
        Assert.Equal(2.5m, fixture.Editor.Lines[0].Quantity);
        control.AddLineButton.PerformClick();
        Assert.Equal(12m, fixture.Editor.Lines[0].Quantity);
        Assert.Equal(2, fixture.Editor.Lines.Count);
        Assert.Equal(new SourceGrid.Position(2, 1), control.LineGrid.Selection.ActivePosition);
        control.RemoveLineButton.PerformClick();
        Assert.Single(fixture.Editor.Lines);
    });

    [Fact]
    public void Current_query_survives_same_context_refresh() => Integrated((fixture, control) =>
    {
        var input = BeginInline(control, 1);
        input.Text = "remote product";
        var native = GoodsReceiptInlineEditorTests.FocusedInput();
        native.SelectionStart = 3;
        native.SelectionLength = 2;
        fixture.Editor.ProductOptions.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(3, native.SelectionStart);
        Assert.Equal(2, native.SelectionLength);
        Assert.Equal("remote product", input.Text);
        Assert.Equal(7, fixture.Editor.Lines[0].ProductId);
        Assert.False(fixture.Editor.IsDirty);
        Assert.Contains(fixture.ProductSource.Requests, request => request.Search == "remote product");
    });

    [Fact]
    public void Explicit_new_cancels_pending_lookup_after_navigation_guard() => Integrated((fixture, control) =>
    {
        fixture.Editor.Load(ReceiptApiClient.Receipt(status: "Draft", productId: 99,
            productDisplayName: "Historical product", productIsActive: false));
        var oldLine = fixture.Editor.Lines[0];
        var mutations = 0;
        oldLine.PropertyChanged += (_, e) => { if (e.PropertyName == "ProductId") mutations++; };
        var input = BeginInline(control, 1);
        input.Text = "Product - Choice";
        Assert.True(control.ProductLookup.HasPendingText);
        InlineReceiptTestDriver.MouseDown(control.NewButton);
        InlineReceiptTestDriver.MouseUp(control.NewButton, false);
        Assert.True(control.ProductLookup.HasPendingText);
        Assert.Equal(41, fixture.Editor.Id);
        Assert.Equal(99, oldLine.ProductId);
        InlineReceiptTestDriver.MouseDown(control.NewButton);
        Assert.True(control.ProductLookup.HasPendingText);
        InlineReceiptTestDriver.MouseUp(control.NewButton);
        System.Windows.Forms.Application.DoEvents();
        Assert.Null(fixture.Editor.Id);
        Assert.Empty(fixture.Editor.Lines);
        Assert.False(fixture.Editor.IsDirty);
        Assert.Equal(99, oldLine.ProductId);
        Assert.Equal(0, mutations);
    });

    private static void Integrated(Action<Fixture, GoodsReceiptControl> action) => StaTest.Run(token =>
    {
        using var guard = new WinFormsTestGuard();
        using var fixture = new Fixture();
        fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
        using var control = new GoodsReceiptControl(fixture.List, fixture.Editor);
        using var form = new Form { Size = new Size(1500, 850), TopMost = true };
        form.Controls.Add(control);
        form.Show();
        var split = System.Linq.Enumerable.Single(System.Linq.Enumerable.OfType<SplitContainer>(control.Controls));
        split.SplitterDistance = 420;
        control.ActivateAsync(token).GetAwaiter().GetResult();
        control.SelectReceiptAsync(41, token).GetAwaiter().GetResult();
        action(fixture, control);
        Assert.Empty(guard.Exceptions);
    }, TimeSpan.FromSeconds(15));

    private static Control BeginInline(GoodsReceiptControl control, int column, int row = 1)
    {
        var grid = control.LineGrid;
        control.FindForm()!.Activate();
        Assert.True(grid.Focus());
        Assert.True(grid.Selection.Focus(new SourceGrid.Position(row, column), true));
        var context = new SourceGrid.CellContext(grid, new SourceGrid.Position(row, column));
        Assert.NotNull(context.Cell.Editor);
        context.Cell.View.Measure(context, Size.Empty);
        Assert.True(grid.Focus());
        GoodsReceiptInlineEditorTests.Key(Keys.F2);
        Assert.True(context.IsEditing(), "Native F2 failed for " + column + "; enabled=" + context.Cell.Editor.EnableEdit + "; value=" + context.Value);
        return ((EditorControlBase)context.Cell.Editor).Control;
    }
}
