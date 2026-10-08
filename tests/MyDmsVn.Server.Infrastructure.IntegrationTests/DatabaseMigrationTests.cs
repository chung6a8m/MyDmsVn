using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class DatabaseMigrationTests
{
    [Theory]
    [InlineData("Server=invalid.invalid;Database=master;User ID=test;Password=test;Connect Timeout=1;Encrypt=false")]
    [InlineData("Server=invalid.invalid;Database=model;User ID=test;Password=test;Connect Timeout=1;Encrypt=false")]
    [InlineData("Server=invalid.invalid;Database=msdb;User ID=test;Password=test;Connect Timeout=1;Encrypt=false")]
    [InlineData("Server=invalid.invalid;Database=tempdb;User ID=test;Password=test;Connect Timeout=1;Encrypt=false")]
    [InlineData("Server=invalid.invalid;User ID=test;Password=test;Connect Timeout=1;Encrypt=false")]
    public void System_or_unspecified_database_is_rejected_before_connecting(string connectionString)
    {
        Assert.Throws<ArgumentException>(() => new DatabaseMigrationRunner().Migrate(connectionString));
    }

    [SqlServerFact]
    public async Task Clean_migration_applies_once_and_repeat_is_a_no_op()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migrator = new DatabaseMigrationRunner();

            var first = migrator.Migrate(database.ConnectionString);
            var second = migrator.Migrate(database.ConnectionString);

            Assert.True(first.Successful, first.Error?.ToString());
            Assert.Single(first.Scripts);
            Assert.EndsWith("001_PersistenceFoundation.sql", first.Scripts.Single().Name, StringComparison.Ordinal);
            Assert.True(second.Successful, second.Error?.ToString());
            Assert.Empty(second.Scripts);

            using var connection = new SqlConnection(database.ConnectionString);
            var tableCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.P1TestProbe');");
            var historyCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SchemaVersions;");
            Assert.Equal(0, tableCount);
            Assert.Equal(1, historyCount);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }
}
