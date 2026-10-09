using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    public sealed class PermissionActionBinder : IDisposable
    {
        private readonly object _sync = new object();
        private readonly HashSet<Control> _actions = new HashSet<Control>();
        private readonly IPermissionApiClient _permissionApiClient;
        private readonly IDesktopSession _session;
        private readonly SynchronizationContext _uiContext;
        private readonly int _uiThreadId;
        private bool _disposed;

        public PermissionActionBinder(
            IPermissionApiClient permissionApiClient,
            IDesktopSession session)
        {
            _permissionApiClient = permissionApiClient ??
                throw new ArgumentNullException(nameof(permissionApiClient));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _uiContext = SynchronizationContext.Current ??
                new WindowsFormsSynchronizationContext();
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
            _session.SessionChanged += OnSessionChanged;
        }

        public async Task ApplyAsync(
            Control action,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                throw new ArgumentException("A permission key is required.", nameof(permissionKey));
            }

            Register(action);
            SetEnabled(action, false, null);

            var sessionVersion = _session.Version;
            if (!_session.IsAuthenticated)
            {
                return;
            }

            var response = await _permissionApiClient
                .CheckAsync(permissionKey, cancellationToken);

            SetEnabled(
                action,
                response.IsSuccess && response.Data!.IsAllowed,
                sessionVersion);
        }

        public void Dispose()
        {
            Control[] actions;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                actions = new Control[_actions.Count];
                _actions.CopyTo(actions);
                _actions.Clear();
            }

            _session.SessionChanged -= OnSessionChanged;
            foreach (var action in actions)
            {
                action.Disposed -= OnActionDisposed;
                SetEnabled(action, false, null);
            }
        }

        private void Register(Control action)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(PermissionActionBinder));
                }

                if (_actions.Add(action))
                {
                    action.Disposed += OnActionDisposed;
                }
            }
        }

        private void OnSessionChanged(object? sender, EventArgs eventArgs)
        {
            Control[] actions;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                actions = new Control[_actions.Count];
                _actions.CopyTo(actions);
            }

            foreach (var action in actions)
            {
                SetEnabled(action, false, null);
            }
        }

        private void OnActionDisposed(object? sender, EventArgs eventArgs)
        {
            var action = sender as Control;
            if (action == null)
            {
                return;
            }

            lock (_sync)
            {
                _actions.Remove(action);
            }

            action.Disposed -= OnActionDisposed;
        }

        private void SetEnabled(
            Control action,
            bool enabled,
            long? requiredSessionVersion)
        {
            if (action.IsDisposed || action.Disposing)
            {
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId == _uiThreadId)
            {
                ApplyEnabled(action, enabled, requiredSessionVersion);
                return;
            }

            _uiContext.Post(
                _ => ApplyEnabled(action, enabled, requiredSessionVersion),
                null);
        }

        private void ApplyEnabled(
            Control action,
            bool enabled,
            long? requiredSessionVersion)
        {
            if (action.IsDisposed || action.Disposing)
            {
                return;
            }

            action.Enabled = enabled && CanEnable(action, requiredSessionVersion);
        }

        private bool CanEnable(Control action, long? requiredSessionVersion)
        {
            lock (_sync)
            {
                return !_disposed &&
                    _actions.Contains(action) &&
                    requiredSessionVersion.HasValue &&
                    _session.IsAuthenticated &&
                    _session.Version == requiredSessionVersion.Value;
            }
        }
    }
}
