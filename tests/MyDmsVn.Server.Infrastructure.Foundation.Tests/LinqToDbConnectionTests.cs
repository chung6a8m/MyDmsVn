using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using LinqToDB.Mapping;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class LinqToDbConnectionTests
{
    [SqlServerFact]
    public Task Borrowed_wrapper_uses_current_connection_transaction_and_leaves_rollback_to_owner() =>
        WithDatabaseAsync(async database =>
        {
            var connections = new SqlConnectionFactory(database.ConnectionString);
            using var uow = new SqlUnitOfWorkFactory(connections).Create();
            var context = (ISqlExecutionContext)uow;
            await NativeAsync((SqlConnection)context.Connection, "CREATE TABLE dbo.LinqProbe (Id int NOT NULL);");
            uow.BeginTransaction();
            var factory = CreateFactory(connections);
            using (var session = factory.Borrow(context, true))
            {
                await session.ExecuteAsync("INSERT dbo.LinqProbe VALUES (42);", CancellationToken.None);
                Assert.Same(context.Connection, session.TryGetDbConnection());
                Assert.Same(context.Transaction, session.Transaction);
                Assert.Equal(42, await NativeAsync((SqlConnection)context.Connection,
                    "SELECT Id FROM dbo.LinqProbe;", (SqlTransaction)context.Transaction!));
            }
            Assert.Equal(ConnectionState.Open, context.Connection.State);
            using (var session = factory.Borrow(context, true))
                Assert.Equal(42, await session.ExecuteAsync<int>("SELECT Id FROM dbo.LinqProbe;", CancellationToken.None));
            uow.Rollback();
            using var verify = await connections.OpenConnectionAsync(CancellationToken.None);
            Assert.Equal(0, await NativeAsync((SqlConnection)verify, "SELECT COUNT(*) FROM dbo.LinqProbe;"));
        });

    [SqlServerFact]
    public Task Repository_resolved_and_read_before_begin_binds_the_later_transaction() =>
        WithDatabaseAsync(async database =>
        {
            var services = new ServiceCollection();
            services.AddSqlPersistence(database.ConnectionString).AddRepository<ILateRepository, LateRepository>();
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().Create();
            var repository = uow.Repository<ILateRepository>();
            Assert.Equal(1, await repository.ReadAsync());
            uow.BeginTransaction();
            Assert.Equal(1, await repository.ReadTransactionCountAsync());
            uow.Commit();
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReadAsync());
        });

    [SqlServerFact]
    public Task Completed_and_disposed_contexts_are_rejected_before_a_wrapper_is_created() =>
        WithDatabaseAsync(async database =>
        {
            var connections = new SqlConnectionFactory(database.ConnectionString);
            var factory = CreateFactory(connections);
            using (var committed = new SqlUnitOfWorkFactory(connections).Create())
            {
                committed.BeginTransaction(); committed.Commit();
                Assert.Throws<InvalidOperationException>(() => factory.Borrow((ISqlExecutionContext)committed, false));
            }
            using (var rolledBack = new SqlUnitOfWorkFactory(connections).Create())
            {
                rolledBack.BeginTransaction(); rolledBack.Rollback();
                Assert.Throws<InvalidOperationException>(() => factory.Borrow((ISqlExecutionContext)rolledBack, false));
            }
            var disposed = new SqlUnitOfWorkFactory(connections).Create(); disposed.Dispose();
            Assert.Throws<ObjectDisposedException>(() => factory.Borrow((ISqlExecutionContext)disposed, false));
            await Task.CompletedTask;
        });

    [SqlServerFact]
    public Task Missing_required_transaction_is_rejected_without_closing_owner_connection() =>
        WithDatabaseAsync(async database =>
        {
            var connections = new SqlConnectionFactory(database.ConnectionString);
            using var uow = new SqlUnitOfWorkFactory(connections).Create();
            Assert.Throws<InvalidOperationException>(() => CreateFactory(connections).Borrow((ISqlExecutionContext)uow, true));
            Assert.Equal(1, await NativeAsync((SqlConnection)((ISqlExecutionContext)uow).Connection, "SELECT 1;"));
        });

    [SqlServerFact]
    public Task Transaction_from_another_connection_is_rejected_without_ending_either_session() =>
        WithDatabaseAsync(async database =>
        {
            var connections = new SqlConnectionFactory(database.ConnectionString);
            using var first = (SqlConnection)await connections.OpenConnectionAsync(CancellationToken.None);
            using var second = (SqlConnection)await connections.OpenConnectionAsync(CancellationToken.None);
            using var transaction = second.BeginTransaction();
            Assert.Throws<InvalidOperationException>(() => CreateFactory(connections).Borrow(new Context(first, transaction), true));
            Assert.Equal(1, await NativeAsync(first, "SELECT 1;"));
            Assert.Equal(1, await NativeAsync(second, "SELECT @@TRANCOUNT;", transaction));
        });

    [Fact]
    public void Wrong_provider_and_closed_connection_are_rejected_without_opening_a_fallback()
    {
        var connections = new UnexpectedConnectionFactory();
        var factory = CreateFactory(connections);
        using var wrong = new System.Data.SqlClient.SqlConnection();
        using var closed = new SqlConnection();
        Assert.Throws<ArgumentException>(() => factory.Borrow(new Context(wrong), false));
        Assert.Throws<InvalidOperationException>(() => factory.Borrow(new Context(closed), false));
        Assert.Equal(ConnectionState.Closed, closed.State);
    }

    [SqlServerFact]
    public Task Borrowed_query_failure_and_cancellation_leave_the_transaction_usable() =>
        WithDatabaseAsync(async database =>
        {
            var connections = new SqlConnectionFactory(database.ConnectionString);
            using var uow = new SqlUnitOfWorkFactory(connections).Create(); uow.BeginTransaction();
            var context = (ISqlExecutionContext)uow;
            using (var session = CreateFactory(connections).Borrow(context, true))
            {
                await Assert.ThrowsAsync<SqlException>(() => session.ExecuteAsync<int>(
                    "SELECT MissingColumn FROM sys.objects;", CancellationToken.None));
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
                var error = await Record.ExceptionAsync(() => session.ExecuteAsync<int>(
                    "WAITFOR DELAY '00:00:10'; SELECT 1;", cancellation.Token));
                Assert.True(error is OperationCanceledException || error is SqlException { Number: 0 },
                    "Expected SqlClient cancellation; query must not complete normally.");
                Assert.True(cancellation.IsCancellationRequested);
                Assert.Equal(1, await session.ExecuteAsync<int>("SELECT @@TRANCOUNT;", CancellationToken.None));
            }
            Assert.Equal(ConnectionState.Open, context.Connection.State);
            uow.Rollback();
        });

    [SqlServerFact]
    public Task Disposing_an_incomplete_reader_and_borrowed_wrapper_leaves_owner_usable() =>
        WithDatabaseAsync(async database =>
        {
            var connections = new SqlConnectionFactory(database.ConnectionString);
            using var uow = new SqlUnitOfWorkFactory(connections).Create(); uow.BeginTransaction();
            var context = (ISqlExecutionContext)uow;
            using (var session = CreateFactory(connections).Borrow(context, true))
            using (var reader = await session.ExecuteReaderAsync("SELECT 1 UNION ALL SELECT 2;", CancellationToken.None))
            {
                Assert.True(await reader.Reader!.ReadAsync());
                Assert.Equal(1, reader.Reader.GetInt32(0));
            }
            Assert.Equal(1, await NativeAsync((SqlConnection)context.Connection, "SELECT @@TRANCOUNT;", (SqlTransaction)context.Transaction!));
            uow.Commit();
        });

    [SqlServerFact]
    public Task Owned_wrapper_disposes_its_connection_and_independent_uows_keep_distinct_sessions() =>
        WithDatabaseAsync(async database =>
        {
            var connections = new SqlConnectionFactory(database.ConnectionString);
            var factory = CreateFactory(connections);
            SqlConnection ownedConnection;
            using (var owned = await factory.OpenOwnedAsync(CancellationToken.None))
            {
                Assert.Equal(1, await owned.ExecuteAsync<int>("SELECT 1;", CancellationToken.None));
                ownedConnection = Assert.IsType<SqlConnection>(owned.TryGetDbConnection());
            }
            Assert.Equal(ConnectionState.Closed, ownedConnection.State);
            using var first = new SqlUnitOfWorkFactory(connections).Create();
            using var second = new SqlUnitOfWorkFactory(connections).Create();
            first.BeginTransaction(); second.BeginTransaction();
            using var a = factory.Borrow((ISqlExecutionContext)first, true);
            using var b = factory.Borrow((ISqlExecutionContext)second, true);
            Assert.NotSame(((ISqlExecutionContext)first).Connection, ((ISqlExecutionContext)second).Connection);
            Assert.NotEqual(await a.ExecuteAsync<int>("SELECT @@SPID;", CancellationToken.None),
                await b.ExecuteAsync<int>("SELECT @@SPID;", CancellationToken.None));
            first.Rollback();
            Assert.Equal(1, await b.ExecuteAsync<int>("SELECT @@TRANCOUNT;", CancellationToken.None));
            second.Rollback();
        });

    [SqlServerFact]
    public Task Owned_construction_failure_closes_opened_connection_and_allows_mapping_retry() =>
        WithDatabaseAsync(async database =>
        {
            var connections = new CapturingConnectionFactory(database.ConnectionString);
            var mapping = new FailingMapping();
            var factory = new LinqToDbSessionFactory(connections, new LinqToDbMappingSchemaProvider(new[] { mapping }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => factory.OpenOwnedAsync(CancellationToken.None));
            Assert.NotNull(connections.Last);
            Assert.Equal(ConnectionState.Closed, connections.Last!.State);
            using var valid = await factory.OpenOwnedAsync(CancellationToken.None);
            Assert.Equal(1, await valid.ExecuteAsync<int>("SELECT 1;", CancellationToken.None));
        });

    [SqlServerFact]
    public Task Borrowed_construction_failure_preserves_connection_and_transaction_for_retry() =>
        WithDatabaseAsync(async database =>
        {
            var connections = new SqlConnectionFactory(database.ConnectionString);
            var factory = new LinqToDbSessionFactory(connections, new LinqToDbMappingSchemaProvider(new[] { new FailingMapping() }));
            using var uow = new SqlUnitOfWorkFactory(connections).Create(); uow.BeginTransaction();
            var context = (ISqlExecutionContext)uow;
            Assert.Throws<InvalidOperationException>(() => factory.Borrow(context, true));
            Assert.Equal(1, await NativeAsync((SqlConnection)context.Connection, "SELECT @@TRANCOUNT;", (SqlTransaction)context.Transaction!));
            using var valid = factory.Borrow(context, true);
            Assert.Equal(1, await valid.ExecuteAsync<int>("SELECT 1;", CancellationToken.None));
            uow.Rollback();
        });

    [Fact]
    public async Task Pre_canceled_owned_open_does_not_allocate_a_connection()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateFactory(new UnexpectedConnectionFactory())
            .OpenOwnedAsync(cancellation.Token));
    }

    [SqlServerFact]
    public Task Composed_fluent_mapping_executes_typed_writes_and_queries_with_native_sql_oracle() =>
        WithDatabaseAsync(async database =>
        {
            var connections = new SqlConnectionFactory(database.ConnectionString);
            using var uow = new SqlUnitOfWorkFactory(connections).Create();
            var context = (ISqlExecutionContext)uow;
            await NativeAsync((SqlConnection)context.Connection, "CREATE TABLE dbo.LinqProbe (ProbeId int NOT NULL PRIMARY KEY);");
            uow.BeginTransaction();
            var mappings = new LinqToDbMappingSchemaProvider(new ILinqToDbMapping[] { new LinqToDbMappingTests.ProbeMapping() });
            using (var session = new LinqToDbSessionFactory(connections, mappings).Borrow(context, true))
            {
                await session.InsertAsync(new LinqToDbMappingTests.Probe { Id = 7 }, token: CancellationToken.None);
                Assert.Equal(7, await session.GetTable<LinqToDbMappingTests.Probe>().Where(row => row.Id > 6)
                    .Select(row => row.Id).SingleAsync(CancellationToken.None));
            }
            Assert.Equal(7, await NativeAsync((SqlConnection)context.Connection,
                "SELECT ProbeId FROM dbo.LinqProbe;", (SqlTransaction)context.Transaction!));
            uow.Rollback();
            using var verify = (SqlConnection)await connections.OpenConnectionAsync(CancellationToken.None);
            Assert.Equal(0, await NativeAsync(verify, "SELECT COUNT(*) FROM dbo.LinqProbe;"));
        });

    private static LinqToDbSessionFactory CreateFactory(IDbConnectionFactory connections) =>
        new(connections, new LinqToDbMappingSchemaProvider(Array.Empty<ILinqToDbMapping>()));

    private static async Task<int> NativeAsync(SqlConnection connection, string sql, SqlTransaction? transaction = null)
    {
        using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 30 };
        var value = await command.ExecuteScalarAsync();
        return value is null || value is DBNull ? 0 : Convert.ToInt32(value);
    }

    private static async Task WithDatabaseAsync(Func<SqlTestDatabase, Task> body)
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? bodyError = null;
        try { await body(database); }
        catch (Exception error) { bodyError = error; throw; }
        finally
        {
            try { await database.DisposeAsync(); }
            catch (Exception cleanupError) when (bodyError is not null)
            { throw new AggregateException("Scenario and owned database cleanup failed.", bodyError, cleanupError); }
        }
    }

    private sealed class Context(IDbConnection connection, IDbTransaction? transaction = null) : ISqlExecutionContext
    {
        public IDbConnection Connection => connection;
        public IDbTransaction? Transaction => transaction;
    }

    private interface ILateRepository { Task<int> ReadAsync(); Task<int> ReadTransactionCountAsync(); }
    private sealed class LateRepository(ISqlExecutionContext context, ILinqToDbSessionFactory sessions) : ILateRepository
    {
        public async Task<int> ReadAsync()
        {
            using var session = sessions.Borrow(context, false);
            return await session.ExecuteAsync<int>("SELECT 1;", CancellationToken.None);
        }
        public async Task<int> ReadTransactionCountAsync()
        {
            using var session = sessions.Borrow(context, true);
            return await session.ExecuteAsync<int>("SELECT @@TRANCOUNT;", CancellationToken.None);
        }
    }

    private sealed class CapturingConnectionFactory(string connectionString) : IDbConnectionFactory
    {
        public SqlConnection? Last { get; private set; }
        public IDbConnection OpenConnection() => throw new NotSupportedException();
        public async Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            Last = new SqlConnection(connectionString); await Last.OpenAsync(cancellationToken); return Last;
        }
    }
    private sealed class UnexpectedConnectionFactory : IDbConnectionFactory
    {
        public IDbConnection OpenConnection() => throw new InvalidOperationException("Unexpected fallback connection.");
        public Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected fallback connection.");
    }
    private sealed class FailingMapping : ILinqToDbMapping
    {
        private int _attempts;
        public void Configure(FluentMappingBuilder builder)
        {
            if (++_attempts == 1) throw new InvalidOperationException("Expected session mapping failure.");
        }
    }
}
