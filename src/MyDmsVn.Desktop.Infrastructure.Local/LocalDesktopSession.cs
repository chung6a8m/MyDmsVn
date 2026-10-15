using System;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalDesktopSession : IDesktopSession, IDesktopSessionActivity
    {
        private readonly object _sync = new object();
        private readonly IMonotonicClock _clock;
        private readonly DesktopSessionOptions _options;
        private CurrentUserDto? _currentUser;
        private long _authenticationGeneration;
        private DesktopSessionState _state = DesktopSessionState.Unauthenticated;
        private TimeSpan _lastUserActivity;

        public LocalDesktopSession(
            IMonotonicClock clock,
            DesktopSessionOptions options)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public event EventHandler? SessionChanged;

        public bool IsAuthenticated
        {
            get
            {
                lock (_sync)
                {
                    return _currentUser != null;
                }
            }
        }

        public CurrentUserDto? CurrentUser
        {
            get
            {
                lock (_sync)
                {
                    return _currentUser;
                }
            }
        }

        public DesktopSessionState State
        {
            get
            {
                lock (_sync)
                {
                    return _state;
                }
            }
        }

        public long Version
        {
            get
            {
                lock (_sync)
                {
                    return _authenticationGeneration;
                }
            }
        }

        internal bool TryCaptureActor(out CurrentUser actor, out long generation)
        {
            bool expired;
            bool captured;
            lock (_sync)
            {
                expired = ExpireIfIdleUnsafe();
                generation = _authenticationGeneration;
                if (_state != DesktopSessionState.Authenticated || _currentUser == null)
                {
                    actor = global::MyDmsVn.Server.Application.Identity.CurrentUser.Anonymous;
                    captured = false;
                }
                else
                {
                    actor = global::MyDmsVn.Server.Application.Identity.CurrentUser.Authenticated(
                        _currentUser.UserId,
                        _currentUser.Username,
                        _currentUser.DisplayName,
                        true);
                    captured = true;
                }
            }

            if (expired)
            {
                SessionChanged?.Invoke(this, EventArgs.Empty);
            }

            return captured;
        }

        internal bool IsCurrent(long generation)
        {
            bool expired;
            bool current;
            lock (_sync)
            {
                expired = ExpireIfIdleUnsafe();
                current = _state == DesktopSessionState.Authenticated &&
                    _authenticationGeneration == generation;
            }

            if (expired)
            {
                SessionChanged?.Invoke(this, EventArgs.Empty);
            }

            return current;
        }

        public void SignOut()
        {
            lock (_sync)
            {
                _state = DesktopSessionState.LoggingOut;
                _authenticationGeneration++;
                _currentUser = null;
                _state = DesktopSessionState.Unauthenticated;
            }

            SessionChanged?.Invoke(this, EventArgs.Empty);
        }

        internal bool RequireReauthentication(long expectedGeneration)
        {
            lock (_sync)
            {
                if (_state != DesktopSessionState.Authenticated ||
                    _authenticationGeneration != expectedGeneration)
                {
                    return false;
                }

                _authenticationGeneration++;
                _currentUser = null;
                _state = DesktopSessionState.Expired;
            }

            SessionChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        internal bool CancelAuthentication(long expectedGeneration)
        {
            lock (_sync)
            {
                if (_state != DesktopSessionState.Authenticating ||
                    _authenticationGeneration != expectedGeneration)
                {
                    return false;
                }

                _authenticationGeneration++;
                _currentUser = null;
                _state = DesktopSessionState.Unauthenticated;
            }

            SessionChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void RecordUserActivity()
        {
            bool expired;
            lock (_sync)
            {
                expired = ExpireIfIdleUnsafe();
                if (!expired && _state == DesktopSessionState.Authenticated)
                {
                    _lastUserActivity = _clock.Elapsed;
                }
            }

            if (expired)
            {
                SessionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool ExpireIfIdle()
        {
            bool expired;
            lock (_sync)
            {
                expired = ExpireIfIdleUnsafe();
            }

            if (expired)
            {
                SessionChanged?.Invoke(this, EventArgs.Empty);
            }

            return expired;
        }

        internal bool TryBeginAuthentication(out long authenticationGeneration)
        {
            lock (_sync)
            {
                if (_state == DesktopSessionState.Authenticated ||
                    _state == DesktopSessionState.LoggingOut)
                {
                    authenticationGeneration = _authenticationGeneration;
                    return false;
                }

                _authenticationGeneration++;
                _currentUser = null;
                _state = DesktopSessionState.Authenticating;
                authenticationGeneration = _authenticationGeneration;
            }

            SessionChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        internal bool TrySignIn(
            CurrentUserDto currentUser,
            long authenticationGeneration,
            System.Threading.CancellationToken cancellationToken)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            lock (_sync)
            {
                if (cancellationToken.IsCancellationRequested ||
                    _authenticationGeneration != authenticationGeneration)
                {
                    return false;
                }

                _authenticationGeneration++;
                _currentUser = currentUser;
                _state = DesktopSessionState.Authenticated;
                _lastUserActivity = _clock.Elapsed;
            }

            SessionChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        internal bool IsAuthenticationCurrent(long authenticationGeneration)
        {
            lock (_sync)
            {
                return _state == DesktopSessionState.Authenticating &&
                    _authenticationGeneration == authenticationGeneration;
            }
        }

        private bool ExpireIfIdleUnsafe()
        {
            if (_state != DesktopSessionState.Authenticated ||
                _clock.Elapsed - _lastUserActivity < _options.IdleTimeout)
            {
                return false;
            }

            _authenticationGeneration++;
            _currentUser = null;
            _state = DesktopSessionState.Expired;
            return true;
        }
    }
}
