using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Identity;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests
{
    [Collection(ApplicationSqlCollectionFixture.CollectionName)]
    public sealed class IdentityStoreIntegrationTests
    {
        private readonly ApplicationSqlCollectionFixture _fixture;
        private readonly ITestOutputHelper _output;

        public IdentityStoreIntegrationTests(ApplicationSqlCollectionFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
        public Task User_and_permission_stores_read_current_sql_state_and_replace_hash_atomically() =>
            _fixture.RunAsync(nameof(User_and_permission_stores_read_current_sql_state_and_replace_hash_atomically), _output, async test =>
            {
                var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                var userId = await InsertUserAsync(connection, "operator", "OPERATOR");
                var roleId = await connection.QuerySingleAsync<int>(
                    "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
                    "OUTPUT INSERTED.RoleId VALUES (N'Warehouse', N'WAREHOUSE');");
                await connection.ExecuteAsync(
                    "INSERT dbo.UserRoles (UserId, RoleId) VALUES (@userId, @roleId); " +
                    "INSERT dbo.RolePermissions (RoleId, PermissionKey) " +
                    "VALUES (@roleId, @permissionKey);",
                    new
                    {
                        userId,
                        roleId,
                        permissionKey = PermissionKeys.CatalogProductsWrite,
                    });

                var services = new ServiceCollection();
                services.AddSqlPersistence(test.ConnectionString);
                var provider = ApplicationSqlCollectionFixture.Track(test, services.BuildServiceProvider());
                var userStore = provider.GetRequiredService<IUserStore>();
                var permissionStore = provider.GetRequiredService<IPermissionStore>();
                var accountValidator = provider.GetRequiredService<IUserAccountValidator>();

                var user = await userStore.FindByNormalizedUsernameAsync(
                    "OPERATOR",
                    CancellationToken.None);
                Assert.NotNull(user);
                Assert.Equal(userId, user!.UserId);
                Assert.True(await accountValidator.IsActiveAsync(
                    userId,
                    CancellationToken.None));
                var initialPermission = await permissionStore.GetSnapshotAsync(
                    userId,
                    PermissionKeys.CatalogProductsWrite,
                    CancellationToken.None);
                Assert.Null(initialPermission.DirectDecision);
                Assert.True(initialPermission.HasRoleGrant);

                await connection.ExecuteAsync(
                    "INSERT dbo.UserPermissions (UserId, PermissionKey, Granted) " +
                    "VALUES (@userId, @permissionKey, 0);",
                    new { userId, permissionKey = PermissionKeys.CatalogProductsWrite });
                var overriddenPermission = await permissionStore.GetSnapshotAsync(
                    userId,
                    PermissionKeys.CatalogProductsWrite,
                    CancellationToken.None);
                Assert.False(overriddenPermission.DirectDecision);
                Assert.True(overriddenPermission.HasRoleGrant);

                var replacement = new PasswordReplacement(
                    userId,
                    "old-hash",
                    "BCrypt",
                    "new-hash",
                    string.Empty,
                    "BCrypt",
                    new DateTime(2026, 10, 9, 6, 30, 0, DateTimeKind.Utc));
                Assert.True(await userStore.TryReplacePasswordAsync(replacement, CancellationToken.None));
                Assert.False(await userStore.TryReplacePasswordAsync(replacement, CancellationToken.None));
                var storedCredential = await connection.QuerySingleAsync<(string Hash, string Salt)>(
                    "SELECT PasswordHash AS Hash, PasswordSalt AS Salt " +
                    "FROM dbo.Users WHERE UserId = @userId;",
                    new { userId });
                Assert.Equal("new-hash", storedCredential.Hash);
                Assert.Equal(string.Empty, storedCredential.Salt);

                await connection.ExecuteAsync(
                    "UPDATE dbo.Users SET IsActive = 0 WHERE UserId = @userId;",
                    new { userId });
                Assert.False(await accountValidator.IsActiveAsync(
                    userId,
                    CancellationToken.None));
                var inactivePermission = await permissionStore.GetSnapshotAsync(
                    userId,
                    PermissionKeys.CatalogProductsWrite,
                    CancellationToken.None);
                Assert.Null(inactivePermission.DirectDecision);
                Assert.False(inactivePermission.HasRoleGrant);
            });

        [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
        public Task Identity_unique_relationship_constraints_reject_duplicates() =>
            _fixture.RunAsync(nameof(Identity_unique_relationship_constraints_reject_duplicates), _output, async test =>
            {
                var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                var userId = await InsertUserAsync(connection, "operator", "OPERATOR");
                var roleId = await connection.QuerySingleAsync<int>(
                    "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
                    "OUTPUT INSERTED.RoleId VALUES (N'Warehouse', N'WAREHOUSE');");
                await connection.ExecuteAsync(
                    "INSERT dbo.UserRoles (UserId, RoleId) VALUES (@userId, @roleId); " +
                    "INSERT dbo.RolePermissions (RoleId, PermissionKey) VALUES (@roleId, N'Catalog.Products.Read'); " +
                    "INSERT dbo.UserPermissions (UserId, PermissionKey, Granted) VALUES (@userId, N'Catalog.Products.Read', 1);",
                    new { userId, roleId });

                await AssertDuplicateAsync(
                    connection.ExecuteAsync(
                        "INSERT dbo.UserRoles (UserId, RoleId) VALUES (@userId, @roleId);",
                        new { userId, roleId }));
                await AssertDuplicateAsync(
                    connection.ExecuteAsync(
                        "INSERT dbo.RolePermissions (RoleId, PermissionKey) VALUES (@roleId, N'Catalog.Products.Read');",
                        new { roleId }));
                await AssertDuplicateAsync(
                    connection.ExecuteAsync(
                        "INSERT dbo.UserPermissions (UserId, PermissionKey, Granted) VALUES (@userId, N'Catalog.Products.Read', 0);",
                        new { userId }));
                await AssertDuplicateAsync(
                    connection.ExecuteAsync(
                        "INSERT dbo.Roles (RoleName, NormalizedRoleName) VALUES (N'warehouse', N'WAREHOUSE');"));
            });

        [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
        public Task Login_with_an_outdated_bcrypt_hash_rehashes_through_the_sql_store() =>
            _fixture.RunAsync(nameof(Login_with_an_outdated_bcrypt_hash_rehashes_through_the_sql_store), _output, async test =>
            {
                const string password = "Maple-River-47-Cobalt!";
                var oldHash = new BcryptPasswordHasher(10).Hash(password);
                {
                    var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                    await connection.ExecuteAsync(
                        "INSERT dbo.Users " +
                        "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm) " +
                        "VALUES (N'operator', N'OPERATOR', N'Operator', N'Local', @oldHash, N'', N'BCrypt');",
                        new { oldHash });
                }

                var services = new ServiceCollection();
                services.AddServerApplication();
                services.AddSqlPersistence(test.ConnectionString);
                var provider = ApplicationSqlCollectionFixture.Track(test, services.BuildServiceProvider());

                var result = await provider.GetRequiredService<ISender>().Send(
                    new LoginCommand("operator", password),
                    CancellationToken.None);

                Assert.False(result.IsError);
                var verificationConnection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
                var newHash = await verificationConnection.QuerySingleAsync<string>(
                    "SELECT PasswordHash FROM dbo.Users WHERE NormalizedUsername = N'OPERATOR';");
                Assert.NotEqual(oldHash, newHash);
                Assert.True(new BcryptPasswordHasher().Verify(password, newHash));
            });

        [SqlServerFact(SqlTestCategory.SecurityProvisioning, SqlTestCategory.SqlConcurrency)]
        public async Task Permission_snapshot_never_exposes_role_grant_without_concurrent_direct_deny()
        {
            var database = await CreateMigratedDatabaseAsync();
            try
            {
                int userId;
                int roleId;
                using (var setupConnection = new SqlConnection(database.ConnectionString))
                {
                    userId = await InsertUserAsync(setupConnection, "operator", "OPERATOR");
                    roleId = await setupConnection.QuerySingleAsync<int>(
                        "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
                        "OUTPUT INSERTED.RoleId VALUES (N'Warehouse', N'WAREHOUSE');");
                }

                var services = new ServiceCollection();
                services.AddSqlPersistence(database.ConnectionString);
                using var provider = services.BuildServiceProvider();
                var permissionStore = provider.GetRequiredService<IPermissionStore>();

                using var adminConnection = new SqlConnection(database.ConnectionString);
                await adminConnection.OpenAsync();
                using var adminTransaction = adminConnection.BeginTransaction(IsolationLevel.Serializable);
                await adminConnection.ExecuteAsync(
                    "INSERT dbo.UserRoles (UserId, RoleId) VALUES (@userId, @roleId); " +
                    "INSERT dbo.RolePermissions (RoleId, PermissionKey) VALUES (@roleId, @permissionKey); " +
                    "INSERT dbo.UserPermissions (UserId, PermissionKey, Granted) VALUES (@userId, @permissionKey, 0);",
                    new
                    {
                        userId,
                        roleId,
                        permissionKey = PermissionKeys.CatalogProductsWrite,
                    },
                    adminTransaction);

                var snapshotTask = permissionStore.GetSnapshotAsync(
                    userId,
                    PermissionKeys.CatalogProductsWrite,
                    CancellationToken.None);
                await Task.Delay(150);
                Assert.False(snapshotTask.IsCompleted);

                adminTransaction.Commit();
                var completed = await Task.WhenAny(snapshotTask, Task.Delay(TimeSpan.FromSeconds(5)));
                Assert.Same(snapshotTask, completed);
                var snapshot = await snapshotTask;
                Assert.False(snapshot.DirectDecision);
                Assert.True(snapshot.HasRoleGrant);
            }
            finally
            {
                await database.DisposeAsync();
            }
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

        private static Task<int> InsertUserAsync(
            SqlConnection connection,
            string username,
            string normalizedUsername)
        {
            return connection.QuerySingleAsync<int>(
                "INSERT dbo.Users " +
                "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm) " +
                "OUTPUT INSERTED.UserId " +
                "VALUES (@username, @normalizedUsername, N'Operator', N'Local', N'old-hash', N'', N'BCrypt');",
                new { username, normalizedUsername });
        }

        private static async Task AssertDuplicateAsync(Task operation)
        {
            var exception = await Assert.ThrowsAsync<SqlException>(() => operation);
            Assert.Contains(exception.Number, new[] { 2601, 2627 });
        }
    }
}
