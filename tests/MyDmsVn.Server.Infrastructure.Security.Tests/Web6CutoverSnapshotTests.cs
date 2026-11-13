using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CutoverSnapshotTests
{
    [Fact]
    public async Task Sql_open_passes_a_cancellable_token_and_checks_the_deadline_before_next_command()
    {
        var now = TimeSpan.Zero;
        using var sql = new SqlConnection();
        var deadline = new Web6CutoverDeadline(TimeSpan.FromSeconds(1), () => now);
        var opened = false;
        await Assert.ThrowsAsync<TimeoutException>(() => deadline.Open(sql, default, token =>
        {
            Assert.True(token.CanBeCanceled);
            opened = true;
            now = TimeSpan.FromSeconds(2);
            return Task.CompletedTask;
        }));
        Assert.True(opened);
        Assert.Throws<TimeoutException>(() => deadline.Command("SELECT 1", default));
    }

    [Fact]
    public async Task Strict_snapshot_resamples_positive_timeout_and_stops_after_a_late_scalar()
    {
        using var sql = new SqlConnection();
        using var source = new CancellationTokenSource();
        var now = TimeSpan.Zero;
        var calls = 0;
        await Assert.ThrowsAsync<TimeoutException>(() => WebHostIisExpressTests.ReadCutoverLifecycleSnapshot(sql, true,
            new Web6CutoverDeadline(TimeSpan.FromMilliseconds(200), () => now), source.Token, command =>
            {
                calls++;
                Assert.Equal(1, command.CommandTimeout);
                now = TimeSpan.FromSeconds(1);
                return Task.FromResult<object?>("[]");
            }));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(31)]
    public async Task Strict_schema_and_ownership_helpers_refuse_nonpositive_or_excessive_timeout(int timeout)
    {
        using var sql = new SqlConnection();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => MyDmsVn.Server.Infrastructure.Operations.SqlSchemaDefinitionFingerprint.ReadAsync(sql, timeout, default));
        var allocation = (SqlTestDatabase)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SqlTestDatabase));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => allocation.VerifyOwnershipAsync(sql, timeout, default));
    }

    [Fact]
    public void Live_cutover_control_path_has_no_uncancelled_or_default_command_edges()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory!.FullName, "tests", "MyDmsVn.Server.Infrastructure.Security.Tests", "WebHostIisExpressTests.Cutover.cs"));
        var start = source.IndexOf("public async Task Switch(", StringComparison.Ordinal);
        var end = source.IndexOf("internal async Task<bool> Cleanup", start, StringComparison.Ordinal);
        var control = source.Substring(start, end - start);
        Assert.DoesNotContain("CancellationToken.None", control);
        Assert.DoesNotContain(".OpenAsync()", control);
        Assert.DoesNotContain("Task.Delay(250)", control);
        Assert.DoesNotContain("stable admission verification is unavailable", control);
        Assert.DoesNotContain("QuerySingleAsync<Guid>(\"", control);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(45)]
    [InlineData(150)]
    public void Phase_cap_never_renews_the_action_remainder(int cap)
    {
        var now = TimeSpan.Zero;
        var action = new Web6CutoverDeadline(TimeSpan.FromSeconds(200), () => now);
        var phase = action.Clamp(TimeSpan.FromSeconds(cap));
        Assert.Equal(TimeSpan.FromSeconds(cap), phase.Remaining);
        now = TimeSpan.FromSeconds(199.75);
        Assert.Equal(TimeSpan.FromMilliseconds(250), action.Clamp(TimeSpan.FromSeconds(cap)).Remaining);
    }

    [Theory]
    [InlineData(0.001, 1)]
    [InlineData(0.9, 1)]
    [InlineData(1.9, 1)]
    [InlineData(15.9, 15)]
    [InlineData(45, 30)]
    public void Sql_timeout_is_positive_capped_and_carries_the_exact_token(double seconds, int expected)
    {
        using var cancellation = new CancellationTokenSource();
        var deadline = new Web6CutoverDeadline(TimeSpan.FromSeconds(seconds), () => TimeSpan.Zero);
        var command = deadline.Command("SELECT 1", cancellation.Token);
        Assert.Equal(expected, command.Timeout);
        Assert.Equal(cancellation.Token, command.Token);
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => deadline.Command("SELECT 2", cancellation.Token));
    }

    [Theory]
    [InlineData("WMI")]
    [InlineData("file")]
    [InlineData("commit")]
    public void Synchronous_return_after_expiry_cannot_start_following_work(string edge)
    {
        var now = TimeSpan.Zero;
        var deadline = new Web6CutoverDeadline(TimeSpan.FromSeconds(1), () => now);
        var entered = false;
        var following = false;
        Assert.Throws<TimeoutException>(() =>
        {
            deadline.Observe(() => { entered = true; now = TimeSpan.FromSeconds(2); return edge; }, default);
            following = true;
        });
        Assert.True(entered);
        Assert.False(following);
    }

    [Fact]
    public async Task Poll_tail_delay_uses_only_remaining_phase_and_refuses_success_at_expiry()
    {
        var now = TimeSpan.Zero;
        var deadline = new Web6CutoverDeadline(TimeSpan.FromMilliseconds(20), () => now);
        await Assert.ThrowsAsync<TimeoutException>(() => deadline.Delay(TimeSpan.FromMilliseconds(250), default, (delay, token) =>
        {
            Assert.Equal(TimeSpan.FromMilliseconds(20), delay);
            now += delay;
            return Task.CompletedTask;
        }));
    }

    [Fact]
    public void Strict_deadline_call_graph_excludes_unowned_waits_and_passes_deadline_and_token()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        string Source(string name) => File.ReadAllText(Path.Combine(directory!.FullName, "tests", "MyDmsVn.Server.Infrastructure.Security.Tests", name));
        var live = Source("WebHostIisExpressTests.Cutover.cs");
        Assert.Contains("deadline.Clamp(Web6CutoverBudgets.HostSwitchPhase)", live);
        Assert.Contains("deadline.Clamp(Web6CutoverBudgets.LifecyclePhase)", live);
        Assert.Contains("deadline.Clamp(Web6CutoverBudgets.SqlMutationOrOraclePhase)", live);
        Assert.Contains("VerifyOwnershipAsync(sql, deadline.SqlTimeout(token), token)", live);
        Assert.Contains("ReadAsync(sql, deadline.SqlTimeout(token), token)", live);
        Assert.Contains("mutationDeadline.Observe(transaction.Commit, mutationToken)", live);
        Assert.Contains("VerifyDatabase(_current, _currentControl, Web6CutoverPair.Current, mutationDeadline, mutationToken)", live);
        Assert.Contains("ReadCutoverLifecycleSnapshot(sql, current, deadline, token)", live);
        Assert.Contains("_cutover!(request, workDeadline, token)", Source("WebRecoveryPilotControl.cs"));
        var native = Source("Web6CutoverNative.cs");
        Assert.DoesNotContain("Task.Run", native);
        Assert.DoesNotContain("WhenAny", native);
        Assert.Contains("stream.ReadAsync(bytes, 0, bytes.Length, token)", native);
        Assert.Contains("await Task.WhenAll(output, error)", native);
        var service = Source("Web5HttpServiceState.cs");
        var strict = service.Substring(service.IndexOf("#if !WEB6_BOOTSTRAP", StringComparison.Ordinal));
        strict = strict.Substring(0, strict.IndexOf("#endif", StringComparison.Ordinal));
        Assert.DoesNotContain("CancellationToken.None", strict);
        Assert.DoesNotContain("Task.Run", strict);
        Assert.DoesNotContain("WhenAny", strict);
        Assert.Contains("TerminateVerified", strict);
        Assert.Contains("JoinOutput(output, error, deadline, token)", strict);
        var admission = Source("Web5IisExpressHostAdmission.cs");
        Assert.Contains("Timeout = deadline.Remaining", admission);
        Assert.Contains("ObserveOptional(identity.Process.ProcessId, deadline, token)", admission);
        var oracle = Source("Web6CandidateOracle.cs");
        var begin = oracle.IndexOf("private static async Task<long> VerifyCommittedSetupCore", StringComparison.Ordinal);
        var end = oracle.IndexOf("private static void Exact", begin, StringComparison.Ordinal);
        var oracleCore = oracle.Substring(begin, end - begin);
        Assert.DoesNotContain("CancellationToken.None", oracleCore);
        Assert.DoesNotContain("QueryAsync<JournalRow>(@", oracleCore);
        Assert.Contains("commandTimeout: commandTimeout?.Invoke(), cancellationToken: cancellationToken", oracleCore);
    }

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
