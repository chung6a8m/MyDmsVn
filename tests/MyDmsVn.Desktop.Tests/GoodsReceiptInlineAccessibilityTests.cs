using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Desktop.WinForms;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests;

public sealed class GoodsReceiptInlineAccessibilityTests
{
    private readonly ITestOutputHelper _output;
    public GoodsReceiptInlineAccessibilityTests(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void Accessible_committed_caption_tracks_current_options_without_resolving_pending_text() => StaTest.Run(_ =>
    {
        using var guard = new WinFormsTestGuard();
        using var host = new GoodsReceiptInlineEditorTests.SessionHost();
        var product = Cell(host.Grid.AccessibilityObject, "Product row 1");
        host.Begin(1, 1);
        host.Adapter.ProductControl.Text = "pending query";
        host.ProductSource.ProductName = "Renamed";
        host.Editor.ProductOptions.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.Contains("Selected: P7 - Renamed", product.Help);
        Assert.Contains("Pending selection: pending query", product.Help);
        Assert.Equal("pending query", host.Adapter.ProductControl.Text);
        Assert.Equal(7, host.Editor.Lines[0].ProductId);
        Assert.Empty(guard.Exceptions);
    }, TimeSpan.FromSeconds(15));

    [Fact]
    public void Native_value_actions_preserve_invalid_raw_and_reused_readonly_state()
    {
        using var probe = new GuiProcessProbeFixture(package: false, inline: true);
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var host = new GoodsReceiptInlineEditorTests.SessionHost();
            foreach (var mode in new[] { "validate", "readonly" })
            {
                if (mode == "readonly") host.Editor.Load(GoodsReceiptInlineEditorTests.SessionHost.Receipt(status: "Posted"));
                var execution = GuiOwnedProcess.RunAsync(probe.Start(host.FormHandle.ToInt64() + " " + mode), TimeSpan.FromSeconds(25), token);
                while (!execution.IsCompleted) { System.Windows.Forms.Application.DoEvents(); Thread.Yield(); }
                var result = execution.GetAwaiter().GetResult();
                _output.WriteLine(result.StandardOutput);
                Assert.True(result.CleanupVerified);
                Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
                Assert.Contains(mode == "validate" ? "NATIVE_VALUE_GUARDS=PASS" : "NATIVE_READONLY_GUARD=PASS", result.StandardOutput);
                Assert.Equal(mode == "validate" ? 7.25m : 2.5m, host.Editor.Lines[0].Quantity);
                Assert.Equal(7, host.Editor.Lines[0].ProductId);
            }
            Assert.False(host.Adapter.QuantityControl.Enabled);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(65));
    }

