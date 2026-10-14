using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
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

    [SqlServerFact]
    public async Task Restore_rejects_managed_administrator_role_renamed_out_of_band()
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
            await engine.ApplyAsync(
                database.ConnectionString,
                registry,
                approvals,
                plan,
                UtcNow,
                CancellationToken.None);
            var key = registry.Definitions.First(definition =>
                definition.AdministratorGrantPolicy == AdministratorGrantPolicy.Eligible).Key;
            await engine.RevokeAdministratorGrantAsync(
                database.ConnectionString,
                key,
                approvals.ReleaseId,
                "security-operator",
                "Prepare reviewed restore regression.",
                UtcNow.AddMinutes(1),
                CancellationToken.None);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "UPDATE role SET RoleName = N'Repurposed role', NormalizedRoleName = N'REPURPOSED ROLE' " +
                    "FROM dbo.Roles AS role " +
                    "JOIN dbo.SystemManagedRoles AS managed ON managed.RoleId = role.RoleId " +
                    "WHERE managed.SystemRoleKey = N'Administrator';");
            }

            var exception = await Assert.ThrowsAsync<PermissionSynchronizationException>(
                () => engine.RestoreAdministratorGrantAsync(
                    database.ConnectionString,
                    key,
                    approvals.ReleaseId,
                    "security-operator",
                    "Attempt restore after ownership drift.",
                    UtcNow.AddMinutes(2),
                    CancellationToken.None));

            Assert.Equal("PermissionSync.ManagedRoleOwnershipConflict", exception.Code);
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal("Revoked", await verification.QuerySingleAsync<string>(
                "SELECT GrantState FROM dbo.ManagedRolePermissionGrants WHERE PermissionKey = @Key;",
                new { Key = key }));
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.RolePermissions AS assignment " +
                "JOIN dbo.SystemManagedRoles AS managed ON managed.RoleId = assignment.RoleId " +
                "WHERE managed.SystemRoleKey = N'Administrator' AND assignment.PermissionKey = @Key;",
                new { Key = key }));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Revoke_rejects_inactive_managed_administrator_role()
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
            await engine.ApplyAsync(
                database.ConnectionString,
                registry,
                approvals,
                plan,
                UtcNow,
                CancellationToken.None);
            var key = registry.Definitions.First(definition =>
                definition.AdministratorGrantPolicy == AdministratorGrantPolicy.Eligible).Key;

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "UPDATE role SET IsActive = 0 " +
                    "FROM dbo.Roles AS role " +
                    "JOIN dbo.SystemManagedRoles AS managed ON managed.RoleId = role.RoleId " +
                    "WHERE managed.SystemRoleKey = N'Administrator';");
            }

            var exception = await Assert.ThrowsAsync<PermissionSynchronizationException>(
                () => engine.RevokeAdministratorGrantAsync(
                    database.ConnectionString,
                    key,
                    approvals.ReleaseId,
                    "security-operator",
                    "Attempt revoke after ownership drift.",
                    UtcNow.AddMinutes(1),
                    CancellationToken.None));

            Assert.Equal("PermissionSync.ManagedRoleOwnershipConflict", exception.Code);
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal("Granted", await verification.QuerySingleAsync<string>(
                "SELECT GrantState FROM dbo.ManagedRolePermissionGrants WHERE PermissionKey = @Key;",
                new { Key = key }));
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.RolePermissions AS assignment " +
                "JOIN dbo.SystemManagedRoles AS managed ON managed.RoleId = assignment.RoleId " +
                "WHERE managed.SystemRoleKey = N'Administrator' AND assignment.PermissionKey = @Key;",
                new { Key = key }));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Later_release_grants_only_independently_approved_new_and_late_permissions()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            var engine = new PermissionSynchronizationEngine();
            var baseline = PermissionRegistryComposition.CreateDefault();
            var baselineApprovals = new PermissionSyncApprovalSet(
                baseline.ReleaseId,
                "release-operator",
                UtcNow.AddHours(4),
                baseline.Definitions.Select(definition => new PermissionSyncApproval(
                    definition.Key,
                    PermissionCatalogDecision.Approved,
                    AdministratorGrantDecision.Approved)));
            var baselinePlan = await engine.CreatePlanAsync(
                database.ConnectionString,
                baseline,
                baselineApprovals,
                UtcNow,
                CancellationToken.None);
            await engine.ApplyAsync(
                database.ConnectionString,
                baseline,
                baselineApprovals,
                baselinePlan,
                UtcNow,
                CancellationToken.None);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "DECLARE @RoleId int; " +
                    "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
                    "VALUES (N'Normal role', N'NORMAL ROLE'); SET @RoleId = SCOPE_IDENTITY(); " +
                    "INSERT dbo.RolePermissions (RoleId, PermissionKey) VALUES (@RoleId, @BaselineKey);",
                    new { BaselineKey = baseline.Definitions[0].Key });
            }

            var version2 = CreateSalesRegistry("release-v2");
            var version2Approvals = CreateSalesApprovals(
                version2,
                approveSensitiveAdministratorGrant: false);
            var version2Plan = await engine.CreatePlanAsync(
                database.ConnectionString,
                version2,
                version2Approvals,
                UtcNow.AddMinutes(1),
                CancellationToken.None);
            Assert.Equal(3, version2Plan.Changes.Count(change =>
                change.Kind == PermissionSyncChangeKind.AddDefinition &&
                change.PermissionKey.StartsWith("Sales.Orders.", StringComparison.Ordinal)));
            await engine.ApplyAsync(
                database.ConnectionString,
                version2,
                version2Approvals,
                version2Plan,
                UtcNow.AddMinutes(1),
                CancellationToken.None);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                Assert.Equal("Active", await connection.QuerySingleAsync<string>(
                    "SELECT CatalogStatus FROM dbo.Permissions WHERE PermissionKey = 'Sales.Orders.Read';"));
                Assert.Equal("PendingActivation", await connection.QuerySingleAsync<string>(
                    "SELECT CatalogStatus FROM dbo.Permissions WHERE PermissionKey = 'Sales.Orders.Write';"));
                Assert.Equal("Active", await connection.QuerySingleAsync<string>(
                    "SELECT CatalogStatus FROM dbo.Permissions WHERE PermissionKey = 'Sales.Orders.Approve';"));
                Assert.Equal(1, await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM dbo.ManagedRolePermissionGrants " +
                    "WHERE PermissionKey = 'Sales.Orders.Read' AND GrantState = 'Granted';"));
                Assert.Equal(0, await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM dbo.ManagedRolePermissionGrants " +
                    "WHERE PermissionKey IN ('Sales.Orders.Write', 'Sales.Orders.Approve') " +
                    "AND GrantState = 'Granted';"));
                Assert.Equal(0, await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM dbo.RolePermissions AS assignment " +
                    "JOIN dbo.Roles AS role ON role.RoleId = assignment.RoleId " +
                    "WHERE role.NormalizedRoleName = N'NORMAL ROLE' " +
                    "AND assignment.PermissionKey LIKE N'Sales.Orders.%';"));
            }

            var version3 = CreateSalesRegistry("release-v3");
            var version3Approvals = CreateSalesApprovals(
                version3,
                approveSensitiveAdministratorGrant: true);
            var version3Plan = await engine.CreatePlanAsync(
                database.ConnectionString,
                version3,
                version3Approvals,
                UtcNow.AddMinutes(2),
                CancellationToken.None);
            Assert.Contains(version3Plan.Changes, change =>
                change.Kind == PermissionSyncChangeKind.GrantAdministratorPermission &&
                change.PermissionKey == "Sales.Orders.Approve");
            await engine.ApplyAsync(
                database.ConnectionString,
                version3,
                version3Approvals,
                version3Plan,
                UtcNow.AddMinutes(2),
                CancellationToken.None);
            var repeat = await engine.CreatePlanAsync(
                database.ConnectionString,
                version3,
                version3Approvals,
                UtcNow.AddMinutes(3),
                CancellationToken.None);
            Assert.DoesNotContain(repeat.Changes, change =>
                change.Kind == PermissionSyncChangeKind.GrantAdministratorPermission &&
                change.PermissionKey == "Sales.Orders.Approve");
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

    private static PermissionRegistry CreateSalesRegistry(string releaseId)
    {
        var definitions = new List<PermissionDefinition>(
            PermissionRegistryComposition.CreateDefault().Definitions)
        {
            new PermissionDefinition(
                "Sales.Orders.Read",
                "Sales",
                "Read sales orders.",
                PermissionLifecycleStatus.Active,
                AdministratorGrantPolicy.Eligible),
            new PermissionDefinition(
                "Sales.Orders.Write",
                "Sales",
                "Create and update sales orders.",
                PermissionLifecycleStatus.Active,
                AdministratorGrantPolicy.Eligible),
            new PermissionDefinition(
                "Sales.Orders.Approve",
                "Sales",
                "Approve sales orders.",
                PermissionLifecycleStatus.Active,
                AdministratorGrantPolicy.Sensitive),
        };
        return PermissionRegistry.Create(releaseId, definitions);
    }

    private static PermissionSyncApprovalSet CreateSalesApprovals(
        IPermissionRegistry registry,
        bool approveSensitiveAdministratorGrant)
    {
        return new PermissionSyncApprovalSet(
            registry.ReleaseId,
            "release-operator",
            UtcNow.AddHours(4),
            registry.Definitions.Select(definition =>
            {
                if (definition.Key == "Sales.Orders.Write")
                {
                    return new PermissionSyncApproval(
                        definition.Key,
                        PermissionCatalogDecision.Pending,
                        AdministratorGrantDecision.Approved);
                }

                if (definition.Key == "Sales.Orders.Approve")
                {
                    return new PermissionSyncApproval(
                        definition.Key,
                        PermissionCatalogDecision.Approved,
                        approveSensitiveAdministratorGrant
                            ? AdministratorGrantDecision.Approved
                            : AdministratorGrantDecision.Pending);
                }

                return new PermissionSyncApproval(
                    definition.Key,
                    PermissionCatalogDecision.Approved,
                    AdministratorGrantDecision.Approved);
            }));
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
