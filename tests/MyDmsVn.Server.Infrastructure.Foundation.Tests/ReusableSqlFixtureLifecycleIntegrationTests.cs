using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class ReusableSqlFixtureLifecycleIntegrationTests
{
    [SqlServerFact]
    public async Task Last_scope_rejects_body_or_cleanup_boundary_drift_in_both_modes()
    {
        foreach (var mode in new[] { SqlFixtureRunMode.Fresh, SqlFixtureRunMode.Reuse })
        foreach (var mutation in new[] { "schema", "trigger", "journal", "ownership" })
        {
            await WithFixtureAsync(async probe =>
            {
                string? connectionString = null;
                string? owner = null;
                try
                {
                    await Assert.ThrowsAnyAsync<Exception>(() => probe.Fixture.RunTestAsync("last-" + mutation,
                        SeedUserAsync, async (scope, _) =>
                        {
                            connectionString = scope.ConnectionString;
                            using var connection = new SqlConnection(connectionString);
                            if (mutation == "ownership")
                            {
                                owner = await connection.QuerySingleAsync<string>(
                                    "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'MyDmsVn.TestOwnership';");
                            }
                            var sql = mutation == "schema" ? "ALTER TABLE dbo.Users ADD LastScopeDrift int NULL;" :
                                mutation == "trigger" ? "DISABLE TRIGGER dbo.TR_StockLedger_AppendOnly ON dbo.StockLedger;" :
                                mutation == "journal" ? "DELETE dbo.SchemaVersions;" :
                                "EXEC sys.sp_updateextendedproperty @name=N'MyDmsVn.TestOwnership',@value=N'changed';";
                            if (mutation == "schema")
                            {
                                // The check must run after tracked resource cleanup, not just after the body.
                                scope.RegisterCleanup(async () =>
                                {
                                    using var cleanup = new SqlConnection(connectionString);
                                    await cleanup.ExecuteAsync(sql);
                                });
                            }
                            else { await connection.ExecuteAsync(sql); }
                        }));
                    Assert.True(probe.Fixture.IsPoisoned);
                    Assert.Single(probe.Fixture.Observations, o => o.Operation == "verify-after" && !o.Succeeded);
                    await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Fixture.BeginTestAsync("later", SeedUserAsync));
                    Assert.Equal(1, Count(probe.Fixture, "reset"));
                }
                finally
                {
                    if (owner != null)
                    {
                        // Restore only this probe's marker so the original guarded allocator can drop it.
                        using var restore = new SqlConnection(connectionString);
                        await restore.ExecuteAsync("EXEC sys.sp_updateextendedproperty @name=N'MyDmsVn.TestOwnership',@value=@owner;", new { owner });
                    }
                }
            }, mode);
        }
    }

    [SqlServerFact]
    public Task Scenario_failure_preserves_post_body_boundary_failure() => WithFixtureAsync(async probe =>
    {
        var error = await Assert.ThrowsAsync<AggregateException>(() => probe.Fixture.RunTestAsync("body-and-drift",
            SeedUserAsync, async (scope, _) =>
            {
                using var connection = new SqlConnection(scope.ConnectionString);
                await connection.ExecuteAsync("ALTER TABLE dbo.Users ADD BodyFailureDrift int NULL;");
                throw new InvalidOperationException("Expected body failure.");
            }));
        Assert.Contains(error.Flatten().InnerExceptions, e => e.Message == "Expected body failure.");
        Assert.Contains(error.Flatten().InnerExceptions, e => e.Message.Contains("boundary changed"));
        Assert.True(probe.Fixture.IsPoisoned);
    });

    [SqlServerFact]
    public async Task Cleanup_timeout_quarantines_active_sql_and_rejects_waiters_and_teardown()
    {
        foreach (var mode in new[] { SqlFixtureRunMode.Fresh, SqlFixtureRunMode.Reuse })
        {
            var probe = new Probe(mode, null, null, null, TimeSpan.FromMilliseconds(150));
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            SqlFixtureTestScope? scope = null;
            Task worker = Task.CompletedTask;
            var cleanupRan = false;
            try
            {
                scope = await probe.Fixture.BeginTestAsync("uncooperative-worker", SeedUserAsync);
                var connectionString = scope.ConnectionString;
                worker = Task.Run(async () =>
                {
                    using var connection = new SqlConnection(connectionString);
                    await connection.OpenAsync();
                    using var transaction = connection.BeginTransaction();
                    await connection.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) " +
                        "SELECT UserId,N'Catalog.Products.Read',1 FROM dbo.Users;", transaction: transaction);
                    ready.SetResult(true);
                    try { await release.Task; }
                    finally { transaction.Rollback(); }
                });
                Assert.Same(ready.Task, await Task.WhenAny(ready.Task, worker, Task.Delay(TimeSpan.FromSeconds(10))));
                scope.RegisterWorker(worker, () => { });
                scope.RegisterCleanup(() => { cleanupRan = true; return Task.CompletedTask; });
                var later = probe.Fixture.BeginTestAsync("waiting", SeedUserAsync);
                var teardown = probe.Fixture.DisposeAsync();
                var disposal = scope.DisposeAsync();
                Assert.Same(disposal, await Task.WhenAny(disposal, Task.Delay(TimeSpan.FromSeconds(3))));
                var failure = await Assert.ThrowsAnyAsync<Exception>(() => disposal);
                Assert.Contains("TimeoutException", failure.ToString(), StringComparison.Ordinal);
                Assert.Same(later, await Task.WhenAny(later, Task.Delay(TimeSpan.FromSeconds(3))));
                var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() => later);
                Assert.Contains("quarantined", rejected.Message, StringComparison.Ordinal);
                Assert.Same(teardown, await Task.WhenAny(teardown, Task.Delay(TimeSpan.FromSeconds(3))));
                var retained = await Assert.ThrowsAsync<InvalidOperationException>(() => teardown);
                Assert.Contains(probe.Databases.Single().DatabaseName, retained.Message, StringComparison.Ordinal);
                Assert.False(worker.IsCompleted);
                Assert.False(cleanupRan);
                Assert.True(probe.Fixture.IsPoisoned);
                Assert.Equal(1, Count(probe.Fixture, "reset"));
                Assert.Equal(0, Count(probe.Fixture, "drop"));
                using var master = new SqlConnection(Admin);
                Assert.Equal(1, await master.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.databases WHERE name=@name;",
                    new { name = probe.Databases.Single().DatabaseName }));
            }
            finally
            {
                // Only this controlled probe releases/awaits the real transaction. The fixture never auto-drops quarantine.
                release.TrySetResult(true);
                await worker;
                if (scope != null) { await Record.ExceptionAsync(() => scope.DisposeAsync()); }
                await Record.ExceptionAsync(() => probe.Fixture.DisposeAsync());
                foreach (var database in probe.Databases) { await database.DisposeAsync(); }
                await probe.AssertAllAbsentAsync();
            }
        }
    }

    [SqlServerFact]
    public Task Second_scope_cannot_overlap() => WithFixtureAsync(async probe =>
    {
        await probe.Fixture.InitializeAsync();
        var first = await probe.Fixture.BeginTestAsync("first", SeedUserAsync);
        var pending = probe.Fixture.BeginTestAsync("second", SeedUserAsync);
        await RunProbeAndCleanupAsync(async () =>
        {
            Assert.False(pending.IsCompleted);
            Assert.Equal(1, Count(probe.Fixture, "reset"));
            await first.DisposeAsync();
            await (await pending).DisposeAsync();
        }, () => first.DisposeAsync(), async () => await (await pending).DisposeAsync());
        await probe.Fixture.DisposeAsync();
        Assert.Equal(1, Count(probe.Fixture, "allocate"));
        Assert.Equal(1, Count(probe.Fixture, "migrate"));
        Assert.Equal(1, Count(probe.Fixture, "initialize-reset"));
        Assert.Equal(2, Count(probe.Fixture, "reset"));
        Assert.Equal(2, Count(probe.Fixture, "seed"));
        Assert.Equal(2, Count(probe.Fixture, "cleanup"));
        Assert.Equal(1, Count(probe.Fixture, "drop"));
    });

    [SqlServerFact]
    public async Task Reset_or_seed_failure_poisons_fixture()
    {
        foreach (var resetFault in new[] { false, true })
        {
            var enabled = false;
            await WithFixtureAsync(async probe =>
            {
                await probe.Fixture.InitializeAsync();
                enabled = resetFault;
                var entered = false;
                await Assert.ThrowsAnyAsync<Exception>(() => probe.Fixture.RunTestAsync("fault", async (scope, token) =>
                {
                    await SeedUserAsync(scope, token);
                    if (!resetFault) { throw new InvalidOperationException("Expected seed failure."); }
                }, (_, _) => { entered = true; return Task.CompletedTask; }));
                Assert.True(probe.Fixture.IsPoisoned);
                Assert.False(entered);
                await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Fixture.BeginTestAsync("later", SeedUserAsync));
                Assert.Equal(1, Count(probe.Fixture, "allocate"));
            }, prepare: (database, token) => RespawnResetProfile.CreateAsync(database,
                afterTriggerDisabled: async (connection, cancellation) =>
                {
                    if (enabled)
                    {
                        using var command = new SqlCommand("THROW 51099, 'Expected reset failure.', 1;", connection);
                        await command.ExecuteNonQueryAsync(cancellation);
                    }
                }, cancellationToken: token));
        }
    }

    [SqlServerFact]
    public async Task Mismatched_profile_is_rejected_before_admission()
    {
        var other = await SqlTestDatabase.CreateAsync(Admin);
        Exception? failure = null;
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(other.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());
            var wrongProfile = await RespawnResetProfile.CreateAsync(other);
            await WithFixtureAsync(async probe =>
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Fixture.InitializeAsync());
                Assert.True(probe.Fixture.IsPoisoned);
                await probe.AssertAllAbsentAsync();
                await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Fixture.BeginTestAsync("wrong-profile", SeedUserAsync));
            }, prepare: (_, _) => Task.FromResult(wrongProfile));
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try { await other.DisposeAsync(); }
            catch (Exception cleanup) when (failure != null)
            {
                throw new AggregateException("Profile mismatch probe and owned cleanup failed.", failure, cleanup);
            }
        }
    }

    [SqlServerFact]
    public Task Seed_schema_drift_rejects_the_body() => WithFixtureAsync(async probe =>
    {
        var entered = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Fixture.RunTestAsync("seed-drift", async (scope, token) =>
        {
            await SeedUserAsync(scope, token);
            using var connection = new SqlConnection(scope.ConnectionString);
            await connection.ExecuteAsync("CREATE TRIGGER dbo.TR_SeedDrift ON dbo.Users AFTER DELETE AS RETURN;");
        }, (_, _) => { entered = true; return Task.CompletedTask; }));
        Assert.False(entered);
        Assert.True(probe.Fixture.IsPoisoned);
    });

    [SqlServerFact]
    public Task Ownership_change_prevents_reset_ddl() => WithFixtureAsync(async probe =>
    {
        var scope = await probe.Fixture.BeginTestAsync("first", SeedUserAsync);
        using var connection = new SqlConnection(scope.ConnectionString);
        var marker = await connection.QuerySingleAsync<string>(
            "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'MyDmsVn.TestOwnership';");
        await scope.DisposeAsync();
        try
        {
            await connection.ExecuteAsync("EXEC sys.sp_updateextendedproperty @name=N'MyDmsVn.TestOwnership',@value=N'changed';");
            await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Fixture.BeginTestAsync("later", SeedUserAsync));
            Assert.True(probe.Fixture.IsPoisoned);
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.triggers WHERE name=N'TR_StockLedger_AppendOnly' AND is_disabled=0;"));
            Assert.Equal(1, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Users;"));
        }
        finally
        {
            await connection.ExecuteAsync("EXEC sys.sp_updateextendedproperty @name=N'MyDmsVn.TestOwnership',@value=@marker;", new { marker });
        }
    });

    [SqlServerFact]
    public Task Outstanding_worker_blocks_reset_until_drained() => WithFixtureAsync(async probe =>
    {
        var scope = await probe.Fixture.BeginTestAsync("worker", SeedUserAsync);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionString = scope.ConnectionString;
        var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resourcesDisposed = false;
        var worker = Task.Run(async () =>
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            await connection.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) " +
                "SELECT UserId,N'Catalog.Products.Read',1 FROM dbo.Users;", transaction: transaction);
            ready.SetResult(true);
            try { await release.Task; }
            finally { transaction.Rollback(); }
        });
        scope.RegisterWorker(worker, () => stopped.TrySetResult(true));
        scope.RegisterCleanup(() => { resourcesDisposed = true; return Task.CompletedTask; });
        Task<SqlFixtureTestScope>? pending = null;
        await RunProbeAndCleanupAsync(async () =>
        {
            Assert.Same(ready.Task, await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(10))));
            var drain = scope.DisposeAsync();
            pending = probe.Fixture.BeginTestAsync("after-worker", async (next, token) =>
            {
                Assert.True(resourcesDisposed);
                await SeedUserAsync(next, token);
            });
            Assert.Same(stopped.Task, await Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromSeconds(3))));
            Assert.False(drain.IsCompleted);
            Assert.False(pending.IsCompleted);
            Assert.False(resourcesDisposed);
            Assert.Equal(1, Count(probe.Fixture, "reset"));
            release.TrySetResult(true);
            await drain;
            var nextScope = await pending;
            await nextScope.DisposeAsync();
        }, () =>
        {
            release.TrySetResult(true);
            return worker;
        }, () => scope.DisposeAsync(), async () =>
        {
            if (pending != null) { await (await pending).DisposeAsync(); }
        });
    });

    [SqlServerFact]
    public Task Scenario_and_cleanup_errors_are_both_visible_and_cleanup_poisons() => WithFixtureAsync(async probe =>
    {
        var error = await Assert.ThrowsAsync<AggregateException>(() => probe.Fixture.RunTestAsync("both", SeedUserAsync, (scope, _) =>
        {
            scope.RegisterCleanup(() => throw new InvalidOperationException("cleanup-fault"));
            throw new InvalidOperationException("scenario-fault");
        }));
        Assert.Contains(error.Flatten().InnerExceptions, e => e.Message == "scenario-fault");
        Assert.Contains(error.Flatten().InnerExceptions, e => e.Message == "cleanup-fault");
        Assert.True(probe.Fixture.IsPoisoned);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Fixture.BeginTestAsync("later", SeedUserAsync));
    });

    [SqlServerFact]
    public Task Scenario_failure_with_drained_resources_allows_next_reset() => WithFixtureAsync(async probe =>
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Fixture.RunTestAsync("failed-body", SeedUserAsync, async (scope, _) =>
        {
            using var connection = new SqlConnection(scope.ConnectionString);
            await connection.ExecuteAsync("UPDATE dbo.Users SET DisplayName=N'contaminated',IsActive=0;");
            throw new InvalidOperationException("Expected scenario failure.");
        }));
        Assert.False(probe.Fixture.IsPoisoned);
        await probe.Fixture.RunTestAsync("clean-body", SeedUserAsync, async (scope, _) =>
        {
            using var connection = new SqlConnection(scope.ConnectionString);
            Assert.Equal("Baseline", await connection.QuerySingleAsync<string>("SELECT DisplayName FROM dbo.Users;"));
            Assert.True(await connection.QuerySingleAsync<bool>("SELECT IsActive FROM dbo.Users;"));
        });
    });

    [SqlServerFact]
    public Task Fresh_mode_allocates_each_case_with_the_same_reset_and_seed() => WithFixtureAsync(async probe =>
    {
        for (var index = 0; index < 2; index++)
        {
            await probe.Fixture.RunTestAsync("fresh-" + index, SeedUserAsync, (_, _) => Task.CompletedTask);
            await probe.AssertAllAbsentAsync();
        }
        Assert.Equal(2, probe.Databases.Select(d => d.DatabaseName).Distinct().Count());
        foreach (var operation in new[] { "allocate", "migrate", "initialize-reset", "reset", "seed", "verify", "cleanup", "drop" })
        {
            Assert.Equal(2, Count(probe.Fixture, operation));
        }
    }, SqlFixtureRunMode.Fresh);

    [SqlServerFact]
    public Task Initialization_failure_attempts_owned_cleanup_and_never_retries_allocation() => WithFixtureAsync(async probe =>
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Fixture.InitializeAsync());
        Assert.Equal("migration-fault", error.Message);
        Assert.True(probe.Fixture.IsPoisoned);
        await probe.AssertAllAbsentAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Fixture.InitializeAsync());
        Assert.Equal(1, Count(probe.Fixture, "allocate"));
        Assert.Equal(1, Count(probe.Fixture, "drop"));
    }, migrate: (_, _) => throw new InvalidOperationException("migration-fault"));

    [SqlServerFact]
    public async Task Fresh_drop_failure_is_retained_for_guarded_teardown_retry()
    {
        var opens = 0;
        await WithFixtureAsync(async probe =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Fixture.RunTestAsync("drop-fault", SeedUserAsync, (_, _) => Task.CompletedTask));
            Assert.True(probe.Fixture.IsPoisoned);
            Assert.Single(probe.Fixture.Observations, o => o.Operation == "drop" && !o.Succeeded);
            await probe.Fixture.DisposeAsync();
            await probe.AssertAllAbsentAsync();
            Assert.Single(probe.Fixture.Observations, o => o.Operation == "drop" && o.Succeeded);
        }, SqlFixtureRunMode.Fresh, allocate: token => SqlTestDatabase.CreateAsync(Admin, token, value =>
        {
            opens++;
            if (opens == 2) { throw new InvalidOperationException("Expected guarded drop fault."); }
            return new SqlConnection(value);
        }, _ => { }));
    }

    [SqlServerFact]
    public Task Fixture_disposal_waits_for_scope_and_rejects_new_admission() => WithFixtureAsync(async probe =>
    {
        var scope = await probe.Fixture.BeginTestAsync("active", SeedUserAsync);
        var disposal = probe.Fixture.DisposeAsync();
        var pending = probe.Fixture.BeginTestAsync("late", SeedUserAsync);
        Assert.False(disposal.IsCompleted);
        await scope.DisposeAsync();
        await disposal;
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        Assert.Equal(1, Count(probe.Fixture, "drop"));
    });

    [SqlServerFact]
    public async Task Reset_cancellation_drains_before_rejecting_the_waiting_scope()
    {
        var observe = false;
        var session = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await WithFixtureAsync(async probe =>
        {
            await probe.Fixture.InitializeAsync();
            using var holder = new SqlConnection(probe.Databases.Single().ConnectionString);
            await holder.OpenAsync();
            using var transaction = holder.BeginTransaction();
            await holder.ExecuteAsync("SELECT COUNT(*) FROM dbo.Users WITH(TABLOCKX,HOLDLOCK);", transaction: transaction);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            observe = true;
            var reset = probe.Fixture.BeginTestAsync("cancelled-reset", SeedUserAsync, cancellation.Token);
            var later = probe.Fixture.BeginTestAsync("waiting-scope", SeedUserAsync);
            try
            {
                Assert.Same(session.Task, await Task.WhenAny(session.Task, Task.Delay(TimeSpan.FromSeconds(10))));
                var resetSession = await session.Task;
                using var observer = new SqlConnection(holder.ConnectionString);
                await observer.OpenAsync();
                var deadline = DateTime.UtcNow.AddSeconds(10);
                var blocked = false;
                while (DateTime.UtcNow < deadline && !reset.IsCompleted)
                {
                    blocked = await observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_exec_requests " +
                        "WHERE session_id=@resetSession AND blocking_session_id=@holder;",
                        new { resetSession, holder = holder.ServerProcessId }, commandTimeout: 5) == 1;
                    if (blocked) { break; }
                    await Task.Delay(25);
                }
                Assert.True(blocked);
                Assert.False(later.IsCompleted);
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<Exception>(() => reset);
                await Assert.ThrowsAsync<InvalidOperationException>(() => later);
                Assert.True(probe.Fixture.IsPoisoned);
                Assert.Equal(0, await observer.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@resetSession;", new { resetSession }));
            }
            finally
            {
                cancellation.Cancel();
                await Record.ExceptionAsync(() => reset);
                await Record.ExceptionAsync(() => later);
                transaction.Rollback();
            }
        }, prepare: (database, token) => RespawnResetProfile.CreateAsync(database, afterTriggerDisabled: (connection, _) =>
        {
            if (observe) { session.TrySetResult(connection.ServerProcessId); }
            return Task.CompletedTask;
        }, cancellationToken: token));
    }

    [Fact]
    public async Task Pending_probe_cleanup_preserves_failure_and_disposes_the_admitted_scope()
    {
        var disposed = false;
        var pending = new TaskCompletionSource<SqlFixtureTestScope>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new SqlFixtureTestScope("pending", "owned-test-connection", _ =>
        {
            disposed = true;
            return Task.CompletedTask;
        });
        var error = await Assert.ThrowsAsync<AggregateException>(() => RunProbeAndCleanupAsync(
            () => throw new ArgumentException("original-assertion"),
            () => { pending.SetResult(second); throw new InvalidOperationException("first-cleanup"); },
            async () => await (await pending.Task).DisposeAsync()));
        Assert.True(disposed);
        Assert.Contains(error.Flatten().InnerExceptions, e => e.Message == "original-assertion");
        Assert.Contains(error.Flatten().InnerExceptions, e => e.Message == "first-cleanup");
    }

    private static async Task RunProbeAndCleanupAsync(Func<Task> body, params Func<Task>[] cleanup)
    {
        var errors = new List<Exception>();
        try { await body(); } catch (Exception error) { errors.Add(error); }
        foreach (var action in cleanup)
        {
            try { await action(); } catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count > 1) { throw new AggregateException("Probe and pending scope cleanup failed.", errors); }
        if (errors.Count == 1) { ExceptionDispatchInfo.Capture(errors[0]).Throw(); }
    }

    private static string Admin => Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
    private static int Count(ReusableSqlFixture fixture, string operation) => fixture.Observations.Count(o => o.Operation == operation);

    internal static async Task SeedUserAsync(SqlFixtureTestScope scope, CancellationToken token)
    {
        using var connection = new SqlConnection(scope.ConnectionString);
        await connection.ExecuteAsync(new SqlTestCommand(@"
INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm)
VALUES(N'fixture-user',N'FIXTURE-USER',N'Baseline',N'Local',N'hash',N'',N'BCrypt');", cancellationToken: token));
        Assert.Equal(1, await connection.QuerySingleAsync<int>(new SqlTestCommand("SELECT COUNT(*) FROM dbo.Users;", cancellationToken: token)));
        Assert.Equal(0L, await connection.QuerySingleAsync<long>(new SqlTestCommand("SELECT AssignmentRevision FROM dbo.Users;", cancellationToken: token)));
    }

    private static async Task WithFixtureAsync(Func<Probe, Task> scenario, SqlFixtureRunMode mode = SqlFixtureRunMode.Reuse,
        Func<string, CancellationToken, Task>? migrate = null,
        Func<SqlTestDatabase, CancellationToken, Task<RespawnResetProfile>>? prepare = null,
        Func<CancellationToken, Task<SqlTestDatabase>>? allocate = null)
    {
        var probe = new Probe(mode, migrate, prepare, allocate);
        Exception? failure = null;
        try { await scenario(probe); }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try { await probe.Fixture.DisposeAsync(); await probe.AssertAllAbsentAsync(); }
            catch (Exception cleanup) when (failure != null)
            {
                throw new AggregateException("Lifecycle probe and guarded cleanup failed.", failure, cleanup);
            }
        }
    }

    private sealed class Probe
    {
        public Probe(SqlFixtureRunMode mode, Func<string, CancellationToken, Task>? migrate,
            Func<SqlTestDatabase, CancellationToken, Task<RespawnResetProfile>>? prepare,
            Func<CancellationToken, Task<SqlTestDatabase>>? allocate, TimeSpan? cleanupTimeout = null)
        {
            Fixture = new ReusableSqlFixture(mode, async token =>
            {
                var database = await (allocate?.Invoke(token) ?? SqlTestDatabase.CreateAsync(Admin, token));
                Databases.Add(database);
                return database;
            }, migrate ?? ((connectionString, token) =>
            {
                token.ThrowIfCancellationRequested();
                var result = new DatabaseMigrationRunner().Migrate(connectionString);
                if (!result.Successful) { throw result.Error ?? new InvalidOperationException("Migration failed."); }
                return Task.CompletedTask;
            }), prepare ?? ((database, token) => RespawnResetProfile.CreateAsync(database, cancellationToken: token)), cleanupTimeout);
        }
        public ReusableSqlFixture Fixture { get; }
        public List<SqlTestDatabase> Databases { get; } = new List<SqlTestDatabase>();
        public async Task AssertAllAbsentAsync()
        {
            using var master = new SqlConnection(Admin);
            foreach (var database in Databases)
            {
                Assert.Equal(0, await master.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.databases WHERE name=@name;", new { name = database.DatabaseName }));
            }
        }
    }
}
