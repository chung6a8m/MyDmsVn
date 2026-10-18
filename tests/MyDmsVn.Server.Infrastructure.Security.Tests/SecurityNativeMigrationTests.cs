using System;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.DbMigrator.SecurityProvisioning;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.IntegrationTests.Pilot;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Server.Infrastructure.Security;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SecurityNativeMigrationTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Identity_preserves_unicode_credentials_nullable_snapshot_and_atomic_password_timestamp() =>
        PilotSql.Owned(async database =>
        {
            using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
            var id = await PilotSql.Scalar<int>(oracle, null,
                "INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'người',N'NGƯỜI',N'Người Việt',N'Local',N'old-hash',N'legacy-salt',N'Legacy')");
            var services = new ServiceCollection(); services.AddSqlPersistence(database.ConnectionString); using var provider = services.BuildServiceProvider();
            var users = provider.GetRequiredService<IUserStore>(); var permissions = provider.GetRequiredService<IPermissionStore>();
            var account = await users.FindByNormalizedUsernameAsync("NGƯỜI", CancellationToken.None);
            Assert.NotNull(account); Assert.Equal("Người Việt", account!.DisplayName); Assert.Equal("legacy-salt", account.PasswordSalt); Assert.Equal("Legacy", account.PasswordAlgorithm);
            Assert.Null(await users.FindByNormalizedUsernameAsync("MISSING", CancellationToken.None));
            var snapshot = await permissions.GetSnapshotAsync(id, "Catalog.Products.Read", CancellationToken.None);
            Assert.True(snapshot.IsAccountActive); Assert.Equal("NGƯỜI", snapshot.DatabaseUsername); Assert.Null(snapshot.DirectDecision); Assert.Null(snapshot.BoundSuperuserUserId); Assert.Null(snapshot.BoundNormalizedUsername); Assert.False(snapshot.HasRoleGrant);
            var absent = await permissions.GetSnapshotAsync(int.MaxValue, "Catalog.Products.Read", CancellationToken.None);
            Assert.False(absent.IsAccountActive); Assert.Null(absent.DatabaseUsername);
            var stamp = new DateTime(2026, 10, 18, 1, 2, 3, DateTimeKind.Utc).AddTicks(1234567);
            var replacement = new PasswordReplacement(id, "old-hash", "Legacy", "new-hash", "", "BCrypt", stamp);
            Assert.True(await users.TryReplacePasswordAsync(replacement, CancellationToken.None)); Assert.False(await users.TryReplacePasswordAsync(replacement, CancellationToken.None));
            Assert.Equal(stamp, await PilotSql.Scalar<DateTime>(oracle, null, "SELECT UpdatedAtUtc FROM dbo.Users"));
            account = await users.FindByNormalizedUsernameAsync("NGƯỜI", CancellationToken.None); Assert.Equal("new-hash", account!.PasswordHash); Assert.Equal("", account.PasswordSalt); Assert.Equal("BCrypt", account.PasswordAlgorithm);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => users.FindByNormalizedUsernameAsync("NGƯỜI", cancel.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => permissions.GetSnapshotAsync(id, "Catalog.Products.Read", cancel.Token));
            Assert.True(await provider.GetRequiredService<IUserAccountValidator>().IsActiveAsync(id, CancellationToken.None));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Procedure_readers_surface_errors_after_empty_and_populated_results_without_committing() =>
        PilotSql.Owned(async database =>
        {
            // Deliberate procedure-fault database: never eligible for fixture reuse.
            using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
            var services = new ServiceCollection(); services.AddSqlPersistence(database.ConnectionString); using var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<IPermissionAssignmentImportStore>();
            var import = new ParsedPermissionAssignmentImportDto(1, PermissionImportTargetTypeDto.Role, 42, Array.Empty<PermissionAssignmentImportEntryDto>());
            await ReplaceBody(oracle, "dbo.ReadSecurityPermissionImportTarget", "SELECT CAST(N'No row' AS nvarchar(100)) AS TargetName, CAST(1 AS bit) AS IsActive, CAST(0 AS bigint) AS AssignmentRevision, CAST(0 AS bit) AS IsProtected WHERE 1=0; THROW 51031, 'Expected missing target.', 1;");
            var missing = await store.ReadTargetAsync(PermissionImportTargetTypeDto.Role, 42, 1, CancellationToken.None);
            Assert.True(missing.IsError); Assert.Equal("Security.TargetNotFound", missing.FirstError.Code);
            await ReplaceBody(oracle, "dbo.ApplySecurityPermissionImport", "SELECT CAST(9 AS bigint) AS TargetRevision, CAST(1 AS int) AS AddedCount, CAST(1 AS bit) AS Changed; THROW 51030, 'Expected late revision conflict.', 1;");
            var conflict = await store.ApplyAsync(import, 1, 0, Guid.NewGuid(), CancellationToken.None);
            Assert.True(conflict.IsError); Assert.Equal("Security.RevisionConflict", conflict.FirstError.Code);
            Assert.Equal(0, await PilotSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.SecurityAuditEvents"));
            await ReplaceBody(oracle, "dbo.ApplySecurityAdministration", "SELECT CAST(11 AS bigint) AS AssignmentRevision, CAST(1 AS bit) AS Changed, CAST(NULL AS bigint) AS RelatedAssignmentRevision; THROW 51030, 'Expected late administration conflict.', 1;");
            var mutation = new SecurityAdministrationMutation(SecurityAdministrationTarget.Role, SecurityAdministrationOperation.Create, 1, 0, 0, "Role", "ROLE", null, null, true, null, null, null, null, null, null, Guid.NewGuid());
            var failed = await provider.GetRequiredService<ISecurityAdministrationStore>().ApplyAsync(mutation, CancellationToken.None);
            Assert.True(failed.IsError); Assert.Equal("Security.RevisionConflict", failed.FirstError.Code);
            Assert.Equal(0, await PilotSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.Roles"));
            using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create(); uow.BeginTransaction();
            var ctx = (ISqlExecutionContext)uow;
            Assert.Equal(1, await PilotSql.Scalar<int>((SqlConnection)ctx.Connection, (SqlTransaction)ctx.Transaction!, "SELECT @@TRANCOUNT"));
            uow.Rollback();
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Maintenance_preserves_datetime2_ticks_and_the_bound_account_identity() =>
        PilotSql.Owned(async database =>
        {
            var hasher = new BcryptPasswordHasher(10);
            var created = new DateTime(2026, 10, 18, 1, 2, 3, DateTimeKind.Utc).AddTicks(1234567);
            var id = await new SuperuserBootstrapEngine(hasher).BootstrapAsync(database.ConnectionString, "root.admin", "Quản trị", "Initial!Pass456", created, CancellationToken.None);
            using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
            Assert.Equal(created, await PilotSql.Scalar<DateTime>(oracle, null, "SELECT CreatedAtUtc FROM dbo.Users"));
            var engine = new SuperuserMaintenanceEngine(hasher); var recovered = created.AddMinutes(1).AddTicks(1);
            await engine.RecoverAsync(database.ConnectionString, "root.admin", id, "Recovery!Pass456", recovered, CancellationToken.None);
            Assert.Equal(recovered, await PilotSql.Scalar<DateTime>(oracle, null, "SELECT UpdatedAtUtc FROM dbo.Users"));
            Assert.Equal("", await PilotSql.Scalar<string>(oracle, null, "SELECT PasswordSalt FROM dbo.Users"));
            var renamed = recovered.AddMinutes(1).AddTicks(1);
            await engine.RenameAsync(database.ConnectionString, "root.admin", id, "root.renamed", renamed, CancellationToken.None);
            Assert.Equal(renamed, await PilotSql.Scalar<DateTime>(oracle, null, "SELECT UpdatedAtUtc FROM dbo.Users"));
            Assert.Equal(created, await PilotSql.Scalar<DateTime>(oracle, null, "SELECT CreatedAtUtc FROM dbo.Users"));
            Assert.Equal(id, await PilotSql.Scalar<int>(oracle, null, "SELECT UserId FROM dbo.Users"));
            Assert.Equal(id, await PilotSql.Scalar<int>(oracle, null, "SELECT SuperuserUserId FROM dbo.SystemSecuritySettings"));
            Assert.Equal("ROOT.RENAMED", await PilotSql.Scalar<string>(oracle, null, "SELECT BoundNormalizedUsername FROM dbo.SystemSecuritySettings"));
            Assert.Equal("ROOT.RENAMED", await PilotSql.Scalar<string>(oracle, null, "SELECT NormalizedUsername FROM dbo.Users"));
            Assert.Equal(0, await PilotSql.Scalar<int>(oracle, null, "SELECT (SELECT COUNT(*) FROM dbo.Roles)+(SELECT COUNT(*) FROM dbo.UserPermissions)"));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Maintenance_rejects_wrong_binding_and_rolls_back_recovery_and_partial_rename_failures() =>
        PilotSql.Owned(async database =>
        {
            var hasher = new BcryptPasswordHasher(10); var engine = new SuperuserMaintenanceEngine(hasher);
            var stamp = new DateTime(2026, 10, 18, 1, 2, 3, DateTimeKind.Utc);
            var id = await new SuperuserBootstrapEngine(hasher).BootstrapAsync(database.ConnectionString, "root.admin", "Root", "Initial!Pass456", stamp, CancellationToken.None);
            using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
            var hash = await PilotSql.Scalar<string>(oracle, null, "SELECT PasswordHash FROM dbo.Users");
            await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecoverAsync(database.ConnectionString, "root.admin", id + 1, "Recovery!Pass456", stamp, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RenameAsync(database.ConnectionString, "wrong.admin", id, "root.renamed", stamp, CancellationToken.None));
            await AssertMaintenanceUnchanged(oracle, id, hash);
            await PilotSql.Execute(oracle, null, "CREATE TRIGGER dbo.TR_TestRecoveryFailure ON dbo.Users AFTER UPDATE AS BEGIN SET NOCOUNT ON; THROW 51193, 'Expected recovery failure.', 1; END;");
            var recoveryError = await Assert.ThrowsAsync<SqlException>(() => engine.RecoverAsync(database.ConnectionString, "root.admin", id, "Recovery!Pass456", stamp, CancellationToken.None));
            Assert.Equal(51193, recoveryError.Number); await AssertMaintenanceUnchanged(oracle, id, hash);
            await PilotSql.Execute(oracle, null, "DROP TRIGGER dbo.TR_TestRecoveryFailure;");
            await PilotSql.Execute(oracle, null, "CREATE TRIGGER dbo.TR_TestRenameFailure ON dbo.SystemSecuritySettings AFTER UPDATE AS BEGIN SET NOCOUNT ON; THROW 51194, 'Expected binding update failure.', 1; END;");
            var renameError = await Assert.ThrowsAsync<SqlException>(() => engine.RenameAsync(database.ConnectionString, "root.admin", id, "root.renamed", stamp, CancellationToken.None));
            Assert.Equal(51194, renameError.Number); await AssertMaintenanceUnchanged(oracle, id, hash);
            await PilotSql.Execute(oracle, null, "DROP TRIGGER dbo.TR_TestRenameFailure;");
            await engine.RenameAsync(database.ConnectionString, "root.admin", id, "root.renamed", stamp, CancellationToken.None);
            Assert.Equal("ROOT.RENAMED", await PilotSql.Scalar<string>(oracle, null, "SELECT NormalizedUsername FROM dbo.Users"));
            Assert.Equal("ROOT.RENAMED", await PilotSql.Scalar<string>(oracle, null, "SELECT BoundNormalizedUsername FROM dbo.SystemSecuritySettings"));
        });

    private static async Task AssertMaintenanceUnchanged(SqlConnection oracle, int id, string hash)
    {
        Assert.Equal(1, await PilotSql.Scalar<int>(oracle, null,
            "SELECT COUNT(*) FROM dbo.Users WHERE UserId=@id AND NormalizedUsername=N'ROOT.ADMIN' AND PasswordHash=@hash AND PasswordSalt=N'' AND PasswordAlgorithm=N'BCrypt' AND IsActive=1 AND UpdatedAtUtc IS NULL AND UpdatedByUserId IS NULL",
            PilotSql.P("@id", SqlDbType.Int, id), PilotSql.P("@hash", SqlDbType.NVarChar, hash)));
        Assert.Equal(id, await PilotSql.Scalar<int>(oracle, null, "SELECT SuperuserUserId FROM dbo.SystemSecuritySettings"));
        Assert.Equal("ROOT.ADMIN", await PilotSql.Scalar<string>(oracle, null, "SELECT BoundNormalizedUsername FROM dbo.SystemSecuritySettings"));
    }

    private static async Task ReplaceBody(SqlConnection connection, string procedure, string body)
    {
        var definition = await PilotSql.Scalar<string>(connection, null, "SELECT OBJECT_DEFINITION(OBJECT_ID(@procedure))", PilotSql.P("@procedure", SqlDbType.NVarChar, procedure));
        var bodyStart = Regex.Match(definition, @"\r?\nAS\b", RegexOptions.IgnoreCase); Assert.True(bodyStart.Success);
        var header = Regex.Replace(definition.Substring(0, bodyStart.Index), @"CREATE PROCEDURE", "ALTER PROCEDURE", RegexOptions.IgnoreCase);
        await PilotSql.Execute(connection, null, header + " AS BEGIN SET NOCOUNT ON; " + body + " END;");
    }
}
