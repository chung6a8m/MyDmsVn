using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.DbMigrator.SecurityProvisioning;
using MyDmsVn.Server.Infrastructure;
using MyDmsVn.Server.Infrastructure.Identity;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class DesktopLoginSessionIntegrationTests
{
    private const string AdministratorPassword = "Bootstrap!Pass456";
    private const string OperatorPassword = "Operator!Pass456";
    private static readonly DateTime UtcNow =
        new DateTime(2026, 10, 15, 3, 0, 0, DateTimeKind.Utc);

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Local_desktop_session_uses_live_sql_identity_and_permission_state()
    {
        var database = await CreateProvisionedDatabaseAsync();
        try
        {
            var services = new ServiceCollection();
            services.AddServerApplication("first.admin");
            services.AddSqlPersistence(database.ConnectionString);
            services.AddLocalDesktopAdapter();
            using var provider = services.BuildServiceProvider();
            var identity = provider.GetRequiredService<IIdentityApiClient>();
            var products = provider.GetRequiredService<IProductApiClient>();
            var session = provider.GetRequiredService<IDesktopSession>();

            var anonymous = await products.ListAsync(
                new CatalogListRequest(null, 1, 20, false),
                CancellationToken.None);
            Assert.False(anonymous.IsSuccess);
            Assert.Equal(ApiStatusCode.Unauthorized, anonymous.Error!.Status);

            var incorrect = await identity.LoginAsync(
                new LoginRequest("first.admin", "incorrect-password"),
                CancellationToken.None);
            Assert.False(incorrect.IsSuccess);
            Assert.Equal("Auth.InvalidCredentials", incorrect.Error!.Code);
            Assert.False(session.IsAuthenticated);

            var administrator = await identity.LoginAsync(
                new LoginRequest("first.admin", AdministratorPassword),
                CancellationToken.None);
            Assert.True(administrator.IsSuccess);
            Assert.Equal("first.admin", session.CurrentUser!.Username);
            var currentAdministrator = await identity.GetCurrentUserAsync(CancellationToken.None);
            Assert.True(currentAdministrator.IsSuccess);
            Assert.Equal(administrator.Data!.UserId, currentAdministrator.Data!.UserId);

            var initiallyAllowed = await products.ListAsync(
                new CatalogListRequest(null, 1, 20, false),
                CancellationToken.None);
            Assert.True(initiallyAllowed.IsSuccess);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "INSERT dbo.UserPermissions (UserId, PermissionKey, Granted) " +
                    "VALUES (@userId, @permissionKey, 0);",
                    new
                    {
                        userId = administrator.Data.UserId,
                        permissionKey = PermissionKeys.CatalogProductsRead,
                    });
            }

            var superuserDeniedGrant = await products.ListAsync(
                new CatalogListRequest(null, 1, 20, false),
                CancellationToken.None);
            Assert.True(superuserDeniedGrant.IsSuccess);
            Assert.True(session.IsAuthenticated);

            int operatorId;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                operatorId = await connection.QuerySingleAsync<int>(
                    "INSERT dbo.Users " +
                    "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm) " +
                    "OUTPUT INSERTED.UserId " +
                    "VALUES (N'operator', N'OPERATOR', N'Warehouse Operator', N'Local', @hash, N'', N'BCrypt');",
                    new { hash = new BcryptPasswordHasher(10).Hash(OperatorPassword) });
            }

            session.SignOut();
            Assert.False(session.IsAuthenticated);
            var operatorLogin = await identity.LoginAsync(
                new LoginRequest("operator", OperatorPassword),
                CancellationToken.None);
            Assert.True(operatorLogin.IsSuccess);
            Assert.Equal(operatorId, operatorLogin.Data!.UserId);
            Assert.Equal("operator", session.CurrentUser!.Username);

            var unassignedOperator = await products.ListAsync(
                new CatalogListRequest(null, 1, 20, false),
                CancellationToken.None);
            Assert.False(unassignedOperator.IsSuccess);
            Assert.Equal(ApiStatusCode.Forbidden, unassignedOperator.Error!.Status);

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "UPDATE dbo.Users SET IsActive = 0 WHERE UserId = @operatorId;",
                    new { operatorId });
            }

            var disabled = await identity.GetCurrentUserAsync(CancellationToken.None);
            Assert.False(disabled.IsSuccess);
            Assert.Equal(ApiStatusCode.Unauthorized, disabled.Error!.Status);
            Assert.False(session.IsAuthenticated);
            Assert.Equal(DesktopSessionState.Expired, session.State);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task<SqlTestDatabase> CreateProvisionedDatabaseAsync()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());

            var engine = new SuperuserBootstrapEngine(new BcryptPasswordHasher(10));
            var userId = await engine.BootstrapAsync(
                database.ConnectionString,
                "first.admin",
                "First Administrator",
                AdministratorPassword,
                UtcNow,
                CancellationToken.None);
            Assert.True(userId > 0);
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
}