    [Fact]
    public void Accessible_writes_marshal_to_the_ui_thread_and_recheck_identity() => StaTest.Run(_ =>
    {
        using var guard = new WinFormsTestGuard();
        using var host = new GoodsReceiptInlineEditorTests.SessionHost();
        var previous = Control.CheckForIllegalCrossThreadCalls;
        Control.CheckForIllegalCrossThreadCalls = true;
        try
        {
            var cell = Cell(host.Grid.AccessibilityObject, "Quantity row 1");
            var write = Task.Run(() => cell.Value = "5.125");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!write.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(3))
            { System.Windows.Forms.Application.DoEvents(); Thread.Yield(); }
            Assert.True(write.IsCompleted);
            write.GetAwaiter().GetResult();
            Assert.Equal(5.125m, host.Editor.Lines[0].Quantity);
        }
        finally { Control.CheckForIllegalCrossThreadCalls = previous; }
        Assert.Empty(guard.Exceptions);
    }, TimeSpan.FromSeconds(15));

    [Fact]
    public void Accessible_line_cells_expose_correct_editability_and_identity() => StaTest.Run(_ =>
    {
        using var guard = new WinFormsTestGuard();
        using var host = new GoodsReceiptInlineEditorTests.SessionHost();
        var root = host.Grid.AccessibilityObject;
        Assert.Equal(AccessibleRole.Table, root.Role);
        var quantity = Cell(root, "Quantity row 1");
        Assert.Equal(AccessibleRole.Cell, quantity.Role);
        Assert.False((quantity.State & AccessibleStates.ReadOnly) != 0);
        quantity.Value = "3.125";
        Assert.Equal(3.125m, host.Editor.Lines[0].Quantity);
        var total = Cell(root, "Total row 1");
        Assert.True((total.State & AccessibleStates.ReadOnly) != 0);
        Assert.Throws<InvalidOperationException>(() => total.Value = "42");
        host.Editor.RemoveLine(host.Editor.Lines[0]);
        Assert.True((quantity.State & AccessibleStates.Unavailable) != 0);
        Assert.Throws<InvalidOperationException>(() => quantity.Value = "99");
        Assert.Equal(2m, host.Editor.Lines[0].Quantity);
        host.Editor.Load(GoodsReceiptInlineEditorTests.SessionHost.Receipt());
        Assert.Throws<InvalidOperationException>(() => quantity.Value = "99");
        var current = Cell(root, "Quantity row 1");
        host.Editor.Load(GoodsReceiptInlineEditorTests.SessionHost.Receipt(status: "Posted"));
        Assert.Throws<InvalidOperationException>(() => current.Value = "99");
        var posted = Cell(root, "Quantity row 1");
        Assert.True((posted.State & AccessibleStates.ReadOnly) != 0);
        Assert.Throws<InvalidOperationException>(() => posted.Value = "99");
        host.Editor.Load(GoodsReceiptInlineEditorTests.SessionHost.Receipt());
        host.CanEdit = false;
        host.Adapter.RefreshEditability();
        Assert.Throws<InvalidOperationException>(() => Cell(root, "Quantity row 1").Value = "99");
        Assert.Equal(2.5m, host.Editor.Lines[0].Quantity);
        Assert.Empty(guard.Exceptions);
    }, TimeSpan.FromSeconds(15));

    [Fact]
    public void Accessible_edits_preserve_validation_and_pending_selection_state() => StaTest.Run(_ =>
    {
        using var guard = new WinFormsTestGuard();
        using var host = new GoodsReceiptInlineEditorTests.SessionHost();
        var quantity = Cell(host.Grid.AccessibilityObject, "Quantity row 1");
        quantity.Value = "7.25000";
        Assert.Equal(7.25m, host.Editor.Lines[0].Quantity);
        Assert.Throws<ArgumentException>(() => quantity.Value = "8.00001");
        Assert.Equal(7.25m, host.Editor.Lines[0].Quantity);
        Assert.Equal("8.00001", host.Adapter.QuantityControl.Text);
        Assert.Contains("decimal places", quantity.Help);
        host.Adapter.CancelActiveEdit();
        var product = Cell(host.Grid.AccessibilityObject, "Product row 1");
        product.Value = "8";
        Assert.Equal(8, host.Editor.Lines[0].ProductId);
        host.Begin(1, 1);
        host.Adapter.ProductControl.Text = "pending query";
        Assert.Contains("Pending selection", product.Help);
        Assert.Contains("8", product.Help);
        Assert.Equal(8, host.Editor.Lines[0].ProductId);
        host.Adapter.CancelActiveEdit();
        Assert.DoesNotContain("Pending selection", product.Help);
        var old = product;
        host.Editor.Load(null);
        var line = host.Editor.AddLine();
        Assert.Throws<InvalidOperationException>(() => old.Value = "7");
        Assert.Equal(0, line.ProductId);
        Assert.Empty(guard.Exceptions);
    }, TimeSpan.FromSeconds(15));

    [Fact]
    public void Native_uia_reaches_the_active_shared_editor()
    {
        using var probe = new GuiProcessProbeFixture(package: false, inline: true);
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var host = new GoodsReceiptInlineEditorTests.SessionHost();
            foreach (var name in new[] { "Quantity row 1", "Quantity row 2", "Unit cost row 1", "Product row 1" })
            {
                var execution = GuiOwnedProcess.RunAsync(probe.Start(host.FormHandle.ToInt64() + " \"cell:" + name + "\""), TimeSpan.FromSeconds(20), token);
                while (!execution.IsCompleted) { System.Windows.Forms.Application.DoEvents(); Thread.Yield(); }
                var result = execution.GetAwaiter().GetResult();
                _output.WriteLine(result.StandardOutput);
                Assert.True(result.CleanupVerified);
                Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
                Assert.Contains("NATIVE_CELL_PIPELINE=PASS", result.StandardOutput);
                if (name == "Quantity row 1") Assert.Equal(6.25m, host.Editor.Lines[0].Quantity);
                if (name == "Quantity row 2") Assert.Equal(6.25m, host.Editor.Lines[1].Quantity);
                if (name == "Unit cost row 1") Assert.Equal(6.25m, host.Editor.Lines[0].UnitCost);
                if (name == "Product row 1") Assert.Equal(8, host.Editor.Lines[0].ProductId);
            }
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(110));
    }

    private static AccessibleObject Cell(AccessibleObject root, string name) => Descendants(root).Single(child => child.Name == name);
    private static IEnumerable<AccessibleObject> Descendants(AccessibleObject root)
    {
        for (var index = 0; index < root.GetChildCount(); index++)
        {
            var child = root.GetChild(index);
            if (child == null) continue;
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
