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
        private readonly Dictionary<Control, long> _actionVersions =
            new Dictionary<Control, long>();
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

            var requestVersion = Register(action);
            SetEnabled(action, false, null, requestVersion);

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
                sessionVersion,
                requestVersion);
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
                actions = new Control[_actionVersions.Count];
                _actionVersions.Keys.CopyTo(actions, 0);
                _actionVersions.Clear();
            }

            _session.SessionChanged -= OnSessionChanged;
            foreach (var action in actions)
            {
                action.Disposed -= OnActionDisposed;
                SetEnabled(action, false, null, null);
            }
        }

        private long Register(Control action)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(PermissionActionBinder));
                }

                if (!_actionVersions.TryGetValue(action, out var requestVersion))
                {
                    action.Disposed += OnActionDisposed;
                }

                requestVersion++;
                _actionVersions[action] = requestVersion;
                return requestVersion;
            }
        }

        private void OnSessionChanged(object? sender, EventArgs eventArgs)
        {
            Control[] actions;
            long[] requestVersions;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                actions = new Control[_actionVersions.Count];
                requestVersions = new long[_actionVersions.Count];
                _actionVersions.Keys.CopyTo(actions, 0);
                for (var index = 0; index < actions.Length; index++)
                {
                    var requestVersion = _actionVersions[actions[index]] + 1;
                    _actionVersions[actions[index]] = requestVersion;
                    requestVersions[index] = requestVersion;
                }
            }

            for (var index = 0; index < actions.Length; index++)
            {
                SetEnabled(actions[index], false, null, requestVersions[index]);
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
                _actionVersions.Remove(action);
            }

            action.Disposed -= OnActionDisposed;
        }

        private void SetEnabled(
            Control action,
            bool enabled,
            long? requiredSessionVersion,
            long? requiredRequestVersion)
        {
            if (action.IsDisposed || action.Disposing)
            {
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId == _uiThreadId)
            {
                ApplyEnabled(
                    action,
                    enabled,
                    requiredSessionVersion,
                    requiredRequestVersion);
                return;
            }

            _uiContext.Post(
                _ => ApplyEnabled(
                    action,
                    enabled,
                    requiredSessionVersion,
                    requiredRequestVersion),
                null);
        }

        private void ApplyEnabled(
            Control action,
            bool enabled,
            long? requiredSessionVersion,
            long? requiredRequestVersion)
        {
            if (action.IsDisposed || action.Disposing)
            {
                return;
            }

            if (TryGetEnabledState(
                action,
                enabled,
                requiredSessionVersion,
                requiredRequestVersion,
                out var enabledState))
            {
                action.Enabled = enabledState;
            }
        }

        private bool TryGetEnabledState(
            Control action,
            bool enabled,
            long? requiredSessionVersion,
            long? requiredRequestVersion,
            out bool enabledState)
        {
            lock (_sync)
            {
                if (requiredRequestVersion.HasValue &&
                    (!_actionVersions.TryGetValue(action, out var currentRequestVersion) ||
                        currentRequestVersion != requiredRequestVersion.Value))
                {
                    enabledState = false;
                    return false;
                }

                enabledState = enabled &&
                    !_disposed &&
                    _actionVersions.ContainsKey(action) &&
                    requiredSessionVersion.HasValue &&
                    _session.IsAuthenticated &&
                    _session.Version == requiredSessionVersion.Value;
                return true;
            }
        }
    }
}
