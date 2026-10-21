using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Linq;
using MediatR;
using ErrorOr;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class InventoryValuationWriterTests
{
    [SqlServerFact]
    public Task Receipt_movement_always_has_its_exact_value_companion() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString);
        await oracle.OpenAsync();
        var seed = await ValuationTestData.SeedAsync(oracle);
        using var provider = ValuationTestData.Provider(database.ConnectionString);
        using (var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create())
        {
            uow.BeginTransaction();
            var repository = uow.Repository<IGoodsReceiptWriteRepository>();
            await repository.AcquirePostingGateAsync(CancellationToken.None);
            await repository.LockValuationBalancesAsync(seed.Header.WarehouseId, new[] { seed.Lines[0].ProductId }, CancellationToken.None);
            await repository.ApplyPostedInventoryAsync(seed.Header, seed.Lines,
                ValuationTestData.Stamp, seed.Actor, CancellationToken.None);
            uow.Commit();
        }
        Assert.Equal(1, await NativeSql.Scalar<int>(oracle, null,
            "SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId WHERE v.ValueChange=0.0001"));
    });
    [SqlServerTheory]
    [InlineData("0.0001", "0", "0")]
    [InlineData("0.0001", "0.5", "0.0001")]
    [InlineData("99999999999999.9999", "1", "99999999999999.9999")]
    [InlineData("999999999", "999999999999999.9999", "999999998999999999900000.0001")]
    public Task Receipt_post_preserves_zero_rounding_and_large_exact_values(string qty, string cost, string expected) => NativeSql.Owned(async database =>
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var seed = await ValuationTestData.SeedAsync(oracle, decimal.Parse(qty, culture), decimal.Parse(cost, culture));
        using var provider = ValuationTestData.Provider(database.ConnectionString, seed.Actor);
        var result = await ValuationTestData.PostAsync(provider, seed.Header);
        Assert.False(result.IsError, result.IsError ? result.FirstError.Code : null);
        var amount = decimal.Parse(expected, culture);
        Assert.Equal(amount, await NativeSql.Scalar<decimal>(oracle, null, "SELECT ValueChange FROM dbo.StockValuationEntries"));
        Assert.Equal(amount, await NativeSql.Scalar<decimal>(oracle, null, "SELECT InventoryValue FROM dbo.StockBalances"));
        Assert.Equal(decimal.Parse(qty, culture), await NativeSql.Scalar<decimal>(oracle, null, "SELECT Quantity FROM dbo.StockBalances"));
        Assert.Empty(await provider.GetRequiredService<IStockReconciliationQueryService>().FindDifferencesAsync(CancellationToken.None));
    });

    [SqlServerTheory]
    [InlineData("line")]
    [InlineData("quantity")]
    [InlineData("value")]
    [InlineData("document")]
    public Task Overflow_rejects_before_status_or_inventory_mutation(string boundary) => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var seed = await ValuationTestData.SeedAsync(oracle,
            boundary == "line" ? 99999999999999.9999m : boundary == "document" ? 1000000000m : 1m,
            boundary == "line" ? 999999999999999.9999m : boundary == "document" ? 600000000000000m : 1m);
        if (boundary == "document")
        {
            var other = await NativeSql.Scalar<int>(oracle, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA')");
            await oracle.ExecuteAsync("INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId) VALUES(@Id,2,@other,1000000000,600000000000000,@Actor)", new { seed.Header.Id, other, seed.Actor });
        }
        if (boundary == "quantity" || boundary == "value")
            await oracle.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@WarehouseId,@ProductId,@Q,@V)",
                new { seed.Header.WarehouseId, seed.Lines[0].ProductId, Q = boundary == "quantity" ? 99999999999999.9999m : 1m,
                    V = boundary == "value" ? 999999999999999999999999.9999m : 1m });
        using var provider = ValuationTestData.Provider(database.ConnectionString, seed.Actor);
        var result = await ValuationTestData.PostAsync(provider, seed.Header);
        Assert.True(result.IsError); Assert.Equal("GoodsReceipt.ValuationOutOfRange", result.FirstError.Code);
        Assert.Equal("Draft", await NativeSql.Scalar<string>(oracle, null, "SELECT Status FROM dbo.GoodsReceipts"));
        Assert.Equal(seed.Header.Version, await NativeSql.Scalar<byte[]>(oracle, null, "SELECT RowVersion FROM dbo.GoodsReceipts"));
        Assert.Equal(0, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.StockLedger"));
        Assert.Equal(0, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo.StockValuationEntries"));
    });

    [SqlServerTheory]
    [InlineData("GoodsReceipts", "UPDATE")]
    [InlineData("StockLedger", "INSERT")]
    [InlineData("StockValuationEntries", "INSERT")]
    [InlineData("StockBalances", "INSERT")]
    [InlineData("StockBalances", "UPDATE")]
    public Task Failure_at_each_write_boundary_rolls_back_status_version_quantity_and_value(string table, string operation) => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var seed = await ValuationTestData.SeedAsync(oracle, 2, 3);
        var existingBalance = table == "StockBalances" && operation == "UPDATE";
        if (existingBalance)
            await oracle.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@WarehouseId,@ProductId,1,4)", new { seed.Header.WarehouseId, seed.Lines[0].ProductId });
        await NativeSql.Execute(oracle, null, "CREATE TRIGGER dbo.TR_ValuationFault ON dbo." + table + " AFTER " + operation + " AS THROW 51220, 'Expected valuation fault', 1;");
        using var provider = ValuationTestData.Provider(database.ConnectionString, seed.Actor);
        var result = await ValuationTestData.PostAsync(provider, seed.Header);
        Assert.True(result.IsError);
        Assert.Equal("Draft", await NativeSql.Scalar<string>(oracle, null, "SELECT Status FROM dbo.GoodsReceipts"));
        Assert.Equal(seed.Header.Version, await NativeSql.Scalar<byte[]>(oracle, null, "SELECT RowVersion FROM dbo.GoodsReceipts"));
        foreach (var name in new[] { "StockLedger", "StockValuationEntries", "StockBalances" })
            Assert.Equal(existingBalance && name == "StockBalances" ? 1 : 0, await NativeSql.Scalar<int>(oracle, null, "SELECT COUNT(*) FROM dbo." + name));
        if (existingBalance)
        {
            Assert.Equal(1m, await NativeSql.Scalar<decimal>(oracle, null, "SELECT Quantity FROM dbo.StockBalances"));
            Assert.Equal(4m, await NativeSql.Scalar<decimal>(oracle, null, "SELECT InventoryValue FROM dbo.StockBalances"));
        }
        await NativeSql.Execute(oracle, null, "DROP TRIGGER dbo.TR_ValuationFault;");
        Assert.False((await ValuationTestData.PostAsync(provider, seed.Header)).IsError);
    });

    [SqlServerFact]
    public Task Receipt_line_rounding_is_individual_and_existing_value_can_reach_maximum_exactly() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var seed = await ValuationTestData.SeedAsync(oracle);
        var other = await NativeSql.Scalar<int>(oracle,null,"INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA')");
        await oracle.ExecuteAsync("INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId) VALUES(@Id,2,@other,0.0001,0.5,@Actor); INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@WarehouseId,@ProductId,100000,999999999999999999999999.9998)",new { seed.Header.Id,other,seed.Actor,seed.Header.WarehouseId,seed.Lines[0].ProductId });
        using var provider=ValuationTestData.Provider(database.ConnectionString,seed.Actor);
        Assert.False((await ValuationTestData.PostAsync(provider,seed.Header)).IsError);
        Assert.Equal(.0002m,await NativeSql.Scalar<decimal>(oracle,null,"SELECT SUM(ValueChange) FROM dbo.StockValuationEntries"));
        Assert.Equal(999999999999999999999999.9999m,await NativeSql.Scalar<decimal>(oracle,null,"SELECT MAX(InventoryValue) FROM dbo.StockBalances"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Restricted_runtime_uses_shared_gate_and_valued_receipt_on_the_owned_transaction() => NativeSql.Owned(async database =>
    {
        using var oracle=new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var seed=await ValuationTestData.SeedAsync(oracle,2,3);
        await NativeSql.Execute(oracle,null,"CREATE USER [ValuationRuntime] WITHOUT LOGIN; CREATE USER [ValuationMigration] WITHOUT LOGIN;");
        var directory=new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while(directory != null && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName,"deployment","sql","Provision-MyDmsVnPrincipals.sql"))) directory=directory.Parent;
        Assert.NotNull(directory);
        var script=System.IO.File.ReadAllText(System.IO.Path.Combine(directory!.FullName,"deployment","sql","Provision-MyDmsVnPrincipals.sql"))
            .Replace("$(RuntimeUser)","ValuationRuntime").Replace("$(MigrationUser)","ValuationMigration");
        await NativeSql.Execute(oracle,null,script);
        using var provider=ValuationTestData.Provider(database.ConnectionString);
        using var uow=provider.GetRequiredService<IUnitOfWorkFactory>().Create();
        var context=(ISqlExecutionContext)uow; var sql=(SqlConnection)context.Connection;
        await NativeSql.Execute(sql,null,"EXECUTE AS USER=N'ValuationRuntime'");
        try
        {
            Assert.Equal(0,await NativeSql.Scalar<int>(sql,null,"SELECT COALESCE(HAS_PERMS_BY_NAME(N'dbo',N'SCHEMA',N'ALTER'),0)"));
            uow.BeginTransaction(); var repo=uow.Repository<IGoodsReceiptWriteRepository>();
            await repo.AcquirePostingGateAsync(CancellationToken.None);
            var header=await repo.LockHeaderAsync(seed.Header.Id,CancellationToken.None);
            var snapshot=await repo.LockPostSnapshotAsync(header!,CancellationToken.None);
            await repo.LockValuationBalancesAsync(header!.WarehouseId,snapshot.Lines.Select(line=>line.ProductId).ToArray(),CancellationToken.None);
            Assert.True(await repo.TryMarkPostedAsync(header.Id,header.Version,ValuationTestData.Stamp,seed.Actor,CancellationToken.None));
            await repo.ApplyPostedInventoryAsync(header,snapshot.Lines,ValuationTestData.Stamp,seed.Actor,CancellationToken.None);
            uow.Commit();
        }
        finally
        {
            if(uow.State==UnitOfWorkState.ActiveTransaction) uow.Rollback();
            await NativeSql.Execute(sql,null,"REVERT");
        }
        Assert.Equal(6m,await NativeSql.Scalar<decimal>(oracle,null,"SELECT InventoryValue FROM dbo.StockBalances"));
        Assert.Equal(6m,await NativeSql.Scalar<decimal>(oracle,null,"SELECT ValueChange FROM dbo.StockValuationEntries"));
    });

    [SqlServerFact]
    public Task Unadmitted_or_unlocked_writer_is_rejected_before_any_movement() => NativeSql.Owned(async database =>
    {
        using var oracle=new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var seed=await ValuationTestData.SeedAsync(oracle,1,2);
        var other=await NativeSql.Scalar<int>(oracle,null,"INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P2',N'Two',N'EA')");
        using var provider=ValuationTestData.Provider(database.ConnectionString);
        using var uow=provider.GetRequiredService<IUnitOfWorkFactory>().Create();
        var repo=uow.Repository<IGoodsReceiptWriteRepository>();
        uow.BeginTransaction();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.ApplyPostedInventoryAsync(seed.Header,seed.Lines,ValuationTestData.Stamp,seed.Actor,CancellationToken.None));
        await repo.AcquirePostingGateAsync(CancellationToken.None);
        await repo.LockValuationBalancesAsync(seed.Header.WarehouseId,new[] { seed.Lines[0].ProductId },CancellationToken.None);
        var extra=new GoodsReceiptLine { Id=999,ProductId=other,LineNumber=2,Quantity=1,UnitCost=2 };
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.ApplyPostedInventoryAsync(seed.Header,seed.Lines.Concat(new[] { extra }).ToArray(),ValuationTestData.Stamp,seed.Actor,CancellationToken.None));
        var context=(ISqlExecutionContext)uow;
        Assert.Equal(0,await NativeSql.Scalar<int>((SqlConnection)context.Connection,(SqlTransaction)context.Transaction!,"SELECT COUNT(*) FROM dbo.StockLedger"));
        uow.Commit();
        await Assert.ThrowsAnyAsync<InvalidOperationException>(()=>repo.AcquirePostingGateAsync(CancellationToken.None));
        Assert.Equal(0,await NativeSql.Scalar<int>(oracle,null,"SELECT COUNT(*) FROM dbo.StockValuationEntries"));
    });

    [SqlServerFact]
    public Task Disposing_before_commit_rolls_back_every_valued_write() => NativeSql.Owned(async database =>
    {
        using var oracle=new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var seed=await ValuationTestData.SeedAsync(oracle,1,2);
        using var provider=ValuationTestData.Provider(database.ConnectionString);
        using(var uow=provider.GetRequiredService<IUnitOfWorkFactory>().Create())
        {
            uow.BeginTransaction(); var repo=uow.Repository<IGoodsReceiptWriteRepository>();
            await repo.AcquirePostingGateAsync(CancellationToken.None);
            await repo.LockValuationBalancesAsync(seed.Header.WarehouseId,new[] { seed.Lines[0].ProductId },CancellationToken.None);
            Assert.True(await repo.TryMarkPostedAsync(seed.Header.Id,seed.Header.Version,ValuationTestData.Stamp,seed.Actor,CancellationToken.None));
            await repo.ApplyPostedInventoryAsync(seed.Header,seed.Lines,ValuationTestData.Stamp,seed.Actor,CancellationToken.None);
            var context=(ISqlExecutionContext)uow;
            Assert.Equal(2m,await NativeSql.Scalar<decimal>((SqlConnection)context.Connection,(SqlTransaction)context.Transaction!,"SELECT InventoryValue FROM dbo.StockBalances"));
        }
        Assert.Equal("Draft",await NativeSql.Scalar<string>(oracle,null,"SELECT Status FROM dbo.GoodsReceipts"));
        Assert.Equal(seed.Header.Version,await NativeSql.Scalar<byte[]>(oracle,null,"SELECT RowVersion FROM dbo.GoodsReceipts"));
        foreach(var table in new[] { "StockLedger","StockValuationEntries","StockBalances" })
            Assert.Equal(0,await NativeSql.Scalar<int>(oracle,null,"SELECT COUNT(*) FROM dbo."+table));
    });

}

