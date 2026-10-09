using System;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalDesktopSession : IDesktopSession, ICurrentUserAccessor
    {
        private readonly object _sync = new object();
        private CurrentUserDto? _currentUser;
        private long _authenticationGeneration;

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

        CurrentUser ICurrentUserAccessor.Current
        {
            get
            {
                var user = CurrentUser;
                return user == null
                    ? global::MyDmsVn.Server.Application.Identity.CurrentUser.Anonymous
                    : global::MyDmsVn.Server.Application.Identity.CurrentUser.Authenticated(
                        user.UserId,
                        user.Username,
                        user.DisplayName,
                        true);
            }
        }

        public void SignOut()
        {
            lock (_sync)
            {
                _authenticationGeneration++;
                _currentUser = null;
            }

            SessionChanged?.Invoke(this, EventArgs.Empty);
        }

        internal long BeginAuthentication()
        {
            long authenticationGeneration;
            lock (_sync)
            {
                _authenticationGeneration++;
                authenticationGeneration = _authenticationGeneration;
            }

            SessionChanged?.Invoke(this, EventArgs.Empty);
            return authenticationGeneration;
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

                _currentUser = currentUser;
            }

            SessionChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}
