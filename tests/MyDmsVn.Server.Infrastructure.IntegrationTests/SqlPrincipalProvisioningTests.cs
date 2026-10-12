using System;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SqlPrincipalProvisioningTests
{
    [SqlServerFact]
    public async Task Provisioning_rejects_the_same_runtime_and_migration_identity()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());
            using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync("CREATE USER [P6SameProbe] WITHOUT LOGIN;");

            var exception = await Assert.ThrowsAsync<SqlException>(
                () => connection.ExecuteAsync(
                    ReadProvisioningScript("P6SameProbe", "P6SameProbe")));

            Assert.Contains("distinct", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.database_role_members AS membership " +
                "WHERE membership.member_principal_id = USER_ID(N'P6SameProbe');"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Provisioning_rejects_an_existing_elevated_runtime_identity()
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
                "CREATE USER [P6ElevatedRuntimeProbe] WITHOUT LOGIN; " +
                "CREATE USER [P6SeparateMigrationProbe] WITHOUT LOGIN; " +
                "ALTER ROLE [db_ddladmin] ADD MEMBER [P6ElevatedRuntimeProbe];");

            var exception = await Assert.ThrowsAsync<SqlException>(
                () => connection.ExecuteAsync(
                    ReadProvisioningScript(
                        "P6ElevatedRuntimeProbe",
                        "P6SeparateMigrationProbe")));

            Assert.Contains("elevated", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.database_role_members AS membership " +
                "INNER JOIN sys.database_principals AS rolePrincipal " +
                "ON rolePrincipal.principal_id = membership.role_principal_id " +
                "WHERE membership.member_principal_id = USER_ID(N'P6ElevatedRuntimeProbe') " +
                "AND rolePrincipal.name = N'MyDmsVnRuntimeRole';"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Provisioning_and_startup_reject_direct_object_alter_permission()
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
                "CREATE USER [P6ObjectAlterProbe] WITHOUT LOGIN; " +
                "CREATE USER [P6ObjectAlterMigrationProbe] WITHOUT LOGIN; " +
                "GRANT ALTER ON OBJECT::dbo.Products TO [P6ObjectAlterProbe];");

            await connection.ExecuteAsync("EXECUTE AS USER = N'P6ObjectAlterProbe';");
            try
            {
                var startupException = await Assert.ThrowsAsync<DesktopStartupException>(
                    () => new SqlRuntimePrincipalValidator()
                        .ValidateConnectionAsync(connection, System.Threading.CancellationToken.None));
                Assert.Equal("Security.RuntimePrincipalOverprivileged", startupException.Code);
            }
            finally
            {
                await connection.ExecuteAsync("REVERT;");
            }

            var provisioningException = await Assert.ThrowsAsync<SqlException>(
                () => connection.ExecuteAsync(
                    ReadProvisioningScript(
                        "P6ObjectAlterProbe",
                        "P6ObjectAlterMigrationProbe")));

            Assert.Contains("elevated", provisioningException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.database_role_members AS membership " +
                "INNER JOIN sys.database_principals AS rolePrincipal " +
                "ON rolePrincipal.principal_id = membership.role_principal_id " +
                "WHERE membership.member_principal_id = USER_ID(N'P6ObjectAlterProbe') " +
                "AND rolePrincipal.name = N'MyDmsVnRuntimeRole';"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

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

            var script = ReadProvisioningScript("P6RuntimeProbe", "P6MigrationProbe");
            await connection.ExecuteAsync(script);
            await connection.ExecuteAsync(
                "GRANT INSERT, UPDATE ON OBJECT::dbo.Users TO [MyDmsVnRuntimeRole];");
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

            var protectedUserId = await connection.QuerySingleAsync<int>(
                "INSERT dbo.Users " +
                "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm, IsActive) " +
                "OUTPUT INSERTED.UserId VALUES " +
                "(N'P6 Protected', N'P6 PROTECTED', N'P6 Protected', N'Local', N'old-hash', N'old-salt', N'Legacy', 1);");
            await connection.ExecuteAsync("EXECUTE AS USER = N'P6RuntimeProbe';");
            try
            {
                await connection.ExecuteAsync(
                    "UPDATE dbo.Users SET PasswordHash = N'new-hash', PasswordSalt = N'new-salt', " +
                    "PasswordAlgorithm = N'BCrypt', UpdatedAtUtc = SYSUTCDATETIME(), " +
                    "UpdatedByUserId = @userId WHERE UserId = @userId;",
                    new { userId = protectedUserId });
                await Assert.ThrowsAsync<SqlException>(
                    () => connection.ExecuteAsync(
                        "INSERT dbo.Users " +
                        "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm, IsActive) " +
                        "VALUES (N'P6 Rogue', N'P6 ROGUE', N'P6 Rogue', N'Local', N'x', N'x', N'Legacy', 1);"));
                await Assert.ThrowsAsync<SqlException>(
                    () => connection.ExecuteAsync(
                        "UPDATE dbo.Users SET IsActive = 0 WHERE UserId = @userId;",
                        new { userId = protectedUserId }));
            }
            finally
            {
                await connection.ExecuteAsync("REVERT;");
            }

            var protectedUser = await connection.QuerySingleAsync<ProtectedUserProbe>(
                "SELECT PasswordHash, IsActive FROM dbo.Users WHERE UserId = @userId;",
                new { userId = protectedUserId });
            Assert.Equal("new-hash", protectedUser.PasswordHash);
            Assert.True(protectedUser.IsActive);
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.Users WHERE NormalizedUsername = N'P6 ROGUE';"));

            var migrationControl = await connection.QuerySingleAsync<int>(
                "EXECUTE AS USER = N'P6MigrationProbe'; " +
                "SELECT HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL'); " +
                "REVERT;");
            Assert.Equal(1, migrationControl);

            await connection.ExecuteAsync("EXECUTE AS USER = N'P6RuntimeProbe';");
            try
            {
                await new SqlRuntimePrincipalValidator()
                    .ValidateConnectionAsync(connection, System.Threading.CancellationToken.None);
                var compatibility = await new SqlSchemaCompatibilityChecker(database.ConnectionString)
                    .CheckConnectionAsync(connection, System.Threading.CancellationToken.None);
                Assert.True(compatibility.IsCompatible, compatibility.SafeMessage);
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
                var runtimeException = await Assert.ThrowsAsync<DesktopStartupException>(
                    () => new SqlRuntimePrincipalValidator()
                        .ValidateConnectionAsync(connection, System.Threading.CancellationToken.None));
                Assert.Equal("Security.RuntimePrincipalOverprivileged", runtimeException.Code);
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

    private static string ReadProvisioningScript(string runtimeUser, string migrationUser)
    {
        return File.ReadAllText(FindProvisioningScript())
            .Replace("$(RuntimeUser)", runtimeUser)
            .Replace("$(MigrationUser)", migrationUser);
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

    private sealed class ProtectedUserProbe
    {
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
