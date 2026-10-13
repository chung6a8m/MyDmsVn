using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.DbMigrator.SecurityProvisioning;
using MyDmsVn.Server.Infrastructure.Identity;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class InitialAdministratorProvisioningIntegrationTests
{
    [SqlServerFact]
    public async Task Provision_creates_active_local_admin_membership_and_durable_marker_atomically()
    {
        var database = await CreateSynchronizedDatabaseAsync();
        try
        {
            var engine = new InitialAdministratorProvisioningEngine(new BcryptPasswordHasher(10));
            var inspection = await engine.InspectAsync(
                database.ConnectionString,
                CancellationToken.None);
            Assert.Equal(InitialAdministratorProvisioningStatus.Ready, inspection.Status);

            var result = await engine.ProvisionAsync(
                database.ConnectionString,
                new InitialAdministratorProvisioningRequest(
                    inspection.DatabaseIdentity,
                    " first.admin ",
                    "First Administrator",
                    StrongPassword,
                    "deployment-operator",
                    PermissionRegistryComposition.CreateDefault().ReleaseId),
                UtcNow,
                CancellationToken.None);
            Assert.Equal(InitialAdministratorProvisioningOutcome.Created, result.Outcome);

            using var connection = new SqlConnection(database.ConnectionString);
            var user = await connection.QuerySingleAsync<UserRow>(
                "SELECT UserId, Username, NormalizedUsername, DisplayName, Source, " +
                "PasswordHash, PasswordSalt, PasswordAlgorithm, IsActive FROM dbo.Users;");
            Assert.Equal("first.admin", user.Username);
            Assert.Equal("FIRST.ADMIN", user.NormalizedUsername);
            Assert.Equal("First Administrator", user.DisplayName);
            Assert.Equal("Local", user.Source);
            Assert.Equal(string.Empty, user.PasswordSalt);
            Assert.Equal(BcryptPasswordHasher.AlgorithmName, user.PasswordAlgorithm);
            Assert.True(user.IsActive);
            Assert.True(new BcryptPasswordHasher(10).Verify(StrongPassword, user.PasswordHash));
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.UserRoles AS membership " +
                "JOIN dbo.SystemManagedRoles AS managed ON managed.RoleId = membership.RoleId " +
                "WHERE membership.UserId = @UserId AND managed.SystemRoleKey = 'Administrator';",
                new { user.UserId }));
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SecurityBootstrapState " +
                "WHERE BootstrapKey = 'InitialAdministrator' AND AdministratorUserId = @UserId;",
                new { user.UserId }));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Completed_marker_makes_repeat_a_no_op_even_after_admin_deletion()
    {
        var database = await CreateSynchronizedDatabaseAsync();
        try
        {
            var engine = new InitialAdministratorProvisioningEngine(new BcryptPasswordHasher(10));
            var inspection = await engine.InspectAsync(database.ConnectionString, CancellationToken.None);
            var request = new InitialAdministratorProvisioningRequest(
                inspection.DatabaseIdentity,
                "first.admin",
                "First Administrator",
                StrongPassword,
                "deployment-operator",
                PermissionRegistryComposition.CreateDefault().ReleaseId);
            await engine.ProvisionAsync(
                database.ConnectionString,
                request,
                UtcNow,
                CancellationToken.None);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync("DELETE dbo.UserRoles; DELETE dbo.Users;");
            }

            var repeat = await engine.ProvisionAsync(
                database.ConnectionString,
                new InitialAdministratorProvisioningRequest(
                    inspection.DatabaseIdentity,
                    "attacker",
                    "Replacement",
                    "DifferentStrong!456",
                    "deployment-operator",
                    request.ReleaseId),
                UtcNow.AddMinutes(1),
                CancellationToken.None);

            Assert.Equal(InitialAdministratorProvisioningOutcome.AlreadyCompleted, repeat.Outcome);
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(0, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Users;"));
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SecurityBootstrapState WHERE BootstrapKey = 'InitialAdministrator';"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Existing_identity_without_marker_fails_closed()
    {
        var database = await CreateSynchronizedDatabaseAsync();
        try
        {
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "INSERT dbo.Users " +
                    "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, " +
                    "PasswordAlgorithm, IsActive) VALUES " +
                    "(N'existing', N'EXISTING', N'Existing', N'Local', N'hash', N'', N'BCrypt', 1);");
            }

            var engine = new InitialAdministratorProvisioningEngine(new BcryptPasswordHasher(10));
            var inspection = await engine.InspectAsync(database.ConnectionString, CancellationToken.None);
            Assert.Equal(InitialAdministratorProvisioningStatus.RecoveryRequired, inspection.Status);
            var exception = await Assert.ThrowsAsync<InitialAdministratorProvisioningException>(
                () => engine.ProvisionAsync(
                    database.ConnectionString,
                    new InitialAdministratorProvisioningRequest(
                        inspection.DatabaseIdentity,
                        "first.admin",
                        "First Administrator",
                        StrongPassword,
                        "deployment-operator",
                        PermissionRegistryComposition.CreateDefault().ReleaseId),
                    UtcNow,
                    CancellationToken.None));
            Assert.Equal("SecurityBootstrap.RecoveryRequired", exception.Code);

            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Users;"));
            Assert.Equal(0, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SecurityBootstrapState;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Failure_before_marker_rolls_back_user_and_membership_and_records_failed_attempt()
    {
        var database = await CreateSynchronizedDatabaseAsync();
        try
        {
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "CREATE TRIGGER dbo.TR_SecurityBootstrapState_RejectTest " +
                    "ON dbo.SecurityBootstrapState INSTEAD OF INSERT AS " +
                    "THROW 51000, 'Injected bootstrap marker failure.', 1;");
            }

            var engine = new InitialAdministratorProvisioningEngine(new BcryptPasswordHasher(10));
            var inspection = await engine.InspectAsync(database.ConnectionString, CancellationToken.None);
            var exception = await Assert.ThrowsAsync<InitialAdministratorProvisioningException>(
                () => engine.ProvisionAsync(
                    database.ConnectionString,
                    new InitialAdministratorProvisioningRequest(
                        inspection.DatabaseIdentity,
                        "first.admin",
                        "First Administrator",
                        StrongPassword,
                        "deployment-operator",
                        PermissionRegistryComposition.CreateDefault().ReleaseId),
                    UtcNow,
                    CancellationToken.None));

            Assert.Equal("SecurityBootstrap.Failed", exception.Code);
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(0, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Users;"));
            Assert.Equal(0, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.UserRoles;"));
            Assert.Equal(0, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SecurityBootstrapState;"));
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.PermissionSecurityAudit " +
                "WHERE Operation = 'SecurityBootstrap.Enroll' AND Outcome = 'Failed' " +
                "AND ReasonCode = 'SecurityBootstrap.Failed';"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Concurrent_enrollment_attempts_create_exactly_one_administrator()
    {
        var database = await CreateSynchronizedDatabaseAsync();
        try
        {
            var engine = new InitialAdministratorProvisioningEngine(new BcryptPasswordHasher(10));
            var inspection = await engine.InspectAsync(database.ConnectionString, CancellationToken.None);
            var request = new InitialAdministratorProvisioningRequest(
                inspection.DatabaseIdentity,
                "first.admin",
                "First Administrator",
                StrongPassword,
                "deployment-operator",
                PermissionRegistryComposition.CreateDefault().ReleaseId);

            async Task<string> AttemptAsync()
            {
                try
                {
                    var result = await engine.ProvisionAsync(
                        database.ConnectionString,
                        request,
                        UtcNow,
                        CancellationToken.None);
                    return result.Outcome.ToString();
                }
                catch (InitialAdministratorProvisioningException exception)
                {
                    return exception.Code;
                }
            }

            var outcomes = await Task.WhenAll(AttemptAsync(), AttemptAsync());
            Assert.Equal(1, outcomes.Count(outcome =>
                outcome == InitialAdministratorProvisioningOutcome.Created.ToString()));
            Assert.Contains(outcomes, outcome =>
                outcome == InitialAdministratorProvisioningOutcome.AlreadyCompleted.ToString() ||
                outcome == "SecurityBootstrap.Busy");

            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Users;"));
            Assert.Equal(1, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.UserRoles;"));
            Assert.Equal(1, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SecurityBootstrapState;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private const string StrongPassword = "Bootstrap!Pass456";
    private static readonly DateTime UtcNow =
        new DateTime(2026, 10, 14, 3, 0, 0, DateTimeKind.Utc);

    private static async Task<SqlTestDatabase> CreateSynchronizedDatabaseAsync()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
        Assert.True(migration.Successful, migration.Error?.ToString());
        var registry = PermissionRegistryComposition.CreateDefault();
        var approvals = new PermissionSyncApprovalSet(
            registry.ReleaseId,
            "release-operator",
            UtcNow.AddHours(4),
            registry.Definitions.Select(definition => new PermissionSyncApproval(
                definition.Key,
                PermissionCatalogDecision.Approved,
                AdministratorGrantDecision.Approved)));
        var synchronization = new PermissionSynchronizationEngine();
        var plan = await synchronization.CreatePlanAsync(
            database.ConnectionString,
            registry,
            approvals,
            UtcNow.AddMinutes(-1),
            CancellationToken.None);
        await synchronization.ApplyAsync(
            database.ConnectionString,
            registry,
            approvals,
            plan,
            UtcNow.AddMinutes(-1),
            CancellationToken.None);
        return database;
    }

    private sealed class UserRow
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string NormalizedUsername { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public string PasswordAlgorithm { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
