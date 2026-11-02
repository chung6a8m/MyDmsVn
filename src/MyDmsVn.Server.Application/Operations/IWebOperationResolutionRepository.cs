using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Operations;

// A read only extension. The caller retains its explicit transaction through serialization.
public interface IWebOperationResolutionRepository
{
    Task<WebOperationResolution?> ReadResolutionAsync(int actorUserId, Guid sessionId, Guid deploymentEpoch,
        Guid operationId, byte[] expectedFingerprint, CancellationToken cancellationToken);
}

public sealed class WebOperationResolution
{
    public WebOperationResolution(WebOperationStatus status, WebOperationTerminalDecision? terminal = null)
    { Status = status; Terminal = terminal; }
    public WebOperationStatus Status { get; }
    public WebOperationTerminalDecision? Terminal { get; }
}

public sealed class WebOperationTerminalDecision
{
    public WebOperationTerminalDecision(Guid decisionId, string caseId, DateTime decidedAtUtc)
    { DecisionId = decisionId; CaseId = caseId; DecidedAtUtc = decidedAtUtc; }
    public Guid DecisionId { get; }
    public string CaseId { get; }
    public DateTime DecidedAtUtc { get; }
}
