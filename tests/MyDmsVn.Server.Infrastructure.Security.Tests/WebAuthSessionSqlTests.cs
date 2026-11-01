using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebAuthSessionSqlTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Configured_idle_and_absolute_lifetimes_apply_to_issue_rotation_and_expiry() => Owned(async test =>
    {
        var issuedAt = test.Clock.UtcNow;
        var store = new SqlWebAuthSessionStore(test.Factory, test.Clock, options: new WebAuthSessionOptions(2, 5));
        var token = (await store.IssueAsync(test.UserId, None))!;
        Assert.Equal(issuedAt.AddDays(2), token.ExpiresAtUtc);
        Assert.Equal(issuedAt.AddDays(5), token.AbsoluteExpiresAtUtc);
        foreach (var day in new[] { 1d, 2.5d, 4d })
        {
            test.Clock.UtcNow = issuedAt.AddDays(day);
            token = (await store.RotateAsync(token.RefreshToken, None))!;
            Assert.NotNull(token);
        }
        Assert.Equal(issuedAt.AddDays(5), token.ExpiresAtUtc);
        test.Clock.UtcNow = issuedAt.AddDays(5);
        Assert.Null(await store.GetActiveUserAsync(token.SessionId, test.UserId, None));
        Assert.Null(await store.RotateAsync(token.RefreshToken, None));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Issue_persists_only_hash_and_authoritative_session_actor() => Owned(async test =>
    {
        var issued = (await test.Store.IssueAsync(test.UserId, None))!;
        Assert.NotNull(issued);
        Assert.Equal(36, issued.RefreshToken.Length);
        Assert.Equal('4', issued.RefreshToken[14]);
        Assert.Contains(issued.RefreshToken[19], "89ab");
        Assert.Equal(0, string.CompareOrdinal(Guid.Parse(issued.RefreshToken).ToString("D"), issued.RefreshToken));
        Assert.Equal(test.Clock.UtcNow.AddDays(7), issued.ExpiresAtUtc);
        Assert.Equal(test.Clock.UtcNow.AddDays(30), issued.AbsoluteExpiresAtUtc);
        using var sha = SHA256.Create();
        var persisted = await test.Connection.QuerySingleAsync<byte[]>("SELECT TokenHash FROM dbo.WebRefreshTokens;");
        Assert.True(sha.ComputeHash(Encoding.UTF8.GetBytes(issued.RefreshToken)).SequenceEqual(persisted));
        Assert.NotNull(await test.Store.GetActiveUserAsync(issued.SessionId, test.UserId, None));
        Assert.Null(await test.Store.GetActiveUserAsync(issued.SessionId, test.UserId + 1, None));
        await test.Connection.ExecuteAsync("UPDATE dbo.Users SET DisplayName=N'New name' WHERE UserId=@id;", new { id = test.UserId });
        Assert.Equal("New name", (await test.Store.GetActiveUserAsync(issued.SessionId, test.UserId, None))!.DisplayName);
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Rotation_consumes_parent_and_replay_revokes_even_issued_child() => Owned(async test =>
    {
        var parent = (await test.Store.IssueAsync(test.UserId, None))!;
        test.Clock.UtcNow = test.Clock.UtcNow.AddDays(3);
        var child = (await test.Store.RotateAsync(parent.RefreshToken, None))!;
        Assert.NotNull(child);
        Assert.Equal(parent.SessionId, child.SessionId);
        Assert.NotEqual(0, string.CompareOrdinal(parent.RefreshToken, child.RefreshToken));
        Assert.Equal(test.Clock.UtcNow.AddDays(7), child.ExpiresAtUtc);
        Assert.Equal(parent.AbsoluteExpiresAtUtc, child.AbsoluteExpiresAtUtc);
        Assert.Equal(1, await test.Connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens WHERE ConsumedAtUtc IS NOT NULL AND ReplacedByTokenHash IS NOT NULL;"));
        Assert.Null(await test.Store.RotateAsync(parent.RefreshToken, None));
        Assert.Null(await test.Store.RotateAsync(child.RefreshToken, None));
        Assert.Null(await test.Store.GetActiveUserAsync(parent.SessionId, test.UserId, None));
        Assert.Equal(2, await test.Connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens WHERE RevokedAtUtc IS NOT NULL;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Consumed_ancestor_replay_revokes_active_family_even_after_ancestor_expiry() => Owned(async test =>
    {
        var parent = (await test.Store.IssueAsync(test.UserId, None))!;
        test.Clock.UtcNow = test.Clock.UtcNow.AddDays(6);
        var child = (await test.Store.RotateAsync(parent.RefreshToken, None))!;
        test.Clock.UtcNow = test.Clock.UtcNow.AddDays(2);
        Assert.NotNull(await test.Store.GetActiveUserAsync(parent.SessionId, test.UserId, None));
        Assert.Null(await test.Store.RotateAsync(parent.RefreshToken, None));
        Assert.Null(await test.Store.RotateAsync(child.RefreshToken, None));
        Assert.Null(await test.Store.GetActiveUserAsync(parent.SessionId, test.UserId, None));
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Concurrent_refresh_grants_at_most_one_child_and_replay_revokes_family() => Owned(async test =>
    {
        var parent = (await test.Store.IssueAsync(test.UserId, None))!;
        var factory = test.Own(new RecordingFactory(test.ConnectionString));
        var store = new SqlWebAuthSessionStore(factory, test.Clock);
        var blocker = await test.BlockSessionAsync(parent.SessionId);
        var attempts = new[] { test.Track(store.RotateAsync(parent.RefreshToken, test.Cancellation.Token)), test.Track(store.RotateAsync(parent.RefreshToken, test.Cancellation.Token)) };
        var first = await factory.NextAsync();
        var second = await factory.NextAsync();
        Assert.NotEqual(first, second);
        await test.WaitKeyBlockedAsync(first, blocker);
        await test.WaitKeyBlockedAsync(second, blocker);
        test.ReleaseBlockers();
        var results = await Task.WhenAll(attempts);
        Assert.Single(results, result => result != null);
        Assert.Equal(2, await test.Connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens;"));
        Assert.Null(await test.Store.GetActiveUserAsync(parent.SessionId, test.UserId, None));
        Assert.Null(await test.Store.RotateAsync(results.Single(result => result != null)!.RefreshToken, None));
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Logout_racing_refresh_leaves_no_active_session_and_is_idempotent() => Owned(async test =>
    {
        var parent = (await test.Store.IssueAsync(test.UserId, None))!;
        var factory = test.Own(new RecordingFactory(test.ConnectionString));
        var store = new SqlWebAuthSessionStore(factory, test.Clock);
        var blocker = await test.BlockSessionAsync(parent.SessionId);
        var refresh = test.Track(store.RotateAsync(parent.RefreshToken, test.Cancellation.Token));
        var logout = test.Track(store.RevokeAsync(parent.RefreshToken, test.Cancellation.Token));
        var first = await factory.NextAsync();
        var second = await factory.NextAsync();
        Assert.NotEqual(first, second);
        await test.WaitKeyBlockedAsync(first, blocker);
        await test.WaitKeyBlockedAsync(second, blocker);
        test.ReleaseBlockers();
        await Task.WhenAll(refresh, logout);
        await test.Store.RevokeAsync(parent.RefreshToken, None);
        Assert.Null(await test.Store.GetActiveUserAsync(parent.SessionId, test.UserId, None));
        if (refresh.Result != null) Assert.Null(await test.Store.RotateAsync(refresh.Result.RefreshToken, None));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Disabled_user_fails_closed_and_refresh_permanently_revokes_family() => Owned(async test =>
    {
        var parent = (await test.Store.IssueAsync(test.UserId, None))!;
        await test.Connection.ExecuteAsync("UPDATE dbo.Users SET IsActive=0 WHERE UserId=@id;", new { id = test.UserId });
        Assert.Null(await test.Store.IssueAsync(test.UserId, None));
        Assert.Null(await test.Store.GetActiveUserAsync(parent.SessionId, test.UserId, None));
        Assert.Null(await test.Store.RotateAsync(parent.RefreshToken, None));
        await test.Connection.ExecuteAsync("UPDATE dbo.Users SET IsActive=1 WHERE UserId=@id;", new { id = test.UserId });
        Assert.Null(await test.Store.GetActiveUserAsync(parent.SessionId, test.UserId, None));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Disable_then_reenable_without_auth_request_cannot_restore_old_session() => Owned(async test =>
    {
        var parent = (await test.Store.IssueAsync(test.UserId, None))!;
        var otherDevice = (await test.Store.IssueAsync(test.UserId, None))!;
        await test.Connection.ExecuteAsync("UPDATE dbo.Users SET IsActive=0 WHERE UserId=@id; UPDATE dbo.Users SET IsActive=1 WHERE UserId=@id;", new { id = test.UserId });
        Assert.Null(await test.Store.GetActiveUserAsync(parent.SessionId, test.UserId, None));
        Assert.Null(await test.Store.GetActiveUserAsync(otherDevice.SessionId, test.UserId, None));
        Assert.Null(await test.Store.RotateAsync(parent.RefreshToken, None));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Idle_expiry_denies_token_and_access_at_exact_boundary() => Owned(async test =>
    {
        var parent = (await test.Store.IssueAsync(test.UserId, None))!;
        test.Clock.UtcNow = parent.ExpiresAtUtc;
        Assert.Null(await test.Store.GetActiveUserAsync(parent.SessionId, test.UserId, None));
        Assert.Null(await test.Store.RotateAsync(parent.RefreshToken, None));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Repeated_refresh_never_extends_absolute_lifetime() => Owned(async test =>
    {
        var token = (await test.Store.IssueAsync(test.UserId, None))!;
        var absolute = token.AbsoluteExpiresAtUtc;
        for (var day = 0; day < 4; day++)
        {
            test.Clock.UtcNow = test.Clock.UtcNow.AddDays(6);
            token = (await test.Store.RotateAsync(token.RefreshToken, None))!;
            Assert.NotNull(token);
        }
        Assert.Equal(absolute, token.ExpiresAtUtc);
        test.Clock.UtcNow = absolute;
        Assert.Null(await test.Store.RotateAsync(token.RefreshToken, None));
        Assert.Null(await test.Store.GetActiveUserAsync(token.SessionId, test.UserId, None));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Unknown_token_never_revokes_another_session() => Owned(async test =>
    {
        var token = (await test.Store.IssueAsync(test.UserId, None))!;
        var unknown = Guid.NewGuid().ToString("D");
        Assert.Null(await test.Store.RotateAsync(unknown, None));
        await test.Store.RevokeAsync(unknown, None);
        Assert.NotNull(await test.Store.GetActiveUserAsync(token.SessionId, test.UserId, None));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Logout_and_replay_revoke_only_the_proven_family() => Owned(async test =>
    {
        var first = (await test.Store.IssueAsync(test.UserId, None))!;
        var second = (await test.Store.IssueAsync(test.UserId, None))!;
        Assert.NotNull(await test.Store.RotateAsync(first.RefreshToken, None));
        Assert.Null(await test.Store.RotateAsync(first.RefreshToken, None));
        await test.Store.RevokeAsync(first.RefreshToken, None);
        Assert.Null(await test.Store.GetActiveUserAsync(first.SessionId, test.UserId, None));
        Assert.NotNull(await test.Store.GetActiveUserAsync(second.SessionId, test.UserId, None));
        Assert.NotNull(await test.Store.RotateAsync(second.RefreshToken, None));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Failed_child_insert_rolls_back_parent_consumption() => Owned(async test =>
    {
        var parent = (await test.Store.IssueAsync(test.UserId, None))!;
        await test.Connection.ExecuteAsync("CREATE TRIGGER dbo.WebAuthTestRejectChild ON dbo.WebRefreshTokens AFTER INSERT AS BEGIN THROW 51991, 'Injected insert failure.', 1; END;");
        await Assert.ThrowsAsync<SqlException>(() => test.Store.RotateAsync(parent.RefreshToken, None));
        Assert.Equal(1, await test.Connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens;"));
        Assert.Equal(0, await test.Connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens WHERE ConsumedAtUtc IS NOT NULL;"));
        await test.Connection.ExecuteAsync("DROP TRIGGER dbo.WebAuthTestRejectChild;");
        Assert.NotNull(await test.Store.RotateAsync(parent.RefreshToken, None));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Failed_initial_token_insert_rolls_back_new_session() => Owned(async test =>
    {
        await test.Connection.ExecuteAsync("CREATE TRIGGER dbo.WebAuthTestRejectIssue ON dbo.WebRefreshTokens AFTER INSERT AS BEGIN THROW 51992, 'Injected initial insert failure.', 1; END;");
        await Assert.ThrowsAsync<SqlException>(() => test.Store.IssueAsync(test.UserId, None));
        Assert.Equal(0, await test.Connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens;"));
        Assert.Equal(0, await test.Connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebAuthSessions;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Maintenance_keeps_replay_evidence_through_absolute_expiry_plus_retention() => Owned(async test =>
    {
        var parent = (await test.Store.IssueAsync(test.UserId, None))!;
        Assert.NotNull(await test.Store.RotateAsync(parent.RefreshToken, None));
        var maintenance = new SqlWebAuthSessionMaintenance(test.Factory, test.Clock);
        test.Clock.UtcNow = parent.AbsoluteExpiresAtUtc.AddDays(7).AddTicks(-1);
        Assert.Equal(0, await maintenance.CleanupExpiredAsync(TimeSpan.FromDays(7), None));
        Assert.Equal(2, await test.Connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens;"));
        test.Clock.UtcNow = test.Clock.UtcNow.AddTicks(1);
        Assert.Equal(1, await maintenance.CleanupExpiredAsync(TimeSpan.FromDays(7), None));
        Assert.Equal(0, await test.Connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens;"));
        Assert.Equal(0, await test.Connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebAuthSessions;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Replay_audit_failure_cannot_undo_committed_revocation_or_expose_token() => Owned(async test =>
    {
        var audit = new FailingAuditWriter();
        var store = new SqlWebAuthSessionStore(test.Factory, test.Clock, audit);
        var parent = (await store.IssueAsync(test.UserId, None))!;
        Assert.NotNull(await store.RotateAsync(parent.RefreshToken, None));
        Assert.Null(await store.RotateAsync(parent.RefreshToken, None));
        Assert.Null(await store.GetActiveUserAsync(parent.SessionId, test.UserId, None));
        Assert.NotNull(audit.Entry);
        Assert.Equal(parent.SessionId.ToString("D"), audit.Entry!.DocumentId);
        Assert.Equal("WebAuthSessionReplay", audit.Entry.DocumentType);
        Assert.Equal(test.UserId, audit.Entry.UserId);
        Assert.Equal(SecurityAuditOutcome.Denied, audit.Entry.Outcome);
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Waiting_session_lookup_checks_clock_after_session_lock_is_acquired() => Owned(async test =>
    {
        var token = (await test.Store.IssueAsync(test.UserId, None))!;
        var factory = test.Own(new RecordingFactory(test.ConnectionString));
        var store = new SqlWebAuthSessionStore(factory, test.Clock);
        var blocker = await test.BlockSessionAsync(token.SessionId);
        var lookup = test.Track(store.GetActiveUserAsync(token.SessionId, test.UserId, test.Cancellation.Token));
        await test.WaitKeyBlockedAsync(await factory.NextAsync(), blocker);
        test.Clock.UtcNow = token.ExpiresAtUtc;
        test.ReleaseBlockers();
        Assert.Null(await lookup);
    });

    private static async Task Owned(Func<Scenario, Task> body)
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? failure = null;
        Scenario? scenario = null;
        try
        {
            var migration = FrozenWeb11Migration.Install(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.GetType().Name);
            using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            var userId = await connection.QuerySingleAsync<int>("INSERT dbo.Users (Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES (N'web',N'WEB',N'Web actor',N'Local',N'hash',N'',N'BCrypt');");
            var clock = new MutableClock();
            var factory = new SqlConnectionFactory(database.ConnectionString);
            scenario = new Scenario(connection, userId, clock, factory, new SqlWebAuthSessionStore(factory, clock), database.ConnectionString);
            await body(scenario);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try
            {
                if (scenario == null) await database.DisposeAsync();
                else await scenario.CleanupAsync(database);
            }
            catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); }
        }
    }

    private sealed class MutableClock : IUtcClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FailingAuditWriter : ISecurityAuditWriter
    {
        public SecurityAuditEntry? Entry { get; private set; }
        public Task WriteAsync(SecurityAuditEntry entry, bool businessOperationCommitted, CancellationToken cancellationToken)
        {
            Assert.True(businessOperationCommitted);
            Entry = entry;
            throw new InvalidOperationException("Audit unavailable.");
        }
    }

    private sealed class Scenario
    {
        private readonly List<Task> _workers = new List<Task>();
        private readonly List<IDisposable> _resources = new List<IDisposable>();
        private readonly List<SqlTransaction> _blockers = new List<SqlTransaction>();
        public Scenario(SqlConnection connection, int userId, MutableClock clock, IDbConnectionFactory factory, SqlWebAuthSessionStore store, string connectionString)
        { Connection = connection; UserId = userId; Clock = clock; Factory = factory; Store = store; ConnectionString = connectionString; }
        public SqlConnection Connection { get; }
        public int UserId { get; }
        public MutableClock Clock { get; }
        public IDbConnectionFactory Factory { get; }
        public SqlWebAuthSessionStore Store { get; }
        public string ConnectionString { get; }
        public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        public T Own<T>(T resource) where T : IDisposable { _resources.Add(resource); return resource; }
        public T Track<T>(T worker) where T : Task { _workers.Add(worker); return worker; }

        public async Task<int> BlockSessionAsync(Guid session)
        {
            var connection = Own(new SqlConnection(ConnectionString));
            await connection.OpenAsync(Cancellation.Token);
            var transaction = Own(connection.BeginTransaction());
            _blockers.Add(transaction);
            await connection.QuerySingleAsync<Guid>("SELECT SessionId FROM dbo.WebAuthSessions WITH (UPDLOCK,HOLDLOCK,ROWLOCK) WHERE SessionId=@session;", new { session }, transaction);
            return connection.ServerProcessId;
        }

        public void ReleaseBlockers()
        {
            foreach (var transaction in _blockers)
                if (transaction.Connection != null) transaction.Rollback();
        }

        public async Task WaitKeyBlockedAsync(int worker, int owner)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(10))
            {
                var count = await Connection.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM sys.dm_tran_locks AS waiting
JOIN sys.dm_tran_locks AS held
 ON waiting.resource_associated_entity_id=held.resource_associated_entity_id
 AND waiting.resource_description=held.resource_description AND waiting.resource_type=held.resource_type
JOIN sys.partitions AS partitionInfo ON partitionInfo.hobt_id=waiting.resource_associated_entity_id
WHERE waiting.request_session_id=@worker AND held.request_session_id=@owner
 AND waiting.resource_database_id=DB_ID() AND held.resource_database_id=DB_ID()
 AND waiting.resource_type='KEY' AND waiting.request_status IN ('WAIT','CONVERT')
 AND waiting.request_mode IN ('U','RangeS-U')
 AND held.request_status='GRANT' AND held.request_mode='U'
 AND partitionInfo.object_id=OBJECT_ID('dbo.WebAuthSessions')
 AND EXISTS (SELECT 1 FROM sys.dm_tran_locks AS accountLock
  JOIN sys.partitions AS accountPartition ON accountPartition.hobt_id=accountLock.resource_associated_entity_id
  WHERE accountLock.request_session_id=@worker AND accountLock.resource_database_id=DB_ID()
   AND accountLock.request_status='GRANT' AND accountLock.request_mode IN ('S','RangeS-S')
   AND accountPartition.object_id=OBJECT_ID('dbo.Users'));", new { worker, owner }, commandTimeout: 5);
                if (count > 0) return;
                await Task.Delay(25, Cancellation.Token);
            }
            throw new TimeoutException("The independent worker did not wait on the held auth-session key.");
        }

        public async Task CleanupAsync(SqlTestDatabase database)
        {
            try
            {
                await SqlConcurrencyCleanup.CompleteAsync(
                    () => { ReleaseBlockers(); return Task.CompletedTask; }, null, Cancellation,
                    async () => { foreach (var resource in Enumerable.Reverse(_resources)) resource.Dispose(); await database.DisposeAsync(); },
                    TimeSpan.FromSeconds(20), _workers.ToArray());
            }
            finally { Cancellation.Dispose(); }
        }
    }

    private sealed class RecordingFactory : IDbConnectionFactory, IDisposable
    {
        private readonly string _connectionString;
        private readonly ConcurrentQueue<int> _sessions = new ConcurrentQueue<int>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        public RecordingFactory(string connectionString) { _connectionString = connectionString; }
        public IDbConnection OpenConnection() => OpenConnectionAsync(None).GetAwaiter().GetResult();
        public async Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(_connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                _sessions.Enqueue(connection.ServerProcessId);
                _signal.Release();
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }
        public async Task<int> NextAsync()
        {
            if (!await _signal.WaitAsync(TimeSpan.FromSeconds(10)) || !_sessions.TryDequeue(out var session))
                throw new TimeoutException("No independently opened SQL worker session was observed.");
            return session;
        }
        public void Dispose() => _signal.Dispose();
    }
}
