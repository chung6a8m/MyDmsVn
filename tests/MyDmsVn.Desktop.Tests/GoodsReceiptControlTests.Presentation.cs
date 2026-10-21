using System;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed partial class GoodsReceiptControlTests
{
    [Fact]
    public void Back_to_list_rejects_dirty_or_pending_numeric_input() => Integrated((fixture, control) =>
    {
        Assert.Equal("Detail", PresentationMode(control));
        var line = fixture.Editor.Lines[0];
        BeginInline(control, 2).Text = "1.23001";
        Assert.False(ReturnToList(control));
        Assert.Equal("Detail", PresentationMode(control));
        Assert.Equal("1.23001", control.QuantityEditor.Text);
        Assert.Equal(2.5m, line.Quantity);
        Assert.True(control.QuantityEditor.ContainsFocus);
        GoodsReceiptInlineEditorTests.Key(Keys.Escape);
        fixture.Editor.Note = "dirty";
        Assert.False(ReturnToList(control));
        Assert.Equal("dirty", fixture.Editor.Note);
        Assert.Equal("Detail", PresentationMode(control));
    });

    [Theory]
    [InlineData("Warehouse", false)]
    [InlineData("Employee", false)]
    [InlineData("Product", false)]
    [InlineData("Warehouse", true)]
    [InlineData("Employee", true)]
    [InlineData("Product", true)]
    public void Back_mouse_down_does_not_commit_highlighted_lookup(string target, bool dirty) => Integrated((fixture, control) =>
    {
        if (target == "Product") fixture.Editor.Load(ReceiptApiClient.Receipt(status: "Draft", productId: 99,
            productDisplayName: "Historical product", productIsActive: false));
        if (dirty) fixture.Editor.Note = "dirty";
        var lookup = target == "Warehouse" ? control.WarehouseLookup : target == "Employee" ? control.EmployeeLookup : control.ProductLookup;
        if (target == "Product") { BeginInline(control, 1); Assert.True(lookup.ContainsFocus, "Product initial focus; visible=" + lookup.Visible + "; bounds=" + lookup.Bounds + "; grid=" + control.LineGrid.Bounds + "; parent=" + lookup.Parent?.GetType().Name); }
        var driver = new NativeLookupTypingDriver(lookup);
        driver.SelectRange(0, driver.Text.Length);
        foreach (var character in "Choice") driver.TypeCharacter(character);
        driver.Key(Keys.F4);
        driver.Key(Keys.Down);
        Assert.True(lookup.IsDropDownOpen);
        Assert.NotNull(lookup.HighlightedItem);
        if (target == "Product") Assert.NotEqual(lookup.SelectedValue,
            Assert.IsType<MyDmsVn.Desktop.Application.CatalogLookupOption>(lookup.HighlightedItem).Id);
        var original = lookup.SelectedValue;
        var window = driver.FocusedHandle;
        var back = PresentationButton(control, "Back to receipts");
        InlineReceiptTestDriver.MouseDown(back);
        Assert.Equal("Choice", driver.Text);
        driver.AssertFocused();
        InlineReceiptTestDriver.MouseUp(back, inside: false);
        Assert.Equal("Detail", PresentationMode(control));
        Assert.Equal("Choice", driver.Text);
        Assert.Equal(window, driver.FocusedHandle);
        Assert.Equal(original, lookup.SelectedValue);
        InlineReceiptTestDriver.MouseDown(back);
        InlineReceiptTestDriver.MouseUp(back);
        Assert.Equal(dirty ? "Detail" : "List", PresentationMode(control));
        Assert.Equal(original, lookup.SelectedValue);
        if (dirty) { Assert.Equal("Choice", driver.Text); driver.AssertFocused(); }
        else Assert.False(lookup.HasPendingText);
    });

    [Fact]
    public void Mode_switch_preserves_list_and_document_identity() => Integrated((fixture, control) =>
    {
        var line = fixture.Editor.Lines[0];
        var grid = control.LineGrid;
        grid.Selection.Focus(new SourceGrid.Position(1, 3), true);
        var version = fixture.Editor.Version;
        Assert.True(ReturnToList(control));
        Assert.Equal("List", PresentationMode(control));
        PresentationButton(control, "Open current receipt").PerformClick();
        Assert.Equal("Detail", PresentationMode(control));
        Assert.Same(grid, control.LineGrid);
        Assert.Same(line, fixture.Editor.Lines[0]);
        Assert.Equal(version, fixture.Editor.Version);
        Assert.Equal(new SourceGrid.Position(1, 3), grid.Selection.ActivePosition);
        Assert.False(fixture.Editor.IsDirty);
    });

    [Fact]
    public void Clean_posted_receipt_can_return_to_list_without_enabling_editing() => Integrated((fixture, control) =>
    {
        fixture.Editor.Load(ReceiptApiClient.Receipt(status: "Posted"));
        Assert.False(control.SaveButton.Enabled);
        Assert.True(ReturnToList(control));
        Assert.Equal("List", PresentationMode(control));
        PresentationButton(control, "Open current receipt").PerformClick();
        Assert.Equal("Detail", PresentationMode(control));
        Assert.False(control.SaveButton.Enabled);
        Assert.Equal("Posted", fixture.Editor.Status);
    });

    [Fact]
    public void Returning_to_list_preserves_result_cells_selection_and_filters() => Integrated((fixture, control) =>
    {
        Assert.True(ReturnToList(control));
        fixture.Api.ListRows = Enumerable.Range(1, 25).Select(index => new MyDmsVn.Contracts.GoodsReceiptDto(
            40 + index, "GR" + index, new DateTime(2026, 10, 21), 3, 5, "Draft", "", new DateTime(2026, 10, 21),
            "AQIDBAUGBwg=", Array.Empty<MyDmsVn.Contracts.GoodsReceiptLineDto>(), "W3", true, "E5", true)).ToArray();
        control.SearchBox.Text = "retained query";
        control.LastOperation.GetAwaiter().GetResult();
        control.StatusFilter.SelectedItem = "Draft";
        PumpMessagesUntil(() => control.LastOperation.IsCompleted, TimeSpan.FromSeconds(2));
        Assert.True(control.WarehouseFilterLookup.SelectValue(3));
        PumpMessagesUntil(() => control.LastOperation.IsCompleted, TimeSpan.FromSeconds(2));
        control.NextPageButton.PerformClick();
        PumpMessagesUntil(() => control.LastOperation.IsCompleted, TimeSpan.FromSeconds(2));
        control.ReceiptGrid.Selection.Focus(new SourceGrid.Position(21, 2), true);
        control.ReceiptGrid.ShowCell(new SourceGrid.Position(21, 2), true);
        var scroll = control.ReceiptGrid.CustomScrollPosition;
        Assert.True(scroll.Y > 0);
        var cell = control.ReceiptGrid[1, 1];
        var listCalls = fixture.Api.ListCalls;
        var getCalls = fixture.Api.GetCalls;
        PresentationButton(control, "Open current receipt").PerformClick();
        fixture.List.PageNumber = 1;
        fixture.List.PageNumber = 2;
        Assert.True(ReturnToList(control));
        Assert.Same(cell, control.ReceiptGrid[1, 1]);
        Assert.Equal(new SourceGrid.Position(21, 2), control.ReceiptGrid.Selection.ActivePosition);
        Assert.Equal(scroll, control.ReceiptGrid.CustomScrollPosition);
        Assert.Equal(3, control.WarehouseFilterLookup.SelectedValue);
        Assert.Equal("Draft", control.StatusFilter.SelectedItem);
        Assert.Equal(2, fixture.List.PageNumber);
        Assert.Equal("retained query", control.SearchBox.Text);
        Assert.Equal(listCalls, fixture.Api.ListCalls);
        Assert.Equal(getCalls, fixture.Api.GetCalls);
    });

    [Fact]
    public void Cached_accessibility_cell_cannot_mutate_a_hidden_detail() => Integrated((fixture, control) =>
    {
        control.LineGrid.Selection.Focus(new SourceGrid.Position(1, 2), true);
        var cell = control.LineGrid.AccessibilityObject.GetChild(1)!.GetChild(2)!;
        Assert.True(ReturnToList(control));
        Assert.True((cell.State & AccessibleStates.ReadOnly) != 0);
        Assert.Throws<InvalidOperationException>(() => cell.Value = "9");
        Assert.Equal(2.5m, fixture.Editor.Lines[0].Quantity);
        Assert.False(fixture.Editor.IsDirty);
        control.OpenCurrentReceiptButton.PerformClick();
        cell.Value = "9";
        Assert.Equal(9m, fixture.Editor.Lines[0].Quantity);
    });

    [Fact]
    public void Saved_state_distinguishes_pending_numeric_data_from_lookup_query() => Integrated((fixture, control) =>
    {
        BeginInline(control, 1).Text = "query only";
        Assert.Contains(PresentationDescendants(control), c => c is Label && c.Text == "Saved");
        GoodsReceiptInlineEditorTests.Key(Keys.Escape);
        GoodsReceiptInlineEditorTests.Key(Keys.Escape);
        BeginInline(control, 2).Text = "8.00001";
        Assert.Contains(PresentationDescendants(control), c => c is Label && c.Text == "Unsaved changes");
        GoodsReceiptInlineEditorTests.Key(Keys.Escape);
        Assert.Contains(PresentationDescendants(control), c => c is Label && c.Text == "Saved");
        Assert.False(fixture.Editor.IsDirty);
    });

    private static string PresentationMode(GoodsReceiptControl control) => control.WorkspaceMode.ToString();
    private static bool ReturnToList(GoodsReceiptControl control) => control.TryShowReceiptList();
    private static Button PresentationButton(Control root, string name) => Assert.Single(PresentationDescendants(root).OfType<Button>(), c => c.Text == name);
    private static System.Collections.Generic.IEnumerable<Control> PresentationDescendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(PresentationDescendants(c)));
}
