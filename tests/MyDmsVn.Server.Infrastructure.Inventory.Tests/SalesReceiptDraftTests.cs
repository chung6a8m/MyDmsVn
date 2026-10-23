using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using MyDmsVn.Server.DbMigrator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SalesReceiptDraftTests
{
    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Detail_read_uses_one_serializable_connection_and_cancellation_releases_its_locks() => Concurrent(async test =>
    {
        var data = await SalesDraftData.Seed(test.Observer);
        using var setup = data.Provider(test.Database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(setup));
        var blocker = test.Own(new SqlConnection(test.Database.ConnectionString)); await blocker.OpenAsync();
        var transaction = test.Own(blocker.BeginTransaction()); test.Releases.Add(() => { if (transaction.Connection != null) transaction.Rollback(); });
        var owner = await NativeSql.Scalar<int>(blocker, transaction, "SELECT CONVERT(int,@@SPID)");
        await NativeSql.Execute(blocker, transaction, "SELECT ProductId FROM dbo.Products WITH(XLOCK,HOLDLOCK) WHERE ProductId=" + data.Product);
        var recording = test.Own(new DraftRecordingConnections(test.Database.ConnectionString));
        var reader = test.Own(data.Provider(test.Database.ConnectionString, "read", recording));
        var query = test.Track(SalesDraftData.Get(reader, saved.SaleId, test.Stop.Token));
        var session = await recording.Next(); await test.Blocked(session, owner);
        Assert.Equal(4, await test.Observer.QuerySingleAsync<int>("SELECT CONVERT(int,transaction_isolation_level) FROM sys.dm_exec_sessions WHERE session_id=@session", new { session }));
        Assert.Equal(1, recording.Opened);
        Assert.True(await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks l JOIN sys.partitions p ON p.hobt_id=l.resource_associated_entity_id WHERE l.request_session_id=@session AND l.request_status='GRANT' AND l.request_mode IN('S','RangeS-S') AND p.object_id=OBJECT_ID('dbo.SalesReceipts')", new { session }) > 0);
        test.Stop.Cancel();
        var failure = await Record.ExceptionAsync(async () => { var result = await query; Assert.True(result.IsError); });
        if (failure != null) Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(0, await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@session AND resource_database_id=DB_ID()", new { session }));
        transaction.Rollback();
        Assert.Equal(saved.Version, SalesDraftData.Success(await SalesDraftData.Get(setup, saved.SaleId)).Version);
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData(false)]
    [InlineData(true)]
    public Task Competing_saves_wait_on_locked_header_and_only_one_version_wins(bool cancel) => Concurrent(async test =>
    {
        var data = await SalesDraftData.Seed(test.Observer);
        using var setup = data.Provider(test.Database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(setup));
        var recording = test.Own(new DraftRecordingConnections(test.Database.ConnectionString));
        var provider = test.Own(data.Provider(test.Database.ConnectionString, connections: recording));
        var block = test.Own(new SqlConnection(test.Database.ConnectionString)); await block.OpenAsync();
        var transaction = test.Own(block.BeginTransaction()); test.Releases.Add(() => { if (transaction.Connection != null) transaction.Rollback(); });
        var owner = await NativeSql.Scalar<int>(block, transaction, "SELECT CONVERT(int,@@SPID)");
        await NativeSql.Execute(block, transaction, "SELECT SaleId FROM dbo.SalesReceipts WITH(UPDLOCK,HOLDLOCK) WHERE SaleId=" + saved.SaleId);
        var first = test.Track(data.Update(provider, saved, token: test.Stop.Token));
        var firstId = await recording.Next();
        await test.Blocked(firstId, owner);
        if (cancel)
        {
            test.Stop.Cancel();
            var error = await Record.ExceptionAsync(async () => { var result = await first; Assert.True(result.IsError); });
            if (error != null) Assert.IsAssignableFrom<OperationCanceledException>(error);
            transaction.Rollback();
            Assert.Equal(Convert.FromBase64String(saved.Version), await NativeSql.Scalar<byte[]>(test.Observer, null, "SELECT RowVersion FROM dbo.SalesReceipts"));
            Assert.Equal(75m, await NativeSql.Scalar<decimal>(test.Observer, null, "SELECT SUM(LineAmount) FROM dbo.SalesReceiptLines"));
        }
        else
        {
            var second = test.Track(data.Update(provider, saved, token: test.Stop.Token));
            var secondId = await recording.Next();
            Assert.NotEqual(firstId, secondId);
            await test.BlockedOnKey(secondId, owner);
            transaction.Rollback();
            var results = await Task.WhenAll(first, second);
            Assert.Single(results, result => !result.IsError);
            Assert.Equal("SalesReceipt.ConcurrencyConflict", Assert.Single(results, result => result.IsError).FirstError.Code);
            Assert.Equal(Convert.FromBase64String(results.Single(result => !result.IsError).Value.Version),
                await NativeSql.Scalar<byte[]>(test.Observer, null, "SELECT RowVersion FROM dbo.SalesReceipts"));
        }
        await SalesDraftData.NoStock(test.Observer);
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Unit_reference_is_held_through_save_and_next_save_takes_reviewed_current_unit() => Concurrent(async test =>
    {
        var data = await SalesDraftData.Seed(test.Observer);
        var gate = await SqlConcurrencyGate.AcquireAsync(test.Database.ConnectionString, "sales-draft-unit", test.Stop.Token);
        test.Own(gate); test.Releases.Add(() => gate.ReleaseAsync(CancellationToken.None).GetAwaiter().GetResult());
        await NativeSql.Execute(test.Observer, null, "CREATE TRIGGER dbo.TR_SalesDraftWait ON dbo.SalesReceipts AFTER INSERT AS BEGIN " + gate.TransactionWaitSql + " END;");
        var recording = test.Own(new DraftRecordingConnections(test.Database.ConnectionString));
        var provider = test.Own(data.Provider(test.Database.ConnectionString, connections: recording));
        var create = test.Track(data.Create(provider));
        var saveSession = await recording.Next();
        await gate.WaitForHolderAsync(saveSession, "dbo.SalesReceipts", TimeSpan.FromSeconds(10), test.Stop.Token);
        var change = test.Track(Task.Run(async () =>
        {
            using var connection = await recording.OpenConnectionAsync(test.Stop.Token);
            await connection.ExecuteAsync(new SqlTestCommand("UPDATE dbo.Products SET Unit=N'BOX' WHERE ProductId=@Product", new { data.Product }, cancellationToken: test.Stop.Token));
        }));
        var changeSession = await recording.Next();
        await gate.WaitForBusinessBlockAsync(changeSession, saveSession, "dbo.Products", TimeSpan.FromSeconds(10), test.Stop.Token);
        await gate.ReleaseAsync(CancellationToken.None);
        var saved = SalesDraftData.Success(await create); await change;
        Assert.All(saved.Lines, line => Assert.Equal("EA", line.Unit));
        await NativeSql.Execute(test.Observer, null, "DROP TRIGGER dbo.TR_SalesDraftWait;");
        var reviewed = SalesDraftData.Success(await data.Update(provider, saved));
        Assert.All(reviewed.Lines, line => Assert.Equal("BOX", line.Unit));
        await SalesDraftData.NoStock(test.Observer);
    });

    [SqlServerFact]
    public Task Two_hundred_duplicate_rows_can_be_reversed_without_losing_any_identity() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle); using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider));
        var expanded = SalesDraftData.Success(await data.Update(provider, saved, Enumerable.Range(0, 200).Select(_ => new SaveSalesReceiptLineRequest(0, data.Product, 1, 0, false)).ToArray()));
        var reverse = SalesDraftData.Success(await data.Update(provider, expanded, expanded.Lines.Reverse().Select(line => new SaveSalesReceiptLineRequest(line.SaleLineId, data.Product, 1, 0, false)).ToArray()));
        Assert.Equal(expanded.Lines.Reverse().Select(line => line.SaleLineId), reverse.Lines.Select(line => line.SaleLineId));
        Assert.Equal(Enumerable.Range(1, 200), reverse.Lines.Select(line => line.LineNo));
        Assert.Equal(200, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.SalesReceiptLines"));
        Assert.Equal(200m, await NativeSql.Scalar<decimal>(oracle, null, "SELECT SUM(Qty) FROM dbo.SalesReceiptLines"));
        await SalesDraftData.NoStock(oracle);
    });

    [SqlServerFact]
    public Task Duplicate_products_preserve_ids_created_audits_and_submitted_order_without_stock() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle);
        using var provider = data.Provider(database.ConnectionString, "write");
        var saved = SalesDraftData.Success(await data.Create(provider));
        Assert.Equal("SA0000000001", saved.SaleNumber);
        Assert.Equal(new[] { 1, 2, 3 }, saved.Lines.Select(x => x.LineNo));
        Assert.Equal(3, saved.Lines.Select(x => x.SaleLineId).Distinct().Count());
        Assert.All(saved.Lines, x => Assert.True(x.SaleLineId > 0));
        Assert.Equal(new[] { 75m, 0m, 0m }, saved.Lines.Select(x => x.LineAmount));
        Assert.Equal(75m, saved.TotalAmount); Assert.Null(saved.TotalCost); Assert.Null(saved.GrossProfit);
        Assert.Equal("C - Customer", saved.CustomerDisplayName);
        Assert.Equal("P - Product", saved.Lines[0].ProductDisplayName);
        var first = saved.Lines[0].SaleLineId; var second = saved.Lines[1].SaleLineId;
        var created = await oracle.QuerySingleAsync<AuditRow>("SELECT CreatedAtUtc,CreatedByUserId FROM dbo.SalesReceiptLines WHERE SaleLineId=@Id", new { Id = second });
        await oracle.ExecuteAsync("UPDATE dbo.Products SET Unit=N'BOX' WHERE ProductId=@Product", new { data.Product });
        var updated = SalesDraftData.Success(await data.Update(provider, saved, new[] {
            new SaveSalesReceiptLineRequest(second, data.Product, 4, 19, true),
            new SaveSalesReceiptLineRequest(0, data.Product, .0001m, .5m, false),
            new SaveSalesReceiptLineRequest(first, data.Product, 1, 0, false) }));
        Assert.Equal(new[] { second, updated.Lines[1].SaleLineId, first }, updated.Lines.Select(x => x.SaleLineId));
        Assert.DoesNotContain(updated.Lines[1].SaleLineId, saved.Lines.Select(x => x.SaleLineId));
        Assert.All(updated.Lines, x => Assert.Equal("BOX", x.Unit));
        Assert.Equal(.0001m, updated.TotalAmount);
        Assert.Equal(19m, updated.Lines[0].UnitPrice); Assert.Equal(0m, updated.Lines[0].LineAmount);
        Assert.NotEqual(saved.Version, updated.Version); Assert.Equal(saved.SaleNumber, updated.SaleNumber);
        var after = await oracle.QuerySingleAsync<AuditRow>("SELECT CreatedAtUtc,CreatedByUserId FROM dbo.SalesReceiptLines WHERE SaleLineId=@Id", new { Id = second });
        Assert.Equal(created.CreatedAtUtc, after.CreatedAtUtc); Assert.Equal(created.CreatedByUserId, after.CreatedByUserId);
        Assert.Equal(data.Actor, await NativeSql.Scalar<int>(oracle, null, "SELECT UpdatedByUserId FROM dbo.SalesReceiptLines WHERE SaleLineId=" + second));
        Assert.Equal(0, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceiptLines WHERE SaleLineId=@Id", new { Id = saved.Lines[2].SaleLineId }));
        Assert.Equal(Convert.FromBase64String(updated.Version), await NativeSql.Scalar<byte[]>(oracle, null, "SELECT RowVersion FROM dbo.SalesReceipts"));
        using var readProvider = data.Provider(database.ConnectionString, "read");
        var reloaded = SalesDraftData.Success(await SalesDraftData.Get(readProvider, saved.SaleId));
        Assert.Equal(updated.Lines.Select(x => x.SaleLineId), reloaded.Lines.Select(x => x.SaleLineId));
        Assert.Equal(.0001m, reloaded.TotalAmount); Assert.Null(reloaded.PostedAtUtc);
        await SalesDraftData.NoStock(oracle);
    });

    [SqlServerTheory]
    [InlineData("Customers", false)]
    [InlineData("Customers", true)]
    [InlineData("Warehouses", false)]
    [InlineData("Warehouses", true)]
    [InlineData("Employees", false)]
    [InlineData("Employees", true)]
    [InlineData("Products", false)]
    [InlineData("Products", true)]
    public Task Missing_or_inactive_reference_rejects_create_and_update_without_mutation(string table, bool missing) => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle);
        using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider));
        if (!missing) await NativeSql.Execute(oracle, null, "UPDATE dbo." + table + " SET IsActive=0");
        var result = await data.Create(provider, table: missing ? table : null);
        Assert.True(result.IsError); Assert.All(result.Errors, e => Assert.Equal(ErrorType.Validation, e.Type));
        var update = await data.Update(provider, saved, table: missing ? table : null);
        Assert.True(update.IsError); Assert.All(update.Errors, e => Assert.Equal(ErrorType.Validation, e.Type));
        Assert.Equal(1, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.SalesReceipts"));
        Assert.Equal(Convert.FromBase64String(saved.Version), await NativeSql.Scalar<byte[]>(oracle, null, "SELECT RowVersion FROM dbo.SalesReceipts"));
        await SalesDraftData.NoStock(oracle);
    });

    [SqlServerTheory]
    [InlineData("foreign")]
    [InlineData("repeat")]
    [InlineData("corrupt-order")]
    public Task Invalid_line_ownership_or_stored_sequence_fails_before_any_header_or_line_write(string boundary) => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle);
        using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider));
        var other = SalesDraftData.Success(await data.Create(provider));
        var id = boundary == "foreign" ? other.Lines[0].SaleLineId : saved.Lines[0].SaleLineId;
        if (boundary == "corrupt-order") await oracle.ExecuteAsync("UPDATE dbo.SalesReceiptLines SET [LineNo]=201 WHERE SaleLineId=@id", new { id });
        var result = await data.Update(provider, saved, new[] { new SaveSalesReceiptLineRequest(id, data.Product, 9, 9, false), new SaveSalesReceiptLineRequest(id, data.Product, 9, 9, false) }.Take(boundary == "repeat" ? 2 : 1).ToArray());
        Assert.True(result.IsError); Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal(Convert.FromBase64String(saved.Version), await oracle.QuerySingleAsync<byte[]>("SELECT RowVersion FROM dbo.SalesReceipts WHERE SaleId=@SaleId", new { saved.SaleId }));
        Assert.Equal(6, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.SalesReceiptLines"));
        Assert.Equal(0, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.SalesReceiptLines WHERE Qty=9"));
    });

    [SqlServerFact]
    public Task Missing_stale_and_posted_updates_are_distinct_and_cannot_overwrite() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle);
        using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider));
        var updated = SalesDraftData.Success(await data.Update(provider, saved));
        var stale = await data.Update(provider, saved);
        Assert.Equal("SalesReceipt.ConcurrencyConflict", stale.FirstError.Code);
        await oracle.ExecuteAsync("UPDATE dbo.SalesReceipts SET Status='Posted',PostedAtUtc=SYSUTCDATETIME(),PostedByUserId=@Actor WHERE SaleId=@SaleId", new { data.Actor, saved.SaleId });
        var postedVersion = await NativeSql.Scalar<byte[]>(oracle, null, "SELECT RowVersion FROM dbo.SalesReceipts");
        var posted = await data.Update(provider, saved);
        Assert.Equal("SalesReceipt.AlreadyPosted", posted.FirstError.Code);
        Assert.Equal(postedVersion, await NativeSql.Scalar<byte[]>(oracle, null, "SELECT RowVersion FROM dbo.SalesReceipts"));
        var missing = await data.Update(provider, updated, saleId: long.MaxValue);
        Assert.Equal("SalesReceipt.NotFound", missing.FirstError.Code);
        await SalesDraftData.NoStock(oracle);
    });

    [SqlServerFact]
    public Task Sales_number_is_separate_bounded_and_does_not_fallback_after_exhaustion() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle);
        using var provider = data.Provider(database.ConnectionString);
        await NativeSql.Execute(oracle, null, "ALTER SEQUENCE dbo.SalesReceiptNumberSequence RESTART WITH 9999999999;");
        var last = SalesDraftData.Success(await data.Create(provider));
        Assert.Equal("SA9999999999", last.SaleNumber);
        var exhausted = await data.Create(provider); Assert.True(exhausted.IsError);
        Assert.Equal(1, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.SalesReceipts"));
        Assert.Equal(1L, await NativeSql.Scalar<long>(oracle, null, "SELECT NEXT VALUE FOR dbo.GoodsReceiptNumberSequence"));
    });

    [SqlServerTheory]
    [InlineData("SalesReceipts", "INSERT")]
    [InlineData("SalesReceiptLines", "INSERT")]
    [InlineData("SalesReceipts", "UPDATE")]
    [InlineData("SalesReceiptLines", "UPDATE")]
    [InlineData("SalesReceiptLines", "DELETE")]
    public Task Failure_at_draft_write_boundaries_rolls_back_the_entire_aggregate(string table, string operation) => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle);
        using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider));
        await NativeSql.Execute(oracle, null, "CREATE TRIGGER dbo.TR_SalesDraftFault ON dbo." + table + " AFTER " + operation + " AS THROW 51251, 'Expected Draft fault', 1;");
        var result = operation == "INSERT" ? await data.Create(provider) : await data.Update(provider, saved,
            new[] { new SaveSalesReceiptLineRequest(saved.Lines[1].SaleLineId, data.Product, 7, 8, false) });
        Assert.True(result.IsError);
        Assert.Equal(1, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.SalesReceipts"));
        Assert.Equal(3, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.SalesReceiptLines"));
        Assert.Equal(Convert.FromBase64String(saved.Version), await NativeSql.Scalar<byte[]>(oracle, null, "SELECT RowVersion FROM dbo.SalesReceipts"));
        Assert.Equal(75m, await NativeSql.Scalar<decimal>(oracle, null, "SELECT SUM(LineAmount) FROM dbo.SalesReceiptLines"));
        await NativeSql.Execute(oracle, null, "DROP TRIGGER dbo.TR_SalesDraftFault;");
        if (operation == "INSERT") Assert.Equal("SA0000000003", SalesDraftData.Success(await data.Create(provider)).SaleNumber);
        else SalesDraftData.Success(await data.Update(provider, saved));
        await SalesDraftData.NoStock(oracle);
    });

    [SqlServerFact]
    public Task Historical_posted_detail_reads_frozen_costs_and_inactive_labels() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var data = await SalesDraftData.Seed(oracle);
        using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider));
        await oracle.ExecuteAsync("UPDATE dbo.SalesReceiptLines SET PostedUnitCost=15,CostAmount=CASE [LineNo] WHEN 1 THEN 45 WHEN 2 THEN 30 ELSE 0 END; UPDATE dbo.SalesReceipts SET Status='Posted',PostedAtUtc=@Stamp,PostedByUserId=@Actor; UPDATE dbo.Customers SET IsActive=0; UPDATE dbo.Products SET IsActive=0,Unit=N'NEW'; UPDATE dbo.Warehouses SET IsActive=0; UPDATE dbo.Employees SET IsActive=0;", new { data.Actor, Stamp = new DateTime(2026, 10, 22, 1, 0, 0) });
        using var reader = data.Provider(database.ConnectionString, "read");
        var detail = SalesDraftData.Success(await SalesDraftData.Get(reader, saved.SaleId));
        Assert.Equal(75m, detail.TotalAmount); Assert.Equal(75m, detail.TotalCost); Assert.Equal(0m, detail.GrossProfit);
        Assert.Equal(new[] { 45m, 30m, 0m }, detail.Lines.Select(x => x.CostAmount!.Value));
        Assert.All(detail.Lines, x => { Assert.Equal("EA", x.Unit); Assert.Equal(15m, x.PostedUnitCost); Assert.False(x.ProductIsActive); });
        Assert.False(detail.CustomerIsActive); Assert.False(detail.WarehouseIsActive); Assert.False(detail.EmployeeIsActive);
        Assert.Equal(new DateTime(2026, 10, 22, 1, 0, 0), detail.PostedAtUtc);
    });

    private sealed class AuditRow
    {
        public DateTime CreatedAtUtc { get; set; }
        public int CreatedByUserId { get; set; }
    }

    private static async Task Concurrent(Func<DraftScenario, Task> body)
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        var test = new DraftScenario(database); Exception? original = null;
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString); Assert.True(migration.Successful, migration.Error?.ToString());
            await test.Observer.OpenAsync(); await body(test);
        }
        catch (Exception error) { original = error; throw; }
        finally
        {
            try { await test.Cleanup(); }
            catch (Exception cleanup) when (original != null) { throw new AggregateException(original, cleanup); }
        }
    }
    private sealed class DraftScenario(SqlTestDatabase database)
    {
        public SqlTestDatabase Database { get; } = database;
        public SqlConnection Observer { get; } = new SqlConnection(database.ConnectionString);
        public CancellationTokenSource Stop { get; } = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        public List<Action> Releases { get; } = new();
        private readonly List<Task> _workers = new();
        private readonly List<IDisposable> _owned = new();
        public T Own<T>(T resource) where T : IDisposable { _owned.Add(resource); return resource; }
        public T Track<T>(T worker) where T : Task { _workers.Add(worker); return worker; }
        public async Task Blocked(int worker, int owner)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(10))
            {
                if (await Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@worker AND blocking_session_id=@owner AND wait_type LIKE 'LCK%'", new { worker, owner }) == 1) return;
                await Task.Delay(25, Stop.Token);
            }
            throw new TimeoutException("Expected SQL header blocker was not observed.");
        }
        public async Task BlockedOnKey(int worker, int owner)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(10))
            {
                if (await Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks w JOIN sys.dm_tran_locks h ON w.resource_associated_entity_id=h.resource_associated_entity_id AND w.resource_description=h.resource_description AND w.resource_type=h.resource_type WHERE w.request_session_id=@worker AND h.request_session_id=@owner AND w.resource_type='KEY' AND w.request_status IN ('WAIT','CONVERT') AND h.request_status='GRANT' AND h.request_mode IN('U','X')", new { worker, owner }) > 0) return;
                await Task.Delay(25, Stop.Token);
            }
            throw new TimeoutException("Expected same-key SQL contention was not observed.");
        }
        public async Task Cleanup()
        {
            try
            {
                await SqlConcurrencyCleanup.CompleteAsync(
                    () => { foreach (var release in Releases) release(); return Task.CompletedTask; },
                    null, Stop, async () => { foreach (var resource in Enumerable.Reverse(_owned)) resource.Dispose(); Observer.Dispose(); await Database.DisposeAsync(); },
                    TimeSpan.FromSeconds(20), _workers.ToArray());
            }
            finally { Observer.Dispose(); Stop.Dispose(); }
        }
    }
    private sealed class DraftRecordingConnections(string connectionString) : IDbConnectionFactory, IDisposable
    {
        private readonly ConcurrentQueue<int> _sessions = new();
        private readonly SemaphoreSlim _signal = new(0);
        private int _opened;
        public int Opened => _opened;
        public IDbConnection OpenConnection() => OpenConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
        public async Task<IDbConnection> OpenConnectionAsync(CancellationToken token)
        {
            var sql = new SqlConnection(connectionString);
            try { await sql.OpenAsync(token); Interlocked.Increment(ref _opened); _sessions.Enqueue(await NativeSql.Scalar<int>(sql, null, "SELECT CONVERT(int,@@SPID)")); _signal.Release(); return sql; }
            catch { sql.Dispose(); throw; }
        }
        public async Task<int> Next()
        {
            if (!await _signal.WaitAsync(TimeSpan.FromSeconds(10)) || !_sessions.TryDequeue(out var session)) throw new TimeoutException("No independent SQL session was opened.");
            return session;
        }
        public void Dispose() => _signal.Dispose();
    }
}

