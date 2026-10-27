using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using Xunit;

namespace MyDmsVn.Desktop.Ui.Tests;

public sealed class UiLabCloseResetLifetimeTests
{
    [Fact]
    public void Reset_does_not_replace_graph_while_close_admission_is_pending()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L10, (_, __) =>
        {
            var prompt = new TaskCompletionSource<WorkspaceCloseChoice>();
            var calls = 0;
            using var host = new SecurityLabForm((state, token) => ++calls == 1 ? prompt.Task : Task.FromResult(WorkspaceCloseChoice.Discard));
            host.Show();
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            host.ScenarioSelector.SelectedItem = SecurityScenarioId.SEC_L10;
            Until(() => host.CurrentSecurityScenarioData?.Id == SecurityScenarioId.SEC_L10 && !host.IsResetting);
            var control = host.CurrentSecurityControl!;
            var scene = host.CurrentSecurityScenarioData!;
            Assert.True(control.ViewModel.BeginRolePermissions());
            control.ViewModel.SetRoleGranted("Sales.Receipts.Post", true);
            host.CloseWorkspaceButton.PerformClick();
            Until(() => calls == 1);
            try
            {
                var reset = host.ResetForTestAsync();
                Until(() => reset.IsCompleted);
                Assert.Same(control, host.CurrentSecurityControl);
                Assert.Same(scene, host.CurrentSecurityScenarioData);
                Assert.False(control.IsDisposed);
            }
            finally
            {
                prompt.TrySetResult(WorkspaceCloseChoice.Cancel);
                Until(() => host.CloseWorkspaceButton.Enabled);
                host.CloseWorkspaceButton.PerformClick();
                Until(() => host.CurrentSecurityControl == null);
            }
        });

    [Fact]
    public void Admitted_close_clears_security_release_controls()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            using var host = new SecurityLabForm();
            host.Show();
            Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
            host.SecurityReviewToggleButton.PerformClick();
            host.SecurityMutationOutcomeSelector.SelectedItem = "Hold success";
            host.SecurityArmMutationButton.PerformClick();
            Assert.True(host.SecurityReleaseMutationButton.Enabled);
            host.CloseWorkspaceButton.PerformClick();
            Until(() => host.CurrentSecurityControl == null);
            Assert.False(host.SecurityReleaseMutationButton.Enabled);
        });

    [Fact]
    public void Admitted_close_clears_sales_release_controls()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            using var host = new SalesLabForm();
            host.Show();
            Until(() => host.CurrentSalesControl != null && !host.IsResetting);
            host.HoldReloadButton.PerformClick();
            Assert.True(host.ReleaseReloadButton.Enabled);
            host.CloseWorkspaceButton.PerformClick();
            Until(() => host.CurrentSalesControl == null);
            Assert.False(host.ReleaseReloadButton.Enabled);
        });

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
