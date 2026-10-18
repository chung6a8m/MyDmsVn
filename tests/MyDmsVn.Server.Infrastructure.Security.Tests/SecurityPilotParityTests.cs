using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Infrastructure.IntegrationTests.Pilot;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Server.Infrastructure.Security;
using Xunit;
using Xunit.Abstractions;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// After T05, the production side is native; the T02 prototype remains until T06.
public sealed class SecurityPilotParityTests(ITestOutputHelper output)
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Security_pilot_preserves_multiset_import_outer_rollback_and_denied_generic_writes()
    {
        foreach (var candidate in new[] { false, true }) await PilotSql.Owned(async database =>
        {
            var seed = await Seed(database.ConnectionString); using var provider = new ServiceCollection().BuildServiceProvider();
            var factory = Factory(database.ConnectionString, candidate, provider);
            using (var uow = factory.Create())
            {
                var ctx = (ISqlExecutionContext)uow;
                Assert.Equal("PilotRuntime", await PilotSql.Scalar<string>((SqlConnection)ctx.Connection, null, "SELECT USER_NAME()"));
                await new SqlRuntimePrincipalValidator().ValidateConnectionAsync((SqlConnection)ctx.Connection, CancellationToken.None);
                uow.BeginTransaction(); var repository = uow.Repository<IPermissionAssignmentImportRepository>();
                var statistics = (SqlConnection)ctx.Connection; statistics.StatisticsEnabled = true; statistics.ResetStatistics();
                var before = await repository.ReadTargetAsync(PermissionImportTargetTypeDto.Role, seed.Role, seed.Actor, CancellationToken.None);
                Assert.Equal(1L, (long)statistics.RetrieveStatistics()["ServerRoundtrips"]!);
                Assert.Equal("Pilot target", before.Name); Assert.Equal(0, before.AssignmentRevision); Assert.Single(before.ExistingDecisions); Assert.True(before.ExistingDecisions["Catalog.Products.Read"]);
                var import = new ParsedPermissionAssignmentImportDto(1, PermissionImportTargetTypeDto.Role, seed.Role, new[] { new PermissionAssignmentImportEntryDto("Catalog.Products.Write", true) });
                statistics.ResetStatistics();
                var changed = await repository.ApplyAsync(import, seed.Actor, 0, Guid.NewGuid(), CancellationToken.None);
                Assert.Equal(1L, (long)statistics.RetrieveStatistics()["ServerRoundtrips"]!);
                Assert.True(changed.Changed); Assert.Equal(1, changed.AddedCount); Assert.Equal(1, changed.TargetRevision);
                var during = await repository.ReadTargetAsync(PermissionImportTargetTypeDto.Role, seed.Role, seed.Actor, CancellationToken.None); Assert.Equal(2, during.ExistingDecisions.Count); Assert.Equal(1, during.AssignmentRevision);
                Assert.Equal(1, await PilotSql.Scalar<int>((SqlConnection)ctx.Connection, (SqlTransaction)ctx.Transaction!, "SELECT @@TRANCOUNT"));
                // Independent administrative oracle only: runtime deliberately cannot SELECT audit.
                using (var auditObserver = new SqlConnection(database.ConnectionString))
                {
                    await auditObserver.OpenAsync();
                    Assert.Equal(1, await PilotSql.Scalar<int>(auditObserver, null, "SELECT COUNT(*) FROM dbo.SecurityAuditEvents WITH (READUNCOMMITTED) WHERE ActorUserId=@actor AND TargetId=@role", PilotSql.P("@actor", SqlDbType.Int, seed.Actor), PilotSql.P("@role", SqlDbType.Int, seed.Role)));
                }
                uow.Rollback();
            }
            using (var owner = new SqlConnection(database.ConnectionString))
            {
                await owner.OpenAsync(); Assert.Equal(0L, await PilotSql.Scalar<long>(owner, null, "SELECT AssignmentRevision FROM dbo.Roles WHERE RoleId=@id", PilotSql.P("@id", SqlDbType.Int, seed.Role)));
                Assert.Equal(0, await PilotSql.Scalar<int>(owner, null, "SELECT COUNT(*) FROM dbo.SecurityAuditEvents"));
                Assert.Equal(1, await PilotSql.Scalar<int>(owner, null, "SELECT COUNT(*) FROM dbo.RolePermissions WHERE RoleId=@id", PilotSql.P("@id", SqlDbType.Int, seed.Role)));
            }
            using (var runtime = (SqlConnection)await new RuntimeConnections(database.ConnectionString).OpenConnectionAsync(CancellationToken.None))
            {
                foreach (var sql in new[] { "INSERT dbo.Roles(RoleName,NormalizedRoleName) VALUES(N'Bypass',N'BYPASS')", "UPDATE dbo.SystemSecuritySettings SET BoundNormalizedUsername=N'BYPASS' WHERE Id=1", "INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@id,N'Catalog.Products.Write')" })
                {
                    var denied = await Assert.ThrowsAsync<SqlException>(() => PilotSql.Execute(runtime, null, sql, PilotSql.P("@id", SqlDbType.Int, seed.Role))); Assert.Equal(229, denied.Number);
                }
            }
            output.WriteLine("Security pilot provider=" + (candidate ? "candidate" : "production") + " runtime principal verified; two result sets, import/audit rollback and RBAC/binding denials preserved.");
        });
    }
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Security_pilot_reuses_existing_store_error_translation_for_invalid_requests_and_revoked_actors()
    {
        foreach (var candidate in new[] { false, true }) await PilotSql.Owned(async database =>
        {
            var seed = await Seed(database.ConnectionString); using var provider = new ServiceCollection().BuildServiceProvider();
            var store = new SqlSecurityAdministrationStore(Factory(database.ConnectionString, candidate, provider));
            var invalid = await store.ReadTargetAsync(PermissionImportTargetTypeDto.Role, 0, seed.Actor, CancellationToken.None); Assert.True(invalid.IsError); Assert.Equal("Security.ImportInvalid", invalid.FirstError.Code);
            var forbidden = await store.ReadTargetAsync(PermissionImportTargetTypeDto.Role, seed.Role, int.MaxValue, CancellationToken.None); Assert.True(forbidden.IsError); Assert.Equal("Auth.Forbidden", forbidden.FirstError.Code);
        });
    }
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Security_pilot_preserves_missing_target_error_translation_for_users_and_roles()
    {
        foreach (var candidate in new[] { false, true })
            await PilotSql.Owned(async database =>
            {
                var seed = await Seed(database.ConnectionString);
                using (var owner = new SqlConnection(database.ConnectionString))
                {
                    await owner.OpenAsync();
                    await PilotSql.Execute(owner, null,
                        "INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@actor,N'Security.Users.Manage',1)",
                        PilotSql.P("@actor", SqlDbType.Int, seed.Actor));
                    Assert.Equal(0, await PilotSql.Scalar<int>(owner, null,
                        "SELECT (SELECT COUNT(*) FROM dbo.Users WHERE UserId=@id) + (SELECT COUNT(*) FROM dbo.Roles WHERE RoleId=@id)",
                        PilotSql.P("@id", SqlDbType.Int, int.MaxValue)));
                }
                using var provider = new ServiceCollection().BuildServiceProvider();
                var store = new SqlSecurityAdministrationStore(Factory(database.ConnectionString, candidate, provider));
                foreach (var targetType in new[] { PermissionImportTargetTypeDto.User, PermissionImportTargetTypeDto.Role })
                {
                    var result = await store.ReadTargetAsync(targetType, int.MaxValue, seed.Actor, CancellationToken.None);
                    Assert.True(result.IsError);
                    Assert.Equal("Security.TargetNotFound", result.FirstError.Code);
                    Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
                }
                // A subsequent successful read still materializes both result sets.
                var existing = await store.ReadTargetAsync(PermissionImportTargetTypeDto.Role, seed.Role, seed.Actor, CancellationToken.None);
                Assert.False(existing.IsError);
                Assert.Equal("Pilot target", existing.Value.Name);
                Assert.Single(existing.Value.ExistingDecisions);
                output.WriteLine("Missing User/Role targets preserve Security.TargetNotFound for provider=" + (candidate ? "candidate" : "production"));
            });
    }

    internal static SqlUnitOfWorkFactory Factory(string cs, bool candidate, IServiceProvider provider)
    {
        var connections = new RuntimeConnections(cs); var sessions = new LinqToDbSessionFactory(connections, new LinqToDbMappingSchemaProvider(Array.Empty<ILinqToDbMapping>()));
        return new SqlUnitOfWorkFactory(connections, null, provider, new Dictionary<Type, Func<IServiceProvider, ISqlExecutionContext, object>>
        {{typeof(IPermissionAssignmentImportRepository),(_,ctx)=>candidate?new SecurityPilot(ctx,sessions):new SqlSecurityAdministrationRepository(ctx)}});
    }
    internal static async Task<(int Actor, int Role)> Seed(string cs)
    {
        using var owner = new SqlConnection(cs); await owner.OpenAsync();
        await PilotSql.Execute(owner, null, "CREATE USER [PilotRuntime] WITHOUT LOGIN; CREATE USER [PilotMigration] WITHOUT LOGIN;");
        var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory != null && !File.Exists(Path.Combine(directory.FullName, "deployment/sql/Provision-MyDmsVnPrincipals.sql"))) directory = directory.Parent;
        if (directory is null) throw new FileNotFoundException("Provisioning script missing.");
        await PilotSql.Execute(owner, null, File.ReadAllText(Path.Combine(directory.FullName, "deployment/sql/Provision-MyDmsVnPrincipals.sql")).Replace("$(RuntimeUser)", "PilotRuntime").Replace("$(MigrationUser)", "PilotMigration"));
        var actor = await PilotSql.Scalar<int>(owner, null, "INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'pilot.manager',N'PILOT.MANAGER',N'Pilot',N'Local',N'hash',N'',N'BCrypt')");
        await PilotSql.Execute(owner, null, "INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@actor,N'Security.Roles.Manage',1)", PilotSql.P("@actor", SqlDbType.Int, actor));
        var role = await PilotSql.Scalar<int>(owner, null, "INSERT dbo.Roles(RoleName,NormalizedRoleName) OUTPUT INSERTED.RoleId VALUES(N'Pilot target',N'PILOT TARGET')");
        await PilotSql.Execute(owner, null, "INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@role,N'Catalog.Products.Read')", PilotSql.P("@role", SqlDbType.Int, role));
        return (actor, role);
    }
    internal sealed class RuntimeConnections(string cs) : IDbConnectionFactory
    {
        public IDbConnection OpenConnection() => OpenConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
        public async Task<IDbConnection> OpenConnectionAsync(CancellationToken token)
        {
            var connection = new SqlConnection(cs);
            try { await connection.OpenAsync(token); await PilotSql.Execute(connection, null, "EXECUTE AS USER=N'PilotRuntime'"); return connection; }
            catch { connection.Dispose(); throw; }
        }
    }
}
