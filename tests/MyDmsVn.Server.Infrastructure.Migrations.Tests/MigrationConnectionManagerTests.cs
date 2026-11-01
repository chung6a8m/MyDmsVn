using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using DbUp.Engine;
using DbUp.Engine.Output;
using DbUp.Engine.Transactions;
using DbUp.SqlServer;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Testing.Sql;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class MigrationConnectionManagerTests
{
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Failed_DbUp_initialization_after_transfer_closes_connection_and_releases_lock()
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? failure = null;
        try
        {
            SqlConnection transferred;
            using (var lease = DatabaseMigrationLock.Acquire(db.ConnectionString))
            {
                transferred = lease.Connection;
                var ownershipTransferred = false;
                var manager = new MigrationConnectionManager(new SqlConnectionManager(() =>
                {
                    var connection = lease.TransferConnectionOwnership();
                    ownershipTransferred = true;
                    return connection;
                }), lease.ReleaseSessionLock);
                manager.TransactionMode = (TransactionMode)999;
                Assert.Throws<KeyNotFoundException>(() => manager.OperationStarting(new NoOpUpgradeLog(), new List<SqlScript>()));
                Assert.True(ownershipTransferred);
                Assert.Equal(ConnectionState.Open, transferred.State);
            }
            Assert.Equal(ConnectionState.Closed, transferred.State);
            using var observer = new SqlConnection(db.ConnectionString);
            await observer.OpenAsync();
            Assert.True(await observer.QuerySingleAsync<int>("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'MyDmsVn.SchemaMigration',@LockOwner=N'Session',@LockMode=N'Exclusive',@LockTimeout=0; SELECT @r;") >= 0);
            await observer.ExecuteAsync("EXEC sys.sp_releaseapplock @Resource=N'MyDmsVn.SchemaMigration',@LockOwner=N'Session';");
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try { await db.DisposeAsync(); }
            catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); }
        }
    }

    [Fact]
    public void Release_failure_still_disposes_scope_and_preserves_both_failures_once()
    {
        var events = new List<string>();
        var releaseError = new InvalidOperationException("release");
        var disposeError = new InvalidOperationException("dispose");
        var inner = new FakeManager(() => { events.Add("dispose"); throw disposeError; });
        var manager = new MigrationConnectionManager(inner, () => { events.Add("release"); throw releaseError; });
        var operation = manager.OperationStarting(new NoOpUpgradeLog(), new List<SqlScript>());
        var failure = Assert.Throws<AggregateException>(() => operation.Dispose());
        Assert.Equal(new[] { releaseError, disposeError }, failure.InnerExceptions);
        operation.Dispose();
        Assert.Equal(new[] { "release", "dispose" }, events);
    }

    private sealed class FakeManager : IConnectionManager
    {
        private readonly Action _dispose;
        public FakeManager(Action dispose) { _dispose = dispose; }
        public IDisposable OperationStarting(IUpgradeLog log, List<SqlScript> scripts) => new Scope(_dispose);
        public void ExecuteCommandsWithManagedConnection(Action<Func<IDbCommand>> action) => throw new NotSupportedException();
        public T ExecuteCommandsWithManagedConnection<T>(Func<Func<IDbCommand>, T> action) => throw new NotSupportedException();
        public TransactionMode TransactionMode { get; set; }
        public bool IsScriptOutputLogged { get; set; }
        public int? ExecutionTimeoutSeconds { get; set; }
        public IEnumerable<string> SplitScriptIntoCommands(string contents) => throw new NotSupportedException();
        public bool TryConnect(IUpgradeLog log, out string error) { error = string.Empty; return true; }
        private sealed class Scope : IDisposable
        {
            private readonly Action _dispose;
            public Scope(Action dispose) { _dispose = dispose; }
            public void Dispose() => _dispose();
        }
    }
}
