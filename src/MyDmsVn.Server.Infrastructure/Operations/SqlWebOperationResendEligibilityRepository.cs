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

public sealed class SqlWebOperationResendEligibilityRepository : IWebOperationResendEligibilityRepository
{
    private readonly ISqlExecutionContext _context;
    private readonly ICurrentUserAccessor _current;
    private readonly IPermissionRegistry _registry;
    private readonly SuperuserIdentityPolicy? _superuser;
    private readonly SqlWebOperationResolutionRepository _resolution;

    public SqlWebOperationResendEligibilityRepository(ISqlExecutionContext context, ICurrentUserAccessor current,
        IPermissionRegistry registry, SuperuserIdentityPolicy? superuser = null)
    {
        _context = context;
        _current = current;
        _registry = registry;
        _superuser = superuser;
        _resolution = new SqlWebOperationResolutionRepository(context, current, registry, superuser);
    }

    public async Task AuthorizeReceiptAsync(int actorUserId, Guid sessionId, Guid deploymentEpoch,
        WebOperationKind kind, CancellationToken cancellationToken)
    {
        RequireTransaction();
        SqlWebOperationJournal.RequireCurrentActor(_current, actorUserId);
        if (!Enum.IsDefined(typeof(WebOperationKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Guid currentEpoch;
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "SELECT DeploymentEpoch FROM dbo.WebDeploymentState WITH (HOLDLOCK) WHERE Id=1;"))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (!(value is Guid epoch) || epoch == Guid.Empty) throw new WebOperationException("Operation.Unavailable");
            currentEpoch = epoch;
        }
        await SqlWebOperationJournal.AuthorizeSessionAsync(_context, actorUserId, sessionId, cancellationToken).ConfigureAwait(false);
        if (currentEpoch != deploymentEpoch) throw new WebOperationException("Deployment.EpochMismatch");
        await SqlWebOperationJournal.AuthorizeOperationAsync(_context, _current, _registry, _superuser,
            actorUserId, kind, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WebOperationResolution?> ReadEligibilityAsync(int actorUserId, Guid sessionId,
        Guid deploymentEpoch, Guid operationId, WebOperationKind expectedKind, long? expectedTargetId,
        byte[] expectedFingerprint, CancellationToken cancellationToken)
    {
        RequireTransaction();
        ValidateIdentity(operationId, expectedKind, expectedTargetId, expectedFingerprint);
        SqlWebOperationJournal.RequireCurrentActor(_current, actorUserId);
        await SqlWebOperationJournal.FenceAsync(_context, deploymentEpoch, actorUserId, operationId, cancellationToken).ConfigureAwait(false);
        await SqlWebOperationJournal.AuthorizeSessionAsync(_context, actorUserId, sessionId, cancellationToken).ConfigureAwait(false);
        await SqlWebOperationJournal.AuthorizeOperationAsync(_context, _current, _registry, _superuser,
            actorUserId, expectedKind, cancellationToken).ConfigureAwait(false);

        Guid storedEpoch;
        WebOperationKind storedKind;
        long? storedTarget;
        byte[] storedFingerprint;
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "SELECT DeploymentEpoch,OperationKind,RequestTargetId,RequestFingerprint FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation;",
            SqlRead.P("@actor", SqlDbType.Int, actorUserId), SqlRead.P("@operation", SqlDbType.UniqueIdentifier, operationId)))
        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
            storedEpoch = reader.GetGuid(0);
            storedKind = (WebOperationKind)reader.GetByte(1);
            storedTarget = reader.IsDBNull(2) ? (long?)null : reader.GetInt64(2);
            storedFingerprint = (byte[])reader[3];
        }

        if (storedEpoch != deploymentEpoch) throw new WebOperationException("Deployment.EpochMismatch");
        if (storedKind != expectedKind || storedTarget != expectedTargetId)
            throw new WebOperationException("Idempotency.KeyConflict");
        if (!storedFingerprint.SequenceEqual(expectedFingerprint))
            throw new WebOperationException("Idempotency.FingerprintMismatch");

        return await _resolution.ReadResolutionAsync(actorUserId, sessionId, deploymentEpoch,
            operationId, expectedFingerprint, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateIdentity(Guid operationId, WebOperationKind kind, long? target, byte[] fingerprint)
    {
        var operation = operationId.ToString("D");
        var create = kind == WebOperationKind.SalesCreate || kind == WebOperationKind.ReceiptCreate;
        if (operationId == Guid.Empty || operation[14] != '4' || "89ab".IndexOf(operation[19]) < 0 ||
            !Enum.IsDefined(typeof(WebOperationKind), kind) || (create ? target.HasValue : !target.HasValue || target <= 0) ||
            fingerprint == null || fingerprint.Length != 32)
            throw new ArgumentException("A complete canonical operation identity is required.");
    }

    private void RequireTransaction()
    {
        if (_context.Transaction == null) throw new InvalidOperationException("Resend eligibility reads require an explicit caller-owned transaction.");
    }
}
