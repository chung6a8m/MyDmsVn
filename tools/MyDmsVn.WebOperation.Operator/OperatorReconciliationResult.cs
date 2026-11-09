using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Infrastructure.Operations;

namespace MyDmsVn.WebOperation.Operator;

public sealed class OperatorReconciliationResult
{
    public OperatorReconciliationResult(WebOperationReconciliationOutcome outcome) => Outcome = outcome;
    public WebOperationReconciliationOutcome Outcome { get; }
}

public interface IOperatorReconciliationClient
{
    Task<OperatorReconciliationResult> ReconcileAsync(ReconcileCommittedOptions options, CancellationToken token);
}
