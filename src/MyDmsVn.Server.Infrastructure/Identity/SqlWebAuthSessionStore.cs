using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Identity;

public sealed class SqlWebAuthSessionStore : IWebAuthSessionStore
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IUtcClock _clock;
    private readonly ISecurityAuditWriter? _auditWriter;

    public SqlWebAuthSessionStore(IDbConnectionFactory connectionFactory, IUtcClock clock, ISecurityAuditWriter? auditWriter = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _auditWriter = auditWriter;
    }

    public async Task<WebAuthSessionToken?> IssueAsync(int userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId <= 0) return null;
        using var connection = await OpenAsync(_connectionFactory, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        // Users first matches the schema11 account-disable trigger's user -> session lock order.
        var user = await ReadUserAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);
        if (user == null || !user.IsActive) { transaction.Commit(); return null; }
        var now = UtcNow();
        var sessionId = Guid.NewGuid();
        var token = NewToken();
        var expiry = now.AddDays(7);
        var absolute = now.AddDays(30);
        using (var command = SqlRead.Command(connection, transaction,
            "INSERT dbo.WebAuthSessions (SessionId,UserId,CreatedAtUtc,AbsoluteExpiresAtUtc,IdleExpiresAtUtc) " +
            "VALUES (@session,@user,@now,@absolute,@expiry);",
            SqlRead.P("@session", SqlDbType.UniqueIdentifier, sessionId), SqlRead.P("@user", SqlDbType.Int, userId),
            SqlRead.P("@now", SqlDbType.DateTime2, now), SqlRead.P("@absolute", SqlDbType.DateTime2, absolute),
            SqlRead.P("@expiry", SqlDbType.DateTime2, expiry)))
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await InsertTokenAsync(connection, transaction, Hash(token), sessionId, now, expiry, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return new WebAuthSessionToken(sessionId, user, token, expiry, absolute);
    }

    public async Task<WebAuthSessionToken?> RotateAsync(string refreshToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCanonicalToken(refreshToken)) return null;
        return await ProcessTokenAsync(Hash(refreshToken), true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCanonicalToken(refreshToken)) return;
        await ProcessTokenAsync(Hash(refreshToken), false, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WebAuthSessionToken?> ProcessTokenAsync(byte[] hash, bool rotate, CancellationToken cancellationToken)
    {
        using var connection = await OpenAsync(_connectionFactory, cancellationToken).ConfigureAwait(false);
        TokenOwner? owner;
        // This lookup retains no locks. Immutable lineage only selects the owner to lock below.
        using (var command = SqlRead.Command(connection, null,
            "SELECT SessionId FROM dbo.WebRefreshTokens WHERE TokenHash=@hash;",
            SqlRead.P("@hash", SqlDbType.Binary, hash)))
            owner = await SqlRead.SingleOrDefaultAsync(command,
                reader => new TokenOwner { SessionId = reader.GetGuid(0) }, cancellationToken).ConfigureAwait(false);
        if (owner == null) return null;
        // Separate statement releases the token read before touching a session (logout locks session first).
        using (var command = SqlRead.Command(connection, null,
            "SELECT UserId FROM dbo.WebAuthSessions WHERE SessionId=@session;",
            SqlRead.P("@session", SqlDbType.UniqueIdentifier, owner.SessionId)))
        {
            var userId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (!(userId is int id)) return null;
            owner.UserId = id;
        }
        using var transaction = connection.BeginTransaction();
        var user = await ReadUserAsync(connection, transaction, owner.UserId, cancellationToken).ConfigureAwait(false);
        var session = await LockSessionAsync(connection, transaction, owner.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null || session.UserId != owner.UserId) { transaction.Commit(); return null; }
        var now = UtcNow();
        if (!rotate)
        {
            await RevokeFamilyAsync(connection, transaction, owner.SessionId, now, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return null;
        }
        if (session.Revoked) { transaction.Commit(); return null; }
        if (user == null || !user.IsActive)
        {
            await RevokeFamilyAsync(connection, transaction, owner.SessionId, now, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return null;
        }
        if (session.AbsoluteExpiresAtUtc <= now || session.IdleExpiresAtUtc <= now)
        { transaction.Commit(); return null; }
        TokenRow? token;
        using (var command = SqlRead.Command(connection, transaction,
            "SELECT IssuedAtUtc,ExpiresAtUtc,ConsumedAtUtc,RevokedAtUtc FROM dbo.WebRefreshTokens WITH (UPDLOCK,HOLDLOCK) WHERE TokenHash=@hash AND SessionId=@session;",
            SqlRead.P("@hash", SqlDbType.Binary, hash), SqlRead.P("@session", SqlDbType.UniqueIdentifier, owner.SessionId)))
            token = await SqlRead.SingleOrDefaultAsync(command, reader => new TokenRow
            {
                IssuedAtUtc = reader.GetDateTime(0), ExpiresAtUtc = reader.GetDateTime(1),
                Consumed = !reader.IsDBNull(2), Revoked = !reader.IsDBNull(3),
            }, cancellationToken).ConfigureAwait(false);
        if (token == null) { transaction.Commit(); return null; }
        // A consumed ancestor proves replay even after its own TTL if the family is still active.
        if (token.Consumed)
        {
            await RevokeFamilyAsync(connection, transaction, owner.SessionId, now, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            await AuditReplayAsync(owner, now).ConfigureAwait(false);
            return null;
        }
        if (token.Revoked || token.ExpiresAtUtc <= now) { transaction.Commit(); return null; }
        if (now < token.IssuedAtUtc) throw new InvalidOperationException("The auth clock moved backwards.");
        var child = NewToken();
        var childHash = Hash(child);
        var expiry = now.AddDays(7);
        if (expiry > session.AbsoluteExpiresAtUtc) expiry = session.AbsoluteExpiresAtUtc;
        await InsertTokenAsync(connection, transaction, childHash, owner.SessionId, now, expiry, cancellationToken).ConfigureAwait(false);
        using (var command = SqlRead.Command(connection, transaction,
            "UPDATE dbo.WebRefreshTokens SET ConsumedAtUtc=@now,ReplacedByTokenHash=@child WHERE TokenHash=@hash AND ConsumedAtUtc IS NULL AND RevokedAtUtc IS NULL AND ExpiresAtUtc>@now; " +
            "IF @@ROWCOUNT<>1 THROW 51191, 'Refresh state changed.', 1; " +
            "UPDATE dbo.WebAuthSessions SET IdleExpiresAtUtc=@expiry WHERE SessionId=@session AND RevokedAtUtc IS NULL; " +
            "IF @@ROWCOUNT<>1 THROW 51191, 'Session state changed.', 1;",
            SqlRead.P("@now", SqlDbType.DateTime2, now), SqlRead.P("@child", SqlDbType.Binary, childHash),
            SqlRead.P("@hash", SqlDbType.Binary, hash), SqlRead.P("@expiry", SqlDbType.DateTime2, expiry),
            SqlRead.P("@session", SqlDbType.UniqueIdentifier, owner.SessionId)))
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return new WebAuthSessionToken(owner.SessionId, user, child, expiry, session.AbsoluteExpiresAtUtc);
    }

    private async Task AuditReplayAsync(TokenOwner owner, DateTime now)
    {
        if (_auditWriter == null) return;
        try
        {
            await _auditWriter.WriteAsync(new SecurityAuditEntry(SecurityAuditAction.Authentication,
                SecurityAuditOutcome.Denied, owner.UserId, now, "WebAuthSessionReplay", owner.SessionId.ToString("D")),
                true, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Revocation already committed. No exception text, credentials or token hashes enter diagnostics.
            System.Diagnostics.Trace.TraceWarning("Web auth replay audit unavailable after committed revocation.");
        }
    }

    private static async Task RevokeFamilyAsync(IDbConnection connection, IDbTransaction transaction,
        Guid sessionId, DateTime now, CancellationToken cancellationToken)
    {
        using var command = SqlRead.Command(connection, transaction,
            "UPDATE dbo.WebAuthSessions SET RevokedAtUtc=CASE WHEN @now<CreatedAtUtc THEN CreatedAtUtc ELSE @now END WHERE SessionId=@session AND RevokedAtUtc IS NULL; " +
            "UPDATE dbo.WebRefreshTokens SET RevokedAtUtc=CASE WHEN @now<IssuedAtUtc THEN IssuedAtUtc ELSE @now END WHERE SessionId=@session AND RevokedAtUtc IS NULL;",
            SqlRead.P("@now", SqlDbType.DateTime2, now), SqlRead.P("@session", SqlDbType.UniqueIdentifier, sessionId));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CurrentUser?> GetActiveUserAsync(Guid sessionId, int userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sessionId == Guid.Empty || userId <= 0) return null;
        using var connection = await OpenAsync(_connectionFactory, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        var user = await ReadUserAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);
        var session = await LockSessionAsync(connection, transaction, sessionId, cancellationToken).ConfigureAwait(false);
        var now = UtcNow();
        var active = user != null && user.IsActive && session != null && session.UserId == userId &&
            !session.Revoked && session.IdleExpiresAtUtc > now && session.AbsoluteExpiresAtUtc > now;
        transaction.Commit();
        return active ? user : null;
    }

    internal static async Task<IDbConnection> OpenAsync(IDbConnectionFactory factory, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        return await factory.OpenConnectionAsync(timeout.Token).ConfigureAwait(false);
    }

    private DateTime UtcNow()
    {
        var now = _clock.UtcNow;
        if (now.Kind != DateTimeKind.Utc) throw new InvalidOperationException("The auth clock must return UTC.");
        return now;
    }

    private static async Task<CurrentUser?> ReadUserAsync(IDbConnection connection, IDbTransaction transaction,
        int userId, CancellationToken cancellationToken)
    {
        using var command = SqlRead.Command(connection, transaction,
            "SELECT UserId,Username,DisplayName,IsActive FROM dbo.Users WITH (HOLDLOCK) WHERE UserId=@user;",
            SqlRead.P("@user", SqlDbType.Int, userId));
        return await SqlRead.SingleOrDefaultAsync(command,
            reader => CurrentUser.Authenticated(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SessionRow?> LockSessionAsync(IDbConnection connection, IDbTransaction transaction,
        Guid sessionId, CancellationToken cancellationToken)
    {
        using var command = SqlRead.Command(connection, transaction,
            "SELECT UserId,AbsoluteExpiresAtUtc,IdleExpiresAtUtc,RevokedAtUtc FROM dbo.WebAuthSessions WITH (UPDLOCK,HOLDLOCK) WHERE SessionId=@session;",
            SqlRead.P("@session", SqlDbType.UniqueIdentifier, sessionId));
        return await SqlRead.SingleOrDefaultAsync(command, reader => new SessionRow
        {
            UserId = reader.GetInt32(0), AbsoluteExpiresAtUtc = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc),
            IdleExpiresAtUtc = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc), Revoked = !reader.IsDBNull(3),
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertTokenAsync(IDbConnection connection, IDbTransaction transaction,
        byte[] hash, Guid sessionId, DateTime now, DateTime expiry, CancellationToken cancellationToken)
    {
        using var command = SqlRead.Command(connection, transaction,
            "INSERT dbo.WebRefreshTokens (TokenHash,SessionId,IssuedAtUtc,ExpiresAtUtc) VALUES (@hash,@session,@now,@expiry);",
            SqlRead.P("@hash", SqlDbType.Binary, hash), SqlRead.P("@session", SqlDbType.UniqueIdentifier, sessionId),
            SqlRead.P("@now", SqlDbType.DateTime2, now), SqlRead.P("@expiry", SqlDbType.DateTime2, expiry));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string NewToken()
    {
        var bytes = new byte[16];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
        bytes[7] = (byte)((bytes[7] & 15) | 64);
        bytes[8] = (byte)((bytes[8] & 63) | 128);
        return new Guid(bytes).ToString("D");
    }

    private static byte[] Hash(string token)
    {
        using var hash = SHA256.Create();
        return hash.ComputeHash(Encoding.UTF8.GetBytes(token));
    }

    private sealed class SessionRow
    {
        public int UserId { get; set; }
        public DateTime AbsoluteExpiresAtUtc { get; set; }
        public DateTime IdleExpiresAtUtc { get; set; }
        public bool Revoked { get; set; }
    }

    private sealed class TokenOwner
    {
        public Guid SessionId { get; set; }
        public int UserId { get; set; }
    }

    private sealed class TokenRow
    {
        public DateTime IssuedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public bool Consumed { get; set; }
        public bool Revoked { get; set; }
    }

    private static bool IsCanonicalToken(string value) => value != null && value.Length == 36 &&
        Guid.TryParseExact(value, "D", out var parsed) && parsed.ToString("D") == value &&
        value[14] == '4' && "89ab".IndexOf(value[19]) >= 0;
}
