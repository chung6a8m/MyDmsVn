using System;

namespace MyDmsVn.Server.Application.Identity;

public sealed class WebAuthSessionOptions
{
    public WebAuthSessionOptions(int idleLifetimeDays = 7, int absoluteLifetimeDays = 30)
    {
        if (idleLifetimeDays < 1 || idleLifetimeDays > 7)
            throw new ArgumentOutOfRangeException(nameof(idleLifetimeDays));
        if (absoluteLifetimeDays < 1 || absoluteLifetimeDays > 30 || idleLifetimeDays > absoluteLifetimeDays)
            throw new ArgumentOutOfRangeException(nameof(absoluteLifetimeDays));
        IdleLifetimeDays = idleLifetimeDays;
        AbsoluteLifetimeDays = absoluteLifetimeDays;
    }

    public int IdleLifetimeDays { get; }
    public int AbsoluteLifetimeDays { get; }
}
