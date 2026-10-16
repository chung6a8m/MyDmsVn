using System;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.DbMigrator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class InventoryQueryIntegrationTests
{
    [SqlServerFact]
    public async Task Stock_card_query_preserves_datetime2_boundaries()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            var lastTickOfSecond = new DateTime(
                2026,
                10,
                10,
                3,
                0,
                0,
                DateTimeKind.Utc).AddSeconds(1).AddTicks(-1);
            await InsertBoundaryMovementsAsync(
                database.ConnectionString,
                seed.ArchivedWarehouseId,
                seed.ArchivedProductId,
                seed.UserId,
                lastTickOfSecond);

            var services = new ServiceCollection();
            services.AddSqlPersistence(database.ConnectionString);
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IInventoryQueryService>();

            var result = await queries.GetStockCardAsync(
                new StockCardQuery(
                    seed.ArchivedWarehouseId,
                    seed.ArchivedProductId,
                    lastTickOfSecond,
                    lastTickOfSecond,
                    1,
                    25),
                CancellationToken.None);

            var movement = Assert.Single(result.Items);
            Assert.Equal(lastTickOfSecond, movement.PostedAtUtc);
            Assert.Equal(1.0000m, movement.QuantityChange);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Balance_and_stock_card_queries_filter_page_sort_and_keep_inactive_history()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            var services = new ServiceCollection();
            services.AddSqlPersistence(database.ConnectionString);
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IInventoryQueryService>();

            var firstBalancePage = await queries.GetStockBalancesAsync(
                new StockBalanceQuery(null, null, null, 1, 1),
                CancellationToken.None);
            var inactiveBalance = await queries.GetStockBalancesAsync(
                new StockBalanceQuery(
                    seed.ArchivedWarehouseId,
                    seed.ArchivedProductId,
                    "archived",
                    1,
                    25),
                CancellationToken.None);
            var firstCardPage = await queries.GetStockCardAsync(
                new StockCardQuery(
                    seed.ArchivedWarehouseId,
                    seed.ArchivedProductId,
                    seed.FirstPostedAtUtc,
                    seed.SecondPostedAtUtc.AddMilliseconds(-500).AddSeconds(1).AddTicks(-1),
                    1,
                    1),
                CancellationToken.None);
            var secondCardPage = await queries.GetStockCardAsync(
                new StockCardQuery(
                    seed.ArchivedWarehouseId,
                    seed.ArchivedProductId,
                    seed.FirstPostedAtUtc,
                    seed.SecondPostedAtUtc.AddMilliseconds(-500).AddSeconds(1).AddTicks(-1),
                    2,
                    1),
                CancellationToken.None);

            Assert.Equal(2, firstBalancePage.TotalCount);
            Assert.Single(firstBalancePage.Items);
            Assert.Equal("P-ARCHIVED", firstBalancePage.Items[0].ProductCode);

            var balance = Assert.Single(inactiveBalance.Items);
            Assert.Equal(12.5000m, balance.Quantity);
            Assert.Equal("WH-ARCHIVED", balance.WarehouseCode);
            Assert.Equal("Archived warehouse", balance.WarehouseName);
            Assert.False(balance.WarehouseIsActive);
            Assert.Equal("P-ARCHIVED", balance.ProductCode);
            Assert.False(balance.ProductIsActive);

            Assert.Equal(2, firstCardPage.TotalCount);
            var firstMovement = Assert.Single(firstCardPage.Items);
            var secondMovement = Assert.Single(secondCardPage.Items);
            Assert.True(firstMovement.LedgerId < secondMovement.LedgerId);
            Assert.Equal("GR0000000201", firstMovement.DocumentNumber);
            Assert.Equal(seed.FirstReceiptId, firstMovement.DocumentId);
            Assert.Equal("GR0000000202", secondMovement.DocumentNumber);
            Assert.Equal(seed.SecondReceiptId, secondMovement.DocumentId);
            Assert.False(firstMovement.WarehouseIsActive);
            Assert.False(firstMovement.ProductIsActive);
            Assert.Equal(new[] { 5.0000m, 7.5000m },
                new[] { firstMovement.QuantityChange, secondMovement.QuantityChange });
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task<SqlTestDatabase> CreateDatabaseAsync(
        [CallerMemberName] string testCaseId = "")
    {
        var measurement = SqlTestDatabase.CreateMeasurementContextForTest(testCaseId);
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!,
            measurement);
        var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
        Assert.True(migration.Successful, migration.Error?.ToString());
        return database;
    }

    private static async Task<SeedData> SeedAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        var userId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Users " +
            "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm, IsActive) " +
            "OUTPUT INSERTED.UserId VALUES (N'inventory-query-user', N'INVENTORY-QUERY-USER', " +
            "N'Inventory Query User', N'Local', N'hash', N'', N'BCrypt', 1);");
        var warehouseId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Warehouses (Code, Name) OUTPUT INSERTED.WarehouseId " +
            "VALUES (N'WH-ARCHIVED', N'Archived warehouse');");
        var otherWarehouseId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Warehouses (Code, Name) OUTPUT INSERTED.WarehouseId " +
            "VALUES (N'WH-Z', N'Other warehouse');");
        var employeeId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Employees (Code, Name) OUTPUT INSERTED.EmployeeId " +
            "VALUES (N'EMP-INVENTORY', N'Inventory operator');");
        var productId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Products (Code, Name, Unit) OUTPUT INSERTED.ProductId " +
            "VALUES (N'P-ARCHIVED', N'Archived product', N'EA');");
        var otherProductId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Products (Code, Name, Unit) OUTPUT INSERTED.ProductId " +
            "VALUES (N'P-Z', N'Other product', N'EA');");
        var firstPosted = new DateTime(2026, 10, 10, 1, 0, 0, DateTimeKind.Utc);
        var secondPosted = new DateTime(2026, 10, 10, 2, 0, 0, DateTimeKind.Utc)
            .AddMilliseconds(500);
        var firstReceiptId = await InsertPostedReceiptAsync(
            connection, "GR0000000201", warehouseId, employeeId, userId, firstPosted);
        var secondReceiptId = await InsertPostedReceiptAsync(
            connection, "GR0000000202", warehouseId, employeeId, userId, secondPosted);
        var firstLineId = await InsertLineAsync(connection, firstReceiptId, productId, 5m, userId);
        var secondLineId = await InsertLineAsync(connection, secondReceiptId, productId, 7.5m, userId);

        await connection.ExecuteAsync(
            "INSERT dbo.StockLedger " +
            "(WarehouseId, ProductId, DocumentType, DocumentId, DocumentLineId, QuantityChange, PostedAtUtc, PostedByUserId) VALUES " +
            "(@warehouseId, @productId, 'GoodsReceipt', @firstReceiptId, @firstLineId, 5.0000, @firstPosted, @userId), " +
            "(@warehouseId, @productId, 'GoodsReceipt', @secondReceiptId, @secondLineId, 7.5000, @secondPosted, @userId); " +
            "INSERT dbo.StockBalances (WarehouseId, ProductId, Quantity) VALUES " +
            "(@warehouseId, @productId, 12.5000), " +
            "(@otherWarehouseId, @otherProductId, 3.0000); " +
            "UPDATE dbo.Warehouses SET IsActive = 0 WHERE WarehouseId = @warehouseId; " +
            "UPDATE dbo.Products SET IsActive = 0 WHERE ProductId = @productId;",
            new
            {
                warehouseId,
                productId,
                firstReceiptId,
                firstLineId,
                firstPosted,
                secondReceiptId,
                secondLineId,
                secondPosted,
                userId,
                otherWarehouseId,
                otherProductId,
            });

        return new SeedData(
            warehouseId,
            productId,
            userId,
            firstReceiptId,
            secondReceiptId,
            firstPosted,
            secondPosted);
    }

    private static Task<long> InsertPostedReceiptAsync(
        SqlConnection connection,
        string receiptNumber,
        int warehouseId,
        int employeeId,
        int userId,
        DateTime postedAtUtc) =>
        connection.QuerySingleAsync<long>(
            "INSERT dbo.GoodsReceipts " +
            "(ReceiptNo, ReceiptDate, WarehouseId, EmployeeId, Status, CreatedByUserId, PostedAtUtc, PostedByUserId) " +
            "OUTPUT INSERTED.ReceiptId VALUES " +
            "(@receiptNumber, CAST(@postedAtUtc AS date), @warehouseId, @employeeId, 'Posted', @userId, @postedAtUtc, @userId);",
            new { receiptNumber, postedAtUtc, warehouseId, employeeId, userId });

    private static Task<long> InsertLineAsync(
        SqlConnection connection,
        long receiptId,
        int productId,
        decimal quantity,
        int userId) =>
        connection.QuerySingleAsync<long>(
            "INSERT dbo.GoodsReceiptLines " +
            "(ReceiptId, [LineNo], ProductId, Quantity, UnitCost, CreatedByUserId) " +
            "OUTPUT INSERTED.ReceiptLineId VALUES (@receiptId, 1, @productId, @quantity, 1.0000, @userId);",
            new { receiptId, productId, quantity, userId });

    private static async Task InsertBoundaryMovementsAsync(
        string connectionString,
        int warehouseId,
        int productId,
        int userId,
        DateTime lastTickOfSecond)
    {
        using var connection = new SqlConnection(connectionString);
        var parameters = new DynamicParameters();
        parameters.Add("warehouseId", warehouseId);
        parameters.Add("productId", productId);
        parameters.Add("lastTickOfSecond", lastTickOfSecond, DbType.DateTime2);
        parameters.Add("nextSecond", lastTickOfSecond.AddTicks(1), DbType.DateTime2);
        parameters.Add("userId", userId);
        await connection.ExecuteAsync(
            "INSERT dbo.StockLedger " +
            "(WarehouseId, ProductId, DocumentType, DocumentId, DocumentLineId, QuantityChange, PostedAtUtc, PostedByUserId) VALUES " +
            "(@warehouseId, @productId, 'Boundary', 1, 9001, 1.0000, @lastTickOfSecond, @userId), " +
            "(@warehouseId, @productId, 'Boundary', 2, 9002, 2.0000, @nextSecond, @userId);",
            parameters);
    }

    private sealed class SeedData
    {
        public SeedData(
            int archivedWarehouseId,
            int archivedProductId,
            int userId,
            long firstReceiptId,
            long secondReceiptId,
            DateTime firstPostedAtUtc,
            DateTime secondPostedAtUtc)
        {
            ArchivedWarehouseId = archivedWarehouseId;
            ArchivedProductId = archivedProductId;
            UserId = userId;
            FirstReceiptId = firstReceiptId;
            SecondReceiptId = secondReceiptId;
            FirstPostedAtUtc = firstPostedAtUtc;
            SecondPostedAtUtc = secondPostedAtUtc;
        }

        public int ArchivedWarehouseId { get; }
        public int ArchivedProductId { get; }
        public int UserId { get; }
        public long FirstReceiptId { get; }
        public long SecondReceiptId { get; }
        public DateTime FirstPostedAtUtc { get; }
        public DateTime SecondPostedAtUtc { get; }
    }
}
