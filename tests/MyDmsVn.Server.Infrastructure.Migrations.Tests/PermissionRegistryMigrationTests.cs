using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DbUp;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class PermissionRegistryMigrationTests
{
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Upgrade_backfills_historical_assignments_as_non_authorizing_drift()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var assembly = typeof(DatabaseMigrationRunner).Assembly;
            var preP31 = DeployChanges.To
                .SqlDatabase(database.ConnectionString)
                .WithScriptsEmbeddedInAssembly(
                    assembly,
                    name => !name.EndsWith(
                        "008_Permission_Provisioning.sql",
                        StringComparison.Ordinal))
                .JournalToSqlTable("dbo", "SchemaVersions")
                .Build()
                .PerformUpgrade();
            Assert.True(preP31.Successful, preP31.Error?.ToString());

            int userId;
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                userId = await connection.QuerySingleAsync<int>(
                    "INSERT dbo.Users " +
                    "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm) " +
                    "OUTPUT INSERTED.UserId VALUES " +
                    "(N'legacy', N'LEGACY', N'Legacy', N'Local', N'hash', N'', N'BCrypt');");
                var roleId = await connection.QuerySingleAsync<int>(
                    "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
                    "OUTPUT INSERTED.RoleId VALUES (N'Legacy', N'LEGACY');");
                await connection.ExecuteAsync(
                    "INSERT dbo.UserRoles (UserId, RoleId) VALUES (@userId, @roleId); " +
                    "INSERT dbo.RolePermissions (RoleId, PermissionKey) " +
                    "VALUES (@roleId, N'Legacy.Unknown.Read');",
                    new { userId, roleId });
            }

            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());
            Assert.Single(
                migration.Scripts,
                script => script.Name.EndsWith(
                    "008_Permission_Provisioning.sql",
                    StringComparison.Ordinal));

            using var verification = new SqlConnection(database.ConnectionString);
            var row = await verification.QuerySingleAsync<PermissionRow>(
                "SELECT PermissionKey, Module, CatalogStatus " +
                "FROM dbo.Permissions WHERE PermissionKey = N'Legacy.Unknown.Read';");
            Assert.Equal("Legacy.Unknown.Read", row.PermissionKey);
            Assert.Equal("Legacy", row.Module);
            Assert.Equal("Unregistered", row.CatalogStatus);

            var services = new ServiceCollection();
            services.AddSqlPersistence(database.ConnectionString);
            using var provider = services.BuildServiceProvider();
            var snapshot = await provider.GetRequiredService<IPermissionStore>()
                .GetSnapshotAsync(userId, "Legacy.Unknown.Read", CancellationToken.None);
            Assert.False(snapshot.IsCatalogActive);
            Assert.True(snapshot.HasRoleGrant);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private sealed class PermissionRow
    {
        public string PermissionKey { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string CatalogStatus { get; set; } = string.Empty;
    }
}
