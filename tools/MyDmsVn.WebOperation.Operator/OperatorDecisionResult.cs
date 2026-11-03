using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;

namespace MyDmsVn.WebOperation.Operator;

public enum OperatorDecisionOutcome { Preview, Completed, Unknown }

public sealed class OperatorDecisionResult
{
    private OperatorDecisionResult(OperatorDecisionOutcome outcome, WebOperationInspection? inspection) { Outcome = outcome; Inspection = inspection; }
    public OperatorDecisionOutcome Outcome { get; }
    public WebOperationInspection? Inspection { get; }
    public static OperatorDecisionResult Preview(WebOperationInspection inspection) => new OperatorDecisionResult(OperatorDecisionOutcome.Preview, inspection);
    public static OperatorDecisionResult Completed(WebOperationInspection inspection) => new OperatorDecisionResult(OperatorDecisionOutcome.Completed, inspection);
    public static OperatorDecisionResult Unknown() => new OperatorDecisionResult(OperatorDecisionOutcome.Unknown, null);
}

public interface IOperatorDecisionClient
{
    Task<OperatorDecisionResult> DryRunAsync(RecordNotExecutedOptions options, CancellationToken cancellationToken);
    Task<OperatorDecisionResult> ExecuteAsync(RecordNotExecutedOptions options, CancellationToken cancellationToken);
}
