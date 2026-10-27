using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Generic;
using MyDmsVn.Testing.Sql;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SalesReceiptPostTests
{
    [SqlServerFact]
    public Task Later_insufficient_product_rejects_an_earlier_sufficient_pool_before_any_write() => NativeSql.Owned(async database =>
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); using var provider = data.Provider(database.ConnectionString);
        var high = await NativeSql.Scalar<int>(sql, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA')");
        var saved = await SalesPostData.Create(data, provider, new[] { new SaveSalesReceiptLineRequest(0, data.Product, 1, 1, false), new SaveSalesReceiptLineRequest(0, high, 1, 1, true) });
        await SalesPostData.Stock(sql, data, 20, 300);
        var result = await SalesPostData.Post(provider, saved); Assert.Equal("SalesReceipt.InsufficientStock", result.FirstError.Code);
        await SalesPostData.Unchanged(sql, saved, 20, 300);
        Assert.Equal(1, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.StockBalances"));
    });

    [SqlServerFact]
    public Task Maximum_quantity_and_value_can_be_fully_depleted_exactly() => NativeSql.Owned(async database =>
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); using var provider = data.Provider(database.ConnectionString);
        var saved = await SalesPostData.Create(data, provider, new[] { new SaveSalesReceiptLineRequest(0, data.Product, 99999999999999.9999m, 0, false) });
        await SalesPostData.Stock(sql, data, 99999999999999.9999m, 999999999999999999999999.9999m);
        Assert.False((await SalesPostData.Post(provider, saved)).IsError);
        Assert.Equal(999999999999999999999999.9999m, await NativeSql.Scalar<decimal>(sql, null, "SELECT CostAmount FROM dbo.SalesReceiptLines"));
        Assert.Equal(1, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.StockBalances WHERE Quantity=0 AND InventoryValue=0"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Restricted_runtime_posts_on_owned_transaction_without_schema_authority() => NativeSql.Owned(async database =>
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider)); await SalesPostData.Stock(sql, data, 20, 300);
        await NativeSql.Execute(sql, null, "CREATE USER [SalesRuntime] WITHOUT LOGIN; CREATE USER [SalesMigration] WITHOUT LOGIN;");
        var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "deployment", "sql", "Provision-MyDmsVnPrincipals.sql"))) directory = directory.Parent;
        Assert.NotNull(directory);
        await NativeSql.Execute(sql, null, System.IO.File.ReadAllText(System.IO.Path.Combine(directory!.FullName, "deployment", "sql", "Provision-MyDmsVnPrincipals.sql")).Replace("$(RuntimeUser)", "SalesRuntime").Replace("$(MigrationUser)", "SalesMigration"));
        using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create(); var context = (ISqlExecutionContext)uow; var connection = (SqlConnection)context.Connection;
        await NativeSql.Execute(connection, null, "EXECUTE AS USER=N'SalesRuntime'");
        try
        {
            Assert.Equal(0, await NativeSql.Scalar<int>(connection, null, "SELECT COALESCE(HAS_PERMS_BY_NAME(N'dbo',N'SCHEMA',N'ALTER'),0)"));
            uow.BeginTransaction(); var repo = uow.Repository<ISalesReceiptWriteRepository>(); await repo.AcquirePostingGateAsync(default);
            var header = await repo.LockHeaderAsync(saved.SaleId, default); var snapshot = await repo.LockPostSnapshotAsync(header!, default);
            await repo.LockValuationBalancesAsync(data.Warehouse, new[] { data.Product }, default);
            var cost = InventoryValuationMath.CalculateIssue(20, 300, snapshot.Lines.Select(line => new InventoryIssueLine(line.LineNo, line.Qty)).ToArray());
            var posted = await repo.TryMarkPostedAsync(saved.SaleId, header!.Version, DateTime.UtcNow, data.Actor, default);
            await repo.ApplyPostedInventoryAsync(posted!, snapshot.Lines, new Dictionary<int, InventoryIssueCost> { [data.Product] = cost }, DateTime.UtcNow, data.Actor, default);
            uow.Commit();
        }
        finally { if (uow.State == UnitOfWorkState.ActiveTransaction) uow.Rollback(); await NativeSql.Execute(connection, null, "REVERT"); }
        Assert.Equal(210m, await NativeSql.Scalar<decimal>(sql, null, "SELECT InventoryValue FROM dbo.StockBalances"));
        Assert.Equal(3, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
    });

    [SqlServerFact]
    public Task Unadmitted_or_incomplete_stock_key_set_cannot_persist_line_costs() => NativeSql.Owned(async database =>
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider)); await SalesPostData.Stock(sql, data, 20, 300);
        using (var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create())
        {
            uow.BeginTransaction(); var repo = uow.Repository<ISalesReceiptWriteRepository>();
            var header = await repo.LockHeaderAsync(saved.SaleId, default); var lines = await repo.LoadLinesAsync(saved.SaleId, default);
            var cost = InventoryValuationMath.CalculateIssue(20, 300, lines.Select(line => new InventoryIssueLine(line.LineNo, line.Qty)).ToArray());
            var costs = new Dictionary<int, InventoryIssueCost> { [data.Product] = cost };
            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ApplyPostedInventoryAsync(header!, lines, costs, DateTime.UtcNow, data.Actor, default));
            await repo.AcquirePostingGateAsync(default);
            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ApplyPostedInventoryAsync(header!, lines, costs, DateTime.UtcNow, data.Actor, default));
            uow.Commit();
        }
        await SalesPostData.Unchanged(sql, saved, 20, 300);
    });
    [SqlServerTheory]
    [InlineData("0", "0", "0", "0")]
    [InlineData("0.0002", "0.0001", "0", "0.0001")]
    public Task Tiny_residual_and_zero_value_allocate_by_line_then_fully_drain(string value, string first, string second, string residual) => NativeSql.Owned(async database =>
    {
        decimal D(string text) => decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); using var provider = data.Provider(database.ConnectionString);
        var sale = await SalesPostData.Create(data, provider, new[] { new SaveSalesReceiptLineRequest(0, data.Product, 1, 1, false), new SaveSalesReceiptLineRequest(0, data.Product, 1, 999, true) });
        await SalesPostData.Stock(sql, data, 3, D(value));
        Assert.False((await SalesPostData.Post(provider, sale)).IsError);
        var detail = SalesDraftData.Success(await SalesDraftData.Get(provider, sale.SaleId));
        Assert.Equal(new[] { D(first), D(second) }, detail.Lines.Select(x => x.CostAmount!.Value));
        Assert.All(detail.Lines, x => Assert.Equal(D(value) == 0 ? 0m : .00006667m, x.PostedUnitCost));
        Assert.Equal(D(residual), await NativeSql.Scalar<decimal>(sql, null, "SELECT InventoryValue FROM dbo.StockBalances"));
        Assert.Equal(2, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
        var last = await SalesPostData.Create(data, provider, new[] { new SaveSalesReceiptLineRequest(0, data.Product, 1, 0, false) });
        Assert.False((await SalesPostData.Post(provider, last)).IsError);
        Assert.Equal(1, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.StockBalances WHERE Quantity=0 AND InventoryValue=0"));
        Assert.Equal(D(value), -await NativeSql.Scalar<decimal>(sql, null, "SELECT SUM(ValueChange) FROM dbo.StockValuationEntries"));
        Assert.Equal(3, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
    });

    [SqlServerTheory]
    [InlineData("SalesReceipts")]
    [InlineData("SalesReceiptLines")]
    [InlineData("StockLedger")]
    [InlineData("StockValuationEntries")]
    [InlineData("StockBalances")]
    public Task Failure_after_each_write_rolls_back_all_tables_version_and_audit(string table) => NativeSql.Owned(async database =>
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); var audit = new SalesPostData.Audit();
        using var provider = SalesPostData.Provider(data, database.ConnectionString, audit);
        var saved = SalesDraftData.Success(await data.Create(provider)); await SalesPostData.Stock(sql, data, 20, 300);
        var operation = table == "StockLedger" || table == "StockValuationEntries" ? "INSERT" : "UPDATE";
        var balanceVersion = await NativeSql.Scalar<byte[]>(sql, null, "SELECT RowVersion FROM dbo.StockBalances");
        await NativeSql.Execute(sql, null, "CREATE TRIGGER dbo.TR_SalesPostFault ON dbo." + table + " AFTER " + operation + " AS THROW 51222,'Expected Sales Post fault',1;");
        Assert.True((await SalesPostData.Post(provider, saved)).IsError);
        await SalesPostData.Unchanged(sql, saved, 20, 300); Assert.Empty(audit.Entries);
        Assert.Equal(balanceVersion, await NativeSql.Scalar<byte[]>(sql, null, "SELECT RowVersion FROM dbo.StockBalances"));
        Assert.Equal(0, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.SalesReceipts WHERE PostedAtUtc IS NOT NULL OR PostedByUserId IS NOT NULL OR UpdatedAtUtc IS NOT NULL"));
        Assert.Equal(0, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.SalesReceiptLines WHERE UpdatedAtUtc IS NOT NULL"));
        await NativeSql.Execute(sql, null, "DROP TRIGGER dbo.TR_SalesPostFault");
        Assert.False((await SalesPostData.Post(provider, saved)).IsError); Assert.Single(audit.Entries);
    });

    [SqlServerFact]
    public Task Before_commit_disposal_rolls_back_complete_post_and_new_version() => NativeSql.Owned(async database =>
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider)); await SalesPostData.Stock(sql, data, 20, 300);
        using (var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create())
        {
            uow.BeginTransaction(); var repo = uow.Repository<ISalesReceiptWriteRepository>();
            await repo.AcquirePostingGateAsync(default); var header = await repo.LockHeaderAsync(saved.SaleId, default);
            var snapshot = await repo.LockPostSnapshotAsync(header!, default);
            var balances = await repo.LockValuationBalancesAsync(data.Warehouse, new[] { data.Product }, default);
            var cost = InventoryValuationMath.CalculateIssue(20, 300, snapshot.Lines.Select(x => new InventoryIssueLine(x.LineNo, x.Qty)).ToArray());
            var posted = await repo.TryMarkPostedAsync(saved.SaleId, Convert.FromBase64String(saved.Version), DateTime.UtcNow, data.Actor, default);
            Assert.NotEqual(saved.Version, Convert.ToBase64String(posted!.Version));
            await repo.ApplyPostedInventoryAsync(posted, snapshot.Lines, new Dictionary<int, InventoryIssueCost> { [data.Product] = cost }, DateTime.UtcNow, data.Actor, default);
            var context = (ISqlExecutionContext)uow;
            Assert.Equal(3, await NativeSql.Scalar<int>((SqlConnection)context.Connection, (SqlTransaction)context.Transaction!, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
        }
        await SalesPostData.Unchanged(sql, saved, 20, 300);
    });

    [SqlServerTheory]
    [InlineData("snapshot")]
    [InlineData("document-cost")]
    [InlineData("group-quantity")]
    [InlineData("document-revenue")]
    [InlineData("line-revenue")]
    public Task Numeric_overflow_rejects_before_status_or_any_inventory_write(string boundary) => NativeSql.Owned(async database =>
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider));
        decimal quantity = 20, value = 300;
        if (boundary == "snapshot") { quantity = .0001m; value = 10000000000000000m; await NativeSql.Execute(sql, null, "UPDATE dbo.SalesReceiptLines SET Qty=0.0001,UnitPrice=0,LineAmount=0; DELETE dbo.SalesReceiptLines WHERE [LineNo]>1"); }
        if (boundary == "group-quantity") await NativeSql.Execute(sql, null, "UPDATE dbo.SalesReceiptLines SET Qty=99999999999999.9999,UnitPrice=0,LineAmount=0");
        if (boundary == "line-revenue") await NativeSql.Execute(sql, null, "UPDATE dbo.SalesReceiptLines SET Qty=99999999999999.9999,UnitPrice=999999999999999.9999 WHERE [LineNo]=1");
        if (boundary == "document-revenue") await NativeSql.Execute(sql, null, "UPDATE dbo.SalesReceiptLines SET Qty=1000000000,UnitPrice=600000000000000,IsGift=0,LineAmount=600000000000000000000000");
        await SalesPostData.Stock(sql, data, quantity, value);
        if (boundary == "document-cost")
        {
            var other = await NativeSql.Scalar<int>(sql, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA')");
            await sql.ExecuteAsync("UPDATE dbo.SalesReceiptLines SET Qty=100000,UnitPrice=0,LineAmount=0; UPDATE dbo.SalesReceiptLines SET ProductId=@other WHERE [LineNo]=2; DELETE dbo.SalesReceiptLines WHERE [LineNo]=3; UPDATE dbo.StockBalances SET Quantity=100000,InventoryValue=600000000000000000000000; INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@Warehouse,@other,100000,600000000000000000000000)", new { other, data.Warehouse });
            quantity = 200000; value = 1200000000000000000000000m;
        }
        var result = await SalesPostData.Post(provider, saved);
        Assert.True(result.IsError); Assert.Equal("SalesReceipt.ValuationOutOfRange", result.FirstError.Code);
        await SalesPostData.Unchanged(sql, saved, quantity, value);
    });

    [SqlServerTheory]
    [InlineData("Customers")]
    [InlineData("Warehouses")]
    [InlineData("Employees")]
    [InlineData("Products")]
    public Task Inactive_references_reject_post_without_mutation(string table) => NativeSql.Owned(async database =>
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider)); await SalesPostData.Stock(sql, data, 20, 300);
        await NativeSql.Execute(sql, null, "UPDATE dbo." + table + " SET IsActive=0");
        var result = await SalesPostData.Post(provider, saved); Assert.True(result.IsError); Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        await SalesPostData.Unchanged(sql, saved, 20, 300);
    });

    [SqlServerFact]
    public Task Posted_cost_is_immutable_after_later_backdated_receipt_and_audit_failure() => NativeSql.Owned(async database =>
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); var audit = new SalesPostData.Audit { Throw = true };
        using var provider = SalesPostData.Provider(data, database.ConnectionString, audit);
        var saved = await SalesPostData.Create(data, provider, data.Lines().Take(2).ToArray()); await SalesPostData.Stock(sql, data, 20, 300);
        var posted = await SalesPostData.Post(provider, saved); Assert.False(posted.IsError); Assert.Single(audit.Entries);
        var receipt = await sql.QuerySingleAsync<long>("INSERT dbo.GoodsReceipts(ReceiptNo,ReceiptDate,WarehouseId,EmployeeId,Status,CreatedByUserId) OUTPUT INSERTED.ReceiptId VALUES(N'GR0000000999','2020-01-01',@Warehouse,@Employee,'Draft',@Actor)", new { data.Warehouse, data.Employee, data.Actor });
        await sql.ExecuteAsync("INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId) VALUES(@receipt,1,@Product,5,30,@Actor)", new { receipt, data.Product, data.Actor });
        var version = await NativeSql.Scalar<byte[]>(sql, null, "SELECT RowVersion FROM dbo.GoodsReceipts");
        using var scope = provider.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PostGoodsReceiptCommand(new PostGoodsReceiptRequest(receipt, Convert.ToBase64String(version))))).IsError);
        Assert.Equal(20m, await NativeSql.Scalar<decimal>(sql, null, "SELECT Quantity FROM dbo.StockBalances"));
        Assert.Equal(375m, await NativeSql.Scalar<decimal>(sql, null, "SELECT InventoryValue FROM dbo.StockBalances"));
        var reloaded = SalesDraftData.Success(await SalesDraftData.Get(provider, saved.SaleId));
        Assert.Equal(75m, reloaded.TotalCost); Assert.All(reloaded.Lines, line => Assert.Equal(15m, line.PostedUnitCost));
        Assert.Equal("SalesReceipt.AlreadyPosted", (await SalesPostData.Post(provider, saved)).FirstError.Code);
        Assert.Equal("SalesReceipt.AlreadyPosted", (await data.Update(provider, saved)).FirstError.Code);
        Assert.Equal(posted.Value.Version, reloaded.Version);
    });
    [SqlServerFact]
    public Task Paid_and_gift_duplicates_freeze_one_average_and_exact_independent_movements() => NativeSql.Owned(async database =>
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); using var provider = data.Provider(database.ConnectionString);
        var saved = await SalesPostData.Create(data, provider, new[] { new SaveSalesReceiptLineRequest(0, data.Product, 3, 25, false), new SaveSalesReceiptLineRequest(0, data.Product, 2, 25, true) });
        await SalesPostData.Stock(sql, data, 20, 300);
        var response = await SalesPostData.Post(provider, saved);
        Assert.False(response.IsError, response.IsError ? response.FirstError.Code : null);
        Assert.NotEqual(saved.Version, response.Value.Version);
        Assert.Equal(Convert.FromBase64String(response.Value.Version), await NativeSql.Scalar<byte[]>(sql, null, "SELECT RowVersion FROM dbo.SalesReceipts"));
        var detail = SalesDraftData.Success(await SalesDraftData.Get(provider, saved.SaleId));
        Assert.Equal(75m, detail.TotalAmount); Assert.Equal(75m, detail.TotalCost); Assert.Equal(0m, detail.GrossProfit);
        Assert.Equal(new[] { 45m, 30m }, detail.Lines.Select(x => x.CostAmount!.Value));
        Assert.All(detail.Lines, x => Assert.Equal(15m, x.PostedUnitCost));
        Assert.Equal(25m, detail.Lines[1].UnitPrice); Assert.Equal(0m, detail.Lines[1].LineAmount);
        Assert.Equal(15m, await NativeSql.Scalar<decimal>(sql, null, "SELECT Quantity FROM dbo.StockBalances"));
        Assert.Equal(225m, await NativeSql.Scalar<decimal>(sql, null, "SELECT InventoryValue FROM dbo.StockBalances"));
        Assert.Equal(2, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId JOIN dbo.SalesReceiptLines s ON s.SaleLineId=l.DocumentLineId AND s.SaleId=l.DocumentId WHERE l.DocumentType='SalesReceipt' AND l.QuantityChange=-s.Qty AND v.ValueChange=-s.CostAmount"));
    });

    [SqlServerTheory]
    [InlineData("unit", "SalesReceipt.UnitChanged")]
    [InlineData("insufficient", "SalesReceipt.InsufficientStock")]
    public Task Invalid_post_rejects_whole_document_without_writes(string boundary, string code) => NativeSql.Owned(async database =>
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        var data = await SalesDraftData.Seed(sql); using var provider = data.Provider(database.ConnectionString);
        var saved = SalesDraftData.Success(await data.Create(provider));
        await SalesPostData.Stock(sql, data, 5, 75);
        if (boundary == "unit") await NativeSql.Execute(sql, null, "UPDATE dbo.Products SET Unit=N'BOX'");
        var result = await SalesPostData.Post(provider, saved);
        Assert.True(result.IsError); Assert.Equal(code, result.FirstError.Code);
        await SalesPostData.Unchanged(sql, saved, 5, 75);
        if (boundary == "unit")
        {
            var reviewed = SalesDraftData.Success(await data.Update(provider, saved, saved.Lines.Take(2).Select(x => new SaveSalesReceiptLineRequest(x.SaleLineId, x.ProductId, x.Qty, x.UnitPrice, x.IsGift)).ToArray()));
            Assert.All(reviewed.Lines, line => Assert.Equal("BOX", line.Unit));
            Assert.False((await SalesPostData.Post(provider, reviewed)).IsError);
        }
    });
}

