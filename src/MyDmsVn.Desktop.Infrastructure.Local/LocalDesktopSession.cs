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
            SetCurrentUser(null);
        }

        internal void SignIn(CurrentUserDto currentUser)
        {
            SetCurrentUser(currentUser ?? throw new ArgumentNullException(nameof(currentUser)));
        }

        private void SetCurrentUser(CurrentUserDto? currentUser)
        {
            lock (_sync)
            {
                _currentUser = currentUser;
            }

            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
