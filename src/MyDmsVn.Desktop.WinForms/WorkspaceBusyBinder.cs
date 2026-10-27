using System;
using System.Threading;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms;

// Projects app-owned admission state into a content-local visual token. The
// scope identity fences late completions; this class never enables commands.
internal sealed class WorkspaceBusyBinder : IDisposable
{
    private readonly Func<BusyPresentation, IDisposable> _begin;
    private readonly int _uiThreadId;
    private readonly SynchronizationContext? _uiContext;
    private readonly Action<Action>? _dispatch;
    private readonly string _operationKey = "busy-presentation-" + Guid.NewGuid().ToString("N");
    private WorkspaceFeedbackScope _scope;
    private IDisposable? _token;
    private WorkspaceOperationContext? _presentationContext;
    private Owner _presentedOwner;
    private string? _message;
    private Action? _cancel;
    private bool _disposed;

    internal WorkspaceBusyBinder(WorkspaceFeedbackScope scope, Func<BusyPresentation, IDisposable> begin,
        Action<Action>? dispatch = null)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _begin = begin ?? throw new ArgumentNullException(nameof(begin));
        _uiThreadId = Thread.CurrentThread.ManagedThreadId;
        _uiContext = SynchronizationContext.Current;
        _dispatch = dispatch;
        _scope.Invalidated += OnScopeInvalidated;
    }

    internal Owner CaptureOwner() => new Owner(_scope.WorkspaceId, _scope.SessionGeneration, _scope.DocumentGeneration);

    internal void Project(Owner owner, bool busy, string message, Action? cancelRequested = null)
    {
        VerifyThread();
        if (_disposed || !owner.Equals(CaptureOwner())) return;
        if (!busy) { Release(); return; }
        if (_token != null && _presentedOwner.Equals(owner) &&
            string.Equals(_message, message, StringComparison.Ordinal) && ReferenceEquals(_cancel, cancelRequested)) return;
        WorkspaceOperationContext context;
        try { context = _scope.BeginOperation(_operationKey, WorkspaceOperationKind.Query); }
        catch (ObjectDisposedException) { Release(); return; }
        IDisposable token;
        try { token = _begin(new BusyPresentation(message, cancelRequested)); }
        catch { Release(); throw; }
        if (!owner.Equals(CaptureOwner()) || !_scope.IsCurrent(context))
        {
            token.Dispose();
            Release();
            return;
        }
        var previous = _token;
        _token = token;
        _presentationContext = context;
        _presentedOwner = owner;
        _message = message;
        _cancel = cancelRequested;
        previous?.Dispose();
    }

    internal void ReplaceScope(WorkspaceFeedbackScope scope)
    {
        VerifyThread();
        if (_disposed) throw new ObjectDisposedException(nameof(WorkspaceBusyBinder));
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        _scope.Invalidated -= OnScopeInvalidated;
        Release();
        _scope = scope;
        _scope.Invalidated += OnScopeInvalidated;
    }

    private void OnScopeInvalidated(object? sender, EventArgs args)
    {
        if (Thread.CurrentThread.ManagedThreadId == _uiThreadId) { Release(); return; }
        void ReleaseIfInvalidated()
        {
            if (!_disposed && ReferenceEquals(sender, _scope) && _presentationContext != null &&
                !_scope.IsCurrent(_presentationContext)) Release();
        }
        if (_dispatch != null) _dispatch(ReleaseIfInvalidated);
        else _uiContext?.Post(_ => ReleaseIfInvalidated(), null);
    }

    private void Release()
    {
        var token = _token;
        _token = null;
        _presentationContext = null;
        _message = null;
        _cancel = null;
        token?.Dispose();
    }

    private void VerifyThread()
    {
        if (Thread.CurrentThread.ManagedThreadId != _uiThreadId)
            throw new InvalidOperationException("Busy presentation must be changed on its UI thread.");
    }

    public void Dispose()
    {
        VerifyThread();
        if (_disposed) return;
        _disposed = true;
        _scope.Invalidated -= OnScopeInvalidated;
        Release();
    }

    internal readonly struct Owner : IEquatable<Owner>
    {
        internal Owner(Guid workspaceId, long session, long document)
        { WorkspaceId = workspaceId; Session = session; Document = document; }
        internal Guid WorkspaceId { get; }
        internal long Session { get; }
        internal long Document { get; }
        public bool Equals(Owner other) => WorkspaceId == other.WorkspaceId && Session == other.Session && Document == other.Document;
        public override bool Equals(object? obj) => obj is Owner other && Equals(other);
        public override int GetHashCode() => WorkspaceId.GetHashCode() ^ Session.GetHashCode() ^ Document.GetHashCode();
    }
}
