using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.DbMigrator;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[Collection(ApplicationSqlCollectionFixture.CollectionName)]
public sealed class GoodsReceiptQueryIntegrationTests
{
    private readonly ApplicationSqlCollectionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public GoodsReceiptQueryIntegrationTests(ApplicationSqlCollectionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [SqlServerFact]
    public Task Get_returns_complete_aggregate_version_and_inactive_historical_labels() =>
        _fixture.RunAsync(nameof(Get_returns_complete_aggregate_version_and_inactive_historical_labels), _output, test => SeedAsync(test.ConnectionString), async (test, seed) =>
        {
            var provider = ApplicationSqlCollectionFixture.Track(test, CreateProvider(test.ConnectionString));
            var scope = ApplicationSqlCollectionFixture.Track(test, provider.CreateScope());
            var queries = scope.ServiceProvider.GetRequiredService<IGoodsReceiptQueryService>();

            var receipt = await queries.GetByIdAsync(seed.Receipt1Id, CancellationToken.None);
            var missing = await queries.GetByIdAsync(long.MaxValue, CancellationToken.None);

            Assert.NotNull(receipt);
            Assert.Equal("GR0000000101", receipt!.ReceiptNumber);
            Assert.Equal(seed.Version1, receipt.Version);
            Assert.Equal("WH-QUERY-1 - Query warehouse 1", receipt.WarehouseDisplayName);
            Assert.False(receipt.WarehouseIsActive);
            Assert.Equal("EMP-QUERY-1 - Query employee 1", receipt.EmployeeDisplayName);
            Assert.False(receipt.EmployeeIsActive);
            Assert.Equal(new[] { 1, 2 }, receipt.Lines.Select(line => line.LineNumber));
            Assert.Equal("P-QUERY-1 - Query product 1", receipt.Lines[0].ProductDisplayName);
            Assert.False(receipt.Lines[0].ProductIsActive);
            Assert.Null(missing);
        });

    [SqlServerFact]
    public Task List_is_deterministic_set_based_and_applies_each_filter() =>
        _fixture.RunAsync(nameof(List_is_deterministic_set_based_and_applies_each_filter), _output, test => SeedAsync(test.ConnectionString), async (test, seed) =>
        {
            var provider = ApplicationSqlCollectionFixture.Track(test, CreateProvider(test.ConnectionString));
            var scope = ApplicationSqlCollectionFixture.Track(test, provider.CreateScope());
            var queries = scope.ServiceProvider.GetRequiredService<IGoodsReceiptQueryService>();

            var firstPage = await queries.ListAsync(
                new GoodsReceiptListRequest(1, 2, null, null),
                CancellationToken.None);
            var secondPage = await queries.ListAsync(
                new GoodsReceiptListRequest(2, 2, null, null),
                CancellationToken.None);
            var warehouse = await queries.ListAsync(
                new GoodsReceiptListRequest(1, 25, seed.Warehouse1Id, null),
                CancellationToken.None);
            var posted = await queries.ListAsync(
                new GoodsReceiptListRequest(1, 25, null, "Posted"),
                CancellationToken.None);
            var productSearch = await queries.ListAsync(
                new GoodsReceiptListRequest(1, 25, null, null, "P-QUERY-1"),
                CancellationToken.None);
            var empty = await queries.ListAsync(
                new GoodsReceiptListRequest(9, 25, null, null),
                CancellationToken.None);

            Assert.Equal(3, firstPage.TotalCount);
            Assert.Equal(new[] { seed.Receipt3Id, seed.Receipt2Id }, firstPage.Items.Select(item => item.Id));
            Assert.All(firstPage.Items, item => Assert.NotEmpty(item.Lines));
            Assert.Single(secondPage.Items);
            Assert.Equal(seed.Receipt1Id, secondPage.Items[0].Id);
            Assert.Equal(2, secondPage.Items[0].Lines.Count);
            Assert.Equal(2, warehouse.TotalCount);
            Assert.Equal(2, warehouse.Items.Count);
            Assert.Single(posted.Items);
            Assert.Equal(seed.Receipt2Id, posted.Items[0].Id);
            Assert.Equal(2, productSearch.TotalCount);
            Assert.Equal(new[] { seed.Receipt2Id, seed.Receipt1Id }, productSearch.Items.Select(item => item.Id));
            Assert.Equal(3, empty.TotalCount);
            Assert.Empty(empty.Items);
        });

    [SqlServerFact]
    public async Task Get_holds_one_revision_while_header_and_lines_are_read()
    {
        var database = await CreateDatabaseAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        ServiceProvider? provider = null;
        IServiceScope? scope = null;
        SqlConnection? lineBlocker = null;
        SqlTransaction? lineBlockerTransaction = null;
        SqlConnection? observer = null;
        Task<GoodsReceiptDto?>? read = null;
        Exception? original = null;
        try
        {
            var seed = await SeedAsync(database.ConnectionString);
            provider = CreateProvider(database.ConnectionString);
            scope = provider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IGoodsReceiptQueryService>();
            lineBlocker = new SqlConnection(database.ConnectionString);
            await lineBlocker.OpenAsync(cancellation.Token);
            lineBlockerTransaction = lineBlocker.BeginTransaction();
            var blockerSession = await NativeSql.Scalar<int>(lineBlocker, lineBlockerTransaction, "SELECT CONVERT(int,@@SPID)");
            await lineBlocker.ExecuteAsync(
                "UPDATE dbo.GoodsReceiptLines SET Quantity = Quantity + 1 " +
                "WHERE ReceiptId = @receiptId;",
                new { receiptId = seed.Receipt1Id },
                lineBlockerTransaction);

            observer = new SqlConnection(database.ConnectionString);
            await observer.OpenAsync(cancellation.Token);
            read = queries.GetByIdAsync(seed.Receipt1Id, cancellation.Token);
            await WaitForBlockedLineReadAsync(observer, blockerSession, read, cancellation.Token);
            _output.WriteLine("Native reader blocked on GoodsReceiptLines while holding GoodsReceipts shared lock.");

            await observer.ExecuteAsync("SET LOCK_TIMEOUT 250;");
            var updateError = await Record.ExceptionAsync(
                () => observer.ExecuteAsync(
                    "UPDATE dbo.GoodsReceipts SET Note = N'concurrent update' " +
                    "WHERE ReceiptId = @receiptId;",
                    new { receiptId = seed.Receipt1Id }));

            var lockTimeout = Assert.IsType<SqlException>(updateError);
            Assert.Equal(1222, lockTimeout.Number);
            lineBlockerTransaction.Rollback();
            var completed = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(read, completed);
            var receipt = await read;

            Assert.NotNull(receipt);
            Assert.Equal(seed.Version1, receipt!.Version);
            Assert.Equal(new[] { 2m, 3m }, receipt.Lines.Select(line => line.Quantity));
        }
        catch (Exception error) { original = error; throw; }
        finally
        {
            try
            {
                await SqlConcurrencyCleanup.CompleteAsync(
                    () => { if (lineBlockerTransaction?.Connection != null) lineBlockerTransaction.Rollback(); return Task.CompletedTask; },
                    () => { lineBlockerTransaction?.Dispose(); lineBlocker?.Dispose(); },
                    cancellation,
                    async () => { scope?.Dispose(); provider?.Dispose(); observer?.Dispose(); await database.DisposeAsync(); },
                    TimeSpan.FromSeconds(20), read);
            }
            catch (Exception cleanup) when (original != null) { throw new AggregateException(original, cleanup); }
        }
    }

    private static async Task WaitForBlockedLineReadAsync(SqlConnection observer, int blocker, Task read, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (read.IsCompleted)
            {
                await read;
                throw new InvalidOperationException("Receipt read finished before the expected line-lock boundary.");
            }
            var blocked = await observer.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.database_id=DB_ID() AND r.blocking_session_id=@blocker AND r.wait_type LIKE 'LCK%'
AND t.text LIKE '%FROM dbo.GoodsReceiptLines line%' AND t.text LIKE '%FROM dbo.GoodsReceipts gr%'
AND EXISTS (SELECT 1 FROM sys.dm_tran_locks l JOIN sys.partitions p ON p.hobt_id=l.resource_associated_entity_id
 WHERE l.request_session_id=r.session_id AND l.request_status IN ('WAIT','CONVERT')
 AND p.object_id=OBJECT_ID('dbo.GoodsReceiptLines'))
AND EXISTS (SELECT 1 FROM sys.dm_tran_locks l JOIN sys.partitions p ON p.hobt_id=l.resource_associated_entity_id
 WHERE l.request_session_id=r.session_id AND l.request_status='GRANT' AND l.request_mode IN ('S','RangeS-S')
 AND p.object_id=OBJECT_ID('dbo.GoodsReceipts'))", new { blocker });
            if (blocked == 1) return;
            await Task.Delay(25, token);
        }
        throw new TimeoutException("Receipt reader did not reach the native line-lock boundary while holding its header lock.");
    }