internal static class SalesPostData
{
    public sealed class Audit : ISecurityAuditSink
    {
        public bool Throw { get; set; }
        public List<SecurityAuditEntry> Entries { get; } = new();
        public Task WriteAsync(SecurityAuditEntry entry, CancellationToken token)
        { Entries.Add(entry); if (Throw) throw new InvalidOperationException("Expected audit failure"); return Task.CompletedTask; }
    }
    public static ServiceProvider Provider(SalesDraftData data, string connection, Audit? audit = null, IDbConnectionFactory? connections = null)
    {
        var services = new ServiceCollection(); services.AddSqlPersistence(connection); services.AddServerApplication();
        services.Replace(ServiceDescriptor.Singleton<ICurrentUserAccessor>(new Actor(data.Actor)));
        services.Replace(ServiceDescriptor.Singleton<IPermissionStore>(new Permissions()));
        if (audit != null) services.Replace(ServiceDescriptor.Singleton<ISecurityAuditSink>(audit));
        if (connections != null) services.Replace(ServiceDescriptor.Singleton(connections));
        return services.BuildServiceProvider();
    }
    private sealed class Actor(int id) : ICurrentUserAccessor { public CurrentUser Current { get; } = CurrentUser.Authenticated(id, "sales", "Sales", true); }
    private sealed class Permissions : IPermissionStore
    { public Task<PermissionSnapshot> GetSnapshotAsync(int id, string key, CancellationToken token) => Task.FromResult(new PermissionSnapshot(true, true)); }
    public static async Task<SalesReceiptDto> Create(SalesDraftData data, ServiceProvider provider, SaveSalesReceiptLineRequest[] lines)
    {
        using var scope = provider.CreateScope();
        return SalesDraftData.Success(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateSalesReceiptDraftCommand(new SaveSalesReceiptRequest(new DateTime(2026, 10, 22), data.Customer, data.Warehouse, data.Employee, null, lines))));
    }
    public static async Task<ErrorOr<PostSalesReceiptResponse>> Post(ServiceProvider provider, SalesReceiptDto saved, CancellationToken token = default)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PostSalesReceiptCommand(new PostSalesReceiptRequest(saved.SaleId, saved.Version)), token);
    }
    public static Task Stock(SqlConnection sql, SalesDraftData data, decimal quantity, decimal value) => sql.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@Warehouse,@Product,@quantity,@value)", new { data.Warehouse, data.Product, quantity, value });
    public static async Task Unchanged(SqlConnection sql, SalesReceiptDto saved, decimal quantity, decimal value)
    {
        Assert.Equal("Draft", await NativeSql.Scalar<string>(sql, null, "SELECT Status FROM dbo.SalesReceipts WHERE SaleId=" + saved.SaleId));
        Assert.Equal(Convert.FromBase64String(saved.Version), await NativeSql.Scalar<byte[]>(sql, null, "SELECT RowVersion FROM dbo.SalesReceipts WHERE SaleId=" + saved.SaleId));
        Assert.Equal(0, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.SalesReceiptLines WHERE CostAmount IS NOT NULL OR PostedUnitCost IS NOT NULL"));
        Assert.Equal(0, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.StockLedger"));
        Assert.Equal(0, await NativeSql.Scalar<int>(sql, null, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
        Assert.Equal(quantity, await NativeSql.Scalar<decimal>(sql, null, "SELECT SUM(Quantity) FROM dbo.StockBalances"));
        Assert.Equal(value, await NativeSql.Scalar<decimal>(sql, null, "SELECT SUM(InventoryValue) FROM dbo.StockBalances"));
    }
}
