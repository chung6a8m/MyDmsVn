using System;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SqlPrincipalProvisioningTests
{
    [SqlServerFact]
    public async Task Provisioning_script_grants_runtime_dml_but_not_schema_or_journal_mutation()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());

            using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                "CREATE USER [P6RuntimeProbe] WITHOUT LOGIN; " +
                "CREATE USER [P6MigrationProbe] WITHOUT LOGIN; " +
                "CREATE TABLE dbo.P6UnlistedSecret (SecretId int NOT NULL); ");

            var script = File.ReadAllText(FindProvisioningScript());
            script = script
                .Replace("$(RuntimeUser)", "P6RuntimeProbe")
                .Replace("$(MigrationUser)", "P6MigrationProbe");
            await connection.ExecuteAsync(script);

            var permissions = await connection.QuerySingleAsync<PermissionProbe>(
                "EXECUTE AS USER = N'P6RuntimeProbe'; " +
                "SELECT " +
                "HAS_PERMS_BY_NAME(N'dbo.Products', N'OBJECT', N'SELECT') AS CanReadProducts, " +
                "HAS_PERMS_BY_NAME(N'dbo.Products', N'OBJECT', N'INSERT') AS CanInsertProducts, " +
                "HAS_PERMS_BY_NAME(N'dbo.Products', N'OBJECT', N'UPDATE') AS CanUpdateProducts, " +
                "HAS_PERMS_BY_NAME(N'dbo.GoodsReceiptLines', N'OBJECT', N'DELETE') AS CanDeleteReceiptLines, " +
                "HAS_PERMS_BY_NAME(N'dbo.StockLedger', N'OBJECT', N'INSERT') AS CanInsertLedger, " +
                "HAS_PERMS_BY_NAME(N'dbo.StockBalances', N'OBJECT', N'UPDATE') AS CanUpdateBalances, " +
                "HAS_PERMS_BY_NAME(N'dbo.GoodsReceiptNumberSequence', N'OBJECT', N'UPDATE') AS CanUseReceiptSequence, " +
                "HAS_PERMS_BY_NAME(N'dbo.CatalogTextContainsNul', N'OBJECT', N'VIEW DEFINITION') AS CanInspectManifestFunction, " +
                "HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'ALTER') AS CanAlterSchema, " +
                "HAS_PERMS_BY_NAME(N'dbo.SchemaVersions', N'OBJECT', N'SELECT') AS CanReadJournal, " +
                "HAS_PERMS_BY_NAME(N'dbo.SchemaVersions', N'OBJECT', N'UPDATE') AS CanUpdateJournal, " +
                "HAS_PERMS_BY_NAME(N'dbo.P6UnlistedSecret', N'OBJECT', N'SELECT') AS CanReadUnlistedObject; " +
                "REVERT;");

            Assert.Equal(1, permissions.CanReadProducts);
            Assert.Equal(1, permissions.CanInsertProducts);
            Assert.Equal(1, permissions.CanUpdateProducts);
            Assert.Equal(1, permissions.CanDeleteReceiptLines);
            Assert.Equal(1, permissions.CanInsertLedger);
            Assert.Equal(1, permissions.CanUpdateBalances);
            Assert.Equal(1, permissions.CanUseReceiptSequence);
            Assert.Equal(1, permissions.CanInspectManifestFunction);
            Assert.Equal(0, permissions.CanAlterSchema);
            Assert.Equal(1, permissions.CanReadJournal);
            Assert.Equal(0, permissions.CanUpdateJournal);
            Assert.Equal(0, permissions.CanReadUnlistedObject);

            var migrationControl = await connection.QuerySingleAsync<int>(
                "EXECUTE AS USER = N'P6MigrationProbe'; " +
                "SELECT HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL'); " +
                "REVERT;");
            Assert.Equal(1, migrationControl);

            await connection.ExecuteAsync("EXECUTE AS USER = N'P6RuntimeProbe';");
            try
            {
                var exception = Assert.Throws<DatabaseMigrationAuthorizationException>(
                    () => DatabaseMigrationPrincipalValidator.Validate(connection));
                Assert.Equal("Migration.InsufficientPrivileges", exception.Code);
            }
            finally
            {
                await connection.ExecuteAsync("REVERT;");
            }

            await connection.ExecuteAsync("EXECUTE AS USER = N'P6MigrationProbe';");
            try
            {
                DatabaseMigrationPrincipalValidator.Validate(connection);
            }
            finally
            {
                await connection.ExecuteAsync("REVERT;");
            }

            await Assert.ThrowsAsync<SqlException>(
                () => connection.ExecuteAsync(
                    "EXECUTE AS USER = N'P6RuntimeProbe'; " +
                    "ALTER TABLE dbo.Products ADD P6UnauthorizedColumn int NULL; " +
                    "REVERT;"));
            var unauthorizedColumnCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Products') " +
                "AND name = N'P6UnauthorizedColumn';");
            Assert.Equal(0, unauthorizedColumnCount);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static string FindProvisioningScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "deployment",
                "sql",
                "Provision-MyDmsVnPrincipals.sql");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("The SQL principal provisioning script was not found.");
    }

    private sealed class PermissionProbe
    {
        public int CanReadProducts { get; set; }

        public int CanInsertProducts { get; set; }

        public int CanUpdateProducts { get; set; }

        public int CanDeleteReceiptLines { get; set; }

        public int CanInsertLedger { get; set; }

        public int CanUpdateBalances { get; set; }

        public int CanUseReceiptSequence { get; set; }

        public int CanInspectManifestFunction { get; set; }

        public int CanAlterSchema { get; set; }

        public int CanReadJournal { get; set; }

        public int CanUpdateJournal { get; set; }

        public int CanReadUnlistedObject { get; set; }
    }
}
