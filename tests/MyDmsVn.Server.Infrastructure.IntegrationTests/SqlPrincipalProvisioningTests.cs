using System;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Security;
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
    public async Task Provisioning_rejects_direct_user_impersonation_permission()
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
                "CREATE USER [P6ImpersonatingRuntimeProbe] WITHOUT LOGIN; " +
                "CREATE USER [P6ImpersonatedMigrationProbe] WITHOUT LOGIN;");

            var script = ReadProvisioningScript(
                "P6ImpersonatingRuntimeProbe",
                "P6ImpersonatedMigrationProbe");
            await connection.ExecuteAsync(script);
            await connection.ExecuteAsync(
                "GRANT IMPERSONATE ON USER::[P6ImpersonatedMigrationProbe] " +
                "TO [P6ImpersonatingRuntimeProbe];");

            var exception = await Assert.ThrowsAsync<SqlException>(
                () => connection.ExecuteAsync(script));

            Assert.Contains("elevated", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Startup_rejects_direct_user_impersonation_permission()
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
                "CREATE USER [P6StartupImpersonatingRuntimeProbe] WITHOUT LOGIN; " +
                "CREATE USER [P6StartupImpersonatedMigrationProbe] WITHOUT LOGIN;");
            await connection.ExecuteAsync(
                ReadProvisioningScript(
                    "P6StartupImpersonatingRuntimeProbe",
                    "P6StartupImpersonatedMigrationProbe"));
            await connection.ExecuteAsync(
                "GRANT IMPERSONATE ON USER::[P6StartupImpersonatedMigrationProbe] " +
                "TO [P6StartupImpersonatingRuntimeProbe]; " +
                "EXECUTE AS USER = N'P6StartupImpersonatingRuntimeProbe';");
            try
            {
                var exception = await Assert.ThrowsAsync<DesktopStartupException>(
                    () => new SqlRuntimePrincipalValidator()
                        .ValidateConnectionAsync(connection, System.Threading.CancellationToken.None));

                Assert.Equal("Security.RuntimePrincipalOverprivileged", exception.Code);
            }
            finally
            {
                await connection.ExecuteAsync("REVERT;");
            }
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Provisioning_and_startup_reject_direct_users_insert_permission()
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
                "CREATE USER [P6DirectDmlRuntimeProbe] WITHOUT LOGIN; " +
                "CREATE USER [P6DirectDmlMigrationProbe] WITHOUT LOGIN;");
            var script = ReadProvisioningScript(
                "P6DirectDmlRuntimeProbe",
                "P6DirectDmlMigrationProbe");
            await connection.ExecuteAsync(script);
            await connection.ExecuteAsync(
                "GRANT INSERT ON OBJECT::dbo.Users TO [P6DirectDmlRuntimeProbe]; " +
                "EXECUTE AS USER = N'P6DirectDmlRuntimeProbe';");
            try
            {
                Assert.Equal(1, await connection.QuerySingleAsync<int>(
                    "SELECT HAS_PERMS_BY_NAME(N'dbo.Users', N'OBJECT', N'INSERT');"));
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
                () => connection.ExecuteAsync(script));

            Assert.Contains("elevated", provisioningException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "EXECUTE AS USER = N'P6DirectDmlRuntimeProbe'; " +
                "SELECT HAS_PERMS_BY_NAME(" +
                "N'dbo.Users', N'OBJECT', N'UPDATE', N'PasswordHash', N'COLUMN'); " +
                "REVERT;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Provisioning_and_startup_reject_disallowed_users_column_update()
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
                "CREATE USER [P6ColumnDmlRuntimeProbe] WITHOUT LOGIN; " +
                "CREATE USER [P6ColumnDmlMigrationProbe] WITHOUT LOGIN;");
            var script = ReadProvisioningScript(
                "P6ColumnDmlRuntimeProbe",
                "P6ColumnDmlMigrationProbe");
            await connection.ExecuteAsync(script);
            await connection.ExecuteAsync(
                "GRANT UPDATE (IsActive) ON OBJECT::dbo.Users TO [P6ColumnDmlRuntimeProbe]; " +
                "EXECUTE AS USER = N'P6ColumnDmlRuntimeProbe';");
            try
            {
                Assert.Equal(1, await connection.QuerySingleAsync<int>(
                    "SELECT HAS_PERMS_BY_NAME(" +
                    "N'dbo.Users', N'OBJECT', N'UPDATE', N'IsActive', N'COLUMN');"));
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
                () => connection.ExecuteAsync(script));

            Assert.Contains("elevated", provisioningException.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Provisioning_and_startup_reject_schema_dml_permission()
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
                "CREATE USER [P6SchemaDmlRuntimeProbe] WITHOUT LOGIN; " +
                "CREATE USER [P6SchemaDmlMigrationProbe] WITHOUT LOGIN;");
            var script = ReadProvisioningScript(
                "P6SchemaDmlRuntimeProbe",
                "P6SchemaDmlMigrationProbe");
            await connection.ExecuteAsync(script);
            await connection.ExecuteAsync(
                "GRANT SELECT, UPDATE ON SCHEMA::dbo TO [P6SchemaDmlRuntimeProbe]; " +
                "EXECUTE AS USER = N'P6SchemaDmlRuntimeProbe';");
            try
            {
                Assert.Equal(1, await connection.QuerySingleAsync<int>(
                    "SELECT HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'UPDATE');"));
                Assert.Equal(1, await connection.QuerySingleAsync<int>(
                    "SELECT HAS_PERMS_BY_NAME(" +
                    "N'dbo.Users', N'OBJECT', N'UPDATE', N'IsActive', N'COLUMN');"));
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
                () => connection.ExecuteAsync(script));

            Assert.Contains("elevated", provisioningException.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Provisioning_and_startup_reject_full_permission_sync_state_read()
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
                "CREATE USER [P6SyncStateRuntimeProbe] WITHOUT LOGIN; " +
                "CREATE USER [P6SyncStateMigrationProbe] WITHOUT LOGIN;");
            var script = ReadProvisioningScript(
                "P6SyncStateRuntimeProbe",
                "P6SyncStateMigrationProbe");
            await connection.ExecuteAsync(script);
            await connection.ExecuteAsync(
                "GRANT SELECT ON OBJECT::dbo.PermissionSynchronizationState TO [P6SyncStateRuntimeProbe]; " +
                "EXECUTE AS USER = N'P6SyncStateRuntimeProbe';");
            try
            {
                Assert.Equal(1, await connection.QuerySingleAsync<int>(
                    "SELECT HAS_PERMS_BY_NAME(N'dbo.PermissionSynchronizationState', " +
                    "N'OBJECT', N'SELECT', N'DatabaseIdentity', N'COLUMN');"));
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
                () => connection.ExecuteAsync(script));

            Assert.Contains("elevated", provisioningException.Message, StringComparison.OrdinalIgnoreCase);
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
                "CREATE USER [P6MigrationProbe] WITHOUT LOGIN;");

            var script = ReadProvisioningScript("P6RuntimeProbe", "P6MigrationProbe");
            await connection.ExecuteAsync(script);
            await connection.ExecuteAsync(
                "GRANT INSERT, UPDATE ON OBJECT::dbo.Users TO [MyDmsVnRuntimeRole];");
            await connection.ExecuteAsync(script);
            await connection.ExecuteAsync(
                "UPDATE dbo.PermissionSynchronizationState SET LastAppliedReleaseId = @releaseId " +
                "WHERE StateId = 1;",
                new { releaseId = PermissionReleaseManifest.CurrentReleaseId });

            var permissions = await connection.QuerySingleAsync<PermissionProbe>(
                "EXECUTE AS USER = N'P6RuntimeProbe'; " +
                "SELECT " +
                "HAS_PERMS_BY_NAME(N'dbo.Products', N'OBJECT', N'SELECT') AS CanReadProducts, " +
                "HAS_PERMS_BY_NAME(N'dbo.Products', N'OBJECT', N'INSERT') AS CanInsertProducts, " +
                "HAS_PERMS_BY_NAME(N'dbo.Products', N'OBJECT', N'UPDATE') AS CanUpdateProducts, " +
                "HAS_PERMS_BY_NAME(N'dbo.GoodsReceiptLines', N'OBJECT', N'DELETE') AS CanDeleteReceiptLines, " +
                "HAS_PERMS_BY_NAME(N'dbo.StockLedger', N'OBJECT', N'INSERT') AS CanInsertLedger, " +
                "HAS_PERMS_BY_NAME(N'dbo.StockBalances', N'OBJECT', N'UPDATE') AS CanUpdateBalances, " +
                "HAS_PERMS_BY_NAME(N'dbo.Permissions', N'OBJECT', N'SELECT') AS CanReadPermissions, " +
                "HAS_PERMS_BY_NAME(N'dbo.Permissions', N'OBJECT', N'UPDATE') AS CanUpdatePermissions, " +
                "HAS_PERMS_BY_NAME(N'dbo.PermissionSynchronizationState', N'OBJECT', N'SELECT') AS CanReadSyncState, " +
                "HAS_PERMS_BY_NAME(N'dbo.PermissionSynchronizationState', N'OBJECT', N'SELECT', " +
                "N'LastAppliedReleaseId', N'COLUMN') AS CanReadSyncRelease, " +
                "HAS_PERMS_BY_NAME(N'dbo.PermissionSynchronizationState', N'OBJECT', N'SELECT', " +
                "N'DatabaseIdentity', N'COLUMN') AS CanReadSyncDatabaseIdentity, " +
                "HAS_PERMS_BY_NAME(N'dbo.SecurityBootstrapState', N'OBJECT', N'INSERT') AS CanInsertBootstrapState, " +
                "HAS_PERMS_BY_NAME(N'dbo.GoodsReceiptNumberSequence', N'OBJECT', N'UPDATE') AS CanUseReceiptSequence, " +
                "HAS_PERMS_BY_NAME(N'dbo.CatalogTextContainsNul', N'OBJECT', N'VIEW DEFINITION') AS CanInspectManifestFunction, " +
                "HAS_PERMS_BY_NAME(N'dbo.Employees', N'OBJECT', N'VIEW DEFINITION') AS CanInspectEmployeeIndexDefinition, " +
                "HAS_PERMS_BY_NAME(N'dbo.StockLedger', N'OBJECT', N'VIEW DEFINITION') AS CanInspectAppendOnlyTriggerDefinition, " +
                "HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'ALTER') AS CanAlterSchema, " +
                "HAS_PERMS_BY_NAME(N'dbo.SchemaVersions', N'OBJECT', N'SELECT') AS CanReadJournal, " +
                "HAS_PERMS_BY_NAME(N'dbo.SchemaVersions', N'OBJECT', N'UPDATE') AS CanUpdateJournal; " +
                "REVERT;");

            Assert.Equal(1, permissions.CanReadProducts);
            Assert.Equal(1, permissions.CanInsertProducts);
            Assert.Equal(1, permissions.CanUpdateProducts);
            Assert.Equal(1, permissions.CanDeleteReceiptLines);
            Assert.Equal(1, permissions.CanInsertLedger);
            Assert.Equal(1, permissions.CanUpdateBalances);
            Assert.Equal(1, permissions.CanReadPermissions);
            Assert.Equal(0, permissions.CanUpdatePermissions);
            Assert.Equal(0, permissions.CanReadSyncState);
            Assert.Equal(1, permissions.CanReadSyncRelease);
            Assert.Equal(0, permissions.CanReadSyncDatabaseIdentity);
            Assert.Equal(0, permissions.CanInsertBootstrapState);
            Assert.Equal(1, permissions.CanUseReceiptSequence);
            Assert.Equal(1, permissions.CanInspectManifestFunction);
            Assert.Equal(1, permissions.CanInspectEmployeeIndexDefinition);
            Assert.Equal(1, permissions.CanInspectAppendOnlyTriggerDefinition);
            Assert.Equal(0, permissions.CanAlterSchema);
            Assert.Equal(1, permissions.CanReadJournal);
            Assert.Equal(0, permissions.CanUpdateJournal);

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

    [SqlServerFact]
    public async Task Runtime_preflight_rejects_unexpected_filtered_unique_index()
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
                "CREATE USER [P6FilteredIndexRuntimeProbe] WITHOUT LOGIN; " +
                "CREATE USER [P6FilteredIndexMigrationProbe] WITHOUT LOGIN;");
            await connection.ExecuteAsync(
                ReadProvisioningScript(
                    "P6FilteredIndexRuntimeProbe",
                    "P6FilteredIndexMigrationProbe"));
            await connection.ExecuteAsync(
                "DROP INDEX UX_Products_Code ON dbo.Products; " +
                "CREATE UNIQUE INDEX UX_Products_Code ON dbo.Products (Code) WHERE IsActive = 1; " +
                "EXECUTE AS USER = N'P6FilteredIndexRuntimeProbe';");
            try
            {
                var compatibility = await new SqlSchemaCompatibilityChecker(database.ConnectionString)
                    .CheckConnectionAsync(connection, System.Threading.CancellationToken.None);

                Assert.Equal(SchemaCompatibilityStatus.Partial, compatibility.Status);
            }
            finally
            {
                await connection.ExecuteAsync("REVERT;");
            }
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Runtime_preflight_detects_unlisted_object_without_data_grants()
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
                "CREATE USER [P6MetadataRuntimeProbe] WITHOUT LOGIN; " +
                "CREATE USER [P6MetadataMigrationProbe] WITHOUT LOGIN;");
            await connection.ExecuteAsync(
                ReadProvisioningScript(
                    "P6MetadataRuntimeProbe",
                    "P6MetadataMigrationProbe"));
            await connection.ExecuteAsync(
                "CREATE TABLE dbo.P6InvisibleUnlistedData (SecretId int NOT NULL);");

            var adminCompatibility = await new SqlSchemaCompatibilityChecker(database.ConnectionString)
                .CheckConnectionAsync(connection, System.Threading.CancellationToken.None);

            await connection.ExecuteAsync("EXECUTE AS USER = N'P6MetadataRuntimeProbe';");
            try
            {
                var canReadUnlistedObject = await connection.QuerySingleAsync<int>(
                    "SELECT HAS_PERMS_BY_NAME(" +
                    "N'dbo.P6InvisibleUnlistedData', N'OBJECT', N'SELECT');");
                var runtimeCompatibility = await new SqlSchemaCompatibilityChecker(database.ConnectionString)
                    .CheckConnectionAsync(connection, System.Threading.CancellationToken.None);

                Assert.Equal(0, canReadUnlistedObject);
                Assert.Equal(SchemaCompatibilityStatus.UnknownHistory, adminCompatibility.Status);
                Assert.Equal(adminCompatibility.Status, runtimeCompatibility.Status);
            }
            finally
            {
                await connection.ExecuteAsync("REVERT;");
            }
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

        public int CanReadPermissions { get; set; }

        public int CanUpdatePermissions { get; set; }

        public int CanReadSyncState { get; set; }

        public int CanReadSyncRelease { get; set; }

        public int CanReadSyncDatabaseIdentity { get; set; }

        public int CanInsertBootstrapState { get; set; }

        public int CanInspectManifestFunction { get; set; }

        public int CanInspectEmployeeIndexDefinition { get; set; }

        public int CanInspectAppendOnlyTriggerDefinition { get; set; }

        public int CanAlterSchema { get; set; }

        public int CanReadJournal { get; set; }

        public int CanUpdateJournal { get; set; }

    }

    private sealed class ProtectedUserProbe
    {
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
