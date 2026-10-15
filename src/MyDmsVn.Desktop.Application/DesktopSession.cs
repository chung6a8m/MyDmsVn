using System;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public enum DesktopSessionState
    {
        Unauthenticated,
        Authenticating,
        Authenticated,
        Expired,
        LoggingOut,
    }

    public interface IDesktopSession
    {
        event EventHandler? SessionChanged;

        bool IsAuthenticated { get; }

        DesktopSessionState State { get; }

        CurrentUserDto? CurrentUser { get; }

        long Version { get; }

        void SignOut();
    }
}
