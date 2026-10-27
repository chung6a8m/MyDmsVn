using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests;

public sealed class GoodsReceiptLookupTypingTests : IClassFixture<LookupGateEnvironment>
{
    private readonly ITestOutputHelper _output;
    private readonly LookupGateEnvironment _gate;
    public GoodsReceiptLookupTypingTests(ITestOutputHelper output, LookupGateEnvironment gate) { _output = output; _gate = gate; }

    [Theory]
    [InlineData("WarehouseFilterLookup")]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void First_character_survives_result_and_operation_completion(string target)
    {
        Run(target, (fixture, failures) =>
        {
            var first = fixture.Type('g');
            Observe(fixture, failures, "first key", "g", 1);
            fixture.CompleteResponse(first, 0);
            Observe(fixture, failures, "first response and operation completion", "g", 1);
            fixture.Type('u');
            Observe(fixture, failures, "next key after response", "gu", 2);
        });
    }

    [Theory]
    [InlineData("WarehouseFilterLookup")]
    [InlineData("WarehouseLookup")]
    [InlineData("EmployeeLookup")]
    public void Cancelled_preceding_search_preserves_per_key_order(string target)
    {
        Run(target, (fixture, failures) =>
        {
            var preceding = fixture.Type('g');
            Observe(fixture, failures, "g pending", "g", 1);
            var latest = fixture.Type('u');
            Observe(fixture, failures, "u pending", "gu", 2);
            Assert.Equal(2, fixture.DelayCount);
            Assert.True(fixture.DelayWasCancelled(0));
            Assert.False(preceding.IsCompleted);
            Assert.False(latest.IsCompleted);
            fixture.CompleteDelay(0);
            fixture.Wait(preceding);
            Assert.False(latest.IsCompleted);
            Assert.Equal(0, fixture.Source.Count); // No latest response can explain this rebind.
            Observe(fixture, failures, "cancelled preceding completion; latest held", "gu", 2);
            fixture.Type('i');
            Observe(fixture, failures, "i after cancelled completion", "gui", 3);
        });
    }

    [Theory]
    [InlineData("WarehouseFilterLookup", "insert")]
    [InlineData("WarehouseLookup", "insert")]
    [InlineData("EmployeeLookup", "insert")]
    [InlineData("WarehouseFilterLookup", "range")]
    [InlineData("WarehouseLookup", "range")]
    [InlineData("EmployeeLookup", "range")]
    [InlineData("WarehouseFilterLookup", "replace")]
    [InlineData("WarehouseLookup", "replace")]
    [InlineData("EmployeeLookup", "replace")]
    public void Rebinding_preserves_mid_string_caret_and_selected_range(string target, string edit)
    {
        Run(target, (fixture, failures) =>
        {
            fixture.Type('g');
            Observe(fixture, failures, "g", "g", 1);
            fixture.Type('u');
            Observe(fixture, failures, "u", "gu", 2);
            var operation = fixture.Type('i');
            Observe(fixture, failures, "i", "gui", 3);
            fixture.Driver.SelectRange(1, edit == "insert" ? 0 : 1);
            Observe(fixture, failures, "selected native range", "gui", 1, edit == "insert" ? 0 : 1);
            var text = "gui";
            var caret = 1;
            var length = 1;
            var delay = 2;
            if (edit != "range")
            {
                operation = fixture.Type('a');
                text = edit == "insert" ? "gaui" : "gai";
                caret = 2;
                length = 0;
                delay = 3;
                Observe(fixture, failures, "native " + edit, text, caret);
            }
            fixture.CompleteResponse(operation, delay);
            Observe(fixture, failures, "response rebind after " + edit, text, caret, length);
        });
    }

