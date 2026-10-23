using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class WorkspaceFeedbackHostTests
    {
        [Fact]
        public void Recovery_clear_cannot_erase_independent_failure_admitted_after_recovery_started()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope))
                {
                    form.Controls.Add(host);
                    form.Show();
                    var recovery = scope.Capture(scope.BeginOperation("reload", WorkspaceOperationKind.Query));
                    scope.Capture(scope.BeginOperation("list", WorkspaceOperationKind.Query)).Publish(
                        new DesktopNotification(DesktopNotificationKind.Error, "New list failure"));
                    Task.Run(() => recovery.ClearActionable()).GetAwaiter().GetResult();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.True(host.Alert.Visible);
                    Assert.Equal("New list failure", host.Alert.Text);
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Save_success_preserves_independent_load_failure()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope))
                {
                    form.Controls.Add(host);
                    form.Show();
                    scope.Capture(scope.BeginOperation("load", WorkspaceOperationKind.Query)).Publish(
                        new DesktopNotification(DesktopNotificationKind.Error, "Load failed"));
                    scope.Capture(scope.BeginOperation("save", WorkspaceOperationKind.Write)).Publish(
                        new DesktopNotification(DesktopNotificationKind.Success, "Saved"));
                    Assert.True(host.Alert.Visible);
                    Assert.Equal("Load failed", host.Alert.Text);
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Coalesced_success_refreshes_occurrence_title_and_full_visible_lifetime()
        {
            StaTest.Run(cancellation =>
            {
                var now = new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc);
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope, () => now))
                {
                    form.Controls.Add(host);
                    form.Show();
                    var sink = scope.Capture(scope.BeginOperation("save", WorkspaceOperationKind.Write));
                    sink.Publish(new DesktopNotification(DesktopNotificationKind.Success, "First"));
                    var toast = Assert.IsType<BootstrapToast>(Assert.Single(host.ToastContainer.Controls.Cast<Control>()));
                    var firstTitle = toast.Title;
                    PumpFor(TimeSpan.FromSeconds(4), cancellation);
                    now = now.AddMinutes(1);
                    sink.Publish(new DesktopNotification(DesktopNotificationKind.Success, "Second"));
                    PumpFor(TimeSpan.FromSeconds(2), cancellation);
                    Assert.False(toast.IsDisposed);
                    Assert.NotEqual(firstTitle, toast.Title);
                    Assert.Equal("Second", toast.Text);
                    var expiry = Stopwatch.StartNew();
                    while (!toast.IsDisposed && expiry.Elapsed < TimeSpan.FromSeconds(5))
                        PumpFor(TimeSpan.FromMilliseconds(20), cancellation);
                    Assert.True(toast.IsDisposed);
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(15));
        }

        private static void PumpFor(TimeSpan duration, CancellationToken cancellation)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < duration)
            {
                cancellation.ThrowIfCancellationRequested();
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }
        }

        [Fact]
        public void Confirmed_success_clears_old_failure_but_information_and_later_refresh_error_do_not()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope))
                {
                    form.Controls.Add(host);
                    form.Show();
                    var sink = scope.Capture(scope.BeginOperation("save", WorkspaceOperationKind.Write));
                    sink.Publish(new DesktopNotification(DesktopNotificationKind.Error, "Save failed"));
                    sink.Publish(new DesktopNotification(DesktopNotificationKind.Information, "Canceled"));
                    Assert.True(host.Alert.Visible);
                    Assert.Equal("Save failed", host.Alert.Text);
                    sink.Publish(new DesktopNotification(DesktopNotificationKind.Success, "Saved"));
                    Assert.False(host.Alert.Visible);
                    Assert.Equal(string.Empty, host.Alert.Text);
                    scope.Capture(scope.BeginOperation("refresh", WorkspaceOperationKind.Query)).Publish(
                        new DesktopNotification(DesktopNotificationKind.Error, "Saved; refresh failed"));
                    Assert.True(host.Alert.Visible);
                    Assert.Equal("Saved; refresh failed", host.Alert.Text);
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Authoritative_recovery_clear_preserves_transient_notices()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope))
                {
                    form.Controls.Add(host);
                    form.Show();
                    var sink = scope.Capture(scope.BeginOperation("reload", WorkspaceOperationKind.Query));
                    sink.Publish(new DesktopNotification(DesktopNotificationKind.Information, "Inspecting"));
                    Task.Run(() =>
                    {
                        sink.Publish(new DesktopNotification(DesktopNotificationKind.Error, "Outcome unknown"));
                        sink.ClearActionable();
                    }).GetAwaiter().GetResult();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.False(host.Alert.Visible);
                    Assert.Equal(string.Empty, host.Alert.Text);
                    Assert.Single(host.ToastContainer.Controls.Cast<Control>());
                    Task.Run(() =>
                    {
                        sink.ClearActionable();
                        sink.Publish(new DesktopNotification(DesktopNotificationKind.Error, "New refresh failure"));
                    }).GetAwaiter().GetResult();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.True(host.Alert.Visible);
                    Assert.Equal("New refresh failure", host.Alert.Text);
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Queued_recovery_clear_cannot_erase_error_after_new_query_or_document(bool replaceDocument)
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope))
                {
                    form.Controls.Add(host);
                    form.Show();
                    var sink = scope.Capture(scope.BeginOperation("reload", WorkspaceOperationKind.Query));
                    sink.Publish(new DesktopNotification(DesktopNotificationKind.Error, "Old recovery"));
                    if (replaceDocument)
                    {
                        Task.Run(() => sink.ClearActionable()).GetAwaiter().GetResult();
                        scope.AdvanceDocument();
                        scope.Capture(scope.BeginOperation("reload", WorkspaceOperationKind.Query)).Publish(
                            new DesktopNotification(DesktopNotificationKind.Error, "Current failure"));
                        sink.ClearActionable();
                    }
                    else
                    {
                        scope.Capture(scope.BeginOperation("write", WorkspaceOperationKind.Write)).Publish(
                            new DesktopNotification(DesktopNotificationKind.Error, "Current failure"));
                        Task.Run(() => sink.ClearActionable()).GetAwaiter().GetResult();
                        scope.BeginOperation("reload", WorkspaceOperationKind.Query);
                    }
                    System.Windows.Forms.Application.DoEvents();
                    Assert.True(host.Alert.Visible);
                    Assert.Equal("Current failure", host.Alert.Text);
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Information_keeps_its_semantic_variant_when_coalesced_with_success()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope))
                {
                    form.Controls.Add(host);
                    form.Show();
                    var sink = scope.Capture(scope.BeginOperation("save", WorkspaceOperationKind.Write));
                    sink.Publish(new DesktopNotification(DesktopNotificationKind.Success, "Saved"));
                    var toast = Assert.IsType<BootstrapToast>(Assert.Single(host.ToastContainer.Controls.Cast<Control>()));
                    Assert.Equal(BootstrapVariant.Success, toast.Variant);
                    sink.Publish(new DesktopNotification(DesktopNotificationKind.Information, "Canceled before dispatch"));
                    Assert.Same(toast, Assert.Single(host.ToastContainer.Controls.Cast<Control>()));
                    Assert.Equal(BootstrapVariant.Info, toast.Variant);
                    Assert.Equal("Canceled before dispatch", toast.Text);
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Completion_stays_in_originating_hidden_workspace_without_stealing_selected_focus()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scopeA = new WorkspaceFeedbackScope())
                using (var scopeB = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var tabs = new TabControl { Dock = DockStyle.Fill })
                using (var hostA = new WorkspaceFeedbackHost(new TextBox(), scopeA))
                using (var hostB = new WorkspaceFeedbackHost(new TextBox(), scopeB))
                {
                    var pageA = new TabPage("A");
                    var pageB = new TabPage("B");
                    pageA.Controls.Add(hostA);
                    pageB.Controls.Add(hostB);
                    tabs.TabPages.Add(pageA);
                    tabs.TabPages.Add(pageB);
                    form.Controls.Add(tabs);
                    form.Show();
                    var sinkA = scopeA.Capture(scopeA.BeginOperation("save", WorkspaceOperationKind.Write));
                    tabs.SelectedTab = pageB;
                    hostB.Content.Focus();
                    Task.Run(() => sinkA.Publish(new DesktopNotification(DesktopNotificationKind.Error, "A failure"))).GetAwaiter().GetResult();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal(string.Empty, hostB.Alert.Text);
                    Assert.True(hostB.Content.Focused);
                    tabs.SelectedTab = pageA;
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal("A failure", hostA.Alert.Text);
                    Assert.True(hostA.Alert.Visible);
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Dispatched_query_error_rechecks_freshness_after_new_query_admission()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope))
                {
                    form.Controls.Add(host);
                    form.Show();
                    var old = scope.Capture(scope.BeginOperation("list", WorkspaceOperationKind.Query));
                    Task.Run(() => old.Publish(new DesktopNotification(DesktopNotificationKind.Error, "Old query"))).GetAwaiter().GetResult();
                    scope.BeginOperation("list", WorkspaceOperationKind.Query);
                    System.Windows.Forms.Application.DoEvents();
                    Assert.False(host.Alert.Visible);
                    Assert.Equal(string.Empty, host.Alert.Text);
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Inactive_success_expires_after_five_minutes_without_expiring_error()
        {
            StaTest.Run(_ =>
            {
                var now = new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc);
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope, () => now))
                {
                    form.Controls.Add(host);
                    form.Show();
                    host.Hide();
                    var sink = scope.Capture(scope.BeginOperation("save", WorkspaceOperationKind.Write));
                    sink.Publish(new DesktopNotification(DesktopNotificationKind.Success, "Expired"));
                    sink.Publish(new DesktopNotification(DesktopNotificationKind.Error, "Recovery required"));
                    now = now.AddMinutes(5);
                    host.Show();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Empty(host.ToastContainer.Controls);
                    Assert.Equal("Recovery required", host.Alert.Text);
                    Assert.True(host.Alert.Visible);
                    host.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => scope.BeginOperation("closed", WorkspaceOperationKind.Query));
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Prehandle_background_error_is_delivered_without_focus_theft()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new TextBox(), scope))
                {
                    form.Controls.Add(host);
                    var context = scope.BeginOperation("save", WorkspaceOperationKind.Write);
                    Task.Run(() => scope.Capture(context).Publish(new DesktopNotification(DesktopNotificationKind.Error, "Keep draft"))).GetAwaiter().GetResult();
                    Assert.False(host.IsHandleCreated);
                    form.Show();
                    host.Content.Focus();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal("Keep draft", host.Alert.Text);
                    Assert.True(host.Alert.Visible);
                    Assert.True(host.Content.Focused);
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Queued_callback_is_rejected_after_document_boundary_and_disposal()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope))
                {
                    form.Controls.Add(host);
                    form.Show();
                    var sink = scope.Capture(scope.BeginOperation("post", WorkspaceOperationKind.Write));
                    Task.Run(() => sink.Publish(new DesktopNotification(DesktopNotificationKind.Error, "Late"))).GetAwaiter().GetResult();
                    scope.AdvanceDocument();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.False(host.Alert.Visible);
                    Assert.Equal(string.Empty, host.Alert.Text);
                    host.Dispose();
                    Task.Run(() => sink.Publish(new DesktopNotification(DesktopNotificationKind.Error, "Disposed"))).GetAwaiter().GetResult();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Inactive_success_keeps_latest_and_error_persists_on_return()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope))
                {
                    form.Controls.Add(host);
                    form.Show();
                    host.Hide();
                    for (var i = 0; i < 20; i++)
                    {
                        scope.Capture(scope.BeginOperation("save", WorkspaceOperationKind.Write)).Publish(
                            new DesktopNotification(DesktopNotificationKind.Success, "Saved " + i));
                    }
                    scope.Capture(scope.BeginOperation("refresh", WorkspaceOperationKind.Query)).Publish(
                        new DesktopNotification(DesktopNotificationKind.Error, "Refresh failed"));
                    Assert.True(host.HasAttention);
                    host.Show();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal("Refresh failed", host.Alert.Text);
                    Assert.False(host.HasAttention);
                    Assert.Single(host.ToastContainer.Controls.Cast<Control>(), control => control.Text == "Saved 19");
                    Assert.DoesNotContain(host.ToastContainer.Controls.Cast<Control>(), control => control.Text == "Saved 0");
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Active_burst_bounds_container_and_pending_successes()
        {
            StaTest.Run(_ =>
            {
                using (var guard = new WinFormsTestGuard())
                using (var scope = new WorkspaceFeedbackScope())
                using (var form = new Form())
                using (var host = new WorkspaceFeedbackHost(new Panel(), scope))
                {
                    form.Controls.Add(host);
                    form.Show();
                    for (var i = 0; i < 30; i++)
                    {
                        scope.Capture(scope.BeginOperation("save" + i, WorkspaceOperationKind.Write)).Publish(
                            new DesktopNotification(DesktopNotificationKind.Success, "Saved " + i));
                    }
                    Assert.InRange(host.ToastContainer.Controls.Count, 1, 3);
                    Assert.InRange(host.PendingSuccessCount, 0, 5);
                    scope.Invalidate();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal(0, host.PendingSuccessCount);
                    Assert.False(host.Alert.Visible);
                    Assert.All(host.ToastContainer.Controls.Cast<Control>(), control => Assert.Equal(string.Empty, control.Text));
                    Assert.Empty(guard.Exceptions);
                }
            }, TimeSpan.FromSeconds(10));
        }
    }
}
