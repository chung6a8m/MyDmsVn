using System;
using System.Collections.Generic;

namespace MyDmsVn.Desktop.Application
{
    public enum WorkspaceOperationKind { Query, Write }
    public sealed class WorkspaceOperationContext
    {
        internal WorkspaceOperationContext(Guid workspaceId, long session, long document, string key, long generation, WorkspaceOperationKind kind)
        { WorkspaceId = workspaceId; SessionGeneration = session; DocumentGeneration = document; OperationKey = key; Generation = generation; Kind = kind; }
        public Guid WorkspaceId { get; }
        public long SessionGeneration { get; }
        public long DocumentGeneration { get; }
        public string OperationKey { get; }
        public long Generation { get; }
        public WorkspaceOperationKind Kind { get; }
    }
    public interface IWorkspaceFeedbackSink
    {
        void Publish(DesktopNotification notice);
        void ClearActionable();
    }
    public interface IWorkspaceFeedbackScope : IDisposable
    {
        IWorkspaceFeedbackSink Capture(WorkspaceOperationContext context);
    }
    public sealed class WorkspaceFeedbackEventArgs : EventArgs
    {
        public WorkspaceFeedbackEventArgs(WorkspaceOperationContext context, DesktopNotification notification)
        { Context = context; Notification = notification; }
        public WorkspaceOperationContext Context { get; }
        public DesktopNotification Notification { get; }
    }
    public sealed class WorkspaceFeedbackScope : IWorkspaceFeedbackScope
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, long> _operationGenerations = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Func<long>? _currentSessionVersion;
        private long _next;
        private bool _disposed;
        public WorkspaceFeedbackScope(long sessionGeneration = 0, Func<long>? currentSessionVersion = null)
        { WorkspaceId = Guid.NewGuid(); SessionGeneration = sessionGeneration; _currentSessionVersion = currentSessionVersion; }
        public Guid WorkspaceId { get; }
        public long SessionGeneration { get; }
        public long DocumentGeneration { get; private set; }
        public event EventHandler<WorkspaceFeedbackEventArgs>? Published;
        public event EventHandler? Invalidated;
        public event EventHandler<WorkspaceOperationContext>? ActionableCleared;
        public WorkspaceOperationContext BeginOperation(string key, WorkspaceOperationKind kind)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("An operation key is required.", nameof(key));
            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(WorkspaceFeedbackScope));
                var generation = ++_next;
                _operationGenerations[kind + ":" + key] = generation;
                return new WorkspaceOperationContext(WorkspaceId, SessionGeneration, DocumentGeneration, key, generation, kind);
            }
        }
        public IWorkspaceFeedbackSink Capture(WorkspaceOperationContext context)
        { if (context == null) throw new ArgumentNullException(nameof(context)); return new Sink(this, context); }
        public bool IsCurrent(WorkspaceOperationContext context)
        {
            lock (_sync)
            {
                return context != null && !_disposed &&
                    context.WorkspaceId == WorkspaceId &&
                    context.SessionGeneration == SessionGeneration &&
                    (_currentSessionVersion == null || _currentSessionVersion() == SessionGeneration) &&
                    context.DocumentGeneration == DocumentGeneration &&
                    _operationGenerations.TryGetValue(context.Kind + ":" + context.OperationKey, out var generation) &&
                    generation == context.Generation;
            }
        }
        public void AdvanceDocument()
        {
            lock (_sync) { if (_disposed) return; DocumentGeneration++; _operationGenerations.Clear(); }
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
        public void Invalidate() => Dispose();
        public void Dispose()
        {
            lock (_sync) { if (_disposed) return; _disposed = true; _operationGenerations.Clear(); }
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
        private void Publish(WorkspaceOperationContext context, DesktopNotification notice)
        {
            if (notice == null) throw new ArgumentNullException(nameof(notice));
            lock (_sync) { if (IsCurrent(context)) Published?.Invoke(this, new WorkspaceFeedbackEventArgs(context, notice)); }
        }
        private void ClearActionable(WorkspaceOperationContext context)
        {
            lock (_sync) { if (IsCurrent(context)) ActionableCleared?.Invoke(this, context); }
        }
        private sealed class Sink : IWorkspaceFeedbackSink
        {
            private readonly WorkspaceFeedbackScope _scope; private readonly WorkspaceOperationContext _context;
            public Sink(WorkspaceFeedbackScope scope, WorkspaceOperationContext context) { _scope = scope; _context = context; }
            public void Publish(DesktopNotification notice) => _scope.Publish(_context, notice);
            public void ClearActionable() => _scope.ClearActionable(_context);
        }
    }

    // Adapts existing ViewModels while preserving the context captured before an async operation starts.
    public sealed class WorkspaceFeedbackNotificationService : IDesktopNotificationService
    {
        private readonly WorkspaceFeedbackScope _scope;
        private readonly System.Threading.AsyncLocal<WorkspaceOperationContext?> _current = new System.Threading.AsyncLocal<WorkspaceOperationContext?>();
        public WorkspaceFeedbackNotificationService(WorkspaceFeedbackScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _scope.Invalidated += (_, __) => LastNotification = null;
        }
        public WorkspaceFeedbackScope Scope => _scope;
        public event EventHandler<DesktopNotification>? NotificationPublished;
        public DesktopNotification? LastNotification { get; private set; }
        public async System.Threading.Tasks.Task RunAsync(string key, WorkspaceOperationKind kind, Func<System.Threading.Tasks.Task> operation)
        {
            var previous = _current.Value;
            _current.Value = _scope.BeginOperation(key, kind);
            try { await operation(); }
            finally { _current.Value = previous; }
        }
        public void ClearActionableForCurrentOperation()
        {
            var context = _current.Value;
            if (context != null && _scope.IsCurrent(context)) _scope.Capture(context).ClearActionable();
        }
        public void Publish(DesktopNotification notification)
        {
            if (notification == null) throw new ArgumentNullException(nameof(notification));
            // Uncaptured background notices must not be promoted to whichever workspace is selected.
            if (_current.Value == null || !_scope.IsCurrent(_current.Value)) return;
            _scope.Capture(_current.Value).Publish(notification);
            if (!_scope.IsCurrent(_current.Value)) return;
            LastNotification = notification;
            NotificationPublished?.Invoke(this, notification);
        }
    }
}
