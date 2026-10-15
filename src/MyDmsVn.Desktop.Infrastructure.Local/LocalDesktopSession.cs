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
            lock (_sync)
            {
                generation = _authenticationGeneration;
                if (_state != DesktopSessionState.Authenticated || _currentUser == null)
                {
                    actor = global::MyDmsVn.Server.Application.Identity.CurrentUser.Anonymous;
                    return false;
                }

                actor = global::MyDmsVn.Server.Application.Identity.CurrentUser.Authenticated(
                    _currentUser.UserId,
                    _currentUser.Username,
                    _currentUser.DisplayName,
                    true);
                return true;
            }
        }

        internal bool IsCurrent(long generation)
        {
            lock (_sync)
            {
                return _state == DesktopSessionState.Authenticated &&
                    _authenticationGeneration == generation;
            }
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

        public void RecordUserActivity()
        {
            lock (_sync)
            {
                if (_state == DesktopSessionState.Authenticated)
                {
                    _lastUserActivity = _clock.Elapsed;
                }
            }
        }

        public bool ExpireIfIdle()
        {
            lock (_sync)
            {
                if (_state != DesktopSessionState.Authenticated ||
                    _clock.Elapsed - _lastUserActivity < _options.IdleTimeout)
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

        internal bool TrySignIn(CurrentUserDto currentUser, long authenticationGeneration)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            lock (_sync)
            {
                if (_authenticationGeneration != authenticationGeneration)
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
    }
}
