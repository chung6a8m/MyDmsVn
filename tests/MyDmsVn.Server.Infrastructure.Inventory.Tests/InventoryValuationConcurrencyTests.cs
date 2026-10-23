using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class InventoryValuationConcurrencyTests(ITestOutputHelper output)
{
    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData(false)]
    [InlineData(true)]
    public Task Posting_gate_precedes_document_and_reference_locks_and_cancellation_rolls_back(bool cancel) => Run(async test =>
    {
        var seed = await ValuationTestData.SeedAsync(test.Observer);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString));
        var provider = test.Own(ValuationTestData.Provider(test.Database.ConnectionString, seed.Actor, recording));
        var blocker = await test.BlockAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='MyDmsVn.InventoryValuation.v1', @LockMode='Exclusive', @LockOwner='Transaction', @DbPrincipal='public'; IF @r<0 THROW 51221,'gate failed',1;");
        var worker = test.Track(ValuationTestData.PostAsync(provider, seed.Header, test.Cancellation.Token));
        var session = await recording.NextAsync();
        await test.WaitBlockedAsync(session, blocker.Session);
        Assert.Equal(0, await test.Observer.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@session AND resource_database_id=DB_ID() AND resource_type IN ('OBJECT','KEY','PAGE','RID')", new { session }));
        output.WriteLine("Actual Post waits on Exclusive application lock before any document/reference/key lock.");
        if (cancel)
        {
            test.Cancellation.Cancel();
            var error = await Record.ExceptionAsync(async () => { var result = await worker; Assert.True(result.IsError); });
            if (error != null) Assert.IsAssignableFrom<OperationCanceledException>(error);
        }
        blocker.Release();
        if (!cancel) Assert.False((await worker).IsError);
        Assert.Equal(cancel ? "Draft" : "Posted", await NativeSql.Scalar<string>(test.Observer, null, "SELECT Status FROM dbo.GoodsReceipts"));
        Assert.Equal(cancel ? 0 : 1, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task All_balance_keys_are_locked_before_first_ledger_insert() => Run(async test =>
    {
        var seed = await ValuationTestData.SeedAsync(test.Observer, 1, 2);
        var second = await NativeSql.Scalar<int>(test.Observer, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA');");
        await test.Observer.ExecuteAsync("INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId) VALUES(@Id,2,@second,1,3,@Actor); INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@WarehouseId,@first,1,2),(@WarehouseId,@second,1,3);",
            new { seed.Header.Id, second, seed.Actor, seed.Header.WarehouseId, first = seed.Lines[0].ProductId });
        var blocker = await test.BlockAsync("SELECT Quantity FROM dbo.StockBalances WITH (XLOCK,HOLDLOCK,ROWLOCK,INDEX(PK_StockBalances)) WHERE WarehouseId=" + seed.Header.WarehouseId + " AND ProductId=" + second);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString));
        var provider = test.Own(ValuationTestData.Provider(test.Database.ConnectionString, seed.Actor, recording));
        var worker = test.Track(ValuationTestData.PostAsync(provider, seed.Header, test.Cancellation.Token));
        var session = await recording.NextAsync();
        await test.WaitBlockedAsync(session, blocker.Session);
        Assert.Equal(0, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockLedger WITH (READUNCOMMITTED)"));
        Assert.Equal("Draft", await NativeSql.Scalar<string>(test.Observer, null, "SELECT Status FROM dbo.GoodsReceipts WITH (READUNCOMMITTED)"));
        Assert.True(await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks l JOIN sys.partitions p ON p.hobt_id=l.resource_associated_entity_id WHERE l.request_session_id=@session AND l.request_status='GRANT' AND l.request_mode IN ('U','RangeS-U') AND p.object_id=OBJECT_ID('dbo.StockBalances')", new { session }) > 0);
        output.WriteLine("Post holds earlier stock-key U lock, blocks at later key, and has written neither status nor ledger.");
        blocker.Release();
        Assert.False((await worker).IsError);
        Assert.Equal(10m, await NativeSql.Scalar<decimal>(test.Observer, null, "SELECT SUM(InventoryValue) FROM dbo.StockBalances"));
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Missing_balance_range_blocks_independent_insert_and_generated_sql_uses_unique_PK() => Run(async test =>
    {
        var seed = await ValuationTestData.SeedAsync(test.Observer);
        var provider = test.Own(ValuationTestData.Provider(test.Database.ConnectionString));
        using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create();
        uow.BeginTransaction();
        var context = (ISqlExecutionContext)uow;
        var trace = new TraceSessions(provider.GetRequiredService<ILinqToDbSessionFactory>(), output);
        var coordinator = new InventoryValuationCoordinator(context, trace);
        await coordinator.AcquirePostingGateAsync(test.Cancellation.Token);
        var states = await coordinator.LockBalancesAsync(seed.Header.WarehouseId, new[] { seed.Lines[0].ProductId }, test.Cancellation.Token);
        Assert.Equal(0m, states[seed.Lines[0].ProductId].Quantity);
        var owner = await NativeSql.Scalar<int>((SqlConnection)context.Connection, (SqlTransaction)context.Transaction!, "SELECT CONVERT(int,@@SPID)");
        var signal = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = test.Track(Task.Run(async () =>
        {
            using var sql = new SqlConnection(test.Database.ConnectionString); await sql.OpenAsync(test.Cancellation.Token);
            signal.SetResult(await NativeSql.Scalar<int>(sql, null, "SELECT CONVERT(int,@@SPID)"));
            using var command = new SqlCommand("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@w,@p,1,0)", sql) { CommandTimeout = 30 };
            command.Parameters.AddWithValue("@w", seed.Header.WarehouseId); command.Parameters.AddWithValue("@p", seed.Lines[0].ProductId);
            await command.ExecuteNonQueryAsync(test.Cancellation.Token);
        }));
        await test.WaitBlockedAsync(await signal.Task, owner);
        Assert.True(await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks l JOIN sys.partitions p ON p.hobt_id=l.resource_associated_entity_id WHERE l.request_session_id=@owner AND l.request_mode='RangeS-U' AND l.request_status='GRANT' AND p.object_id=OBJECT_ID('dbo.StockBalances') AND p.index_id=1", new { owner }) > 0);
        Assert.Contains(trace.Sql, sql => sql.Contains("UPDLOCK") && sql.Contains("HOLDLOCK") && sql.Contains("PK_StockBalances"));
        output.WriteLine("Absent composite PK RangeS-U lock observed; independent INSERT waits for owning transaction.");
        uow.Rollback();
        await worker;
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData(false)]
    [InlineData(true)]
    public Task Reconciliation_exclusive_gate_serializes_both_writer_orderings(bool reconciliationFirst) => Run(async test =>
    {
        var seed = await ValuationTestData.SeedAsync(test.Observer, 1, 2);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString));
        var provider = test.Own(ValuationTestData.Provider(test.Database.ConnectionString, seed.Actor, recording));
        if (reconciliationFirst)
        {
            var tableBlocker = await test.BlockAsync("SELECT TOP(1) LedgerId FROM dbo.StockLedger WITH (TABLOCKX,HOLDLOCK)");
            var reconcile = test.Track(provider.GetRequiredService<IStockReconciliationQueryService>().FindDifferencesAsync(test.Cancellation.Token));
            var reconcileSession = await recording.NextAsync();
            await test.WaitBlockedAsync(reconcileSession, tableBlocker.Session);
            var post = test.Track(ValuationTestData.PostAsync(provider, seed.Header, test.Cancellation.Token));
            var postSession = await recording.NextAsync();
            await test.WaitBlockedAsync(postSession, reconcileSession);
            Assert.Equal(0, await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@postSession AND resource_type IN ('OBJECT','KEY','PAGE')", new { postSession }));
            tableBlocker.Release();
            Assert.Empty(await reconcile); Assert.False((await post).IsError);
        }
        else
        {
            var headerBlocker = await test.BlockAsync("SELECT Status FROM dbo.GoodsReceipts WITH (XLOCK,HOLDLOCK,ROWLOCK,INDEX(PK_GoodsReceipts)) WHERE ReceiptId=" + seed.Header.Id);
            var post = test.Track(ValuationTestData.PostAsync(provider, seed.Header, test.Cancellation.Token));
            var postSession = await recording.NextAsync();
            await test.WaitBlockedAsync(postSession, headerBlocker.Session);
            var reconcile = test.Track(provider.GetRequiredService<IStockReconciliationQueryService>().FindDifferencesAsync(test.Cancellation.Token));
            var reconcileSession = await recording.NextAsync();
            await test.WaitBlockedAsync(reconcileSession, postSession);
            Assert.Equal(0, await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@reconcileSession AND resource_type IN ('OBJECT','KEY','PAGE')", new { reconcileSession }));
            headerBlocker.Release();
            Assert.False((await post).IsError); Assert.Empty(await reconcile);
        }
        Assert.Equal(2m, await NativeSql.Scalar<decimal>(test.Observer, null, "SELECT InventoryValue FROM dbo.StockBalances"));
        output.WriteLine("Observed blocking chain for reconciliationFirst=" + reconciliationFirst + "; both operations drained with coherent Q/V.");
    });


    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Reversed_product_lines_lock_numeric_product_order_before_stock() => Run(async test =>
    {
        var seed = await ValuationTestData.SeedAsync(test.Observer, 1, 2);
        var high = await NativeSql.Scalar<int>(test.Observer, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA')");
        await test.Observer.ExecuteAsync("UPDATE dbo.GoodsReceiptLines SET [LineNo]=2; INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId) VALUES(@Id,1,@high,1,2,@Actor)", new { seed.Header.Id, high, seed.Actor });
        var blocker = await test.BlockAsync("SELECT Unit FROM dbo.Products WITH(XLOCK,HOLDLOCK,ROWLOCK,INDEX(PK_Products)) WHERE ProductId=" + seed.Lines[0].ProductId);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString));
        var provider = test.Own(ValuationTestData.Provider(test.Database.ConnectionString, seed.Actor, recording));
        var post = test.Track(ValuationTestData.PostAsync(provider, seed.Header, test.Cancellation.Token));
        var session = await recording.NextAsync();
        await test.WaitBlockedAsync(session, blocker.Session);
        using (var probe = test.Observer.BeginTransaction())
        {
            await NativeSql.Execute(test.Observer, probe, "SELECT Unit FROM dbo.Products WITH(UPDLOCK,ROWLOCK,NOWAIT,INDEX(PK_Products)) WHERE ProductId=" + high);
            probe.Rollback();
        }
        var generated = await test.Observer.QuerySingleAsync<string>("SELECT t.text FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.session_id=@session", new { session });
        output.WriteLine(generated);
        Assert.Contains("UPDLOCK", generated); Assert.Contains("HOLDLOCK", generated);
        Assert.Equal(0, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockLedger WITH(READUNCOMMITTED)"));
        blocker.Release(); Assert.False((await post).IsError);
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Reversed_multi_key_writers_wait_on_first_key_and_preserve_both_values() => Run(async test =>
    {
        var seed = await ValuationTestData.SeedAsync(test.Observer, 1, 1);
        var first = seed.Lines[0].ProductId;
        var second = await NativeSql.Scalar<int>(test.Observer, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA')");
        await test.Observer.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@WarehouseId,@first,1,2),(@WarehouseId,@second,1,2)", new { seed.Header.WarehouseId, first, second });
        var blocker = await test.BlockAsync("SELECT Quantity FROM dbo.StockBalances WITH(XLOCK,HOLDLOCK,ROWLOCK,INDEX(PK_StockBalances)) WHERE WarehouseId=" + seed.Header.WarehouseId + " AND ProductId=" + first);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString));
        var provider = test.Own(ValuationTestData.Provider(test.Database.ConnectionString, connections: recording));
        async Task Write(int index, int[] keys)
        {
            using var uow = await provider.GetRequiredService<IUnitOfWorkFactory>().CreateAsync(test.Cancellation.Token);
            uow.BeginTransaction();
            var coordinator = new InventoryValuationCoordinator((ISqlExecutionContext)uow,
                new TraceSessions(provider.GetRequiredService<ILinqToDbSessionFactory>(), output));
            await coordinator.AcquirePostingGateAsync(test.Cancellation.Token);
            await coordinator.LockBalancesAsync(seed.Header.WarehouseId, keys, test.Cancellation.Token);
            foreach (var key in keys)
                await coordinator.AppendMovementAsync(new StockLedgerEntry { WarehouseId=seed.Header.WarehouseId, ProductId=key,
                    DocumentType="GoodsReceipt", DocumentId=seed.Header.Id, DocumentLineId=1000+index*10+key,
                    QuantityChange=1, PostedAtUtc=ValuationTestData.Stamp, PostedByUserId=seed.Actor }, 2, test.Cancellation.Token);
            foreach (var key in keys.OrderBy(x => x)) await coordinator.ApplyBalanceAsync(seed.Header.WarehouseId,key,1,2,test.Cancellation.Token);
            uow.Commit();
        }
        var a = test.Track(Write(0,new[] { second,first })); var sa = await recording.NextAsync();
        var b = test.Track(Write(1,new[] { first,second })); var sb = await recording.NextAsync();
        await test.WaitKeyBlockedAsync(sa,blocker.Session); await test.WaitKeyBlockedAsync(sb,blocker.Session);
        using (var probe = test.Observer.BeginTransaction())
        {
            await NativeSql.Execute(test.Observer,probe,"SELECT Quantity FROM dbo.StockBalances WITH(UPDLOCK,ROWLOCK,NOWAIT,INDEX(PK_StockBalances)) WHERE WarehouseId="+seed.Header.WarehouseId+" AND ProductId="+second);
            probe.Rollback();
        }
        Assert.Equal(0,await NativeSql.Scalar<int>(test.Observer,null,"SELECT COUNT(*) FROM dbo.StockLedger WITH(READUNCOMMITTED)"));
        blocker.Release(); await Task.WhenAll(a,b);
        Assert.Equal(2,await NativeSql.Scalar<int>(test.Observer,null,"SELECT COUNT(*) FROM dbo.StockBalances WHERE Quantity=3 AND InventoryValue=6"));
        Assert.Equal(4,await NativeSql.Scalar<int>(test.Observer,null,"SELECT COUNT(*) FROM dbo.StockValuationEntries WHERE ValueChange=2"));
        output.WriteLine("Both reversed inputs blocked at lowest key; higher key remained independently lockable; committed sums are Q3/V6 each.");
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Gate_timeout_rolls_back_and_never_replays_post() => Run(async test =>
    {
        var seed = await ValuationTestData.SeedAsync(test.Observer,1,2);
        var blocker = await test.BlockAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='MyDmsVn.InventoryValuation.v1',@LockMode='Exclusive',@LockOwner='Transaction',@DbPrincipal='public'; IF @r<0 THROW 51221,'gate failed',1;");
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString));
        var provider = test.Own(ValuationTestData.Provider(test.Database.ConnectionString,seed.Actor,recording));
        var watch=Stopwatch.StartNew();
        var worker=test.Track(ValuationTestData.PostAsync(provider,seed.Header,test.Cancellation.Token));
        var session=await recording.NextAsync(); await test.WaitBlockedAsync(session,blocker.Session);
        Assert.True((await worker).IsError); Assert.InRange(watch.Elapsed.TotalSeconds,14,30);
        Assert.Equal("Draft",await NativeSql.Scalar<string>(test.Observer,null,"SELECT Status FROM dbo.GoodsReceipts"));
        Assert.Equal(0,await NativeSql.Scalar<int>(test.Observer,null,"SELECT COUNT(*) FROM dbo.StockLedger"));
        Assert.Equal(0,await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@session AND resource_type='APPLICATION'",new { session }));
        blocker.Release();
        Assert.False((await ValuationTestData.PostAsync(provider,seed.Header)).IsError);
        Assert.Equal(1,await NativeSql.Scalar<int>(test.Observer,null,"SELECT COUNT(*) FROM dbo.StockValuationEntries"));
    });


    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Deadlock_victim_rolls_back_the_post_without_implicit_retry() => Run(async test =>
    {
        var seed=await ValuationTestData.SeedAsync(test.Observer,1,2);
        var blocker=await test.BlockAsync("SET DEADLOCK_PRIORITY HIGH; SELECT Status FROM dbo.GoodsReceipts WITH(XLOCK,HOLDLOCK,ROWLOCK,INDEX(PK_GoodsReceipts)) WHERE ReceiptId="+seed.Header.Id);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString,lowDeadlockPriority:true));
        var provider = test.Own(ValuationTestData.Provider(test.Database.ConnectionString,seed.Actor,recording));
        var post=test.Track(ValuationTestData.PostAsync(provider,seed.Header,test.Cancellation.Token));
        var session=await recording.NextAsync(); await test.WaitBlockedAsync(session,blocker.Session);
        var exclusive=test.Track(Task.Run(async () =>
        {
            using var command=new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='MyDmsVn.InventoryValuation.v1',@LockMode='Exclusive',@LockOwner='Transaction',@DbPrincipal='public',@LockTimeout=15000; SELECT @r;",blocker.Connection,blocker.Transaction) { CommandTimeout=30 };
            Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync(test.Cancellation.Token))>=0);
        }));
        await exclusive; Assert.True((await post).IsError);
        blocker.Release();
        Assert.Equal("Draft",await NativeSql.Scalar<string>(test.Observer,null,"SELECT Status FROM dbo.GoodsReceipts"));
        Assert.Equal(seed.Header.Version,await NativeSql.Scalar<byte[]>(test.Observer,null,"SELECT RowVersion FROM dbo.GoodsReceipts"));
        Assert.Equal(0,await NativeSql.Scalar<int>(test.Observer,null,"SELECT COUNT(*) FROM dbo.StockLedger"));
        Assert.False((await ValuationTestData.PostAsync(provider,seed.Header)).IsError);
        Assert.Equal(1,await NativeSql.Scalar<int>(test.Observer,null,"SELECT COUNT(*) FROM dbo.StockValuationEntries"));
        output.WriteLine("Native lock cycle selected LOW-priority Post as victim; no status/version/inventory committed; explicit later Post succeeded once.");
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Applock_deadlock_negative_result_is_rolled_back_by_the_owned_caller() => Run(async test =>
    {
        var seed = await ValuationTestData.SeedAsync(test.Observer, 1, 2);
        var blocker = await test.BlockAsync("SET DEADLOCK_PRIORITY HIGH; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='MyDmsVn.InventoryValuation.v1',@LockMode='Exclusive',@LockOwner='Transaction',@DbPrincipal='public'; IF @r<0 THROW 51221,'gate failed',1;");
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString, lowDeadlockPriority: true));
        var provider = test.Own(ValuationTestData.Provider(test.Database.ConnectionString, connections: recording));
        async Task Acquire()
        {
            using var uow = await provider.GetRequiredService<IUnitOfWorkFactory>().CreateAsync(test.Cancellation.Token);
            uow.BeginTransaction();
            var context = (ISqlExecutionContext)uow;
            // Deliberately adversarial caller setup tests rollback after -3. Production Post's gate-first order is tested separately.
            await NativeSql.Execute((SqlConnection)context.Connection, (SqlTransaction)context.Transaction!,
                "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='ValuationTest.Secondary',@LockMode='Exclusive',@LockOwner='Transaction',@DbPrincipal='public'; IF @r<0 THROW 51221,'secondary failed',1; UPDATE dbo.GoodsReceipts SET Note=N'uncommitted sentinel';");
            var coordinator = new InventoryValuationCoordinator(context, provider.GetRequiredService<ILinqToDbSessionFactory>());
            await coordinator.AcquirePostingGateAsync(test.Cancellation.Token);
        }
        var acquire = test.Track(Acquire());
        var session = await recording.NextAsync(); await test.WaitBlockedAsync(session, blocker.Session);
        var other = test.Track(Task.Run(async () =>
        {
            using var command = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='ValuationTest.Secondary',@LockMode='Exclusive',@LockOwner='Transaction',@DbPrincipal='public',@LockTimeout=15000; SELECT @r;", blocker.Connection, blocker.Transaction) { CommandTimeout = 30 };
            Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync(test.Cancellation.Token)) >= 0);
        }));
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => acquire);
        Assert.Contains("(-3)", failure.Message);
        await other; blocker.Release();
        Assert.Equal(seed.Header.Version, await NativeSql.Scalar<byte[]>(test.Observer, null, "SELECT RowVersion FROM dbo.GoodsReceipts"));
        Assert.Equal(0, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE Note=N'uncommitted sentinel'"));
        Assert.Equal(0, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockLedger"));
        output.WriteLine("Observed actual sp_getapplock return -3; caller disposal rolled back sentinel update and released secondary transaction lock.");
    });

    private static async Task Run(Func<Scenario, Task> body)
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Scenario? test = null; Exception? original = null;
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());
            test = new Scenario(database); await test.Observer.OpenAsync();
            await body(test);
        }
        catch (Exception error) { original = error; throw; }
        finally
        {
            try
            {
                if (test == null) await database.DisposeAsync();
                else await test.CleanupAsync();
            }
            catch (Exception cleanup) when (original != null) { throw new AggregateException(original, cleanup); }
        }
    }

    private sealed class Scenario(SqlTestDatabase database)
    {
        public SqlTestDatabase Database { get; } = database;
        public SqlConnection Observer { get; } = new SqlConnection(database.ConnectionString);
        public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        private readonly List<Task> _workers = new();
        private readonly List<IDisposable> _resources = new();
        public T Own<T>(T resource) where T : IDisposable { _resources.Add(resource); return resource; }
        private readonly List<Blocker> _blockers = new();
        public T Track<T>(T task) where T : Task { _workers.Add(task); return task; }
        public async Task<Blocker> BlockAsync(string sql)
        {
            var connection = new SqlConnection(Database.ConnectionString);
            Blocker blocker;
            try
            {
                await connection.OpenAsync(Cancellation.Token);
                blocker = new Blocker(connection, connection.BeginTransaction()); _blockers.Add(blocker);
            }
            catch { connection.Dispose(); throw; }
            blocker.Session = await NativeSql.Scalar<int>(connection, blocker.Transaction, "SELECT CONVERT(int,@@SPID)");
            await NativeSql.Execute(connection, blocker.Transaction, sql);
            return blocker;
        }
        public async Task WaitBlockedAsync(int worker, int owner)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(10))
            {
                if (await Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@worker AND blocking_session_id=@owner AND wait_type LIKE 'LCK%';", new { worker, owner }) == 1) return;
                await Task.Delay(25, Cancellation.Token);
            }
            throw new TimeoutException("Expected independent SQL blocker was not observed.");
        }
        public async Task WaitKeyBlockedAsync(int worker, int owner)
        {
            var watch=Stopwatch.StartNew();
            while(watch.Elapsed<TimeSpan.FromSeconds(10))
            {
                // SQL may report the preceding queued U waiter as blocker; identify the actual held key.
                if(await Observer.QuerySingleAsync<int>(@"SELECT COUNT(*) FROM sys.dm_tran_locks w JOIN sys.dm_tran_locks h
ON w.resource_associated_entity_id=h.resource_associated_entity_id AND w.resource_description=h.resource_description AND w.resource_type=h.resource_type
WHERE w.request_session_id=@worker AND h.request_session_id=@owner AND w.resource_type='KEY'
AND w.request_status IN ('WAIT','CONVERT') AND h.request_status='GRANT' AND h.request_mode='X'",new { worker,owner })>0) return;
                await Task.Delay(25,Cancellation.Token);
            }
            throw new TimeoutException("Expected blocked request on the same independently held key was not observed.");
        }

        public async Task CleanupAsync()
        {
            try
            {
                await SqlConcurrencyCleanup.CompleteAsync(
                    () => { foreach (var blocker in _blockers) blocker.Release(); return Task.CompletedTask; },
                    () => { foreach (var blocker in _blockers) blocker.Dispose(); }, Cancellation,
                    async () => { foreach (var resource in Enumerable.Reverse(_resources)) resource.Dispose(); Observer.Dispose(); await Database.DisposeAsync(); }, TimeSpan.FromSeconds(20), _workers.ToArray());
            }
            finally { Observer.Dispose(); Cancellation.Dispose(); }
        }
    }

    private sealed class Blocker(SqlConnection connection, SqlTransaction transaction) : IDisposable
    {
        public SqlConnection Connection { get; } = connection;
        public SqlTransaction Transaction { get; } = transaction;
        public int Session { get; set; }
        private bool _released;
        public void Release() { if (!_released) { _released = true; Transaction.Rollback(); } }
        public void Dispose() { Transaction.Dispose(); Connection.Dispose(); }
    }

    private sealed class RecordingFactory(string connectionString, bool lowDeadlockPriority = false) : IDbConnectionFactory, IDisposable
    {
        private readonly ConcurrentQueue<int> _ids = new();
        private readonly SemaphoreSlim _signal = new(0);
        public IDbConnection OpenConnection() => OpenConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
        public async Task<IDbConnection> OpenConnectionAsync(CancellationToken token)
        {
            var connection = new SqlConnection(connectionString);
            try { await connection.OpenAsync(token); if(lowDeadlockPriority) await NativeSql.Execute(connection,null,"SET DEADLOCK_PRIORITY LOW"); _ids.Enqueue(await NativeSql.Scalar<int>(connection, null, "SELECT CONVERT(int,@@SPID)")); _signal.Release(); return connection; }
            catch { connection.Dispose(); throw; }
        }
        public async Task<int> NextAsync()
        {
            if (!await _signal.WaitAsync(TimeSpan.FromSeconds(10)) || !_ids.TryDequeue(out var id)) throw new TimeoutException("No worker session.");
            return id;
        }
        public void Dispose() => _signal.Dispose();
    }

    private sealed class TraceSessions(ILinqToDbSessionFactory inner, ITestOutputHelper output) : ILinqToDbSessionFactory
    {
        public List<string> Sql { get; } = new();
        public DataConnection Borrow(ISqlExecutionContext context, bool requireTransaction)
        {
            using var borrowed = inner.Borrow(context, requireTransaction);
            return new DataConnection(borrowed.Options.UseTracing(TraceLevel.Verbose,
                trace => { if (!string.IsNullOrEmpty(trace.SqlText)) { Sql.Add(trace.SqlText!); output.WriteLine(trace.SqlText!); } }));
        }
        public Task<DataConnection> OpenOwnedAsync(CancellationToken token) => inner.OpenOwnedAsync(token);
    }
}