internal sealed class ValuationTestData
{
    public static readonly DateTime Stamp = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);
    public int Actor { get; private set; }
    public GoodsReceipt Header { get; private set; } = null!;
    public GoodsReceiptLine[] Lines { get; private set; } = null!;
    public static ServiceProvider Provider(string connectionString, int? actor = null, IDbConnectionFactory? connections = null)
    {
        var services = new ServiceCollection();
        services.AddSqlPersistence(connectionString);
        if (connections != null) services.Replace(ServiceDescriptor.Singleton(connections));
        if (actor.HasValue)
        {
            services.AddServerApplication();
            services.Replace(ServiceDescriptor.Scoped<ICurrentUserAccessor>(_ => new ActorAccessor(actor.Value)));
            services.Replace(ServiceDescriptor.Scoped<IPermissionStore>(_ => new Permissions()));
        }
        return services.BuildServiceProvider();
    }
    public static async Task<ErrorOr<PostGoodsReceiptResponse>> PostAsync(ServiceProvider provider, GoodsReceipt header, CancellationToken token = default)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new PostGoodsReceiptCommand(new PostGoodsReceiptRequest(header.Id, Convert.ToBase64String(header.Version))), token);
    }
    private sealed class ActorAccessor : ICurrentUserAccessor
    {
        public ActorAccessor(int actor) => Current = CurrentUser.Authenticated(actor, "value", "Value", true);
        public CurrentUser Current { get; }
    }
    private sealed class Permissions : IPermissionStore
    {
        public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string permissionKey, CancellationToken token) =>
            Task.FromResult(new PermissionSnapshot(true, hasRoleGrant: true));
    }

    public static async Task<ValuationTestData> SeedAsync(SqlConnection connection, decimal quantity = 0.0001m, decimal unitCost = 0.5m)
    {
        var actor = await NativeSql.Scalar<int>(connection, null, "INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'value',N'VALUE',N'Value',N'Local',N'hash',N'',N'BCrypt');");
        var warehouse = await NativeSql.Scalar<int>(connection, null, "INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'WH',N'Warehouse');");
        var employee = await NativeSql.Scalar<int>(connection, null, "INSERT dbo.Employees(Code,Name) OUTPUT INSERTED.EmployeeId VALUES(N'EMP',N'Employee');");
        var product = await NativeSql.Scalar<int>(connection, null, "INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P1',N'Product',N'EA');");
        using var provider = Provider(connection.ConnectionString);
        using var uow = provider.GetRequiredService<IUnitOfWorkFactory>().Create();
        uow.BeginTransaction();
        var lines = new[] { new GoodsReceiptLine { LineNumber = 1, ProductId = product, Quantity = quantity, UnitCost = unitCost, CreatedByUserId = actor } };
        var header = await uow.Repository<IGoodsReceiptWriteRepository>().InsertDraftAsync(new GoodsReceipt
        { ReceiptDate = Stamp.Date, WarehouseId = warehouse, EmployeeId = employee, CreatedByUserId = actor }, lines, CancellationToken.None);
        uow.Commit();
        return new ValuationTestData { Actor = actor, Header = header, Lines = lines };
    }
}
