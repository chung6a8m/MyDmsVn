using System;
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
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Domain.Inventory;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class GoodsReceiptCommandIntegrationTests
{
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

    [SqlServerFact]
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

    [SqlServerFact]
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

    [SqlServerFact]
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
        decimal quantity)
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
            CancellationToken.None);
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
        catch
        {
            await database.DisposeAsync();
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
        string? onlyPermission = null)
    {
        var services = new ServiceCollection();
        services.AddServerApplication();
        services.AddSqlPersistence(connectionString);
        services.Replace(ServiceDescriptor.Scoped<ICurrentUserAccessor>(
            _ => new TestCurrentUserAccessor(userId)));
        services.Replace(ServiceDescriptor.Scoped<IPermissionStore>(
            _ => new TestPermissionStore(allow, onlyPermission)));
        return services.BuildServiceProvider();
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
