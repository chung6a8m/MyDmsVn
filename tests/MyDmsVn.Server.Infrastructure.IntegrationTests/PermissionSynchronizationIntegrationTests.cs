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

public sealed class PermissionSynchronizationIntegrationTests
{
    [SqlServerFact]
    public async Task Approved_plan_activates_manifest_and_repeat_plan_is_idempotent()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            var registry = PermissionRegistryComposition.CreateDefault();
            var approvals = ApproveCatalog(registry);
            var engine = new PermissionSynchronizationEngine();

            var plan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                approvals,
                UtcNow,
                CancellationToken.None);

            Assert.Equal(64, plan.PlanId.Length);
            Assert.Equal(registry.Definitions.Count, plan.Changes.Count(
                change => change.Kind == PermissionSyncChangeKind.AddDefinition));
            Assert.Empty(plan.Conflicts);

            var result = await engine.ApplyAsync(
                database.ConnectionString,
                registry,
                approvals,
                plan,
                UtcNow,
                CancellationToken.None);
            Assert.Equal(plan.PlanId, result.PlanId);

            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(
                registry.Definitions.Count,
                await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM dbo.Permissions WHERE CatalogStatus = N'Active';"));
            Assert.Equal(
                registry.Definitions.Count,
                await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM dbo.PermissionGrantApprovals " +
                    "WHERE CatalogDecision = N'Approved' " +
                    "AND AdministratorGrantDecision = N'Pending';"));

            var repeatPlan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                approvals,
                UtcNow.AddMinutes(1),
                CancellationToken.None);
            Assert.DoesNotContain(
                repeatPlan.Changes,
                change => change.Kind == PermissionSyncChangeKind.AddDefinition ||
                          change.Kind == PermissionSyncChangeKind.UpdateDefinition ||
                          change.Kind == PermissionSyncChangeKind.ActivateDefinition);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Apply_rejects_database_drift_and_records_no_committed_sync()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            var registry = PermissionRegistryComposition.CreateDefault();
            var approvals = ApproveCatalog(registry);
            var engine = new PermissionSynchronizationEngine();
            var plan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                approvals,
                UtcNow,
                CancellationToken.None);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "INSERT dbo.Permissions " +
                    "(PermissionKey, Module, Description, CatalogStatus, AdministratorGrantPolicy) " +
                    "VALUES (N'Legacy.Concurrent.Read', N'Legacy', N'Concurrent drift.', " +
                    "N'Unregistered', N'NotEligible');");
            }

            var exception = await Assert.ThrowsAsync<PermissionSynchronizationException>(
                () => engine.ApplyAsync(
                    database.ConnectionString,
                    registry,
                    approvals,
                    plan,
                    UtcNow,
                    CancellationToken.None));
            Assert.Equal("PermissionSync.StalePlan", exception.Code);

            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.Permissions WHERE Module <> N'Legacy';"));
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.PermissionSynchronizationState " +
                "WHERE LastAppliedPlanId IS NOT NULL;"));
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.PermissionSecurityAudit " +
                "WHERE Operation = N'PermissionSync.Apply' AND Outcome = N'Rejected' " +
                "AND ReasonCode = N'PermissionSync.StalePlan';"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Apply_rejects_assignment_drift_not_visible_in_the_catalog()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            var registry = PermissionRegistryComposition.CreateDefault();
            var approvals = ApproveCatalog(registry);
            var engine = new PermissionSynchronizationEngine();
            var plan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                approvals,
                UtcNow,
                CancellationToken.None);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                var roleId = await connection.QuerySingleAsync<int>(
                    "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
                    "OUTPUT INSERTED.RoleId VALUES (N'Concurrent', N'CONCURRENT');");
                await connection.ExecuteAsync(
                    "INSERT dbo.RolePermissions (RoleId, PermissionKey) " +
                    "VALUES (@RoleId, @PermissionKey);",
                    new { RoleId = roleId, PermissionKey = registry.Definitions[0].Key });
            }

            var exception = await Assert.ThrowsAsync<PermissionSynchronizationException>(
                () => engine.ApplyAsync(
                    database.ConnectionString,
                    registry,
                    approvals,
                    plan,
                    UtcNow,
                    CancellationToken.None));

            Assert.Equal("PermissionSync.StalePlan", exception.Code);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Apply_rejects_expired_approval_and_audits_without_catalog_mutation()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            var registry = PermissionRegistryComposition.CreateDefault();
            var approvals = ApproveCatalog(registry);
            var engine = new PermissionSynchronizationEngine();
            var plan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                approvals,
                UtcNow,
                CancellationToken.None);

            var exception = await Assert.ThrowsAsync<PermissionSynchronizationException>(
                () => engine.ApplyAsync(
                    database.ConnectionString,
                    registry,
                    approvals,
                    plan,
                    approvals.ExpiresAtUtc,
                    CancellationToken.None));

            Assert.Equal("PermissionSync.ApprovalExpired", exception.Code);
            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.Permissions;"));
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.PermissionSecurityAudit " +
                "WHERE Outcome = N'Rejected' AND ReasonCode = N'PermissionSync.ApprovalExpired';"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static readonly DateTime UtcNow =
        new DateTime(2026, 10, 14, 1, 0, 0, DateTimeKind.Utc);

    private static PermissionSyncApprovalSet ApproveCatalog(IPermissionRegistry registry)
    {
        return new PermissionSyncApprovalSet(
            registry.ReleaseId,
            "release-operator",
            UtcNow.AddHours(4),
            registry.Definitions.Select(
                definition => new PermissionSyncApproval(
                    definition.Key,
                    PermissionCatalogDecision.Approved,
                    AdministratorGrantDecision.Pending)));
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
