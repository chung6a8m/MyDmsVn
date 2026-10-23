using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentValidation;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Server.Infrastructure.Sales;
using MyDmsVn.Testing.Sql;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SalesReceiptQueryOwnershipTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Invalid_input_and_precancellation_do_not_admit_any_connection()
    {
        var sessions = new RefusingSessions(); var service = new SqlSalesReceiptQueryService(sessions);
        await Assert.ThrowsAsync<ValidationException>(() => service.ListAsync(Request(0), CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => service.ListAsync(Request(50, "invalid"), CancellationToken.None));
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ListAsync(Request(50), stop.Token));
        Assert.Equal(0, sessions.Opened);
    }

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData("success")]
    [InlineData("cancel")]
    [InlineData("failure")]
    public Task Page_aggregate_and_probe_share_one_serializable_transaction_and_release_on_every_exit(string outcome) => Run(async test =>
    {
        var data = await SalesDraftData.Seed(test.Observer); using var provider = data.Provider(test.Database.ConnectionString);
        for (var i = 0; i < 5; i++) await data.Create(provider);
        var inner = provider.GetRequiredService<ILinqToDbSessionFactory>();
        var sessions = new RecordingSessions(inner);
        sessions.AfterHeader = () =>
        {
            if (outcome == "cancel") test.Stop.Cancel();
            if (outcome == "failure") throw new InvalidOperationException("Injected materialization failure.");
        };
        var service = new SqlSalesReceiptQueryService(sessions);
        // Obtain an incoming Next independently, then instrument only one paged request.
        var first = await provider.GetRequiredService<ISalesReceiptQueryService>().ListAsync(Request(2), CancellationToken.None);
        var work = test.Track(service.ListAsync(Request(2, first.NextCursor), test.Stop.Token));
        if (outcome == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        else if (outcome == "failure") await Assert.ThrowsAsync<InvalidOperationException>(() => work);
        else
        {
            var page = await work; Assert.Equal(new long[] { 3, 2 }, page.Items.Select(r => r.SaleId)); Assert.NotNull(page.NextCursor); Assert.NotNull(page.PreviousCursor);
            Assert.Equal(3, sessions.Commands.Count); Assert.All(page.Items, row => Assert.Equal(75m, row.TotalAmount));
        }
        Assert.Equal(1, sessions.Opened); Assert.NotNull(sessions.Connection); Assert.Equal(ConnectionState.Closed, sessions.Connection!.State);
        Assert.All(sessions.Transactions, tx => Assert.Same(sessions.Transactions[0], tx)); Assert.All(sessions.Isolations, level => Assert.Equal(IsolationLevel.Serializable, level));
        Assert.All(sessions.Connections, connection => Assert.Same(sessions.Connection, connection));
        Assert.Equal(0, await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@id AND resource_database_id=DB_ID()", new { id = sessions.SessionId }));
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Page_holds_header_revision_until_complete_totals_and_direction_probe_finish() => Run(async test =>
    {
        var data = await SalesDraftData.Seed(test.Observer); using var provider = data.Provider(test.Database.ConnectionString);
        for (var i = 0; i < 4; i++) await data.Create(provider);
        using var reached = new ManualResetEventSlim(); var release = test.Own(new ManualResetEventSlim()); test.Release = () => release.Set();
        var sessions = new RecordingSessions(provider.GetRequiredService<ILinqToDbSessionFactory>()) { AfterHeader = () => { reached.Set(); if (!release.Wait(TimeSpan.FromSeconds(10), test.Stop.Token)) throw new TimeoutException("Page pause expired."); } };
        var service = new SqlSalesReceiptQueryService(sessions);
        var work = test.Track(Task.Run(() => service.ListAsync(Request(2), test.Stop.Token)));
        Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
        using (var writer = new SqlConnection(test.Database.ConnectionString))
        {
            await writer.OpenAsync(); await NativeSql.Execute(writer, null, "SET LOCK_TIMEOUT 300");
            var error = await Assert.ThrowsAsync<SqlException>(() => NativeSql.Execute(writer, null, "UPDATE dbo.SalesReceipts SET Note=N'changed' WHERE SaleId=4")); Assert.Equal(1222, error.Number);
        }
        Assert.Equal(4, await test.Observer.QuerySingleAsync<int>("SELECT CONVERT(int,transaction_isolation_level) FROM sys.dm_exec_sessions WHERE session_id=@id", new { id = sessions.SessionId }));
        release.Set(); var page = await work; Assert.Equal(new long[] { 4, 3 }, page.Items.Select(row => row.SaleId)); Assert.All(page.Items, row => Assert.Equal(75m, row.TotalAmount));
        Assert.Equal(ConnectionState.Closed, sessions.Connection!.State);
        await NativeSql.Execute(test.Observer, null, "UPDATE dbo.SalesReceipts SET Note=N'after completed' WHERE SaleId=4");
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Cancellation_during_blocked_aggregate_releases_header_locks_and_owned_connection() => Run(async test =>
    {
        var data = await SalesDraftData.Seed(test.Observer); using var provider = data.Provider(test.Database.ConnectionString); await data.Create(provider);
        using var blocker = new SqlConnection(test.Database.ConnectionString); await blocker.OpenAsync(); using var transaction = blocker.BeginTransaction();
        test.Release = () => { if (transaction.Connection != null) transaction.Rollback(); };
        await NativeSql.Execute(blocker, transaction, "UPDATE dbo.SalesReceiptLines SET LineAmount=LineAmount WHERE SaleId=1");
        var owner = await NativeSql.Scalar<int>(blocker, transaction, "SELECT CONVERT(int,@@SPID)");
        var sessions = new RecordingSessions(provider.GetRequiredService<ILinqToDbSessionFactory>()); var service = new SqlSalesReceiptQueryService(sessions);
        var work = test.Track(service.ListAsync(Request(2), test.Stop.Token));
        await test.WaitUntil(async () => sessions.SessionId > 0 && await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@id AND blocking_session_id=@owner AND wait_type LIKE 'LCK%'", new { id = sessions.SessionId, owner }) == 1);
        Assert.Single(sessions.Transactions.Distinct()); Assert.Equal(2, sessions.Commands.Count);
        test.Stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.Equal(ConnectionState.Closed, sessions.Connection!.State); transaction.Rollback();
        await NativeSql.Execute(test.Observer, null, "UPDATE dbo.SalesReceipts SET Note=N'lock released' WHERE SaleId=1");
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Delayed_lower_identity_commit_is_live_membership_and_never_reorders_by_business_date() => Run(async test =>
    {
        var data = await SalesDraftData.Seed(test.Observer); using var provider = data.Provider(test.Database.ConnectionString);
        using var delayed = new SqlConnection(test.Database.ConnectionString); await delayed.OpenAsync(); using var transaction = delayed.BeginTransaction();
        test.Release = () => { if (transaction.Connection != null) transaction.Rollback(); };
        var low = await NativeSql.Scalar<long>(delayed, transaction, "INSERT dbo.SalesReceipts(SaleNo,SaleDate,CustomerId,WarehouseId,EmployeeId,Status,CreatedByUserId) OUTPUT INSERTED.SaleId VALUES(N'SA-delay','20261021',@c,@w,@e,'Draft',@u)", NativeSql.P("@c", SqlDbType.Int, data.Customer), NativeSql.P("@w", SqlDbType.Int, data.Warehouse), NativeSql.P("@e", SqlDbType.Int, data.Employee), NativeSql.P("@u", SqlDbType.Int, data.Actor));
        var high = SalesDraftData.Success(await data.Create(provider)); Assert.True(high.SaleId > low);
        // Anchor is intentionally reconstructed; it is a key comparison, not a persisted cursor authority.
        var cursor = SalesReceiptCursor.Parse(Request(1)).Encode(high.SaleId, false);
        var sessions = new RecordingSessions(provider.GetRequiredService<ILinqToDbSessionFactory>()); var service = new SqlSalesReceiptQueryService(sessions);
        var owner = await NativeSql.Scalar<int>(delayed, transaction, "SELECT CONVERT(int,@@SPID)");
        var work = test.Track(service.ListAsync(Request(1, cursor), test.Stop.Token));
        await test.WaitUntil(async () => sessions.SessionId > 0 && await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@id AND blocking_session_id=@owner AND wait_type LIKE 'LCK%'", new { id = sessions.SessionId, owner }) == 1);
        transaction.Commit(); var page = await work; Assert.Equal(low, Assert.Single(page.Items).SaleId); Assert.Null(page.NextCursor); Assert.NotNull(page.PreviousCursor);
        Assert.Equal(0, page.Items[0].LineCount); Assert.Equal(0m, page.Items[0].TotalAmount); Assert.Equal(ConnectionState.Closed, sessions.Connection!.State);
    });

    [SqlServerFact]
    public Task Actual_LINQ_SQL_is_bounded_parameterized_and_plans_cover_deep_selective_both_directions() => Run(async test =>
    {
        var data = await SalesDraftData.Seed(test.Observer); using var provider = data.Provider(test.Database.ConnectionString);
        // A bounded synthetic workload, not a claim about every production selectivity.
        var other = await NativeSql.Scalar<int>(test.Observer, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'OTHER',N'Other',N'EA')");
        await test.Observer.ExecuteAsync(@";WITH n AS (SELECT TOP(1200) ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) n FROM sys.all_objects a CROSS JOIN sys.all_objects b)
INSERT dbo.SalesReceipts(SaleNo,SaleDate,CustomerId,WarehouseId,EmployeeId,Status,Note,CreatedByUserId)
SELECT CONCAT(N'SA-Q',n),'20261022',@Customer,@Warehouse,@Employee,'Draft',CASE WHEN n%17=0 THEN N'Đ%_[' ELSE N'plain' END,@Actor FROM n;
INSERT dbo.SalesReceiptLines(SaleId,[LineNo],ProductId,Unit,Qty,UnitPrice,IsGift,LineAmount,CreatedByUserId)
SELECT SaleId,n.n,CASE WHEN SaleId%7=0 THEN @Product ELSE @other END,N'EA',1,2,0,2,@Actor FROM dbo.SalesReceipts CROSS JOIN (VALUES(1),(2),(3)) n(n);", new { data.Customer, data.Warehouse, data.Employee, data.Product, data.Actor, other });
        var sessions = new RecordingSessions(provider.GetRequiredService<ILinqToDbSessionFactory>()); var service = new SqlSalesReceiptQueryService(sessions);
        foreach (var selective in new[] { false, true }) foreach (var previous in new[] { false, true })
            {
                sessions.Commands.Clear();
                var criteria = new SalesReceiptListRequest(null, null, selective ? data.Customer : (int?)null, selective ? data.Warehouse : (int?)null, selective ? data.Employee : (int?)null, selective ? "Draft" : null, selective ? data.Product : (int?)null, selective ? "Đ%_[" : null, 3);
                var cursor = SalesReceiptCursor.Parse(criteria).Encode(600, previous);
                var page = await service.ListAsync(new SalesReceiptListRequest(criteria.FromDate, criteria.ToDate, criteria.CustomerId, criteria.WarehouseId, criteria.EmployeeId, criteria.Status, criteria.ProductId, criteria.Search, 3, cursor), test.Stop.Token);
                Assert.Equal(3, page.Items.Count); Assert.All(page.Items, row => { Assert.Equal(3, row.LineCount); Assert.Equal(6m, row.TotalAmount); }); Assert.Equal(3, sessions.Commands.Count);
                Assert.Equal(selective ? (previous ? new long[] { 952, 833, 714 } : new long[] { 595, 476, 357 }) : (previous ? new long[] { 603, 602, 601 } : new long[] { 599, 598, 597 }), page.Items.Select(row => row.SaleId));
                var sql = sessions.Commands[0].Text;
                if (selective) Assert.Contains("EXISTS", sql.ToUpperInvariant()); Assert.Contains("TOP", sql.ToUpperInvariant()); Assert.Contains("ORDER BY", sql.ToUpperInvariant());
                Assert.DoesNotContain("OFFSET", sql.ToUpperInvariant()); Assert.DoesNotContain("Đ%_[", sql); Assert.DoesNotContain("PostedUnitCost", sql);
                Assert.Contains(previous ? ">" : "<", sql); Assert.Contains("GROUP BY", sessions.Commands[1].Text.ToUpperInvariant());
                foreach (var command in sessions.Commands)
                {
                    var plan = await Plan(test.Observer, command); Assert.NotEmpty(plan.Operators);
                    output.WriteLine("selective={0}; direction={1}; command={2}; logicalReads={3}; operators={4}; indexes={5}", selective, previous ? "previous" : "next", sessions.Commands.IndexOf(command) + 1, plan.Reads, string.Join(",", plan.Operators), string.Join(",", plan.Indexes));
                    output.WriteLine(command.Text); output.WriteLine("Actual plan XML: {0}", plan.Xml);
                }
            }
        Assert.Equal(4, sessions.Opened);
        for (var i = 0; i < 4; i++) { var expected = sessions.Connections[i * 3]; Assert.All(sessions.Connections.Skip(i * 3).Take(3), c => Assert.Same(expected, c)); if (i > 0) Assert.NotSame(sessions.Connections[0], expected); }
    });

    private static async Task<(string[] Operators, string[] Indexes, int Reads, string Xml)> Plan(SqlConnection observer, CommandSnapshot snapshot)
    {
        var reads = 0; SqlInfoMessageEventHandler handler = (_, args) => { foreach (SqlError message in args.Errors) { var match = System.Text.RegularExpressions.Regex.Match(message.Message, @"logical reads (\d+)"); if (match.Success) reads += int.Parse(match.Groups[1].Value); } };
        observer.InfoMessage += handler;
        try
        {
            using var command = new SqlCommand("SET STATISTICS IO ON; SET STATISTICS XML ON; " + snapshot.Text, observer) { CommandTimeout = 30 };
            command.Parameters.AddRange(snapshot.Parameters.Select(p => (SqlParameter)((ICloneable)p).Clone()).ToArray());
            using var reader = await command.ExecuteReaderAsync(); XDocument? plan = null;
            do { while (await reader.ReadAsync()) if (reader.FieldCount == 1 && reader.GetValue(0) is string xml && xml.Contains("ShowPlanXML")) plan = XDocument.Parse(xml); } while (await reader.NextResultAsync());
            Assert.NotNull(plan); XNamespace ns = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";
            return (plan!.Descendants(ns + "RelOp").Select(e => (string?)e.Attribute("PhysicalOp") ?? "").Distinct().ToArray(), plan.Descendants(ns + "Object").Select(e => (string?)e.Attribute("Index") ?? "").Where(x => x.Length > 0).Distinct().ToArray(), reads, plan.ToString());
        }
        finally { observer.InfoMessage -= handler; await NativeSql.Execute(observer, null, "SET STATISTICS XML OFF; SET STATISTICS IO OFF"); }
    }

    private static SalesReceiptListRequest Request(int size, string? cursor = null) => new(null, null, null, null, null, null, null, null, size, cursor);
    private static async Task Run(Func<QueryScenario, Task> body)
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        var test = new QueryScenario(database); Exception? original = null;
        try { var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString); Assert.True(migration.Successful, migration.Error?.ToString()); await test.Observer.OpenAsync(); await body(test); }
        catch (Exception error) { original = error; throw; }
        finally { try { await test.Cleanup(); } catch (Exception cleanup) when (original != null) { throw new AggregateException(original, cleanup); } }
    }
    private sealed class QueryScenario(SqlTestDatabase database)
    {
        public SqlTestDatabase Database { get; } = database; public SqlConnection Observer { get; } = new(database.ConnectionString); public CancellationTokenSource Stop { get; } = new(TimeSpan.FromSeconds(45));
        public Action? Release { get; set; }
        private readonly List<Task> _workers = new(); private readonly List<IDisposable> _owned = new();
        public T Own<T>(T resource) where T : IDisposable { _owned.Add(resource); return resource; }
        public T Track<T>(T task) where T : Task { _workers.Add(task); return task; }
        public async Task WaitUntil(Func<Task<bool>> predicate) { var watch = Stopwatch.StartNew(); while (watch.Elapsed < TimeSpan.FromSeconds(10)) { if (await predicate()) return; await Task.Delay(25, Stop.Token); } throw new TimeoutException("Expected independent SQL handshake was not observed."); }
        public async Task Cleanup()
        { try { await SqlConcurrencyCleanup.CompleteAsync(() => { Release?.Invoke(); return Task.CompletedTask; }, null, Stop, async () => { foreach (var resource in _owned) resource.Dispose(); Observer.Dispose(); await Database.DisposeAsync(); }, TimeSpan.FromSeconds(20), _workers.ToArray()); } finally { Observer.Dispose(); Stop.Dispose(); } }
    }
    private sealed class RefusingSessions : ILinqToDbSessionFactory
    {
        public int Opened { get; private set; }
        public DataConnection Borrow(ISqlExecutionContext context, bool requireTransaction) => throw new NotSupportedException();
        public Task<DataConnection> OpenOwnedAsync(CancellationToken token) { Opened++; throw new InvalidOperationException("Unexpected query admission."); }
    }
    private sealed class CommandSnapshot(string text, SqlParameter[] parameters) { public string Text { get; } = text; public SqlParameter[] Parameters { get; } = parameters; }
    private sealed class RecordingSessions(ILinqToDbSessionFactory inner) : ILinqToDbSessionFactory
    {
        public int Opened { get; private set; }
        public int SessionId { get; private set; }
        public DbConnection? Connection { get; private set; }
        public List<CommandSnapshot> Commands { get; } = new(); public List<DbConnection> Connections { get; } = new(); public List<DbTransaction> Transactions { get; } = new(); public List<IsolationLevel> Isolations { get; } = new(); public Action? AfterHeader { get; set; }
        public DataConnection Borrow(ISqlExecutionContext context, bool requireTransaction) => throw new InvalidOperationException("List must own its read session.");
        public async Task<DataConnection> OpenOwnedAsync(CancellationToken token)
        {
            var db = await inner.OpenOwnedAsync(token); Opened++; var header = false;
            db.UseOptions(options => options.UseTracing(TraceLevel.Verbose, trace =>
            {
                if (trace.TraceInfoStep == TraceInfoStep.BeforeExecute && trace.Command != null && trace.Command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    var command = trace.Command; Connection = command.Connection; SessionId = ((SqlConnection)command.Connection!).ServerProcessId;
                    Connections.Add(command.Connection!); Assert.NotNull(command.Transaction); Transactions.Add(command.Transaction!); Isolations.Add(command.Transaction!.IsolationLevel);
                    Commands.Add(new CommandSnapshot(command.CommandText, command.Parameters.Cast<SqlParameter>().Select(p => (SqlParameter)((ICloneable)p).Clone()).ToArray()));
                    header = Commands.Count == 1;
                }
                if (trace.TraceInfoStep == TraceInfoStep.Completed && header) { header = false; AfterHeader?.Invoke(); }
            }));
            return db;
        }
    }
}
