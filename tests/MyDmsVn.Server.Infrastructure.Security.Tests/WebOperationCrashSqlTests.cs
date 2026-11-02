using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Data;
using System.Threading;
using MyDmsVn.Desktop.Tests;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using HttpScenario = MyDmsVn.Server.Infrastructure.IntegrationTests.WebAuthHttpSqlTests.Scenario;
using Reply = MyDmsVn.Server.Infrastructure.IntegrationTests.WebAuthHttpSqlTests.Reply;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed partial class WebOperationHttpSqlTests
{
    private readonly ITestOutputHelper _crashOutput;
    public WebOperationHttpSqlTests(ITestOutputHelper output) => _crashOutput = output;
    private static readonly string[] CrashKinds = { "Sales.Create", "Sales.Update", "Sales.Post", "Receipt.Create", "Receipt.Update", "Receipt.Post" };

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task External_process_death_before_commit_rolls_back_all_six_commands() => CrashMatrix("before-commit");

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task External_process_death_racing_commit_classifies_all_six_from_fresh_SQL() => CrashMatrix("commit-window");

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task External_process_death_after_commit_recovers_all_six_original_results() => CrashMatrix("after-commit");

    private async Task CrashMatrix(string phase)
    {
        foreach (var kind in CrashKinds)
            await OwnedCrash(async scope =>
            {
                var arranged = await ArrangeCrash(scope.Data, kind);
                scope.Operation = arranged.Intent;
                var intent = arranged.Intent;
                var before = await CrashSnapshot(scope.ConnectionString);
                var child = await scope.Start(CrashConfiguration(scope, arranged, phase));
                await child.DispatchAsync();
                if (phase == "commit-window")
                {
                    await child.EventAsync("commit-ready");
                    await child.ReleaseAsync();
                    await child.EventAsync("commit-enter");
                    // This races the invocation. It never claims SQL was killed inside Commit.
                }
                else await child.EventAsync(phase);
                await child.KillAsync();
                await AwaitCrashQuiescence(scope, scope.Data.Http.Admin.ServerProcessId);
                // New non-pooled oracle connection, opened only after owned child drain.
                using var oracle = await CrashOracle(scope.ConnectionString);
                var count = await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@key;",
                    new { actor = scope.Data.Http.Alice, key = intent.Key });
                Assert.InRange(count, 0, 1);
                if (phase == "before-commit") Assert.Equal(0, count);
                if (phase == "after-commit") Assert.Equal(1, count);
                _crashOutput.WriteLine("WEB5 crash: {0}; {1}; SQL={2}; child drained", kind, phase, count == 0 ? "ROLLED_BACK" : "COMMITTED");
                if (count == 0)
                {
                    Assert.Equal(before, await CrashSnapshot(scope.ConnectionString));
                    Error(await CrashRequest(scope, CrashConfiguration(scope, arranged, "none", status: true)), 404, "Operation.NotObserved");
                    Assert.Equal(before, await CrashSnapshot(scope.ConnectionString));
                    // Test-only explicit exact-key replay after physical rollback observation;
                    // status404 itself never establishes a product unlock or retry policy.
                    Success(await CrashRequest(scope, CrashConfiguration(scope, arranged, "none")));
                }
                await VerifyCrashCommitted(oracle, scope.Data, arranged);
                await RecoverAndReplayCrash(scope, arranged);
            });
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Independent_same_key_worker_waits_for_delayed_original_then_replays_all_six() => DelayedCrashMatrix(killOriginal: false);

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Independent_same_key_worker_waits_for_killed_original_then_executes_all_six() => DelayedCrashMatrix(killOriginal: true);

    private async Task DelayedCrashMatrix(bool killOriginal)
    {
        foreach (var kind in CrashKinds)
            await OwnedCrash(async scope =>
            {
                var arranged = await ArrangeCrash(scope.Data, kind);
                scope.Operation = arranged.Intent;
                var original = await scope.Start(CrashConfiguration(scope, arranged, "before-commit"));
                await original.DispatchAsync();
                var barrier = await original.EventAsync("before-commit");
                var originalSession = (int)barrier["sqlSessionId"]!;
                Assert.True(originalSession > 0);
                var contender = await scope.Start(CrashConfiguration(scope, arranged, "none"));
                await contender.DispatchAsync();
                using (var observer = await CrashOracle(scope.ConnectionString))
                    await WaitForCrashFence(observer, originalSession, contender.ProcessId);
                Reply? originalReply = null;
                if (killOriginal) await original.KillAsync();
                else
                {
                    await original.ReleaseAsync();
                    originalReply = await original.ReplyAsync();
                    Success(originalReply);
                    await original.CloseAsync();
                }
                var replay = await contender.ReplyAsync();
                Success(replay);
                await contender.CloseAsync();
                if (originalReply != null) Assert.True(JToken.DeepEquals(originalReply.Json, replay.Json));
                await AwaitCrashQuiescence(scope, scope.Data.Http.Admin.ServerProcessId);
                using var oracle = await CrashOracle(scope.ConnectionString);
                await VerifyCrashCommitted(oracle, scope.Data, arranged);
                await RecoverAndReplayCrash(scope, arranged);
                _crashOutput.WriteLine("WEB5 fence: {0}; original {1}; SQL waiter observed; one committed effect; children drained", kind, killOriginal ? "killed" : "released");
            });
    }

    private static async Task WaitForCrashFence(SqlConnection observer, int holder, int contenderPid)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(7))
        {
            var waiting = await observer.QuerySingleAsync<int>(@"SELECT COUNT(*) FROM sys.dm_tran_locks waiter
JOIN sys.dm_tran_locks holder ON holder.resource_database_id=waiter.resource_database_id
 AND holder.resource_type=waiter.resource_type AND holder.resource_description=waiter.resource_description
JOIN sys.dm_exec_sessions session ON session.session_id=waiter.request_session_id
WHERE waiter.resource_database_id=DB_ID() AND waiter.resource_type='APPLICATION'
 AND waiter.request_status='WAIT' AND holder.request_status='GRANT'
 AND holder.request_session_id=@holder AND session.host_process_id=@contenderPid;", new { holder, contenderPid });
            if (waiting > 0) return;
            await Task.Delay(25);
        }
        Assert.Fail("Independent same-key contender was not observed waiting on the original transaction application lock within 7 seconds.");
    }

    private sealed class CrashArrangement
    {
        public Intent Intent { get; set; } = null!;
        public string Access { get; set; } = "";
        public JObject SavedBody { get; set; } = null!;
        public JToken? OriginalDetail { get; set; }
        public int PriorJournalCount { get; set; }
        public int OtherWarehouse { get; set; }
        public string OtherWarehouseState { get; set; } = "";
    }

    private static async Task<CrashArrangement> ArrangeCrash(Data data, string kind)
    {
        await data.GrantMutations(data.Http.Alice);
        var access = (await data.Http.Login("alice")).Access;
        await data.Context(access);
        // Same product, different warehouse and moving-average cost. No target
        // operation may consume/revalue this independent stock position.
        var otherWarehouse = await data.Http.Admin.QuerySingleAsync<int>("INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'W2',N'Independent warehouse');");
        var otherBody = data.Receipt(); otherBody["warehouseId"] = otherWarehouse;
        otherBody["lines"] = new JArray(data.ReceiptLine(data.Product, "7.0000", "5.0000"));
        var otherReceipt = await data.Execute(new Intent("Receipt.Create", otherBody), access);
        await data.Execute(new Intent("Receipt.Post", Post(otherReceipt.Result), (string)otherReceipt.Result["receiptId"]!), access);
        var otherState = await CrashWarehouseSnapshot(data.Http.Admin, otherWarehouse);
        var sales = kind.StartsWith("Sales.", StringComparison.Ordinal);
        var body = sales ? data.Sale() : data.Receipt();
        JToken? detail = null;
        string? target = null;
        if (kind == "Sales.Post")
        {
            var stock = await data.Execute(new Intent("Receipt.Create", data.Receipt()), access);
            await data.Execute(new Intent("Receipt.Post", Post(stock.Result), (string)stock.Result["receiptId"]!), access);
        }
        if (!kind.EndsWith(".Create", StringComparison.Ordinal))
        {
            var created = await data.Execute(new Intent(sales ? "Sales.Create" : "Receipt.Create", body), access);
            detail = created.Result;
            target = (string)detail[sales ? "saleId" : "receiptId"]!;
            body = kind.EndsWith(".Post", StringComparison.Ordinal) ? Post(detail) : Save(detail, sales);
            if (kind.EndsWith(".Update", StringComparison.Ordinal)) body["note"] = "WEB5 exact frozen update";
        }
        return new CrashArrangement { Intent = new Intent(kind, body, target), Access = access, OriginalDetail = detail,
            SavedBody = kind.EndsWith(".Post", StringComparison.Ordinal) ? Save(detail!, sales) : (JObject)body.DeepClone(),
            OtherWarehouse = otherWarehouse, OtherWarehouseState = otherState,
            PriorJournalCount = await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;") };
    }

    private static JObject CrashConfiguration(CrashScope scope, CrashArrangement arranged, string phase, bool status = false) => new JObject
    {
        ["connectionString"] = scope.ConnectionString, ["phase"] = phase,
        ["method"] = status ? "GET" : arranged.Intent.Method.Method,
        ["path"] = status ? "operations/" + arranged.Intent.Key.ToString("D") : arranged.Intent.Path,
        ["body"] = status ? JValue.CreateNull() : new JValue(arranged.Intent.Body.ToString(Formatting.None)),
        ["access"] = arranged.Access,
        ["headers"] = status ? new JObject {
            ["X-MyDmsVn-Deployment-Epoch"] = new JArray(scope.Data.Epoch),
            ["X-MyDmsVn-Operation-Fingerprint"] = new JArray(arranged.Intent.Fingerprint) }
            : new JObject { ["Idempotency-Key"] = new JArray(arranged.Intent.Key.ToString("D")),
                ["X-MyDmsVn-Deployment-Epoch"] = new JArray(scope.Data.Epoch) }
    };

    private static async Task<Reply> CrashRequest(CrashScope scope, JObject configuration)
    {
        var child = await scope.Start(configuration);
        await child.DispatchAsync();
        var reply = await child.ReplyAsync();
        await child.CloseAsync();
        return reply;
    }

    private static async Task RecoverAndReplayCrash(CrashScope scope, CrashArrangement arranged)
    {
        using var oracle = await CrashOracle(scope.ConnectionString);
        var envelope = await CrashOriginalEnvelope(oracle, scope.Data, arranged.Intent);
        var before = await CrashSnapshot(scope.ConnectionString);
        AssertStatus(await CrashRequest(scope, CrashConfiguration(scope, arranged, "none", status: true)), arranged.Intent, scope.Data.Epoch, "COMMITTED", envelope);
        var replay = await CrashRequest(scope, CrashConfiguration(scope, arranged, "none"));
        Success(replay);
        Assert.True(JToken.DeepEquals(envelope, replay.Json), "Restart readback/replay must retain original result bytes, versions and ordered IDs.");
        Assert.Equal(before, await CrashSnapshot(scope.ConnectionString));
    }

    private static async Task<JObject> CrashOriginalEnvelope(SqlConnection oracle, Data data, Intent intent)
    {
        var bytes = await oracle.QuerySingleAsync<byte[]>("SELECT ResultBytes FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@key;", new { actor = data.Http.Alice, key = intent.Key });
        return JObject.Parse(new UTF8Encoding(false, true).GetString(bytes));
    }

    private static async Task VerifyCrashCommitted(SqlConnection oracle, Data data, CrashArrangement arranged)
    {
        var intent = arranged.Intent;
        var sales = intent.Kind.StartsWith("Sales.", StringComparison.Ordinal);
        var post = intent.Kind.EndsWith(".Post", StringComparison.Ordinal);
        var update = intent.Kind.EndsWith(".Update", StringComparison.Ordinal);
        var envelope = await CrashOriginalEnvelope(oracle, data, intent);
        var result = envelope["data"]!;
        var idName = sales ? "saleId" : "receiptId";
        var id = long.Parse((string)result[idName]!, CultureInfo.InvariantCulture);
        Assert.Equal(arranged.OtherWarehouseState, await CrashWarehouseSnapshot(oracle, arranged.OtherWarehouse));
        Assert.Equal(arranged.PriorJournalCount + 1, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
        var journals = await CrashRows(oracle, "WebOperationJournal WHERE ActorUserId=" + data.Http.Alice.ToString(CultureInfo.InvariantCulture) + " AND OperationId='" + intent.Key.ToString("D") + "'");
        var journal = Assert.Single(journals);
        Assert.Equal("COMMITTED", (string?)journal["Disposition"]);
        Assert.Equal(data.Epoch, Guid.Parse((string)journal["DeploymentEpoch"]!).ToString("D"));
        Assert.Equal(1, (int)journal["ContractVersion"]!); Assert.Equal(1, (int)journal["CodecVersion"]!);
        Assert.Equal(Array.IndexOf(CrashKinds, intent.Kind) + 1, (int)journal["OperationKind"]!);
        Assert.Equal(intent.Target == null ? (long?)null : long.Parse(intent.Target, CultureInfo.InvariantCulture), (long?)journal["RequestTargetId"]);
        Assert.Equal(id, (long)journal["ResultDocumentId"]!);
        Assert.Equal(HashBytes(intent.Fingerprint), Convert.FromBase64String((string)journal["RequestFingerprint"]!));
        var headers = await CrashRows(oracle, (sales ? "SalesReceipts" : "GoodsReceipts") + " WHERE WarehouseId=" + data.Warehouse.ToString(CultureInfo.InvariantCulture));
        var header = Assert.Single(headers);
        Assert.Equal(id, (long)header[sales ? "SaleId" : "ReceiptId"]!);
        Assert.Equal(data.Http.Alice, (int)header["CreatedByUserId"]!);
        if (update || post) Assert.Equal(data.Http.Alice, (int)header["UpdatedByUserId"]!);
        Assert.Equal(post ? "Posted" : "Draft", (string?)header["Status"]);
        Assert.Equal(post ? "Posted" : "Draft", (string?)result["status"]);
        Assert.Equal(data.Warehouse, (int)header["WarehouseId"]!); Assert.Equal(data.Employee, (int)header["EmployeeId"]!);
        Assert.Equal((string?)arranged.SavedBody["note"], (string?)header["Note"]);
        if (sales) Assert.Equal(data.Customer, (int)header["CustomerId"]!);
        var dateName = sales ? "saleDate" : "receiptDate";
        Assert.Equal((string?)arranged.SavedBody[dateName], ((DateTime)header[sales ? "SaleDate" : "ReceiptDate"]!).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (!post)
        {
            foreach (var field in sales ? new[] { "saleDate", "customerId", "warehouseId", "employeeId", "note" } : new[] { "receiptDate", "warehouseId", "employeeId", "note" })
                Assert.True(JToken.DeepEquals(arranged.SavedBody[field], result[field]), "Original result field differs from frozen intent: " + field);
            Assert.Equal((string?)header[sales ? "SaleNo" : "ReceiptNo"], (string?)result[sales ? "saleNumber" : "receiptNumber"]);
        }
        if (post)
        {
            Assert.Equal(data.Http.Alice, (int)header["PostedByUserId"]!);
            Assert.Equal((DateTime)header["PostedAtUtc"]!, (DateTime)result["postedAtUtc"]!);
        }
        if (sales || !post) Assert.Equal((string?)header["RowVersion"], (string?)result["version"]);
        else Assert.Null(result["version"]);
        if (arranged.OriginalDetail != null) Assert.NotEqual((string?)arranged.OriginalDetail["version"], (string?)header["RowVersion"]);
        var lines = await CrashRows(oracle, (sales ? "SalesReceiptLines WHERE SaleId=" : "GoodsReceiptLines WHERE ReceiptId=") + id.ToString(CultureInfo.InvariantCulture) + " ORDER BY [LineNo]");
        var frozenLines = (JArray)arranged.SavedBody["lines"]!;
        Assert.Equal(frozenLines.Count, lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index]; var sent = frozenLines[index];
            Assert.Equal(index + 1, (int)line["LineNo"]!);
            Assert.Equal(id, (long)line[sales ? "SaleId" : "ReceiptId"]!);
            Assert.Equal(data.Http.Alice, (int)line["CreatedByUserId"]!);
            Assert.Equal((int)sent["productId"]!, (int)line["ProductId"]!);
            Assert.Equal(decimal.Parse((string)sent[sales ? "qty" : "quantity"]!, CultureInfo.InvariantCulture), (decimal)line[sales ? "Qty" : "Quantity"]!);
            Assert.Equal(decimal.Parse((string)sent[sales ? "unitPrice" : "unitCost"]!, CultureInfo.InvariantCulture), (decimal)line[sales ? "UnitPrice" : "UnitCost"]!);
            var lineId = (long)line[sales ? "SaleLineId" : "ReceiptLineId"]!;
            if (!post) Assert.Equal(lineId.ToString(CultureInfo.InvariantCulture), (string?)result["lines"]![index]![sales ? "saleLineId" : "receiptLineId"]);
            else Assert.Equal(lineId.ToString(CultureInfo.InvariantCulture), (string?)arranged.OriginalDetail!["lines"]![index]![sales ? "saleLineId" : "receiptLineId"]);
            if (!post)
            {
                var originalLine = result["lines"]![index]!;
                Assert.Equal((int)sent["productId"]!, (int)originalLine["productId"]!);
                foreach (var field in sales ? new[] { "qty", "unitPrice" } : new[] { "quantity", "unitCost" })
                    Assert.Equal(decimal.Parse((string)sent[field]!, CultureInfo.InvariantCulture), decimal.Parse((string)originalLine[field]!, CultureInfo.InvariantCulture));
            }
            if (sales)
            {
                Assert.Equal((bool)sent["isGift"]!, (bool)line["IsGift"]!);
                Assert.Equal(index == 0 ? 75m : 0m, (decimal)line["LineAmount"]!);
                if (post) { Assert.Equal(12m, (decimal)line["PostedUnitCost"]!); Assert.Equal(new[] { 36m, 24m, 12m }[index], (decimal)line["CostAmount"]!); }
                else { Assert.Null(line["PostedUnitCost"]); Assert.Null(line["CostAmount"]); }
                if ((string?)sent["saleLineId"] != "0") Assert.Equal((string?)sent["saleLineId"], lineId.ToString(CultureInfo.InvariantCulture));
            }
        }
        Assert.Equal(sales ? 1 : 0, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceipts;"));
        Assert.Equal(!sales || post ? 2 : 1, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.GoodsReceipts;"));
        var ledgerCount = post ? sales ? 4 : 1 : 0;
        Assert.Equal(ledgerCount + 1, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
        Assert.Equal(ledgerCount + 1, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries;"));
        Assert.Equal(ledgerCount, await oracle.QuerySingleAsync<int>(@"SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId
WHERE l.WarehouseId=@warehouse AND l.ProductId=@product AND l.PostedByUserId=@actor AND v.ValueChange=l.QuantityChange*12;", new { warehouse = data.Warehouse, product = data.Product, actor = data.Http.Alice }));
        Assert.Equal(post ? 2 : 1, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockBalances;"));
        Assert.Equal(7m, await oracle.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances WHERE WarehouseId=@warehouse AND ProductId=@product;", new { warehouse = arranged.OtherWarehouse, product = data.Product }));
        Assert.Equal(35m, await oracle.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances WHERE WarehouseId=@warehouse AND ProductId=@product;", new { warehouse = arranged.OtherWarehouse, product = data.Product }));
        if (post)
        {
            Assert.Equal(sales ? 4m : 10m, await oracle.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances WHERE WarehouseId=@warehouse AND ProductId=@product;", new { warehouse = data.Warehouse, product = data.Product }));
            Assert.Equal(sales ? 48m : 120m, await oracle.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances WHERE WarehouseId=@warehouse AND ProductId=@product;", new { warehouse = data.Warehouse, product = data.Product }));
            var linked = sales ? @"SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.SalesReceiptLines s ON l.DocumentId=s.SaleId AND l.DocumentLineId=s.SaleLineId JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId WHERE l.DocumentType='SalesReceipt' AND l.QuantityChange=-s.Qty AND v.ValueChange=-s.CostAmount;"
                : @"SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.GoodsReceiptLines r ON l.DocumentId=r.ReceiptId AND l.DocumentLineId=r.ReceiptLineId JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId WHERE l.DocumentType='GoodsReceipt' AND l.WarehouseId=@warehouse AND l.QuantityChange=r.Quantity AND v.ValueChange=r.Quantity*r.UnitCost;";
            Assert.Equal(sales ? 3 : 1, await oracle.QuerySingleAsync<int>(linked, new { warehouse = data.Warehouse }));
        }
    }

    private static async Task<string> CrashWarehouseSnapshot(SqlConnection oracle, int warehouse)
    {
        var filter = warehouse.ToString(CultureInfo.InvariantCulture);
        var rows = new List<string>();
        foreach (var source in new[] {
            "GoodsReceipts WHERE WarehouseId=" + filter + " ORDER BY ReceiptId",
            "GoodsReceiptLines WHERE ReceiptId IN (SELECT ReceiptId FROM dbo.GoodsReceipts WHERE WarehouseId=" + filter + ") ORDER BY ReceiptLineId",
            "StockLedger WHERE WarehouseId=" + filter + " ORDER BY LedgerId",
            "StockValuationEntries WHERE LedgerId IN (SELECT LedgerId FROM dbo.StockLedger WHERE WarehouseId=" + filter + ") ORDER BY LedgerId",
            "StockBalances WHERE WarehouseId=" + filter + " ORDER BY ProductId" })
            rows.Add((await CrashRows(oracle, source)).ToString(Formatting.None));
        return string.Join("|", rows);
    }

    private static async Task<SqlConnection> CrashOracle(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { Pooling = false };
        var connection = new SqlConnection(builder.ConnectionString);
        try { await connection.OpenAsync(); return connection; }
        catch { connection.Dispose(); throw; }
    }
    private static async Task<JArray> CrashRows(SqlConnection connection, string source) => JArray.Parse(await connection.QuerySingleAsync<string>("SELECT COALESCE((SELECT * FROM dbo." + source + " FOR JSON PATH),N'[]');"));
    private static async Task<string> CrashSnapshot(string connectionString)
    {
        using var oracle = await CrashOracle(connectionString);
        var rows = new List<string>();
        foreach (var source in new[] { "SalesReceipts ORDER BY SaleId", "SalesReceiptLines ORDER BY SaleLineId", "GoodsReceipts ORDER BY ReceiptId", "GoodsReceiptLines ORDER BY ReceiptLineId", "StockBalances ORDER BY WarehouseId,ProductId", "StockLedger ORDER BY LedgerId", "StockValuationEntries ORDER BY LedgerId", "WebOperationJournal ORDER BY ActorUserId,OperationId" })
            rows.Add((await CrashRows(oracle, source)).ToString(Formatting.None));
        // SQL identity/sequence/database rowversion allocation is nontransactional;
        // every persisted business/journal row (including its rowversion) is compared.
        return string.Join("|", rows);
    }

    private sealed class CrashScope
    {
        public Data Data { get; set; } = null!;
        public string ConnectionString { get; set; } = "";
        public Intent? Operation { get; set; }
        public List<WebOperationCrashProcess> Children { get; } = new();
        public async Task<WebOperationCrashProcess> Start(JObject configuration)
        {
            var child = await WebOperationCrashProcess.StartAsync(configuration);
            Children.Add(child); return child;
        }
    }
    // Child process exit does not prove SQL has finished rollback/Commit. Before
    // classification and teardown, independently establish SQL quiescence. The
    // fixture owns every connection; none is pooled and no SQL session is killed.
    private static async Task AwaitCrashQuiescence(CrashScope scope, int retainedAdminSession = 0)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var builder = new SqlConnectionStringBuilder(scope.ConnectionString) { Pooling = false, ConnectTimeout = 2 };
        using var observer = new SqlConnection(builder.ConnectionString);
        try
        {
            await observer.OpenAsync(budget.Token);
            using var command = observer.CreateCommand();
            command.CommandTimeout = 2;
            var ownedPids = scope.Children.Select(child => child.ProcessId).Where(pid => pid > 0).Distinct().ToArray();
            var pidList = ownedPids.Length == 0 ? "-1" : string.Join(",", ownedPids.Select(pid => pid.ToString(CultureInfo.InvariantCulture)));
            command.CommandText = @"SELECT
 (SELECT COUNT(*) FROM sys.dm_exec_sessions s
  WHERE s.is_user_process=1 AND s.session_id<>@@SPID AND s.session_id<>@retained
   AND (s.database_id=DB_ID()
    OR EXISTS(SELECT 1 FROM sys.dm_exec_requests r WHERE r.session_id=s.session_id AND r.database_id=DB_ID())
    OR EXISTS(SELECT 1 FROM sys.dm_tran_session_transactions st
       JOIN sys.dm_tran_database_transactions dt ON dt.transaction_id=st.transaction_id
       WHERE st.session_id=s.session_id AND dt.database_id=DB_ID())))
 + (SELECT COUNT(*) FROM sys.dm_tran_database_transactions dt
    WHERE dt.database_id=DB_ID() AND dt.database_transaction_type IN(1,2)
     AND NOT EXISTS(SELECT 1 FROM sys.dm_tran_session_transactions st
       WHERE st.transaction_id=dt.transaction_id AND st.session_id IN(@@SPID,@retained)))
 + (SELECT COUNT(*) FROM sys.dm_exec_sessions s WHERE s.is_user_process=1
     AND s.session_id<>@@SPID AND s.host_process_id IN(" + pidList + "));";
            command.Parameters.Add("@retained", SqlDbType.Int).Value = retainedAdminSession;
            while (true)
            {
                budget.Token.ThrowIfCancellationRequested();
                if (Convert.ToInt32(await command.ExecuteScalarAsync(budget.Token), CultureInfo.InvariantCulture) == 0) break;
                await Task.Delay(50, budget.Token);
            }
            if (scope.Operation == null) return; // Arrangement failed before any crash dispatch.
            // Same epoch -> actor/key transaction fence as the real writer. The
            // lock is released before fresh oracle/replay; all writers are gone.
            using var fence = observer.CreateCommand();
            fence.CommandTimeout = 3;
            fence.CommandText = @"SET LOCK_TIMEOUT 2000;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.WebDeploymentState WITH(HOLDLOCK) WHERE Id=1 AND DeploymentEpoch=@epoch)
 BEGIN ROLLBACK; THROW 51231,'Crash oracle epoch mismatch.',1; END;
DECLARE @result int;
EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=2000;
IF @result<0 BEGIN ROLLBACK; THROW 51232,'Crash oracle fence unavailable.',1; END;
COMMIT;";
            fence.Parameters.Add("@epoch", SqlDbType.UniqueIdentifier).Value = Guid.Parse(scope.Data.Epoch);
            fence.Parameters.Add("@resource", SqlDbType.NVarChar, 255).Value = "MyDmsVn.WebOperation:" + scope.Data.Http.Alice.ToString(CultureInfo.InvariantCulture) + ":" + scope.Operation.Key.ToString("N");
            await fence.ExecuteNonQueryAsync(budget.Token);
        }
        catch (Exception)
        {
            // Do not print provider errors, credentials, SQL text or request data.
            var failure = new InvalidOperationException("Owned SQL quiescence/epoch/fence was not proved within the bounded observation; classification or cleanup is unsafe.");
            GuiTestSafety.Poison(failure);
            throw failure;
        }
    }

    private async Task OwnedCrash(Func<CrashScope, Task> body)
    {
        Assert.False(WebOperationCrashProcess.IsPoisoned, "An earlier child cleanup was uncertain; no further database is admitted.");
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        var scope = new CrashScope { ConnectionString = database.ConnectionString };
        HttpScenario? http = null;
        Exception? failure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().MigrateWeb(database.ConnectionString).Successful);
            http = await HttpScenario.Create(database.ConnectionString, web4Enabled: true);
            scope.Data = await Data.Seed(http);
            await body(scope);
        }
        catch (Exception error) { failure = error; }
        var cleanup = new List<Exception>();
        foreach (var child in scope.Children)
            try { await child.CloseAsync(); } catch (Exception error) { cleanup.Add(error); }
        try { http?.Dispose(); }
        catch (Exception error)
        {
            cleanup.Add(error);
            GuiTestSafety.Poison(new InvalidOperationException("Owned HTTP scope disposal failed; crash database quarantined."));
        }
        // A failed body/assertion is not itself poison. Even then, no database
        // drop may substitute for independent proof that every SQL owner drained.
        try { await AwaitCrashQuiescence(scope); }
        catch (Exception error)
        {
            cleanup.Add(error);
            GuiTestSafety.Poison(error);
        }
        if (WebOperationCrashProcess.IsPoisoned)
        {
            cleanup.Add(new InvalidOperationException("Owned process/HTTP/SQL cleanup is uncertain; database quarantined and intentionally not dropped."));
            _crashOutput.WriteLine("WEB5 cleanup: QUARANTINED; no database drop; stop SQL test run.");
        }
        else
        {
            try
            {
                await database.DisposeAsync();
                _crashOutput.WriteLine("WEB5 cleanup: child drain and independent SQL quiescence/fence proved; owned database removed.");
            }
            catch (Exception error)
            {
                cleanup.Add(error);
                GuiTestSafety.Poison(new InvalidOperationException("Owned crash database disposal failed; further SQL tests are unsafe."));
                _crashOutput.WriteLine("WEB5 cleanup: database disposal failed; stop SQL test run.");
            }
        }
        if (failure != null) cleanup.Insert(0, failure);
        if (cleanup.Count != 0) throw new AggregateException("Crash fixture failed or cleanup was incomplete.", cleanup);
    }
}