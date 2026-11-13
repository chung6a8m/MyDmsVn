using System;
using System.IO;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal static class Web6CutoverBudgets
{
    internal static readonly TimeSpan LifecyclePhase = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan SqlMutationOrOraclePhase = TimeSpan.FromSeconds(45);
    internal static readonly TimeSpan HostSwitchPhase = TimeSpan.FromSeconds(150);

    internal static readonly TimeSpan FirstScenario = TimeSpan.FromSeconds(420);
    internal static readonly TimeSpan SecondScenario = TimeSpan.FromSeconds(420);
    internal static readonly TimeSpan ThirdScenario = TimeSpan.FromSeconds(690);
    internal static readonly TimeSpan FirstResetReserve = TimeSpan.FromSeconds(180);
    internal static readonly TimeSpan SecondResetReserve = TimeSpan.FromSeconds(180);
    internal static readonly TimeSpan ThirdResetReserve = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan BrowserPack = TimeSpan.FromSeconds(1560);
    internal static readonly TimeSpan Controller = TimeSpan.FromSeconds(1620);
    internal static readonly TimeSpan ControllerJoin = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan AggregateParent = TimeSpan.FromSeconds(3300);
    internal static readonly TimeSpan SelectorGate = TimeSpan.FromSeconds(3600);
    internal static readonly TimeSpan SelectorProcessMaximum = TimeSpan.FromSeconds(3660);
    internal static readonly TimeSpan ResponseReserve = TimeSpan.FromSeconds(5);

    internal static TimeSpan ActionCap(string action) => action switch
    {
        "oldClientNewHostOn" or "oldClientNewHostOff" or "newClientOldHostOn" or "newClientOldHostOff" or
            "restoreEpoch" or "resetCutover" => TimeSpan.FromSeconds(180),
        "sameEpochRestart" => TimeSpan.FromSeconds(210),
        "committedUnavailable" => TimeSpan.FromSeconds(60),
        "revokeGrantOn" or "revokeGrantOff" or "verifyNoCutoverMutation" => TimeSpan.FromSeconds(45),
        _ => throw new InvalidDataException("Invalid WEB6 cutover control frame."),
    };

    internal static TimeSpan PhaseAllowance(TimeSpan phaseCap, TimeSpan workRemaining) =>
        phaseCap <= workRemaining ? phaseCap : workRemaining;
}
