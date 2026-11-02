using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Operations;

public sealed class SqlWebOperationReadRepository : IWebOperationReadRepository
{
    private readonly ISqlExecutionContext _context;
    private readonly ICurrentUserAccessor _current;
    private readonly IPermissionRegistry _registry;
    private readonly SuperuserIdentityPolicy? _superuser;

    public SqlWebOperationReadRepository(ISqlExecutionContext context, ICurrentUserAccessor current,
        IPermissionRegistry registry, SuperuserIdentityPolicy? superuser = null)
    {
        _context = context;
        _current = current;
        _registry = registry;
        _superuser = superuser;
    }

    public async Task<Guid> ReadContextAsync(int actorUserId, Guid sessionId, CancellationToken cancellationToken)
    {
        RequireTransaction();
        SqlWebOperationJournal.RequireCurrentActor(_current, actorUserId);
        Guid epoch;
        // Keep the same epoch -> Users -> session ordering as mutation admission.
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "SELECT DeploymentEpoch FROM dbo.WebDeploymentState WITH (HOLDLOCK) WHERE Id=1;"))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (!(value is Guid validEpoch) || validEpoch == Guid.Empty)
                throw new WebOperationException("Operation.Unavailable");
            epoch = validEpoch;
        }
        await SqlWebOperationJournal.AuthorizeSessionAsync(_context, actorUserId, sessionId, cancellationToken).ConfigureAwait(false);
        return epoch;
    }

    public async Task<WebOperationStatus?> ReadStatusAsync(int actorUserId, Guid sessionId, Guid deploymentEpoch,
        Guid operationId, byte[] expectedFingerprint, CancellationToken cancellationToken)
    {
        RequireTransaction();
        SqlWebOperationJournal.RequireCurrentActor(_current, actorUserId);
        if (operationId == Guid.Empty || expectedFingerprint == null || expectedFingerprint.Length != 32)
            throw new ArgumentException("An operation identity and SHA256 fingerprint are required.");
        var expected = (byte[])expectedFingerprint.Clone();
        await SqlWebOperationJournal.FenceAsync(_context, deploymentEpoch, actorUserId, operationId, cancellationToken).ConfigureAwait(false);
        await SqlWebOperationJournal.AuthorizeSessionAsync(_context, actorUserId, sessionId, cancellationToken).ConfigureAwait(false);

        Guid storedEpoch;
        WebOperationKind kind;
        long? requestTargetId;
        byte[] fingerprint;
        long? resultDocumentId;
        DateTime completedAtUtc;
        string state;
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "SELECT DeploymentEpoch,OperationKind,RequestTargetId,RequestFingerprint,ResultDocumentId,CompletedAtUtc,Disposition,CASE WHEN ResultExpiresAtUtc<=SYSUTCDATETIME() THEN 1 ELSE 0 END,ContractVersion,CodecVersion FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation;",
            SqlRead.P("@actor", SqlDbType.Int, actorUserId), SqlRead.P("@operation", SqlDbType.UniqueIdentifier, operationId)))
        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            // Other actors' rows are indistinguishable from absence. Neither is rollback evidence.
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;
            storedEpoch = reader.GetGuid(0);
            kind = (WebOperationKind)reader.GetByte(1);
            requestTargetId = reader.IsDBNull(2) ? (long?)null : reader.GetInt64(2);
            fingerprint = (byte[])reader[3];
            resultDocumentId = reader.IsDBNull(4) ? (long?)null : reader.GetInt64(4);
            completedAtUtc = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc);
            state = reader.GetString(6) == "EXPIRED" || reader.GetInt32(7) == 1
                ? "RESULT_EXPIRED"
                : reader.GetString(6) == "COMMITTED" && !reader.IsDBNull(4) && reader.GetInt16(8) == 1 && reader.GetInt16(9) == 1
                    ? "COMMITTED" : "RESULT_UNAVAILABLE";
        }

        // Permission derives from the owned immutable row, never from caller-supplied kind or Read.
        // Delay loading outcome bytes until current authority and immutable request identity match.
        await SqlWebOperationJournal.AuthorizeOperationAsync(_context, _current, _registry, _superuser, actorUserId, kind, cancellationToken).ConfigureAwait(false);
        if (storedEpoch != deploymentEpoch)
            throw new WebOperationException("Deployment.EpochMismatch");
        if (!fingerprint.SequenceEqual(expected))
            throw new WebOperationException("Idempotency.FingerprintMismatch");

        byte[]? bytes = null;
        if (state == "COMMITTED")
        {
            using var command = SqlRead.Command(_context.Connection, _context.Transaction,
                "SELECT ResultBytes FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation;",
                SqlRead.P("@actor", SqlDbType.Int, actorUserId), SqlRead.P("@operation", SqlDbType.UniqueIdentifier, operationId));
            bytes = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as byte[];
            if (bytes == null || bytes.Length == 0 || bytes.Length > 1048576)
            {
                state = "RESULT_UNAVAILABLE";
                bytes = null;
            }
        }
        return new WebOperationStatus(operationId, storedEpoch, kind, requestTargetId, fingerprint,
            resultDocumentId, completedAtUtc, state, bytes);
    }

    private void RequireTransaction()
    {
        if (_context.Transaction == null)
            throw new InvalidOperationException("Operation reads require an explicit caller-owned transaction.");
    }
}
