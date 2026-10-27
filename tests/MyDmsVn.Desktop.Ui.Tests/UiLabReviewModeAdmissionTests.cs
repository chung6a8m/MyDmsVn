using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.Tests;
using Xunit;

namespace MyDmsVn.Desktop.Ui.Tests;

public sealed class UiLabReviewModeAdmissionTests
{
    [Fact]
    public void Review_mode_from_fit_lab_preserves_current_viewport_until_it_is_restored()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            using var host = new UiLabForm { ClientSize = new Size(1000, 700) };
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting && host.StatusText.Contains("ready"));
            host.ModuleSelector.SelectedIndex = 1;
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting && host.StatusText.Contains("SQL-free Security"));
            var workspace = host.CurrentSecurityControl!;
            var size = workspace.ClientSize;
            Assert.True(size.Width > 0 && size.Height > 0);
            var toggle = Assert.IsType<Button>(Assert.Single(host.Controls.Find("ReviewModeToggle", true)));
            toggle.PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Same(workspace, host.CurrentSecurityControl);
            Assert.Equal(size, workspace.ClientSize);
            NativeLookupTypingDriver.Activate(host);
            SendKeys.SendWait("{F11}");
            Until(() => toggle.Visible);
            Assert.Same(workspace, host.CurrentSecurityControl);
            Assert.Equal(size, workspace.ClientSize);
            host.CloseWorkspaceButton.PerformClick();
            Until(() => host.CurrentSecurityControl == null);
        });

    private static void Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("UI Lab did not settle.");
            System.Windows.Forms.Application.DoEvents();
            System.Threading.Thread.Sleep(10);
        }
    }
}
