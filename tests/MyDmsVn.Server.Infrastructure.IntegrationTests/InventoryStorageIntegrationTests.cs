using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.DbMigrator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class InventoryStorageIntegrationTests
{
    [SqlServerFact]
    public async Task Inventory_migration_enforces_ledger_and_balance_storage_invariants()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();

            var tableCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'StockLedger', N'StockBalances');");
            Assert.Equal(2, tableCount);

            var sourceIdentityIndex = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockLedger') " +
                "AND name = N'UX_StockLedger_DocumentType_DocumentLineId' AND is_unique = 1;");
            Assert.Equal(1, sourceIdentityIndex);

            var stockCardIndex = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockLedger') " +
                "AND name = N'IX_StockLedger_WarehouseId_ProductId_PostedAtUtc_LedgerId';");
            Assert.Equal(1, stockCardIndex);

            var balanceShape = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.StockBalances') AND (" +
                "(name = N'Quantity' AND TYPE_NAME(user_type_id) = N'decimal' AND precision = 18 AND scale = 4 AND is_nullable = 0) OR " +
                "(name = N'RowVersion' AND TYPE_NAME(user_type_id) = N'timestamp' AND is_nullable = 0));");
            Assert.Equal(2, balanceShape);

            var primaryKeyColumns = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.index_columns ic " +
                "INNER JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id " +
                "WHERE i.object_id = OBJECT_ID(N'dbo.StockBalances') AND i.is_primary_key = 1;");
            Assert.Equal(2, primaryKeyColumns);

            var ids = await SeedReferencesAsync(connection);
            await connection.ExecuteAsync(
                "INSERT dbo.StockLedger " +
                "(WarehouseId, ProductId, DocumentType, DocumentId, DocumentLineId, QuantityChange, PostedAtUtc, PostedByUserId) " +
                "VALUES (@WarehouseId, @ProductId, 'GoodsReceipt', 100, 1001, 2.5000, SYSUTCDATETIME(), @UserId);",
                new { ids.WarehouseId, ProductId = ids.ProductIds[0], ids.UserId });

            var duplicate = await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync(
                "INSERT dbo.StockLedger " +
                "(WarehouseId, ProductId, DocumentType, DocumentId, DocumentLineId, QuantityChange, PostedAtUtc, PostedByUserId) " +
                "VALUES (@WarehouseId, @ProductId, 'GoodsReceipt', 101, 1001, 1.0000, SYSUTCDATETIME(), @UserId);",
                new { ids.WarehouseId, ProductId = ids.ProductIds[1], ids.UserId }));
            Assert.Contains(duplicate.Number, new[] { 2601, 2627 });

            var update = await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync(
                "UPDATE dbo.StockLedger SET QuantityChange = 9.0000 WHERE DocumentLineId = 1001;"));
            Assert.Equal(51000, update.Number);

            var delete = await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync(
                "DELETE dbo.StockLedger WHERE DocumentLineId = 1001;"));
            Assert.Equal(51000, delete.Number);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Reconciliation_flags_missing_keys_on_either_side_and_quantity_mismatch()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.OpenAsync();
                var ids = await SeedReferencesAsync(connection);
                await connection.ExecuteAsync(
                    "INSERT dbo.StockLedger " +
                    "(WarehouseId, ProductId, DocumentType, DocumentId, DocumentLineId, QuantityChange, PostedAtUtc, PostedByUserId) VALUES " +
                    "(@WarehouseId, @LedgerOnlyProductId, 'GoodsReceipt', 200, 2001, 5.0000, SYSUTCDATETIME(), @UserId), " +
                    "(@WarehouseId, @MismatchProductId, 'GoodsReceipt', 201, 2002, 7.0000, SYSUTCDATETIME(), @UserId); " +
                    "INSERT dbo.StockBalances (WarehouseId, ProductId, Quantity) VALUES " +
                    "(@WarehouseId, @ZeroBalanceOnlyProductId, 0.0000), " +
                    "(@WarehouseId, @NonzeroBalanceOnlyProductId, 3.0000), " +
                    "(@WarehouseId, @MismatchProductId, 6.0000);",
                    new
                    {
                        ids.WarehouseId,
                        LedgerOnlyProductId = ids.ProductIds[0],
                        ZeroBalanceOnlyProductId = ids.ProductIds[1],
                        NonzeroBalanceOnlyProductId = ids.ProductIds[2],
                        MismatchProductId = ids.ProductIds[3],
                        ids.UserId,
                    });
            }

            var services = new ServiceCollection();
            services.AddSqlPersistence(database.ConnectionString);
            using var provider = services.BuildServiceProvider();
            var differences = await provider
                .GetRequiredService<IStockReconciliationQueryService>()
                .FindDifferencesAsync(CancellationToken.None);

            Assert.Equal(4, differences.Count);
            Assert.Contains(differences, difference =>
                difference.HasLedger && !difference.HasBalance && difference.LedgerQuantity == 5.0000m);
            Assert.Contains(differences, difference =>
                !difference.HasLedger && difference.HasBalance && difference.BalanceQuantity == 0.0000m);
            Assert.Contains(differences, difference =>
                !difference.HasLedger && difference.HasBalance && difference.BalanceQuantity == 3.0000m);
            Assert.Contains(differences, difference =>
                difference.HasLedger && difference.HasBalance &&
                difference.LedgerQuantity == 7.0000m && difference.BalanceQuantity == 6.0000m);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task<SqlTestDatabase> CreateDatabaseAsync()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
        Assert.True(migration.Successful, migration.Error?.ToString());
        return database;
    }

    private static async Task<ReferenceIds> SeedReferencesAsync(SqlConnection connection)
    {
        var userId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Users " +
            "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm, IsActive) " +
            "OUTPUT INSERTED.UserId VALUES (N'inventory-user', N'INVENTORY-USER', N'Inventory User', N'Local', N'hash', N'', N'BCrypt', 1);");
        var warehouseId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Warehouses (Code, Name) OUTPUT INSERTED.WarehouseId VALUES (N'WH-STOCK', N'Stock Warehouse');");
        var productIds = new int[4];
        for (var index = 0; index < productIds.Length; index++)
        {
            productIds[index] = await connection.QuerySingleAsync<int>(
                "INSERT dbo.Products (Code, Name, Unit) OUTPUT INSERTED.ProductId " +
                "VALUES (@Code, @Name, N'EA');",
                new { Code = "PR-STOCK-" + index, Name = "Stock Product " + index });
        }

        return new ReferenceIds(userId, warehouseId, productIds);
    }

    private sealed class ReferenceIds
    {
        public ReferenceIds(int userId, int warehouseId, int[] productIds)
        {
            UserId = userId;
            WarehouseId = warehouseId;
            ProductIds = productIds;
        }

        public int UserId { get; }
        public int WarehouseId { get; }
        public int[] ProductIds { get; }
    }
}
