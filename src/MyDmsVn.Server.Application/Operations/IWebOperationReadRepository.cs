using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Operations;

// The caller owns an explicit transaction through serialization of the protected response.
public interface IWebOperationReadRepository
{
    Task<Guid> ReadContextAsync(int actorUserId, Guid sessionId, CancellationToken cancellationToken);
    Task<WebOperationStatus?> ReadStatusAsync(int actorUserId, Guid sessionId, Guid deploymentEpoch,
        Guid operationId, byte[] expectedFingerprint, CancellationToken cancellationToken);
}

// Internal application data, not a wire response. HTTP derives method/route from the stored kind,
// validates the original result codec and never adds resultDocumentId/completedAtUtc to the envelope.
public sealed class WebOperationStatus
{
    private readonly byte[] _requestFingerprint;
    private readonly byte[]? _resultBytes;

    public WebOperationStatus(Guid operationId, Guid deploymentEpoch, WebOperationKind kind,
        long? requestTargetId, byte[] requestFingerprint, long? resultDocumentId,
        DateTime completedAtUtc, string state, byte[]? resultBytes)
    {
        OperationId = operationId;
        DeploymentEpoch = deploymentEpoch;
        Kind = kind;
        RequestTargetId = requestTargetId;
        _requestFingerprint = (byte[])requestFingerprint.Clone();
        ResultDocumentId = resultDocumentId;
        CompletedAtUtc = completedAtUtc;
        State = state;
        _resultBytes = resultBytes == null ? null : (byte[])resultBytes.Clone();
    }

    public Guid OperationId { get; }
    public Guid DeploymentEpoch { get; }
    public WebOperationKind Kind { get; }
    public long? RequestTargetId { get; }
    public byte[] RequestFingerprint => (byte[])_requestFingerprint.Clone();
    public long? ResultDocumentId { get; }
    public DateTime CompletedAtUtc { get; }
    public string State { get; }
    public byte[]? ResultBytes => _resultBytes == null ? null : (byte[])_resultBytes.Clone();
}
