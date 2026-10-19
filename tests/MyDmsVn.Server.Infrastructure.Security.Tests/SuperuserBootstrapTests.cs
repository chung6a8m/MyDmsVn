using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.DbMigrator.SecurityProvisioning;
using MyDmsVn.Server.Infrastructure.IntegrationTests;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Operations;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.Security.Tests;

public sealed class SuperuserBootstrapTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Bootstrap_binds_one_configured_identity_without_creating_roles_or_grants()
    {
        var database = await CreateFreshDatabaseAsync();
        try
        {
            var userId = await CreateEngine().BootstrapAsync(
                database.ConnectionString,
                "root.admin",
                "Root Admin",
                "Bootstrap!Pass456",
                UtcNow,
                CancellationToken.None);

            using var connection = new SqlConnection(database.ConnectionString);
            var identity = await connection.QuerySingleAsync<BindingRow>(
                "SELECT u.UserId, u.NormalizedUsername, settings.SuperuserUserId, " +
                "settings.BoundNormalizedUsername FROM dbo.Users AS u " +
                "INNER JOIN dbo.SystemSecuritySettings AS settings ON settings.Id = 1;");
            Assert.Equal(userId, identity.UserId);
            Assert.Equal(userId, identity.SuperuserUserId);
            Assert.Equal("ROOT.ADMIN", identity.NormalizedUsername);
            Assert.Equal("ROOT.ADMIN", identity.BoundNormalizedUsername);
            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Roles;"));
            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.UserRoles;"));
            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.RolePermissions;"));
            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.UserPermissions;"));
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.Permissions');"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Binding_insert_failure_rolls_back_the_account_insert()
    {
        var database = await CreateFreshDatabaseAsync();
        try
        {
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "CREATE TRIGGER dbo.TR_Test_FailSuperuserBinding ON dbo.SystemSecuritySettings " +
                    "AFTER INSERT AS BEGIN THROW 51000, 'Test binding failure.', 1; END;");
            }

            await Assert.ThrowsAsync<SqlException>(() => CreateEngine().BootstrapAsync(
                database.ConnectionString,
                "root.admin",
                "Root Admin",
                "Bootstrap!Pass456",
                UtcNow,
                CancellationToken.None));

            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(0, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Users;"));
            Assert.Equal(0, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SystemSecuritySettings;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Startup_binding_check_rejects_configuration_mismatch_but_allows_disabled_bound_account()
    {
        var database = await CreateFreshDatabaseAsync();
        try
        {
            var userId = await CreateEngine().BootstrapAsync(
                database.ConnectionString,
                "root.admin",
                "Root Admin",
                "Bootstrap!Pass456",
                UtcNow,
                CancellationToken.None);
            var validator = new SqlSuperuserBindingValidator();

            await validator.ValidateAsync(
                database.ConnectionString,
                " ROOT.ADMIN ",
                CancellationToken.None);
            var mismatch = await Assert.ThrowsAsync<DesktopStartupException>(() => validator.ValidateAsync(
                database.ConnectionString,
                "other.admin",
                CancellationToken.None));
            Assert.Equal("Security.SuperuserBindingMismatch", mismatch.Code);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "UPDATE dbo.Users SET IsActive = 0 WHERE UserId = @userId;",
                    new { userId });
            }

            await validator.ValidateAsync(
                database.ConnectionString,
                "root.admin",
                CancellationToken.None);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Concurrent_bootstrap_creates_exactly_one_user_and_binding()
    {
        var database = await CreateFreshDatabaseAsync();
        try
        {
            var attempts = Enumerable.Range(0, 2).Select(async _ =>
            {
                try
                {
                    return (int?)await CreateEngine().BootstrapAsync(
                        database.ConnectionString,
                        "root.admin",
                        "Root Admin",
                        "Bootstrap!Pass456",
                        UtcNow,
                        CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            });

            var results = await Task.WhenAll(attempts);
            Assert.Single(results, result => result.HasValue);
            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Users;"));
            Assert.Equal(1, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SystemSecuritySettings;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static readonly DateTime UtcNow = new(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc);

    private static SuperuserBootstrapEngine CreateEngine() =>
        new(new BcryptPasswordHasher(10));

    private static async Task<SqlTestDatabase> CreateFreshDatabaseAsync()
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

    private sealed class BindingRow
    {
        public int UserId { get; set; }
        public int SuperuserUserId { get; set; }
        public string NormalizedUsername { get; set; } = string.Empty;
        public string BoundNormalizedUsername { get; set; } = string.Empty;
    }
}
