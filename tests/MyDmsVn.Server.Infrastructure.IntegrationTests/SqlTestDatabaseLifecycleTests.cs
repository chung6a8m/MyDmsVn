using System;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using Microsoft.Data.SqlClient;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SqlTestDatabaseLifecycleTests
{
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Gui_allocation_verifies_live_ownership_and_refuses_a_changed_marker_before_drop()
    {
        var factory = typeof(SqlTestDatabase).GetMethod("CreateGuiAsync");
        Assert.NotNull(factory);
        var administration = Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var allocation = await (Task<SqlTestDatabase>)factory!.Invoke(null,
            new object[] { administration, System.Threading.CancellationToken.None })!;
        string? marker = null;
        try
        {
            Assert.StartsWith("MyDmsVn_GuiP5_", allocation.DatabaseName, StringComparison.Ordinal);
            using (var connection = new SqlConnection(allocation.ConnectionString))
            {
                await connection.OpenAsync();
                await allocation.VerifyOwnershipAsync(connection, default);
                marker = await connection.QuerySingleAsync<string>(
                    "SELECT CONVERT(nvarchar(128), value) FROM sys.extended_properties " +
                    "WHERE class=0 AND name=N'MyDmsVn.TestOwnership';");
                await connection.ExecuteAsync("EXEC sys.sp_updateextendedproperty " +
                    "@name=N'MyDmsVn.TestOwnership', @value=N'changed';");
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => allocation.DisposeAsync());
            using var master = new SqlConnection(administration);
            Assert.Equal(1, await master.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.databases WHERE name=@name;", new { name = allocation.DatabaseName }));
        }
        finally
        {
            if (marker != null)
            {
                using var connection = new SqlConnection(allocation.ConnectionString);
                await connection.ExecuteAsync("EXEC sys.sp_updateextendedproperty " +
                    "@name=N'MyDmsVn.TestOwnership', @value=@marker;", new { marker });
            }
            await allocation.DisposeAsync();
        }
        using var verify = new SqlConnection(administration);
        Assert.Equal(0, await verify.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sys.databases WHERE name=@name;", new { name = allocation.DatabaseName }));
    }

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
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

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
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

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
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

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Teardown_does_not_clear_an_unrelated_master_connection_pool()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var poolBuilder = new SqlConnectionStringBuilder(adminConnectionString)
        {
            ApplicationName = "MyDmsVn-T03-Unrelated-" + Guid.NewGuid().ToString("N"),
            Pooling = true,
            MinPoolSize = 0,
            MaxPoolSize = 1,
        };
        var pooledMasterConnectionString = poolBuilder.ConnectionString;
        try
        {
            Guid firstConnectionId;
            using (var pooled = new SqlConnection(pooledMasterConnectionString))
            {
                await pooled.OpenAsync();
                firstConnectionId = pooled.ClientConnectionId;
            }

            var database = await SqlTestDatabase.CreateAsync(adminConnectionString);
            await database.DisposeAsync();

            Guid preservedConnectionId;
            using (var pooled = new SqlConnection(pooledMasterConnectionString))
            {
                await pooled.OpenAsync();
                preservedConnectionId = pooled.ClientConnectionId;
            }

            Assert.Equal(firstConnectionId, preservedConnectionId);

            SqlConnection.ClearPool(new SqlConnection(pooledMasterConnectionString));
            using var replacement = new SqlConnection(pooledMasterConnectionString);
            await replacement.OpenAsync();

            Assert.NotEqual(preservedConnectionId, replacement.ClientConnectionId);
        }
        finally
        {
            SqlConnection.ClearPool(new SqlConnection(pooledMasterConnectionString));
        }
    }
}
