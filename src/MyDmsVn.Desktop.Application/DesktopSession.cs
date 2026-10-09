using System;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public interface IDesktopSession
    {
        event EventHandler? SessionChanged;

        bool IsAuthenticated { get; }

        CurrentUserDto? CurrentUser { get; }

        long Version { get; }

        void SignOut();
    }
}
