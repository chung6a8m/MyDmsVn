using System;
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
using MyDmsVn.Server.Infrastructure.Identity;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests
{
    public sealed class IdentityStoreIntegrationTests
    {
        [SqlServerFact]
        public async Task User_and_permission_stores_read_current_sql_state_and_replace_hash_atomically()
        {
            var database = await CreateMigratedDatabaseAsync();
            try
            {
                using var connection = new SqlConnection(database.ConnectionString);
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
                services.AddSqlPersistence(database.ConnectionString);
                using var provider = services.BuildServiceProvider();
                var userStore = provider.GetRequiredService<IUserStore>();
                var permissionStore = provider.GetRequiredService<IPermissionStore>();

                var user = await userStore.FindByNormalizedUsernameAsync(
                    "OPERATOR",
                    CancellationToken.None);
                Assert.NotNull(user);
                Assert.Equal(userId, user!.UserId);
                Assert.Null(await permissionStore.GetDirectDecisionAsync(
                    userId,
                    PermissionKeys.CatalogProductsWrite,
                    CancellationToken.None));
                Assert.True(await permissionStore.HasRoleGrantAsync(
                    userId,
                    PermissionKeys.CatalogProductsWrite,
                    CancellationToken.None));

                await connection.ExecuteAsync(
                    "INSERT dbo.UserPermissions (UserId, PermissionKey, Granted) " +
                    "VALUES (@userId, @permissionKey, 0);",
                    new { userId, permissionKey = PermissionKeys.CatalogProductsWrite });
                Assert.False(await permissionStore.GetDirectDecisionAsync(
                    userId,
                    PermissionKeys.CatalogProductsWrite,
                    CancellationToken.None));

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
                Assert.Null(await permissionStore.GetDirectDecisionAsync(
                    userId,
                    PermissionKeys.CatalogProductsWrite,
                    CancellationToken.None));
                Assert.False(await permissionStore.HasRoleGrantAsync(
                    userId,
                    PermissionKeys.CatalogProductsWrite,
                    CancellationToken.None));
            }
            finally
            {
                await database.DisposeAsync();
            }
        }

        [SqlServerFact]
        public async Task Identity_unique_relationship_constraints_reject_duplicates()
        {
            var database = await CreateMigratedDatabaseAsync();
            try
            {
                using var connection = new SqlConnection(database.ConnectionString);
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
            }
            finally
            {
                await database.DisposeAsync();
            }
        }

        [SqlServerFact]
        public async Task Login_with_an_outdated_bcrypt_hash_rehashes_through_the_sql_store()
        {
            var database = await CreateMigratedDatabaseAsync();
            try
            {
                const string password = "Maple-River-47-Cobalt!";
                var oldHash = new BcryptPasswordHasher(10).Hash(password);
                using (var connection = new SqlConnection(database.ConnectionString))
                {
                    await connection.ExecuteAsync(
                        "INSERT dbo.Users " +
                        "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm) " +
                        "VALUES (N'operator', N'OPERATOR', N'Operator', N'Local', @oldHash, N'', N'BCrypt');",
                        new { oldHash });
                }

                var services = new ServiceCollection();
                services.AddServerApplication();
                services.AddSqlPersistence(database.ConnectionString);
                using var provider = services.BuildServiceProvider();

                var result = await provider.GetRequiredService<ISender>().Send(
                    new LoginCommand("operator", password),
                    CancellationToken.None);

                Assert.False(result.IsError);
                using var verificationConnection = new SqlConnection(database.ConnectionString);
                var newHash = await verificationConnection.QuerySingleAsync<string>(
                    "SELECT PasswordHash FROM dbo.Users WHERE NormalizedUsername = N'OPERATOR';");
                Assert.NotEqual(oldHash, newHash);
                Assert.True(new BcryptPasswordHasher().Verify(password, newHash));
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
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            return database;
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
