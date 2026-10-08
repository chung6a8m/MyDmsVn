using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SqlTestDatabaseLifecycleTests
{
    [SqlServerFact]
    public async Task Create_and_dispose_only_manages_a_generated_database()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var database = await SqlTestDatabase.CreateAsync(adminConnectionString);
        var databaseName = database.DatabaseName;

        try
        {
            using var connection = new SqlConnection(database.ConnectionString);
            var actual = await connection.QuerySingleAsync<string>("SELECT DB_NAME();");

            Assert.Equal(databaseName, actual);
        }
        finally
        {
            await database.DisposeAsync();
        }

        using var adminConnection = new SqlConnection(adminConnectionString);
        var remaining = await adminConnection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sys.databases WHERE name = @databaseName;",
            new { databaseName });
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task Create_rejects_an_admin_connection_targeting_a_non_system_database()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = "localhost",
            InitialCatalog = "MyDmsVn",
            IntegratedSecurity = true,
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SqlTestDatabase.CreateAsync(builder.ConnectionString));

        Assert.Contains("master", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SqlServerFact]
    public async Task Provisioning_failure_after_create_attempts_guarded_cleanup()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        string? attemptedDatabaseName = null;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SqlTestDatabase.CreateAsync(
                adminConnectionString,
                default,
                connectionString => new SqlConnection(connectionString),
                databaseName =>
                {
                    attemptedDatabaseName = databaseName;
                    throw new InvalidOperationException("Expected post-create failure.");
                }));

        Assert.NotNull(attemptedDatabaseName);
        using var adminConnection = new SqlConnection(adminConnectionString);
        var remaining = await adminConnection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sys.databases WHERE name = @attemptedDatabaseName;",
            new { attemptedDatabaseName });
        Assert.Equal(0, remaining);
    }

    [SqlServerFact]
    public async Task Failed_teardown_can_be_retried_without_losing_database_ownership()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var connectionCreationCount = 0;
        var database = await SqlTestDatabase.CreateAsync(
            adminConnectionString,
            default,
            connectionString =>
            {
                connectionCreationCount++;
                if (connectionCreationCount == 2)
                {
                    throw new InvalidOperationException("Expected transient teardown failure.");
                }

                return new SqlConnection(connectionString);
            },
            _ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.DisposeAsync());
        await database.DisposeAsync();

        using var adminConnection = new SqlConnection(adminConnectionString);
        var remaining = await adminConnection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sys.databases WHERE name = @databaseName;",
            new { databaseName = database.DatabaseName });
        Assert.Equal(0, remaining);
    }
}
