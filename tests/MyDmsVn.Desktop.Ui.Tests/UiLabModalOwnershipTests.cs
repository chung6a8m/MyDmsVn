using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using Xunit;

namespace MyDmsVn.Desktop.Ui.Tests;

[Trait("Module", "Security")]
public sealed class UiLabModalOwnershipTests
{
    [Fact]
    public void Held_mutation_release_is_added_only_to_its_own_host_dialog()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            using var first = new SecurityLabForm();
            using var second = new SecurityLabForm();
            first.Show(); second.Show();
            Until(() => first.CurrentSecurityControl != null && !first.IsResetting &&
                second.CurrentSecurityControl != null && !second.IsResetting);
            first.ScenarioSelector.SelectedItem = SecurityScenarioId.SEC_L03;
            second.ScenarioSelector.SelectedItem = SecurityScenarioId.SEC_L03;
            Until(() => first.CurrentSecurityScenarioData?.Id == SecurityScenarioId.SEC_L03 && !first.IsResetting &&
                second.CurrentSecurityScenarioData?.Id == SecurityScenarioId.SEC_L03 && !second.IsResetting);
            first.SecurityReviewToggleButton.PerformClick();
            second.SecurityReviewToggleButton.PerformClick();
            first.SecurityMutationOutcomeSelector.SelectedItem = "Hold success";
            first.SecurityArmMutationButton.PerformClick();
            Assert.True(first.SecurityReleaseMutationButton.Enabled);
            try
            {
                InspectDialog(second, expectRelease: false);
                Assert.True(first.SecurityReleaseMutationButton.Enabled);
                second.SecurityMutationOutcomeSelector.SelectedItem = "Hold success";
                second.SecurityArmMutationButton.PerformClick();
                Assert.True(second.SecurityReleaseMutationButton.Enabled);
                InspectDialog(second, expectRelease: true);
                Assert.True(first.SecurityReleaseMutationButton.Enabled);
                Assert.True(second.SecurityReleaseMutationButton.Enabled);
            }
            finally
            {
                if (first.SecurityReleaseMutationButton.Enabled) first.SecurityReleaseMutationButton.PerformClick();
                if (second.SecurityReleaseMutationButton.Enabled) second.SecurityReleaseMutationButton.PerformClick();
                first.CloseWorkspaceButton.PerformClick();
                second.CloseWorkspaceButton.PerformClick();
                Until(() => first.CurrentSecurityControl == null && second.CurrentSecurityControl == null);
            }
        });

    private static void InspectDialog(SecurityLabForm host, bool expectRelease)
    {
        ExceptionDispatchInfo? failure = null;
        var watch = Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 40 };
        timer.Tick += (_, __) =>
        {
            var dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form =>
                form.Modal && ReferenceEquals(form.Owner, host) && form.Text.StartsWith("Đặt lại mật khẩu — ", StringComparison.Ordinal));
            if (dialog == null) return;
            try
            {
                var fields = Assert.Single(dialog.Controls.OfType<FlowLayoutPanel>());
                var release = fields.Controls.OfType<Button>().FirstOrDefault(button => button.Name == "SecurityLabReleaseMutation");
                // Give both host timers time to tick; absence before then does not prove ownership.
                if (watch.Elapsed < TimeSpan.FromMilliseconds(800)) return;
                if (expectRelease) Assert.NotNull(release);
                else Assert.Null(release);
                timer.Stop();
                dialog.Close();
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); timer.Stop(); dialog.Close(); }
        };
        try
        {
            timer.Start();
            var reset = Assert.Single(host.CurrentSecurityControl!.UserDetail.HeaderActions.Controls.OfType<Button>(),
                button => button.Text == "Đặt lại mật khẩu…");
            reset.PerformClick();
            Assert.False(timer.Enabled, "Password dialog did not open or close for the expected host.");
            failure?.Throw();
        }
        finally { timer.Stop(); }
    }

    private static void Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(8)) throw new TimeoutException("UI Lab did not settle.");
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }
    }
}
