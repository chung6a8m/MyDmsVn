using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Operations;

public sealed class SqlWebOperationResolutionRepository : IWebOperationResolutionRepository
{
    private readonly ISqlExecutionContext _context;
    private readonly SqlWebOperationReadRepository _original;
    public SqlWebOperationResolutionRepository(ISqlExecutionContext context, ICurrentUserAccessor current,
        IPermissionRegistry registry, SuperuserIdentityPolicy? superuser = null)
    { _context = context; _original = new SqlWebOperationReadRepository(context, current, registry, superuser); }

    public async Task<WebOperationResolution?> ReadResolutionAsync(int actorUserId, Guid sessionId, Guid deploymentEpoch,
        Guid operationId, byte[] expectedFingerprint, CancellationToken cancellationToken)
    {
        // Performs original session, stored-kind Write/Post, epoch and fingerprint checks under
        // the same key fence before any audit metadata is loaded. Ordinary Read is irrelevant.
        var status = await _original.ReadStatusAsync(actorUserId, sessionId, deploymentEpoch, operationId,
            expectedFingerprint, cancellationToken).ConfigureAwait(false);
        if (status == null) return null;
        using var command = SqlRead.Command(_context.Connection, _context.Transaction, @"
SELECT j.Disposition,a.DecisionId,a.CaseId,a.DecidedAtUtc
FROM dbo.WebOperationJournal j LEFT JOIN dbo.WebOperationTerminalAudit a
ON j.Disposition='CANCELLED' AND a.ActorUserId=j.ActorUserId AND a.OperationId=j.OperationId
AND a.DeploymentEpoch=j.DeploymentEpoch AND a.OperationKind=j.OperationKind
AND ISNULL(a.RequestTargetId,0)=ISNULL(j.RequestTargetId,0) AND a.RequestFingerprint=j.RequestFingerprint
AND a.DecidedAtUtc=j.CreatedAtUtc AND a.DecidedAtUtc=j.CompletedAtUtc
AND j.ContractVersion=1 AND j.CodecVersion=1
WHERE j.ActorUserId=@actor AND j.OperationId=@operation;",
            SqlRead.P("@actor", SqlDbType.Int, actorUserId), SqlRead.P("@operation", SqlDbType.UniqueIdentifier, operationId));
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new WebOperationException("Operation.Unavailable");
        if (reader.GetString(0) != "CANCELLED") return new WebOperationResolution(status);
        var terminal = reader.IsDBNull(1) ? null : new WebOperationTerminalDecision(reader.GetGuid(1), reader.GetString(2),
            DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc));
        var terminalStatus = new WebOperationStatus(status.OperationId, status.DeploymentEpoch, status.Kind,
            status.RequestTargetId, status.RequestFingerprint, null, status.CompletedAtUtc,
            terminal == null ? "INDETERMINATE" : "NOT_EXECUTED", null);
        return new WebOperationResolution(terminalStatus, terminal);
    }
}
