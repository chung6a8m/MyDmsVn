using System;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalDesktopSession : IDesktopSession
    {
        private readonly object _sync = new object();
        private CurrentUserDto? _currentUser;
        private long _authenticationGeneration;
        private DesktopSessionState _state = DesktopSessionState.Unauthenticated;

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
