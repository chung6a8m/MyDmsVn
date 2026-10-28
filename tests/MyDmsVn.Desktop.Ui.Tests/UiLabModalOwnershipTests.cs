using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
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

            var observed = false;
            ExceptionDispatchInfo? failure = null;
            var watch = Stopwatch.StartNew();
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (_, __) =>
            {
                var dialog = GetForegroundWindow();
                GetWindowThreadProcessId(dialog, out var processId);
                if (dialog == IntPtr.Zero || WindowText(dialog, className: true) != "#32770" ||
                    processId != (uint)Process.GetCurrentProcess().Id)
                {
                    if (watch.Elapsed < TimeSpan.FromSeconds(5)) return;
                    timer.Stop();
                    failure = ExceptionDispatchInfo.Capture(new TimeoutException("Native Security Lab Close prompt did not appear within 5 seconds."));
                    return;
                }
                timer.Stop();
                try
                {
                    observed = true;
                    Assert.Equal("UI Lab", WindowText(dialog, className: false));
                    Assert.Equal(lab.Handle, GetWindow(dialog, 4));
                    var focus = GetFocus();
                    Assert.NotEqual(IntPtr.Zero, focus);
                    Assert.True(IsChild(dialog, focus), "Native Security Lab Close prompt must own the keyboard focus.");
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                }
                catch (Exception error)
                {
                    failure = ExceptionDispatchInfo.Capture(error);
                    NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                }
            };
            try
            {
                timer.Start();
                lab.CloseWorkspaceButton.PerformClick();
                Assert.False(timer.Enabled, "Native Security Lab Close prompt did not complete its bounded observation.");
                failure?.Throw();
            }
            finally { timer.Stop(); }

            Assert.True(observed);
            Assert.Same(control, lab.CurrentSecurityControl);
            Assert.False(control.IsDisposed);
            Assert.True(control.ViewModel.HasDirtyEditor);
            Assert.Equal(editorIdentity, control.ViewModel.EditorIdentity);
            Assert.Contains("Close rejected", lab.StatusText);
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

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetFocus();
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int count);

    private static string WindowText(IntPtr window, bool className)
    {
        var text = new StringBuilder(256);
        if (className) GetClassName(window, text, text.Capacity);
        else GetWindowText(window, text, text.Capacity);
        return text.ToString();
    }
}
