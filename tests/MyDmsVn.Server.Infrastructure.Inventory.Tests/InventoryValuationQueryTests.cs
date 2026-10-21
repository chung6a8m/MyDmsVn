using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class InventoryValuationQueryTests
{
    [SqlServerFact]
    public Task Balance_projection_returns_authoritative_value_and_round8_average() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString);
        await oracle.OpenAsync();
        var seed = await ValuationTestData.SeedAsync(oracle);
        await oracle.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@WarehouseId,@ProductId,3,0.0002);",
            new { seed.Header.WarehouseId, seed.Lines[0].ProductId });
        using var provider = ValuationTestData.Provider(database.ConnectionString);
        var page = await provider.GetRequiredService<IInventoryQueryService>().GetStockBalancesAsync(
            new StockBalanceQuery(null, null, null, 1, 20), CancellationToken.None);
        var json = JObject.FromObject(Assert.Single(page.Items));
        Assert.Equal(0.0002m, json.Value<decimal>("InventoryValue"));
        Assert.Equal(0.00006667m, json.Value<decimal>("AverageUnitCost"));
    });

    [SqlServerFact]
    public Task Stock_card_missing_companion_fails_instead_of_hiding_the_movement() => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString);
        await oracle.OpenAsync();
        var seed = await ValuationTestData.SeedAsync(oracle);
        await oracle.ExecuteAsync("INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) VALUES(@WarehouseId,@ProductId,'GoodsReceipt',@Id,@LineId,0.0001,@Stamp,@Actor);",
            new { seed.Header.WarehouseId, seed.Lines[0].ProductId, seed.Header.Id, LineId = seed.Lines[0].Id, Stamp = ValuationTestData.Stamp, seed.Actor });
        using var provider = ValuationTestData.Provider(database.ConnectionString);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRequiredService<IInventoryQueryService>()
            .GetStockCardAsync(new StockCardQuery(seed.Header.WarehouseId, seed.Lines[0].ProductId, null, null, 1, 20), CancellationToken.None));
    });
    [SqlServerTheory]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("zero-value-mismatch")]
    [InlineData("average-overflow")]
    public Task Balance_projection_null_and_invalid_boundaries_are_explicit(string state) => NativeSql.Owned(async database =>
    {
        using var oracle = new SqlConnection(database.ConnectionString); await oracle.OpenAsync();
        var seed = await ValuationTestData.SeedAsync(oracle);
        await oracle.ExecuteAsync("ALTER TABLE dbo.StockBalances NOCHECK CONSTRAINT ALL; INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@WarehouseId,@ProductId,@Q,@V)",
            new { seed.Header.WarehouseId, seed.Lines[0].ProductId,
                Q = state == "negative" ? -1m : state == "average-overflow" ? .0001m : 0m,
                V = state == "zero-value-mismatch" ? 1m : state == "average-overflow" ? 999999999999999999999999.9999m : 0m });
        using var provider = ValuationTestData.Provider(database.ConnectionString);
        var query = provider.GetRequiredService<IInventoryQueryService>();
        if (state == "zero")
        {
            var row = Assert.Single((await query.GetStockBalancesAsync(new StockBalanceQuery(null,null,null,1,20),CancellationToken.None)).Items);
            Assert.Null(row.AverageUnitCost); Assert.Equal(0m,row.InventoryValue);
        }
        else
        {
            var error = await Record.ExceptionAsync(() => query.GetStockBalancesAsync(new StockBalanceQuery(null,null,null,1,20),CancellationToken.None));
            Assert.True(error is ArgumentException || error is OverflowException);
        }
    });

}
