using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Persistence;
using LinqToDB;
using LinqToDB.Mapping;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class PersistenceTransactionTests
{
    [SqlServerFact]
    public async Task Commit_persists_writes_from_two_repositories_in_one_transaction()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            using var provider = CreateProvider(database.ConnectionString);
            using (var unitOfWork = await CreateUnitOfWorkAsync(provider))
            {
                unitOfWork.BeginTransaction();
                await unitOfWork.Repository<IFirstProbeRepository>()
                    .InsertAsync("first", CancellationToken.None);
                await unitOfWork.Repository<ISecondProbeRepository>()
                    .InsertAsync("second", CancellationToken.None);
                unitOfWork.Commit();
            }

            Assert.Equal(2, await CountRowsAsync(database.ConnectionString));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Failure_in_second_repository_rolls_back_the_first_write()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            using var provider = CreateProvider(database.ConnectionString);
            using (var unitOfWork = await CreateUnitOfWorkAsync(provider))
            {
                unitOfWork.BeginTransaction();
                await unitOfWork.Repository<IFirstProbeRepository>()
                    .InsertAsync("will-roll-back", CancellationToken.None);

                await Assert.ThrowsAsync<SqlException>(
                    () => unitOfWork.Repository<ISecondProbeRepository>()
                        .FailAsync(CancellationToken.None));
                unitOfWork.Rollback();
                Assert.Equal(UnitOfWorkState.RolledBack, unitOfWork.State);
            }

            Assert.Equal(0, await CountRowsAsync(database.ConnectionString));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Parallel_unit_of_works_have_distinct_connections_and_transactions()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            using var provider = CreateProvider(database.ConnectionString);
            var factory = provider.GetRequiredService<IUnitOfWorkFactory>();
            using var first = await factory.CreateAsync(CancellationToken.None);
            using var second = await factory.CreateAsync(CancellationToken.None);
            first.BeginTransaction();
            second.BeginTransaction();
            var firstContext = Assert.IsAssignableFrom<ISqlExecutionContext>(first);
            var secondContext = Assert.IsAssignableFrom<ISqlExecutionContext>(second);

            Assert.NotSame(firstContext.Connection, secondContext.Connection);
            Assert.NotSame(firstContext.Transaction, secondContext.Transaction);

            await Task.WhenAll(
                first.Repository<IFirstProbeRepository>()
                    .InsertAsync("parallel-one", CancellationToken.None),
                second.Repository<IFirstProbeRepository>()
                    .InsertAsync("parallel-two", CancellationToken.None));
            first.Commit();
            second.Commit();

            Assert.Equal(2, await CountRowsAsync(database.ConnectionString));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Repository_and_native_oracle_share_the_same_server_session()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            using var provider = CreateProvider(database.ConnectionString);
            using var unitOfWork = await CreateUnitOfWorkAsync(provider);
            unitOfWork.BeginTransaction();
            var repositorySession = await unitOfWork.Repository<IFirstProbeRepository>()
                .GetServerSessionIdAsync(CancellationToken.None);
            var context = Assert.IsAssignableFrom<ISqlExecutionContext>(unitOfWork);
            var querySession = await context.Connection.QuerySingleAsync<int>(
                "SELECT @@SPID;",
                transaction: context.Transaction);

            Assert.Equal(repositorySession, querySession);
            unitOfWork.Rollback();
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Canceled_command_allows_safe_scope_disposal_and_a_new_connection()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            using var provider = CreateProvider(database.ConnectionString);
            var unitOfWork = await CreateUnitOfWorkAsync(provider);
            unitOfWork.BeginTransaction();
            var context = Assert.IsAssignableFrom<ISqlExecutionContext>(unitOfWork);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            var cancellationException = await Record.ExceptionAsync(
                () => context.Connection.ExecuteAsync(
                    new SqlTestCommand(
                        "WAITFOR DELAY '00:00:02';",
                        transaction: context.Transaction,
                        cancellationToken: cancellation.Token)));

            Assert.True(cancellation.IsCancellationRequested);
            Assert.True(
                cancellationException is OperationCanceledException or SqlException,
                $"Expected a cancellation exception from SqlClient, got {cancellationException?.GetType().FullName}.");

            unitOfWork.Dispose();
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await verification.QuerySingleAsync<int>("SELECT 1;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static ServiceProvider CreateProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services
            .AddSqlPersistence(connectionString)
            .AddRepository<IFirstProbeRepository, FirstProbeRepository>()
            .AddRepository<ISecondProbeRepository, SecondProbeRepository>();
        services.AddSingleton<ILinqToDbMapping>(new TransactionProbeMapping());
        return services.BuildServiceProvider();
    }

    private static async Task<SqlTestDatabase> CreateMigratedDatabaseAsync()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            TestDatabaseMigrationRunner.Migrate(database.ConnectionString);
        }
        catch (Exception setupError)
        {
            try
            {
                await database.DisposeAsync();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(
                    "Test database setup and cleanup both failed.",
                    setupError,
                    cleanupError);
            }

            throw;
        }

        return database;
    }

    private static Task<IUnitOfWork> CreateUnitOfWorkAsync(IServiceProvider provider)
    {
        return provider.GetRequiredService<IUnitOfWorkFactory>()
            .CreateAsync(CancellationToken.None);
    }

    private static async Task<int> CountRowsAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.P1TestProbe;");
    }

    private interface IFirstProbeRepository
    {
        Task<int> InsertAsync(string value, CancellationToken cancellationToken);

        Task<int> GetServerSessionIdAsync(CancellationToken cancellationToken);
    }

    private interface ISecondProbeRepository
    {
        Task<int> InsertAsync(string value, CancellationToken cancellationToken);

        Task FailAsync(CancellationToken cancellationToken);
    }

    private sealed class FirstProbeRepository : IFirstProbeRepository
    {
        private readonly ISqlExecutionContext _context;
        private readonly ILinqToDbSessionFactory _sessions;

        public FirstProbeRepository(ISqlExecutionContext context, ILinqToDbSessionFactory sessions)
        {
            _context = context;
            _sessions = sessions;
        }

        public Task<int> InsertAsync(string value, CancellationToken cancellationToken)
        {
            return InsertProbeAsync(_context, _sessions, value, cancellationToken);
        }

        public Task<int> GetServerSessionIdAsync(CancellationToken cancellationToken)
        {
            return _context.Connection.QuerySingleAsync<int>(
                new SqlTestCommand(
                    "SELECT @@SPID;",
                    transaction: RequireTransaction(_context),
                    cancellationToken: cancellationToken));
        }
    }

    private sealed class SecondProbeRepository : ISecondProbeRepository
    {
        private readonly ISqlExecutionContext _context;
        private readonly ILinqToDbSessionFactory _sessions;

        public SecondProbeRepository(ISqlExecutionContext context, ILinqToDbSessionFactory sessions)
        {
            _context = context;
            _sessions = sessions;
        }

        public Task<int> InsertAsync(string value, CancellationToken cancellationToken)
        {
            return InsertProbeAsync(_context, _sessions, value, cancellationToken);
        }

        public Task FailAsync(CancellationToken cancellationToken)
        {
            return _context.Connection.ExecuteAsync(
                new SqlTestCommand(
                    "THROW 51000, 'Expected second repository failure.', 1;",
                    transaction: RequireTransaction(_context),
                    cancellationToken: cancellationToken));
        }
    }

    private sealed class TransactionProbe
    {
        public int Id { get; set; }

        public string Value { get; set; } = string.Empty;
    }

    private sealed class TransactionProbeMapping : ILinqToDbMapping
    {
        public void Configure(FluentMappingBuilder builder)
        {
            var probe = builder.Entity<TransactionProbe>().HasSchemaName("dbo").HasTableName("P1TestProbe");
            probe.Property(row => row.Id).HasColumnName("ProbeId").IsPrimaryKey().IsIdentity();
            probe.Property(row => row.Value).HasColumnName("ProbeValue").HasDataType(DataType.NVarChar).HasLength(128);
        }
    }

    private static async Task<int> InsertProbeAsync(
        ISqlExecutionContext context,
        ILinqToDbSessionFactory sessions,
        string value,
        CancellationToken cancellationToken)
    {
        using var db = sessions.Borrow(context, true);
        return await db.GetTable<TransactionProbe>().Value(row => row.Value, value).InsertWithInt32IdentityAsync(cancellationToken)
            ?? throw new InvalidOperationException("Probe insert returned no identity.");
    }

    private static IDbTransaction RequireTransaction(ISqlExecutionContext context)
    {
        return context.Transaction
            ?? throw new InvalidOperationException("Repository operation requires an active transaction.");
    }
}
