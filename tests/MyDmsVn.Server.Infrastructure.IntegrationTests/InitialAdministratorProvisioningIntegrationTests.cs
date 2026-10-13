using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.DbMigrator.SecurityProvisioning;
using MyDmsVn.Server.Infrastructure;
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

            string originalHash;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                originalHash = await connection.QuerySingleAsync<string>(
                    "SELECT PasswordHash FROM dbo.Users WHERE NormalizedUsername = N'FIRST.ADMIN';");
            }

            var noReset = await engine.ProvisionAsync(
                database.ConnectionString,
                new InitialAdministratorProvisioningRequest(
                    inspection.DatabaseIdentity,
                    "first.admin",
                    "First Administrator",
                    "DifferentStrong!456",
                    "deployment-operator",
                    request.ReleaseId),
                UtcNow.AddSeconds(30),
                CancellationToken.None);
            Assert.Equal(InitialAdministratorProvisioningOutcome.AlreadyCompleted, noReset.Outcome);
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                Assert.Equal(originalHash, await connection.QuerySingleAsync<string>(
                    "SELECT PasswordHash FROM dbo.Users WHERE NormalizedUsername = N'FIRST.ADMIN';"));
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

    [SqlServerFact]
    public async Task Fresh_install_admin_logs_in_and_receives_only_active_assignments_with_direct_deny_precedence()
    {
        var database = await CreateSynchronizedDatabaseAsync();
        try
        {
            var engine = new InitialAdministratorProvisioningEngine(new BcryptPasswordHasher(10));
            var inspection = await engine.InspectAsync(database.ConnectionString, CancellationToken.None);
            await engine.ProvisionAsync(
                database.ConnectionString,
                new InitialAdministratorProvisioningRequest(
                    inspection.DatabaseIdentity,
                    "first.admin",
                    "First Administrator",
                    StrongPassword,
                    "deployment-operator",
                    PermissionRegistryComposition.CreateDefault().ReleaseId),
                UtcNow,
                CancellationToken.None);

            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddSqlPersistence(database.ConnectionString);
            using var provider = services.BuildServiceProvider();
            var login = await provider.GetRequiredService<MediatR.ISender>().Send(
                new LoginCommand("first.admin", StrongPassword),
                CancellationToken.None);
            Assert.False(login.IsError);

            var registry = PermissionRegistryComposition.CreateDefault();
            var store = provider.GetRequiredService<IPermissionStore>();
            var authenticated = CurrentUser.Authenticated(
                login.Value.UserId,
                login.Value.Username,
                login.Value.DisplayName,
                isActive: true);
            var authorization = new PermissionAuthorizationService(
                new FixedCurrentUserAccessor(authenticated),
                store,
                registry);
            foreach (var definition in registry.Definitions)
            {
                Assert.True(await authorization.IsAllowedAsync(
                    definition.Key,
                    CancellationToken.None));
            }

            var deniedKey = registry.Definitions[0].Key;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "INSERT dbo.UserPermissions (UserId, PermissionKey, Granted) " +
                    "VALUES (@UserId, @PermissionKey, 0);",
                    new { UserId = login.Value.UserId, PermissionKey = deniedKey });
            }

            Assert.False(await authorization.IsAllowedAsync(deniedKey, CancellationToken.None));
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "UPDATE dbo.Users SET IsActive = 0 WHERE UserId = @UserId;",
                    new { UserId = login.Value.UserId });
            }

            var inactiveLogin = await provider.GetRequiredService<MediatR.ISender>().Send(
                new LoginCommand("first.admin", StrongPassword),
                CancellationToken.None);
            Assert.True(inactiveLogin.IsError);
            var anonymousAuthorization = new PermissionAuthorizationService(
                new FixedCurrentUserAccessor(CurrentUser.Anonymous),
                store,
                registry);
            Assert.False(await anonymousAuthorization.IsAllowedAsync(
                registry.Definitions[1].Key,
                CancellationToken.None));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Redirected_bootstrap_apply_exits_promptly_without_reading_or_echoing_secret()
    {
        var database = await CreateSynchronizedDatabaseAsync();
        try
        {
            var inspection = await new InitialAdministratorProvisioningEngine(
                    new BcryptPasswordHasher(10))
                .InspectAsync(database.ConnectionString, CancellationToken.None);
            var executable = Path.Combine(
                AppContext.BaseDirectory,
                "MyDmsVn.Server.DbMigrator.exe");
            Assert.True(File.Exists(executable), "DbMigrator executable was not copied to test output.");
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments =
                    "security bootstrap-admin --apply " +
                    "--confirm-database " + inspection.DatabaseIdentity.ToString("D") + " " +
                    "--username first.admin --display-name FirstAdministrator " +
                    "--actor deployment-operator --release-id " +
                    PermissionRegistryComposition.CreateDefault().ReleaseId,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.EnvironmentVariables[
                "MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING"] = database.ConnectionString;
            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            process!.StandardInput.WriteLine("SentinelSecret!456");
            process.StandardInput.Close();
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000))
            {
                process.Kill();
                Assert.Fail("Bootstrap subprocess did not exit within 15 seconds.");
            }

            var output = await standardOutput;
            var error = await standardError;
            Assert.Equal(3, process.ExitCode);
            Assert.Contains("SecurityBootstrap.InteractiveInputRequired", error, StringComparison.Ordinal);
            Assert.DoesNotContain("SentinelSecret", output, StringComparison.Ordinal);
            Assert.DoesNotContain("SentinelSecret", error, StringComparison.Ordinal);
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

    private sealed class FixedCurrentUserAccessor : ICurrentUserAccessor
    {
        public FixedCurrentUserAccessor(CurrentUser current)
        {
            Current = current;
        }

        public CurrentUser Current { get; }
    }
}
