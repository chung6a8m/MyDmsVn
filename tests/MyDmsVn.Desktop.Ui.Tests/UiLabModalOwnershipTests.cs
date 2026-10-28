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
    public void Security_lab_dirty_close_uses_native_owned_prompt_and_escape_keeps_workspace_and_draft()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L10, (_, __) =>
        {
            using var lab = new SecurityLabForm();
            lab.Show();
            Until(() => lab.CurrentSecurityControl != null && !lab.IsResetting);
            var control = lab.CurrentSecurityControl!;
            control.Tabs.SelectedIndex = 1;
            Assert.True(control.ViewModel.BeginRolePermissions());
            control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
            var editorIdentity = control.ViewModel.EditorIdentity;
            Assert.True(control.ViewModel.HasDirtyEditor);
            NativeLookupTypingDriver.Activate(lab);

            using var observer = new NativeMessageBoxObserver(lab.Handle, "UI Lab");
            observer.Invoke(lab.CloseWorkspaceButton.PerformClick);

            Assert.True(observer.Observed);
            Assert.False(observer.DialogStillOpen);
            Assert.Same(control, lab.CurrentSecurityControl);
            Assert.False(control.IsDisposed);
            Assert.True(control.ViewModel.HasDirtyEditor);
            Assert.Equal(editorIdentity, control.ViewModel.EditorIdentity);
            Assert.Contains("Close rejected", lab.StatusText);
        });

    [Fact]
    public void Security_lab_close_prompt_lost_foreground_fails_bounded_cleans_only_its_prompt_and_recovers()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L10, (_, __) =>
        {
            using var lab = new SecurityLabForm();
            using var unrelated = new Form { Text = "Unrelated foreground window" };
            lab.Show();
            unrelated.Show();
            Until(() => lab.CurrentSecurityControl != null && !lab.IsResetting);
            var labScene = Assert.IsType<SecurityScenario>(lab.CurrentSecurityScenarioData);
            var control = lab.CurrentSecurityControl!;
            control.Tabs.SelectedIndex = 1;
            Assert.True(control.ViewModel.BeginRolePermissions());
            control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
            var editorIdentity = control.ViewModel.EditorIdentity;
            NativeLookupTypingDriver.Activate(lab);

            using (var failed = new NativeMessageBoxObserver(lab.Handle, "UI Lab",
                _ => NativeLookupTypingDriver.Activate(unrelated)))
            {
                var watch = Stopwatch.StartNew();
                var failure = Assert.Throws<InvalidOperationException>(new Action(() =>
                    failed.Invoke(lab.CloseWorkspaceButton.PerformClick)));
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(6), failure.ToString());
                Assert.Contains("foreground", failure.Message, StringComparison.OrdinalIgnoreCase);
                Assert.True(failed.Observed);
                Assert.False(failed.DialogStillOpen);
                Assert.False(unrelated.IsDisposed);
                Assert.True(unrelated.Visible);
            }

            NativeLookupTypingDriver.Activate(lab);
            using (var recovered = new NativeMessageBoxObserver(lab.Handle, "UI Lab"))
                recovered.Invoke(lab.CloseWorkspaceButton.PerformClick);

            Assert.Same(control, lab.CurrentSecurityControl);
            Assert.False(control.IsDisposed);
            Assert.True(control.ViewModel.HasDirtyEditor);
            Assert.Equal(editorIdentity, control.ViewModel.EditorIdentity);
            Assert.DoesNotContain(labScene.Journal.Entries, entry => entry.Operation == "Security.Mutate");
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Modal_owner_or_title_mismatch_fails_bounded_closes_its_dialog_and_allows_next_operation(bool wrongOwner)
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            using var first = new SecurityLabForm();
            using var second = new SecurityLabForm();
            first.Show(); second.Show();
            Until(() => first.CurrentSecurityControl != null && !first.IsResetting &&
                second.CurrentSecurityControl != null && !second.IsResetting);
            try
            {
                var watch = Stopwatch.StartNew();
                var failure = Assert.ThrowsAny<Exception>(() => InspectDialog(first,
                    wrongOwner ? second : first, expectRelease: false,
                    wrongOwner ? "Đặt lại mật khẩu — " : "Tiêu đề không khớp"));
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(6), failure.ToString());
                Assert.Contains(wrongOwner ? "owner" : "title", failure.Message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(System.Windows.Forms.Application.OpenForms.Cast<Form>(), form => form.Modal);

                InspectDialog(first, expectRelease: false);
                Assert.DoesNotContain(System.Windows.Forms.Application.OpenForms.Cast<Form>(), form => form.Modal);
            }
            finally
            {
                first.CloseWorkspaceButton.PerformClick();
                second.CloseWorkspaceButton.PerformClick();
                Until(() => first.CurrentSecurityControl == null && second.CurrentSecurityControl == null);
            }
        });

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

    private static void InspectDialog(SecurityLabForm host, bool expectRelease) =>
        InspectDialog(host, host, expectRelease, "Đặt lại mật khẩu — ");

    private static void InspectDialog(SecurityLabForm commandHost, SecurityLabForm expectedOwner,
        bool expectRelease, string expectedTitlePrefix)
    {
        ExceptionDispatchInfo? failure = null;
        var existingForms = System.Windows.Forms.Application.OpenForms.Cast<Form>().ToArray();
        Form? observed = null;
        var watch = Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 40 };
        timer.Tick += (_, __) =>
        {
            var dialog = observed;
            if (dialog == null || dialog.IsDisposed)
                dialog = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form =>
                    form.Modal && !existingForms.Contains(form));
            if (dialog == null)
            {
                if (watch.Elapsed < TimeSpan.FromSeconds(5)) return;
                timer.Stop();
                failure = ExceptionDispatchInfo.Capture(new TimeoutException(
                    "Password dialog did not appear within the 5 second observation deadline."));
                return;
            }
            observed = dialog;
            if (!ReferenceEquals(dialog.Owner, expectedOwner))
            {
                timer.Stop();
                failure = ExceptionDispatchInfo.Capture(new InvalidOperationException(
                    "Password dialog owner mismatch. Expected=" + expectedOwner.Text +
                    "; actual=" + (dialog.Owner?.Text ?? "<null>") + "."));
                dialog.Close();
                return;
            }
            if (!dialog.Text.StartsWith(expectedTitlePrefix, StringComparison.Ordinal))
            {
                timer.Stop();
                failure = ExceptionDispatchInfo.Capture(new InvalidOperationException(
                    "Password dialog title mismatch. Expected prefix='" + expectedTitlePrefix +
                    "'; actual='" + dialog.Text + "'."));
                dialog.Close();
                return;
            }
            try
            {
                var fields = Assert.IsAssignableFrom<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapModal>(dialog).FooterPanel;
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
            var reset = Assert.Single(commandHost.CurrentSecurityControl!.UserDetail.HeaderActions.Controls.OfType<Button>(),
                button => button.Text == "Đặt lại mật khẩu…");
            reset.PerformClick();
            Assert.False(timer.Enabled, "Password dialog did not open or close for the expected host.");
            failure?.Throw();
        }
        finally
        {
            timer.Stop();
            if (observed != null && !observed.IsDisposed) observed.Close();
        }
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
