using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    /// <summary>Owns passive feedback for one actual workspace instance.</summary>
    public sealed class WorkspaceFeedbackHost : UserControl
    {
        private sealed class Notice
        {
            public Notice(WorkspaceOperationContext context, DesktopNotification? notification, DateTime occurredUtc)
            {
                Context = context;
                Notification = notification;
                OccurredUtc = occurredUtc;
            }
            public WorkspaceOperationContext Context { get; }
            public DesktopNotification? Notification { get; }
            public bool ClearsActionable => Notification == null;
            public DateTime OccurredUtc { get; }
        }

        private readonly WorkspaceFeedbackScope _scope;
        private readonly Func<DateTime> _utcNow;
        private readonly object _sync = new object();
        private readonly List<Notice> _incoming = new List<Notice>();
        private readonly List<Notice> _pending = new List<Notice>();
        private readonly Dictionary<string, BootstrapToast> _visible = new Dictionary<string, BootstrapToast>(StringComparer.Ordinal);
        private readonly int _uiThread = Thread.CurrentThread.ManagedThreadId;
        private Notice? _inactiveSuccess;
        private WorkspaceOperationContext? _alertContext;
        private bool _clearRequested;
        private bool _disposed;
        private bool _draining;

        public WorkspaceFeedbackHost(Control content, WorkspaceFeedbackScope scope)
            : this(content, scope, () => DateTime.UtcNow)
        {
        }

        internal WorkspaceFeedbackHost(Control content, WorkspaceFeedbackScope scope, Func<DateTime> utcNow)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            Dock = DockStyle.Fill;
            Content.Dock = DockStyle.Fill;
            Alert = new BootstrapAlert { Dock = DockStyle.Top, Height = 64, Visible = false, Dismissible = false, Variant = BootstrapVariant.Danger };
            ToastContainer = new BootstrapToastContainer { MaximumVisibleToasts = 3, Size = new Size(340, 240), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            Controls.Add(Content);
            Controls.Add(Alert);
            Controls.Add(ToastContainer);
            ToastContainer.BringToFront();
            _scope.Published += OnPublished;
            _scope.Invalidated += OnInvalidated;
            _scope.ActionableCleared += OnActionableCleared;
        }

        public Control Content { get; }
        public BootstrapAlert Alert { get; }
        public BootstrapToastContainer ToastContainer { get; }
        public int PendingSuccessCount => _pending.Count + (_inactiveSuccess == null ? 0 : 1);
        public bool HasAttention { get; private set; }
        public event EventHandler? AttentionChanged;

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (ToastContainer != null)
            {
                ToastContainer.Height = Math.Max(1, _visible.Values.Sum(toast => toast.GetPreferredSize(new Size(toast.Width, 0)).Height) + Math.Max(0, _visible.Count - 1) * ToastContainer.ToastSpacing + 8);
                ToastContainer.Location = new Point(Math.Max(0, ClientSize.Width - ToastContainer.Width - 8), Alert.Visible ? Alert.Bottom + 8 : 8);
                ToastContainer.Visible = _visible.Count > 0;
            }
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Drain(); }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (_scope == null || _disposed) return;
            Drain();
            if (Visible) SetAttention(false);
            if (Visible && _inactiveSuccess != null)
            {
                var retained = _inactiveSuccess;
                _inactiveSuccess = null;
                if (IsRelevant(retained)) EnqueueSuccess(retained);
            }
            PumpSuccesses();
        }

        private void OnPublished(object? sender, WorkspaceFeedbackEventArgs e)
        {
            lock (_sync)
            {
                if (_disposed) return;
                var notice = new Notice(e.Context, e.Notification, _utcNow());
                // Coalesce per operation, and bound even the pre-handle/dispatch queue.
                _incoming.RemoveAll(item => !item.ClearsActionable && IsSuccess(item) == IsSuccess(notice) &&
                    (!IsSuccess(notice) || (item.Context.OperationKey == e.Context.OperationKey && item.Notification!.Kind == e.Notification.Kind)));
                _incoming.Add(notice);
                while (_incoming.Count(item => IsSuccess(item)) > 8)
                    _incoming.Remove(_incoming.First(item => IsSuccess(item)));
            }
            ScheduleDrain();
        }

        private void OnActionableCleared(object? sender, WorkspaceOperationContext context)
        {
            lock (_sync)
            {
                if (_disposed) return;
                _incoming.RemoveAll(item => item.ClearsActionable && item.Context.OperationKey == context.OperationKey);
                _incoming.Add(new Notice(context, null, _utcNow()));
                while (_incoming.Count(item => item.ClearsActionable) > 8)
                    _incoming.Remove(_incoming.First(item => item.ClearsActionable));
            }
            ScheduleDrain();
        }

        private void OnInvalidated(object? sender, EventArgs e)
        {
            lock (_sync)
            {
                if (_disposed) return;
                _clearRequested = true;
                _incoming.Clear();
            }
            ScheduleDrain();
        }

        private void ScheduleDrain()
        {
            if (Thread.CurrentThread.ManagedThreadId == _uiThread) { Drain(); return; }
            // BeginInvoke cannot be used until an owning handle exists. HandleCreated drains later.
            if (!IsHandleCreated || IsDisposed) return;
            try { BeginInvoke(new Action(Drain)); }
            catch (InvalidOperationException) { /* Handle destroyed; recreation drains, disposal rejects. */ }
        }

        private void Drain()
        {
            if (_disposed || _draining || !IsHandleCreated) return;
            _draining = true;
            try
            {
                Notice[] notices;
                bool clear;
                lock (_sync)
                {
                    clear = _clearRequested;
                    _clearRequested = false;
                    notices = _incoming.ToArray();
                    _incoming.Clear();
                }
                if (clear) ClearPresentation();
                foreach (var notice in notices)
                {
                    if (!_scope.IsCurrent(notice.Context)) continue;
                    if (notice.ClearsActionable)
                    {
                        if (_alertContext == null || _alertContext.Generation <= notice.Context.Generation)
                            ClearActionablePresentation();
                        continue;
                    }
                    if (IsSuccess(notice))
                    {
                        if (notice.Notification!.Kind == DesktopNotificationKind.Success && _alertContext != null &&
                            _alertContext.Kind == notice.Context.Kind &&
                            string.Equals(_alertContext.OperationKey, notice.Context.OperationKey, StringComparison.Ordinal))
                            ClearActionablePresentation();
                        if (!Visible) _inactiveSuccess = notice;
                        else EnqueueSuccess(notice);
                    }
                    else
                    {
                        Alert.Text = notice.Notification!.Message;
                        _alertContext = notice.Context;
                        Alert.Variant = notice.Notification!.Kind == DesktopNotificationKind.Error ? BootstrapVariant.Danger : BootstrapVariant.Warning;
                        Alert.Visible = true;
                        if (!Visible) SetAttention(true);
                    }
                }
                PerformLayout();
            }
            finally { _draining = false; }
        }

        private static bool IsSuccess(Notice notice) => notice.Notification?.Kind == DesktopNotificationKind.Success || notice.Notification?.Kind == DesktopNotificationKind.Information;
        private bool IsRelevant(Notice notice) => _scope.IsCurrent(notice.Context) && _utcNow() - notice.OccurredUtc < TimeSpan.FromMinutes(5);

        private void EnqueueSuccess(Notice notice)
        {
            var key = notice.Context.OperationKey;
            if (_visible.TryGetValue(key, out var existing))
            {
                existing.Text = notice.Notification!.Message;
                existing.Variant = notice.Notification!.Kind == DesktopNotificationKind.Success ? BootstrapVariant.Success : BootstrapVariant.Info;
                existing.Title = FormatTitle(notice);
                // Public pinned API: restarting eligibility resets the full visible delay.
                existing.AutoHide = false;
                existing.AutoHide = true;
                return;
            }
            _pending.RemoveAll(item => item.Context.OperationKey == key || !IsRelevant(item));
            _pending.Add(notice);
            PumpSuccesses();
            while (_pending.Count > 5) _pending.RemoveAt(0);
        }

        private void PumpSuccesses()
        {
            if (_disposed || !Visible || !IsHandleCreated) return;
            while (_visible.Count < 3 && _pending.Count > 0)
            {
                var notice = _pending[0];
                _pending.RemoveAt(0);
                if (!IsRelevant(notice)) continue;
                var key = notice.Context.OperationKey;
                var toast = new BootstrapToast
                {
                    Text = notice.Notification!.Message,
                    Title = FormatTitle(notice),
                    Variant = notice.Notification!.Kind == DesktopNotificationKind.Success ? BootstrapVariant.Success : BootstrapVariant.Info,
                    AutoHide = true,
                    AutoHideDelay = 5000,
                    Width = 320,
                };
                _visible.Add(key, toast);
                toast.Disposed += (_, __) =>
                {
                    if (_visible.TryGetValue(key, out var current) && ReferenceEquals(current, toast)) _visible.Remove(key);
                    if (!_disposed) { PumpSuccesses(); PerformLayout(); }
                };
                ToastContainer.Visible = true;
                ToastContainer.ShowToast(toast);
            }
        }

        private void ClearPresentation()
        {
            _pending.Clear();
            _inactiveSuccess = null;
            _visible.Clear();
            foreach (var toast in ToastContainer.Controls.OfType<BootstrapToast>()) toast.Text = string.Empty;
            ToastContainer.DismissAll();
            ToastContainer.Visible = false;
            ClearActionablePresentation();
        }

        private static string FormatTitle(Notice notice) => "Thông báo · " + notice.OccurredUtc.ToLocalTime().ToString("HH:mm:ss");

        private void ClearActionablePresentation()
        {
            _alertContext = null;
            Alert.Text = string.Empty;
            Alert.Visible = false;
            SetAttention(false);
        }

        private void SetAttention(bool value)
        {
            if (HasAttention == value) return;
            HasAttention = value;
            AttentionChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                lock (_sync) { _disposed = true; _incoming.Clear(); }
                _scope.Published -= OnPublished;
                _scope.Invalidated -= OnInvalidated;
                _scope.ActionableCleared -= OnActionableCleared;
                _scope.Dispose();
                _pending.Clear();
                _inactiveSuccess = null;
                _visible.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
