using System;
using System.Collections.Generic;
using System.Linq;

namespace MyDmsVn.Desktop.Application.Security;

public enum SecuritySaveCompletion { NoChanges, Complete, Partial, NoneConfirmed }
public enum SecuritySaveStopReason { None, Rejected, Conflict, Forbidden, CanceledBeforeDispatch, OutcomeUnknown }
public enum SecurityRefreshOutcome { NotAttempted, Complete, Failed, Forbidden, SessionChanged }
/// <summary>Confirmed items retain their independent commits even when a later item stops the sequence.</summary>
public sealed class SecuritySaveOutcome
{
    public SecuritySaveOutcome(SecuritySaveCompletion completion, SecuritySaveStopReason stopReason, SecurityRefreshOutcome refreshOutcome, IEnumerable<string> confirmedKeys, IEnumerable<string> rejectedKeys, IEnumerable<string> uncertainKeys, IEnumerable<string> unattemptedKeys, long? lastConfirmedRevision)
    {
        Completion = completion;
        StopReason = stopReason;
        RefreshOutcome = refreshOutcome;
        ConfirmedKeys = Array.AsReadOnly(confirmedKeys.ToArray());
        RejectedKeys = Array.AsReadOnly(rejectedKeys.ToArray());
        UncertainKeys = Array.AsReadOnly(uncertainKeys.ToArray());
        UnattemptedKeys = Array.AsReadOnly(unattemptedKeys.ToArray());
        LastConfirmedRevision = lastConfirmedRevision;
    }
    public SecuritySaveCompletion Completion { get; }
    public SecuritySaveStopReason StopReason { get; }
    public SecurityRefreshOutcome RefreshOutcome { get; }
    public IReadOnlyList<string> ConfirmedKeys { get; }
    public IReadOnlyList<string> RejectedKeys { get; }
    public IReadOnlyList<string> UncertainKeys { get; }
    public IReadOnlyList<string> UnattemptedKeys { get; }
    public long? LastConfirmedRevision { get; }
}
