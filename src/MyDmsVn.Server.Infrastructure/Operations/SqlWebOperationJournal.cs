using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Operations;

public sealed class SqlWebOperationJournal : IWebOperationJournal
{
    private readonly ISqlExecutionContext _context;
    private readonly ICurrentUserAccessor _current;
    private readonly IPermissionRegistry _registry;
    private readonly SuperuserIdentityPolicy? _superuser;
    private WebOperationContext? _admitted;
    private int _actor;
    public SqlWebOperationJournal(ISqlExecutionContext context, ICurrentUserAccessor current, IPermissionRegistry registry, SuperuserIdentityPolicy? superuser = null)
    {
        _context = context;
        _current = current;
        _registry = registry;
        _superuser = superuser;
    }

    public async Task<byte[]?> AdmitAsync(WebOperationContext operation, int actorUserId, CancellationToken token)
    {
        RequireTransaction();
        if (_admitted != null)
            throw new InvalidOperationException("One operation per transaction is required.");
        if (!_current.Current.IsAuthenticated || !_current.Current.IsActive || _current.Current.UserId != actorUserId)
            throw new WebOperationException("Auth.Unauthorized");
        await FenceAsync(_context, operation.DeploymentEpoch, actorUserId, operation.OperationId, token).ConfigureAwait(false);
        // Users precedes session, matching account-disable and refresh lock order. HOLDLOCK retains
        // shared keys/ranges through the caller's commit, including absent grants and membership.
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "SELECT IsActive FROM dbo.Users WITH (HOLDLOCK) WHERE UserId=@actor;",
            SqlRead.P("@actor", SqlDbType.Int, actorUserId)))
            if (!Equals(await command.ExecuteScalarAsync(token).ConfigureAwait(false), true))
                throw new WebOperationException("Auth.Unauthorized");
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "SELECT COUNT(*) FROM dbo.WebAuthSessions WITH (HOLDLOCK) WHERE SessionId=@session AND UserId=@actor AND RevokedAtUtc IS NULL AND AbsoluteExpiresAtUtc>SYSUTCDATETIME() AND IdleExpiresAtUtc>SYSUTCDATETIME();",
            SqlRead.P("@session", SqlDbType.UniqueIdentifier, operation.SessionId), SqlRead.P("@actor", SqlDbType.Int, actorUserId)))
            if (Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false)) != 1)
                throw new WebOperationException("Auth.Unauthorized");
        var auth = new PermissionAuthorizationService(_current, new TransactionPermissionStore(_context), _registry, _superuser);
        if (!await auth.IsAllowedAsync(actorUserId, WebOperationContext.Permission(operation.Kind), token).ConfigureAwait(false))
            throw new WebOperationException("Auth.Forbidden");
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "SELECT DeploymentEpoch,OperationKind,RequestTargetId,RequestFingerprint,Disposition,ResultBytes,CASE WHEN ResultExpiresAtUtc<=SYSUTCDATETIME() THEN 1 ELSE 0 END,ContractVersion,CodecVersion FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation;",
            SqlRead.P("@actor", SqlDbType.Int, actorUserId), SqlRead.P("@operation", SqlDbType.UniqueIdentifier, operation.OperationId)))
        using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (reader.GetGuid(0) != operation.DeploymentEpoch || reader.GetByte(1) != (byte)operation.Kind ||
                    (reader.IsDBNull(2) ? (long?)null : reader.GetInt64(2)) != operation.RequestTargetId || !((byte[])reader[3]).SequenceEqual(operation.Fingerprint))
                    throw new WebOperationException("Idempotency.KeyConflict");
                if (reader.GetString(4) == "EXPIRED" || reader.GetInt32(6) == 1)
                    throw new WebOperationException("Operation.ResultExpired");
                if (reader.GetString(4) != "COMMITTED" || reader.IsDBNull(5) || reader.GetInt16(7) != 1 || reader.GetInt16(8) != 1)
                    throw new WebOperationException("Operation.ResultUnavailable");
                return (byte[])reader[5];
            }
        }
        _admitted = operation;
        _actor = actorUserId;
        return null;
    }

    public async Task CompleteAsync(WebOperationContext operation, int actorUserId, long resultDocumentId, byte[] bytes, CancellationToken token)
    {
        RequireTransaction();
        if (!ReferenceEquals(operation, _admitted) || actorUserId != _actor)
            throw new InvalidOperationException("An unseen admitted intent is required.");
        if (bytes == null || bytes.Length == 0 || bytes.Length > 1048576 || resultDocumentId <= 0 || (operation.RequestTargetId.HasValue && operation.RequestTargetId != resultDocumentId))
            throw new WebOperationException("Operation.ResultUnavailable");
        new System.Text.UTF8Encoding(false, true).GetString(bytes);
        using var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "DECLARE @now datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebOperationJournal(ActorUserId,OperationId,DeploymentEpoch,ContractVersion,CodecVersion,OperationKind,RequestTargetId,RequestFingerprint,ResultDocumentId,Disposition,ResultBytes,CreatedAtUtc,CompletedAtUtc,ResultExpiresAtUtc) VALUES(@actor,@operation,@epoch,1,1,@kind,@target,@hash,@result,'COMMITTED',@bytes,@now,@now,DATEADD(day,7,@now));",
            SqlRead.P("@actor", SqlDbType.Int, actorUserId), SqlRead.P("@operation", SqlDbType.UniqueIdentifier, operation.OperationId),
            SqlRead.P("@epoch", SqlDbType.UniqueIdentifier, operation.DeploymentEpoch), SqlRead.P("@kind", SqlDbType.TinyInt, (byte)operation.Kind),
            SqlRead.P("@target", SqlDbType.BigInt, (object?)operation.RequestTargetId ?? DBNull.Value), SqlRead.P("@hash", SqlDbType.Binary, operation.Fingerprint),
            SqlRead.P("@result", SqlDbType.BigInt, resultDocumentId), SqlRead.P("@bytes", SqlDbType.VarBinary, bytes));
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        _admitted = null;
    }

    internal static async Task FenceAsync(ISqlExecutionContext context, Guid epoch, int actor, Guid operation, CancellationToken token)
    {
        if (context.Transaction == null)
            throw new InvalidOperationException("An explicit current transaction is required.");
        using (var command = SqlRead.Command(context.Connection, context.Transaction,
            "SELECT DeploymentEpoch FROM dbo.WebDeploymentState WITH (HOLDLOCK) WHERE Id=1;"))
            if (!Equals(await command.ExecuteScalarAsync(token).ConfigureAwait(false), epoch))
                throw new WebOperationException("Deployment.EpochMismatch");
        using (var command = SqlRead.Command(context.Connection, context.Transaction,
            "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; SELECT @r;",
            SqlRead.Text("@key", "MyDmsVn.WebOperation:" + actor.ToString(CultureInfo.InvariantCulture) + ":" + operation.ToString("N"))))
            if (Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false)) < 0)
                throw new WebOperationException("Operation.Unavailable");
    }
    private void RequireTransaction()
    {
        if (_context.Transaction == null)
            throw new InvalidOperationException("Journal work requires the business transaction.");
    }

    private sealed class TransactionPermissionStore : IPermissionStore
    {
        private readonly ISqlExecutionContext _context;
        public TransactionPermissionStore(ISqlExecutionContext context)
        {
            _context = context;
        }
        public async Task<PermissionSnapshot> GetSnapshotAsync(int userId, string permissionKey, CancellationToken token)
        {
            using var command = SqlRead.Command(_context.Connection, _context.Transaction, @"
SELECT u.IsActive,u.NormalizedUsername,s.SuperuserUserId,s.BoundNormalizedUsername,
CASE WHEN u.IsActive=1 AND NOT EXISTS(SELECT 1 FROM dbo.SystemSecuritySettings b WITH(HOLDLOCK) WHERE b.Id=1 AND b.SuperuserUserId=u.UserId AND b.BoundNormalizedUsername=u.NormalizedUsername)
THEN (SELECT up.Granted FROM dbo.UserPermissions up WITH(HOLDLOCK) WHERE up.UserId=u.UserId AND up.PermissionKey=@permission) ELSE NULL END,
CAST(CASE WHEN u.IsActive=1 AND NOT EXISTS(SELECT 1 FROM dbo.SystemSecuritySettings b WITH(HOLDLOCK) WHERE b.Id=1 AND b.SuperuserUserId=u.UserId AND b.BoundNormalizedUsername=u.NormalizedUsername)
AND EXISTS(SELECT 1 FROM dbo.UserRoles ur WITH(HOLDLOCK) JOIN dbo.Roles r WITH(HOLDLOCK) ON r.RoleId=ur.RoleId AND r.IsActive=1 JOIN dbo.RolePermissions rp WITH(HOLDLOCK) ON rp.RoleId=ur.RoleId WHERE ur.UserId=u.UserId AND rp.PermissionKey=@permission) THEN 1 ELSE 0 END AS bit)
FROM dbo.Users u WITH(HOLDLOCK) LEFT JOIN dbo.SystemSecuritySettings s WITH(HOLDLOCK) ON s.Id=1 WHERE u.UserId=@user;",
                SqlRead.P("@user", SqlDbType.Int, userId), SqlRead.Text("@permission", permissionKey));
            return await SqlRead.SingleOrDefaultAsync(command, r => new PermissionSnapshot(r.IsDBNull(4) ? (bool?)null : r.GetBoolean(4), r.GetBoolean(5), r.GetBoolean(0), r.GetString(1), r.IsDBNull(2) ? (int?)null : r.GetInt32(2), r.IsDBNull(3) ? null : r.GetString(3)), token).ConfigureAwait(false)
                ?? new PermissionSnapshot(null, false, false);
        }
    }
}
