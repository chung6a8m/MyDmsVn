using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests;

// Opt-in observation only. These timings are never correctness assertions.
public sealed class ReceiptEditingPerformanceTests
{
    private readonly ITestOutputHelper _output;
    public ReceiptEditingPerformanceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Measure_receipt_edit_commit_and_projection()
    {
        if (Environment.GetEnvironmentVariable("MYDMSVN_UIN_T12_PERF") != "1") return;
        var machine = Environment.GetEnvironmentVariable("MYDMSVN_UIN_T12_MACHINE");
        Assert.False(string.IsNullOrWhiteSpace(machine), "Set MYDMSVN_UIN_T12_MACHINE to identify the reference machine.");
        _output.WriteLine("machine=" + machine + "; OS=" + Environment.OSVersion + "; process=" + (IntPtr.Size * 8) + " bit; repetitions=12; first 2 warmup");
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            foreach (var count in new[] { 1, 50, 200 })
            {
                using var host = new SalesGridHost();
                while (host.Editor.Lines.Count < count) host.Editor.AddLine();
                var line = host.Editor.Lines[count / 2];
                var adapter = (MyDmsVn.Desktop.WinForms.SalesReceiptLinesGridAdapter)host.Adapter;
                var refresh = adapter.GetType().GetMethod("RefreshProjection", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var events = 0; var invalidations = 0;
                host.Editor.PropertyChanged += (_, __) => events++;
                host.Grid.Invalidated += (_, __) => invalidations++;
                var edits = new List<double>(); var editBytes = new List<long>();
                var projections = new List<double>(); var projectionBytes = new List<long>();
                for (var repetition = 0; repetition < 14; ++repetition)
                {
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    var watch = Stopwatch.StartNew();
                    host.Set(line, 3, repetition % 2 == 0 ? "2.5" : "3.5");
                    watch.Stop();
                    if (repetition >= 2) { edits.Add(watch.Elapsed.TotalMilliseconds); editBytes.Add(GC.GetAllocatedBytesForCurrentThread() - before); }
                    before = GC.GetAllocatedBytesForCurrentThread(); watch.Restart();
                    refresh.Invoke(adapter, null);
                    watch.Stop();
                    if (repetition >= 2) { projections.Add(watch.Elapsed.TotalMilliseconds); projectionBytes.Add(GC.GetAllocatedBytesForCurrentThread() - before); }
                }
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                _output.WriteLine("Sales lines=" + count + "; edit/commit=" + Distribution(edits) + "; bytes=" + Distribution(editBytes.Select(v => (double)v).ToList()) +
                    "; projection=" + Distribution(projections) + "; projectionBytes=" + Distribution(projectionBytes.Select(v => (double)v).ToList()) +
                    "; editorEvents=" + events + "; gridInvalidations=" + invalidations + "; retainedControls=" + CountControls(host.Form));
            }
            _output.WriteLine(GoodsReceiptControlTests.MeasureGoodsEditingPerformance());
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromMinutes(4));
    }

    private static string Distribution(IReadOnlyList<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        return "p50=" + ordered[(int)Math.Ceiling(ordered.Length * .50) - 1].ToString("F2") +
            ",p95=" + ordered[(int)Math.Ceiling(ordered.Length * .95) - 1].ToString("F2");
    }
    private static int CountControls(Control root) => 1 + root.Controls.Cast<Control>().Sum(CountControls);
}
