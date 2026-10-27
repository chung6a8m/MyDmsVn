using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.Tests;
using Xunit;

namespace MyDmsVn.Desktop.Ui.Tests;

[Trait("Module", "Common")]
public sealed class UiLabViewportTests
{
    [Fact]
    public void Fixed_review_viewport_survives_review_tool_visibility_and_reports_actual_geometry()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            using var host = new SecurityLabForm();
            host.Show();
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            var width = Assert.IsType<NumericUpDown>(Assert.Single(host.Controls.Find("ReviewViewportWidth", true)));
            var height = Assert.IsType<NumericUpDown>(Assert.Single(host.Controls.Find("ReviewViewportHeight", true)));
            var apply = Assert.IsType<Button>(Assert.Single(host.Controls.Find("ReviewViewportApply", true)));
            var diagnostics = Assert.IsType<Label>(Assert.Single(host.Controls.Find("ViewportDiagnostics", true)));
            width.Value = 652;
            height.Value = 497;
            apply.PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Equal(new Size(652, 497), host.CurrentSecurityControl!.ClientSize);
            host.SecurityReviewToggleButton.PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Equal(new Size(652, 497), host.CurrentSecurityControl!.ClientSize);
            Assert.Contains("Outer", diagnostics.Text);
            Assert.Contains("Content", diagnostics.Text);
            Assert.Contains("DeviceDpi", diagnostics.Text);
            Assert.Contains("Physical scale: unverified", diagnostics.Text);
            host.WindowState = FormWindowState.Maximized;
            System.Windows.Forms.Application.DoEvents();
            Assert.True(host.ClientSize.Width == diagnostics.ClientSize.Width, $"host={host.ClientSize}, label={diagnostics.ClientSize}, dock={diagnostics.Dock}, auto={diagnostics.AutoSize}, parent={diagnostics.Parent?.GetType().Name}");
            var measured = TextRenderer.MeasureText(diagnostics.Text, diagnostics.Font,
                new Size(diagnostics.ClientSize.Width, int.MaxValue), TextFormatFlags.WordBreak);
            Assert.True(measured.Height <= diagnostics.ClientSize.Height,
                $"Diagnostics text needs {measured.Height}px but has {diagnostics.ClientSize.Height}px (width {diagnostics.ClientSize.Width}).");
            host.CloseWorkspaceButton.PerformClick();
            Until(() => host.CurrentSecurityControl == null);
        });

    [Fact]
    public void Review_mode_keeps_viewport_and_hides_developer_chrome_until_restored()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            using var host = new SecurityLabForm { ClientSize = new Size(1000, 700) };
            host.Show();
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            var width = Assert.IsType<NumericUpDown>(Assert.Single(host.Controls.Find("ReviewViewportWidth", true)));
            var height = Assert.IsType<NumericUpDown>(Assert.Single(host.Controls.Find("ReviewViewportHeight", true)));
            var apply = Assert.IsType<Button>(Assert.Single(host.Controls.Find("ReviewViewportApply", true)));
            var review = Assert.IsType<Button>(Assert.Single(host.Controls.Find("ReviewModeToggle", true)));
            var diagnostics = Assert.IsType<Label>(Assert.Single(host.Controls.Find("ViewportDiagnostics", true)));
            width.Value = 600;
            height.Value = 550;
            apply.PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Contains("viewport clipped", diagnostics.Text);
            var original = host.CurrentSecurityControl;
            review.PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Same(original, host.CurrentSecurityControl);
            Assert.Equal(new Size(600, 550), original!.ClientSize);
            Assert.Contains("viewport visible", diagnostics.Text);
            Assert.Contains("F11", diagnostics.Text);
            Assert.False(review.Visible);
            NativeLookupTypingDriver.Activate(host);
            SendKeys.SendWait("{F11}");
            Until(() => review.Visible);
            Assert.Same(original, host.CurrentSecurityControl);
            Assert.Equal(new Size(600, 550), original.ClientSize);
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
