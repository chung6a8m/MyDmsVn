using MyDmsVn.Desktop.WinForms;
using System;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests;

public sealed class InlineValidationPresentationTests
{
    private readonly ITestOutputHelper _output;
    public InlineValidationPresentationTests(ITestOutputHelper output) { _output = output; }
    [Fact]
    public void Addresses_use_reference_identity_and_expire_with_document()
    {
        var store = new InlineValidationStore();
        var a = new object(); var b = new object();
        InlineValidationAddress Address(long generation, object line) => new InlineValidationAddress(generation, line, "qty");
        store.Set(Address(1, a), new[] { new ValidationIssue("InvalidQuantity", "Enter a valid quantity.", "local") });
        Assert.Single(store.Get(Address(1, a)));
        Assert.Empty(store.Get(Address(1, b)));
        Assert.Empty(store.Get(Address(2, a)));
        store.Clear(Address(1, a));
        Assert.Empty(store.Get(Address(1, a)));
    }

    [Fact]
    public void Invalid_quantity_is_exposed_on_cell_and_summary_without_changing_raw_editor()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            host.Begin(3); host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapDecimalInput>("QuantityEditor").Text = "1.00001";
            host.CallTask("SaveAsync");
            var grid = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid");
            var help = grid.AccessibilityObject.GetChild(1)!.GetChild(3)!.Help;
            Assert.Contains("Validation error", help);
            Assert.Contains("Quantity", help, StringComparison.OrdinalIgnoreCase);
            var summary = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapButton>("ValidationSummaryButton");
            Assert.True(summary.Visible); Assert.Contains("Line 1 Quantity", summary.Text);
            var quantity = host.Value<BootstrapDecimalInput>("QuantityEditor");
            var errors = (ErrorProvider)host.Control.GetType().GetField("_errors", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host.Control)!;
            Assert.Equal(BootstrapValidationState.Invalid, quantity.ValidationState);
            Assert.Contains("Quantity", quantity.AccessibleDescription, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(errors.GetError(quantity));
            Assert.Equal("1.00001", quantity.Text);
            Assert.Equal(1m, host.Editor.Lines.Single().Quantity);
            Assert.True(new SourceGrid.CellContext(grid, new SourceGrid.Position(1, 3)).EndEdit(true));
            Assert.Equal(BootstrapValidationState.None, quantity.ValidationState);
            Assert.Empty(errors.GetError(quantity));
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Server_error_arriving_after_begin_updates_active_editor_and_clears_without_losing_buffer()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            host.Begin(4);
            var price = host.Value<BootstrapDecimalInput>("UnitPriceEditor");
            price.Text = "9.25";
            var inline = (SalesReceiptLinesGridAdapter)host.Control.GetType().GetField("_inline", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host.Control)!;
            var errors = (ErrorProvider)host.Control.GetType().GetField("_errors", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host.Control)!;
            var issues = new[] { new MyDmsVn.Desktop.Application.SalesReceiptValidationIssue(host.Editor.Lines.Single(), "unitPrice", "Sales.Price", "Price rejected by server.", "server") };
            typeof(MyDmsVn.Desktop.Application.SalesReceiptEditorViewModel).GetProperty("ValidationIssues")!.SetValue(host.Editor, issues);
            inline.SetServerIssues(issues);
            Assert.Equal(BootstrapValidationState.Invalid, price.ValidationState);
            Assert.Contains("Price rejected by server.", price.AccessibleDescription);
            Assert.Contains("Price rejected by server.", errors.GetError(price));
            Assert.Equal("9.25", price.Text);
            var cleared = Array.Empty<MyDmsVn.Desktop.Application.SalesReceiptValidationIssue>();
            typeof(MyDmsVn.Desktop.Application.SalesReceiptEditorViewModel).GetProperty("ValidationIssues")!.SetValue(host.Editor, cleared);
            inline.SetServerIssues(cleared);
            Assert.Equal(BootstrapValidationState.None, price.ValidationState);
            Assert.DoesNotContain("Price rejected by server.", price.AccessibleDescription);
            Assert.Empty(errors.GetError(price));
            Assert.Equal("9.25", price.Text);
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Server_indexed_error_follows_submitted_line_reference_after_reorder_and_summary_focus_is_explicit()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            var first = host.Editor.Lines.Single();
            var second = host.Editor.AddLine()!; second.SetProduct(7, "P7 - Product", true, "box");
            host.Api.UpdateError = new ApiError(ApiStatusCode.BadRequest, "Sales.Invalid", "Resolve line errors.",
                new[]
                {
                    new ApiErrorDetail("Sales.Qty", "Quantity rejected by server.", "Request.Lines[1].Qty"),
                    new ApiErrorDetail("Sales.Unknown", "Additional review required.", "legacy.path"),
                });
            host.CallTask("SaveAsync");
            var grid = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid");
            Assert.DoesNotContain("Quantity rejected", grid.AccessibilityObject.GetChild(1)!.GetChild(3)!.Help);
            Assert.Contains("Quantity rejected", grid.AccessibilityObject.GetChild(2)!.GetChild(3)!.Help);
            var focusBefore = grid.Selection.ActivePosition;
            Assert.True(host.Editor.MoveLine(second, 0));
            Assert.Equal(focusBefore.Column, grid.Selection.ActivePosition.Column);
            Assert.Contains("Quantity rejected", grid.AccessibilityObject.GetChild(1)!.GetChild(3)!.Help);
            Assert.DoesNotContain("Quantity rejected", grid.AccessibilityObject.GetChild(2)!.GetChild(3)!.Help);
            var summary = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapButton>("ValidationSummaryButton");
            Assert.Contains("Line 1 Quantity", summary.Text);
            Assert.Contains("legacy.path: Additional review required.", summary.Text);
            summary.PerformClick();
            Assert.Equal(3, grid.Selection.ActivePosition.Column);
            Assert.True(host.Editor.RemoveLine(first));
            Assert.Contains("Quantity rejected", grid.AccessibilityObject.GetChild(1)!.GetChild(3)!.Help);
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Server_row_error_marks_ordinal_and_accessible_row_without_claiming_a_field()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            host.Editor.Note = "changed";
            host.Api.UpdateError = new ApiError(ApiStatusCode.BadRequest, "Sales.Invalid", "Resolve line error.",
                new[] { new ApiErrorDetail("Sales.Line", "Line rejected by server.", "Request.Lines[0]") });
            host.CallTask("SaveAsync");
            var grid = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid");
            Assert.Contains("Line rejected", grid.AccessibilityObject.GetChild(1)!.Name);
            Assert.DoesNotContain("Line rejected", grid.AccessibilityObject.GetChild(1)!.GetChild(3)!.Help);
            var summary = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapButton>("ValidationSummaryButton");
            Assert.StartsWith("Line 1:", summary.Text);
        }, TimeSpan.FromSeconds(20));
    }

    [Theory]
    [InlineData(BootstrapThemeMode.Light)]
    [InlineData(BootstrapThemeMode.Dark)]
    public void Compact_sales_validation_indicator_and_keyboard_summary_work_in_both_themes(BootstrapThemeMode mode)
    {
        StaTest.Run(token =>
        {
            var previous = BootstrapThemeManager.CurrentTheme;
            try
            {
                BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
                host.Form.ClientSize = new System.Drawing.Size(944, 700); System.Windows.Forms.Application.DoEvents();
                host.Editor.Note = "changed";
                host.Api.UpdateError = new ApiError(ApiStatusCode.BadRequest, "Sales.Invalid", "Resolve line errors.",
                    new[] { new ApiErrorDetail("Sales.Price", "Unit price rejected.", "Request.Lines[0].UnitPrice") });
                host.CallTask("SaveAsync");
                var grid = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid");
                var summary = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapButton>("ValidationSummaryButton");
                Assert.True(summary.Visible); Assert.Contains("Line 1 Unit price", summary.Text);
                Assert.Contains("Unit price rejected", grid.AccessibilityObject.GetChild(1)!.GetChild(4)!.Help);
                Assert.NotEqual(((SourceGrid.Cells.Views.Cell)grid[1, 3].View).BackColor,
                    ((SourceGrid.Cells.Views.Cell)grid[1, 4].View).BackColor);
                Assert.False(host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapDecimalInput>("UnitPriceEditor").ContainsFocus);
                Assert.True(summary.CanSelect); summary.Select(); summary.Focus();
                NativeLookupTypingDriver.KeyCharacter(summary, Keys.Enter, '\r');
                Assert.Equal(4, grid.Selection.ActivePosition.Column);
                var last = grid.PositionToRectangle(new SourceGrid.Position(1, 6));
                Assert.True(last.Right <= grid.ClientSize.Width, "Last sales column must remain visible; last=" + last + "; grid=" + grid.ClientSize);
                _output.WriteLine($"Sales validation theme={mode}; DeviceDpi={host.Form.DeviceDpi}; client={host.Form.ClientSize}; grid={grid.ClientSize}; last={last}");
                _output.WriteLine(UiNextNativeScaleObservation.Observe(host.Form, token));
            }
            finally { BootstrapThemeManager.CurrentTheme = previous; }
        }, TimeSpan.FromSeconds(20));
    }
}
