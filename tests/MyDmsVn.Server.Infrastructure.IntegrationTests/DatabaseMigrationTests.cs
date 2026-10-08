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
            Assert.Equal(2, first.Scripts.Count());
            Assert.Collection(
                first.Scripts.OrderBy(script => script.Name, StringComparer.Ordinal),
                script => Assert.EndsWith("001_PersistenceFoundation.sql", script.Name, StringComparison.Ordinal),
                script => Assert.EndsWith("002_Identity.sql", script.Name, StringComparison.Ordinal));
            Assert.True(second.Successful, second.Error?.ToString());
            Assert.Empty(second.Scripts);

            using var connection = new SqlConnection(database.ConnectionString);
            var tableCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.P1TestProbe');");
            var historyCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SchemaVersions;");
            Assert.Equal(0, tableCount);
            Assert.Equal(2, historyCount);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Identity_migration_creates_required_tables_and_enforces_unique_normalized_names()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);

            Assert.True(result.Successful, result.Error?.ToString());
            using var connection = new SqlConnection(database.ConnectionString);
            var identityTableCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE name IN " +
                "(N'Users', N'Roles', N'UserRoles', N'RolePermissions', N'UserPermissions');");
            Assert.Equal(5, identityTableCount);

            var credentialColumnCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users') " +
                "AND name IN (N'PasswordHash', N'PasswordSalt', N'PasswordAlgorithm');");
            Assert.Equal(3, credentialColumnCount);

            const string insertUser =
                "INSERT dbo.Users " +
                "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, " +
                "PasswordAlgorithm, IsActive, CreatedAtUtc) " +
                "VALUES (@Username, @NormalizedUsername, @DisplayName, N'Local', N'hash', N'', " +
                "N'BCrypt', 1, SYSUTCDATETIME());";
            await connection.ExecuteAsync(insertUser, new
            {
                Username = "operator",
                NormalizedUsername = "OPERATOR",
                DisplayName = "Operator",
            });

            var duplicate = await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync(insertUser, new
            {
                Username = "Operator",
                NormalizedUsername = "OPERATOR",
                DisplayName = "Duplicate",
            }));
            Assert.Contains(duplicate.Number, new[] { 2601, 2627 });
        }
        finally
        {
            await database.DisposeAsync();
        }
    }
}
