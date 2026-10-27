using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using Xunit;

namespace MyDmsVn.Desktop.Ui.Tests;

public sealed class UiLabPermissionRefreshLifetimeTests
{
    [Fact]
    public void Reset_drains_held_permission_refresh_before_replacing_sales_graph()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            using var host = new SalesLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            var data = host.CurrentScenarioData!;
            var control = host.CurrentSalesControl!;
            var gate = new ScenarioGate<bool> { IgnoreCancellation = true, IgnoreOwnerRelease = true };
            data.Permissions.NextCheckGate = gate;
            host.ApplyAccessButton.PerformClick();
            Until(() => data.Permissions.OutstandingChecks.Any(task => !task.IsCompleted));
            var reset = host.ResetForTestAsync();
            PumpFor(300);
            try
            {
                Assert.False(reset.IsCompleted);
                Assert.Same(control, host.CurrentSalesControl);
                Assert.False(control.IsDisposed);
                gate.Complete(true);
                Until(() => reset.IsCompleted);
                reset.GetAwaiter().GetResult();
                Assert.True(control.IsDisposed);
                Assert.NotNull(host.CurrentScenarioData);
                Assert.NotSame(data, host.CurrentScenarioData);
            }
            finally
            {
                gate.Complete(true);
                Until(() => reset.IsCompleted && data.Permissions.OutstandingChecks.All(task => task.IsCompleted));
                PumpFor(100);
                host.CloseWorkspaceButton.PerformClick();
                Until(() => host.CurrentSalesControl == null);
            }
        });

    [Fact]
    public void Old_permission_refresh_cannot_overwrite_status_after_session_replacement()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            using var host = new SalesLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            var data = host.CurrentScenarioData!;
            var gate = new ScenarioGate<bool> { IgnoreCancellation = true, IgnoreOwnerRelease = true };
            data.Permissions.NextCheckGate = gate;
            host.ApplyAccessButton.PerformClick();
            Until(() => data.Permissions.OutstandingChecks.Any(task => !task.IsCompleted));
            var replacement = AllControls(host).OfType<Button>().Single(button => button.Text == "Replace session");
            replacement.PerformClick();
            var statusAfterReplacement = host.StatusText;
            try
            {
                gate.Complete(true);
                Until(() => data.Permissions.OutstandingChecks.All(task => task.IsCompleted));
                PumpFor(300);
                Assert.Equal(statusAfterReplacement, host.StatusText);
            }
            finally
            {
                gate.Complete(true);
                host.CloseWorkspaceButton.PerformClick();
                Until(() => host.CurrentSalesControl == null);
            }
        });

    [Fact]
    public void Older_permission_refresh_cannot_overwrite_newer_result_in_same_graph()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            using var host = new SalesLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            var data = host.CurrentScenarioData!;
            var gate = new ScenarioGate<bool> { IgnoreCancellation = true, IgnoreOwnerRelease = true };
            data.Permissions.NextCheckGate = gate;
            host.PermissionVariantSelector.SelectedIndex = 0;
            host.ApplyAccessButton.PerformClick();
            Until(() => data.Permissions.OutstandingChecks.Any(task => !task.IsCompleted));
            try
            {
                host.PermissionVariantSelector.SelectedIndex = 2;
                host.ApplyAccessButton.PerformClick();
                Until(() => host.StatusText == "Deny Write applied.");
                gate.Complete(true);
                Until(() => data.Permissions.OutstandingChecks.All(task => task.IsCompleted));
                PumpFor(300);
                Assert.Equal("Deny Write applied.", host.StatusText);
            }
            finally
            {
                gate.Complete(true);
                host.CloseWorkspaceButton.PerformClick();
                Until(() => host.CurrentSalesControl == null);
            }
        });

    private static System.Collections.Generic.IEnumerable<Control> AllControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in AllControls(child)) yield return descendant;
        }
    }
    private static void PumpFor(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
    }
    private static void Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(8)) throw new TimeoutException("UI Lab did not settle.");
            System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10);
        }
    }
}
