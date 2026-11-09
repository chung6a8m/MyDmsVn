using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.WebOperation.Operator;

public enum OperatorCompactionOutcome
{
    Unknown,
    Eligible,
    NoChange,
    Completed,
    AuditDeliveryFailed
}

public sealed class OperatorCompactionResult
{
    public OperatorCompactionResult(OperatorCompactionOutcome outcome) => Outcome = outcome;

    public OperatorCompactionOutcome Outcome { get; }
}

public interface IOperatorCompactionClient
{
    Task<OperatorCompactionResult> ExecuteAsync(CompactOptions options, CancellationToken cancellationToken);
    Task<OperatorCompactionResult> PreviewAsync(CompactOptions options, CancellationToken cancellationToken);
}
