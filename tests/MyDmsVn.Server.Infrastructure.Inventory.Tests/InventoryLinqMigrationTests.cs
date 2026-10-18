using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.IntegrationTests.Pilot;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class InventoryLinqMigrationTests
{
    [SqlServerFact]
    public Task Registered_inventory_mapping_matches_native_schema_and_preserves_write_allowlists() =>
        PilotSql.Owned(async database =>
        {
            var services = new ServiceCollection(); services.AddSqlPersistence(database.ConnectionString);
            using var provider = services.BuildServiceProvider();
            Assert.Contains(provider.GetServices<ILinqToDbMapping>(), m => m.GetType().Name == "InventoryLinqToDbMapping");
            using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create(); uow.BeginTransaction();
            var ctx = (ISqlExecutionContext)uow; var sql = (SqlConnection)ctx.Connection; var tx = (SqlTransaction)ctx.Transaction!;
            var actor = await PilotSql.Scalar<int>(sql, tx, "INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'map',N'MAP',N'Map',N'Local',N'hash',N'',N'BCrypt')");
            var warehouse = await PilotSql.Scalar<int>(sql, tx, "INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'W',N'Kho')");
            var employee = await PilotSql.Scalar<int>(sql, tx, "INSERT dbo.Employees(Code,Name) OUTPUT INSERTED.EmployeeId VALUES(N'E',N'Người')");
            var product = await PilotSql.Scalar<int>(sql, tx, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P',N'Hàng',N'cái')");
            var old = new DateTime(2000, 1, 1); var stamp = new DateTime(2026, 10, 18, 1, 2, 3).AddTicks(1234567);
            var input = new GoodsReceipt { Id = 999, ReceiptNumber = "client-number", ReceiptDate = stamp, WarehouseId = warehouse, EmployeeId = employee, Note = "Tiếng Việt", CreatedAtUtc = old, CreatedByUserId = actor, UpdatedAtUtc = old, UpdatedByUserId = actor, PostedAtUtc = old, PostedByUserId = actor, Version = new byte[] { 9 } };
            var line = new GoodsReceiptLine { Id = 999, LineNumber = 1, ProductId = product, Quantity = 99999999999999.9999m, UnitCost = 999999999999999.9999m, CreatedAtUtc = old, CreatedByUserId = actor, UpdatedAtUtc = old, UpdatedByUserId = actor };
            var r = uow.Repository<IGoodsReceiptWriteRepository>();
            var saved = await r.InsertDraftAsync(input, new[] { line }, CancellationToken.None);
            Assert.True(saved.Id > 0); Assert.NotEqual(999, saved.Id); Assert.NotEqual(999, line.Id);
            Assert.StartsWith("GR", saved.ReceiptNumber); Assert.NotEqual("client-number", saved.ReceiptNumber);
            Assert.Equal(stamp.Date, saved.ReceiptDate); Assert.Equal("Tiếng Việt", saved.Note);
            Assert.True(saved.CreatedAtUtc > old); Assert.Equal(actor, saved.CreatedByUserId);
            Assert.Null(saved.UpdatedAtUtc); Assert.Null(saved.UpdatedByUserId); Assert.Null(saved.PostedAtUtc); Assert.Null(saved.PostedByUserId);
            Assert.Equal(8, saved.Version.Length);
            using var db = provider.GetRequiredService<ILinqToDbSessionFactory>().Borrow(ctx, true);
            var loadedLine = await db.GetTable<GoodsReceiptLine>().SingleAsync(x => x.Id == line.Id);
            Assert.Equal(line.Quantity, loadedLine.Quantity); Assert.Equal(line.UnitCost, loadedLine.UnitCost);
            Assert.True(loadedLine.CreatedAtUtc > old); Assert.Null(loadedLine.UpdatedAtUtc); Assert.Null(loadedLine.UpdatedByUserId);
            saved.Status = "Posted"; saved.ReceiptNumber = "overwrite"; saved.CreatedAtUtc = old; saved.CreatedByUserId = -1;
            saved.PostedAtUtc = old; saved.PostedByUserId = -1; saved.Note = null; saved.UpdatedAtUtc = stamp; saved.UpdatedByUserId = actor;
            var changed = await r.ReplaceDraftAsync(saved, new[] { line }, CancellationToken.None);
            Assert.Equal("Draft", changed.Status); Assert.NotEqual("overwrite", changed.ReceiptNumber); Assert.Null(changed.Note);
            Assert.True(changed.CreatedAtUtc > old); Assert.Equal(actor, changed.CreatedByUserId); Assert.Null(changed.PostedAtUtc); Assert.Null(changed.PostedByUserId);
            Assert.Equal(stamp, changed.UpdatedAtUtc); Assert.Equal(actor, changed.UpdatedByUserId); Assert.False(changed.Version.SequenceEqual(saved.Version));
            Assert.False(await r.TryMarkPostedAsync(changed.Id, saved.Version, stamp, actor, CancellationToken.None));
            Assert.True(await r.TryMarkPostedAsync(changed.Id, changed.Version, stamp, actor, CancellationToken.None));
            await r.AppendStockLedgerAsync(changed.Id, warehouse, new[] { line }, stamp, actor, CancellationToken.None);
            var entry = await db.GetTable<StockLedgerEntry>().SingleAsync();
            Assert.Equal(line.Id, entry.DocumentLineId); Assert.Equal(line.Quantity, entry.QuantityChange); Assert.Equal(stamp, entry.PostedAtUtc); Assert.Equal(actor, entry.PostedByUserId);
            Assert.Equal(stamp, await PilotSql.Scalar<DateTime>(sql, tx, "SELECT PostedAtUtc FROM dbo.StockLedger"));
            foreach (var item in new[] { (typeof(GoodsReceipt), "GoodsReceipts"), (typeof(GoodsReceiptLine), "GoodsReceiptLines"), (typeof(StockLedgerEntry), "StockLedger") })
            foreach (var column in db.MappingSchema.GetEntityDescriptor(item.Item1, null).Columns)
            {
                using var cmd = PilotSql.Command(sql, tx, "SELECT is_nullable, max_length, precision, scale FROM sys.columns WHERE object_id=OBJECT_ID(@table) AND name=@column", PilotSql.P("@table", SqlDbType.NVarChar, "dbo." + item.Item2), PilotSql.P("@column", SqlDbType.NVarChar, column.ColumnName));
                using var reader = await cmd.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
                Assert.Equal(reader.GetBoolean(0), column.CanBeNull);
                if (column.DataType == DataType.Decimal) { Assert.Equal((int)reader.GetByte(2), column.Precision); Assert.Equal((int)reader.GetByte(3), column.Scale); }
                if (column.DataType == DataType.NVarChar) Assert.Equal(reader.GetInt16(1) / 2, column.Length);
                if (column.DataType == DataType.VarChar) Assert.Equal((int)reader.GetInt16(1), column.Length);
            }
            db.Dispose(); uow.Rollback();
        });

    [SqlServerFact]
    public Task Native_inventory_queries_preserve_literal_unicode_search_tied_movements_fallbacks_and_long_offsets() =>
        PilotSql.Owned(async database =>
        {
            using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
            var actor = await PilotSql.Scalar<int>(oracle, null, "INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'inquiry',N'INQUIRY',N'Inquiry',N'Local',N'hash',N'',N'BCrypt')");
            var warehouse = await PilotSql.Scalar<int>(oracle, null, "INSERT dbo.Warehouses(Code,Name,IsActive) OUTPUT INSERTED.WarehouseId VALUES(N'W',N'Kho %_ Việt',0)");
            var product = await PilotSql.Scalar<int>(oracle, null, "INSERT dbo.Products(Code,Name,Unit,IsActive) OUTPUT INSERTED.ProductId VALUES(N'P',N'Hàng',N'cái',0)");
            var stamp = new DateTime(2026, 10, 18, 1, 2, 3, DateTimeKind.Utc).AddTicks(1234567);
            await PilotSql.Execute(oracle, null, "INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity) VALUES(@w,@p,-.0001)", PilotSql.P("@w", SqlDbType.Int, warehouse), PilotSql.P("@p", SqlDbType.Int, product));
            var ids = new long[2];
            for (var index = 0; index < 2; index++)
                ids[index] = await PilotSql.Scalar<long>(oracle, null,
                    "INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) OUTPUT INSERTED.LedgerId VALUES(@w,@p,@type,@doc,@line,@q,@stamp,@actor)",
                    PilotSql.P("@w", SqlDbType.Int, warehouse), PilotSql.P("@p", SqlDbType.Int, product), PilotSql.P("@type", SqlDbType.VarChar, index == 0 ? "Adjustment" : "GoodsReceipt"),
                    PilotSql.P("@doc", SqlDbType.BigInt, 900L + index), PilotSql.P("@line", SqlDbType.BigInt, index + 1L), PilotSql.P("@q", SqlDbType.Decimal, index == 0 ? -.0001m : .0001m), PilotSql.P("@stamp", SqlDbType.DateTime2, stamp), PilotSql.P("@actor", SqlDbType.Int, actor));
            var services = new ServiceCollection(); services.AddSqlPersistence(database.ConnectionString); using var provider = services.BuildServiceProvider();
            var query = provider.GetRequiredService<IInventoryQueryService>();
            var balance = await query.GetStockBalancesAsync(new StockBalanceQuery(warehouse, product, "  %_  ", 1, 10), CancellationToken.None);
            var row = Assert.Single(balance.Items); Assert.Equal(1, balance.TotalCount); Assert.Equal(-.0001m, row.Quantity);
            Assert.Equal("Kho %_ Việt", row.WarehouseName); Assert.Equal("Hàng", row.ProductName); Assert.False(row.WarehouseIsActive); Assert.False(row.ProductIsActive);
            Assert.Empty((await query.GetStockBalancesAsync(new StockBalanceQuery(null, null, "%missing_", 1, 10), CancellationToken.None)).Items);
            for (var page = 1; page <= 2; page++)
            {
                var result = await query.GetStockCardAsync(new StockCardQuery(warehouse, product, stamp, stamp, page, 1), CancellationToken.None);
                var movement = Assert.Single(result.Items); Assert.Equal(2, result.TotalCount); Assert.Equal(ids[page - 1], movement.LedgerId);
                Assert.Equal(page == 1 ? "Adjustment#900" : "GoodsReceipt#901", movement.DocumentNumber);
                Assert.Equal(page == 1 ? -.0001m : .0001m, movement.QuantityChange);
                Assert.Equal(stamp, movement.PostedAtUtc); Assert.Equal(DateTimeKind.Utc, movement.PostedAtUtc.Kind);
            }
            var beyond = await query.GetStockCardAsync(new StockCardQuery(warehouse, product, stamp.AddTicks(1), null, 1, 10), CancellationToken.None);
            Assert.Equal(0, beyond.TotalCount); Assert.Empty(beyond.Items);
            var farCard = await query.GetStockCardAsync(new StockCardQuery(warehouse, product, null, null, int.MaxValue, 100), CancellationToken.None);
            Assert.Equal(2, farCard.TotalCount); Assert.Empty(farCard.Items);
            var farBalance = await query.GetStockBalancesAsync(new StockBalanceQuery(null, null, null, int.MaxValue, 100), CancellationToken.None);
            Assert.Equal(1, farBalance.TotalCount); Assert.Empty(farBalance.Items);
            var receipts = provider.GetRequiredService<IGoodsReceiptQueryService>();
            Assert.Null(await receipts.GetByIdAsync(long.MaxValue, CancellationToken.None));
            var farReceipts = await receipts.ListAsync(new GoodsReceiptListRequest(int.MaxValue, 100, null, null, null), CancellationToken.None);
            Assert.Equal(0, farReceipts.TotalCount); Assert.Empty(farReceipts.Items);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.GetStockCardAsync(new StockCardQuery(warehouse, product, null, null, 1, 10), cancel.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.GetStockBalancesAsync(new StockBalanceQuery(null, null, null, 1, 10), cancel.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => receipts.GetByIdAsync(1, cancel.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => receipts.ListAsync(new GoodsReceiptListRequest(1, 10, null, null, null), cancel.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<IStockReconciliationQueryService>().FindDifferencesAsync(cancel.Token));
        });

}
