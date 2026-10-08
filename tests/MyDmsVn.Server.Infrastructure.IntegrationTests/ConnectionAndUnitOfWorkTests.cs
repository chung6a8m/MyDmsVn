using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class ConnectionAndUnitOfWorkTests
{
    [SqlServerFact]
    public async Task Connection_factory_returns_fresh_open_connections()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var factory = new SqlConnectionFactory(database.ConnectionString);

            using var first = await factory.OpenConnectionAsync(CancellationToken.None);
            using var second = await factory.OpenConnectionAsync(CancellationToken.None);

            Assert.Equal(ConnectionState.Open, first.State);
            Assert.Equal(ConnectionState.Open, second.State);
            Assert.NotSame(first, second);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Connection_factory_honors_a_pre_canceled_open()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var factory = new SqlConnectionFactory(database.ConnectionString);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => factory.OpenConnectionAsync(cancellation.Token));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Unit_of_work_enforces_legal_state_transitions()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var factory = CreateUnitOfWorkFactory(database.ConnectionString);
            using var unitOfWork = await factory.CreateAsync(CancellationToken.None);

            Assert.Equal(UnitOfWorkState.Created, unitOfWork.State);
            unitOfWork.BeginTransaction();
            Assert.Equal(UnitOfWorkState.ActiveTransaction, unitOfWork.State);
            Assert.Throws<InvalidOperationException>(() => unitOfWork.BeginTransaction());

            unitOfWork.Commit();
            Assert.Equal(UnitOfWorkState.Committed, unitOfWork.State);
            Assert.Throws<InvalidOperationException>(() => unitOfWork.Commit());
            Assert.Throws<InvalidOperationException>(() => unitOfWork.Rollback());
            Assert.Throws<InvalidOperationException>(() => unitOfWork.BeginTransaction());

            unitOfWork.Dispose();
            Assert.Equal(UnitOfWorkState.Disposed, unitOfWork.State);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Disposing_an_active_unit_of_work_rolls_back_before_closing_connection()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            using (var setup = new Microsoft.Data.SqlClient.SqlConnection(database.ConnectionString))
            {
                await setup.ExecuteAsync(
                    "CREATE TABLE dbo.UnitOfWorkProbe (ProbeId int NOT NULL PRIMARY KEY);");
            }

            var factory = CreateUnitOfWorkFactory(database.ConnectionString);
            var unitOfWork = await factory.CreateAsync(CancellationToken.None);
            unitOfWork.BeginTransaction();
            var context = Assert.IsAssignableFrom<ISqlExecutionContext>(unitOfWork);
            await context.Connection.ExecuteAsync(
                "INSERT INTO dbo.UnitOfWorkProbe (ProbeId) VALUES (1);",
                transaction: context.Transaction);

            unitOfWork.Dispose();

            using var verification = new Microsoft.Data.SqlClient.SqlConnection(database.ConnectionString);
            var count = await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.UnitOfWorkProbe;");
            Assert.Equal(0, count);
            Assert.Equal(UnitOfWorkState.Disposed, unitOfWork.State);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static SqlUnitOfWorkFactory CreateUnitOfWorkFactory(string connectionString)
    {
        return new SqlUnitOfWorkFactory(new SqlConnectionFactory(connectionString));
    }

    private static Task<SqlTestDatabase> CreateDatabaseAsync()
    {
        return SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
    }
}
