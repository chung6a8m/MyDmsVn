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
            Assert.Equal(5, first.Scripts.Count());
            Assert.Collection(
                first.Scripts.OrderBy(script => script.Name, StringComparer.Ordinal),
                script => Assert.EndsWith("001_PersistenceFoundation.sql", script.Name, StringComparison.Ordinal),
                script => Assert.EndsWith("002_Identity.sql", script.Name, StringComparison.Ordinal),
                script => Assert.EndsWith("003_Catalog.sql", script.Name, StringComparison.Ordinal),
                script => Assert.EndsWith("004_Catalog_Nul_Constraints.sql", script.Name, StringComparison.Ordinal),
                script => Assert.EndsWith("005_GoodsReceipt.sql", script.Name, StringComparison.Ordinal));
            Assert.True(second.Successful, second.Error?.ToString());
            Assert.Empty(second.Scripts);

            using var connection = new SqlConnection(database.ConnectionString);
            var tableCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.P1TestProbe');");
            var historyCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SchemaVersions;");
            Assert.Equal(0, tableCount);
            Assert.Equal(5, historyCount);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Goods_receipt_migration_enforces_aggregate_keys_checks_and_rowversion()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            using var connection = new SqlConnection(database.ConnectionString);

            var tableCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'GoodsReceipts', N'GoodsReceiptLines');");
            Assert.Equal(2, tableCount);

            var requiredIndexCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.indexes WHERE is_unique = 1 AND name IN " +
                "(N'UX_GoodsReceipts_ReceiptNo', N'UX_GoodsReceiptLines_ReceiptId_LineNo', " +
                "N'UX_GoodsReceiptLines_ReceiptId_ProductId');");
            Assert.Equal(3, requiredIndexCount);

            var rowVersionCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.GoodsReceipts') " +
                "AND name = N'RowVersion' AND TYPE_NAME(user_type_id) = N'timestamp' AND is_nullable = 0;");
            Assert.Equal(1, rowVersionCount);

            var decimalColumnCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.GoodsReceiptLines') AND (" +
                "(name = N'Quantity' AND TYPE_NAME(user_type_id) = N'decimal' AND precision = 18 AND scale = 4) OR " +
                "(name = N'UnitCost' AND TYPE_NAME(user_type_id) = N'decimal' AND precision = 19 AND scale = 4));");
            Assert.Equal(2, decimalColumnCount);

            var requiredColumnCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.columns AS columns WHERE columns.is_nullable = 0 AND (" +
                "(columns.object_id = OBJECT_ID(N'dbo.GoodsReceipts') AND columns.name IN " +
                "(N'ReceiptNo', N'ReceiptDate', N'WarehouseId', N'EmployeeId', N'Status', " +
                "N'CreatedAtUtc', N'CreatedByUserId', N'RowVersion')) OR " +
                "(columns.object_id = OBJECT_ID(N'dbo.GoodsReceiptLines') AND columns.name IN " +
                "(N'ReceiptId', N'LineNo', N'ProductId', N'Quantity', N'UnitCost', " +
                "N'CreatedAtUtc', N'CreatedByUserId')));");
            Assert.Equal(15, requiredColumnCount);

            var foreignKeyCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN " +
                "(OBJECT_ID(N'dbo.GoodsReceipts'), OBJECT_ID(N'dbo.GoodsReceiptLines'));");
            Assert.Equal(9, foreignKeyCount);

            var checkConstraintCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN " +
                "(OBJECT_ID(N'dbo.GoodsReceipts'), OBJECT_ID(N'dbo.GoodsReceiptLines'));");
            Assert.Equal(6, checkConstraintCount);

            var userId = await connection.QuerySingleAsync<int>(
                "INSERT dbo.Users " +
                "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm, IsActive) " +
                "OUTPUT INSERTED.UserId VALUES (N'receipt-user', N'RECEIPT-USER', N'Receipt User', N'Local', N'hash', N'', N'BCrypt', 1);");
            var warehouseId = await connection.QuerySingleAsync<int>(
                "INSERT dbo.Warehouses (Code, Name) OUTPUT INSERTED.WarehouseId VALUES (N'WH-GR', N'Receipt Warehouse');");
            var employeeId = await connection.QuerySingleAsync<int>(
                "INSERT dbo.Employees (Code, Name) OUTPUT INSERTED.EmployeeId VALUES (N'EMP-GR', N'Receipt Employee');");
            var productId = await connection.QuerySingleAsync<int>(
                "INSERT dbo.Products (Code, Name, Unit) OUTPUT INSERTED.ProductId VALUES (N'PR-GR', N'Receipt Product', N'EA');");
            var secondProductId = await connection.QuerySingleAsync<int>(
                "INSERT dbo.Products (Code, Name, Unit) OUTPUT INSERTED.ProductId VALUES (N'PR-GR-2', N'Receipt Product 2', N'EA');");
            var receiptId = await connection.QuerySingleAsync<long>(
                "INSERT dbo.GoodsReceipts " +
                "(ReceiptNo, ReceiptDate, WarehouseId, EmployeeId, Status, CreatedByUserId) " +
                "OUTPUT INSERTED.ReceiptId VALUES (N'GR-TEST-001', '20261010', @warehouseId, @employeeId, 'Draft', @userId);",
                new { warehouseId, employeeId, userId });

            await connection.ExecuteAsync(
                "INSERT dbo.GoodsReceiptLines " +
                "(ReceiptId, [LineNo], ProductId, Quantity, UnitCost, CreatedByUserId) " +
                "VALUES (@receiptId, 1, @productId, 1.2500, 5.5000, @userId);",
                new { receiptId, productId, userId });

            var duplicateProduct = await Assert.ThrowsAsync<SqlException>(() =>
                connection.ExecuteAsync(
                    "INSERT dbo.GoodsReceiptLines " +
                    "(ReceiptId, [LineNo], ProductId, Quantity, UnitCost, CreatedByUserId) " +
                    "VALUES (@receiptId, 2, @productId, 2.0000, 6.0000, @userId);",
                    new { receiptId, productId, userId }));
            Assert.Contains(duplicateProduct.Number, new[] { 2601, 2627 });

            var duplicateReceiptNumber = await Assert.ThrowsAsync<SqlException>(() =>
                connection.ExecuteAsync(
                    "INSERT dbo.GoodsReceipts " +
                    "(ReceiptNo, ReceiptDate, WarehouseId, EmployeeId, Status, CreatedByUserId) " +
                    "VALUES (N'gr-test-001', '20261010', @warehouseId, @employeeId, 'Draft', @userId);",
                    new { warehouseId, employeeId, userId }));
            Assert.Contains(duplicateReceiptNumber.Number, new[] { 2601, 2627 });

            var duplicateLineNumber = await Assert.ThrowsAsync<SqlException>(() =>
                connection.ExecuteAsync(
                    "INSERT dbo.GoodsReceiptLines " +
                    "(ReceiptId, [LineNo], ProductId, Quantity, UnitCost, CreatedByUserId) " +
                    "VALUES (@receiptId, 1, @secondProductId, 2.0000, 6.0000, @userId);",
                    new { receiptId, secondProductId, userId }));
            Assert.Contains(duplicateLineNumber.Number, new[] { 2601, 2627 });

            var foreignKeyViolation = await Assert.ThrowsAsync<SqlException>(() =>
                connection.ExecuteAsync(
                    "INSERT dbo.GoodsReceiptLines " +
                    "(ReceiptId, [LineNo], ProductId, Quantity, UnitCost, CreatedByUserId) " +
                    "VALUES (@receiptId, 2, 2147483647, 2.0000, 6.0000, @userId);",
                    new { receiptId, userId }));
            Assert.Equal(547, foreignKeyViolation.Number);

            var notNullViolation = await Assert.ThrowsAsync<SqlException>(() =>
                connection.ExecuteAsync(
                    "INSERT dbo.GoodsReceiptLines " +
                    "(ReceiptId, [LineNo], ProductId, Quantity, UnitCost, CreatedByUserId) " +
                    "VALUES (@receiptId, 2, @secondProductId, NULL, 6.0000, @userId);",
                    new { receiptId, secondProductId, userId }));
            Assert.Equal(515, notNullViolation.Number);

            var invalidValues = new[]
            {
                    "UPDATE dbo.GoodsReceipts SET Status = 'Invalid' WHERE ReceiptId = @receiptId;",
                    "UPDATE dbo.GoodsReceiptLines SET [LineNo] = 0 WHERE ReceiptId = @receiptId;",
                    "UPDATE dbo.GoodsReceiptLines SET Quantity = 0 WHERE ReceiptId = @receiptId;",
                    "UPDATE dbo.GoodsReceiptLines SET UnitCost = -0.0001 WHERE ReceiptId = @receiptId;",
                };
            foreach (var invalidValue in invalidValues)
            {
                var violation = await Assert.ThrowsAsync<SqlException>(() =>
                    connection.ExecuteAsync(invalidValue, new { receiptId }));
                Assert.Equal(547, violation.Number);
            }

            var firstVersion = await connection.QuerySingleAsync<byte[]>(
                "SELECT RowVersion FROM dbo.GoodsReceipts WHERE ReceiptId = @receiptId;",
                new { receiptId });
            await connection.ExecuteAsync(
                "UPDATE dbo.GoodsReceipts SET Note = N'changed' WHERE ReceiptId = @receiptId;",
                new { receiptId });
            var secondVersion = await connection.QuerySingleAsync<byte[]>(
                "SELECT RowVersion FROM dbo.GoodsReceipts WHERE ReceiptId = @receiptId;",
                new { receiptId });
            Assert.NotEqual(firstVersion, secondVersion);
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

    [SqlServerFact]
    public async Task Catalog_required_text_rejects_display_whitespace_only_values()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            using var connection = new SqlConnection(database.ConnectionString);
            var invalidInserts = new[]
            {
                "INSERT dbo.Products (Code, Name, Unit) VALUES (NCHAR(9), N'Product', N'Each');",
                "INSERT dbo.Products (Code, Name, Unit) VALUES (N'P-NAME', NCHAR(10), N'Each');",
                "INSERT dbo.Products (Code, Name, Unit) VALUES (N'P-UNIT', N'Product', NCHAR(13));",
                "INSERT dbo.Warehouses (Code, Name) VALUES (NCHAR(11), N'Warehouse');",
                "INSERT dbo.Warehouses (Code, Name) VALUES (N'W-NAME', NCHAR(12));",
                "INSERT dbo.Employees (Code, Name) VALUES (NCHAR(13), N'Employee');",
                "INSERT dbo.Employees (Code, Name) VALUES (N'E-NAME', NCHAR(160));",
                "INSERT dbo.Customers (Code, Name) VALUES (NCHAR(160), N'Customer');",
                "INSERT dbo.Customers (Code, Name) VALUES (N'C-NAME', NCHAR(9) + NCHAR(10));",
            };

            foreach (var invalidInsert in invalidInserts)
            {
                var violation = await Assert.ThrowsAsync<SqlException>(
                    () => connection.ExecuteAsync(invalidInsert));
                Assert.Equal(547, violation.Number);
            }
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Catalog_searchable_text_rejects_nul_values_at_database_boundary()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            using var connection = new SqlConnection(database.ConnectionString);
            var invalidInserts = new[]
            {
                "INSERT dbo.Products (Code, Name, Unit) VALUES (N'P' + NCHAR(0), N'Product', N'Each');",
                "INSERT dbo.Products (Code, Name, Unit) VALUES (N'P-NAME', N'Product' + NCHAR(0), N'Each');",
                "INSERT dbo.Products (Code, Name, Unit) VALUES (N'P-UNIT', N'Product', N'Each' + NCHAR(0));",
                "INSERT dbo.Warehouses (Code, Name) VALUES (N'W' + NCHAR(0), N'Warehouse');",
                "INSERT dbo.Warehouses (Code, Name) VALUES (N'W-NAME', N'Warehouse' + NCHAR(0));",
                "INSERT dbo.Employees (Code, Name) VALUES (N'E' + NCHAR(0), N'Employee');",
                "INSERT dbo.Employees (Code, Name) VALUES (N'E-NAME', N'Employee' + NCHAR(0));",
                "INSERT dbo.Customers (Code, Name) VALUES (N'C' + NCHAR(0), N'Customer');",
                "INSERT dbo.Customers (Code, Name) VALUES (N'C-NAME', N'Customer' + NCHAR(0));",
            };

            foreach (var invalidInsert in invalidInserts)
            {
                var violation = await Assert.ThrowsAsync<SqlException>(
                    () => connection.ExecuteAsync(invalidInsert));
                Assert.Equal(547, violation.Number);
            }
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Every_catalog_code_is_unique_case_and_accent_insensitively()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            using var connection = new SqlConnection(database.ConnectionString);
            var catalogInserts = new[]
            {
                "INSERT dbo.Products (Code, Name, Unit) VALUES (@Code, @Name, @Unit);",
                "INSERT dbo.Warehouses (Code, Name) VALUES (@Code, @Name);",
                "INSERT dbo.Employees (Code, Name) VALUES (@Code, @Name);",
                "INSERT dbo.Customers (Code, Name) VALUES (@Code, @Name);",
            };

            foreach (var catalogInsert in catalogInserts)
            {
                await connection.ExecuteAsync(catalogInsert, new
                {
                    Code = "MÃ-01",
                    Name = "First row",
                    Unit = "Each-1",
                });
                var duplicate = await Assert.ThrowsAsync<SqlException>(
                    () => connection.ExecuteAsync(catalogInsert, new
                    {
                        Code = "ma-01",
                        Name = "Second row",
                        Unit = "Each-2",
                    }));
                Assert.Contains(duplicate.Number, new[] { 2601, 2627 });
            }
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Employee_user_link_allows_many_nulls_but_only_one_employee_per_user()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            using var connection = new SqlConnection(database.ConnectionString);
            var userId = await connection.QuerySingleAsync<int>(
                "INSERT dbo.Users " +
                "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, " +
                "PasswordAlgorithm, IsActive) " +
                "OUTPUT INSERTED.UserId " +
                "VALUES (N'catalog-user', N'CATALOG-USER', N'Catalog User', N'Local', N'hash', N'', " +
                "N'BCrypt', 1);");
            const string insertEmployee =
                "INSERT dbo.Employees (Code, Name, UserId) VALUES (@Code, @Name, @UserId);";

            await connection.ExecuteAsync(insertEmployee, new
            {
                Code = "NV-LINK-1",
                Name = "Linked Employee 1",
                UserId = (int?)userId,
            });
            var duplicateLink = await Assert.ThrowsAsync<SqlException>(() =>
                connection.ExecuteAsync(insertEmployee, new
                {
                    Code = "NV-LINK-2",
                    Name = "Linked Employee 2",
                    UserId = (int?)userId,
                }));
            Assert.Contains(duplicateLink.Number, new[] { 2601, 2627 });

            await connection.ExecuteAsync(insertEmployee, new
            {
                Code = "NV-NULL-1",
                Name = "Unlinked Employee 1",
                UserId = (int?)null,
            });
            await connection.ExecuteAsync(insertEmployee, new
            {
                Code = "NV-NULL-2",
                Name = "Unlinked Employee 2",
                UserId = (int?)null,
            });
            var unlinkedCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.Employees WHERE UserId IS NULL;");
            Assert.Equal(2, unlinkedCount);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }
}
