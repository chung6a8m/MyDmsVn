using System;
using System.Collections.Generic;
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
            var receiptCreateFields = GetWriteFields("GoodsReceiptCreate");
            var receiptDraftUpdateFields = GetWriteFields("GoodsReceiptDraftUpdate");
            var lineCreateFields = GetWriteFields("GoodsReceiptLineCreate");
            var receiptEntity = new GoodsReceipt
            {
                ReceiptNumber = "GR-MAP-001",
                ReceiptDate = new DateTime(2026, 10, 10),
                WarehouseId = warehouseId,
                EmployeeId = employeeId,
                Status = GoodsReceiptStatuses.Draft,
                Note = "Mapped",
                CreatedAtUtc = createdAtUtc,
                CreatedByUserId = userId,
                Version = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
            };
            var receiptId = await context.Connection.InsertAsync<GoodsReceipt, long>(
                receiptEntity,
                fields: receiptCreateFields,
                transaction: transaction,
                cancellationToken: CancellationToken.None);
            var lineEntity = new GoodsReceiptLine
            {
                ReceiptId = receiptId,
                LineNumber = 1,
                ProductId = productId,
                Quantity = 2.5000m,
                UnitCost = 19.9900m,
                CreatedAtUtc = createdAtUtc,
                CreatedByUserId = userId,
            };
            var lineId = await context.Connection.InsertAsync<GoodsReceiptLine, long>(
                lineEntity,
                fields: lineCreateFields,
                transaction: transaction,
                cancellationToken: CancellationToken.None);

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
            Assert.NotEqual(createdAtUtc, receipt.CreatedAtUtc);
            Assert.Equal(userId, receipt.CreatedByUserId);
            Assert.Equal(8, receipt.Version.Length);
            Assert.Equal(lineId, line.Id);
            Assert.Equal(receiptId, line.ReceiptId);
            Assert.Equal(1, line.LineNumber);
            Assert.Equal(productId, line.ProductId);
            Assert.Equal(2.5000m, line.Quantity);
            Assert.Equal(19.9900m, line.UnitCost);
            Assert.NotEqual(createdAtUtc, line.CreatedAtUtc);
            Assert.Equal(userId, line.CreatedByUserId);

            var firstVersion = receipt.Version;
            receiptEntity.Id = receiptId;
            receiptEntity.Note = "Updated through explicit fields";
            receiptEntity.UpdatedAtUtc = createdAtUtc;
            receiptEntity.UpdatedByUserId = userId;
            var affected = await context.Connection.UpdateAsync(
                receiptEntity,
                fields: receiptDraftUpdateFields,
                transaction: transaction,
                cancellationToken: CancellationToken.None);
            var updatedReceipt = (await context.Connection.QueryAsync<GoodsReceipt>(
                receiptId,
                transaction: transaction,
                cancellationToken: CancellationToken.None)).Single();

            Assert.Equal(1, affected);
            Assert.Equal("Updated through explicit fields", updatedReceipt.Note);
            Assert.NotEqual(firstVersion, updatedReceipt.Version);

            unitOfWork.Rollback();
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static IEnumerable<Field> GetWriteFields(string fieldName)
    {
        var mappingAssembly = typeof(MyDmsVn.Server.Infrastructure.DependencyInjection).Assembly;
        var writeFieldsType = mappingAssembly.GetType(
            "MyDmsVn.Server.Infrastructure.Inventory.InventoryRepoDbWriteFields");
        Assert.NotNull(writeFieldsType);
        var writeFields = writeFieldsType!
            .GetField(fieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(null);
        return Assert.IsAssignableFrom<IEnumerable<Field>>(writeFields);
    }
}
