using System;
using System.Collections.Generic;
using MyDmsVn.Desktop.Application;
using Xunit;
namespace MyDmsVn.Desktop.Session.Tests
{
    public sealed class WorkspaceFeedbackScopeTests
    {
        [Fact]
        public void Session_version_change_rejects_pending_catalog_feedback_before_ui_cleanup()
        {
            long currentSession = 7;
            using var scope = new WorkspaceFeedbackScope(currentSession, () => currentSession);
            var sink = scope.Capture(scope.BeginOperation("save", WorkspaceOperationKind.Write));
            int publications = 0;
            scope.Published += (_, __) => publications++;
            currentSession = 8;
            sink.Publish(new DesktopNotification(DesktopNotificationKind.Error, "old session"));
            Assert.Equal(0, publications);
            Assert.False(scope.IsCurrent(scope.BeginOperation("next", WorkspaceOperationKind.Query)));
        }
        [Fact]
        public void Recovery_clear_is_explicit_and_fenced_by_document_lifetime()
        {
            using var scope = new WorkspaceFeedbackScope();
            var stale = scope.Capture(scope.BeginOperation("recovery", WorkspaceOperationKind.Query));
            int clears = 0; scope.ActionableCleared += (_, __) => clears++;
            scope.BeginOperation("list", WorkspaceOperationKind.Query);
            Assert.Equal(0, clears);
            stale.ClearActionable(); Assert.Equal(1, clears);
            scope.AdvanceDocument(); stale.ClearActionable(); Assert.Equal(1, clears);
        }
        [Fact]
        public async System.Threading.Tasks.Task Legacy_adapter_captures_before_await_and_scrubs_late_document_result()
        {
            using var scope = new WorkspaceFeedbackScope();
            var adapter = new WorkspaceFeedbackNotificationService(scope);
            var gate = new System.Threading.Tasks.TaskCompletionSource<bool>();
            int publications = 0;
            scope.Published += (_, __) => publications++;
            var operation = adapter.RunAsync("save", WorkspaceOperationKind.Write, async () =>
            {
                await gate.Task;
                adapter.Publish(new DesktopNotification(DesktopNotificationKind.Error, "old document"));
            });
            scope.AdvanceDocument(); gate.SetResult(true); await operation;
            Assert.Equal(0, publications); Assert.Null(adapter.LastNotification);
            await adapter.RunAsync("save", WorkspaceOperationKind.Write, () =>
            {
                adapter.Publish(new DesktopNotification(DesktopNotificationKind.Success, "saved"));
                return System.Threading.Tasks.Task.CompletedTask;
            });
            Assert.Equal(1, publications);
            scope.Invalidate(); Assert.Null(adapter.LastNotification);
        }
        [Fact]
        public void Query_supersession_does_not_hide_authoritative_write_outcome()
        {
            using var scope = new WorkspaceFeedbackScope(7);
            var old = scope.BeginOperation("load", WorkspaceOperationKind.Query);
            var write = scope.BeginOperation("save", WorkspaceOperationKind.Write);
            var current = scope.BeginOperation("load", WorkspaceOperationKind.Query);
            var notices = new List<string>();
            scope.Published += (_, e) => notices.Add(e.Notification.Message);
            scope.Capture(old).Publish(new DesktopNotification(DesktopNotificationKind.Error, "stale"));
            scope.Capture(write).Publish(new DesktopNotification(DesktopNotificationKind.Success, "saved"));
            scope.Capture(current).Publish(new DesktopNotification(DesktopNotificationKind.Error, "refresh failed"));
            Assert.Equal(new[] { "saved", "refresh failed" }, notices);
        }
        [Fact]
        public void Document_session_and_reopened_instance_reject_captured_callbacks()
        {
            using var scope = new WorkspaceFeedbackScope(7);
            var context = scope.BeginOperation("save", WorkspaceOperationKind.Write);
            using var reopened = new WorkspaceFeedbackScope(7);
            Assert.False(reopened.IsCurrent(context));
            scope.AdvanceDocument();
            Assert.False(scope.IsCurrent(context));
            var next = scope.BeginOperation("save", WorkspaceOperationKind.Write);
            scope.Invalidate();
            Assert.False(scope.IsCurrent(next));
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Concurrent_workspaces_complete_in_origin_order_and_dispose_once(bool aFirst)
        {
            using var a = new WorkspaceFeedbackScope(1); using var b = new WorkspaceFeedbackScope(1);
            var seen = new List<Guid>();
            a.Published += (_, e) => seen.Add(e.Context.WorkspaceId);
            b.Published += (_, e) => seen.Add(e.Context.WorkspaceId);
            var sa = a.Capture(a.BeginOperation("save", WorkspaceOperationKind.Write));
            var sb = b.Capture(b.BeginOperation("save", WorkspaceOperationKind.Write));
            if (aFirst) sa.Publish(new DesktopNotification(DesktopNotificationKind.Success, "A"));
            sb.Publish(new DesktopNotification(DesktopNotificationKind.Success, "B"));
            if (!aFirst) sa.Publish(new DesktopNotification(DesktopNotificationKind.Success, "A"));
            Assert.Equal(aFirst ? new[] { a.WorkspaceId, b.WorkspaceId } : new[] { b.WorkspaceId, a.WorkspaceId }, seen);
            int invalidations = 0; a.Invalidated += (_, __) => invalidations++;
            a.Dispose(); a.Dispose(); sa.Publish(new DesktopNotification(DesktopNotificationKind.Error, "late"));
            Assert.Equal(1, invalidations); Assert.Equal(2, seen.Count);
        }
    }
}
