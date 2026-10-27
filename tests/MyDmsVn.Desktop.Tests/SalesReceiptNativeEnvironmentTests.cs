using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using MyDmsVn.Desktop.WinForms;
using MyDmsVn.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests;

public sealed class SalesReceiptNativeEnvironmentTests
{
    private readonly ITestOutputHelper _output;
    public SalesReceiptNativeEnvironmentTests(ITestOutputHelper output) { _output = output; }
    [Fact]
    public void Independent_observer_records_actual_monitor_window_bounds_and_process_ownership()
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("WarehouseLookup", "Light", token, _output.WriteLine);
            using var probe = new GuiProcessProbeFixture(package: false, inline: true);
            _output.WriteLine(Observe(host, probe, token));
        });
    }
    [Theory]
    [InlineData(1680, 900, "Light")]
    [InlineData(1680, 900, "Dark")]
    [InlineData(1200, 760, "Light")]
    [InlineData(1200, 760, "Dark")]
    [InlineData(944, 600, "Light")]
    [InlineData(944, 600, "Dark")]
    public void Native_compact_matrix_requires_actual_bounds_reachable_commands_errors_and_popup(int width, int height, string theme)
    {
        SalesNativeHost.Run((token, guard) =>
        {
            using var host = new SalesNativeHost("WarehouseLookup", theme, token, _output.WriteLine);
            host.ReleaseLookupHolds();
            host.Api.Current = new SalesReceiptDto(41, "SA0000000041", new DateTime(2026, 10, 22), 1, 2, 3, "Draft", "Same product, independent gift and ordinary rows", null, "AQIDBAUGBwg=",
                new[] { new SalesReceiptLineDto(91, 1, 7, "box", 2m, 4m, false, 8m, null, null, "G7 - Guide", true),
                    new SalesReceiptLineDto(92, 2, 7, "box", 1m, 12.5m, true, 0m, null, null, "G7 - Guide", true),
                    new SalesReceiptLineDto(93, 3, 8, "ream", 3m, 2.125m, false, 6.375m, null, null, "G8 - Other", true) }, 14.375m, null, null, "C1 - Customer", true, "W2 - Warehouse", true, "E3 - Employee", true);
            // The synthetic source changed: admit an explicit List query before comparing its page with Detail.
            Assert.True(host.Control.TryShowReceiptList());
            host.Wait(host.Control.ActivateAsync(token)); host.Wait(host.Control.SelectReceiptAsync(41, token));
            host.Form.TopMost = true; host.Form.ClientSize = new Size(width, height);
            host.Form.Location = new Point(0, 0); host.Pump(); NativeLookupTypingDriver.Activate(host.Form);
            using var probe = new GuiProcessProbeFixture(package: false, inline: true);
            _output.WriteLine("SAL_REQUESTED_LOGICAL_CLIENT=" + width + "x" + height + ";ACTUAL_LOGICAL_CLIENT=" + host.Form.ClientSize + ";THEME=" + theme + ";TEST_PROCESS_BITS=" + IntPtr.Size * 8);
            _output.WriteLine(Observe(host, probe, token));
            Assert.True(host.Form.ClientSize == new Size(width, height), "Requested logical client is unavailable in this physical environment; matrix row NOT RUN/BLOCKED.");
            var directory = Environment.GetEnvironmentVariable("MYDMSVN_SAL_CAPTURE_DIRECTORY");
            Assert.False(string.IsNullOrWhiteSpace(directory), "Declare a local ignored SAL capture directory for the native matrix.");
            Directory.CreateDirectory(directory!);
            var prefix = Path.Combine(directory!, "sales-" + width + "x" + height + "-" + theme);
            Capture(host, probe, token, prefix + "-detail.png");
            Assert.True(host.Control.TryShowReceiptList());
            Assert.Equal(3, Convert.ToInt32(host.Control.ReceiptGrid[1, 6].Value));
            Assert.Equal(14.375m, Convert.ToDecimal(host.Control.ReceiptGrid[1, 7].Value));
            Capture(host, probe, token, prefix + "-list.png");
            Assert.True(host.Control.TryShowReceiptDetail());
            var position = new SourceGrid.Position(1, 3); host.Control.LineGrid.Focus(); host.Control.LineGrid.Selection.Focus(position, true);
            new SourceGrid.CellContext(host.Control.LineGrid, position).StartEdit();
            host.Control.QuantityEditor.Text = "1.00001"; host.Wait(host.Control.SaveAsync());
            Assert.Equal(2m, host.Control.Editor.Lines[0].Quantity); Assert.Equal("1.00001", host.Control.QuantityEditor.Text);
            Capture(host, probe, token, prefix + "-error-focus.png");
            GoodsReceiptInlineEditorTests.Key(System.Windows.Forms.Keys.Escape); host.Source.Hold = false;
            var customer = host.Control.CustomerLookup; var driver = new NativeLookupTypingDriver(customer);
            driver.SelectRange(0, driver.Text.Length); driver.TypeCharacter('G'); driver.Key(System.Windows.Forms.Keys.F4);
            Assert.True(customer.IsDropDownOpen); guard.Attach(customer.ResultsGrid);
            Capture(host, probe, token, prefix + "-popup.png");
            driver.Key(System.Windows.Forms.Keys.Escape);
            foreach (var button in new[] { host.Control.BackToReceiptsButton, host.Control.NewButton, host.Control.SaveButton, host.Control.PostButton, host.Control.ReloadButton })
            { Assert.True(button.Visible); Assert.True(host.Form.ClientRectangle.Contains(host.Form.RectangleToClient(button.RectangleToScreen(button.ClientRectangle))), button.Text + " must be reachable within the actual client."); }
            _output.WriteLine("SAL_NATIVE_COMPACT_CONTEXT=PASS;theme=" + theme + ";logicalClient=" + width + "x" + height + ";scale=" + Environment.GetEnvironmentVariable("MYDMSVN_SAL_EXPECTED_SCALE") + ";sourceSynthetic=True;packageAndAttended=False");
        });
    }
    private string Observe(SalesNativeHost host, GuiProcessProbeFixture probe, CancellationToken token)
    {
        var start = probe.Start(host.Form.Handle.ToInt64() + " measure-sales " + Process.GetCurrentProcess().Id);
        var expected = Environment.GetEnvironmentVariable("MYDMSVN_SAL_EXPECTED_SCALE"); if (expected != null) start.EnvironmentVariables["MYDMSVN_SAL_EXPECTED_SCALE"] = expected;
        var task = GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(15), token); host.Wait(task); var result = task.GetAwaiter().GetResult();
        Assert.True(result.CleanupVerified); Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError); return result.StandardOutput;
    }
    private void Capture(SalesNativeHost host, GuiProcessProbeFixture probe, CancellationToken token, string path)
    {
        var start = probe.Start(host.Form.Handle.ToInt64() + " capture-sales " + Process.GetCurrentProcess().Id + " \"" + path + "\"");
        var expected = Environment.GetEnvironmentVariable("MYDMSVN_SAL_EXPECTED_SCALE"); if (expected != null) start.EnvironmentVariables["MYDMSVN_SAL_EXPECTED_SCALE"] = expected;
        var task = GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(15), token); host.Wait(task); var result = task.GetAwaiter().GetResult();
        _output.WriteLine(result.StandardOutput); Assert.True(result.CleanupVerified); Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        Assert.True(File.Exists(path));
    }
}
