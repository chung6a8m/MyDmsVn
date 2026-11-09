using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;

namespace MyDmsVn.WebOperation.Operator;

public interface IOperatorTargetOptions
{
    string EnvironmentName { get; }
    string DatabaseName { get; }
    int TimeoutSeconds { get; }
}

public interface IOperatorInspectionClient
{
    Task<WebOperationInspection> InspectAsync(InspectOptions options, CancellationToken cancellationToken);
}
