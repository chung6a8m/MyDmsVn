using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using Microsoft.Data.SqlClient;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SqlTestDatabaseMutationTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Server=localhost;Integrated Security=true")]
    [InlineData("Server=localhost;Database=MyDmsVn;Integrated Security=true")]
    [InlineData("Server=localhost;Database=model;Integrated Security=true")]
    [InlineData("Database=master;Integrated Security=true")]
    [InlineData("Server=localhost;Database=master;AttachDbFilename=test.mdf;Integrated Security=true")]
    [InlineData("Server=localhost;Database=master;User Instance=true;Integrated Security=true")]
    public async Task Invalid_admin_configuration_is_rejected_before_opening_a_connection(
        string connectionString)
    {
        var connectionRequested = false;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SqlTestDatabase.CreateAsync(
                connectionString,
                CancellationToken.None,
                value =>
                {
                    connectionRequested = true;
                    return new SqlConnection(value);
                },
                _ => { }));

        Assert.False(connectionRequested);
    }

    [Fact]
    public async Task Cancelled_create_does_not_open_a_connection()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var connectionRequested = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SqlTestDatabase.CreateAsync(
                "Server=localhost;Database=master;Integrated Security=true",
                cancellation.Token,
                value =>
                {
                    connectionRequested = true;
                    return new SqlConnection(value);
                },
                _ => { }));

        Assert.False(connectionRequested);
    }

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public Task Teardown_refuses_a_database_with_a_changed_ownership_marker() =>
        AssertMarkerRequiredForCleanupAsync(removeMarker: false);

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public Task Teardown_refuses_a_database_with_a_missing_ownership_marker() =>
        AssertMarkerRequiredForCleanupAsync(removeMarker: true);

    private static async Task AssertMarkerRequiredForCleanupAsync(bool removeMarker)
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var database = await SqlTestDatabase.CreateAsync(adminConnectionString);
        string? originalMarker = null;
        var markerChanged = false;
        try
        {
            using var connection = new SqlConnection(database.ConnectionString);
            originalMarker = await connection.QuerySingleAsync<string>(
                "SELECT CONVERT(nvarchar(128), value) FROM sys.extended_properties " +
                "WHERE class = 0 AND name = N'MyDmsVn.TestOwnership';");
            await connection.ExecuteAsync(removeMarker
                ? "EXEC sys.sp_dropextendedproperty @name = N'MyDmsVn.TestOwnership';"
                : "EXEC sys.sp_updateextendedproperty @name = N'MyDmsVn.TestOwnership', " +
                  "@value = N'changed-by-lifecycle-test';");
            markerChanged = true;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => database.DisposeAsync());

            Assert.Contains("ownership", exception.Message, StringComparison.OrdinalIgnoreCase);
            using var adminConnection = new SqlConnection(adminConnectionString);
            var access = await adminConnection.QuerySingleAsync<string>(
                "SELECT user_access_desc FROM sys.databases WHERE name = @databaseName;",
                new { databaseName = database.DatabaseName });
            Assert.Equal("MULTI_USER", access);
        }
        finally
        {
            try
            {
                if (markerChanged)
                {
                    using var connection = new SqlConnection(database.ConnectionString);
                    await connection.ExecuteAsync(
                        "EXEC sys." + (removeMarker ? "sp_addextendedproperty" : "sp_updateextendedproperty") +
                        " @name = N'MyDmsVn.TestOwnership', @value = @originalMarker;",
                        new { originalMarker });
                }
            }
            finally
            {
                await database.DisposeAsync();
            }
        }
    }
}