internal sealed class SalesDraftData
{
    public int Actor { get; private set; }
    public int Customer { get; private set; }
    public int Warehouse { get; private set; }
    public int Employee { get; private set; }
    public int Product { get; private set; }
    public static async Task<SalesDraftData> Seed(SqlConnection oracle) => new SalesDraftData
    {
        Actor = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'sales',N'SALES',N'Sales',N'Local',N'hash',N'',N'BCrypt')"),
        Customer = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Customers(Code,Name) OUTPUT INSERTED.CustomerId VALUES(N'C',N'Customer')"),
        Warehouse = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'W',N'Warehouse')"),
        Employee = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Employees(Code,Name) OUTPUT INSERTED.EmployeeId VALUES(N'E',N'Employee')"),
        Product = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P',N'Product',N'EA')")
    };
    public SaveSalesReceiptLineRequest[] Lines() => new[] {
        new SaveSalesReceiptLineRequest(0, Product, 3, 25, false),
        new SaveSalesReceiptLineRequest(0, Product, 2, 25, true),
        new SaveSalesReceiptLineRequest(0, Product, 1, 0, false) };
    public async Task<ErrorOr<SalesReceiptDto>> Create(ServiceProvider provider, string? table = null)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateSalesReceiptDraftCommand(new SaveSalesReceiptRequest(
            new DateTime(2026, 10, 22), table == "Customers" ? int.MaxValue : Customer, table == "Warehouses" ? int.MaxValue : Warehouse,
            table == "Employees" ? int.MaxValue : Employee, "Draft", table == "Products" ? new[] { new SaveSalesReceiptLineRequest(0, int.MaxValue, 1, 1, false) } : Lines())));
    }
    public async Task<ErrorOr<SalesReceiptDto>> Update(ServiceProvider provider, SalesReceiptDto saved, SaveSalesReceiptLineRequest[]? lines = null, string? table = null, long? saleId = null, CancellationToken token = default)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new UpdateSalesReceiptDraftCommand(new UpdateSalesReceiptDraftRequest(
            saleId ?? saved.SaleId, saved.Version, saved.SaleDate, table == "Customers" ? int.MaxValue : Customer, table == "Warehouses" ? int.MaxValue : Warehouse,
            table == "Employees" ? int.MaxValue : Employee, "Updated", table == "Products" ? new[] { new SaveSalesReceiptLineRequest(0, int.MaxValue, 1, 1, false) } : lines ?? saved.Lines.Select(x => new SaveSalesReceiptLineRequest(x.SaleLineId, x.ProductId, x.Qty, x.UnitPrice, x.IsGift)).ToArray())), token);
    }
    public static async Task<ErrorOr<SalesReceiptDto>> Get(ServiceProvider provider, long id, CancellationToken token = default)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetSalesReceiptByIdQuery(id), token);
    }
    public static SalesReceiptDto Success(ErrorOr<SalesReceiptDto> result) { Assert.False(result.IsError, result.IsError ? result.FirstError.Code : null); return result.Value; }
    public static async Task NoStock(SqlConnection oracle)
    {
        foreach (var table in new[] { "StockLedger", "StockValuationEntries", "StockBalances" })
            Assert.Equal(0, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo." + table));
    }
    public ServiceProvider Provider(string connectionString, string grant = "all", IDbConnectionFactory? connections = null)
    {
        var services = new ServiceCollection(); services.AddSqlPersistence(connectionString); services.AddServerApplication();
        services.Replace(ServiceDescriptor.Scoped<ICurrentUserAccessor>(_ => new ActorAccessor(Actor)));
        services.Replace(ServiceDescriptor.Scoped<IPermissionStore>(_ => new Permissions(grant)));
        if (connections != null) services.Replace(ServiceDescriptor.Singleton(connections));
        return services.BuildServiceProvider();
    }
    private sealed class ActorAccessor : ICurrentUserAccessor
    {
        public ActorAccessor(int id) => Current = CurrentUser.Authenticated(id, "sales", "Sales", true);
        public CurrentUser Current { get; }
    }
    private sealed class Permissions : IPermissionStore
    {
        private readonly string _grant;
        public Permissions(string grant) => _grant = grant;
        public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string key, CancellationToken token) => Task.FromResult(new PermissionSnapshot(null,
            hasRoleGrant: _grant == "all" || key == (_grant == "write" ? PermissionKeys.SalesReceiptsWrite : PermissionKeys.SalesReceiptsRead)));
    }
}