    private void Run(string target, Action<GoodsReceiptLookupFixture, List<string>> scenario)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            // DoEvents alone enters/exits a temporary loop and can uninstall WinForms'
            // context. Keep a real outer loop while exercising asynchronous control work.
            ExceptionDispatchInfo? failure = null;
            using var loop = new ApplicationContext();
            using var startup = new Control();
            startup.CreateControl();
            startup.BeginInvoke(new Action(() =>
            {
                try
                {
                    var failures = new List<string>();
                    using (var fixture = new GoodsReceiptLookupFixture(target, token))
                    {
                        Assert.Equal(4, IntPtr.Size);
                        _output.WriteLine(_gate.Observe(fixture));
                        guard.Attach(fixture.Control.WarehouseFilterLookup.ResultsGrid);
                        guard.Attach(fixture.Control.WarehouseLookup.ResultsGrid);
                        guard.Attach(fixture.Control.EmployeeLookup.ResultsGrid);
                        _output.WriteLine("Setup context before explicit installation=" + fixture.ContextBeforeInstallation +
                            "; owned WindowsFormsSynchronizationContext installed; STA/collection mutation ownership asserted at every wait");
                        _output.WriteLine("Target=" + target + "; DPI=" + fixture.Form.DeviceDpi +
                            "; client=" + fixture.Form.ClientSize + "; workArea=" + System.Windows.Forms.Screen.FromControl(fixture.Form).WorkingArea +
                            "; theme=" + MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme.Mode);
                        _output.WriteLine("Initial raw='" + fixture.InitialText + "'; filter=null; warehouse=3; employee=5; pending=False; native EDIT=" +
                            fixture.Driver.FocusedHandle + "; ownedProcessAndThread=True; initial selected range=" + fixture.Driver.SelectionStart + "+" + fixture.Driver.SelectionLength);
                        scenario(fixture, failures);
                    }
                    _output.WriteLine("CLEANUP=drained; form/control/viewmodels disposed; guard exceptions=" + guard.Exceptions.Count);
                    Assert.Empty(guard.Exceptions);
                    // Keep observing later keys even on a broken baseline; every checkpoint retains
                    // independent literal expectations. Native focus/fixture faults fail immediately.
                    Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
                }
                catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
                finally { loop.ExitThread(); }
            }));
            System.Windows.Forms.Application.Run(loop);
            failure?.Throw();
        }, TimeSpan.FromSeconds(30));
    }

    private void Observe(GoodsReceiptLookupFixture fixture, List<string> failures, string step, string expectedText, int expectedStart, int expectedLength = 0)
    {
        var driver = fixture.Driver;
        driver.AssertFocused();
        var text = driver.Text;
        var start = driver.SelectionStart;
        var length = driver.SelectionLength;
        var selected = fixture.Lookup.SelectedValue;
        var expectedId = fixture.Target == "WarehouseFilterLookup" ? (int?)null : fixture.Target == "WarehouseLookup" ? 3 : 5;
        var highlighted = (fixture.Lookup.HighlightedItem as CatalogLookupOption)?.Id;
        _output.WriteLine(step + ": text='" + text + "'; range=" + start + "+" + length +
            "; pending=" + fixture.Lookup.HasPendingText + "; selected=" + (selected ?? "null") +
            "; highlighted=" + highlighted + "; popup=" + fixture.Lookup.IsDropDownOpen + "; focusedOwnedEdit=True");
        _output.WriteLine("Model: filter=" + (fixture.Control.WarehouseFilterLookup.SelectedValue ?? "null") +
            "; warehouse=" + fixture.Editor.WarehouseId + "; employee=" + fixture.Editor.EmployeeId + "; dirty=" + fixture.Editor.IsDirty);
        // Execute exact assertions at every checkpoint, retaining all product differences.
        Check(() => Assert.Equal(expectedText, text));
        Check(() => Assert.Equal(expectedStart, start));
        Check(() => Assert.Equal(expectedLength, length));
        Check(() => Assert.True(fixture.Lookup.HasPendingText));
        Check(() => Assert.Equal(expectedId, selected as int?));
        Check(() => Assert.Equal(3, fixture.Editor.WarehouseId));
        Check(() => Assert.Equal(5, fixture.Editor.EmployeeId));
        Check(() => Assert.False(fixture.Editor.IsDirty));
        Check(() => Assert.Null(fixture.Control.WarehouseFilterLookup.SelectedValue));
        Check(() => Assert.All(fixture.Api.Requests, request => Assert.Null(request.WarehouseId)));
        void Check(Action assertion)
        {
            try { assertion(); }
            catch (Xunit.Sdk.XunitException exception) { failures.Add(step + ": " + exception.Message); }
        }
    }
}
