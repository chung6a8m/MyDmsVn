using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SecurityAdministrationSqlTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Administration_is_revisioned_audited_and_preserves_direct_decision_precedence()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            int actorId;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                actorId = await CreateManagerAsync(connection);
                await connection.ExecuteAsync(
                    "INSERT dbo.UserPermissions (UserId, PermissionKey, Granted) " +
                    "VALUES (@actorId, N'Security.Users.Manage', 1);",
                    new { actorId });
            }

            using var provider = CreateProvider(database.ConnectionString, actorId);
            var sender = provider.GetRequiredService<ISender>();
            var roleResult = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.Role,
                SecurityAdministrationOperation.Create,
                name: "Warehouse",
                isActive: true));
            Assert.False(roleResult.IsError);
            Assert.Equal(0, roleResult.Value.AssignmentRevision);

            var roleId = await QueryRoleIdAsync(database.ConnectionString, "WAREHOUSE");
            var userResult = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.Create,
                name: "operator",
                displayName: "Operator",
                email: "operator@example.invalid",
                password: "T3-Safe-Local-Password-47"));
            Assert.False(userResult.IsError);
            Assert.Equal(0, userResult.Value.AssignmentRevision);
            var userId = await QueryUserIdAsync(database.ConnectionString, "OPERATOR");

            var roleGrant = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.Role,
                SecurityAdministrationOperation.SetPermission,
                targetId: roleId,
                permissionKey: PermissionKeys.CatalogProductsRead,
                decision: true,
                expectedRevision: 0));
            Assert.False(roleGrant.IsError);
            Assert.Equal(1, roleGrant.Value.AssignmentRevision);

            var duplicateRoleGrant = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.Role,
                SecurityAdministrationOperation.SetPermission,
                targetId: roleId,
                permissionKey: PermissionKeys.CatalogProductsRead,
                decision: true,
                expectedRevision: 1));
            Assert.False(duplicateRoleGrant.IsError);
            Assert.Equal(1, duplicateRoleGrant.Value.AssignmentRevision);

            var membership = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetMembership,
                targetId: userId,
                relatedId: roleId,
                isActive: true,
                expectedRevision: 0,
                expectedRelatedRevision: 1));
            Assert.False(membership.IsError);
            Assert.Equal(1, membership.Value.AssignmentRevision);
            Assert.Equal(2, membership.Value.RelatedAssignmentRevision);

            var duplicateMembership = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetMembership,
                targetId: userId,
                relatedId: roleId,
                isActive: true,
                expectedRevision: 1,
                expectedRelatedRevision: 2));
            Assert.False(duplicateMembership.IsError);
            Assert.Equal(1, duplicateMembership.Value.AssignmentRevision);
            Assert.Equal(2, duplicateMembership.Value.RelatedAssignmentRevision);

            var directDeny = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetPermission,
                targetId: userId,
                permissionKey: PermissionKeys.CatalogProductsRead,
                decision: false,
                expectedRevision: 1));
            Assert.False(directDeny.IsError);
            Assert.Equal(2, directDeny.Value.AssignmentRevision);
            var permissionStore = provider.GetRequiredService<IPermissionStore>();
            var deniedSnapshot = await permissionStore.GetSnapshotAsync(
                userId,
                PermissionKeys.CatalogProductsRead,
                CancellationToken.None);
            Assert.False(deniedSnapshot.DirectDecision);
            Assert.True(deniedSnapshot.HasRoleGrant);

            var inherit = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetPermission,
                targetId: userId,
                permissionKey: PermissionKeys.CatalogProductsRead,
                decision: null,
                expectedRevision: 2));
            Assert.False(inherit.IsError);
            Assert.Equal(3, inherit.Value.AssignmentRevision);
            var inheritedSnapshot = await permissionStore.GetSnapshotAsync(
                userId,
                PermissionKeys.CatalogProductsRead,
                CancellationToken.None);
            Assert.Null(inheritedSnapshot.DirectDecision);
            Assert.True(inheritedSnapshot.HasRoleGrant);

            var stale = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetActive,
                targetId: userId,
                isActive: false,
                expectedRevision: 2));
            Assert.True(stale.IsError);
            Assert.Equal("Security.RevisionConflict", stale.FirstError.Code);

            var listedUsers = await sender.Send(new ListSecurityUsersQuery());
            var listedRoles = await sender.Send(new ListSecurityRolesQuery());
            Assert.Contains(listedUsers.Value, user => user.UserId == userId);
            Assert.DoesNotContain(listedUsers.Value, user => user.IsProtectedAccount);
            Assert.Contains(listedRoles.Value, role => role.RoleId == roleId);

            using var verification = new SqlConnection(database.ConnectionString);
            var userHash = await verification.QuerySingleAsync<string>(
                "SELECT PasswordHash FROM dbo.Users WHERE UserId = @userId;",
                new { userId });
            var auditStates = (await verification.QueryAsync<(string Operation, string? BeforeState, string? AfterState)>(
                "SELECT Operation, BeforeState, AfterState " +
                "FROM dbo.SecurityAuditEvents ORDER BY AuditEventId;")).ToArray();
            Assert.Equal(6, auditStates.Length);
            Assert.All(auditStates, state =>
            {
                var payload = (state.BeforeState ?? string.Empty) + "|" + (state.AfterState ?? string.Empty);
                Assert.DoesNotContain("T3-Safe-Local-Password-47", payload, StringComparison.Ordinal);
                Assert.DoesNotContain(userHash, payload, StringComparison.Ordinal);
            });
            var membershipAudit = Assert.Single(auditStates, state => state.Operation == "SetMembership");
            Assert.Contains("Member=False", membershipAudit.BeforeState, StringComparison.Ordinal);
            Assert.Contains("Member=True", membershipAudit.AfterState, StringComparison.Ordinal);
            var permissionAudits = auditStates.Where(state => state.Operation == "SetPermission").ToArray();
            Assert.Equal(3, permissionAudits.Length);
            Assert.Contains("Decision=Inherit", permissionAudits[0].BeforeState, StringComparison.Ordinal);
            Assert.Contains("Decision=Allow", permissionAudits[0].AfterState, StringComparison.Ordinal);
            Assert.Contains("Decision=Inherit", permissionAudits[1].BeforeState, StringComparison.Ordinal);
            Assert.Contains("Decision=Deny", permissionAudits[1].AfterState, StringComparison.Ordinal);
            Assert.Contains("Decision=Deny", permissionAudits[2].BeforeState, StringComparison.Ordinal);
            Assert.Contains("Decision=Inherit", permissionAudits[2].AfterState, StringComparison.Ordinal);
            Assert.Equal(3, await verification.QuerySingleAsync<long>(
                "SELECT AssignmentRevision FROM dbo.Users WHERE UserId = @userId;",
                new { userId }));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Missing_targets_and_bound_superuser_lifecycle_changes_are_rejected()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            int actorId;
            int boundUserId;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                actorId = await CreateManagerAsync(connection);
                boundUserId = await InsertUserAsync(connection, "root.admin", "ROOT.ADMIN");
                await connection.ExecuteAsync(
                    "UPDATE dbo.Users SET IsActive = 0 WHERE UserId = @boundUserId; " +
                    "INSERT dbo.SystemSecuritySettings (Id, SuperuserUserId, BoundNormalizedUsername) " +
                    "VALUES (1, @boundUserId, N'ROOT.ADMIN');",
                    new { boundUserId });
            }

            using var provider = CreateProvider(database.ConnectionString, actorId);
            var sender = provider.GetRequiredService<ISender>();
            var rename = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.Update,
                targetId: boundUserId,
                name: "replacement",
                displayName: "Replacement",
                expectedRevision: 0));
            var reactivate = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetActive,
                targetId: boundUserId,
                isActive: true,
                expectedRevision: 0));
            var resetPassword = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetPassword,
                targetId: boundUserId,
                password: "T3-Safe-Reset-Password-49",
                expectedRevision: 0));
            var missing = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetActive,
                targetId: int.MaxValue,
                isActive: true,
                expectedRevision: 0));

            Assert.True(rename.IsError);
            Assert.Equal("Security.ReservedSuperuser", rename.FirstError.Code);
            Assert.True(reactivate.IsError);
            Assert.Equal("Security.ReservedSuperuser", reactivate.FirstError.Code);
            Assert.True(resetPassword.IsError);
            Assert.Equal("Security.ReservedSuperuser", resetPassword.FirstError.Code);
            Assert.True(missing.IsError);
            Assert.Equal("Security.TargetNotFound", missing.FirstError.Code);
            var listed = await sender.Send(new ListSecurityUsersQuery());
            Assert.Contains(listed.Value, user => user.UserId == boundUserId && user.IsProtectedAccount);

            using var verification = new SqlConnection(database.ConnectionString);
            var bound = await verification.QuerySingleAsync<(string NormalizedUsername, bool IsActive, long Revision)>(
                "SELECT NormalizedUsername, IsActive, AssignmentRevision AS Revision " +
                "FROM dbo.Users WHERE UserId = @boundUserId;",
                new { boundUserId });
            Assert.Equal("ROOT.ADMIN", bound.NormalizedUsername);
            Assert.False(bound.IsActive);
            Assert.Equal(0, bound.Revision);
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SecurityAuditEvents;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Audit_failure_rolls_back_the_security_mutation_and_revision()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            int actorId;
            int userId;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                actorId = await CreateManagerAsync(connection);
                userId = await InsertUserAsync(connection, "operator", "OPERATOR");
                await connection.ExecuteAsync(
                    "CREATE TRIGGER dbo.TR_P6_RejectSecurityAudit ON dbo.SecurityAuditEvents " +
                    "AFTER INSERT AS BEGIN SET NOCOUNT ON; " +
                    "THROW 51100, 'Injected audit failure.', 1; END;");
            }

            using var provider = CreateProvider(database.ConnectionString, actorId);
            var result = await provider.GetRequiredService<ISender>().Send(
                new MutateSecurityAdministrationCommand(
                    SecurityAdministrationTarget.User,
                    SecurityAdministrationOperation.SetActive,
                    targetId: userId,
                    isActive: false,
                    expectedRevision: 0));

            Assert.True(result.IsError);
            using var verification = new SqlConnection(database.ConnectionString);
            var user = await verification.QuerySingleAsync<(bool IsActive, long Revision)>(
                "SELECT IsActive, AssignmentRevision AS Revision FROM dbo.Users WHERE UserId = @userId;",
                new { userId });
            Assert.True(user.IsActive);
            Assert.Equal(0, user.Revision);
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SecurityAuditEvents;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning, SqlTestCategory.SqlConcurrency)]
    public async Task Conflicting_security_writes_serialize_and_only_one_revision_commits()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            int actorId;
            int userId;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                actorId = await CreateManagerAsync(connection);
                userId = await InsertUserAsync(connection, "operator", "OPERATOR");
            }

            using var provider = CreateProvider(database.ConnectionString, actorId);
            var sender = provider.GetRequiredService<ISender>();
            var first = sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetPermission,
                targetId: userId,
                permissionKey: PermissionKeys.CatalogProductsRead,
                decision: false,
                expectedRevision: 0));
            var second = sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetPermission,
                targetId: userId,
                permissionKey: PermissionKeys.CatalogProductsWrite,
                decision: true,
                expectedRevision: 0));

            var results = await Task.WhenAll(first, second);

            Assert.Single(results, result => !result.IsError);
            Assert.Single(results, result => result.IsError &&
                result.FirstError.Code == "Security.RevisionConflict");
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await verification.QuerySingleAsync<long>(
                "SELECT AssignmentRevision FROM dbo.Users WHERE UserId = @userId;",
                new { userId }));
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SecurityAuditEvents WHERE TargetType = N'User' AND TargetId = @userId;",
                new { userId }));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static ServiceProvider CreateProvider(string connectionString, int actorId)
    {
        var services = new ServiceCollection();
        services.AddServerApplication("ROOT.ADMIN");
        services.AddSingleton<ICurrentUserAccessor>(new StubCurrentUserAccessor(actorId));
        services.AddSqlPersistence(connectionString);
        return services.BuildServiceProvider();
    }

    private static async Task<int> CreateManagerAsync(SqlConnection connection)
    {
        var actorId = await InsertUserAsync(connection, "manager", "MANAGER");
        var roleId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
            "OUTPUT INSERTED.RoleId VALUES (N'Security managers', N'SECURITY MANAGERS');");
        await connection.ExecuteAsync(
            "INSERT dbo.UserRoles (UserId, RoleId) VALUES (@actorId, @roleId); " +
            "INSERT dbo.RolePermissions (RoleId, PermissionKey) VALUES " +
            "(@roleId, N'Security.Users.Manage'), (@roleId, N'Security.Roles.Manage');",
            new { actorId, roleId });
        return actorId;
    }

    private static Task<int> InsertUserAsync(
        SqlConnection connection,
        string username,
        string normalizedUsername) =>
        connection.QuerySingleAsync<int>(
            "INSERT dbo.Users " +
            "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm) " +
            "OUTPUT INSERTED.UserId VALUES (@username, @normalizedUsername, N'Operator', N'Local', N'hash', N'', N'BCrypt');",
            new { username, normalizedUsername });

    private static async Task<int> QueryUserIdAsync(string connectionString, string normalizedUsername)
    {
        using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleAsync<int>(
            "SELECT UserId FROM dbo.Users WHERE NormalizedUsername = @normalizedUsername;",
            new { normalizedUsername }).ConfigureAwait(false);
    }

    private static async Task<int> QueryRoleIdAsync(string connectionString, string normalizedName)
    {
        using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleAsync<int>(
            "SELECT RoleId FROM dbo.Roles WHERE NormalizedRoleName = @normalizedName;",
            new { normalizedName }).ConfigureAwait(false);
    }

    private static async Task<SqlTestDatabase> CreateMigratedDatabaseAsync(
        [CallerMemberName] string testCaseId = "")
    {
        var measurement = SqlTestDatabase.CreateMeasurementContextForTest(testCaseId);
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!,
            measurement);
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            return database;
        }
        catch
        {
            try
            {
                await database.DisposeAsync();
            }
            catch
            {
                // Preserve the migration failure; the ownership journal records any failed cleanup.
            }

            throw;
        }
    }

    private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
    {
        public StubCurrentUserAccessor(int actorId) =>
            Current = CurrentUser.Authenticated(actorId, "manager", "Manager", true);

        public CurrentUser Current { get; }
    }
}
