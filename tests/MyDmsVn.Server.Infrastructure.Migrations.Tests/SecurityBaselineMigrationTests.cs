using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SecurityBaselineMigrationTests
{
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Fresh_baseline_has_only_manual_rbac_and_empty_superuser_binding()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());

            using var connection = new SqlConnection(database.ConnectionString);
            var state = await connection.QuerySingleAsync<BaselineState>(
                "SELECT " +
                "CAST(CASE WHEN OBJECT_ID(N'dbo.SystemSecuritySettings', N'U') IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasBindingTable, " +
                "CAST(CASE WHEN OBJECT_ID(N'dbo.RolePermissions', N'U') IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasRoleAssignments, " +
                "CAST(CASE WHEN OBJECT_ID(N'dbo.UserPermissions', N'U') IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasUserAssignments, " +
                "(SELECT COUNT_BIG(*) FROM sys.objects AS obsolete " +
                "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = obsolete.schema_id " +
                "WHERE ownerSchema.name = N'dbo' AND obsolete.name IN " +
                "(N'Permissions', N'PermissionLegacyAssignmentDrift', N'PermissionSynchronizationState', " +
                "N'SystemManagedRoles', N'PermissionGrantApprovals', N'ManagedRolePermissionGrants', " +
                "N'SecurityBootstrapState', N'PermissionSecurityAudit')) AS ObsoleteObjectCount, " +
                "CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.database_permissions AS permissionRow " +
                "INNER JOIN sys.objects AS obsolete ON obsolete.object_id = permissionRow.major_id " +
                "INNER JOIN sys.schemas AS ownerSchema ON ownerSchema.schema_id = obsolete.schema_id " +
                "WHERE ownerSchema.name = N'dbo' AND obsolete.name IN " +
                "(N'Permissions', N'PermissionLegacyAssignmentDrift', N'PermissionSynchronizationState', " +
                "N'SystemManagedRoles', N'PermissionGrantApprovals', N'ManagedRolePermissionGrants', " +
                "N'SecurityBootstrapState', N'PermissionSecurityAudit')) THEN 1 ELSE 0 END AS bit) " +
                "AS HasObsoleteObjectPermissions");

            Assert.True(state.HasBindingTable);
            Assert.True(state.HasRoleAssignments);
            Assert.True(state.HasUserAssignments);
            Assert.Equal(0, state.ObsoleteObjectCount);
            Assert.False(state.HasObsoleteObjectPermissions);
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SystemSecuritySettings;"));
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.RolePermissions;"));
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.UserPermissions;"));
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.SystemSecuritySettings') " +
                "AND name = N'CK_SystemSecuritySettings_Singleton' AND is_disabled = 0 AND is_not_trusted = 0;"));
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.SystemSecuritySettings') " +
                "AND referenced_object_id = OBJECT_ID(N'dbo.Users') AND delete_referential_action = 0;"));
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SystemSecuritySettings') " +
                "AND name = N'UX_SystemSecuritySettings_SuperuserUserId' AND is_unique = 1;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Unsupported_nonempty_database_is_rejected_before_migration_ddl()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync("CREATE TABLE dbo.LegacyUsers (Id int NOT NULL);");
            }

            var exception = Assert.Throws<DatabaseMigrationPreflightException>(
                () => new DatabaseMigrationRunner().Migrate(database.ConnectionString));
            Assert.Equal("Migration.UnknownHistory", exception.Code);

            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.LegacyUsers');"));
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.SchemaVersions');"));
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.Users');"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private sealed class BaselineState
    {
        public bool HasBindingTable { get; set; }
        public bool HasRoleAssignments { get; set; }
        public bool HasUserAssignments { get; set; }
        public long ObsoleteObjectCount { get; set; }
        public bool HasObsoleteObjectPermissions { get; set; }
    }
}
