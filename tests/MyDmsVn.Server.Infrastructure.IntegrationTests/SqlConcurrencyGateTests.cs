using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SqlConcurrencyGateTests
{
    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Holder_waits_for_controller_release_when_observation_is_delayed_past_ten_seconds()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var database = await SqlTestDatabase.CreateAsync(adminConnectionString);
        SqlConcurrencyGate? gate = null;
        SqlConnection? holderConnection = null;
        SqlTransaction? holderTransaction = null;
        Task<int>? holder = null;
        using var holderCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try
        {
            using (var setup = new SqlConnection(database.ConnectionString))
            {
                await setup.ExecuteAsync(
                    "CREATE TABLE dbo.TestGateDelayedObservation " +
                    "(Id int NOT NULL CONSTRAINT PK_TestGateDelayedObservation PRIMARY KEY, Value int NOT NULL); " +
                    "INSERT dbo.TestGateDelayedObservation (Id, Value) VALUES (1, 0);");
            }

            gate = await SqlConcurrencyGate.AcquireAsync(
                database.ConnectionString,
                "delayed-observation",
                CancellationToken.None);
            using (var setup = new SqlConnection(database.ConnectionString))
            {
                await setup.ExecuteAsync(
                    "CREATE TRIGGER dbo.TR_TestGateDelayedObservation " +
                    "ON dbo.TestGateDelayedObservation AFTER UPDATE AS " +
                    "BEGIN SET NOCOUNT ON; " + gate.TransactionWaitSql + " END;");
            }

            holderConnection = new SqlConnection(database.ConnectionString);
            await holderConnection.OpenAsync();
            var holderSessionId = await holderConnection.QuerySingleAsync<int>("SELECT @@SPID;");
            holderTransaction = holderConnection.BeginTransaction();
            holder = holderConnection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE dbo.TestGateDelayedObservation SET Value = Value + 1 WHERE Id = 1;",
                    transaction: holderTransaction,
                    cancellationToken: holderCancellation.Token));
            await gate.WaitForHolderAsync(
                holderSessionId,
                "dbo.TestGateDelayedObservation",
                TimeSpan.FromSeconds(5),
                CancellationToken.None);

            await Task.Delay(TimeSpan.FromSeconds(11));
            Assert.False(holder.IsCompleted);

            await gate.ReleaseAsync(CancellationToken.None);
            Assert.Equal(1, await holder);
            holderTransaction.Commit();
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                holderCancellation,
                () => DisposeDatabaseResourcesAsync(
                    database,
                    holderTransaction,
                    holderConnection),
                holder);
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Canceled_observation_releases_gate_and_drains_the_holder_before_database_disposal()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var database = await SqlTestDatabase.CreateAsync(adminConnectionString);
        SqlConcurrencyGate? gate = null;
        SqlConnection? holderConnection = null;
        SqlTransaction? holderTransaction = null;
        Task<int>? holder = null;
        using var holderCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            using (var setup = new SqlConnection(database.ConnectionString))
            {
                await setup.ExecuteAsync(
                    "CREATE TABLE dbo.TestGateCancellation " +
                    "(Id int NOT NULL CONSTRAINT PK_TestGateCancellation PRIMARY KEY, Value int NOT NULL); " +
                    "INSERT dbo.TestGateCancellation (Id, Value) VALUES (1, 0);");
            }

            gate = await SqlConcurrencyGate.AcquireAsync(
                database.ConnectionString,
                "observer-cancellation",
                CancellationToken.None);
            using (var setup = new SqlConnection(database.ConnectionString))
            {
                await setup.ExecuteAsync(
                    "CREATE TRIGGER dbo.TR_TestGateCancellation ON dbo.TestGateCancellation AFTER UPDATE AS " +
                    "BEGIN SET NOCOUNT ON; " + gate.TransactionWaitSql + " END;");
            }

            holderConnection = new SqlConnection(database.ConnectionString);
            await holderConnection.OpenAsync();
            var holderSessionId = await holderConnection.QuerySingleAsync<int>("SELECT @@SPID;");
            holderTransaction = holderConnection.BeginTransaction();
            holder = holderConnection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE dbo.TestGateCancellation SET Value = Value + 1 WHERE Id = 1;",
                    transaction: holderTransaction,
                    cancellationToken: holderCancellation.Token));
            await gate.WaitForHolderAsync(
                holderSessionId,
                "dbo.TestGateCancellation",
                TimeSpan.FromSeconds(5),
                CancellationToken.None);

            using var canceledObserver = new CancellationTokenSource();
            var observation = gate.WaitForBusinessBlockAsync(
                int.MaxValue,
                holderSessionId,
                "dbo.TestGateCancellation",
                TimeSpan.FromSeconds(5),
                canceledObserver.Token);
            await Task.Delay(50);
            Assert.False(observation.IsCompleted);
            canceledObserver.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observation);
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                holderCancellation,
                () => DisposeDatabaseResourcesAsync(
                    database,
                    holderTransaction,
                    holderConnection),
                holder);
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Canceled_release_can_be_retried_without_leaving_the_controller_lock_held()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var database = await SqlTestDatabase.CreateAsync(adminConnectionString);
        SqlConcurrencyGate? gate = null;
        using var cleanupCancellation = new CancellationTokenSource();
        try
        {
            gate = await SqlConcurrencyGate.AcquireAsync(
                database.ConnectionString,
                "release-retry",
                CancellationToken.None);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => gate.ReleaseAsync(canceled.Token));

            await gate.ReleaseAsync(CancellationToken.None);

            using var contender = new SqlConnection(database.ConnectionString);
            var result = await contender.QuerySingleAsync<int>(
                "DECLARE @result int; EXEC @result = sys.sp_getapplock " +
                "@Resource = @resourceName, @LockMode = N'Exclusive', " +
                "@LockOwner = N'Session', @LockTimeout = 0; SELECT @result;",
                new { resourceName = gate.ResourceName });
            Assert.True(result >= 0, "The controller APP lock remained held after a retried release.");
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                cleanupCancellation,
                database.DisposeAsync);
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Observer_identifies_app_gate_holder_and_business_block_without_reading_uncommitted_signal()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var database = await SqlTestDatabase.CreateAsync(adminConnectionString);
        SqlConcurrencyGate? gate = null;
        SqlConnection? holderConnection = null;
        SqlTransaction? holderTransaction = null;
        SqlConnection? contenderConnection = null;
        SqlTransaction? contenderTransaction = null;
        Task<int>? holder = null;
        Task<int>? contender = null;
        using var cleanupCancellation = new CancellationTokenSource();

        try
        {
            using (var setup = new SqlConnection(database.ConnectionString))
            {
                await setup.ExecuteAsync(
                    "CREATE TABLE dbo.TestGateBusiness " +
                    "(Id int NOT NULL CONSTRAINT PK_TestGateBusiness PRIMARY KEY, Value int NOT NULL); " +
                    "INSERT dbo.TestGateBusiness (Id, Value) VALUES (1, 0);");
            }

            gate = await SqlConcurrencyGate.AcquireAsync(
                database.ConnectionString,
                "observer-probe",
                CancellationToken.None);
            using (var setup = new SqlConnection(database.ConnectionString))
            {
                await setup.ExecuteAsync(
                    "CREATE TRIGGER dbo.TR_TestGateBusiness ON dbo.TestGateBusiness AFTER UPDATE AS " +
                    "BEGIN SET NOCOUNT ON; " + gate.TransactionWaitSql + " END;");
            }

            holderConnection = new SqlConnection(database.ConnectionString);
            await holderConnection.OpenAsync();
            var holderSessionId = await holderConnection.QuerySingleAsync<int>("SELECT @@SPID;");
            holderTransaction = holderConnection.BeginTransaction();
            holder = holderConnection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE dbo.TestGateBusiness SET Value = Value + 1 WHERE Id = 1;",
                    transaction: holderTransaction,
                    cancellationToken: cleanupCancellation.Token));

            var holderObservation = await gate.WaitForHolderAsync(
                holderSessionId,
                "dbo.TestGateBusiness",
                TimeSpan.FromSeconds(5),
                CancellationToken.None);

            Assert.Equal(gate.ControllerSessionId, holderObservation.BlockingSessionId);
            Assert.StartsWith("LCK_M_", holderObservation.WaitType, StringComparison.Ordinal);
            Assert.False(holder.IsCompleted);

            contenderConnection = new SqlConnection(database.ConnectionString);
            await contenderConnection.OpenAsync();
            var contenderSessionId = await contenderConnection.QuerySingleAsync<int>("SELECT @@SPID;");
            contenderTransaction = contenderConnection.BeginTransaction();
            contender = contenderConnection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE dbo.TestGateBusiness SET Value = Value + 1 WHERE Id = 1;",
                    transaction: contenderTransaction,
                    cancellationToken: cleanupCancellation.Token));

            var contenderObservation = await gate.WaitForBusinessBlockAsync(
                contenderSessionId,
                holderSessionId,
                "dbo.TestGateBusiness",
                TimeSpan.FromSeconds(5),
                CancellationToken.None);

            Assert.Equal(holderSessionId, contenderObservation.BlockingSessionId);
            Assert.StartsWith("LCK_M_", contenderObservation.WaitType, StringComparison.Ordinal);
            Assert.False(contender.IsCompleted);

            await gate.ReleaseAsync(CancellationToken.None);
            Assert.Equal(1, await holder);
            holderTransaction.Commit();
            Assert.Equal(1, await contender);
            contenderTransaction.Commit();
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                cleanupCancellation,
                () => DisposeDatabaseResourcesAsync(
                    database,
                    contenderTransaction,
                    contenderConnection,
                    holderTransaction,
                    holderConnection),
                holder,
                contender);
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Failure_after_confirmed_business_block_cancels_tasks_before_database_disposal()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var database = await SqlTestDatabase.CreateAsync(adminConnectionString);
        var databaseName = database.DatabaseName;
        SqlConcurrencyGate? gate = null;
        SqlConnection? holderConnection = null;
        SqlTransaction? holderTransaction = null;
        SqlConnection? contenderConnection = null;
        SqlTransaction? contenderTransaction = null;
        Task<int>? holder = null;
        Task<int>? contender = null;
        using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            using (var setup = new SqlConnection(database.ConnectionString))
            {
                await setup.ExecuteAsync(
                    "CREATE TABLE dbo.TestGateInjectedFailure " +
                    "(Id int NOT NULL CONSTRAINT PK_TestGateInjectedFailure PRIMARY KEY, Value int NOT NULL); " +
                    "INSERT dbo.TestGateInjectedFailure (Id, Value) VALUES (1, 0);");
            }

            gate = await SqlConcurrencyGate.AcquireAsync(
                database.ConnectionString,
                "failure-before-release",
                CancellationToken.None);
            using (var setup = new SqlConnection(database.ConnectionString))
            {
                await setup.ExecuteAsync(
                    "CREATE TRIGGER dbo.TR_TestGateInjectedFailure ON dbo.TestGateInjectedFailure AFTER UPDATE AS " +
                    "BEGIN SET NOCOUNT ON; " + gate.TransactionWaitSql + " END;");
            }

            holderConnection = new SqlConnection(database.ConnectionString);
            await holderConnection.OpenAsync();
            var holderSessionId = await holderConnection.QuerySingleAsync<int>("SELECT @@SPID;");
            holderTransaction = holderConnection.BeginTransaction();
            holder = holderConnection.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.TestGateInjectedFailure SET Value = Value + 1 WHERE Id = 1;",
                transaction: holderTransaction,
                cancellationToken: cleanupCancellation.Token));
            await gate.WaitForHolderAsync(
                holderSessionId,
                "dbo.TestGateInjectedFailure",
                TimeSpan.FromSeconds(5),
                CancellationToken.None);

            contenderConnection = new SqlConnection(database.ConnectionString);
            await contenderConnection.OpenAsync();
            var contenderSessionId = await contenderConnection.QuerySingleAsync<int>("SELECT @@SPID;");
            contenderTransaction = contenderConnection.BeginTransaction();
            contender = contenderConnection.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.TestGateInjectedFailure SET Value = Value + 1 WHERE Id = 1;",
                transaction: contenderTransaction,
                cancellationToken: cleanupCancellation.Token));
            await gate.WaitForBusinessBlockAsync(
                contenderSessionId,
                holderSessionId,
                "dbo.TestGateInjectedFailure",
                TimeSpan.FromSeconds(5),
                CancellationToken.None);

            Assert.False(holder.IsCompleted);
            Assert.False(contender.IsCompleted);
            // Simulate an assertion failure here by leaving through finally before controller release.
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                cleanupCancellation,
                () => DisposeDatabaseResourcesAsync(
                    database,
                    contenderTransaction,
                    contenderConnection,
                    holderTransaction,
                    holderConnection),
                holder,
                contender);
        }

        Assert.True(holder!.IsCompleted);
        Assert.True(contender!.IsCompleted);
        using var admin = new SqlConnection(adminConnectionString);
        Assert.Equal(0, await admin.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sys.databases WHERE name = @databaseName;",
            new { databaseName }));
    }

    [Fact]
    public async Task Cleanup_attempts_every_step_after_task_completion_even_when_release_fails()
    {
        using var cancellation = new CancellationTokenSource();
        var completedTask = Task.CompletedTask;
        var controllerClosed = false;
        var guardedCleanupCalled = false;

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            SqlConcurrencyCleanup.CompleteAsync(
                () => Task.FromException(new InvalidOperationException("injected release failure")),
                () => controllerClosed = true,
                cancellation,
                () =>
                {
                    guardedCleanupCalled = true;
                    return Task.CompletedTask;
                },
                TimeSpan.FromMilliseconds(25),
                completedTask));

        Assert.Contains(exception.InnerExceptions, error =>
            error is InvalidOperationException && error.Message == "injected release failure");
        Assert.True(controllerClosed);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(guardedCleanupCalled);
        Assert.True(completedTask.IsCompleted);
    }

    [Fact]
    public async Task Cleanup_reports_task_that_remains_stalled_after_guarded_cleanup()
    {
        using var cancellation = new CancellationTokenSource();
        var stalled = new TaskCompletionSource<object?>();
        var guardedCleanupCalled = false;

        await Assert.ThrowsAsync<SqlConcurrencyCleanupIncompleteException>(() =>
            SqlConcurrencyCleanup.CompleteAsync(
                release: null,
                closeController: null,
                cancellation,
                () =>
                {
                    guardedCleanupCalled = true;
                    return Task.CompletedTask;
                },
                TimeSpan.FromMilliseconds(25),
                stalled.Task));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(guardedCleanupCalled);
    }

    [Fact]
    public async Task Cleanup_keeps_root_provider_alive_until_started_tasks_are_drained()
    {
        using var cancellation = new CancellationTokenSource();
        var services = new ServiceCollection();
        services.AddSingleton<DisposalTracker>();
        var provider = services.BuildServiceProvider();
        var tracker = provider.GetRequiredService<DisposalTracker>();
        var stalled = new TaskCompletionSource<object?>();
        var guardedCleanupCalled = false;

        var cleanup = SqlConcurrencyCleanup.CompleteAsync(
            release: null,
            closeController: null,
            cancellation,
            provider,
            () =>
            {
                Assert.True(tracker.IsDisposed);
                guardedCleanupCalled = true;
                return Task.CompletedTask;
            },
            TimeSpan.FromSeconds(1),
            stalled.Task);

        await Task.Delay(50);
        Assert.False(tracker.IsDisposed);
        Assert.False(guardedCleanupCalled);

        stalled.SetResult(null);
        await cleanup;

        Assert.True(tracker.IsDisposed);
        Assert.True(guardedCleanupCalled);
    }

    private static async Task DisposeDatabaseResourcesAsync(
        SqlTestDatabase database,
        params IDisposable?[] resources)
    {
        var errors = new List<Exception>();
        foreach (var resource in resources)
        {
            try
            {
                resource?.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        try
        {
            await database.DisposeAsync();
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }

        if (errors.Count != 0)
        {
            throw new AggregateException("SQL concurrency resource disposal failed.", errors);
        }
    }

    private sealed class DisposalTracker : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
