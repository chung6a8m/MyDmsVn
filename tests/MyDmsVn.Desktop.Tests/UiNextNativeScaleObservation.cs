using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

internal static class UiNextNativeScaleObservation
{
    internal static string Observe(Form form, CancellationToken token)
    {
        using var probe = new GuiProcessProbeFixture(package: false, inline: true);
        // The probe's generic monitor branch exits before its receipt-cell checks.
        var start = probe.Start(form.Handle.ToInt64() + " measure-sales " + Process.GetCurrentProcess().Id);
        var expected = Environment.GetEnvironmentVariable("MYDMSVN_UIN_EXPECTED_SCALE");
        if (expected != null)
        {
            Assert.True(expected == "100" || expected == "125", "Declare UI-NEXT native scale as 100 or 125.");
            start.EnvironmentVariables["MYDMSVN_SAL_EXPECTED_SCALE"] = expected;
        }
        var execution = GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(15), token);
        while (!execution.IsCompleted) { System.Windows.Forms.Application.DoEvents(); Thread.Yield(); }
        var result = execution.GetAwaiter().GetResult();
        Assert.True(result.CleanupVerified);
        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        Assert.Contains("OBSERVER_CONTEXT=PerMonitorV2", result.StandardOutput);
        if (expected != null)
        {
            Assert.Contains("MONITOR_SCALE=" + expected + ";", result.StandardOutput);
            Assert.Contains("SAL_NATIVE_ENVIRONMENT=PASS", result.StandardOutput);
        }
        return result.StandardOutput;
    }
}
