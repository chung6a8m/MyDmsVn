using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DbUp;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SchemaCompatibilityIntegrationTests
{
    [SqlServerFact]
    public async Task Interrupted_upgrade_prefix_blocks_client_and_forward_fix_preserves_existing_data()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var assembly = typeof(DatabaseMigrationRunner).Assembly;
            var interrupted = DeployChanges.To
                .SqlDatabase(database.ConnectionString)
                .WithScriptsEmbeddedInAssembly(
                    assembly,
                    resourceName =>
                        resourceName.Contains(".Scripts.") &&
                        !resourceName.EndsWith("007_Inventory_Stock.sql", StringComparison.Ordinal))
                .JournalToSqlTable("dbo", "SchemaVersions")
                .WithTransactionPerScript()
                .LogToNowhere()
                .Build()
                .PerformUpgrade();
            Assert.True(interrupted.Successful, interrupted.Error?.ToString());
            Assert.Equal(6, interrupted.Scripts.Count());

            var injectedFailure = DeployChanges.To
                .SqlDatabase(database.ConnectionString)
                .WithScript(
                    "007_FaultInjection.sql",
                    "CREATE TABLE dbo.P6FaultArtifact (Id int NOT NULL); " +
                    "THROW 51000, 'Injected migration failure.', 1;")
                .JournalToSqlTable("dbo", "SchemaVersions")
                .WithTransactionPerScript()
                .LogToNowhere()
                .Build()
                .PerformUpgrade();
            Assert.False(injectedFailure.Successful);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                Assert.Null(await connection.ExecuteScalarAsync<int?>(
                    "SELECT OBJECT_ID(N'dbo.P6FaultArtifact', N'U');"));
                Assert.Equal(6, await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM dbo.SchemaVersions;"));
                await connection.ExecuteAsync(
                    "INSERT dbo.Products (Code, Name, Unit) VALUES (N'P6-SAFE', N'Preserved', N'EA');");
            }

            var blocked = await new SqlSchemaCompatibilityChecker(database.ConnectionString)
                .CheckAsync(CancellationToken.None);
            Assert.Equal(SchemaCompatibilityStatus.TooOld, blocked.Status);

            var forwardFix = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(forwardFix.Successful, forwardFix.Error?.ToString());
            Assert.Single(forwardFix.Scripts);

            var compatible = await new SqlSchemaCompatibilityChecker(database.ConnectionString)
                .CheckAsync(CancellationToken.None);
            Assert.True(compatible.IsCompatible);
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.Products WHERE Code = N'P6-SAFE' AND Name = N'Preserved';"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Current_database_passes_repeatable_read_only_preflight()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            var checker = new SqlSchemaCompatibilityChecker(database.ConnectionString);

            var first = await checker.CheckAsync(CancellationToken.None);
            var second = await checker.CheckAsync(CancellationToken.None);

            Assert.True(first.IsCompatible);
            Assert.True(second.IsCompatible);
            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(7, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Applied_artifacts_without_matching_journal_entry_are_partial()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "DELETE dbo.SchemaVersions WHERE ScriptName LIKE N'%007_Inventory_Stock.sql';");
            }

            var result = await new SqlSchemaCompatibilityChecker(database.ConnectionString)
                .CheckAsync(CancellationToken.None);

            Assert.Equal(SchemaCompatibilityStatus.Partial, result.Status);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Disabled_or_untrusted_check_constraint_is_partial()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE dbo.Products NOCHECK CONSTRAINT CK_Products_Code_NoNul;");
            }

            var result = await new SqlSchemaCompatibilityChecker(database.ConnectionString)
                .CheckAsync(CancellationToken.None);

            Assert.Equal(SchemaCompatibilityStatus.Partial, result.Status);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Disabled_append_only_trigger_is_partial()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "DISABLE TRIGGER dbo.TR_StockLedger_AppendOnly ON dbo.StockLedger;");
            }

            var result = await new SqlSchemaCompatibilityChecker(database.ConnectionString)
                .CheckAsync(CancellationToken.None);

            Assert.Equal(SchemaCompatibilityStatus.Partial, result.Status);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Clean_known_prefix_is_too_old()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "DROP TABLE dbo.StockBalances; " +
                    "DROP TABLE dbo.StockLedger; " +
                    "DELETE dbo.SchemaVersions WHERE ScriptName LIKE N'%007_Inventory_Stock.sql';");
            }

            var result = await new SqlSchemaCompatibilityChecker(database.ConnectionString)
                .CheckAsync(CancellationToken.None);

            Assert.Equal(SchemaCompatibilityStatus.TooOld, result.Status);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Future_and_forked_histories_fail_with_distinct_codes()
    {
        var futureDatabase = await CreateMigratedDatabaseAsync();
        try
        {
            using (var connection = new SqlConnection(futureDatabase.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "INSERT dbo.SchemaVersions (ScriptName, Applied) " +
                    "VALUES (N'MyDmsVn.Server.DbMigrator.Scripts.008_Future.sql', SYSUTCDATETIME());");
            }

            var future = await new SqlSchemaCompatibilityChecker(futureDatabase.ConnectionString)
                .CheckAsync(CancellationToken.None);
            Assert.Equal(SchemaCompatibilityStatus.TooNew, future.Status);
        }
        finally
        {
            await futureDatabase.DisposeAsync();
        }

        var forkedDatabase = await CreateMigratedDatabaseAsync();
        try
        {
            using (var connection = new SqlConnection(forkedDatabase.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "UPDATE dbo.SchemaVersions SET ScriptName = N'004_Forked.sql' " +
                    "WHERE ScriptName LIKE N'%004_Catalog_Nul_Constraints.sql';");
            }

            var forked = await new SqlSchemaCompatibilityChecker(forkedDatabase.ConnectionString)
                .CheckAsync(CancellationToken.None);
            Assert.Equal(SchemaCompatibilityStatus.UnknownHistory, forked.Status);
        }
        finally
        {
            await forkedDatabase.DisposeAsync();
        }
    }

    private static async Task<SqlTestDatabase> CreateMigratedDatabaseAsync()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
        Assert.True(migration.Successful, migration.Error?.ToString());
        return database;
    }
}
