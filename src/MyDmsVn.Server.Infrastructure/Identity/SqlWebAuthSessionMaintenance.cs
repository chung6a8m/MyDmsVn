using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Identity;

// Construct explicitly with maintenance credentials; never registered in the web runtime container.
public sealed class SqlWebAuthSessionMaintenance
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IUtcClock _clock;

    public SqlWebAuthSessionMaintenance(IDbConnectionFactory connectionFactory, IUtcClock clock)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<int> CleanupExpiredAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        if (retention < TimeSpan.FromDays(7) || retention > TimeSpan.FromDays(365))
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention must be between 7 and 365 days after absolute family expiry.");
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock.UtcNow;
        if (now.Kind != DateTimeKind.Utc) throw new InvalidOperationException("The auth clock must return UTC.");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(60));
        using var connection = await SqlWebAuthSessionStore.OpenAsync(_connectionFactory, budget.Token).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        // One bounded batch, entire expired families only. Consumed ancestors remain for replay detection.
        using var command = SqlRead.Command(connection, transaction,
            "DECLARE @families TABLE (SessionId uniqueidentifier PRIMARY KEY); " +
            "INSERT @families SELECT TOP (1000) SessionId FROM dbo.WebAuthSessions WITH (UPDLOCK,HOLDLOCK) WHERE AbsoluteExpiresAtUtc<=@cutoff ORDER BY AbsoluteExpiresAtUtc,SessionId; " +
            "DELETE t FROM dbo.WebRefreshTokens AS t INNER JOIN @families AS f ON f.SessionId=t.SessionId; " +
            "DELETE s FROM dbo.WebAuthSessions AS s INNER JOIN @families AS f ON f.SessionId=s.SessionId; " +
            "SELECT COUNT(*) FROM @families;",
            SqlRead.P("@cutoff", SqlDbType.DateTime2, now.Subtract(retention)));
        var count = await SqlRead.SingleAsync(command, reader => reader.GetInt32(0), budget.Token).ConfigureAwait(false);
        budget.Token.ThrowIfCancellationRequested();
        transaction.Commit();
        return count;
    }
}
