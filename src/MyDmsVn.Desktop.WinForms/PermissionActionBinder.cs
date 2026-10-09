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
        private bool _disposed;

        public PermissionActionBinder(
            IPermissionApiClient permissionApiClient,
            IDesktopSession session)
        {
            _permissionApiClient = permissionApiClient ??
                throw new ArgumentNullException(nameof(permissionApiClient));
            _session = session ?? throw new ArgumentNullException(nameof(session));
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
            SetEnabled(action, false);

            var sessionVersion = _session.Version;
            if (!_session.IsAuthenticated)
            {
                return;
            }

            var response = await _permissionApiClient
                .CheckAsync(permissionKey, cancellationToken);

            if (IsDisposed)
            {
                return;
            }

            var sessionIsCurrent = _session.IsAuthenticated &&
                _session.Version == sessionVersion;
            SetEnabled(
                action,
                sessionIsCurrent && response.IsSuccess && response.Data!.IsAllowed);
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
                SetEnabled(action, false);
            }
        }

        private bool IsDisposed
        {
            get
            {
                lock (_sync)
                {
                    return _disposed;
                }
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
                SetEnabled(action, false);
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

        private static void SetEnabled(Control action, bool enabled)
        {
            if (action.IsDisposed || action.Disposing)
            {
                return;
            }

            if (action.IsHandleCreated && action.InvokeRequired)
            {
                try
                {
                    action.BeginInvoke((Action)(() => SetEnabled(action, enabled)));
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            action.Enabled = enabled;
        }
    }
}
