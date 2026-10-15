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

    public interface IDesktopSessionActivity
    {
        void RecordUserActivity();

        bool ExpireIfIdle();
    }

    public interface IMonotonicClock
    {
        TimeSpan Elapsed { get; }
    }

    public sealed class DesktopSessionOptions
    {
        public DesktopSessionOptions(TimeSpan idleTimeout)
        {
            if (idleTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(idleTimeout));
            }

            IdleTimeout = idleTimeout;
        }

        public TimeSpan IdleTimeout { get; }

        public static DesktopSessionOptions Default { get; } =
            new DesktopSessionOptions(TimeSpan.FromMinutes(30));
    }
}
