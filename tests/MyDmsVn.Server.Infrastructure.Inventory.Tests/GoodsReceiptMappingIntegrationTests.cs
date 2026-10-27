using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using LinqToDB;
using LinqToDB.Async;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[Collection(ApplicationSqlCollectionFixture.CollectionName)]
public sealed class GoodsReceiptMappingIntegrationTests
{
    private readonly ApplicationSqlCollectionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public GoodsReceiptMappingIntegrationTests(ApplicationSqlCollectionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [SqlServerFact]
    public Task Goods_receipt_entities_round_trip_through_registered_Linq_mappings() =>
        _fixture.RunAsync(nameof(Goods_receipt_entities_round_trip_through_registered_Linq_mappings), _output, async test =>
        {
            var services = new ServiceCollection();
            services.AddSqlPersistence(test.ConnectionString);
            var provider = ApplicationSqlCollectionFixture.Track(test, services.BuildServiceProvider());
            var unitOfWork = ApplicationSqlCollectionFixture.Track(test, await provider
                .GetRequiredService<IUnitOfWorkFactory>()
                .CreateAsync(CancellationToken.None));
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
            using var db = provider.GetRequiredService<ILinqToDbSessionFactory>().Borrow(context, true);
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
            var receiptId = await db.GetTable<GoodsReceipt>()
                .Value(row => row.ReceiptNumber, receiptEntity.ReceiptNumber).Value(row => row.ReceiptDate, receiptEntity.ReceiptDate)
                .Value(row => row.WarehouseId, receiptEntity.WarehouseId).Value(row => row.EmployeeId, receiptEntity.EmployeeId)
                .Value(row => row.Status, receiptEntity.Status).Value(row => row.Note, receiptEntity.Note)
                .Value(row => row.CreatedByUserId, receiptEntity.CreatedByUserId).InsertWithInt64IdentityAsync() ?? throw new InvalidOperationException("Missing header identity.");
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
            var lineId = await db.GetTable<GoodsReceiptLine>()
                .Value(row => row.ReceiptId, lineEntity.ReceiptId).Value(row => row.LineNumber, lineEntity.LineNumber)
                .Value(row => row.ProductId, lineEntity.ProductId).Value(row => row.Quantity, lineEntity.Quantity)
                .Value(row => row.UnitCost, lineEntity.UnitCost).Value(row => row.CreatedByUserId, lineEntity.CreatedByUserId)
                .InsertWithInt64IdentityAsync() ?? throw new InvalidOperationException("Missing line identity.");

            var receipt = await db.GetTable<GoodsReceipt>().SingleAsync(row => row.Id == receiptId);
            var line = await db.GetTable<GoodsReceiptLine>().SingleAsync(row => row.Id == lineId);

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
            var affected = await db.GetTable<GoodsReceipt>().Where(row => row.Id == receiptId)
                .Set(row => row.ReceiptDate, receiptEntity.ReceiptDate).Set(row => row.WarehouseId, receiptEntity.WarehouseId)
                .Set(row => row.EmployeeId, receiptEntity.EmployeeId).Set(row => row.Note, receiptEntity.Note)
                .Set(row => row.UpdatedAtUtc, receiptEntity.UpdatedAtUtc).Set(row => row.UpdatedByUserId, receiptEntity.UpdatedByUserId).UpdateAsync();
            var updatedReceipt = await db.GetTable<GoodsReceipt>().SingleAsync(row => row.Id == receiptId);

            Assert.Equal(1, affected);
            Assert.Equal("Updated through explicit fields", updatedReceipt.Note);
            Assert.NotEqual(firstVersion, updatedReceipt.Version);

            var sql = (Microsoft.Data.SqlClient.SqlConnection)context.Connection;
            var sqlTx = (Microsoft.Data.SqlClient.SqlTransaction)transaction;
            Assert.Equal(receiptId, await NativeSql.Scalar<long>(sql, sqlTx, "SELECT ReceiptId FROM dbo.GoodsReceipts"));
            Assert.Equal("GR-MAP-001", await NativeSql.Scalar<string>(sql, sqlTx, "SELECT ReceiptNo FROM dbo.GoodsReceipts"));
            Assert.Equal(lineId, await NativeSql.Scalar<long>(sql, sqlTx, "SELECT ReceiptLineId FROM dbo.GoodsReceiptLines"));
            Assert.Equal(updatedReceipt.Version, await NativeSql.Scalar<byte[]>(sql, sqlTx, "SELECT RowVersion FROM dbo.GoodsReceipts"));
            Assert.Equal(createdAtUtc, updatedReceipt.UpdatedAtUtc);
            Assert.Equal(userId, updatedReceipt.UpdatedByUserId);
            db.Dispose(); unitOfWork.Rollback();
        });

}