    private static ServiceProvider CreateProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddSqlPersistence(connectionString);
        return services.BuildServiceProvider();
    }

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
            "OUTPUT INSERTED.UserId VALUES (N'receipt-query-user', N'RECEIPT-QUERY-USER', " +
            "N'Receipt Query User', N'Local', N'hash', N'', N'BCrypt', 1);");
        var warehouse1Id = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Warehouses (Code, Name) OUTPUT INSERTED.WarehouseId " +
            "VALUES (N'WH-QUERY-1', N'Query warehouse 1');");
        var warehouse2Id = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Warehouses (Code, Name) OUTPUT INSERTED.WarehouseId " +
            "VALUES (N'WH-QUERY-2', N'Query warehouse 2');");
        var employee1Id = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Employees (Code, Name) OUTPUT INSERTED.EmployeeId " +
            "VALUES (N'EMP-QUERY-1', N'Query employee 1');");
        var employee2Id = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Employees (Code, Name) OUTPUT INSERTED.EmployeeId " +
            "VALUES (N'EMP-QUERY-2', N'Query employee 2');");
        var product1Id = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Products (Code, Name, Unit) OUTPUT INSERTED.ProductId " +
            "VALUES (N'P-QUERY-1', N'Query product 1', N'EA');");
        var product2Id = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Products (Code, Name, Unit) OUTPUT INSERTED.ProductId " +
            "VALUES (N'P-QUERY-2', N'Query product 2', N'EA');");

        var receipt1Id = await InsertReceiptAsync(
            connection, "GR0000000101", new DateTime(2026, 10, 10), warehouse1Id, employee1Id,
            "Draft", userId, null);
        await InsertLineAsync(connection, receipt1Id, 1, product1Id, 2m, 10m, userId);
        await InsertLineAsync(connection, receipt1Id, 2, product2Id, 3m, 20m, userId);
        var receipt2Id = await InsertReceiptAsync(
            connection, "GR0000000102", new DateTime(2026, 10, 11), warehouse1Id, employee1Id,
            "Posted", userId, new DateTime(2026, 10, 11, 1, 2, 3, DateTimeKind.Utc));
        await InsertLineAsync(connection, receipt2Id, 1, product1Id, 1m, 30m, userId);
        var receipt3Id = await InsertReceiptAsync(
            connection, "GR0000000103", new DateTime(2026, 10, 11), warehouse2Id, employee2Id,
            "Draft", userId, null);
        await InsertLineAsync(connection, receipt3Id, 1, product2Id, 4m, 40m, userId);

        var version1 = Convert.ToBase64String(await connection.QuerySingleAsync<byte[]>(
            "SELECT RowVersion FROM dbo.GoodsReceipts WHERE ReceiptId = @receiptId;",
            new { receiptId = receipt1Id }));
        await connection.ExecuteAsync(
            "UPDATE dbo.Warehouses SET IsActive = 0 WHERE WarehouseId = @warehouse1Id; " +
            "UPDATE dbo.Employees SET IsActive = 0 WHERE EmployeeId = @employee1Id; " +
            "UPDATE dbo.Products SET IsActive = 0 WHERE ProductId = @product1Id;",
            new { warehouse1Id, employee1Id, product1Id });
        return new SeedData(
            warehouse1Id,
            receipt1Id,
            receipt2Id,
            receipt3Id,
            version1);
    }

    private static Task<long> InsertReceiptAsync(
        SqlConnection connection,
        string number,
        DateTime date,
        int warehouseId,
        int employeeId,
        string status,
        int userId,
        DateTime? postedAtUtc) =>
        connection.QuerySingleAsync<long>(
            "INSERT dbo.GoodsReceipts " +
            "(ReceiptNo, ReceiptDate, WarehouseId, EmployeeId, Status, CreatedByUserId, PostedAtUtc, PostedByUserId) " +
            "OUTPUT INSERTED.ReceiptId VALUES " +
            "(@number, @date, @warehouseId, @employeeId, @status, @userId, @postedAtUtc, " +
            "CASE WHEN @postedAtUtc IS NULL THEN NULL ELSE @userId END);",
            new { number, date, warehouseId, employeeId, status, userId, postedAtUtc });

    private static async Task InsertLineAsync(
        SqlConnection connection,
        long receiptId,
        int lineNumber,
        int productId,
        decimal quantity,
        decimal unitCost,
        int userId) =>
        await connection.ExecuteAsync(
            "INSERT dbo.GoodsReceiptLines " +
            "(ReceiptId, [LineNo], ProductId, Quantity, UnitCost, CreatedByUserId) " +
            "VALUES (@receiptId, @lineNumber, @productId, @quantity, @unitCost, @userId);",
            new { receiptId, lineNumber, productId, quantity, unitCost, userId });

    private sealed class SeedData
    {
        public SeedData(
            int warehouse1Id,
            long receipt1Id,
            long receipt2Id,
            long receipt3Id,
            string version1)
        {
            Warehouse1Id = warehouse1Id;
            Receipt1Id = receipt1Id;
            Receipt2Id = receipt2Id;
            Receipt3Id = receipt3Id;
            Version1 = version1;
        }

        public int Warehouse1Id { get; }
        public long Receipt1Id { get; }
        public long Receipt2Id { get; }
        public long Receipt3Id { get; }
        public string Version1 { get; }
    }
}
