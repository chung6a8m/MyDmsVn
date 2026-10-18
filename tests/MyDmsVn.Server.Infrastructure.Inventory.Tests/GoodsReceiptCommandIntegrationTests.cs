using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class GoodsReceiptCommandIntegrationTests
{
    [Theory]
    [InlineData(15, "VIEW SERVER STATE")]
    [InlineData(16, "VIEW SERVER PERFORMANCE STATE")]
    public void Cross_session_DMV_permission_changes_at_SQL_Server_2022(
        int majorVersion,
        string expectedPermission)
    {
        Assert.Equal(expectedPermission, GetRequiredCrossSessionDmvPermission(majorVersion));
    }

    [Theory]
    [InlineData(15, "VIEW SERVER STATE")]
    [InlineData(16, "VIEW SERVER PERFORMANCE STATE")]
    public void Missing_cross_session_DMV_permission_reports_the_required_grant(
        int majorVersion,
        string expectedPermission)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ThrowIfCrossSessionDmvPermissionMissing(majorVersion, hasPermission: false));

        Assert.Contains("sys.dm_exec_requests", exception.Message);
        Assert.Contains(expectedPermission, exception.Message);
    }

    [SqlServerFact]
    public async Task Create_and_update_draft_use_server_numbers_atomic_lines_and_advancing_versions()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            using var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true);
            using var scope = provider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var first = await sender.Send(
                new CreateGoodsReceiptDraftCommand(Save(seed, "original", seed.Product1Id)),
                CancellationToken.None);
            var second = await sender.Send(
                new CreateGoodsReceiptDraftCommand(Save(seed, "second", seed.Product2Id)),
                CancellationToken.None);

            Assert.False(first.IsError);
            Assert.False(second.IsError);
            Assert.Matches(new Regex("^GR[0-9]{10}$", RegexOptions.CultureInvariant), first.Value.ReceiptNumber);
            Assert.Matches(new Regex("^GR[0-9]{10}$", RegexOptions.CultureInvariant), second.Value.ReceiptNumber);
            Assert.NotEqual(first.Value.ReceiptNumber, second.Value.ReceiptNumber);

            var update = await sender.Send(
                new UpdateGoodsReceiptDraftCommand(
                    new UpdateGoodsReceiptDraftRequest(
                        first.Value.Id,
                        first.Value.Version,
                        first.Value.ReceiptDate,
                        seed.WarehouseId,
                        seed.EmployeeId,
                        "original",
                        new[]
                        {
                            new SaveGoodsReceiptLineRequest(seed.Product1Id, 3m, 11m),
                            new SaveGoodsReceiptLineRequest(seed.Product2Id, 4m, 12m),
                        })),
                CancellationToken.None);

            Assert.False(update.IsError);
            Assert.NotEqual(first.Value.Version, update.Value.Version);
            Assert.Equal(new[] { 1, 2 }, update.Value.Lines.Select(line => line.LineNumber));

            using var connection = new SqlConnection(database.ConnectionString);
            var stored = await connection.QuerySingleAsync<StoredReceipt>(
                "SELECT Note, RowVersion AS Version FROM dbo.GoodsReceipts WHERE ReceiptId = @id;",
                new { id = first.Value.Id });
            var storedLines = (await connection.QueryAsync<StoredLine>(
                "SELECT [LineNo] AS LineNumber, ProductId, Quantity, UnitCost " +
                "FROM dbo.GoodsReceiptLines WHERE ReceiptId = @id ORDER BY [LineNo];",
                new { id = first.Value.Id })).ToArray();
            Assert.Equal("original", stored.Note);
            Assert.Equal(update.Value.Version, Convert.ToBase64String(stored.Version));
            Assert.Equal(2, storedLines.Length);
            Assert.Equal(3m, storedLines[0].Quantity);
            Assert.Equal(seed.Product2Id, storedLines[1].ProductId);

            var stale = await sender.Send(
                new UpdateGoodsReceiptDraftCommand(
                    new UpdateGoodsReceiptDraftRequest(
                        first.Value.Id,
                        first.Value.Version,
                        first.Value.ReceiptDate,
                        seed.WarehouseId,
                        seed.EmployeeId,
                        "stale",
                        new[] { new SaveGoodsReceiptLineRequest(seed.Product1Id, 99m, 1m) })),
                CancellationToken.None);
            Assert.True(stale.IsError);
            Assert.Equal("GoodsReceipt.ConcurrencyConflict", stale.FirstError.Code);
            Assert.Equal("original", await connection.QuerySingleAsync<string>(
                "SELECT Note FROM dbo.GoodsReceipts WHERE ReceiptId = @id;",
                new { id = first.Value.Id }));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Create_draft_resolves_and_persists_a_multi_line_product_batch()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            var productIds = new int[25];
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                for (var index = 0; index < productIds.Length; index++)
                {
                    productIds[index] = await connection.QuerySingleAsync<int>(
                        "INSERT dbo.Products (Code, Name, Unit) OUTPUT INSERTED.ProductId " +
                        "VALUES (@code, @name, N'EA');",
                        new
                        {
                            code = "P-BATCH-" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
                            name = "Batch Product " + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        });
                }
            }

            using var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true);
            using var scope = provider.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateGoodsReceiptDraftCommand(
                    new SaveGoodsReceiptRequest(
                        new DateTime(2026, 10, 10),
                        seed.WarehouseId,
                        seed.EmployeeId,
                        "batch",
                        productIds.Select((productId, index) =>
                            new SaveGoodsReceiptLineRequest(productId, index + 1, 2.5m)))),
                CancellationToken.None);

            Assert.False(result.IsError);
            Assert.Equal(25, result.Value.Lines.Count);
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(25, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.GoodsReceiptLines WHERE ReceiptId = @id;",
                new { id = result.Value.Id }));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Failed_update_rolls_back_header_version_and_all_line_replacement()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            using var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true);
            GoodsReceiptDto created;
            using (var createScope = provider.CreateScope())
            {
                var result = await createScope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new CreateGoodsReceiptDraftCommand(Save(seed, "before", seed.Product1Id)),
                    CancellationToken.None);
                Assert.False(result.IsError);
                created = result.Value;
            }

            using (var setup = new SqlConnection(database.ConnectionString))
            {
                var triggerSql = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "CREATE TRIGGER dbo.TR_GoodsReceiptLines_TestFailure ON dbo.GoodsReceiptLines " +
                    "AFTER INSERT AS BEGIN SET NOCOUNT ON; " +
                    "IF EXISTS (SELECT 1 FROM inserted WHERE ProductId = {0}) " +
                    "BEGIN; THROW 51000, 'Injected line failure', 1; END; END;",
                    seed.Product2Id);
                await setup.ExecuteAsync(triggerSql);
            }

            using (var updateScope = provider.CreateScope())
            {
                var failed = await updateScope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new UpdateGoodsReceiptDraftCommand(
                        new UpdateGoodsReceiptDraftRequest(
                            created.Id,
                            created.Version,
                            created.ReceiptDate,
                            seed.WarehouseId,
                            seed.EmployeeId,
                            "after",
                            new[]
                            {
                                new SaveGoodsReceiptLineRequest(seed.Product1Id, 2m, 1m),
                                new SaveGoodsReceiptLineRequest(seed.Product2Id, 3m, 1m),
                            })),
                    CancellationToken.None);
                Assert.True(failed.IsError);
            }

            using var connection = new SqlConnection(database.ConnectionString);
            var stored = await connection.QuerySingleAsync<StoredReceipt>(
                "SELECT Note, RowVersion AS Version FROM dbo.GoodsReceipts WHERE ReceiptId = @id;",
                new { id = created.Id });
            var lines = (await connection.QueryAsync<StoredLine>(
                "SELECT [LineNo] AS LineNumber, ProductId, Quantity, UnitCost " +
                "FROM dbo.GoodsReceiptLines WHERE ReceiptId = @id;",
                new { id = created.Id })).ToArray();
            Assert.Equal("before", stored.Note);
            Assert.Equal(created.Version, Convert.ToBase64String(stored.Version));
            Assert.Single(lines);
            Assert.Equal(seed.Product1Id, lines[0].ProductId);
            Assert.Equal(1m, lines[0].Quantity);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Concurrent_updates_with_one_expected_version_allow_exactly_one_commit()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            using var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true);
            GoodsReceiptDto created;
            using (var scope = provider.CreateScope())
            {
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new CreateGoodsReceiptDraftCommand(Save(seed, "initial", seed.Product1Id)),
                    CancellationToken.None);
                created = result.Value;
            }

            var firstTask = UpdateInOwnScopeAsync(provider, created, seed, "first", 10m);
            var secondTask = UpdateInOwnScopeAsync(provider, created, seed, "second", 20m);
            var results = await Task.WhenAll(firstTask, secondTask);

            Assert.Single(results, result => !result.IsError);
            var conflict = Assert.Single(results, result => result.IsError);
            Assert.Equal("GoodsReceipt.ConcurrencyConflict", conflict.FirstError.Code);

            using var connection = new SqlConnection(database.ConnectionString);
            var lineCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.GoodsReceiptLines WHERE ReceiptId = @id;",
                new { id = created.Id });
            Assert.Equal(1, lineCount);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Concurrent_creates_in_separate_uows_issue_unique_server_numbers()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            using var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true);

            var tasks = Enumerable.Range(1, 8)
                .Select(index => CreateInOwnScopeAsync(provider, seed, "concurrent-" + index))
                .ToArray();
            var results = await Task.WhenAll(tasks);

            Assert.All(results, result => Assert.False(result.IsError));
            var numbers = results.Select(result => result.Value.ReceiptNumber).ToArray();
            Assert.Equal(numbers.Length, numbers.Distinct(StringComparer.Ordinal).Count());
            Assert.All(numbers, number =>
                Assert.Matches(new Regex("^GR[0-9]{10}$", RegexOptions.CultureInvariant), number));

            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(8, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(DISTINCT ReceiptNo) FROM dbo.GoodsReceipts;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Post_only_user_cannot_read_or_write_and_leaves_the_aggregate_unchanged()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            GoodsReceiptDto created;
            using (var allowedProvider = CreateProvider(database.ConnectionString, seed.UserId, allow: true))
            using (var scope = allowedProvider.CreateScope())
            {
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new CreateGoodsReceiptDraftCommand(Save(seed, "before", seed.Product1Id)),
                    CancellationToken.None);
                Assert.False(result.IsError);
                created = result.Value;
            }
            var before = await LoadAggregateSnapshotAsync(database.ConnectionString, created.Id);

            using (var postOnlyProvider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: false,
                onlyPermission: PermissionKeys.InventoryGoodsReceiptsPost))
            using (var scope = postOnlyProvider.CreateScope())
            {
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var create = await sender.Send(
                    new CreateGoodsReceiptDraftCommand(Save(seed, "forbidden-create", seed.Product2Id)),
                    CancellationToken.None);
                var update = await sender.Send(
                    new UpdateGoodsReceiptDraftCommand(
                        new UpdateGoodsReceiptDraftRequest(
                            created.Id,
                            created.Version,
                            created.ReceiptDate,
                            seed.WarehouseId,
                            seed.EmployeeId,
                            "forbidden-update",
                            new[] { new SaveGoodsReceiptLineRequest(seed.Product2Id, 9m, 9m) })),
                    CancellationToken.None);
                var list = await sender.Send(
                    new ListGoodsReceiptsQuery(new GoodsReceiptListRequest(1, 25, null, null)),
                    CancellationToken.None);
                var get = await sender.Send(
                    new GetGoodsReceiptByIdQuery(created.Id),
                    CancellationToken.None);

                Assert.True(create.IsError);
                Assert.True(update.IsError);
                Assert.True(list.IsError);
                Assert.True(get.IsError);
                Assert.All(create.Errors, error => Assert.Equal(ErrorOr.ErrorType.Forbidden, error.Type));
                Assert.All(update.Errors, error => Assert.Equal(ErrorOr.ErrorType.Forbidden, error.Type));
                Assert.All(list.Errors, error => Assert.Equal(ErrorOr.ErrorType.Forbidden, error.Type));
                Assert.All(get.Errors, error => Assert.Equal(ErrorOr.ErrorType.Forbidden, error.Type));
            }

            var after = await LoadAggregateSnapshotAsync(database.ConnectionString, created.Id);
            AssertAggregateEqual(before, after);
            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.GoodsReceipts;"));
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.GoodsReceiptLines;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Inactive_reference_and_explicit_write_deny_leave_receipt_tables_empty()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            using var connection = new SqlConnection(database.ConnectionString);
            await connection.ExecuteAsync(
                "UPDATE dbo.Warehouses SET IsActive = 0 WHERE WarehouseId = @id;",
                new { id = seed.WarehouseId });

            using (var allowedProvider = CreateProvider(database.ConnectionString, seed.UserId, allow: true))
            using (var scope = allowedProvider.CreateScope())
            {
                var inactive = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new CreateGoodsReceiptDraftCommand(Save(seed, null, seed.Product1Id)),
                    CancellationToken.None);
                Assert.True(inactive.IsError);
                Assert.Equal("GoodsReceipt.WarehouseInactive", inactive.FirstError.Code);
            }

            await connection.ExecuteAsync(
                "UPDATE dbo.Warehouses SET IsActive = 1 WHERE WarehouseId = @id;",
                new { id = seed.WarehouseId });
            using (var deniedProvider = CreateProvider(database.ConnectionString, seed.UserId, allow: false))
            using (var scope = deniedProvider.CreateScope())
            {
                var denied = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new CreateGoodsReceiptDraftCommand(Save(seed, null, seed.Product1Id)),
                    CancellationToken.None);
                Assert.True(denied.IsError);
                Assert.Equal(ErrorOr.ErrorType.Forbidden, denied.FirstError.Type);
            }

            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.GoodsReceipts;"));
            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.GoodsReceiptLines;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Post_commits_status_ledger_and_balances_once()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            GoodsReceiptDto created;
            using (var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true))
            using (var scope = provider.CreateScope())
            {
                var save = new SaveGoodsReceiptRequest(
                    new DateTime(2026, 10, 10),
                    seed.WarehouseId,
                    seed.EmployeeId,
                    "post",
                    new[]
                    {
                        new SaveGoodsReceiptLineRequest(seed.Product1Id, 2.5m, 10m),
                        new SaveGoodsReceiptLineRequest(seed.Product2Id, 1m, 20m),
                    });
                var create = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new CreateGoodsReceiptDraftCommand(save),
                    CancellationToken.None);
                Assert.False(create.IsError);
                created = create.Value;
            }

            using var postProvider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: false,
                onlyPermission: PermissionKeys.InventoryGoodsReceiptsPost);
            using (var scope = postProvider.CreateScope())
            {
                var post = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new PostGoodsReceiptCommand(
                        new PostGoodsReceiptRequest(created.Id, created.Version)),
                    CancellationToken.None);
                Assert.False(post.IsError);
                Assert.Equal(GoodsReceiptStatuses.Posted, post.Value.Status);
            }

            using var connection = new SqlConnection(database.ConnectionString);
            var stored = await connection.QuerySingleAsync<PostedReceiptRow>(
                "SELECT Status, PostedAtUtc, PostedByUserId FROM dbo.GoodsReceipts WHERE ReceiptId = @id;",
                new { id = created.Id });
            var ledger = (await connection.QueryAsync<LedgerRow>(
                "SELECT DocumentType, DocumentId, DocumentLineId, WarehouseId, ProductId, QuantityChange, PostedByUserId " +
                "FROM dbo.StockLedger WHERE DocumentId = @id ORDER BY DocumentLineId;",
                new { id = created.Id })).ToArray();
            var balances = (await connection.QueryAsync<BalanceRow>(
                "SELECT ProductId, Quantity FROM dbo.StockBalances WHERE WarehouseId = @warehouseId ORDER BY ProductId;",
                new { warehouseId = seed.WarehouseId })).ToArray();
            Assert.Equal(GoodsReceiptStatuses.Posted, stored.Status);
            Assert.NotNull(stored.PostedAtUtc);
            Assert.Equal(seed.UserId, stored.PostedByUserId);
            Assert.Equal(2, ledger.Length);
            Assert.All(ledger, row => Assert.Equal("GoodsReceipt", row.DocumentType));
            Assert.All(ledger, row => Assert.Equal(created.Id, row.DocumentId));
            Assert.Equal(created.Lines.Select(line => line.Id).OrderBy(id => id), ledger.Select(row => row.DocumentLineId));
            Assert.Equal(new[] { 2.5m, 1m }, ledger.Select(row => row.QuantityChange));
            Assert.Equal(2, balances.Length);
            Assert.Equal(2.5m, balances.Single(row => row.ProductId == seed.Product1Id).Quantity);
            Assert.Equal(1m, balances.Single(row => row.ProductId == seed.Product2Id).Quantity);

            using (var balanceProvider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: false,
                onlyPermission: PermissionKeys.InventoryBalancesRead))
            using (var scope = balanceProvider.CreateScope())
            {
                var balanceRead = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new GetStockBalancesQuery(
                        new StockBalanceQuery(
                            seed.WarehouseId,
                            seed.Product1Id,
                            "P-CMD-1",
                            1,
                            25)),
                    CancellationToken.None);
                Assert.False(balanceRead.IsError);
                var balance = Assert.Single(balanceRead.Value.Items);
                Assert.Equal(2.5m, balance.Quantity);
            }

            using (var cardProvider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: false,
                onlyPermission: PermissionKeys.InventoryStockCardRead))
            using (var scope = cardProvider.CreateScope())
            {
                var cardRead = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new GetStockCardQuery(
                        new StockCardQuery(
                            seed.WarehouseId,
                            seed.Product1Id,
                            null,
                            null,
                            1,
                            25)),
                    CancellationToken.None);
                Assert.False(cardRead.IsError);
                var movement = Assert.Single(cardRead.Value.Items);
                Assert.Equal(created.Id, movement.DocumentId);
                Assert.Equal(created.ReceiptNumber, movement.DocumentNumber);
                Assert.Equal(2.5m, movement.QuantityChange);
            }

            using (var scope = postProvider.CreateScope())
            {
                var second = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new PostGoodsReceiptCommand(
                        new PostGoodsReceiptRequest(created.Id, created.Version)),
                    CancellationToken.None);
                Assert.True(second.IsError);
                Assert.Equal("GoodsReceipt.AlreadyPosted", second.FirstError.Code);
            }

            Assert.Equal(2, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
            Assert.Equal(3.5m, await connection.QuerySingleAsync<decimal>("SELECT SUM(Quantity) FROM dbo.StockBalances;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Stale_or_denied_post_leaves_status_ledger_and_balances_unchanged()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            GoodsReceiptDto created;
            GoodsReceiptDto updated;
            using (var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true))
            using (var scope = provider.CreateScope())
            {
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var create = await sender.Send(
                    new CreateGoodsReceiptDraftCommand(Save(seed, "before", seed.Product1Id)),
                    CancellationToken.None);
                created = create.Value;
                var update = await sender.Send(
                    new UpdateGoodsReceiptDraftCommand(
                        new UpdateGoodsReceiptDraftRequest(
                            created.Id,
                            created.Version,
                            created.ReceiptDate,
                            seed.WarehouseId,
                            seed.EmployeeId,
                            "updated",
                            new[] { new SaveGoodsReceiptLineRequest(seed.Product1Id, 2m, 3m) })),
                    CancellationToken.None);
                updated = update.Value;
            }

            using (var postProvider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: false,
                onlyPermission: PermissionKeys.InventoryGoodsReceiptsPost))
            using (var scope = postProvider.CreateScope())
            {
                var stale = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new PostGoodsReceiptCommand(
                        new PostGoodsReceiptRequest(created.Id, created.Version)),
                    CancellationToken.None);
                Assert.True(stale.IsError);
                Assert.Equal("GoodsReceipt.ConcurrencyConflict", stale.FirstError.Code);
            }

            using (var deniedProvider = CreateProvider(database.ConnectionString, seed.UserId, allow: false))
            using (var scope = deniedProvider.CreateScope())
            {
                var denied = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new PostGoodsReceiptCommand(
                        new PostGoodsReceiptRequest(updated.Id, updated.Version)),
                    CancellationToken.None);
                Assert.True(denied.IsError);
                Assert.Equal(ErrorOr.ErrorType.Forbidden, denied.FirstError.Type);
            }

            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(GoodsReceiptStatuses.Draft, await connection.QuerySingleAsync<string>(
                "SELECT Status FROM dbo.GoodsReceipts WHERE ReceiptId = @id;",
                new { id = created.Id }));
            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockBalances;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Inactive_product_blocks_post_without_any_inventory_mutation()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            GoodsReceiptDto created;
            using (var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true))
            using (var scope = provider.CreateScope())
            {
                var save = new SaveGoodsReceiptRequest(
                    new DateTime(2026, 10, 10),
                    seed.WarehouseId,
                    seed.EmployeeId,
                    "inactive",
                    new[]
                    {
                        new SaveGoodsReceiptLineRequest(seed.Product2Id, 1m, 2m),
                        new SaveGoodsReceiptLineRequest(seed.Product1Id, 1m, 2m),
                    });
                var create = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new CreateGoodsReceiptDraftCommand(save),
                    CancellationToken.None);
                created = create.Value;
            }

            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.ExecuteAsync(
                    "UPDATE dbo.Products SET IsActive = 0 WHERE ProductId = @id;",
                    new { id = seed.Product1Id });
            }

            using (var provider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: false,
                onlyPermission: PermissionKeys.InventoryGoodsReceiptsPost))
            using (var scope = provider.CreateScope())
            {
                var post = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new PostGoodsReceiptCommand(
                        new PostGoodsReceiptRequest(created.Id, created.Version)),
                    CancellationToken.None);
                Assert.True(post.IsError);
                Assert.Equal("GoodsReceipt.ProductInactive", post.FirstError.Code);
                Assert.NotNull(post.FirstError.Metadata);
                Assert.Equal("Request.Lines[1].ProductId", post.FirstError.Metadata["Field"]);
            }

            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(GoodsReceiptStatuses.Draft, await verification.QuerySingleAsync<string>(
                "SELECT Status FROM dbo.GoodsReceipts WHERE ReceiptId = @id;",
                new { id = created.Id }));
            Assert.Equal(0, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
            Assert.Equal(0, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockBalances;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Ledger_failure_rolls_back_posted_status_and_balance_changes()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            GoodsReceiptDto created;
            using (var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true))
            using (var scope = provider.CreateScope())
            {
                var create = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new CreateGoodsReceiptDraftCommand(Save(seed, "rollback", seed.Product1Id)),
                    CancellationToken.None);
                created = create.Value;
            }

            using (var setup = new SqlConnection(database.ConnectionString))
            {
                await setup.ExecuteAsync(
                    "INSERT dbo.StockLedger " +
                    "(WarehouseId, ProductId, DocumentType, DocumentId, DocumentLineId, QuantityChange, PostedAtUtc, PostedByUserId) " +
                    "VALUES (@warehouseId, @productId, 'GoodsReceipt', 999, @lineId, 1, SYSUTCDATETIME(), @userId);",
                    new
                    {
                        warehouseId = seed.WarehouseId,
                        productId = seed.Product1Id,
                        lineId = created.Lines.Single().Id,
                        userId = seed.UserId,
                    });
            }

            using (var provider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: false,
                onlyPermission: PermissionKeys.InventoryGoodsReceiptsPost))
            using (var scope = provider.CreateScope())
            {
                var failed = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new PostGoodsReceiptCommand(
                        new PostGoodsReceiptRequest(created.Id, created.Version)),
                    CancellationToken.None);
                Assert.True(failed.IsError);
                Assert.Equal("InternalError", failed.FirstError.Code);
            }

            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(GoodsReceiptStatuses.Draft, await verification.QuerySingleAsync<string>(
                "SELECT Status FROM dbo.GoodsReceipts WHERE ReceiptId = @id;",
                new { id = created.Id }));
            Assert.Equal(1, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
            Assert.Equal(0, await verification.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockBalances;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Reconciliation_reports_no_transient_difference_while_receipts_post_concurrently()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            using var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true);
            var receipts = new GoodsReceiptDto[16];
            for (var index = 0; index < receipts.Length; index++)
            {
                using var createScope = provider.CreateScope();
                var productId = index % 2 == 0 ? seed.Product1Id : seed.Product2Id;
                var create = await createScope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new CreateGoodsReceiptDraftCommand(
                        Save(seed, "concurrent-post-" + index, productId)),
                    CancellationToken.None);
                Assert.False(create.IsError);
                receipts[index] = create.Value;
            }

            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var postTasks = receipts
                .Select(receipt => PostAfterSignalInOwnScopeAsync(provider, receipt, start.Task))
                .ToArray();
            var allPosts = Task.WhenAll(postTasks);
            start.SetResult(true);

            using var reconciliationScope = provider.CreateScope();
            var reconciliation = reconciliationScope.ServiceProvider
                .GetRequiredService<IStockReconciliationQueryService>();
            var snapshots = 0;
            do
            {
                var differences = await reconciliation.FindDifferencesAsync(CancellationToken.None);
                Assert.Empty(differences);
                snapshots++;
            }
            while (!allPosts.IsCompleted && snapshots < 50);

            var results = await allPosts;
            Assert.All(results, result => Assert.False(result.IsError));
            Assert.Empty(await reconciliation.FindDifferencesAsync(CancellationToken.None));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Concurrent_posts_for_same_receipt_commit_exactly_once()
    {
        var database = await CreateDatabaseAsync();
        SqlConcurrencyGate? gate = null;
        ServiceProvider? provider = null;
        Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>>? first = null;
        Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>>? second = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await EnsureCrossSessionDmvPermissionAsync(database.ConnectionString);
            var seed = await SeedAsync(database.ConnectionString);
            var recordingConnectionFactory = new SessionRecordingConnectionFactory(database.ConnectionString);
            provider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: true,
                connectionFactory: recordingConnectionFactory);
            GoodsReceiptDto created;
            using (var scope = provider.CreateScope())
            {
                var create = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new CreateGoodsReceiptDraftCommand(Save(seed, "same-receipt", seed.Product1Id)),
                    CancellationToken.None);
                Assert.False(create.IsError);
                created = create.Value;
            }

            gate = await SqlConcurrencyGate.AcquireAsync(
                database.ConnectionString,
                "same-receipt",
                CancellationToken.None);
            await CreateGoodsReceiptGateTriggerAsync(
                database.ConnectionString,
                gate,
                "EXISTS (SELECT 1 FROM inserted WHERE ReceiptId = " + created.Id + " AND Status = 'Posted')");
            recordingConnectionFactory.Clear();

            first = PostInOwnScopeAsync(provider, created, cancellation.Token);
            var firstSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForHolderAsync(
                firstSessionId,
                "dbo.GoodsReceipts",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            second = PostInOwnScopeAsync(provider, created, cancellation.Token);
            var secondSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForBusinessBlockAsync(
                secondSessionId,
                firstSessionId,
                "dbo.GoodsReceipts",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            Assert.False(second.IsCompleted);

            await gate.ReleaseAsync(CancellationToken.None);
            var results = await AwaitWithTimeoutAsync(Task.WhenAll(first, second));

            Assert.Single(results, result => !result.IsError);
            var rejected = Assert.Single(results, result => result.IsError);
            Assert.Equal(ErrorOr.ErrorType.Conflict, rejected.FirstError.Type);
            Assert.Equal("GoodsReceipt.AlreadyPosted", rejected.FirstError.Code);

            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentId = @receiptId;",
                new { receiptId = created.Id }));
            Assert.Equal(1m, await connection.QuerySingleAsync<decimal>(
                "SELECT Quantity FROM dbo.StockBalances WHERE WarehouseId = @warehouseId AND ProductId = @productId;",
                new { warehouseId = seed.WarehouseId, productId = seed.Product1Id }));
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                cancellation,
                provider,
                database.DisposeAsync,
                first,
                second);
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Update_that_locks_first_wins_and_post_rejects_the_stale_snapshot()
    {
        var database = await CreateDatabaseAsync();
        SqlConcurrencyGate? gate = null;
        ServiceProvider? provider = null;
        Task<ErrorOr.ErrorOr<GoodsReceiptDto>>? update = null;
        Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>>? post = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await EnsureCrossSessionDmvPermissionAsync(database.ConnectionString);
            var seed = await SeedAsync(database.ConnectionString);
            var recordingConnectionFactory = new SessionRecordingConnectionFactory(database.ConnectionString);
            provider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: true,
                connectionFactory: recordingConnectionFactory);
            var created = await CreateReceiptAsync(provider, seed, "initial", seed.Product1Id, 1m);
            gate = await SqlConcurrencyGate.AcquireAsync(
                database.ConnectionString,
                "update-first",
                CancellationToken.None);
            await CreateGoodsReceiptGateTriggerAsync(
                database.ConnectionString,
                gate,
                "EXISTS (SELECT 1 FROM inserted WHERE Note = N'update-wins')");
            recordingConnectionFactory.Clear();

            update = UpdateInOwnScopeAsync(
                provider, created, seed, "update-wins", 7m, cancellation.Token);
            var updateSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForHolderAsync(
                updateSessionId,
                "dbo.GoodsReceipts",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            post = PostInOwnScopeAsync(provider, created, cancellation.Token);
            var postSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForBusinessBlockAsync(
                postSessionId,
                updateSessionId,
                "dbo.GoodsReceipts",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            await gate.ReleaseAsync(CancellationToken.None);
            var results = await AwaitWithTimeoutAsync(Task.WhenAll(
                ObserveUpdateAsync(update),
                ObservePostAsync(post)));

            Assert.False(results[0].IsError);
            Assert.True(results[1].IsError);
            Assert.Equal("GoodsReceipt.ConcurrencyConflict", results[1].ErrorCode);

            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(GoodsReceiptStatuses.Draft, await connection.QuerySingleAsync<string>(
                "SELECT Status FROM dbo.GoodsReceipts WHERE ReceiptId = @receiptId;",
                new { receiptId = created.Id }));
            Assert.Equal(7m, await connection.QuerySingleAsync<decimal>(
                "SELECT Quantity FROM dbo.GoodsReceiptLines WHERE ReceiptId = @receiptId;",
                new { receiptId = created.Id }));
            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockBalances;"));
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                cancellation,
                provider,
                database.DisposeAsync,
                update,
                post);
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Post_that_locks_first_wins_and_update_cannot_replace_posted_lines()
    {
        var database = await CreateDatabaseAsync();
        SqlConcurrencyGate? gate = null;
        ServiceProvider? provider = null;
        Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>>? post = null;
        Task<ErrorOr.ErrorOr<GoodsReceiptDto>>? update = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await EnsureCrossSessionDmvPermissionAsync(database.ConnectionString);
            var seed = await SeedAsync(database.ConnectionString);
            var recordingConnectionFactory = new SessionRecordingConnectionFactory(database.ConnectionString);
            provider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: true,
                connectionFactory: recordingConnectionFactory);
            var created = await CreateReceiptAsync(provider, seed, "initial", seed.Product1Id, 3m);
            gate = await SqlConcurrencyGate.AcquireAsync(
                database.ConnectionString,
                "post-first",
                CancellationToken.None);
            await CreateGoodsReceiptGateTriggerAsync(
                database.ConnectionString,
                gate,
                "EXISTS (SELECT 1 FROM inserted WHERE Status = 'Posted')");
            recordingConnectionFactory.Clear();

            post = PostInOwnScopeAsync(provider, created, cancellation.Token);
            var postSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForHolderAsync(
                postSessionId,
                "dbo.GoodsReceipts",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            update = UpdateInOwnScopeAsync(
                provider, created, seed, "late-update", 9m, cancellation.Token);
            var updateSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForBusinessBlockAsync(
                updateSessionId,
                postSessionId,
                "dbo.GoodsReceipts",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            await gate.ReleaseAsync(CancellationToken.None);
            var results = await AwaitWithTimeoutAsync(Task.WhenAll(
                ObservePostAsync(post),
                ObserveUpdateAsync(update)));

            Assert.False(results[0].IsError);
            Assert.True(results[1].IsError);
            Assert.Equal("GoodsReceipt.AlreadyPosted", results[1].ErrorCode);

            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(GoodsReceiptStatuses.Posted, await connection.QuerySingleAsync<string>(
                "SELECT Status FROM dbo.GoodsReceipts WHERE ReceiptId = @receiptId;",
                new { receiptId = created.Id }));
            Assert.Equal("initial", await connection.QuerySingleAsync<string>(
                "SELECT Note FROM dbo.GoodsReceipts WHERE ReceiptId = @receiptId;",
                new { receiptId = created.Id }));
            Assert.Equal(3m, await connection.QuerySingleAsync<decimal>(
                "SELECT Quantity FROM dbo.GoodsReceiptLines WHERE ReceiptId = @receiptId;",
                new { receiptId = created.Id }));
            Assert.Equal(3m, await connection.QuerySingleAsync<decimal>(
                "SELECT QuantityChange FROM dbo.StockLedger WHERE DocumentId = @receiptId;",
                new { receiptId = created.Id }));
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                cancellation,
                provider,
                database.DisposeAsync,
                post,
                update);
        }
    }

    [SqlServerFact]
    public async Task Every_inactive_posting_reference_rejects_post_without_inventory_mutation()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            using var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true);
            var cases = new[]
            {
                new InactiveReferenceCase("Warehouses", "WarehouseId", seed.WarehouseId, "GoodsReceipt.WarehouseInactive"),
                new InactiveReferenceCase("Employees", "EmployeeId", seed.EmployeeId, "GoodsReceipt.EmployeeInactive"),
                new InactiveReferenceCase("Products", "ProductId", seed.Product1Id, "GoodsReceipt.ProductInactive"),
            };

            foreach (var item in cases)
            {
                var created = await CreateReceiptAsync(provider, seed, "inactive-" + item.TableName, seed.Product1Id, 1m);
                await SetReferenceActiveAsync(database.ConnectionString, item, isActive: false);
                var post = await PostInOwnScopeAsync(provider, created);

                Assert.True(post.IsError);
                Assert.Equal(item.ExpectedErrorCode, post.FirstError.Code);
                await AssertDraftHasNoInventoryAsync(database.ConnectionString, created.Id);
                using (var verification = new SqlConnection(database.ConnectionString))
                {
                    Assert.Equal(0, await verification.QuerySingleAsync<int>(
                        "SELECT COUNT(*) FROM dbo.StockBalances;"));
                }

                await SetReferenceActiveAsync(database.ConnectionString, item, isActive: true);
            }
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Reactivated_reference_allows_the_original_draft_to_post_once()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            using var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true);
            var created = await CreateReceiptAsync(
                provider,
                seed,
                "reactivation",
                seed.Product1Id,
                4m);
            var product = new InactiveReferenceCase(
                "Products",
                "ProductId",
                seed.Product1Id,
                "GoodsReceipt.ProductInactive");

            await SetReferenceActiveAsync(database.ConnectionString, product, isActive: false);
            var blocked = await PostInOwnScopeAsync(provider, created);
            Assert.True(blocked.IsError);
            Assert.Equal("GoodsReceipt.ProductInactive", blocked.FirstError.Code);
            await AssertDraftHasNoInventoryAsync(database.ConnectionString, created.Id);

            await SetReferenceActiveAsync(database.ConnectionString, product, isActive: true);
            var posted = await PostInOwnScopeAsync(provider, created);
            var duplicate = await PostInOwnScopeAsync(provider, created);

            Assert.False(posted.IsError);
            Assert.True(duplicate.IsError);
            Assert.Equal("GoodsReceipt.AlreadyPosted", duplicate.FirstError.Code);
            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentId = @receiptId;",
                new { receiptId = created.Id }));
            Assert.Equal(4m, await connection.QuerySingleAsync<decimal>(
                "SELECT Quantity FROM dbo.StockBalances " +
                "WHERE WarehouseId = @warehouseId AND ProductId = @productId;",
                new { warehouseId = seed.WarehouseId, productId = seed.Product1Id }));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Deactivation_and_post_serialize_in_both_lock_orders_for_every_reference()
    {
        var database = await CreateDatabaseAsync();
        var cleanupIncomplete = false;
        ServiceProvider? provider = null;
        using var cleanupCancellation = new CancellationTokenSource();
        try
        {
            await EnsureCrossSessionDmvPermissionAsync(database.ConnectionString);
            var seed = await SeedAsync(database.ConnectionString);
            var recordingConnectionFactory = new SessionRecordingConnectionFactory(database.ConnectionString);
            provider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: true,
                connectionFactory: recordingConnectionFactory);
            var cases = new[]
            {
                new InactiveReferenceCase("Warehouses", "WarehouseId", seed.WarehouseId, "GoodsReceipt.WarehouseInactive"),
                new InactiveReferenceCase("Employees", "EmployeeId", seed.EmployeeId, "GoodsReceipt.EmployeeInactive"),
                new InactiveReferenceCase("Products", "ProductId", seed.Product1Id, "GoodsReceipt.ProductInactive"),
            };

            foreach (var item in cases)
            {
                var deactivationFirst = await CreateReceiptAsync(
                    provider, seed, "deactivate-first-" + item.TableName, seed.Product1Id, 1m);
                await RunDeactivationFirstScenarioAsync(
                    database.ConnectionString,
                    provider,
                    recordingConnectionFactory,
                    seed,
                    item,
                    deactivationFirst);

                var postFirst = await CreateReceiptAsync(
                    provider, seed, "post-first-" + item.TableName, seed.Product1Id, 2m);
                await RunPostFirstScenarioAsync(
                    database.ConnectionString,
                    provider,
                    recordingConnectionFactory,
                    seed,
                    item,
                    postFirst);
            }
        }
        catch (SqlConcurrencyCleanupIncompleteException)
        {
            cleanupIncomplete = true;
            throw;
        }
        finally
        {
            if (!cleanupIncomplete)
            {
                await SqlConcurrencyCleanup.CompleteAsync(
                    gate: null,
                    cleanupCancellation,
                    provider,
                    database.DisposeAsync);
            }
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Concurrent_receipt_posts_serialize_on_catalog_locks_and_preserve_the_balance_sum()
    {
        var database = await CreateDatabaseAsync();
        SqlConcurrencyGate? gate = null;
        ServiceProvider? provider = null;
        Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>>? firstPost = null;
        Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>>? secondPost = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await EnsureCrossSessionDmvPermissionAsync(database.ConnectionString);
            var seed = await SeedAsync(database.ConnectionString);
            var recordingConnectionFactory = new SessionRecordingConnectionFactory(database.ConnectionString);
            provider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: true,
                connectionFactory: recordingConnectionFactory);
            var first = await CreateReceiptAsync(provider, seed, "balance-one", seed.Product1Id, 4m);
            var second = await CreateReceiptAsync(provider, seed, "balance-two", seed.Product1Id, 6m);
            gate = await SqlConcurrencyGate.AcquireAsync(
                database.ConnectionString,
                "catalog-lock-order",
                CancellationToken.None);
            await CreateStockBalanceGateTriggerAsync(
                database.ConnectionString,
                gate,
                "EXISTS (SELECT 1 FROM inserted WHERE WarehouseId = " + seed.WarehouseId +
                " AND ProductId = " + seed.Product1Id + " AND Quantity = 4)");
            recordingConnectionFactory.Clear();

            firstPost = PostInOwnScopeAsync(provider, first, cancellation.Token);
            var firstSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForHolderAsync(
                firstSessionId,
                "dbo.Warehouses",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            secondPost = PostInOwnScopeAsync(provider, second, cancellation.Token);
            var secondSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForBusinessBlockAsync(
                secondSessionId,
                firstSessionId,
                "dbo.Warehouses",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            Assert.False(secondPost.IsCompleted);

            await gate.ReleaseAsync(CancellationToken.None);
            var results = await AwaitWithTimeoutAsync(Task.WhenAll(firstPost, secondPost));

            Assert.All(results, result => Assert.False(result.IsError));
            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(10m, await connection.QuerySingleAsync<decimal>(
                "SELECT Quantity FROM dbo.StockBalances WHERE WarehouseId = @warehouseId AND ProductId = @productId;",
                new { warehouseId = seed.WarehouseId, productId = seed.Product1Id }));
            Assert.Equal(10m, await connection.QuerySingleAsync<decimal>(
                "SELECT SUM(QuantityChange) FROM dbo.StockLedger WHERE WarehouseId = @warehouseId AND ProductId = @productId;",
                new { warehouseId = seed.WarehouseId, productId = seed.Product1Id }));
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                cancellation,
                provider,
                database.DisposeAsync,
                firstPost,
                secondPost);
        }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Concurrent_balance_upserts_block_on_StockBalances_and_preserve_the_sum()
    {
        var database = await CreateDatabaseAsync();
        SqlConcurrencyGate? gate = null;
        ServiceProvider? provider = null;
        Task? first = null;
        Task? second = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await EnsureCrossSessionDmvPermissionAsync(database.ConnectionString);
            var seed = await SeedAsync(database.ConnectionString);
            var recordingConnectionFactory = new SessionRecordingConnectionFactory(database.ConnectionString);
            provider = CreateProvider(
                database.ConnectionString,
                seed.UserId,
                allow: true,
                connectionFactory: recordingConnectionFactory);
            gate = await SqlConcurrencyGate.AcquireAsync(
                database.ConnectionString,
                "stock-balance-upsert",
                CancellationToken.None);
            await CreateStockBalanceGateTriggerAsync(
                database.ConnectionString,
                gate,
                "EXISTS (SELECT 1 FROM inserted WHERE WarehouseId = " + seed.WarehouseId +
                " AND ProductId = " + seed.Product1Id + " AND Quantity = 4)");
            recordingConnectionFactory.Clear();

            first = ApplyBalanceDeltaInOwnUnitOfWorkAsync(
                provider, seed.WarehouseId, seed.Product1Id, 4m, cancellation.Token);
            var firstSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForHolderAsync(
                firstSessionId,
                "dbo.StockBalances",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            second = ApplyBalanceDeltaInOwnUnitOfWorkAsync(
                provider, seed.WarehouseId, seed.Product1Id, 6m, cancellation.Token);
            var secondSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForBusinessBlockAsync(
                secondSessionId,
                firstSessionId,
                "dbo.StockBalances",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            Assert.False(second.IsCompleted);

            await gate.ReleaseAsync(CancellationToken.None);
            await AwaitWithTimeoutAsync(Task.WhenAll(first, second));

            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.StockBalances WHERE WarehouseId = @warehouseId AND ProductId = @productId;",
                new { warehouseId = seed.WarehouseId, productId = seed.Product1Id }));
            Assert.Equal(10m, await connection.QuerySingleAsync<decimal>(
                "SELECT Quantity FROM dbo.StockBalances WHERE WarehouseId = @warehouseId AND ProductId = @productId;",
                new { warehouseId = seed.WarehouseId, productId = seed.Product1Id }));
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                cancellation,
                provider,
                database.DisposeAsync,
                first,
                second);
        }
    }

    [SqlServerFact]
    public async Task Failure_after_ledger_insert_rolls_back_status_ledger_and_balance()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            using var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true);
            var created = await CreateReceiptAsync(provider, seed, "balance-failure", seed.Product1Id, 5m);
            using (var setup = new SqlConnection(database.ConnectionString))
            {
                await setup.ExecuteAsync(
                    "CREATE TRIGGER dbo.TR_StockBalances_TestFailure ON dbo.StockBalances " +
                    "AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; " +
                    "THROW 51002, 'Injected failure after ledger insertion.', 1; END;");
            }

            var failed = await PostInOwnScopeAsync(provider, created);

            Assert.True(failed.IsError);
            Assert.Equal("InternalError", failed.FirstError.Code);
            await AssertDraftHasNoInventoryAsync(database.ConnectionString, created.Id);
            using var verification = new SqlConnection(database.ConnectionString);
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.StockBalances;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Failed_duplicate_product_insert_rolls_back_the_aggregate_and_burns_its_sequence_value()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            using var provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true);
            var header = new GoodsReceipt { ReceiptDate = new DateTime(2026, 10, 18), WarehouseId = seed.WarehouseId, EmployeeId = seed.EmployeeId, CreatedByUserId = seed.UserId };
            using (var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create())
            {
                uow.BeginTransaction();
                var lines = new[]
                {
                    new GoodsReceiptLine { LineNumber = 1, ProductId = seed.Product1Id, Quantity = .0001m, UnitCost = 0m, CreatedByUserId = seed.UserId },
                    new GoodsReceiptLine { LineNumber = 2, ProductId = seed.Product1Id, Quantity = 1m, UnitCost = 1m, CreatedByUserId = seed.UserId },
                };
                var error = await Assert.ThrowsAsync<SqlException>(() => uow.Repository<IGoodsReceiptWriteRepository>().InsertDraftAsync(header, lines, CancellationToken.None));
                Assert.Contains(error.Number, new[] { 2601, 2627 });
                Assert.True(header.Id > 0); Assert.True(lines[0].Id > 0);
                uow.Rollback();
            }
            using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
            Assert.Equal(0, await Pilot.PilotSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.GoodsReceipts"));
            Assert.Equal(0, await Pilot.PilotSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.GoodsReceiptLines"));
            var saved = await CreateInOwnScopeAsync(provider, seed, "after-rollback"); Assert.False(saved.IsError);
            var consumed = long.Parse(header.ReceiptNumber.Substring(2), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal("GR" + (consumed + 1).ToString("D10", System.Globalization.CultureInfo.InvariantCulture), saved.Value.ReceiptNumber);
        }
        finally { await database.DisposeAsync(); }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public async Task Canceling_a_blocked_line_insert_rolls_back_the_aggregate_and_allows_a_fresh_scope()
    {
        var database = await CreateDatabaseAsync();
        SqlConcurrencyGate? gate = null; ServiceProvider? provider = null;
        Task<ErrorOr.ErrorOr<GoodsReceiptDto>>? worker = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await EnsureCrossSessionDmvPermissionAsync(database.ConnectionString);
            var seed = await SeedAsync(database.ConnectionString);
            var connections = new SessionRecordingConnectionFactory(database.ConnectionString);
            var reporter = new CancellationExceptionReporter();
            provider = CreateProvider(database.ConnectionString, seed.UserId, allow: true, connectionFactory: connections, reporter: reporter);
            gate = await SqlConcurrencyGate.AcquireAsync(database.ConnectionString, "cancel-line-insert", CancellationToken.None);
            using (var setup = new SqlConnection(database.ConnectionString))
            {
                await setup.OpenAsync();
                using var trigger = new SqlCommand("CREATE TRIGGER dbo.TR_TestLineCancellation ON dbo.GoodsReceiptLines AFTER INSERT AS BEGIN SET NOCOUNT ON; " + gate.TransactionWaitSql + " END;", setup);
                await trigger.ExecuteNonQueryAsync();
            }
            connections.Clear();
            worker = CreateWithCancellationAsync(provider, seed, cancellation.Token);
            var session = await connections.TakeNextSessionIdAsync();
            await gate.WaitForHolderAsync(session, "dbo.GoodsReceiptLines", TimeSpan.FromSeconds(10), CancellationToken.None);
            Assert.False(worker.IsCompleted);
            cancellation.Cancel();
            // SqlClient/net48 can cancel with SqlException (as in the existing Foundation
            // cancellation contract) or OperationCanceledException; preserve the pipeline semantics.
            ErrorOr.ErrorOr<GoodsReceiptDto> cancelled = default;
            var cancellationError = await Record.ExceptionAsync(async () => { cancelled = await AwaitWithTimeoutAsync(worker); });
            if (cancellationError != null) Assert.IsAssignableFrom<OperationCanceledException>(cancellationError);
            else
            {
                Assert.True(cancelled.IsError); Assert.Equal("InternalError", cancelled.FirstError.Code);
                var sqlError = Assert.IsType<SqlException>(reporter.Error);
                Assert.Contains(sqlError.Errors.Cast<SqlError>(), error => error.Number == 0);
            }
            Assert.True(cancellation.IsCancellationRequested);
            await gate.ReleaseAsync(CancellationToken.None);
            using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
            foreach (var table in new[] { "GoodsReceipts", "GoodsReceiptLines", "StockLedger", "StockBalances" })
                Assert.Equal(0, await Pilot.PilotSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo." + table));
            await Pilot.PilotSql.Execute(oracle, null, "DROP TRIGGER dbo.TR_TestLineCancellation;");
            var saved = await CreateInOwnScopeAsync(provider, seed, "after-cancel"); Assert.False(saved.IsError);
            Assert.Equal(1, await Pilot.PilotSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.GoodsReceipts"));
            Assert.Equal(1, await Pilot.PilotSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.GoodsReceiptLines"));
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(gate, cancellation, provider, database.DisposeAsync, worker);
        }
    }

    private static async Task<ErrorOr.ErrorOr<GoodsReceiptDto>> CreateWithCancellationAsync(ServiceProvider provider, SeedData seed, CancellationToken token)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateGoodsReceiptDraftCommand(Save(seed, "cancel", seed.Product1Id)), token);
    }

    private static async Task<GoodsReceiptDto> CreateReceiptAsync(
        ServiceProvider provider,
        SeedData seed,
        string note,
        int productId,
        decimal quantity)
    {
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateGoodsReceiptDraftCommand(
                new SaveGoodsReceiptRequest(
                    new DateTime(2026, 10, 10),
                    seed.WarehouseId,
                    seed.EmployeeId,
                    note,
                    new[] { new SaveGoodsReceiptLineRequest(productId, quantity, 2m) })),
            CancellationToken.None);
        Assert.False(result.IsError);
        return result.Value;
    }

    private static async Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>> PostInOwnScopeAsync(
        ServiceProvider provider,
        GoodsReceiptDto receipt,
        CancellationToken cancellationToken = default)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new PostGoodsReceiptCommand(
                new PostGoodsReceiptRequest(receipt.Id, receipt.Version)),
            cancellationToken);
    }

    private static async Task RunDeactivationFirstScenarioAsync(
        string connectionString,
        ServiceProvider provider,
        SessionRecordingConnectionFactory recordingConnectionFactory,
        SeedData seed,
        InactiveReferenceCase reference,
        GoodsReceiptDto receipt)
    {
        SqlConcurrencyGate? gate = null;
        Task? deactivate = null;
        Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>>? blockedPost = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            gate = await SqlConcurrencyGate.AcquireAsync(
                connectionString,
                "deactivate-first-" + reference.TableName,
                CancellationToken.None);
            await CreateReferenceGateTriggerAsync(connectionString, reference, gate);
            recordingConnectionFactory.Clear();
            var holderSession = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            deactivate = SetReferenceActiveAsync(
                connectionString,
                reference,
                isActive: false,
                holderSession,
                cancellation.Token);
            var holderSessionId = await AwaitWithTimeoutAsync(holderSession.Task);
            await gate.WaitForHolderAsync(
                holderSessionId,
                "dbo." + reference.TableName,
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            var balanceBeforeRejectedPost = await GetBalanceQuantityAsync(
                connectionString,
                seed.WarehouseId,
                seed.Product1Id);

            blockedPost = PostInOwnScopeAsync(provider, receipt, cancellation.Token);
            var blockedPostSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForBusinessBlockAsync(
                blockedPostSessionId,
                holderSessionId,
                "dbo." + reference.TableName,
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            Assert.False(blockedPost.IsCompleted);

            await gate.ReleaseAsync(CancellationToken.None);
            await AwaitWithTimeoutAsync(Task.WhenAll(deactivate, ObserveCompletionAsync(blockedPost)));
            var rejected = await blockedPost;
            Assert.True(rejected.IsError);
            Assert.Equal(reference.ExpectedErrorCode, rejected.FirstError.Code);
            await AssertDraftHasNoInventoryAsync(connectionString, receipt.Id);
            Assert.Equal(
                balanceBeforeRejectedPost,
                await GetBalanceQuantityAsync(connectionString, seed.WarehouseId, seed.Product1Id));
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                cancellation,
                async () =>
                {
                    await DropTriggerAsync(connectionString, "TR_TestReferenceSignal");
                    await SetReferenceActiveAsync(connectionString, reference, isActive: true);
                },
                deactivate,
                blockedPost);
        }
    }

    private static async Task RunPostFirstScenarioAsync(
        string connectionString,
        ServiceProvider provider,
        SessionRecordingConnectionFactory recordingConnectionFactory,
        SeedData seed,
        InactiveReferenceCase reference,
        GoodsReceiptDto receipt)
    {
        SqlConcurrencyGate? gate = null;
        Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>>? winningPost = null;
        Task? blockedDeactivate = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            gate = await SqlConcurrencyGate.AcquireAsync(
                connectionString,
                "post-first-" + reference.TableName,
                CancellationToken.None);
            await CreateGoodsReceiptGateTriggerAsync(
                connectionString,
                gate,
                "EXISTS (SELECT 1 FROM inserted WHERE ReceiptId = " + receipt.Id + " AND Status = 'Posted')");
            recordingConnectionFactory.Clear();
            winningPost = PostInOwnScopeAsync(provider, receipt, cancellation.Token);
            var holderSessionId = await recordingConnectionFactory.TakeNextSessionIdAsync();
            await gate.WaitForHolderAsync(
                holderSessionId,
                "dbo." + reference.TableName,
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            var balanceBeforeWinningPost = await GetBalanceQuantityAsync(
                connectionString,
                seed.WarehouseId,
                seed.Product1Id);

            var deactivationSession = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            blockedDeactivate = SetReferenceActiveAsync(
                connectionString,
                reference,
                isActive: false,
                deactivationSession,
                cancellation.Token);
            var blockedDeactivationSessionId = await AwaitWithTimeoutAsync(deactivationSession.Task);
            await gate.WaitForBusinessBlockAsync(
                blockedDeactivationSessionId,
                holderSessionId,
                "dbo." + reference.TableName,
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            Assert.False(blockedDeactivate.IsCompleted);

            await gate.ReleaseAsync(CancellationToken.None);
            await AwaitWithTimeoutAsync(Task.WhenAll(ObserveCompletionAsync(winningPost), blockedDeactivate));
            var posted = await winningPost;
            Assert.False(posted.IsError);
            using (var connection = new SqlConnection(connectionString))
            {
                Assert.Equal(0, await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM dbo." + reference.TableName + " WHERE " + reference.KeyColumn +
                    " = @id AND IsActive = 1;",
                    new { id = reference.Id }));
                Assert.Equal(2m, await connection.QuerySingleAsync<decimal>(
                    "SELECT QuantityChange FROM dbo.StockLedger WHERE DocumentId = @receiptId;",
                    new { receiptId = receipt.Id }));
            }

            Assert.Equal(
                balanceBeforeWinningPost + 2m,
                await GetBalanceQuantityAsync(connectionString, seed.WarehouseId, seed.Product1Id));
        }
        finally
        {
            await SqlConcurrencyCleanup.CompleteAsync(
                gate,
                cancellation,
                async () =>
                {
                    await DropTriggerAsync(connectionString, "TR_TestGoodsReceiptSignal");
                    await SetReferenceActiveAsync(connectionString, reference, isActive: true);
                },
                winningPost,
                blockedDeactivate);
        }
    }

    private static async Task ApplyBalanceDeltaInOwnUnitOfWorkAsync(
        ServiceProvider provider,
        int warehouseId,
        int productId,
        decimal delta,
        CancellationToken cancellationToken = default)
    {
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
        using var unitOfWork = await factory.CreateAsync(cancellationToken);
        unitOfWork.BeginTransaction();
        await unitOfWork.Repository<IGoodsReceiptWriteRepository>().ApplyStockBalanceDeltasAsync(
            warehouseId,
            new Dictionary<int, decimal> { [productId] = delta },
            cancellationToken);
        unitOfWork.Commit();
    }

    private static async Task<ObservedOperation> ObservePostAsync(
        Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>> task)
    {
        var result = await task;
        return new ObservedOperation(result.IsError, result.IsError ? result.FirstError.Code : null);
    }

    private static async Task<ObservedOperation> ObserveUpdateAsync(
        Task<ErrorOr.ErrorOr<GoodsReceiptDto>> task)
    {
        var result = await task;
        return new ObservedOperation(result.IsError, result.IsError ? result.FirstError.Code : null);
    }

    private static async Task ObserveCompletionAsync<T>(Task<T> task) =>
        await task;

    private static async Task<T> AwaitWithTimeoutAsync<T>(Task<T> task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(15)));
        if (!ReferenceEquals(completed, task))
        {
            throw new TimeoutException("The SQL concurrency scenario did not complete within 15 seconds.");
        }

        return await task;
    }

    private static async Task AwaitWithTimeoutAsync(Task task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(15)));
        if (!ReferenceEquals(completed, task))
        {
            throw new TimeoutException("The SQL concurrency scenario did not complete within 15 seconds.");
        }

        await task;
    }

    private static async Task CreateGoodsReceiptGateTriggerAsync(
        string connectionString,
        SqlConcurrencyGate gate,
        string condition)
    {
        await DropTriggerAsync(connectionString, "TR_TestGoodsReceiptSignal");
        using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(
            "CREATE TRIGGER dbo.TR_TestGoodsReceiptSignal ON dbo.GoodsReceipts AFTER UPDATE AS " +
            "BEGIN SET NOCOUNT ON; IF (" + condition + ") BEGIN " +
            gate.TransactionWaitSql + " END; END;");
    }

    private static async Task CreateReferenceGateTriggerAsync(
        string connectionString,
        InactiveReferenceCase reference,
        SqlConcurrencyGate gate)
    {
        await DropTriggerAsync(connectionString, "TR_TestReferenceSignal");
        using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(
            "CREATE TRIGGER dbo.TR_TestReferenceSignal ON dbo." + reference.TableName + " AFTER UPDATE AS " +
            "BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM inserted WHERE " + reference.KeyColumn +
            " = " + reference.Id + " AND IsActive = 0) BEGIN " +
            gate.TransactionWaitSql + " END; END;");
    }

    private static async Task CreateStockBalanceGateTriggerAsync(
        string connectionString,
        SqlConcurrencyGate gate,
        string condition)
    {
        await DropTriggerAsync(connectionString, "TR_TestStockBalanceSignal");
        using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(
            "CREATE TRIGGER dbo.TR_TestStockBalanceSignal ON dbo.StockBalances AFTER INSERT, UPDATE AS " +
            "BEGIN SET NOCOUNT ON; IF (" + condition + ") BEGIN " +
            gate.TransactionWaitSql + " END; END;");
    }

    private static async Task DropTriggerAsync(string connectionString, string triggerName)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync("DROP TRIGGER IF EXISTS dbo." + triggerName + ";");
    }

    private static async Task EnsureCrossSessionDmvPermissionAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        var majorVersion = await connection.QuerySingleAsync<int>(
            "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion'));");
        var requiredPermission = GetRequiredCrossSessionDmvPermission(majorVersion);
        var hasPermission = await connection.QuerySingleAsync<int?>(
            "SELECT HAS_PERMS_BY_NAME(NULL, NULL, @requiredPermission);",
            new { requiredPermission });

        ThrowIfCrossSessionDmvPermissionMissing(majorVersion, hasPermission == 1);
    }

    private static string GetRequiredCrossSessionDmvPermission(int majorVersion) =>
        majorVersion >= 16 ? "VIEW SERVER PERFORMANCE STATE" : "VIEW SERVER STATE";

    private static void ThrowIfCrossSessionDmvPermissionMissing(int majorVersion, bool hasPermission)
    {
        if (hasPermission)
        {
            return;
        }

        var requiredPermission = GetRequiredCrossSessionDmvPermission(majorVersion);
        throw new InvalidOperationException(
            "The P5.3 concurrency gate reads sys.dm_exec_requests from a different SQL session. " +
            "SQL Server major version " + majorVersion + " requires server permission " +
            requiredPermission + " for the login in " +
            SqlTestDatabase.ConnectionStringEnvironmentVariable + ". Grant it only on the disposable-test " +
            "SQL Server instance, for example: GRANT " + requiredPermission + " TO [test_login].");
    }

    private static async Task SetReferenceActiveAsync(
        string connectionString,
        InactiveReferenceCase reference,
        bool isActive,
        TaskCompletionSource<int>? openedSession = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        if (openedSession != null)
        {
            openedSession.SetResult(await connection.QuerySingleAsync<int>(
                new CommandDefinition("SELECT @@SPID;", cancellationToken: cancellationToken)));
        }

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE dbo." + reference.TableName + " SET IsActive = @isActive WHERE " +
                reference.KeyColumn + " = @id;",
                new { isActive, id = reference.Id },
                cancellationToken: cancellationToken));
        Assert.Equal(1, affected);
    }

    private static async Task AssertDraftHasNoInventoryAsync(string connectionString, long receiptId)
    {
        using var connection = new SqlConnection(connectionString);
        Assert.Equal(GoodsReceiptStatuses.Draft, await connection.QuerySingleAsync<string>(
            "SELECT Status FROM dbo.GoodsReceipts WHERE ReceiptId = @receiptId;",
            new { receiptId }));
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentId = @receiptId;",
            new { receiptId }));
    }

    private static async Task<decimal> GetBalanceQuantityAsync(
        string connectionString,
        int warehouseId,
        int productId)
    {
        using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleAsync<decimal>(
            "SELECT COALESCE(SUM(Quantity), 0) FROM dbo.StockBalances " +
            "WHERE WarehouseId = @warehouseId AND ProductId = @productId;",
            new { warehouseId, productId });
    }

    private static async Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>> PostAfterSignalInOwnScopeAsync(
        ServiceProvider provider,
        GoodsReceiptDto receipt,
        Task start)
    {
        await start;
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new PostGoodsReceiptCommand(
                new PostGoodsReceiptRequest(receipt.Id, receipt.Version)),
            CancellationToken.None);
    }

    private static async Task<ErrorOr.ErrorOr<GoodsReceiptDto>> UpdateInOwnScopeAsync(
        ServiceProvider provider,
        GoodsReceiptDto created,
        SeedData seed,
        string note,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new UpdateGoodsReceiptDraftCommand(
                new UpdateGoodsReceiptDraftRequest(
                    created.Id,
                    created.Version,
                    created.ReceiptDate,
                    seed.WarehouseId,
                    seed.EmployeeId,
                    note,
                    new[] { new SaveGoodsReceiptLineRequest(seed.Product1Id, quantity, 1m) })),
            cancellationToken);
    }

    private static async Task<StoredAggregateSnapshot> LoadAggregateSnapshotAsync(
        string connectionString,
        long receiptId)
    {
        using var connection = new SqlConnection(connectionString);
        var header = await connection.QuerySingleAsync<StoredAggregateHeader>(
            "SELECT ReceiptId, ReceiptNo AS ReceiptNumber, ReceiptDate, WarehouseId, EmployeeId, Status, Note, " +
            "CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId, PostedAtUtc, " +
            "PostedByUserId, RowVersion AS Version " +
            "FROM dbo.GoodsReceipts WHERE ReceiptId = @receiptId;",
            new { receiptId });
        var lines = (await connection.QueryAsync<StoredAggregateLine>(
            "SELECT ReceiptLineId, ReceiptId, [LineNo] AS LineNumber, ProductId, Quantity, UnitCost, " +
            "CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId " +
            "FROM dbo.GoodsReceiptLines WHERE ReceiptId = @receiptId " +
            "ORDER BY [LineNo], ReceiptLineId;",
            new { receiptId })).ToArray();
        return new StoredAggregateSnapshot(header, lines);
    }

    private static void AssertAggregateEqual(
        StoredAggregateSnapshot expected,
        StoredAggregateSnapshot actual)
    {
        Assert.Equal(expected.Header.ReceiptId, actual.Header.ReceiptId);
        Assert.Equal(expected.Header.ReceiptNumber, actual.Header.ReceiptNumber);
        Assert.Equal(expected.Header.ReceiptDate, actual.Header.ReceiptDate);
        Assert.Equal(expected.Header.WarehouseId, actual.Header.WarehouseId);
        Assert.Equal(expected.Header.EmployeeId, actual.Header.EmployeeId);
        Assert.Equal(expected.Header.Status, actual.Header.Status);
        Assert.Equal(expected.Header.Note, actual.Header.Note);
        Assert.Equal(expected.Header.CreatedAtUtc, actual.Header.CreatedAtUtc);
        Assert.Equal(expected.Header.CreatedByUserId, actual.Header.CreatedByUserId);
        Assert.Equal(expected.Header.UpdatedAtUtc, actual.Header.UpdatedAtUtc);
        Assert.Equal(expected.Header.UpdatedByUserId, actual.Header.UpdatedByUserId);
        Assert.Equal(expected.Header.PostedAtUtc, actual.Header.PostedAtUtc);
        Assert.Equal(expected.Header.PostedByUserId, actual.Header.PostedByUserId);
        Assert.Equal(expected.Header.Version, actual.Header.Version);
        Assert.Equal(expected.Lines.Length, actual.Lines.Length);
        for (var index = 0; index < expected.Lines.Length; index++)
        {
            var expectedLine = expected.Lines[index];
            var actualLine = actual.Lines[index];
            Assert.Equal(expectedLine.ReceiptLineId, actualLine.ReceiptLineId);
            Assert.Equal(expectedLine.ReceiptId, actualLine.ReceiptId);
            Assert.Equal(expectedLine.LineNumber, actualLine.LineNumber);
            Assert.Equal(expectedLine.ProductId, actualLine.ProductId);
            Assert.Equal(expectedLine.Quantity, actualLine.Quantity);
            Assert.Equal(expectedLine.UnitCost, actualLine.UnitCost);
            Assert.Equal(expectedLine.CreatedAtUtc, actualLine.CreatedAtUtc);
            Assert.Equal(expectedLine.CreatedByUserId, actualLine.CreatedByUserId);
            Assert.Equal(expectedLine.UpdatedAtUtc, actualLine.UpdatedAtUtc);
            Assert.Equal(expectedLine.UpdatedByUserId, actualLine.UpdatedByUserId);
        }
    }

    private static async Task<ErrorOr.ErrorOr<GoodsReceiptDto>> CreateInOwnScopeAsync(
        ServiceProvider provider,
        SeedData seed,
        string note)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateGoodsReceiptDraftCommand(Save(seed, note, seed.Product1Id)),
            CancellationToken.None);
    }

    private static SaveGoodsReceiptRequest Save(SeedData seed, string? note, int productId) =>
        new SaveGoodsReceiptRequest(
            new DateTime(2026, 10, 10),
            seed.WarehouseId,
            seed.EmployeeId,
            note,
            new[] { new SaveGoodsReceiptLineRequest(productId, 1m, 2m) });

    private static async Task<SqlTestDatabase> CreateDatabaseAsync()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());
            return database;
        }
        catch (Exception setupError)
        {
            try
            {
                await database.DisposeAsync();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(
                    "Test database setup and cleanup both failed.",
                    setupError,
                    cleanupError);
            }

            throw;
        }
    }

    private static async Task<SeedData> SeedAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        var userId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Users " +
            "(Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm, IsActive) " +
            "OUTPUT INSERTED.UserId VALUES (N'receipt-command-user', N'RECEIPT-COMMAND-USER', " +
            "N'Receipt Command User', N'Local', N'hash', N'', N'BCrypt', 1);");
        var warehouseId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Warehouses (Code, Name) OUTPUT INSERTED.WarehouseId VALUES (N'WH-CMD', N'Warehouse');");
        var employeeId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Employees (Code, Name) OUTPUT INSERTED.EmployeeId VALUES (N'EMP-CMD', N'Employee');");
        var product1Id = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Products (Code, Name, Unit) OUTPUT INSERTED.ProductId VALUES (N'P-CMD-1', N'Product 1', N'EA');");
        var product2Id = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Products (Code, Name, Unit) OUTPUT INSERTED.ProductId VALUES (N'P-CMD-2', N'Product 2', N'EA');");
        return new SeedData(userId, warehouseId, employeeId, product1Id, product2Id);
    }

    private static ServiceProvider CreateProvider(
        string connectionString,
        int userId,
        bool allow,
        string? onlyPermission = null,
        IDbConnectionFactory? connectionFactory = null,
        IApplicationExceptionReporter? reporter = null)
    {
        var services = new ServiceCollection();
        services.AddServerApplication();
        if (reporter != null) services.Replace(ServiceDescriptor.Singleton(reporter));
        services.AddSqlPersistence(connectionString);
        if (connectionFactory != null)
        {
            services.Replace(ServiceDescriptor.Singleton(connectionFactory));
        }

        services.Replace(ServiceDescriptor.Scoped<ICurrentUserAccessor>(
            _ => new TestCurrentUserAccessor(userId)));
        services.Replace(ServiceDescriptor.Scoped<IPermissionStore>(
            _ => new TestPermissionStore(allow, onlyPermission)));
        return services.BuildServiceProvider();
    }

    private sealed class CancellationExceptionReporter : IApplicationExceptionReporter
    {
        public Exception? Error { get; private set; }
        public void Report(Type requestType, Exception exception) => Error = exception;
    }

    private sealed class SessionRecordingConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        private readonly ConcurrentQueue<int> _sessionIds = new ConcurrentQueue<int>();
        private readonly SemaphoreSlim _sessionAvailable = new SemaphoreSlim(0);

        public SessionRecordingConnectionFactory(string connectionString) =>
            _connectionString = connectionString;

        public IDbConnection OpenConnection()
        {
            var connection = new SqlConnection(_connectionString);
            try
            {
                connection.Open();
                using var command = new SqlCommand("SELECT @@SPID;", connection);
                Record(Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public async Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(_connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                using var command = new SqlCommand("SELECT @@SPID;", connection);
                var sessionId = await command.ExecuteScalarAsync(cancellationToken);
                Record(Convert.ToInt32(sessionId, System.Globalization.CultureInfo.InvariantCulture));
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public void Clear()
        {
            while (_sessionIds.TryDequeue(out _))
            {
            }

            while (_sessionAvailable.Wait(0))
            {
            }
        }

        public async Task<int> TakeNextSessionIdAsync()
        {
            if (!await _sessionAvailable.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("No UnitOfWork SQL session was opened within 10 seconds.");
            }

            if (!_sessionIds.TryDequeue(out var sessionId))
            {
                throw new InvalidOperationException("A recorded SQL session signal had no matching session ID.");
            }

            return sessionId;
        }

        private void Record(int sessionId)
        {
            _sessionIds.Enqueue(sessionId);
            _sessionAvailable.Release();
        }
    }

    private sealed class TestCurrentUserAccessor : ICurrentUserAccessor
    {
        public TestCurrentUserAccessor(int userId) =>
            Current = CurrentUser.Authenticated(userId, "receipt-user", "Receipt User", true);

        public CurrentUser Current { get; }
    }

    private sealed class TestPermissionStore : IPermissionStore
    {
        private readonly bool _allow;
        private readonly string? _onlyPermission;

        public TestPermissionStore(bool allow, string? onlyPermission)
        {
            _allow = allow;
            _onlyPermission = onlyPermission;
        }

        public Task<PermissionSnapshot> GetSnapshotAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken) =>
            _onlyPermission == null
                ? Task.FromResult(new PermissionSnapshot(_allow, hasRoleGrant: true))
                : Task.FromResult(
                    new PermissionSnapshot(
                        directDecision: null,
                        hasRoleGrant: permissionKey == _onlyPermission));
    }

    private sealed class ObservedOperation
    {
        public ObservedOperation(bool isError, string? errorCode)
        {
            IsError = isError;
            ErrorCode = errorCode;
        }

        public bool IsError { get; }

        public string? ErrorCode { get; }
    }

    private sealed class InactiveReferenceCase
    {
        public InactiveReferenceCase(
            string tableName,
            string keyColumn,
            int id,
            string expectedErrorCode)
        {
            TableName = tableName;
            KeyColumn = keyColumn;
            Id = id;
            ExpectedErrorCode = expectedErrorCode;
        }

        public string TableName { get; }

        public string KeyColumn { get; }

        public int Id { get; }

        public string ExpectedErrorCode { get; }
    }

    private sealed class SeedData
    {
        public SeedData(int userId, int warehouseId, int employeeId, int product1Id, int product2Id)
        {
            UserId = userId;
            WarehouseId = warehouseId;
            EmployeeId = employeeId;
            Product1Id = product1Id;
            Product2Id = product2Id;
        }

        public int UserId { get; }
        public int WarehouseId { get; }
        public int EmployeeId { get; }
        public int Product1Id { get; }
        public int Product2Id { get; }
    }

    private sealed class StoredReceipt
    {
        public string? Note { get; set; }
        public byte[] Version { get; set; } = Array.Empty<byte>();
    }

    private sealed class StoredLine
    {
        public int LineNumber { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }

    private sealed class PostedReceiptRow
    {
        public string Status { get; set; } = string.Empty;
        public DateTime? PostedAtUtc { get; set; }
        public int? PostedByUserId { get; set; }
    }

    private sealed class LedgerRow
    {
        public string DocumentType { get; set; } = string.Empty;
        public long DocumentId { get; set; }
        public long DocumentLineId { get; set; }
        public int WarehouseId { get; set; }
        public int ProductId { get; set; }
        public decimal QuantityChange { get; set; }
        public int PostedByUserId { get; set; }
    }

    private sealed class BalanceRow
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    private sealed class StoredAggregateSnapshot
    {
        public StoredAggregateSnapshot(StoredAggregateHeader header, StoredAggregateLine[] lines)
        {
            Header = header;
            Lines = lines;
        }

        public StoredAggregateHeader Header { get; }
        public StoredAggregateLine[] Lines { get; }
    }

    private sealed class StoredAggregateHeader
    {
        public long ReceiptId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }
        public int WarehouseId { get; set; }
        public int EmployeeId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime? PostedAtUtc { get; set; }
        public int? PostedByUserId { get; set; }
        public byte[] Version { get; set; } = Array.Empty<byte>();
    }

    private sealed class StoredAggregateLine
    {
        public long ReceiptLineId { get; set; }
        public long ReceiptId { get; set; }
        public int LineNumber { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public int? UpdatedByUserId { get; set; }
    }
}
