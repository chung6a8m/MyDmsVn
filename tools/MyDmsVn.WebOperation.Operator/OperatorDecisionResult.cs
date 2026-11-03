using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;

namespace MyDmsVn.WebOperation.Operator;

public enum OperatorDecisionOutcome { Preview, Completed, Unknown }
public enum OperatorDecisionUnknownReason { None, CommitOrReadback, PreCommitCleanupUnverified }

public sealed class OperatorDecisionResult
{
    private OperatorDecisionResult(OperatorDecisionOutcome outcome, WebOperationInspection? inspection,
        OperatorDecisionUnknownReason unknownReason = OperatorDecisionUnknownReason.None)
    { Outcome = outcome; Inspection = inspection; UnknownReason = unknownReason; }
    public OperatorDecisionOutcome Outcome { get; }
    public WebOperationInspection? Inspection { get; }
    public OperatorDecisionUnknownReason UnknownReason { get; }
    public static OperatorDecisionResult Preview(WebOperationInspection inspection) => new OperatorDecisionResult(OperatorDecisionOutcome.Preview, inspection);
    public static OperatorDecisionResult Completed(WebOperationInspection inspection) => new OperatorDecisionResult(OperatorDecisionOutcome.Completed, inspection);
    public static OperatorDecisionResult Unknown(OperatorDecisionUnknownReason reason = OperatorDecisionUnknownReason.CommitOrReadback) =>
        new OperatorDecisionResult(OperatorDecisionOutcome.Unknown, null, reason);
}

public interface IOperatorDecisionClient
{
    Task<OperatorDecisionResult> DryRunAsync(RecordNotExecutedOptions options, CancellationToken cancellationToken);
    Task<OperatorDecisionResult> ExecuteAsync(RecordNotExecutedOptions options, CancellationToken cancellationToken);
}
