using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using RepoDb;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class GoodsReceiptMappingIntegrationTests
{
    [SqlServerFact]
    public async Task Goods_receipt_entities_round_trip_through_registered_RepoDb_mappings()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());

            var services = new ServiceCollection();
            services.AddSqlPersistence(database.ConnectionString);
            using var provider = services.BuildServiceProvider();
            using var unitOfWork = await provider
                .GetRequiredService<IUnitOfWorkFactory>()
                .CreateAsync(CancellationToken.None);
            unitOfWork.BeginTransaction();
            var context = Assert.IsAssignableFrom<ISqlExecutionContext>(unitOfWork);
            var transaction = Assert.IsAssignableFrom<IDbTransaction>(context.Transaction);
            var createdAtUtc = new DateTime(2026, 10, 10, 3, 4, 5, DateTimeKind.Utc);

            var userId = await context.Connection.QuerySingleAsync<int>(
                "INSERT dbo.Users " +
                "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm, IsActive) " +
                "OUTPUT INSERTED.UserId VALUES (N'mapping-user', N'MAPPING-USER', N'Mapping User', N'Local', N'hash', N'', N'BCrypt', 1);",
                transaction: transaction);
            var warehouseId = await context.Connection.QuerySingleAsync<int>(
                "INSERT dbo.Warehouses (Code, Name) OUTPUT INSERTED.WarehouseId VALUES (N'WH-MAP', N'Mapping Warehouse');",
                transaction: transaction);
            var employeeId = await context.Connection.QuerySingleAsync<int>(
                "INSERT dbo.Employees (Code, Name) OUTPUT INSERTED.EmployeeId VALUES (N'EMP-MAP', N'Mapping Employee');",
                transaction: transaction);
            var productId = await context.Connection.QuerySingleAsync<int>(
                "INSERT dbo.Products (Code, Name, Unit) OUTPUT INSERTED.ProductId VALUES (N'PR-MAP', N'Mapping Product', N'EA');",
                transaction: transaction);
            var receiptId = await context.Connection.QuerySingleAsync<long>(
                "INSERT dbo.GoodsReceipts " +
                "(ReceiptNo, ReceiptDate, WarehouseId, EmployeeId, Status, Note, CreatedAtUtc, CreatedByUserId) " +
                "OUTPUT INSERTED.ReceiptId VALUES " +
                "(N'GR-MAP-001', '20261010', @warehouseId, @employeeId, 'Draft', N'Mapped', @createdAtUtc, @userId);",
                new { warehouseId, employeeId, createdAtUtc, userId },
                transaction);
            var lineId = await context.Connection.QuerySingleAsync<long>(
                "INSERT dbo.GoodsReceiptLines " +
                "(ReceiptId, [LineNo], ProductId, Quantity, UnitCost, CreatedAtUtc, CreatedByUserId) " +
                "OUTPUT INSERTED.ReceiptLineId VALUES " +
                "(@receiptId, 1, @productId, 2.5000, 19.9900, @createdAtUtc, @userId);",
                new { receiptId, productId, createdAtUtc, userId },
                transaction);

            var receipt = (await context.Connection.QueryAsync<GoodsReceipt>(
                receiptId,
                transaction: transaction,
                cancellationToken: CancellationToken.None)).Single();
            var line = (await context.Connection.QueryAsync<GoodsReceiptLine>(
                lineId,
                transaction: transaction,
                cancellationToken: CancellationToken.None)).Single();

            Assert.Equal(receiptId, receipt.Id);
            Assert.Equal("GR-MAP-001", receipt.ReceiptNumber);
            Assert.Equal(new DateTime(2026, 10, 10), receipt.ReceiptDate);
            Assert.Equal(warehouseId, receipt.WarehouseId);
            Assert.Equal(employeeId, receipt.EmployeeId);
            Assert.Equal(GoodsReceiptStatuses.Draft, receipt.Status);
            Assert.Equal("Mapped", receipt.Note);
            Assert.Equal(createdAtUtc, receipt.CreatedAtUtc);
            Assert.Equal(userId, receipt.CreatedByUserId);
            Assert.Equal(8, receipt.Version.Length);
            Assert.Equal(lineId, line.Id);
            Assert.Equal(receiptId, line.ReceiptId);
            Assert.Equal(1, line.LineNumber);
            Assert.Equal(productId, line.ProductId);
            Assert.Equal(2.5000m, line.Quantity);
            Assert.Equal(19.9900m, line.UnitCost);
            Assert.Equal(createdAtUtc, line.CreatedAtUtc);
            Assert.Equal(userId, line.CreatedByUserId);

            unitOfWork.Rollback();
        }
        finally
        {
            await database.DisposeAsync();
        }
    }
}
