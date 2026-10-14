using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.DbMigrator.SecurityProvisioning;
using MyDmsVn.Server.Infrastructure;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class PermissionSynchronizationIntegrationTests
{
    [Fact]
    public void Sql_datetime2_timestamp_is_canonicalized_as_utc_without_local_time_conversion()
    {
        var sqlTimestamp = new DateTime(
            2026,
            10,
            14,
            1,
            2,
            3,
            DateTimeKind.Unspecified);
        var format = typeof(PermissionSynchronizationEngine).GetMethod(
            "Format",
            BindingFlags.NonPublic | BindingFlags.Static);

        var canonical = Assert.IsType<string>(format!.Invoke(null, new object?[] { sqlTimestamp }));

        Assert.Equal("2026-10-14T01:02:03.0000000Z", canonical);
    }

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
    public async Task Apply_rejects_administrator_membership_added_after_grant_plan_review()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            var registry = PermissionRegistryComposition.CreateDefault();
            var baselineApprovals = ApproveCatalog(registry);
            var engine = new PermissionSynchronizationEngine();
            var baselinePlan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                baselineApprovals,
                UtcNow,
                CancellationToken.None);
            await engine.ApplyAsync(
                database.ConnectionString,
                registry,
                baselineApprovals,
                baselinePlan,
                UtcNow,
                CancellationToken.None);

            var grantApprovals = new PermissionSyncApprovalSet(
                registry.ReleaseId,
                "release-operator",
                UtcNow.AddHours(4),
                registry.Definitions.Select(definition => new PermissionSyncApproval(
                    definition.Key,
                    PermissionCatalogDecision.Approved,
                    definition.AdministratorGrantPolicy == AdministratorGrantPolicy.Eligible
                        ? AdministratorGrantDecision.Approved
                        : AdministratorGrantDecision.Pending)));
            var reviewedPlan = await engine.CreatePlanAsync(
                database.ConnectionString,
                registry,
                grantApprovals,
                UtcNow.AddMinutes(1),
                CancellationToken.None);
            Assert.Contains(reviewedPlan.Changes, change =>
                change.Kind == PermissionSyncChangeKind.GrantAdministratorPermission);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "DECLARE @UserId int; " +
                    "INSERT dbo.Users " +
                    "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm) " +
                    "VALUES (N'late-admin', N'LATE-ADMIN', N'Late Administrator', N'Local', N'hash', N'', N'BCrypt'); " +
                    "SET @UserId = SCOPE_IDENTITY(); " +
                    "INSERT dbo.UserRoles (UserId, RoleId) " +
                    "SELECT @UserId, RoleId FROM dbo.SystemManagedRoles " +
                    "WHERE SystemRoleKey = N'Administrator';");
            }

            var exception = await Assert.ThrowsAsync<PermissionSynchronizationException>(
                () => engine.ApplyAsync(
                    database.ConnectionString,
                    registry,
                    grantApprovals,
                    reviewedPlan,
                    UtcNow.AddMinutes(1),
                    CancellationToken.None));

            Assert.Equal("PermissionSync.StalePlan", exception.Code);
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.ManagedRolePermissionGrants WHERE GrantState = N'Granted';"));
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

    [SqlServerFact]
    public async Task Historical_assignment_stays_denied_until_declared_key_is_independently_activated()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            const string key = "Legacy.Future.Read";
            int userId;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.OpenAsync();
                userId = await connection.QuerySingleAsync<int>(
                    "INSERT dbo.Users " +
                    "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm) " +
                    "OUTPUT INSERTED.UserId VALUES " +
                    "(N'legacy-user', N'LEGACY-USER', N'Legacy User', N'Local', N'hash', N'', N'BCrypt');");
                var roleId = await connection.QuerySingleAsync<int>(
                    "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
                    "OUTPUT INSERTED.RoleId VALUES (N'Legacy role', N'LEGACY ROLE');");
                await connection.ExecuteAsync(
                    "INSERT dbo.UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId); " +
                    "INSERT dbo.RolePermissions (RoleId, PermissionKey) VALUES (@RoleId, @Key); " +
                    "INSERT dbo.Permissions " +
                    "(PermissionKey, Module, Description, CatalogStatus, AdministratorGrantPolicy) " +
                    "VALUES (@Key, N'Legacy', N'Historical assignment.', 'Unregistered', 'NotEligible'); " +
                    "INSERT dbo.PermissionLegacyAssignmentDrift " +
                    "(PermissionKey, RoleGrantCount, UserGrantCount, ReviewStatus) " +
                    "VALUES (@Key, 1, 0, 'Unreviewed');",
                    new { UserId = userId, RoleId = roleId, Key = key });
            }

            var definitions = PermissionRegistryComposition.CreateDefault().Definitions
                .Concat(new[]
                {
                    new PermissionDefinition(
                        key,
                        "Legacy",
                        "Adopted future permission.",
                        PermissionLifecycleStatus.Active,
                        AdministratorGrantPolicy.NotEligible),
                });
            var pendingRegistry = PermissionRegistry.Create("legacy-adoption-v1", definitions);
            var pendingApprovals = ApprovalForRegistry(
                pendingRegistry,
                key,
                PermissionCatalogDecision.Pending);
            var engine = new PermissionSynchronizationEngine();
            var pendingPlan = await engine.CreatePlanAsync(
                database.ConnectionString,
                pendingRegistry,
                pendingApprovals,
                UtcNow,
                CancellationToken.None);
            await engine.ApplyAsync(
                database.ConnectionString,
                pendingRegistry,
                pendingApprovals,
                pendingPlan,
                UtcNow,
                CancellationToken.None);

            var services = new ServiceCollection();
            services.AddSqlPersistence(database.ConnectionString);
            using var provider = services.BuildServiceProvider();
            var authorization = new PermissionAuthorizationService(
                new AnonymousCurrentUserAccessor(),
                provider.GetRequiredService<IPermissionStore>(),
                pendingRegistry);
            Assert.False(await authorization.IsAllowedAsync(userId, key, CancellationToken.None));

            var activeRegistry = PermissionRegistry.Create("legacy-adoption-v2", definitions);
            var activeApprovals = ApprovalForRegistry(
                activeRegistry,
                key,
                PermissionCatalogDecision.Approved);
            var activePlan = await engine.CreatePlanAsync(
                database.ConnectionString,
                activeRegistry,
                activeApprovals,
                UtcNow.AddMinutes(1),
                CancellationToken.None);
            await engine.ApplyAsync(
                database.ConnectionString,
                activeRegistry,
                activeApprovals,
                activePlan,
                UtcNow.AddMinutes(1),
                CancellationToken.None);

            var activeAuthorization = new PermissionAuthorizationService(
                new AnonymousCurrentUserAccessor(),
                provider.GetRequiredService<IPermissionStore>(),
                activeRegistry);
            Assert.True(await activeAuthorization.IsAllowedAsync(userId, key, CancellationToken.None));
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal("Adopted", await verification.QuerySingleAsync<string>(
                "SELECT ReviewStatus FROM dbo.PermissionLegacyAssignmentDrift WHERE PermissionKey = @Key;",
                new { Key = key }));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Competing_apply_attempts_commit_once_without_duplicate_catalog_or_role_rows()
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

            async Task<string> ApplyAsync()
            {
                try
                {
                    await engine.ApplyAsync(
                        database.ConnectionString,
                        registry,
                        approvals,
                        plan,
                        UtcNow,
                        CancellationToken.None);
                    return "Succeeded";
                }
                catch (PermissionSynchronizationException exception)
                {
                    return exception.Code;
                }
            }

            var outcomes = await Task.WhenAll(ApplyAsync(), ApplyAsync());
            Assert.Equal(1, outcomes.Count(outcome => outcome == "Succeeded"));
            Assert.Contains(outcomes, outcome =>
                outcome == "PermissionSync.Busy" || outcome == "PermissionSync.StalePlan");

            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(registry.Definitions.Count, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.Permissions WHERE Module <> N'Legacy';"));
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SystemManagedRoles WHERE SystemRoleKey = 'Administrator';"));
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.PermissionSecurityAudit " +
                "WHERE Operation = 'PermissionSync.Apply' AND Outcome = 'Succeeded';"));
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

    private static PermissionSyncApprovalSet ApprovalForRegistry(
        IPermissionRegistry registry,
        string specialKey,
        PermissionCatalogDecision specialDecision)
    {
        return new PermissionSyncApprovalSet(
            registry.ReleaseId,
            "release-operator",
            UtcNow.AddHours(4),
            registry.Definitions.Select(definition => new PermissionSyncApproval(
                definition.Key,
                definition.Key == specialKey
                    ? specialDecision
                    : PermissionCatalogDecision.Approved,
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

    private sealed class AnonymousCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser Current => CurrentUser.Anonymous;
    }
}
