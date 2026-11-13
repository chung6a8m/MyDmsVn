using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CutoverSnapshotTests
{
    private static readonly (string Key, string Select)[] Sources =
    {
        ("sales", "SELECT * FROM dbo.SalesReceipts ORDER BY SaleId"),
        ("saleLines", "SELECT * FROM dbo.SalesReceiptLines ORDER BY SaleLineId"),
        ("receipts", "SELECT * FROM dbo.GoodsReceipts ORDER BY ReceiptId"),
        ("receiptLines", "SELECT * FROM dbo.GoodsReceiptLines ORDER BY ReceiptLineId"),
        ("balances", "SELECT * FROM dbo.StockBalances ORDER BY WarehouseId,ProductId"),
        ("ledger", "SELECT * FROM dbo.StockLedger ORDER BY LedgerId"),
        ("valuation", "SELECT * FROM dbo.StockValuationEntries ORDER BY LedgerId"),
        ("journal", "SELECT * FROM dbo.WebOperationJournal ORDER BY ActorUserId,OperationId"),
        ("sequences", "SELECT name,CONVERT(decimal(38,0),current_value) AS value,CONVERT(decimal(38,0),last_used_value) AS lastUsed,CONVERT(decimal(38,0),start_value) AS start FROM sys.sequences WHERE object_id IN (OBJECT_ID(N'dbo.SalesReceiptNumberSequence'),OBJECT_ID(N'dbo.GoodsReceiptNumberSequence')) ORDER BY name"),
        ("terminal", "SELECT * FROM dbo.WebOperationTerminalAudit ORDER BY ActorUserId,OperationId"),
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Lifecycle_snapshot_uses_fixed_ordered_array_scalar_queries_on_one_connection(bool current)
    {
        using var sql = new SqlConnection();
        var index = 0;
        var snapshot = await WebHostIisExpressTests.ReadCutoverLifecycleSnapshot(sql, current, command =>
        {
            Assert.Same(sql, command.Connection);
            Assert.Equal(CommandType.Text, command.CommandType);
            Assert.Equal(30, command.CommandTimeout);
            Assert.Null(command.Transaction);
            Assert.Empty(command.Parameters);
            var select = Sources[index++].Select;
            var expected = "SELECT ISNULL((" + select + " FOR JSON PATH,INCLUDE_NULL_VALUES), CONVERT(nvarchar(max), N'[]'));";
            // Emulate a SQL NULL from the inner JSON projection and SQL-side empty-array normalization.
            return Task.FromResult<object?>(command.CommandText == expected ? "[]" : null);
        });
        Assert.Equal(current ? 10 : 9, index);
        Assert.Equal(Sources.Select(source => source.Key), snapshot.Properties().Select(property => property.Name));
        Assert.All(snapshot.Properties(), property => Assert.Empty(Assert.IsType<JArray>(property.Value)));
    }

    [Fact]
    public async Task Lifecycle_snapshot_preserves_large_populated_array_and_escaped_values()
    {
        using var sql = new SqlConnection();
        var value = new string('x', 6200) + " dấu \"quoted\" \\ \n";
        var payload = new JArray(new JObject { ["SaleId"] = 1, ["Value"] = value, ["Optional"] = JValue.CreateNull() },
            new JObject { ["SaleId"] = 2 }).ToString(Newtonsoft.Json.Formatting.None);
        var calls = 0;
        var snapshot = await WebHostIisExpressTests.ReadCutoverLifecycleSnapshot(sql, true,
            _ => Task.FromResult<object?>(calls++ == 0 ? payload : "[]"));
        var sales = Assert.IsType<JArray>(snapshot["sales"]);
        Assert.Equal(new[] { 1, 2 }, sales.Select(row => (int)row["SaleId"]!));
        Assert.Equal(value, (string?)sales[0]["Value"]);
        Assert.Equal(JTokenType.Null, sales[0]["Optional"]!.Type);
        Assert.Equal(10, calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData("")]
    [InlineData("[{\"private\":")]
    [InlineData("{\"private\":1}")]
    [InlineData("[1]")]
    [InlineData("[{\"private\":1,}]")]
    [InlineData("[/*private*/{}]")]
    [InlineData("[{\"private\":1,\"private\":2}]")]
    [InlineData("[] []")]
    public async Task Lifecycle_snapshot_refuses_invalid_scalar_with_only_fixed_source_in_error(object? value) =>
        await RequireRefusal(value);

    [Fact]
    public async Task Lifecycle_snapshot_refuses_dbnull() => await RequireRefusal(DBNull.Value);

    private static async Task RequireRefusal(object? value)
    {
        using var sql = new SqlConnection();
        var commands = new List<string>();
        var failure = await Assert.ThrowsAsync<IOException>(() => WebHostIisExpressTests.ReadCutoverLifecycleSnapshot(sql, true, command =>
        {
            commands.Add(command.CommandText);
            return Task.FromResult(commands.Count == 2 ? value : "[]");
        }));
        Assert.Equal(2, commands.Count);
        Assert.Equal("Cutover snapshot SalesReceiptLines returned an invalid JSON row array.", failure.Message);
        Assert.Null(failure.InnerException);
    }
}
