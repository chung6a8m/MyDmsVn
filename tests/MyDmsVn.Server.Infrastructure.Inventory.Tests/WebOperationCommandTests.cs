using System;
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
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationCommandTests
{
    [SqlServerFact]
    public Task Local_exact10_all_six_commands_preserve_versions_ledger_value_and_never_resolve_journal() => Owned(false, async s => await SixCommands(s, false));
    [SqlServerFact]
    public Task Web_exact12_all_six_capture_original_results_and_replay_without_dynamic_work_or_Read() => Owned(true, async s => await SixCommands(s, true));

    private static async Task SixCommands(Scenario s, bool web)
    {
        var receiptCreate = new CreateGoodsReceiptDraftCommand(new SaveGoodsReceiptRequest(s.Date, s.Data.Warehouse, s.Data.Employee, "receipt", new[] { new SaveGoodsReceiptLineRequest(s.Data.Product, 20, 2) }), s.Operation(WebOperationKind.ReceiptCreate));
        var receipt = Success(await s.Send(receiptCreate));
        Assert.Null(receipt.WarehouseDisplayName);
        Assert.Null(receipt.Lines[0].ProductDisplayName);
        Assert.True(receipt.Lines[0].ProductIsActive);
        var receiptUpdate = new UpdateGoodsReceiptDraftCommand(new UpdateGoodsReceiptDraftRequest(receipt.Id, receipt.Version, s.Date, s.Data.Warehouse, s.Data.Employee, "updated", new[] { new SaveGoodsReceiptLineRequest(s.Data.Product, 30, 3) }), s.Operation(WebOperationKind.ReceiptUpdate, receipt.Id));
        var updatedReceipt = Success(await s.Send(receiptUpdate));
        Assert.NotEqual(receipt.Version, updatedReceipt.Version);
        Assert.NotEqual(receipt.Lines[0].Id, updatedReceipt.Lines[0].Id);
        var receiptPost = new PostGoodsReceiptCommand(new PostGoodsReceiptRequest(receipt.Id, updatedReceipt.Version), s.Operation(WebOperationKind.ReceiptPost, receipt.Id));
        var receiptAck = Success(await s.Send(receiptPost));
        Assert.DoesNotContain("Version", JsonConvert.SerializeObject(receiptAck), StringComparison.Ordinal);
        var salesCreate = new CreateSalesReceiptDraftCommand(new SaveSalesReceiptRequest(s.Date, s.Data.Customer, s.Data.Warehouse, s.Data.Employee, "sales", s.Data.Lines()), s.Operation(WebOperationKind.SalesCreate));
        var sale = Success(await s.Send(salesCreate));
        var salesUpdate = new UpdateSalesReceiptDraftCommand(new UpdateSalesReceiptDraftRequest(sale.SaleId, sale.Version, s.Date, s.Data.Customer, s.Data.Warehouse, s.Data.Employee, "updated", sale.Lines.Select(x => new SaveSalesReceiptLineRequest(x.SaleLineId, x.ProductId, x.Qty, x.UnitPrice, x.IsGift)).ToArray()), s.Operation(WebOperationKind.SalesUpdate, sale.SaleId));
        var updatedSale = Success(await s.Send(salesUpdate));
        Assert.NotEqual(sale.Version, updatedSale.Version);
        var salesPost = new PostSalesReceiptCommand(new PostSalesReceiptRequest(sale.SaleId, updatedSale.Version), s.Operation(WebOperationKind.SalesPost, sale.SaleId));
        var salesAck = Success(await s.Send(salesPost));
        Assert.Equal(8, Convert.FromBase64String(salesAck.Version).Length);
        if (web)
        {
            // Dynamic reference validation would now fail. Original versions, labels, replacement
            // receipt line identities and posted acknowledgements must still replay exactly.
            await s.Oracle.ExecuteAsync("UPDATE dbo.Products SET Name=N'Changed',IsActive=0;");
            Equal(receipt, Success(await s.Send(receiptCreate)));
            Equal(updatedReceipt, Success(await s.Send(receiptUpdate)));
            Equal(receiptAck, Success(await s.Send(receiptPost)));
            Equal(sale, Success(await s.Send(salesCreate)));
            Equal(updatedSale, Success(await s.Send(salesUpdate)));
            Equal(salesAck, Success(await s.Send(salesPost)));
            Assert.Equal(6, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
        }
        else
        {
            Assert.Equal(0, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN(N'WebOperationJournal',N'WebDeploymentState');"));
            Assert.Equal(10, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        Assert.Equal(1, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE Status='Posted';"));
        Assert.Equal(1, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceipts WHERE Status='Posted';"));
        Assert.Equal(4, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
        Assert.Equal(4, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries;"));
        Assert.Equal(24m, await s.Oracle.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances;"));
        Assert.Equal(72m, await s.Oracle.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances;"));
        Assert.Equal(18m, await s.Oracle.QuerySingleAsync<decimal>("SELECT SUM(CostAmount) FROM dbo.SalesReceiptLines;"));
        Assert.Equal(75m, await s.Oracle.QuerySingleAsync<decimal>("SELECT SUM(LineAmount) FROM dbo.SalesReceiptLines;"));
        Assert.Equal(0, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.UserPermissions WHERE PermissionKey LIKE '%.Read' AND Granted=1;"));
    }

    [SqlServerTheory]
    [InlineData("codec")]
    [InlineData("oversize")]
    [InlineData("journal")]
    public Task Receipt_post_precommit_result_failure_rolls_back_status_version_ledger_and_value(string failure) => Owned(true, async s =>
    {
        var receipt = Success(await s.Send(new CreateGoodsReceiptDraftCommand(new SaveGoodsReceiptRequest(s.Date, s.Data.Warehouse, s.Data.Employee, null, new[] { new SaveGoodsReceiptLineRequest(s.Data.Product, 30, 3) }), s.Operation(WebOperationKind.ReceiptCreate))));
        if (failure == "journal")
            await s.Oracle.ExecuteAsync("CREATE TRIGGER dbo.TestRejectJournal ON dbo.WebOperationJournal AFTER INSERT AS BEGIN THROW 51997, 'Injected journal failure.',1; END;");
        s.Codec.Failure = failure;
        var command = new PostGoodsReceiptCommand(new PostGoodsReceiptRequest(receipt.Id, receipt.Version), s.Operation(WebOperationKind.ReceiptPost, receipt.Id));
        Assert.True((await s.Send(command)).IsError);
        Assert.Equal("Draft", await s.Oracle.QuerySingleAsync<string>("SELECT Status FROM dbo.GoodsReceipts;"));
        Assert.Equal(Convert.FromBase64String(receipt.Version), await s.Oracle.QuerySingleAsync<byte[]>("SELECT RowVersion FROM dbo.GoodsReceipts;"));
        foreach (var table in new[] { "StockLedger", "StockValuationEntries", "StockBalances" })
            Assert.Equal(0, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo." + table));
        Assert.Equal(1, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    });

    [SqlServerFact]
    public Task Sales_post_journal_failure_rolls_back_version_frozen_cost_and_all_inventory_effects() => Owned(true, async s =>
    {
        var receipt = Success(await s.Send(new CreateGoodsReceiptDraftCommand(new SaveGoodsReceiptRequest(s.Date, s.Data.Warehouse, s.Data.Employee, null, new[] { new SaveGoodsReceiptLineRequest(s.Data.Product, 30, 3) }), s.Operation(WebOperationKind.ReceiptCreate))));
        Success(await s.Send(new PostGoodsReceiptCommand(new PostGoodsReceiptRequest(receipt.Id, receipt.Version), s.Operation(WebOperationKind.ReceiptPost, receipt.Id))));
        var sale = Success(await s.Send(new CreateSalesReceiptDraftCommand(new SaveSalesReceiptRequest(s.Date, s.Data.Customer, s.Data.Warehouse, s.Data.Employee, null, s.Data.Lines()), s.Operation(WebOperationKind.SalesCreate))));
        await s.Oracle.ExecuteAsync("CREATE TRIGGER dbo.TestRejectJournal ON dbo.WebOperationJournal AFTER INSERT AS BEGIN THROW 51997, 'Injected journal failure.',1; END;");
        Assert.True((await s.Send(new PostSalesReceiptCommand(new PostSalesReceiptRequest(sale.SaleId, sale.Version), s.Operation(WebOperationKind.SalesPost, sale.SaleId)))).IsError);
        Assert.Equal("Draft", await s.Oracle.QuerySingleAsync<string>("SELECT Status FROM dbo.SalesReceipts;"));
        Assert.Equal(Convert.FromBase64String(sale.Version), await s.Oracle.QuerySingleAsync<byte[]>("SELECT RowVersion FROM dbo.SalesReceipts;"));
        Assert.Equal(0, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceiptLines WHERE CostAmount IS NOT NULL;"));
        Assert.Equal(1, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
        Assert.Equal(1, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries;"));
        Assert.Equal(30m, await s.Oracle.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances;"));
        Assert.Equal(90m, await s.Oracle.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances;"));
        Assert.Equal(3, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    });

    [SqlServerFact]
    public Task Maximum_sales_shape_is_encoded_without_truncation_and_oversize_create_rolls_back() => Owned(true, async s =>
    {
        await s.Oracle.ExecuteAsync("UPDATE dbo.Products SET Name=REPLICATE(NCHAR(1),256),Code=REPLICATE(N'C',32),Unit=REPLICATE(N'U',32);");
        var body = new SaveSalesReceiptRequest(s.Date, s.Data.Customer, s.Data.Warehouse, s.Data.Employee, new string('x', 1000), Enumerable.Range(0, 200).Select(_ => new SaveSalesReceiptLineRequest(0, s.Data.Product, 1, 1, false)).ToArray());
        var saved = Success(await s.Send(new CreateSalesReceiptDraftCommand(body, s.Operation(WebOperationKind.SalesCreate))));
        Assert.Equal(200, saved.Lines.Count);
        Assert.InRange(s.Codec.LastLength, 1, 1048576);
        s.Codec.Failure = "boundary";
        var boundary = Success(await s.Send(new CreateSalesReceiptDraftCommand(body, s.Operation(WebOperationKind.SalesCreate))));
        Assert.Equal(200, boundary.Lines.Count);
        Assert.Equal(1048576, await s.Oracle.QuerySingleAsync<int>("SELECT MAX(DATALENGTH(ResultBytes)) FROM dbo.WebOperationJournal;"));
        s.Codec.Failure = "oversize";
        Assert.Equal("Operation.ResultUnavailable", (await s.Send(new CreateSalesReceiptDraftCommand(body, s.Operation(WebOperationKind.SalesCreate)))).FirstError.Code);
        Assert.Equal(2, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceipts;"));
        Assert.Equal(400, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceiptLines;"));
        Assert.Equal(2, await s.Oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    });

    private static T Success<T>(ErrorOr<T> result)
    {
        Assert.False(result.IsError, result.IsError ? result.FirstError.Code : null);
        return result.Value;
    }
    private static void Equal<T>(T expected, T actual) => Assert.Equal(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(actual));
    private static async Task Owned(bool web, Func<Scenario, Task> body)
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? failure = null;
        try
        {
            Assert.True((web ? new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString) : new DatabaseMigrationRunner().Migrate(db.ConnectionString)).Successful);
            using var oracle = new SqlConnection(db.ConnectionString);
            await oracle.OpenAsync();
            var data = await SalesDraftData.Seed(oracle);
            foreach (var grant in new[] { PermissionKeys.SalesReceiptsWrite, PermissionKeys.SalesReceiptsPost, PermissionKeys.InventoryGoodsReceiptsWrite, PermissionKeys.InventoryGoodsReceiptsPost })
                await oracle.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@actor,@grant,1);", new
                {
                    actor = data.Actor,
                    grant
                });
            var services = new ServiceCollection();
            services.AddSqlPersistence(db.ConnectionString);
            services.AddServerApplication();
            services.Replace(ServiceDescriptor.Scoped<ICurrentUserAccessor>(_ => new ActorAccessor(data.Actor)));
            if (!web)
            {
                var registration = services.Last(x => x.ServiceType == typeof(IUnitOfWorkFactory));
                services.Remove(registration);
                services.AddScoped<IUnitOfWorkFactory>(sp => new GuardFactory((IUnitOfWorkFactory)registration.ImplementationFactory!(sp)));
            }
            using var provider = services.BuildServiceProvider();
            var s = new Scenario { Oracle = oracle, Data = data, Provider = provider, Web = web };
            if (web)
            {
                s.Epoch = await oracle.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;");
                await oracle.ExecuteAsync("DECLARE @n datetime2(7)=SYSUTCDATETIME();INSERT dbo.WebAuthSessions VALUES(@session,@actor,@n,DATEADD(day,30,@n),DATEADD(day,7,@n),NULL);", new
                {
                    session = s.Session,
                    actor = data.Actor
                });
            }
            await body(s);
        }
        catch (Exception error) { failure = error; throw; }
        finally { try { await db.DisposeAsync(); } catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); } }
    }
    private sealed class Scenario
    {
        public SqlConnection Oracle = null!; public SalesDraftData Data = null!; public ServiceProvider Provider = null!; public bool Web; public Guid Epoch; public Guid Session = Guid.NewGuid(); public TestCodec Codec = new TestCodec(); public DateTime Date = new DateTime(2026, 11, 1);
        public WebOperationContext? Operation(WebOperationKind kind, long? target = null) => Web ? new WebOperationContext(Guid.NewGuid(), Epoch, Session, kind, target, new byte[32], Codec) : null;
        public async Task<ErrorOr<T>> Send<T>(IRequest<ErrorOr<T>> command)
        {
            using var scope = Provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
        }
    }
    // Test-only DTO codec exercises the Application seam; B04 owns canonical production wire mapping.
    private sealed class TestCodec : IWebOperationResultCodec
    {
        public string? Failure; public int LastLength;
        public byte[] Encode<T>(WebOperationKind kind, T result)
        {
            if (Failure == "codec")
                throw new InvalidOperationException("Injected codec failure.");
            if (Failure == "oversize")
                return new byte[1048577];
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
            LastLength = bytes.Length;
            if (Failure == "boundary")
            {
                var padded = Enumerable.Repeat((byte)32, 1048576).ToArray();
                Array.Copy(bytes, padded, bytes.Length);
                return padded;
            }
            return bytes;
        }
        public T Decode<T>(WebOperationKind kind, byte[] bytes) => JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes))!;
    }
    private sealed class ActorAccessor : ICurrentUserAccessor
    {
        public ActorAccessor(int id)
        {
            Current = CurrentUser.Authenticated(id, "sales", "Sales", true);
        }
        public CurrentUser Current
        {
            get;
        }
    }
    private sealed class GuardFactory : IUnitOfWorkFactory
    {
        private readonly IUnitOfWorkFactory _inner; public GuardFactory(IUnitOfWorkFactory inner)
        {
            _inner = inner;
        }
        public IUnitOfWork Create() => new Guard(_inner.Create());
        public async Task<IUnitOfWork> CreateAsync(CancellationToken token) => new Guard(await _inner.CreateAsync(token));
    }
    private sealed class Guard : IUnitOfWork
    {
        private readonly IUnitOfWork _inner; public Guard(IUnitOfWork inner)
        {
            _inner = inner;
        }
        public UnitOfWorkState State => _inner.State;
        public void BeginTransaction() => _inner.BeginTransaction(); public void Commit() => _inner.Commit(); public void Rollback() => _inner.Rollback(); public void Dispose() => _inner.Dispose();
        public T Repository<T>() where T : class
        {
            Assert.NotEqual(typeof(IWebOperationJournal), typeof(T));
            return _inner.Repository<T>();
        }
    }
}
