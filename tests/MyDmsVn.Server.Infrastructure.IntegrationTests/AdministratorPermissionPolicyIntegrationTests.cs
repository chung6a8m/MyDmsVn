using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.DbMigrator.SecurityProvisioning;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class AdministratorPermissionPolicyIntegrationTests
{
    [SqlServerFact]
    public async Task Approved_eligible_permissions_create_owned_Administrator_role_and_grants()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            var registry = PermissionRegistryComposition.CreateDefault();
            var approvals = ApproveEligibleAdministratorGrants(registry);
            var engine = new PermissionSynchronizationEngine();
            var plan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                approvals,
                UtcNow,
                CancellationToken.None);

            Assert.Contains(plan.Changes, change =>
                change.Kind == PermissionSyncChangeKind.CreateAdministratorRole);
            Assert.Equal(
                registry.Definitions.Count(definition =>
                    definition.AdministratorGrantPolicy == AdministratorGrantPolicy.Eligible),
                plan.Changes.Count(change =>
                    change.Kind == PermissionSyncChangeKind.GrantAdministratorPermission));

            await engine.ApplyAsync(
                database.ConnectionString,
                registry,
                approvals,
                plan,
                UtcNow,
                CancellationToken.None);

            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.Roles AS role " +
                "JOIN dbo.SystemManagedRoles AS managed ON managed.RoleId = role.RoleId " +
                "WHERE role.NormalizedRoleName = N'ADMINISTRATOR' " +
                "AND managed.SystemRoleKey = 'Administrator';"));
            var expectedGrants = registry.Definitions.Count(definition =>
                definition.AdministratorGrantPolicy == AdministratorGrantPolicy.Eligible);
            Assert.Equal(expectedGrants, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.ManagedRolePermissionGrants WHERE GrantState = 'Granted';"));
            Assert.Equal(expectedGrants, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.RolePermissions AS permission " +
                "JOIN dbo.SystemManagedRoles AS managed ON managed.RoleId = permission.RoleId " +
                "WHERE managed.SystemRoleKey = 'Administrator';"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Unowned_Administrator_name_collision_is_a_conflict_and_never_adopted()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
                    "VALUES (N'Administrator', N'ADMINISTRATOR');");
            }

            var registry = PermissionRegistryComposition.CreateDefault();
            var approvals = ApproveEligibleAdministratorGrants(registry);
            var engine = new PermissionSynchronizationEngine();
            var plan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                approvals,
                UtcNow,
                CancellationToken.None);

            Assert.Contains(plan.Conflicts, conflict =>
                conflict.Code == "PermissionSync.AdministratorRoleOwnershipCollision");
            var exception = await Assert.ThrowsAsync<PermissionSynchronizationException>(
                () => engine.ApplyAsync(
                    database.ConnectionString,
                    registry,
                    approvals,
                    plan,
                    UtcNow,
                    CancellationToken.None));
            Assert.Equal("PermissionSync.PlanConflict", exception.Code);

            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SystemManagedRoles;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Missing_managed_grant_becomes_revoked_until_explicit_restore()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            var registry = PermissionRegistryComposition.CreateDefault();
            var approvals = ApproveEligibleAdministratorGrants(registry);
            var engine = new PermissionSynchronizationEngine();
            var initial = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                approvals,
                UtcNow,
                CancellationToken.None);
            await engine.ApplyAsync(
                database.ConnectionString,
                registry,
                approvals,
                initial,
                UtcNow,
                CancellationToken.None);
            var key = registry.Definitions.First(definition =>
                definition.AdministratorGrantPolicy == AdministratorGrantPolicy.Eligible).Key;

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "DELETE assignment FROM dbo.RolePermissions AS assignment " +
                    "JOIN dbo.SystemManagedRoles AS managed ON managed.RoleId = assignment.RoleId " +
                    "WHERE managed.SystemRoleKey = 'Administrator' AND assignment.PermissionKey = @Key;",
                    new { Key = key });
            }

            var driftPlan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                approvals,
                UtcNow.AddMinutes(1),
                CancellationToken.None);
            Assert.Contains(driftPlan.Changes, change =>
                change.Kind == PermissionSyncChangeKind.RecordAdministratorRevocation &&
                change.PermissionKey == key);
            await engine.ApplyAsync(
                database.ConnectionString,
                registry,
                approvals,
                driftPlan,
                UtcNow.AddMinutes(1),
                CancellationToken.None);

            var routinePlan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                approvals,
                UtcNow.AddMinutes(2),
                CancellationToken.None);
            Assert.DoesNotContain(routinePlan.Changes, change =>
                change.Kind == PermissionSyncChangeKind.GrantAdministratorPermission &&
                change.PermissionKey == key);

            await engine.RestoreAdministratorGrantAsync(
                database.ConnectionString,
                key,
                approvals.ReleaseId,
                "security-operator",
                "Reviewed restore after incident resolution.",
                UtcNow.AddMinutes(3),
                CancellationToken.None);

            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal("Granted", await verification.QuerySingleAsync<string>(
                "SELECT GrantState FROM dbo.ManagedRolePermissionGrants WHERE PermissionKey = @Key;",
                new { Key = key }));
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.RolePermissions AS assignment " +
                "JOIN dbo.SystemManagedRoles AS managed ON managed.RoleId = assignment.RoleId " +
                "WHERE managed.SystemRoleKey = 'Administrator' AND assignment.PermissionKey = @Key;",
                new { Key = key }));

            await engine.RevokeAdministratorGrantAsync(
                database.ConnectionString,
                key,
                approvals.ReleaseId,
                "security-operator",
                "Reviewed least-privilege revocation.",
                UtcNow.AddMinutes(4),
                CancellationToken.None);
            Assert.Equal("Revoked", await verification.QuerySingleAsync<string>(
                "SELECT GrantState FROM dbo.ManagedRolePermissionGrants WHERE PermissionKey = @Key;",
                new { Key = key }));
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.RolePermissions AS assignment " +
                "JOIN dbo.SystemManagedRoles AS managed ON managed.RoleId = assignment.RoleId " +
                "WHERE managed.SystemRoleKey = 'Administrator' AND assignment.PermissionKey = @Key;",
                new { Key = key }));

            await verification.ExecuteAsync(
                "INSERT dbo.RolePermissions (RoleId, PermissionKey) " +
                "SELECT RoleId, @Key FROM dbo.SystemManagedRoles " +
                "WHERE SystemRoleKey = 'Administrator';",
                new { Key = key });
            var resurrectedPlan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                approvals,
                UtcNow.AddMinutes(5),
                CancellationToken.None);
            Assert.Contains(resurrectedPlan.Conflicts, conflict =>
                conflict.Code == "PermissionSync.ManagedGrantAssignmentConflict" &&
                conflict.PermissionKey == key);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static readonly DateTime UtcNow =
        new DateTime(2026, 10, 14, 2, 0, 0, DateTimeKind.Utc);

    private static PermissionSyncApprovalSet ApproveEligibleAdministratorGrants(
        IPermissionRegistry registry)
    {
        return new PermissionSyncApprovalSet(
            registry.ReleaseId,
            "release-operator",
            UtcNow.AddHours(4),
            registry.Definitions.Select(definition => new PermissionSyncApproval(
                definition.Key,
                PermissionCatalogDecision.Approved,
                definition.AdministratorGrantPolicy == AdministratorGrantPolicy.Eligible
                    ? AdministratorGrantDecision.Approved
                    : AdministratorGrantDecision.Pending)));
    }

    private static async Task<SqlTestDatabase> CreateMigratedDatabaseAsync()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
        Assert.True(result.Successful, result.Error?.ToString());
        return database;
    }
}
