using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class DatabaseMigrationTests
{
    [Theory]
    [InlineData("Server=invalid.invalid;Database=master;User ID=test;Password=test;Connect Timeout=1;Encrypt=false")]
    [InlineData("Server=invalid.invalid;Database=model;User ID=test;Password=test;Connect Timeout=1;Encrypt=false")]
    [InlineData("Server=invalid.invalid;Database=msdb;User ID=test;Password=test;Connect Timeout=1;Encrypt=false")]
    [InlineData("Server=invalid.invalid;Database=tempdb;User ID=test;Password=test;Connect Timeout=1;Encrypt=false")]
    [InlineData("Server=invalid.invalid;User ID=test;Password=test;Connect Timeout=1;Encrypt=false")]
    public void System_or_unspecified_database_is_rejected_before_connecting(string connectionString)
    {
        Assert.Throws<ArgumentException>(() => new DatabaseMigrationRunner().Migrate(connectionString));
    }

    [SqlServerFact]
    public async Task Clean_migration_applies_once_and_repeat_is_a_no_op()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migrator = new DatabaseMigrationRunner();

            var first = migrator.Migrate(database.ConnectionString);
            var second = migrator.Migrate(database.ConnectionString);

            Assert.True(first.Successful, first.Error?.ToString());
            Assert.Equal(3, first.Scripts.Count());
            Assert.Collection(
                first.Scripts.OrderBy(script => script.Name, StringComparer.Ordinal),
                script => Assert.EndsWith("001_PersistenceFoundation.sql", script.Name, StringComparison.Ordinal),
                script => Assert.EndsWith("002_Identity.sql", script.Name, StringComparison.Ordinal),
                script => Assert.EndsWith("003_Catalog.sql", script.Name, StringComparison.Ordinal));
            Assert.True(second.Successful, second.Error?.ToString());
            Assert.Empty(second.Scripts);

            using var connection = new SqlConnection(database.ConnectionString);
            var tableCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.P1TestProbe');");
            var historyCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SchemaVersions;");
            Assert.Equal(0, tableCount);
            Assert.Equal(3, historyCount);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Identity_migration_creates_required_tables_and_enforces_unique_normalized_names()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);

            Assert.True(result.Successful, result.Error?.ToString());
            using var connection = new SqlConnection(database.ConnectionString);
            var identityTableCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE name IN " +
                "(N'Users', N'Roles', N'UserRoles', N'RolePermissions', N'UserPermissions');");
            Assert.Equal(5, identityTableCount);

            var credentialColumnCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users') " +
                "AND name IN (N'PasswordHash', N'PasswordSalt', N'PasswordAlgorithm');");
            Assert.Equal(3, credentialColumnCount);

            const string insertUser =
                "INSERT dbo.Users " +
                "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, " +
                "PasswordAlgorithm, IsActive, CreatedAtUtc) " +
                "VALUES (@Username, @NormalizedUsername, @DisplayName, N'Local', N'hash', N'', " +
                "N'BCrypt', 1, SYSUTCDATETIME());";
            await connection.ExecuteAsync(insertUser, new
            {
                Username = "operator",
                NormalizedUsername = "OPERATOR",
                DisplayName = "Operator",
            });

            var duplicate = await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync(insertUser, new
            {
                Username = "Operator",
                NormalizedUsername = "OPERATOR",
                DisplayName = "Duplicate",
            }));
            Assert.Contains(duplicate.Number, new[] { 2601, 2627 });
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Catalog_migration_creates_empty_tables_and_enforces_code_uniqueness_and_employee_user_fk()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);

            Assert.True(result.Successful, result.Error?.ToString());
            using var connection = new SqlConnection(database.ConnectionString);
            var catalogTableCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE name IN " +
                "(N'Products', N'Warehouses', N'Employees', N'Customers');");
            Assert.Equal(4, catalogTableCount);

            var seededRowCount = await connection.QuerySingleAsync<int>(
                "SELECT " +
                "(SELECT COUNT(*) FROM dbo.Products) + " +
                "(SELECT COUNT(*) FROM dbo.Warehouses) + " +
                "(SELECT COUNT(*) FROM dbo.Employees) + " +
                "(SELECT COUNT(*) FROM dbo.Customers);");
            Assert.Equal(0, seededRowCount);

            var uniqueCodeIndexCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.indexes WHERE is_unique = 1 AND name IN " +
                "(N'UX_Products_Code', N'UX_Warehouses_Code', N'UX_Employees_Code', N'UX_Customers_Code');");
            Assert.Equal(4, uniqueCodeIndexCount);

            var auditColumnCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.columns AS columns " +
                "INNER JOIN sys.tables AS tables ON tables.object_id = columns.object_id " +
                "WHERE tables.name IN (N'Products', N'Warehouses', N'Employees', N'Customers') AND (" +
                "(columns.name = N'CreatedAtUtc' AND TYPE_NAME(columns.user_type_id) = N'datetime2' " +
                "AND columns.scale = 7 AND columns.is_nullable = 0) OR " +
                "(columns.name = N'CreatedByUserId' AND TYPE_NAME(columns.user_type_id) = N'int' " +
                "AND columns.is_nullable = 1) OR " +
                "(columns.name = N'UpdatedAtUtc' AND TYPE_NAME(columns.user_type_id) = N'datetime2' " +
                "AND columns.scale = 7 AND columns.is_nullable = 1) OR " +
                "(columns.name = N'UpdatedByUserId' AND TYPE_NAME(columns.user_type_id) = N'int' " +
                "AND columns.is_nullable = 1));");
            Assert.Equal(16, auditColumnCount);

            var catalogSpecificTypeCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.columns AS columns " +
                "INNER JOIN sys.tables AS tables ON tables.object_id = columns.object_id " +
                "WHERE " +
                "(tables.name = N'Products' AND columns.name = N'Unit' " +
                "AND TYPE_NAME(columns.user_type_id) = N'nvarchar' AND columns.max_length = 64) OR " +
                "(tables.name = N'Warehouses' AND columns.name = N'Address' " +
                "AND TYPE_NAME(columns.user_type_id) = N'nvarchar' AND columns.max_length = 1000 " +
                "AND columns.is_nullable = 1) OR " +
                "(tables.name = N'Employees' AND columns.name = N'UserId' " +
                "AND TYPE_NAME(columns.user_type_id) = N'int' AND columns.is_nullable = 1) OR " +
                "(tables.name = N'Customers' AND columns.name = N'TaxCode' " +
                "AND TYPE_NAME(columns.user_type_id) = N'nvarchar' AND columns.max_length = 64 " +
                "AND columns.is_nullable = 1);");
            Assert.Equal(4, catalogSpecificTypeCount);

            const string insertProduct =
                "INSERT dbo.Products (Code, Name, Unit, IsActive, CreatedByUserId) " +
                "VALUES (@Code, @Name, @Unit, 1, NULL);";
            await connection.ExecuteAsync(insertProduct, new
            {
                Code = "SP001",
                Name = "San pham 1",
                Unit = "Cai",
            });
            var duplicate = await Assert.ThrowsAsync<SqlException>(() =>
                connection.ExecuteAsync(insertProduct, new
                {
                    Code = "sp001",
                    Name = "San pham trung",
                    Unit = "Cai",
                }));
            Assert.Contains(duplicate.Number, new[] { 2601, 2627 });

            await connection.ExecuteAsync(
                "INSERT dbo.Employees (Code, Name, UserId, IsActive, CreatedByUserId) " +
                "VALUES (N'NV001', N'Nhan vien 1', NULL, 1, NULL);");
            var foreignKeyViolation = await Assert.ThrowsAsync<SqlException>(() =>
                connection.ExecuteAsync(
                    "INSERT dbo.Employees (Code, Name, UserId, IsActive, CreatedByUserId) " +
                    "VALUES (N'NV002', N'Nhan vien 2', 2147483647, 1, NULL);"));
            Assert.Equal(547, foreignKeyViolation.Number);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }
}
