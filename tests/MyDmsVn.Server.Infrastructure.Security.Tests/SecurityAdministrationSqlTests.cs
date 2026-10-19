using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[Collection(ApplicationSqlCollectionFixture.CollectionName)]
public sealed class SecurityAdministrationSqlTests
{
    private readonly ApplicationSqlCollectionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SecurityAdministrationSqlTests(ApplicationSqlCollectionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Permission_import_previews_without_writes_then_adds_user_and_role_decisions_atomically() =>
        _fixture.RunAsync(nameof(Permission_import_previews_without_writes_then_adds_user_and_role_decisions_atomically), _output, async test =>
        {
            int actorId;
            int userId;
            int roleId;
            {
                var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                actorId = await CreateManagerAsync(connection);
                userId = await InsertUserAsync(connection, "import.target", "IMPORT.TARGET");
                roleId = await connection.QuerySingleAsync<int>(
                    "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
                    "OUTPUT INSERTED.RoleId VALUES (N'Import target role', N'IMPORT TARGET ROLE');");
            }

            var provider = ApplicationSqlCollectionFixture.Track(test, CreateProvider(test.ConnectionString, actorId));
            var sender = provider.GetRequiredService<ISender>();
            var roleJson = ImportJson("Role", roleId,
                "Catalog.Products.Read", "Allow", "Catalog.Products.Write", "Allow");
            var preview = await sender.Send(new PreviewPermissionAssignmentImportQuery(
                PermissionImportTargetTypeDto.Role,
                roleId,
                roleJson));
            Assert.False(preview.IsError);
            Assert.Equal(2, preview.Value.Additions.Count);
            Assert.Equal(0, preview.Value.TargetRevision);
            {
                var verification = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                Assert.Equal(0, await verification.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM dbo.RolePermissions WHERE RoleId = @roleId;", new { roleId }));
                Assert.Equal(0, await verification.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM dbo.SecurityAuditEvents WHERE TargetId = @roleId;", new { roleId }));
            }

            var roleApply = await sender.Send(new ApplyPermissionAssignmentImportCommand(
                PermissionImportTargetTypeDto.Role,
                roleId,
                roleJson,
                preview.Value.TargetRevision,
                preview.Value.CatalogDigest));
            Assert.False(roleApply.IsError);
            Assert.Equal(2, roleApply.Value.AddedCount);
            Assert.Equal(1, roleApply.Value.TargetRevision);
            Assert.True(roleApply.Value.Changed);

            var repeated = await sender.Send(new ApplyPermissionAssignmentImportCommand(
                PermissionImportTargetTypeDto.Role,
                roleId,
                roleJson,
                roleApply.Value.TargetRevision,
                preview.Value.CatalogDigest));
            Assert.False(repeated.IsError);
            Assert.Equal(0, repeated.Value.AddedCount);
            Assert.False(repeated.Value.Changed);
            Assert.Equal(1, repeated.Value.TargetRevision);

            var userJson = ImportJson("User", userId,
                "Catalog.Products.Read", "Deny");
            var userPreview = await sender.Send(new PreviewPermissionAssignmentImportQuery(
                PermissionImportTargetTypeDto.User,
                userId,
                userJson));
            Assert.False(userPreview.IsError);
            var userApply = await sender.Send(new ApplyPermissionAssignmentImportCommand(
                PermissionImportTargetTypeDto.User,
                userId,
                userJson,
                userPreview.Value.TargetRevision,
                userPreview.Value.CatalogDigest));
            Assert.False(userApply.IsError);
            Assert.Equal(1, userApply.Value.AddedCount);

            var finalCheck = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
            Assert.Equal(2, await finalCheck.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.RolePermissions WHERE RoleId = @roleId;", new { roleId }));
            Assert.False(await finalCheck.QuerySingleAsync<bool>(
                "SELECT Granted FROM dbo.UserPermissions WHERE UserId = @userId " +
                "AND PermissionKey = N'Catalog.Products.Read';", new { userId }));
            Assert.Equal(2, await finalCheck.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SecurityAuditEvents WHERE TargetType = N'Role' AND TargetId = @roleId;",
                new { roleId }));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Permission_import_rejects_stale_catalog_revision_conflicts_and_audit_failure()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            int actorId;
            int userId;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                actorId = await CreateManagerAsync(connection);
                userId = await InsertUserAsync(connection, "import.target", "IMPORT.TARGET");
                await connection.ExecuteAsync(
                    "INSERT dbo.UserPermissions (UserId, PermissionKey, Granted) " +
                    "VALUES (@userId, N'Catalog.Products.Read', 0);",
                    new { userId });
            }

            using var provider = CreateProvider(database.ConnectionString, actorId);
            var sender = provider.GetRequiredService<ISender>();
            var conflictingJson = ImportJson("User", userId, "Catalog.Products.Read", "Allow");
            var previewConflict = await sender.Send(new PreviewPermissionAssignmentImportQuery(
                PermissionImportTargetTypeDto.User, userId, conflictingJson));
            Assert.True(previewConflict.IsError);
            Assert.Equal("Security.ImportDecisionConflict", previewConflict.FirstError.Code);

            var malformed = await sender.Send(new PreviewPermissionAssignmentImportQuery(
                PermissionImportTargetTypeDto.User,
                userId,
                "{\"schemaVersion\":1,\"targetType\":\"User\",\"targetId\":" + userId +
                ",\"entries\":[{\"permissionKey\":\"Catalog.Products.Write\"}]}"));
            Assert.True(malformed.IsError);

            var validJson = ImportJson("User", userId, "Catalog.Products.Write", "Allow");
            var preview = await sender.Send(new PreviewPermissionAssignmentImportQuery(
                PermissionImportTargetTypeDto.User, userId, validJson));
            Assert.False(preview.IsError);

            var changedDigest = await sender.Send(new ApplyPermissionAssignmentImportCommand(
                PermissionImportTargetTypeDto.User, userId, validJson, 0, new string('0', 64)));
            Assert.True(changedDigest.IsError);
            Assert.Equal("Security.ImportCatalogChanged", changedDigest.FirstError.Code);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(
                    "CREATE TRIGGER dbo.TR_TestRejectPermissionImportAudit ON dbo.SecurityAuditEvents " +
                    "AFTER INSERT AS BEGIN SET NOCOUNT ON; " +
                    "THROW 51101, 'Injected import audit failure.', 1; END;");
            }

            var auditFailure = await sender.Send(new ApplyPermissionAssignmentImportCommand(
                PermissionImportTargetTypeDto.User,
                userId,
                validJson,
                preview.Value.TargetRevision,
                preview.Value.CatalogDigest));
            Assert.True(auditFailure.IsError);

            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.UserPermissions WHERE UserId = @userId " +
                "AND PermissionKey = N'Catalog.Products.Write';", new { userId }));
            Assert.Equal(0, await verification.QuerySingleAsync<long>(
                "SELECT AssignmentRevision FROM dbo.Users WHERE UserId = @userId;", new { userId }));
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SecurityAuditEvents;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Permission_import_rechecks_actor_permission_after_preview() =>
        _fixture.RunAsync(nameof(Permission_import_rechecks_actor_permission_after_preview), _output, async test =>
        {
            int actorId;
            int targetId;
            int managerRoleId;
            {
                var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                actorId = await CreateManagerAsync(connection);
                targetId = await InsertUserAsync(connection, "import.target", "IMPORT.TARGET");
                managerRoleId = await connection.QuerySingleAsync<int>(
                    "SELECT RoleId FROM dbo.UserRoles WHERE UserId = @actorId;", new { actorId });
            }

            var provider = ApplicationSqlCollectionFixture.Track(test, CreateProvider(test.ConnectionString, actorId));
            var sender = provider.GetRequiredService<ISender>();
            var json = ImportJson("User", targetId, "Catalog.Products.Write", "Allow");
            var preview = await sender.Send(new PreviewPermissionAssignmentImportQuery(
                PermissionImportTargetTypeDto.User, targetId, json));
            Assert.False(preview.IsError);

            {
                var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                await connection.ExecuteAsync(
                    "DELETE dbo.RolePermissions WHERE RoleId = @managerRoleId " +
                    "AND PermissionKey = N'Security.Users.Manage';",
                    new { managerRoleId });
            }

            var apply = await sender.Send(new ApplyPermissionAssignmentImportCommand(
                PermissionImportTargetTypeDto.User, targetId, json,
                preview.Value.TargetRevision, preview.Value.CatalogDigest));
            Assert.True(apply.IsError);
            Assert.Equal("Auth.Forbidden", apply.FirstError.Code);
            var verification = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.UserPermissions WHERE UserId = @targetId " +
                "AND PermissionKey = N'Catalog.Products.Write';", new { targetId }));
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SecurityAuditEvents WHERE TargetId = @targetId;", new { targetId }));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Permission_import_rejects_inactive_targets_and_stale_preview_revisions() =>
        _fixture.RunAsync(nameof(Permission_import_rejects_inactive_targets_and_stale_preview_revisions), _output, async test =>
        {
            int actorId;
            int targetId;
            {
                var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                actorId = await CreateManagerAsync(connection);
                targetId = await InsertUserAsync(connection, "import.target", "IMPORT.TARGET");
            }

            var provider = ApplicationSqlCollectionFixture.Track(test, CreateProvider(test.ConnectionString, actorId));
            var sender = provider.GetRequiredService<ISender>();
            var json = ImportJson("User", targetId, "Catalog.Products.Write", "Allow");
            var preview = await sender.Send(new PreviewPermissionAssignmentImportQuery(
                PermissionImportTargetTypeDto.User, targetId, json));
            Assert.False(preview.IsError);

            {
                var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                await connection.ExecuteAsync(
                    "UPDATE dbo.Users SET IsActive = 0 WHERE UserId = @targetId;", new { targetId });
            }

            var inactive = await sender.Send(new ApplyPermissionAssignmentImportCommand(
                PermissionImportTargetTypeDto.User, targetId, json,
                preview.Value.TargetRevision, preview.Value.CatalogDigest));
            Assert.True(inactive.IsError);
            Assert.Equal("Security.ImportTargetUnavailable", inactive.FirstError.Code);

            {
                var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                await connection.ExecuteAsync(
                    "UPDATE dbo.Users SET IsActive = 1, AssignmentRevision = AssignmentRevision + 1 " +
                    "WHERE UserId = @targetId;", new { targetId });
            }

            var currentPreview = await sender.Send(new PreviewPermissionAssignmentImportQuery(
                PermissionImportTargetTypeDto.User, targetId, json));
            Assert.False(currentPreview.IsError);
            {
                var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                await connection.ExecuteAsync(
                    "UPDATE dbo.Users SET AssignmentRevision = AssignmentRevision + 1 WHERE UserId = @targetId;",
                    new { targetId });
            }

            var stale = await sender.Send(new ApplyPermissionAssignmentImportCommand(
                PermissionImportTargetTypeDto.User, targetId, json,
                currentPreview.Value.TargetRevision, currentPreview.Value.CatalogDigest));
            Assert.True(stale.IsError);
            Assert.Equal("Security.RevisionConflict", stale.FirstError.Code);
            var verification = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.UserPermissions WHERE UserId = @targetId " +
                "AND PermissionKey = N'Catalog.Products.Write';", new { targetId }));
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SecurityAuditEvents WHERE TargetId = @targetId;", new { targetId }));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning, SqlTestCategory.SqlConcurrency)]
    public async Task Concurrent_permission_imports_on_one_preview_commit_at_most_once()
    {
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            int actorId;
            int roleId;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                actorId = await CreateManagerAsync(connection);
                roleId = await connection.QuerySingleAsync<int>(
                    "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
                    "OUTPUT INSERTED.RoleId VALUES (N'Concurrent import role', N'CONCURRENT IMPORT ROLE');");
            }

            using var provider = CreateProvider(database.ConnectionString, actorId);
            var sender = provider.GetRequiredService<ISender>();
            var firstJson = ImportJson("Role", roleId, "Catalog.Products.Read", "Allow");
            var secondJson = ImportJson("Role", roleId, "Catalog.Products.Write", "Allow");
            var preview = await sender.Send(new PreviewPermissionAssignmentImportQuery(
                PermissionImportTargetTypeDto.Role, roleId, firstJson));
            Assert.False(preview.IsError);

            var first = sender.Send(new ApplyPermissionAssignmentImportCommand(
                PermissionImportTargetTypeDto.Role, roleId, firstJson, 0, preview.Value.CatalogDigest));
            var second = sender.Send(new ApplyPermissionAssignmentImportCommand(
                PermissionImportTargetTypeDto.Role, roleId, secondJson, 0, preview.Value.CatalogDigest));
            var results = await Task.WhenAll(first, second);

            Assert.Single(results, result => !result.IsError);
            Assert.Single(results, result => result.IsError && result.FirstError.Code == "Security.RevisionConflict");
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await verification.QuerySingleAsync<long>(
                "SELECT AssignmentRevision FROM dbo.Roles WHERE RoleId = @roleId;", new { roleId }));
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.RolePermissions WHERE RoleId = @roleId;", new { roleId }));
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SecurityAuditEvents WHERE TargetType = N'Role' AND TargetId = @roleId;",
                new { roleId }));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Administration_is_revisioned_audited_and_preserves_direct_decision_precedence() =>
        _fixture.RunAsync(nameof(Administration_is_revisioned_audited_and_preserves_direct_decision_precedence), _output, async test =>
        {
            int actorId;
            {
                var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                actorId = await CreateManagerAsync(connection);
                await connection.ExecuteAsync(
                    "INSERT dbo.UserPermissions (UserId, PermissionKey, Granted) " +
                    "VALUES (@actorId, N'Security.Users.Manage', 1);",
                    new { actorId });
            }

            var provider = ApplicationSqlCollectionFixture.Track(test, CreateProvider(test.ConnectionString, actorId));
            var sender = provider.GetRequiredService<ISender>();
            var roleResult = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.Role,
                SecurityAdministrationOperation.Create,
                name: "Warehouse",
                isActive: true));
            Assert.False(roleResult.IsError);
            Assert.Equal(0, roleResult.Value.AssignmentRevision);

            var roleId = await QueryRoleIdAsync(test.ConnectionString, "WAREHOUSE");
            var userResult = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.Create,
                name: "operator",
                displayName: "Operator",
                email: "operator@example.invalid",
                password: "T3-Safe-Local-Password-47"));
            Assert.False(userResult.IsError);
            Assert.Equal(0, userResult.Value.AssignmentRevision);
            var userId = await QueryUserIdAsync(test.ConnectionString, "OPERATOR");

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

            var verification = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
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
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Missing_targets_and_bound_superuser_lifecycle_changes_are_rejected() =>
        _fixture.RunAsync(nameof(Missing_targets_and_bound_superuser_lifecycle_changes_are_rejected), _output, async test =>
        {
            int actorId;
            int boundUserId;
            {
                var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                actorId = await CreateManagerAsync(connection);
                boundUserId = await InsertUserAsync(connection, "root.admin", "ROOT.ADMIN");
                await connection.ExecuteAsync(
                    "UPDATE dbo.Users SET IsActive = 0 WHERE UserId = @boundUserId; " +
                    "INSERT dbo.SystemSecuritySettings (Id, SuperuserUserId, BoundNormalizedUsername) " +
                    "VALUES (1, @boundUserId, N'ROOT.ADMIN');",
                    new { boundUserId });
            }

            var provider = ApplicationSqlCollectionFixture.Track(test, CreateProvider(test.ConnectionString, actorId));
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

            var directProcedure = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
            await Assert.ThrowsAsync<SqlException>(() => directProcedure.ExecuteAsync(
                "EXEC dbo.ApplySecurityAdministration " +
                "@Target = N'User', @Operation = N'Update', @ActorUserId = @actorId, " +
                "@TargetId = @boundUserId, @Name = N'root.admin', @NormalizedName = N'ROOT.ADMIN', " +
                "@DisplayName = N'Changed protected display name', @Email = N'changed@example.test', " +
                "@ExpectedRevision = 0, @CorrelationId = @correlationId;",
                new { actorId, boundUserId, correlationId = Guid.NewGuid() }));

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

            var verification = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
            var bound = await verification.QuerySingleAsync<(
                string Username, string NormalizedUsername, string DisplayName, string? Email, bool IsActive, long Revision)>(
                "SELECT Username, NormalizedUsername, DisplayName, Email, IsActive, " +
                "AssignmentRevision AS Revision " +
                "FROM dbo.Users WHERE UserId = @boundUserId;",
                new { boundUserId });
            Assert.Equal("root.admin", bound.Username);
            Assert.Equal("ROOT.ADMIN", bound.NormalizedUsername);
            Assert.Equal("Operator", bound.DisplayName);
            Assert.Null(bound.Email);
            Assert.False(bound.IsActive);
            Assert.Equal(0, bound.Revision);
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SecurityAuditEvents;"));
        });

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

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Catalog_only_release_adds_a_registered_key_without_rewriting_assignments()
    {
        const string newKey = "Catalog.ReleaseScenario.Access";
        var database = await CreateMigratedDatabaseAsync();
        try
        {
            int superuserId;
            int ordinaryUserId;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                superuserId = await InsertUserAsync(connection, "root.admin", "ROOT.ADMIN");
                ordinaryUserId = await InsertUserAsync(connection, "ordinary", "ORDINARY");
                await connection.ExecuteAsync(
                    "INSERT dbo.SystemSecuritySettings (Id, SuperuserUserId, BoundNormalizedUsername) " +
                    "VALUES (1, @superuserId, N'ROOT.ADMIN');",
                    new { superuserId });
            }

            var registry = PermissionRegistry.Create(
                PermissionRegistryComposition.CreateDefault().Definitions.Concat(new[]
                {
                    new PermissionDefinition(newKey, "Catalog", "Release scenario permission."),
                }));
            var assignmentsBefore = await ReadAssignmentSnapshotAsync(database.ConnectionString);
            using var superuserProvider = CreateProvider(database.ConnectionString, superuserId, registry);
            var sender = superuserProvider.GetRequiredService<ISender>();
            var catalog = await sender.Send(new ListPermissionCatalogQuery());
            Assert.False(catalog.IsError);
            Assert.Contains(catalog.Value.Items, item => item.Key == newKey && item.Status == "Active");

            using var exported = new StringWriter();
            using var errors = new StringWriter();
            var exitCode = PermissionCatalogCommand.Run(
                new[] { "list", "--format", "json" }, exported, errors, registry);
            Assert.Equal(0, exitCode);
            Assert.Contains(newKey, exported.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, errors.ToString());

            var superuserAuthorization = superuserProvider
                .GetRequiredService<IPermissionAuthorizationService>();
            Assert.True(await superuserAuthorization.IsAllowedAsync(newKey, CancellationToken.None));

            using var ordinaryProvider = CreateProvider(database.ConnectionString, ordinaryUserId, registry);
            var ordinaryAuthorization = ordinaryProvider
                .GetRequiredService<IPermissionAuthorizationService>();
            Assert.False(await ordinaryAuthorization.IsAllowedAsync(newKey, CancellationToken.None));

            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());
            Assert.Equal(assignmentsBefore, await ReadAssignmentSnapshotAsync(database.ConnectionString));

            var grant = await sender.Send(new MutateSecurityAdministrationCommand(
                SecurityAdministrationTarget.User,
                SecurityAdministrationOperation.SetPermission,
                targetId: ordinaryUserId,
                permissionKey: newKey,
                decision: true,
                expectedRevision: 0));
            Assert.False(grant.IsError);
            Assert.Equal(1, grant.Value.AssignmentRevision);
            Assert.True(await ordinaryAuthorization.IsAllowedAsync(newKey, CancellationToken.None));
            var assignmentsAfterGrant = await ReadAssignmentSnapshotAsync(database.ConnectionString);
            Assert.NotEqual(assignmentsBefore, assignmentsAfterGrant);
            Assert.Single(assignmentsAfterGrant,
                assignment => assignment == "U|" + ordinaryUserId + "|" + newKey + "|1");
        }
        finally
        {
            await database.DisposeAsync();
        }
    }
    private static ServiceProvider CreateProvider(string connectionString, int actorId, IPermissionRegistry? registry = null)
    {
        var services = new ServiceCollection();
        if (registry != null)
        {
            services.AddSingleton(registry);
        }
        services.AddServerApplication("ROOT.ADMIN");
        services.AddSingleton<ICurrentUserAccessor>(new StubCurrentUserAccessor(actorId));
        services.AddSqlPersistence(connectionString);
        return services.BuildServiceProvider();
    }

    private static async Task<string[]> ReadAssignmentSnapshotAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        var rows = await connection.QueryAsync<string>(
            "SELECT Entry FROM (" +
            "SELECT N'U|' + CONVERT(nvarchar(20), UserId) + N'|' + PermissionKey + N'|' + " +
            "CASE WHEN Granted = 1 THEN N'1' ELSE N'0' END AS Entry FROM dbo.UserPermissions " +
            "UNION ALL SELECT N'R|' + CONVERT(nvarchar(20), RoleId) + N'|' + PermissionKey + N'|1' " +
            "FROM dbo.RolePermissions " +
            "UNION ALL SELECT N'M|' + CONVERT(nvarchar(20), UserId) + N'|' + " +
            "CONVERT(nvarchar(20), RoleId) + N'|1' FROM dbo.UserRoles) assignments ORDER BY Entry;");
        return rows.ToArray();
    }
    private static string ImportJson(string targetType, int targetId, params string[] keyAndDecisionPairs)
    {
        var entries = Enumerable.Range(0, keyAndDecisionPairs.Length / 2)
            .Select(index => "{\"permissionKey\":\"" + keyAndDecisionPairs[index * 2] + "\",\"decision\":\"" +
                             keyAndDecisionPairs[(index * 2) + 1] + "\"}");
        return "{\"schemaVersion\":1,\"targetType\":\"" + targetType + "\",\"targetId\":" +
               targetId + ",\"entries\":[" + string.Join(",", entries) + "]}";
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

    private static async Task<SqlTestDatabase> CreateMigratedDatabaseAsync()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            return database;
        }
        catch (Exception setupError)
        {
            try
            {
                await database.DisposeAsync();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(
                    "Test database setup and cleanup both failed.",
                    setupError,
                    cleanupError);
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
