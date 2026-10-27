using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Contracts;
using MediatR;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SalesReceiptConcurrencyTests(ITestOutputHelper output)
{
    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData("release")]
    [InlineData("cancel")]
    [InlineData("timeout")]
    public Task Shared_gate_is_first_and_failure_never_replays_post(string outcome) => Run(async test =>
    {
        var (data, saved) = await Seed(test);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString));
        var provider = test.Own(SalesPostData.Provider(data, test.Database.ConnectionString, connections: recording));
        var blocker = await test.BlockAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='MyDmsVn.InventoryValuation.v1',@LockMode='Exclusive',@LockOwner='Transaction',@DbPrincipal='public'; IF @r<0 THROW 51221,'gate failed',1;");
        var watch = Stopwatch.StartNew(); var post = test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token));
        var session = await recording.NextAsync(); await test.WaitBlockedAsync(session, blocker.Session);
        Assert.Equal(0, await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@session AND resource_database_id=DB_ID() AND resource_type IN ('OBJECT','KEY','PAGE','RID')", new { session }));
        if (outcome == "release") { blocker.Release(); Assert.False((await post).IsError); }
        else
        {
            if (outcome == "cancel") test.Cancellation.Cancel();
            var failure = await Record.ExceptionAsync(async () => Assert.True((await post).IsError));
            if (failure != null) Assert.IsAssignableFrom<OperationCanceledException>(failure);
            if (outcome == "timeout") Assert.InRange(watch.Elapsed.TotalSeconds, 14, 30);
            await SalesPostData.Unchanged(test.Observer, saved, 20, 300);
            Assert.Equal(0, await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@session AND resource_type='APPLICATION'", new { session }));
            blocker.Release(); Assert.False((await SalesPostData.Post(provider, saved)).IsError);
        }
        Assert.Equal(3, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
        output.WriteLine("Shared gate observed before document/reference locks; outcome=" + outcome + "; no implicit replay.");
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData("Warehouse")]
    [InlineData("Employee")]
    [InlineData("Customer")]
    [InlineData("Product")]
    public Task Reference_points_are_acquired_in_common_order_before_stock(string boundary) => Run(async test =>
    {
        var (data, saved) = await Seed(test);
        var high = await NativeSql.Scalar<int>(test.Observer, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA')");
        await test.Observer.ExecuteAsync("UPDATE dbo.SalesReceiptLines SET ProductId=@high WHERE [LineNo]=1; INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@Warehouse,@high,20,300)", new { high, data.Warehouse });
        var table = boundary == "Warehouse" ? "Warehouses" : boundary == "Employee" ? "Employees" : boundary == "Customer" ? "Customers" : "Products";
        var id = boundary == "Warehouse" ? data.Warehouse : boundary == "Employee" ? data.Employee : boundary == "Customer" ? data.Customer : data.Product;
        var blocker = await test.BlockAsync("SELECT " + boundary + "Id FROM dbo." + table + " WITH(XLOCK,HOLDLOCK,ROWLOCK) WHERE " + boundary + "Id=" + id);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString));
        var provider = test.Own(SalesPostData.Provider(data, test.Database.ConnectionString, connections: recording));
        var post = test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token)); var session = await recording.NextAsync();
        await test.WaitBlockedAsync(session, blocker.Session);
        var nextTable = boundary == "Warehouse" ? "Employees" : boundary == "Employee" ? "Customers" : "Products";
        var nextKey = boundary == "Warehouse" ? "EmployeeId=" + data.Employee : boundary == "Employee" ? "CustomerId=" + data.Customer : "ProductId=" + high;
        using (var probe = test.Observer.BeginTransaction())
        {
            await NativeSql.Execute(test.Observer, probe, "SELECT TOP(1) * FROM dbo." + nextTable + " WITH(XLOCK,ROWLOCK,NOWAIT) WHERE " + nextKey);
            probe.Rollback();
        }
        Assert.Equal(0, await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks l JOIN sys.partitions p ON p.hobt_id=l.resource_associated_entity_id WHERE l.request_session_id=@session AND p.object_id=OBJECT_ID('dbo.StockBalances')", new { session }));
        Assert.Equal(0, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockLedger WITH(READUNCOMMITTED)"));
        var generated = await test.Observer.QuerySingleAsync<string>("SELECT t.text FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.session_id=@session", new { session });
        Assert.Contains("HOLDLOCK", generated); output.WriteLine("Reference blocker=" + boundary + "; next reference still X-lockable; no stock lock. " + generated);
        blocker.Release(); Assert.False((await post).IsError);
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData(false)]
    [InlineData(true)]
    public Task Reversed_lines_lock_all_sorted_stock_keys_before_any_mutation(bool firstKey) => Run(async test =>
    {
        var (data, saved) = await Seed(test);
        var high = await NativeSql.Scalar<int>(test.Observer, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA')");
        await test.Observer.ExecuteAsync("UPDATE dbo.SalesReceiptLines SET ProductId=@high WHERE [LineNo]=1; INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@Warehouse,@high,20,300)", new { high, data.Warehouse });
        var blocker = await test.BlockAsync("SELECT Quantity FROM dbo.StockBalances WITH(XLOCK,HOLDLOCK,ROWLOCK,INDEX(PK_StockBalances)) WHERE WarehouseId=" + data.Warehouse + " AND ProductId=" + (firstKey ? data.Product : high));
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString)); var provider = test.Own(SalesPostData.Provider(data, test.Database.ConnectionString, connections: recording));
        var post = test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token)); var session = await recording.NextAsync(); await test.WaitBlockedAsync(session, blocker.Session);
        if (firstKey)
        {
            using var probe = test.Observer.BeginTransaction();
            await NativeSql.Execute(test.Observer, probe, "SELECT Quantity FROM dbo.StockBalances WITH(UPDLOCK,ROWLOCK,NOWAIT,INDEX(PK_StockBalances)) WHERE WarehouseId=" + data.Warehouse + " AND ProductId=" + high); probe.Rollback();
        }
        else Assert.True(await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks l JOIN sys.partitions p ON p.hobt_id=l.resource_associated_entity_id WHERE l.request_session_id=@session AND l.request_status='GRANT' AND l.request_mode IN('U','RangeS-U') AND p.object_id=OBJECT_ID('dbo.StockBalances')", new { session }) > 0);
        Assert.Equal("Draft", await NativeSql.Scalar<string>(test.Observer, null, "SELECT Status FROM dbo.SalesReceipts WITH(READUNCOMMITTED)"));
        Assert.Equal(0, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.SalesReceiptLines WITH(READUNCOMMITTED) WHERE CostAmount IS NOT NULL"));
        Assert.Equal(0, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockLedger WITH(READUNCOMMITTED)"));
        var generated = await test.Observer.QuerySingleAsync<string>("SELECT t.text FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.session_id=@session", new { session });
        Assert.Contains("UPDLOCK", generated); Assert.Contains("HOLDLOCK", generated); Assert.Contains("PK_StockBalances", generated); output.WriteLine(generated);
        blocker.Release(); Assert.False((await post).IsError);
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData("post-post")]
    [InlineData("save-post")]
    [InlineData("post-save")]
    public Task Same_document_commands_serialize_and_return_precise_conflicts(string order) => Run(async test =>
    {
        var (data, saved) = await Seed(test);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString)); var provider = test.Own(SalesPostData.Provider(data, test.Database.ConnectionString, connections: recording));
        var gate = test.Own(await SqlConcurrencyGate.AcquireAsync(test.Database.ConnectionString, "sales-document-race", test.Cancellation.Token));
        test.ReleaseActions.Add(() => gate.ReleaseAsync(CancellationToken.None).GetAwaiter().GetResult());
        await NativeSql.Execute(test.Observer, null, "CREATE TRIGGER dbo.TR_SalesWait ON dbo.SalesReceipts AFTER UPDATE AS BEGIN " + gate.TransactionWaitSql + " END;");
        var firstPost = order != "save-post" ? test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token)) : null;
        var firstSave = order == "save-post" ? test.Track(data.Update(provider, saved, token: test.Cancellation.Token)) : null;
        var firstSession = await recording.NextAsync(); await gate.WaitForHolderAsync(firstSession, "dbo.SalesReceipts", TimeSpan.FromSeconds(10), test.Cancellation.Token);
        var secondPost = order != "post-save" ? test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token)) : null;
        var secondSave = order == "post-save" ? test.Track(data.Update(provider, saved, token: test.Cancellation.Token)) : null;
        var secondSession = await recording.NextAsync(); await test.WaitBlockedAsync(secondSession, firstSession);
        await gate.ReleaseAsync(CancellationToken.None);
        if (firstPost != null) Assert.False((await firstPost).IsError); else Assert.False((await firstSave!).IsError);
        var code = secondPost != null ? (await secondPost).FirstError.Code : (await secondSave!).FirstError.Code;
        Assert.Equal(order == "save-post" ? "SalesReceipt.ConcurrencyConflict" : "SalesReceipt.AlreadyPosted", code);
        Assert.Equal(order == "save-post" ? 0 : 3, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Competing_sales_cannot_both_consume_insufficient_combined_stock() => Run(async test =>
    {
        var (data, saved) = await Seed(test); await NativeSql.Execute(test.Observer, null, "UPDATE dbo.StockBalances SET Quantity=10,InventoryValue=150");
        using var setup = data.Provider(test.Database.ConnectionString); var other = SalesDraftData.Success(await data.Create(setup));
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString)); var provider = test.Own(SalesPostData.Provider(data, test.Database.ConnectionString, connections: recording));
        var gate = test.Own(await SqlConcurrencyGate.AcquireAsync(test.Database.ConnectionString, "sales-compete", test.Cancellation.Token)); test.ReleaseActions.Add(() => gate.ReleaseAsync(CancellationToken.None).GetAwaiter().GetResult());
        await NativeSql.Execute(test.Observer, null, "CREATE TRIGGER dbo.TR_SalesWait ON dbo.SalesReceipts AFTER UPDATE AS BEGIN " + gate.TransactionWaitSql + " END;");
        var first = test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token)); var a = await recording.NextAsync(); await gate.WaitForHolderAsync(a, "dbo.SalesReceipts", TimeSpan.FromSeconds(10), test.Cancellation.Token);
        var second = test.Track(SalesPostData.Post(provider, other, test.Cancellation.Token)); var b = await recording.NextAsync(); await test.WaitBlockedAsync(b, a);
        await gate.ReleaseAsync(CancellationToken.None); Assert.False((await first).IsError); Assert.Equal("SalesReceipt.InsufficientStock", (await second).FirstError.Code);
        Assert.Equal(4m, await NativeSql.Scalar<decimal>(test.Observer, null, "SELECT Quantity FROM dbo.StockBalances")); Assert.Equal(60m, await NativeSql.Scalar<decimal>(test.Observer, null, "SELECT InventoryValue FROM dbo.StockBalances"));
        Assert.Equal(3, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData(false)]
    [InlineData(true)]
    public Task Receipt_and_sale_use_actual_serial_posting_order(bool receiptFirst) => Run(async test =>
    {
        var (data, saved) = await Seed(test); await NativeSql.Execute(test.Observer, null, "DELETE dbo.SalesReceiptLines WHERE [LineNo]>1; UPDATE dbo.StockBalances SET Quantity=5,InventoryValue=50");
        var receipt = await test.Observer.QuerySingleAsync<long>("INSERT dbo.GoodsReceipts(ReceiptNo,ReceiptDate,WarehouseId,EmployeeId,Status,CreatedByUserId) OUTPUT INSERTED.ReceiptId VALUES(N'GR0000000999','2020-01-01',@Warehouse,@Employee,'Draft',@Actor)", new { data.Warehouse, data.Employee, data.Actor });
        await test.Observer.ExecuteAsync("INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId) VALUES(@receipt,1,@Product,5,20,@Actor)", new { receipt, data.Product, data.Actor });
        var version = await NativeSql.Scalar<byte[]>(test.Observer, null, "SELECT RowVersion FROM dbo.GoodsReceipts");
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString)); var provider = test.Own(SalesPostData.Provider(data, test.Database.ConnectionString, connections: recording));
        async Task<ErrorOr.ErrorOr<PostGoodsReceiptResponse>> Receipt() { using var scope = provider.CreateScope(); return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PostGoodsReceiptCommand(new PostGoodsReceiptRequest(receipt, Convert.ToBase64String(version))), test.Cancellation.Token); }
        var gate = test.Own(await SqlConcurrencyGate.AcquireAsync(test.Database.ConnectionString, "sales-receipt-order", test.Cancellation.Token)); test.ReleaseActions.Add(() => gate.ReleaseAsync(CancellationToken.None).GetAwaiter().GetResult());
        var table = receiptFirst ? "GoodsReceipts" : "SalesReceipts";
        await NativeSql.Execute(test.Observer, null, "CREATE TRIGGER dbo.TR_WriterWait ON dbo." + table + " AFTER UPDATE AS BEGIN " + gate.TransactionWaitSql + " END;");
        var receive = receiptFirst ? test.Track(Receipt()) : null; var sale = !receiptFirst ? test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token)) : null;
        var a = await recording.NextAsync(); await gate.WaitForHolderAsync(a, "dbo." + table, TimeSpan.FromSeconds(10), test.Cancellation.Token);
        if (receiptFirst) sale = test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token)); else receive = test.Track(Receipt());
        var b = await recording.NextAsync(); await test.WaitBlockedAsync(b, a); await gate.ReleaseAsync(CancellationToken.None);
        Assert.False((await receive!).IsError); Assert.False((await sale!).IsError);
        Assert.Equal(7m, await NativeSql.Scalar<decimal>(test.Observer, null, "SELECT Quantity FROM dbo.StockBalances"));
        Assert.Equal(receiptFirst ? 105m : 120m, await NativeSql.Scalar<decimal>(test.Observer, null, "SELECT InventoryValue FROM dbo.StockBalances"));
        Assert.Equal(receiptFirst ? 45m : 30m, await NativeSql.Scalar<decimal>(test.Observer, null, "SELECT CostAmount FROM dbo.SalesReceiptLines"));
        output.WriteLine("Both writers share stock pool; receiptFirst=" + receiptFirst + "; independently observed serial value.");
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Absent_sales_stock_key_holds_PK_range_against_independent_insert() => Run(async test =>
    {
        // Never inserted: a recently deleted unique key can instead retain a ghost-key U lock.
        var (data, saved) = await Seed(test, stock: false);
        var provider = test.Own(data.Provider(test.Database.ConnectionString)); using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create(); uow.BeginTransaction();
        var repo = uow.Repository<ISalesReceiptWriteRepository>(); await repo.AcquirePostingGateAsync(test.Cancellation.Token);
        var states = await repo.LockValuationBalancesAsync(data.Warehouse, new[] { data.Product }, test.Cancellation.Token); Assert.Equal(0m, states[data.Product].Quantity);
        var context = (ISqlExecutionContext)uow; var owner = await NativeSql.Scalar<int>((SqlConnection)context.Connection, (SqlTransaction)context.Transaction!, "SELECT CONVERT(int,@@SPID)");
        var signal = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var insert = test.Track(Task.Run(async () => { using var connection = new SqlConnection(test.Database.ConnectionString); await connection.OpenAsync(test.Cancellation.Token); signal.SetResult(await NativeSql.Scalar<int>(connection, null, "SELECT CONVERT(int,@@SPID)")); await SalesPostData.Stock(connection, data, 20, 300); }));
        await test.WaitBlockedAsync(await WaitForSessionAsync(signal.Task, insert, TimeSpan.FromSeconds(10), test.Cancellation.Token), owner);
        Assert.True(await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks l JOIN sys.partitions p ON p.hobt_id=l.resource_associated_entity_id WHERE l.request_session_id=@owner AND l.request_mode='RangeS-U' AND l.request_status='GRANT' AND p.object_id=OBJECT_ID('dbo.StockBalances') AND p.index_id=1", new { owner }) > 0);
        uow.Rollback(); await insert; Assert.False((await SalesPostData.Post(provider, saved)).IsError);
    });

    [Theory]
    [InlineData("worker-fault")]
    [InlineData("worker-cancel")]
    [InlineData("scenario-cancel")]
    [InlineData("timeout")]
    public async Task Session_handshake_exits_before_publication_failure_and_drains(string boundary)
    {
        var signal = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var failure = new InvalidOperationException("Connection failed before publishing its session.");
        if (boundary == "worker-fault") worker.SetException(failure);
        if (boundary == "worker-cancel") worker.SetCanceled();
        if (boundary == "scenario-cancel") cancellation.Cancel();
        var waiting = WaitForSessionAsync(signal.Task, worker.Task, TimeSpan.FromMilliseconds(50), cancellation.Token);
        var cleaned = false;
        try
        {
            Assert.Same(waiting, await Task.WhenAny(waiting, Task.Delay(TimeSpan.FromSeconds(1))));
            var error = await Record.ExceptionAsync(async () => await waiting);
            if (boundary == "worker-fault") Assert.Same(failure, error);
            else if (boundary == "timeout") Assert.IsType<TimeoutException>(error);
            else Assert.IsAssignableFrom<OperationCanceledException>(error);
        }
        finally
        {
            // Keep the regression's old broken implementation finite as well.
            signal.TrySetCanceled(); worker.TrySetCanceled();
            await SqlConcurrencyCleanup.CompleteAsync(null, null, cancellation,
                () => { cleaned = true; return Task.CompletedTask; }, TimeSpan.FromSeconds(1), waiting, worker.Task);
            Assert.True(cleaned);
        }
    }

    private static async Task<int> WaitForSessionAsync(Task<int> signal, Task worker, TimeSpan timeout, CancellationToken token)
    {
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            var deadline = Task.Delay(timeout, timer.Token);
            var completed = await Task.WhenAny(signal, worker, deadline);
            if (signal.IsCompleted) return await signal;
            if (completed == worker)
            {
                await worker;
                throw new InvalidOperationException("Worker finished without publishing its session.");
            }
            token.ThrowIfCancellationRequested();
            throw new TimeoutException("Worker session was not published within the bounded handshake.");
        }
        finally { timer.Cancel(); }
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Deadlock_victim_rolls_back_sales_and_never_automatically_retries() => Run(async test =>
    {
        var (data, saved) = await Seed(test);
        var blocker = await test.BlockAsync("SET DEADLOCK_PRIORITY HIGH; SELECT Status FROM dbo.SalesReceipts WITH(XLOCK,HOLDLOCK,ROWLOCK) WHERE SaleId=" + saved.SaleId);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString, lowDeadlockPriority: true)); var provider = test.Own(SalesPostData.Provider(data, test.Database.ConnectionString, connections: recording));
        var post = test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token)); var session = await recording.NextAsync(); await test.WaitBlockedAsync(session, blocker.Session);
        var exclusive = test.Track(Task.Run(async () => { using var command = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='MyDmsVn.InventoryValuation.v1',@LockMode='Exclusive',@LockOwner='Transaction',@DbPrincipal='public',@LockTimeout=15000; SELECT @r;", blocker.Connection, blocker.Transaction) { CommandTimeout = 30 }; Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync(test.Cancellation.Token)) >= 0); }));
        await exclusive; Assert.True((await post).IsError); blocker.Release(); await SalesPostData.Unchanged(test.Observer, saved, 20, 300);
        Assert.False((await SalesPostData.Post(provider, saved)).IsError);
    });

    private static async Task<(SalesDraftData Data, SalesReceiptDto Saved)> Seed(Scenario test, bool stock = true)
    {
        var data = await SalesDraftData.Seed(test.Observer); using var provider = data.Provider(test.Database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider)); if (stock) await SalesPostData.Stock(test.Observer, data, 20, 300); return (data, saved);
    }

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData(false)]
    [InlineData(true)]
    public Task Sales_and_reconciliation_share_exclusive_barrier_in_both_orders(bool reconciliationFirst) => Run(async test =>
    {
        var (data, saved) = await Seed(test); await NativeSql.Execute(test.Observer, null, "DELETE dbo.StockBalances");
        var receipt = await test.Observer.QuerySingleAsync<long>("INSERT dbo.GoodsReceipts(ReceiptNo,ReceiptDate,WarehouseId,EmployeeId,Status,CreatedByUserId) OUTPUT INSERTED.ReceiptId VALUES(N'GR0000000001','2020-01-01',@Warehouse,@Employee,'Draft',@Actor)", new { data.Warehouse, data.Employee, data.Actor });
        await test.Observer.ExecuteAsync("INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId) VALUES(@receipt,1,@Product,20,15,@Actor)", new { receipt, data.Product, data.Actor });
        var version = await NativeSql.Scalar<byte[]>(test.Observer, null, "SELECT RowVersion FROM dbo.GoodsReceipts");
        using (var setup = SalesPostData.Provider(data, test.Database.ConnectionString))
        using (var scope = setup.CreateScope()) Assert.False((await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PostGoodsReceiptCommand(new PostGoodsReceiptRequest(receipt, Convert.ToBase64String(version))))).IsError);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString)); var provider = test.Own(SalesPostData.Provider(data, test.Database.ConnectionString, connections: recording));
        if (reconciliationFirst)
        {
            var blocker = await test.BlockAsync("SELECT TOP(1) LedgerId FROM dbo.StockLedger WITH(TABLOCKX,HOLDLOCK)");
            var reconcile = test.Track(provider.GetRequiredService<IStockReconciliationQueryService>().FindDifferencesAsync(test.Cancellation.Token));
            var a = await recording.NextAsync(); await test.WaitBlockedAsync(a, blocker.Session);
            var post = test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token)); var b = await recording.NextAsync(); await test.WaitBlockedAsync(b, a);
            Assert.Equal(0, await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@b AND resource_type IN('OBJECT','KEY','PAGE')", new { b }));
            blocker.Release(); Assert.Empty(await reconcile); Assert.False((await post).IsError);
        }
        else
        {
            var blocker = await test.BlockAsync("SELECT SaleId FROM dbo.SalesReceipts WITH(XLOCK,HOLDLOCK,ROWLOCK) WHERE SaleId=" + saved.SaleId);
            var post = test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token)); var a = await recording.NextAsync(); await test.WaitBlockedAsync(a, blocker.Session);
            var reconcile = test.Track(provider.GetRequiredService<IStockReconciliationQueryService>().FindDifferencesAsync(test.Cancellation.Token)); var b = await recording.NextAsync(); await test.WaitBlockedAsync(b, a);
            Assert.Equal(0, await test.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@b AND resource_type IN('OBJECT','KEY','PAGE')", new { b }));
            blocker.Release(); Assert.False((await post).IsError); Assert.Empty(await reconcile);
        }
        Assert.Equal(210m, await NativeSql.Scalar<decimal>(test.Observer, null, "SELECT InventoryValue FROM dbo.StockBalances"));
        output.WriteLine("Sales/reconciliation actual blocking chain; reconciliationFirst=" + reconciliationFirst + "; independent quantity/value reconciliation empty.");
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Two_sales_with_reversed_product_orders_wait_on_same_lowest_stock_key() => Run(async test =>
    {
        var (data, unused) = await Seed(test); var high = await NativeSql.Scalar<int>(test.Observer, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA')");
        await test.Observer.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@Warehouse,@high,20,300)", new { data.Warehouse, high });
        using var setup = data.Provider(test.Database.ConnectionString);
        var first = await SalesPostData.Create(data, setup, new[] { new SaveSalesReceiptLineRequest(0, high, 3, 1, false), new SaveSalesReceiptLineRequest(0, data.Product, 3, 1, false) });
        // Keep line-range phantom protection on adjacent Sales separate from this stock-key ordering proof.
        var spacer = SalesDraftData.Success(await data.Create(setup));
        var second = await SalesPostData.Create(data, setup, new[] { new SaveSalesReceiptLineRequest(0, data.Product, 3, 1, false), new SaveSalesReceiptLineRequest(0, high, 3, 1, false) });
        var blocker = await test.BlockAsync("SELECT Quantity FROM dbo.StockBalances WITH(XLOCK,HOLDLOCK,ROWLOCK,INDEX(PK_StockBalances)) WHERE WarehouseId=" + data.Warehouse + " AND ProductId=" + data.Product);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString)); var provider = test.Own(SalesPostData.Provider(data, test.Database.ConnectionString, connections: recording));
        var a = test.Track(SalesPostData.Post(provider, first, test.Cancellation.Token)); var sa = await recording.NextAsync(); await test.WaitKeyBlockedAsync(sa, blocker.Session);
        var b = test.Track(SalesPostData.Post(provider, second, test.Cancellation.Token)); var sb = await recording.NextAsync();
        try { await test.WaitKeyBlockedAsync(sb, blocker.Session); }
        catch
        {
            output.WriteLine(await test.Observer.QuerySingleAsync<string>("SELECT r.session_id,r.blocking_session_id,r.wait_type,t.text FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.session_id IN (@sa,@sb) FOR XML RAW", new { sa, sb }));
            throw;
        }
        using (var probe = test.Observer.BeginTransaction()) { await NativeSql.Execute(test.Observer, probe, "SELECT Quantity FROM dbo.StockBalances WITH(UPDLOCK,ROWLOCK,NOWAIT,INDEX(PK_StockBalances)) WHERE WarehouseId=" + data.Warehouse + " AND ProductId=" + high); probe.Rollback(); }
        blocker.Release(); Assert.False((await a).IsError); Assert.False((await b).IsError);
        Assert.Equal(2, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockBalances WHERE Quantity=14 AND InventoryValue=210"));
        Assert.Equal(4, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
        Assert.Equal("Draft", await NativeSql.Scalar<string>(test.Observer, null, "SELECT Status FROM dbo.SalesReceipts WHERE SaleId=" + spacer.SaleId));
        Assert.Equal(0, await NativeSql.Scalar<int>(test.Observer, null, "SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentType='SalesReceipt' AND DocumentId=" + spacer.SaleId));
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData(false)]
    [InlineData(true)]
    public Task Post_holds_unit_until_commit_and_cancellation_after_status_rolls_back(bool cancel) => Run(async test =>
    {
        var (data, saved) = await Seed(test);
        var recording = test.Own(new RecordingFactory(test.Database.ConnectionString)); var provider = test.Own(SalesPostData.Provider(data, test.Database.ConnectionString, connections: recording));
        var gate = test.Own(await SqlConcurrencyGate.AcquireAsync(test.Database.ConnectionString, "sales-unit-and-cancel", test.Cancellation.Token)); test.ReleaseActions.Add(() => gate.ReleaseAsync(CancellationToken.None).GetAwaiter().GetResult());
        await NativeSql.Execute(test.Observer, null, "CREATE TRIGGER dbo.TR_SalesWait ON dbo.SalesReceipts AFTER UPDATE AS BEGIN " + gate.TransactionWaitSql + " END;");
        var post = test.Track(SalesPostData.Post(provider, saved, test.Cancellation.Token)); var session = await recording.NextAsync();
        await gate.WaitForHolderAsync(session, "dbo.SalesReceipts", TimeSpan.FromSeconds(10), test.Cancellation.Token);
        Assert.Equal("Posted", await NativeSql.Scalar<string>(test.Observer, null, "SELECT Status FROM dbo.SalesReceipts WITH(READUNCOMMITTED)"));
        if (cancel)
        {
            test.Cancellation.Cancel(); var error = await Record.ExceptionAsync(async () => Assert.True((await post).IsError));
            if (error != null) Assert.IsAssignableFrom<OperationCanceledException>(error);
            await SalesPostData.Unchanged(test.Observer, saved, 20, 300); await gate.ReleaseAsync(CancellationToken.None);
        }
        else
        {
            var change = test.Track(Task.Run(async () => { using var connection = await recording.OpenConnectionAsync(test.Cancellation.Token); await connection.ExecuteAsync(new SqlTestCommand("UPDATE dbo.Products SET Unit=N'BOX' WHERE ProductId=@Product", new { data.Product }, cancellationToken: test.Cancellation.Token)); }));
            var changer = await recording.NextAsync(); await gate.WaitForBusinessBlockAsync(changer, session, "dbo.Products", TimeSpan.FromSeconds(10), test.Cancellation.Token);
            await gate.ReleaseAsync(CancellationToken.None); Assert.False((await post).IsError); await change;
            var detail = SalesDraftData.Success(await SalesDraftData.Get(provider, saved.SaleId));
            Assert.All(detail.Lines, line => { Assert.Equal("EA", line.Unit); Assert.Equal(15m, line.PostedUnitCost); });
            Assert.Equal("BOX", await NativeSql.Scalar<string>(test.Observer, null, "SELECT Unit FROM dbo.Products"));
        }
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
        public List<Action> ReleaseActions { get; } = new();
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
                    () => { foreach (var release in ReleaseActions) release(); foreach (var blocker in _blockers) blocker.Release(); return Task.CompletedTask; },
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

}
