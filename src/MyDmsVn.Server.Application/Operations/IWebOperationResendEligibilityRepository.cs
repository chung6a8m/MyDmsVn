using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Operations;

// Read-only WEB6 admission. The caller owns an explicit transaction through
// serialization of the protected response; receipt possession is never authority.
public interface IWebOperationResendEligibilityRepository
{
    Task AuthorizeReceiptAsync(int actorUserId, Guid sessionId, Guid deploymentEpoch,
        WebOperationKind kind, CancellationToken cancellationToken);

    Task<WebOperationResolution?> ReadEligibilityAsync(int actorUserId, Guid sessionId,
        Guid deploymentEpoch, Guid operationId, WebOperationKind expectedKind,
        long? expectedTargetId, byte[] expectedFingerprint, CancellationToken cancellationToken);
}
