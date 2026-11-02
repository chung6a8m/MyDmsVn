using System;

namespace MyDmsVn.Server.Application.Operations;

// Operator-only metadata, never a browser certificate, original result or editing baseline.
public sealed class WebOperationInspection
{
    public WebOperationInspection(Guid deploymentEpoch, int actorUserId, Guid operationId, WebOperationKind? kind,
        long? requestTargetId, string disposition, bool originalResultRetained, bool originalVersionRetained,
        bool originalLineIdsRetained, WebOperationTerminalDecision? terminal)
    {
        DeploymentEpoch = deploymentEpoch; ActorUserId = actorUserId; OperationId = operationId;
        Kind = kind; RequestTargetId = requestTargetId; Disposition = disposition;
        OriginalResultRetained = originalResultRetained; OriginalVersionRetained = originalVersionRetained;
        OriginalLineIdsRetained = originalLineIdsRetained; Terminal = terminal;
    }
    public Guid DeploymentEpoch { get; }
    public int ActorUserId { get; }
    public Guid OperationId { get; }
    public WebOperationKind? Kind { get; }
    public long? RequestTargetId { get; }
    public string Disposition { get; }
    public bool OriginalResultRetained { get; }
    public bool OriginalVersionRetained { get; }
    public bool OriginalLineIdsRetained { get; }
    public WebOperationTerminalDecision? Terminal { get; }
}
