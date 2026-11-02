using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationTerminalCommandTests
{
    // Removing the shared actor/key fence, or accepting CANCELLED as a replay, must
    // fail these physical source/ledger checks for each actual Application command.
    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData(WebOperationKind.SalesCreate, false)]
    [InlineData(WebOperationKind.SalesCreate, true)]
    [InlineData(WebOperationKind.SalesUpdate, false)]
    [InlineData(WebOperationKind.SalesUpdate, true)]
    [InlineData(WebOperationKind.SalesPost, false)]
    [InlineData(WebOperationKind.SalesPost, true)]
    [InlineData(WebOperationKind.ReceiptCreate, false)]
    [InlineData(WebOperationKind.ReceiptCreate, true)]
    [InlineData(WebOperationKind.ReceiptUpdate, false)]
    [InlineData(WebOperationKind.ReceiptUpdate, true)]
    [InlineData(WebOperationKind.ReceiptPost, false)]
    [InlineData(WebOperationKind.ReceiptPost, true)]
    public Task All_six_original_commands_and_operator_are_serialized_under_both_fence_orders(WebOperationKind kind, bool operatorFirst) => Owned(async s =>
    {
        var command = await s.Prepare(kind);
        var before = await s.Physical();
        s.Gate = await SqlConcurrencyGate.AcquireAsync(s.Database.ConnectionString, "web5-terminal", s.Stop.Token);
        await s.Oracle.ExecuteAsync("CREATE TRIGGER dbo.TestTerminalFenceGate ON dbo.WebOperationJournal AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE OperationId='" + s.Subject!.OperationId.ToString("D") + "') BEGIN " + s.Gate.TransactionWaitSql + " END END;");
        s.Operator = new OperatorContext(new SqlConnection(s.Database.ConnectionString));
        await s.Operator.Sql.OpenAsync(s.Stop.Token); s.Operator.Tx = s.Operator.Sql.BeginTransaction();
        Task? terminal = null; Task<Outcome>? writer = null;
        if (operatorFirst) terminal = s.Track(s.Record()); else writer = s.Track(command());
        var holder = await s.HolderAtGate();
        if (operatorFirst)
        {
            Assert.Equal(s.Operator.Sql.ServerProcessId, holder);
            writer = s.Track(command());
            await s.WaitingOnKey(holder, null);
        }
        else
        {
            terminal = s.Track(s.Record());
            await s.WaitingOnKey(holder, s.Operator.Sql.ServerProcessId);
        }
        await s.Gate.ReleaseAsync(CancellationToken.None);
        if (operatorFirst)
        {
            await terminal!; s.Operator.Tx.Commit();
            var rejected = await writer!;
            Assert.Equal("Operation.ResultUnavailable", rejected.Error);
            Assert.Equal(before, await s.Physical());
            Assert.Equal("CANCELLED", await s.Oracle.QuerySingleAsync<string>("SELECT Disposition FROM dbo.WebOperationJournal WHERE OperationId=@id;", new { id = s.Subject.OperationId }));
            Assert.Equal(1, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit WHERE OperationId=@id;", new { id = s.Subject.OperationId }));
            // Explicit subsequent attempts stay fenced, including unchanged original bytes.
            Assert.Equal("Operation.ResultUnavailable", (await command()).Error);
            Assert.Equal(before, await s.Physical());
        }
        else
        {
            var committed = await writer!; Assert.Null(committed.Error); Assert.NotNull(committed.Value);
            Assert.Equal("Operation.ResolutionConflict", (await Assert.ThrowsAsync<WebOperationException>(async () => await terminal!)).Code);
            if (s.Operator.Tx.Connection != null) s.Operator.Tx.Rollback();
            Assert.Equal("COMMITTED", await s.Oracle.QuerySingleAsync<string>("SELECT Disposition FROM dbo.WebOperationJournal WHERE OperationId=@id;", new { id = s.Subject.OperationId }));
            Assert.Equal(0, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
            var after = await s.Physical(); Assert.NotEqual(before, after);
            var retained = await s.Oracle.QuerySingleAsync<byte[]>("SELECT ResultBytes FROM dbo.WebOperationJournal WHERE OperationId=@id;", new { id = s.Subject.OperationId });
            Assert.Equal(committed.Value, Encoding.UTF8.GetString(retained));
            Assert.Equal(committed.Value, (await command()).Value);
            Assert.Equal(after, await s.Physical());
            await s.AssertResultAndLedger(kind);
        }
        Assert.Equal(1, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@id;", new { actor = s.Data.Actor, id = s.Subject.OperationId }));
        Assert.Equal(0, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.UserPermissions WHERE PermissionKey LIKE '%.Read' AND Granted=1;"));
    });

    private static async Task Owned(Func<Scenario, Task> body)
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Scenario? s = null; Exception? failure = null;
        try { Assert.True(new DatabaseMigrationRunner().MigrateWeb5(db.ConnectionString).Successful); s = await Scenario.Create(db); await body(s); }
        catch (Exception error) { failure = error; throw; }
        finally { try { if (s == null) await db.DisposeAsync(); else await s.Cleanup(); } catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); } }
    }

    private sealed class Scenario
    {
        public SqlTestDatabase Database = null!; public SqlConnection Oracle = null!; public SalesDraftData Data = null!; public ServiceProvider Provider = null!;
        public Guid Epoch, Session = Guid.NewGuid(); public WebOperationContext? Subject; public SqlConcurrencyGate? Gate; public OperatorContext? Operator;
        public CancellationTokenSource Stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        private readonly List<Task> _workers = new List<Task>();
        private readonly DateTime _date = new DateTime(2026, 11, 2);
        private long? _target; private string? _beforeVersion;
        public static async Task<Scenario> Create(SqlTestDatabase db)
        {
            var s = new Scenario { Database = db, Oracle = new SqlConnection(db.ConnectionString) };
            try
            {
                await s.Oracle.OpenAsync(); s.Data = await SalesDraftData.Seed(s.Oracle);
                foreach (var permission in new[] { PermissionKeys.SalesReceiptsWrite, PermissionKeys.SalesReceiptsPost, PermissionKeys.InventoryGoodsReceiptsWrite, PermissionKeys.InventoryGoodsReceiptsPost })
                    await s.Oracle.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@actor,@permission,1);", new { actor = s.Data.Actor, permission });
                s.Epoch = await s.Oracle.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;");
                await s.Oracle.ExecuteAsync("DECLARE @n datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebAuthSessions VALUES(@session,@actor,@n,DATEADD(day,30,@n),DATEADD(day,7,@n),NULL);", new { session = s.Session, actor = s.Data.Actor });
                var services = new ServiceCollection(); services.AddSqlPersistence(db.ConnectionString); services.AddServerApplication();
                services.Replace(ServiceDescriptor.Scoped<ICurrentUserAccessor>(_ => new Actor(s.Data.Actor))); s.Provider = services.BuildServiceProvider();
                return s;
            }
            catch { s.Provider?.Dispose(); s.Oracle.Dispose(); s.Stop.Dispose(); throw; }
        }
        private WebOperationContext Operation(WebOperationKind kind, long? target = null) => new WebOperationContext(Guid.NewGuid(), Epoch, Session, kind, target, Enumerable.Repeat((byte)9, 32).ToArray(), new Codec());
        private async Task<ErrorOr<T>> Send<T>(IRequest<ErrorOr<T>> command)
        {
            using var scope = Provider.CreateScope(); return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, Stop.Token);
        }
        private Func<Task<Outcome>> SubjectCommand<T>(IRequest<ErrorOr<T>> command) => async () =>
        {
            var result = await Send(command); return result.IsError ? new Outcome { Error = result.FirstError.Code } : new Outcome { Value = JsonConvert.SerializeObject(result.Value) };
        };
        private static T Success<T>(ErrorOr<T> value) { Assert.False(value.IsError, value.IsError ? value.FirstError.Code : null); return value.Value; }
        public async Task<Func<Task<Outcome>>> Prepare(WebOperationKind kind)
        {
            // Real receipt posting establishes independently valued starting inventory (30 × 3).
            var stock = Success(await Send(new CreateGoodsReceiptDraftCommand(new SaveGoodsReceiptRequest(_date, Data.Warehouse, Data.Employee, "stock", new[] { new SaveGoodsReceiptLineRequest(Data.Product, 30, 3) }), Operation(WebOperationKind.ReceiptCreate))));
            Success(await Send(new PostGoodsReceiptCommand(new PostGoodsReceiptRequest(stock.Id, stock.Version), Operation(WebOperationKind.ReceiptPost, stock.Id))));
            if (kind == WebOperationKind.ReceiptCreate)
            {
                Subject = Operation(kind); return SubjectCommand(new CreateGoodsReceiptDraftCommand(new SaveGoodsReceiptRequest(_date, Data.Warehouse, Data.Employee, "subject", new[] { new SaveGoodsReceiptLineRequest(Data.Product, 5, 4) }), Subject));
            }
            if (kind == WebOperationKind.ReceiptUpdate || kind == WebOperationKind.ReceiptPost)
            {
                var receipt = Success(await Send(new CreateGoodsReceiptDraftCommand(new SaveGoodsReceiptRequest(_date, Data.Warehouse, Data.Employee, "subject", new[] { new SaveGoodsReceiptLineRequest(Data.Product, 5, 4) }), Operation(WebOperationKind.ReceiptCreate))));
                _target = receipt.Id; _beforeVersion = receipt.Version; Subject = Operation(kind, receipt.Id);
                return kind == WebOperationKind.ReceiptPost ? SubjectCommand(new PostGoodsReceiptCommand(new PostGoodsReceiptRequest(receipt.Id, receipt.Version), Subject))
                    : SubjectCommand(new UpdateGoodsReceiptDraftCommand(new UpdateGoodsReceiptDraftRequest(receipt.Id, receipt.Version, _date, Data.Warehouse, Data.Employee, "changed", new[] { new SaveGoodsReceiptLineRequest(Data.Product, 7, 5) }), Subject));
            }
            if (kind == WebOperationKind.SalesCreate)
            {
                Subject = Operation(kind); return SubjectCommand(new CreateSalesReceiptDraftCommand(new SaveSalesReceiptRequest(_date, Data.Customer, Data.Warehouse, Data.Employee, "subject", Data.Lines()), Subject));
            }
            var sale = Success(await Send(new CreateSalesReceiptDraftCommand(new SaveSalesReceiptRequest(_date, Data.Customer, Data.Warehouse, Data.Employee, "subject", Data.Lines()), Operation(WebOperationKind.SalesCreate))));
            _target = sale.SaleId; _beforeVersion = sale.Version; Subject = Operation(kind, sale.SaleId);
            return kind == WebOperationKind.SalesPost ? SubjectCommand(new PostSalesReceiptCommand(new PostSalesReceiptRequest(sale.SaleId, sale.Version), Subject))
                : SubjectCommand(new UpdateSalesReceiptDraftCommand(new UpdateSalesReceiptDraftRequest(sale.SaleId, sale.Version, _date, Data.Customer, Data.Warehouse, Data.Employee, "changed", sale.Lines.Select(line => new SaveSalesReceiptLineRequest(line.SaleLineId, line.ProductId, line.Qty, line.UnitPrice, line.IsGift)).ToArray()), Subject));
        }
        public Task Record() => new SqlWebOperationResolutionMaintenance(Operator!).RecordNotExecutedAsync(Subject!, Data.Actor, Guid.NewGuid(), "case-race", "ORIGINAL_NOT_EXECUTED", "evidence-race", Stop.Token);
        public T Track<T>(T worker) where T : Task { _workers.Add(worker); return worker; }
        public async Task<int> HolderAtGate()
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < TimeSpan.FromSeconds(10))
            {
                var holders = (await Oracle.QueryAsync<int>("SELECT session_id FROM sys.dm_exec_requests WHERE blocking_session_id=@controller AND database_id=DB_ID();", new { controller = Gate!.ControllerSessionId })).ToArray();
                if (holders.Length == 1) return holders[0];
                await Task.Delay(25, Stop.Token);
            }
            throw new TimeoutException("Original actor did not reach the test gate while holding its transaction.");
        }
        public async Task WaitingOnKey(int holder, int? waiter)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < TimeSpan.FromSeconds(10))
            {
                if (await Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_exec_requests r WHERE r.blocking_session_id=@holder AND (@waiter IS NULL OR r.session_id=@waiter) AND r.database_id=DB_ID() AND EXISTS(SELECT 1 FROM sys.dm_tran_locks l WHERE l.request_session_id=r.session_id AND l.resource_type='APPLICATION' AND l.request_status='WAIT');", new { holder, waiter }) == 1) return;
                await Task.Delay(25, Stop.Token);
            }
            throw new TimeoutException("Contender did not block on the original transaction-owned key fence.");
        }
        public async Task<string> Physical()
        {
            var parts = new List<string>();
            foreach (var table in new[] { "GoodsReceipts", "GoodsReceiptLines", "SalesReceipts", "SalesReceiptLines", "StockLedger", "StockValuationEntries", "StockBalances" })
                parts.Add(await Oracle.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo." + table + " ORDER BY 1 FOR JSON PATH,INCLUDE_NULL_VALUES);"));
            return string.Join("\n", parts);
        }
        public async Task AssertResultAndLedger(WebOperationKind kind)
        {
            var id = await Oracle.QuerySingleAsync<long>("SELECT ResultDocumentId FROM dbo.WebOperationJournal WHERE OperationId=@id;", new { id = Subject!.OperationId });
            if (_target.HasValue) Assert.Equal(_target.Value, id);
            var bytes = await Oracle.QuerySingleAsync<byte[]>("SELECT ResultBytes FROM dbo.WebOperationJournal WHERE OperationId=@id;", new { id = Subject.OperationId });
            var result = Newtonsoft.Json.Linq.JObject.Parse(Encoding.UTF8.GetString(bytes));
            if (kind == WebOperationKind.ReceiptPost) Assert.Null(result["Version"]);
            else
            {
                var table = (byte)kind <= 3 ? "SalesReceipts" : "GoodsReceipts";
                var column = (byte)kind <= 3 ? "SaleId" : "ReceiptId";
                Assert.Equal(Convert.ToBase64String(await Oracle.QuerySingleAsync<byte[]>("SELECT RowVersion FROM dbo." + table + " WHERE " + column + "=@id;", new { id })), (string?)result["Version"]);
                if (_beforeVersion != null) Assert.NotEqual(_beforeVersion, (string?)result["Version"]);
            }
            if (kind != WebOperationKind.SalesPost && kind != WebOperationKind.ReceiptPost)
            {
                var sale = (byte)kind <= 3;
                var physicalIds = await Oracle.QueryAsync<long>(sale
                    ? "SELECT SaleLineId FROM dbo.SalesReceiptLines WHERE SaleId=@id ORDER BY [LineNo];"
                    : "SELECT ReceiptLineId FROM dbo.GoodsReceiptLines WHERE ReceiptId=@id ORDER BY [LineNo];", new { id });
                Assert.Equal(physicalIds.ToArray(), result["Lines"]!.Select(line => (long)line[sale ? "SaleLineId" : "Id"]!).ToArray());
            }
            var postedSale = kind == WebOperationKind.SalesPost; var postedReceipt = kind == WebOperationKind.ReceiptPost;
            Assert.Equal(postedSale ? 24m : postedReceipt ? 35m : 30m, await Oracle.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances;"));
            Assert.Equal(postedSale ? 72m : postedReceipt ? 110m : 90m, await Oracle.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances;"));
            Assert.Equal(postedSale ? 4 : postedReceipt ? 2 : 1, await Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
            Assert.Equal(postedSale ? 4 : postedReceipt ? 2 : 1, await Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries;"));
            Assert.Equal(postedSale ? 18m : 0m, await Oracle.QuerySingleAsync<decimal>("SELECT COALESCE(SUM(CostAmount),0) FROM dbo.SalesReceiptLines;"));
        }
        public async Task Cleanup()
        {
            try
            {
                await SqlConcurrencyCleanup.CompleteAsync(async () => { if (Gate != null) await Gate.ReleaseAsync(CancellationToken.None); if (Operator?.Tx?.Connection != null) Operator.Tx.Rollback(); },
                    null, Stop, Provider, async () => { Gate?.Dispose(); Operator?.Dispose(); Oracle.Dispose(); await Database.DisposeAsync(); }, TimeSpan.FromSeconds(20), _workers.ToArray());
            }
            finally { Stop.Dispose(); }
        }
    }
    private sealed class Outcome { public string? Error; public string? Value; }
    private sealed class OperatorContext : ISqlExecutionContext, IDisposable
    {
        public OperatorContext(SqlConnection sql) { Sql = sql; }
        public SqlConnection Sql; public SqlTransaction Tx = null!;
        public IDbConnection Connection => Sql; public IDbTransaction? Transaction => Tx;
        public void Dispose() { Tx?.Dispose(); Sql.Dispose(); }
    }
    private sealed class Actor : ICurrentUserAccessor
    {
        public Actor(int id) { Current = CurrentUser.Authenticated(id, "sales", "Sales", true); }
        public CurrentUser Current { get; }
    }
    private sealed class Codec : IWebOperationResultCodec
    {
        public byte[] Encode<T>(WebOperationKind kind, T result) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
        public T Decode<T>(WebOperationKind kind, byte[] bytes) => JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes))!;
    }
}
