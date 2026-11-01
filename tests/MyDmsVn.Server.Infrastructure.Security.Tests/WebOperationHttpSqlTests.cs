using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using HttpScenario = MyDmsVn.Server.Infrastructure.IntegrationTests.WebAuthHttpSqlTests.Scenario;
using Reply = MyDmsVn.Server.Infrastructure.IntegrationTests.WebAuthHttpSqlTests.Reply;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Real OWIN + SQL-backed session + Application + restricted Web SQL principal.
// Administrative SQL arranges fixtures and observes physical rows independently.
// TestServer restart is a fresh HTTP/service-provider lifetime, not an OS-process crash.
public sealed class WebOperationHttpSqlTests
{
    [WebFrontendPortSqlFact]
    [Trait("Category", "SecurityProvisioning")]
    public Task Frontend_ports_recover_six_commands_through_actual_TestServer() => Owned(async data =>
    {
        await data.GrantMutations(data.Http.Alice);
        var observed = await WebFrontendPortBridge.RunAsync(data.Http, new JObject
        {
            ["actor"] = new JObject { ["username"] = "alice", ["password"] = HttpScenario.Password, ["userId"] = data.Http.Alice },
            ["catalog"] = new JObject { ["customerId"] = data.Customer, ["warehouseId"] = data.Warehouse,
                ["employeeId"] = data.Employee, ["productId"] = data.Product, ["otherProductId"] = data.OtherProduct }
        });
        Assert.Equal(1, observed.CompletedTests);
        Assert.Equal(6, observed.MutationDispatches.Count);
        Assert.All(observed.MutationDispatches, mutation =>
        {
            Assert.True(Guid.TryParseExact(mutation.Key, "D", out var key) && key != Guid.Empty);
            Assert.Equal(2, mutation.Value); // One initial send and one explicit same-intent replay.
        });
        Assert.True(observed.StatusReads >= 6);
        Assert.Equal(2, observed.DeniedDocumentReads);
        Assert.Equal(1, observed.Restarts);
        Assert.Equal(6, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE Disposition='COMMITTED';"));
        await data.Oracle();
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task First_unique_key_business_rejections_leave_source_inventory_and_journal_unchanged() => Owned(async data =>
    {
        await data.GrantMutations(data.Http.Alice);
        var access = (await data.Http.Login("alice")).Access;
        await data.Context(access);
        var keys = new HashSet<Guid>();
        var rejected = 0;
        async Task<Completed> Execute(Intent intent)
        {
            Assert.True(keys.Add(intent.Key), "Every HTTP invocation in this case must have a fresh key.");
            return await data.Execute(intent, access);
        }
        async Task Reject(Intent intent, int status, string code, string? detailCode = null)
        {
            Assert.True(keys.Add(intent.Key), "A known rejection must not stand in for an older unknown attempt.");
            var before = await data.State();
            Assert.Equal(0, await data.Http.Admin.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@key;",
                new { actor = data.Http.Alice, key = intent.Key }));
            var reply = await data.Send(intent, access); // Exactly one dispatch; no status lookup or retry.
            Error(reply, status, code);
            Assert.Equal(new[] { "error" }, Names(reply.Json));
            var error = reply.Json["error"]!;
            Assert.Equal(new[] { "code", "correlationId", "details", "message" }, Names(error));
            Assert.Equal(JTokenType.String, error["message"]!.Type);
            Assert.Equal(JTokenType.String, error["correlationId"]!.Type);
            var details = Assert.IsType<JArray>(error["details"]);
            Assert.NotEmpty(details);
            Assert.All(details, detail =>
            {
                Assert.Equal(detailCode ?? code, (string?)detail["code"]);
                Assert.Equal(JTokenType.String, detail["message"]!.Type);
                Assert.All(Names(detail), name => Assert.Contains(name, new[] { "code", "field", "message" }));
                if (detail["field"] != null) Assert.Equal(JTokenType.String, detail["field"]!.Type);
            });
            Assert.Equal(before, await data.State());
            Assert.Equal(0, await data.Http.Admin.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@key;",
                new { actor = data.Http.Alice, key = intent.Key }));
            rejected++;
        }

        var receiptCreate = await Execute(new Intent("Receipt.Create", data.Receipt()));
        var saleCreate = await Execute(new Intent("Sales.Create", data.Sale()));
        var receiptId = (string)receiptCreate.Result["receiptId"]!;
        var saleId = (string)saleCreate.Result["saleId"]!;
        // Valid wire requests reach first-execution business validation. Inactive
        // Product is shared by all six commands; no malformed-header shortcut.
        await data.Http.Admin.ExecuteAsync("UPDATE dbo.Products SET IsActive=0 WHERE ProductId=@product;", new { product = data.Product });
        foreach (var intent in new[]
        {
            new Intent("Receipt.Create", data.Receipt()),
            new Intent("Receipt.Update", Save(receiptCreate.Result, false), receiptId),
            new Intent("Receipt.Post", Post(receiptCreate.Result), receiptId),
            new Intent("Sales.Create", data.Sale()),
            new Intent("Sales.Update", Save(saleCreate.Result, true), saleId),
            new Intent("Sales.Post", Post(saleCreate.Result), saleId)
        }) await Reject(intent, 400, "ValidationError", intent.Kind.StartsWith("Sales.", StringComparison.Ordinal)
            ? "SalesReceipt.ProductInactive" : "GoodsReceipt.ProductInactive");
        await data.Http.Admin.ExecuteAsync("UPDATE dbo.Products SET IsActive=1 WHERE ProductId=@product;", new { product = data.Product });

        const string missing = "9223372036854775807";
        foreach (var intent in new[]
        {
            new Intent("Receipt.Update", Save(receiptCreate.Result, false), missing),
            new Intent("Receipt.Post", Post(receiptCreate.Result), missing),
            new Intent("Sales.Update", Save(saleCreate.Result, true), missing),
            new Intent("Sales.Post", Post(saleCreate.Result), missing)
        }) await Reject(intent, 404, intent.Kind.StartsWith("Sales.", StringComparison.Ordinal) ? "SalesReceipt.NotFound" : "GoodsReceipt.NotFound");

        var receiptUpdateBody = Save(receiptCreate.Result, false);
        receiptUpdateBody["lines"] = new JArray(data.ReceiptLine(data.OtherProduct, "4.0000", "2.5000"), data.ReceiptLine(data.Product, "20.0000", "15.0000"));
        var receiptUpdate = await Execute(new Intent("Receipt.Update", receiptUpdateBody, receiptId));
        var saleUpdateBody = Save(saleCreate.Result, true);
        ((JArray)saleUpdateBody["lines"]!).Add(data.SaleLine(data.OtherProduct, "1.0000", "10.0000", false));
        var saleUpdate = await Execute(new Intent("Sales.Update", saleUpdateBody, saleId));
        foreach (var intent in new[]
        {
            new Intent("Receipt.Update", Save(receiptCreate.Result, false), receiptId),
            new Intent("Receipt.Post", Post(receiptCreate.Result), receiptId),
            new Intent("Sales.Update", Save(saleCreate.Result, true), saleId),
            new Intent("Sales.Post", Post(saleCreate.Result), saleId)
        }) await Reject(intent, 409, intent.Kind.StartsWith("Sales.", StringComparison.Ordinal) ? "SalesReceipt.ConcurrencyConflict" : "GoodsReceipt.ConcurrencyConflict");

        await Execute(new Intent("Receipt.Post", Post(receiptUpdate.Result), receiptId));
        await Execute(new Intent("Sales.Post", Post(saleUpdate.Result), saleId));
        foreach (var intent in new[]
        {
            new Intent("Receipt.Update", Save(receiptUpdate.Result, false), receiptId),
            new Intent("Receipt.Post", Post(receiptUpdate.Result), receiptId),
            new Intent("Sales.Update", Save(saleUpdate.Result, true), saleId),
            new Intent("Sales.Post", Post(saleUpdate.Result), saleId)
        }) await Reject(intent, 409, intent.Kind.StartsWith("Sales.", StringComparison.Ordinal) ? "SalesReceipt.AlreadyPosted" : "GoodsReceipt.AlreadyPosted");

        Assert.Equal(18, rejected); // Eighteen rejection assertions inside ONE xUnit case.
        Assert.Equal(24, keys.Count); // Six successful setup commands, eighteen fresh rejected intents.
        Assert.Equal(6, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE Disposition='COMMITTED';"));
        await data.Oracle();
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Six_commands_recover_original_results_after_acknowledgement_suppression_and_host_restart_without_Read() =>
        Owned(data => Lifecycle(data, concurrent: false));

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Concurrent_duplicates_of_all_six_commands_have_one_original_result_and_one_set_of_SQL_effects() =>
        Owned(data => Lifecycle(data, concurrent: true));

    private static async Task Lifecycle(Data data, bool concurrent)
    {
        await data.GrantMutations(data.Http.Alice);
        var access = (await data.Http.Login("alice")).Access;
        await data.Context(access);
        data.Http.Audit.ThrowOnPostSuccess = true;
        var completed = new List<Completed>();

        var receiptCreate = await data.Execute(new Intent("Receipt.Create", data.Receipt()), access, concurrent);
        completed.Add(receiptCreate);
        var receiptId = (string)receiptCreate.Result["receiptId"]!;
        var receiptUpdateBody = Save(receiptCreate.Result, sales: false);
        receiptUpdateBody["lines"] = new JArray(data.ReceiptLine(data.OtherProduct, "4.0000", "2.5000"), data.ReceiptLine(data.Product, "20.0000", "15.0000"));
        receiptUpdateBody["note"] = "Nhập kho đã sửa";
        var receiptUpdate = await data.Execute(new Intent("Receipt.Update", receiptUpdateBody, receiptId), access, concurrent);
        completed.Add(receiptUpdate);
        Assert.NotEqual((string?)receiptCreate.Result["version"], (string?)receiptUpdate.Result["version"]);
        var originalReceiptLines = receiptCreate.Result["lines"]!.Select(line => (string)line["receiptLineId"]!).ToArray();
        Assert.All(receiptUpdate.Result["lines"]!, line => Assert.DoesNotContain((string)line["receiptLineId"]!, originalReceiptLines));
        Assert.Equal(new[] { data.OtherProduct, data.Product }, receiptUpdate.Result["lines"]!.Select(line => (int)line["productId"]!).ToArray());
        Assert.Equal(0, await data.Http.Admin.QuerySingleAsync<int>("SELECT (SELECT COUNT(*) FROM dbo.StockLedger)+(SELECT COUNT(*) FROM dbo.StockValuationEntries)+(SELECT COUNT(*) FROM dbo.StockBalances);"));
        var receiptPost = await data.Execute(new Intent("Receipt.Post", Post(receiptUpdate.Result), receiptId), access, concurrent);
        completed.Add(receiptPost);
        Assert.Equal(new[] { "postedAtUtc", "receiptId", "status" }, Names(receiptPost.Result));
        Assert.Null(receiptPost.Result["version"]);

        var saleCreate = await data.Execute(new Intent("Sales.Create", data.Sale()), access, concurrent);
        completed.Add(saleCreate);
        var saleId = (string)saleCreate.Result["saleId"]!;
        var saleUpdateBody = Save(saleCreate.Result, sales: true);
        ((JArray)saleUpdateBody["lines"]!).Add(data.SaleLine(data.OtherProduct, "1.0000", "10.0000", false));
        var saleUpdate = await data.Execute(new Intent("Sales.Update", saleUpdateBody, saleId), access, concurrent);
        completed.Add(saleUpdate);
        Assert.NotEqual((string?)saleCreate.Result["version"], (string?)saleUpdate.Result["version"]);
        Assert.Equal(saleCreate.Result["lines"]!.Select(line => (string)line["saleLineId"]!), saleUpdate.Result["lines"]!.Take(3).Select(line => (string)line["saleLineId"]!));
        Assert.Equal(4, saleUpdate.Result["lines"]!.Select(line => (string)line["saleLineId"]!).Distinct().Count());
        Assert.Equal(JTokenType.String, saleUpdate.Result["totalAmount"]!.Type);
        Assert.Equal(85m, decimal.Parse((string)saleUpdate.Result["totalAmount"]!, CultureInfo.InvariantCulture));
        var salePost = await data.Execute(new Intent("Sales.Post", Post(saleUpdate.Result), saleId), access, concurrent);
        completed.Add(salePost);
        Assert.Equal(new[] { "postedAtUtc", "saleId", "status", "version" }, Names(salePost.Result));
        Assert.NotEqual((string?)saleUpdate.Result["version"], (string?)salePost.Result["version"]);
        Assert.Equal(Convert.FromBase64String((string)salePost.Result["version"]!), await data.Http.Admin.QuerySingleAsync<byte[]>("SELECT RowVersion FROM dbo.SalesReceipts;"));
        Assert.Equal(receiptUpdate.Result["lines"]!.Select(line => (string)line["receiptLineId"]!),
            (await data.Http.Admin.QueryAsync<long>("SELECT ReceiptLineId FROM dbo.GoodsReceiptLines ORDER BY [LineNo];")).Select(id => id.ToString(CultureInfo.InvariantCulture)));
        Assert.Equal(saleUpdate.Result["lines"]!.Select(line => (string)line["saleLineId"]!),
            (await data.Http.Admin.QueryAsync<long>("SELECT SaleLineId FROM dbo.SalesReceiptLines ORDER BY [LineNo];")).Select(id => id.ToString(CultureInfo.InvariantCulture)));
        await data.Oracle();
        Assert.Equal(2, data.Http.Audit.PostFailures);

        Error(await data.Http.Get("sales-receipts/" + saleId, access), 403, "Auth.Forbidden", cache: false);
        Error(await data.Http.Get("goods-receipts/" + receiptId, access), 403, "Auth.Forbidden", cache: false);
        // A reconstructed Get or rerun of mutable reference validators would now fail.
        await data.Http.Admin.ExecuteAsync("UPDATE dbo.Products SET IsActive=0,Name=N'Changed after commit'; UPDATE dbo.Customers SET IsActive=0; UPDATE dbo.Warehouses SET IsActive=0; UPDATE dbo.Employees SET IsActive=0;");
        var beforeRecovery = await data.State();
        // The server completed every response above. The test retains it only as an oracle;
        // a consumer suppresses acknowledgement and reconnects. This does not simulate a crash.
        data.Http.RestartHost();
        await data.Context(access);
        foreach (var operation in completed)
        {
            var status = await data.Status(operation.Intent, access);
            AssertStatus(status, operation.Intent, data.Epoch, "COMMITTED", operation.Reply.Json);
            var replay = await data.Send(operation.Intent, access);
            Success(replay);
            Assert.True(JToken.DeepEquals(operation.Reply.Json, replay.Json), "Replay must retain original version, ordered server line IDs and all original fields.");
            Assert.Equal(beforeRecovery, await data.State());
            var permission = (operation.Intent.Kind.StartsWith("Sales.", StringComparison.Ordinal) ? "Sales.Receipts." : "Inventory.GoodsReceipts.")
                + (operation.Intent.Kind.EndsWith(".Post", StringComparison.Ordinal) ? "Post" : "Write");
            await data.Http.Admin.ExecuteAsync("UPDATE dbo.UserPermissions SET Granted=0 WHERE UserId=@actor AND PermissionKey=@permission;", new { actor = data.Http.Alice, permission });
            Error(await data.Send(operation.Intent, access), 403, "Auth.Forbidden");
            Error(await data.Status(operation.Intent, access), 403, "Auth.Forbidden");
            await data.Http.Admin.ExecuteAsync("UPDATE dbo.UserPermissions SET Granted=1 WHERE UserId=@actor AND PermissionKey=@permission;", new { actor = data.Http.Alice, permission });
            Assert.Equal(beforeRecovery, await data.State());
        }
        Assert.Equal(6, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE Disposition='COMMITTED';"));
        Assert.Equal(2, data.Http.Audit.PostFailures); // Replay must not run Post or its success audit again.
        await data.Oracle();
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Same_key_with_changed_body_route_target_or_version_is_conflict_without_new_effects() => Owned(async data =>
    {
        await data.GrantMutations(data.Http.Alice);
        var access = (await data.Http.Login("alice")).Access;
        await data.Context(access);
        var create = await data.Execute(new Intent("Sales.Create", data.Sale()), access);
        var id = (string)create.Result["saleId"]!;
        var update = await data.Execute(new Intent("Sales.Update", Save(create.Result, true), id), access);
        var before = await data.State();
        var changedBody = (JObject)create.Intent.Body.DeepClone();
        changedBody["note"] = "different immutable intent";
        var changedVersion = (JObject)update.Intent.Body.DeepClone();
        changedVersion["expectedVersion"] = update.Result["version"]!.DeepClone();
        foreach (var intent in new[]
        {
            new Intent("Sales.Create", changedBody, key: create.Intent.Key),
            new Intent("Receipt.Create", data.Receipt(), key: create.Intent.Key),
            new Intent("Sales.Update", update.Intent.Body, "9223372036854775807", update.Intent.Key),
            new Intent("Sales.Update", changedVersion, id, update.Intent.Key),
            new Intent("Sales.Post", Post(update.Result), id, update.Intent.Key)
        })
        {
            Error(await data.Send(intent, access), 409, "Idempotency.KeyConflict");
            Assert.Equal(before, await data.State());
        }
        var equivalent = (JObject)create.Intent.Body.DeepClone();
        equivalent["lines"]![0]!["qty"] = "0003.0000";
        var replay = await data.Send(new Intent("Sales.Create", equivalent, key: create.Intent.Key), access);
        Assert.True(JToken.DeepEquals(create.Reply.Json, SuccessEnvelope(replay)));
        Assert.Equal(before, await data.State());
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData("grant", 403, "Auth.Forbidden")]
    [InlineData("session", 401, "Auth.Unauthorized")]
    [InlineData("account", 401, "Auth.Unauthorized")]
    public Task Replay_and_status_recheck_current_authority(string revoke, int status, string code) => Owned(async data =>
    {
        await data.GrantMutations(data.Http.Alice);
        var access = (await data.Http.Login("alice")).Access;
        await data.Context(access);
        var original = await data.Execute(new Intent("Sales.Create", data.Sale()), access);
        if (revoke == "grant") await data.Http.Admin.ExecuteAsync("UPDATE dbo.UserPermissions SET Granted=0 WHERE UserId=@actor;", new { actor = data.Http.Alice });
        if (revoke == "session") await data.Http.Admin.ExecuteAsync("UPDATE dbo.WebAuthSessions SET RevokedAtUtc=SYSUTCDATETIME() WHERE UserId=@actor;", new { actor = data.Http.Alice });
        if (revoke == "account") await data.Http.Admin.ExecuteAsync("UPDATE dbo.Users SET IsActive=0 WHERE UserId=@actor;", new { actor = data.Http.Alice });
        var before = await data.State();
        Error(await data.Send(original.Intent, access), status, code);
        Error(await data.Status(original.Intent, access), status, code);
        Assert.Equal(before, await data.State());
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Status_other_actor_and_missing_are_indistinguishable_and_new_session_can_recover_own_operation() => Owned(async data =>
    {
        await data.GrantMutations(data.Http.Alice);
        await data.GrantMutations(data.Http.Bob);
        var access = (await data.Http.Login("alice")).Access;
        await data.Context(access);
        var original = await data.Execute(new Intent("Sales.Create", data.Sale()), access);
        var bob = (await data.Http.Login("bob")).Access;
        var missing = new Intent("Sales.Create", data.Sale());
        var absent = await data.Status(missing, bob);
        var otherActor = await data.Status(original.Intent, bob);
        Error(absent, 404, "Operation.NotObserved");
        Error(otherActor, 404, "Operation.NotObserved");
        Assert.True(JToken.DeepEquals(absent.Json, otherActor.Json));
        Assert.Empty(otherActor.Json["error"]!["details"]!);
        Error(await data.Status(original.Intent, null), 401, "Auth.Unauthorized");
        var before = await data.State();
        Error(await data.Status(missing, access), 404, "Operation.NotObserved");
        Assert.Equal(before, await data.State()); // No status request silently resubmits the missing intent.
        await data.Http.Admin.ExecuteAsync("UPDATE dbo.WebAuthSessions SET RevokedAtUtc=SYSUTCDATETIME() WHERE UserId=@actor;", new { actor = data.Http.Alice });
        var fresh = (await data.Http.Login("alice")).Access;
        AssertStatus(await data.Status(original.Intent, fresh), original.Intent, data.Epoch, "COMMITTED", original.Reply.Json);
        Assert.True(JToken.DeepEquals(original.Reply.Json, SuccessEnvelope(await data.Send(original.Intent, fresh))));
        Assert.Equal(before, await data.State());
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Wrong_fingerprint_and_stale_database_epoch_do_not_disclose_or_write() => Owned(async data =>
    {
        await data.GrantMutations(data.Http.Alice);
        var access = (await data.Http.Login("alice")).Access;
        await data.Context(access);
        var original = await data.Execute(new Intent("Sales.Create", data.Sale()), access);
        var before = await data.State();
        Error(await data.Status(original.Intent, access, fingerprint: new string('0', 64)), 409, "Idempotency.FingerprintMismatch");
        Error(await data.Status(original.Intent, access, epoch: Guid.NewGuid().ToString("D")), 409, "Deployment.EpochMismatch");
        Error(await data.Send(new Intent("Sales.Create", data.Sale()), access, epoch: Guid.NewGuid().ToString("D")), 409, "Deployment.EpochMismatch");
        Assert.Equal(before, await data.State());
        var oldEpoch = data.Epoch;
        await data.Http.Admin.ExecuteAsync("UPDATE dbo.WebDeploymentState SET DeploymentEpoch=NEWID(),RotatedAtUtc=SYSUTCDATETIME() WHERE Id=1;");
        Error(await data.Status(original.Intent, access), 409, "Deployment.EpochMismatch");
        Error(await data.Send(original.Intent, access), 409, "Deployment.EpochMismatch");
        await data.Context(access);
        Assert.NotEqual(oldEpoch, data.Epoch);
        Error(await data.Send(original.Intent, access), 409, "Idempotency.KeyConflict");
        Assert.Equal(before, await data.State());
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData("COMMITTED", "RESULT_EXPIRED", 410, "Operation.ResultExpired")]
    [InlineData("EXPIRED", "RESULT_EXPIRED", 410, "Operation.ResultExpired")]
    [InlineData("CANCELLED", "RESULT_UNAVAILABLE", 503, "Operation.ResultUnavailable")]
    public Task Retained_terminal_key_returns_null_status_outcome_and_never_reexecutes(string disposition, string state, int status, string code) => Owned(async data =>
    {
        await data.GrantMutations(data.Http.Alice);
        var access = (await data.Http.Login("alice")).Access;
        await data.Context(access);
        var original = await data.Execute(new Intent("Sales.Create", data.Sale()), access);
        var terminal = new Intent("Sales.Create", data.Sale());
        // Arrange an old retained key through admin SQL; runtime cannot update/delete keys.
        await data.Http.Admin.ExecuteAsync(@"DECLARE @n datetime2(7)=DATEADD(day,-8,SYSUTCDATETIME());
INSERT dbo.WebOperationJournal(ActorUserId,OperationId,DeploymentEpoch,ContractVersion,CodecVersion,OperationKind,RequestTargetId,RequestFingerprint,ResultDocumentId,Disposition,ResultBytes,CreatedAtUtc,CompletedAtUtc,ResultExpiresAtUtc)
VALUES(@actor,@key,@epoch,1,1,1,NULL,@hash,CASE WHEN @disposition='CANCELLED' THEN NULL ELSE @document END,@disposition,CASE WHEN @disposition='COMMITTED' THEN @bytes ELSE NULL END,@n,@n,CASE WHEN @disposition='CANCELLED' THEN NULL ELSE DATEADD(day,7,@n) END);", new
        {
            actor = data.Http.Alice, key = terminal.Key, epoch = Guid.Parse(data.Epoch), hash = HashBytes(terminal.Fingerprint),
            document = long.Parse((string)original.Result["saleId"]!, CultureInfo.InvariantCulture), disposition,
            bytes = Encoding.UTF8.GetBytes(original.Reply.Body)
        });
        var before = await data.State();
        data.Http.RestartHost();
        AssertStatus(await data.Status(terminal, access), terminal, data.Epoch, state, null);
        Error(await data.Send(terminal, access), status, code);
        var changed = (JObject)terminal.Body.DeepClone(); changed["note"] = "must not reuse terminal key";
        Error(await data.Send(new Intent("Sales.Create", changed, key: terminal.Key), access), 409, "Idempotency.KeyConflict");
        Assert.Equal(before, await data.State());
        Assert.Equal(1, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceipts;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Required_key_epoch_and_status_hash_headers_fail_closed_before_effects() => Owned(async data =>
    {
        await data.GrantMutations(data.Http.Alice);
        var access = (await data.Http.Login("alice")).Access;
        await data.Context(access);
        var intent = new Intent("Sales.Create", data.Sale());
        var before = await data.State();
        foreach (var headers in new[]
        {
            new Dictionary<string,string[]>(),
            new Dictionary<string,string[]> { ["Idempotency-Key"] = new[] { intent.Key.ToString("D") } },
            new Dictionary<string,string[]> { ["X-MyDmsVn-Deployment-Epoch"] = new[] { data.Epoch } },
            new Dictionary<string,string[]> { ["Idempotency-Key"] = new[] { intent.Key.ToString("D"), intent.Key.ToString("D") }, ["X-MyDmsVn-Deployment-Epoch"] = new[] { data.Epoch } },
            new Dictionary<string,string[]> { ["Idempotency-Key"] = new[] { Guid.Empty.ToString("D") }, ["X-MyDmsVn-Deployment-Epoch"] = new[] { data.Epoch } }
        }) Error(await data.Http.Request(intent.Method, intent.Path, intent.Body.ToString(Formatting.None), null, access, headers), 400, "ValidationError");
        Error(await data.Http.Request(HttpMethod.Get, "operations/" + intent.Key.ToString("D"), null, null, access,
            new Dictionary<string,string[]> { ["X-MyDmsVn-Deployment-Epoch"] = new[] { data.Epoch } }), 400, "ValidationError");
        Error(await data.Http.Get("operations/context", "invalid-token"), 401, "Auth.Unauthorized");
        Assert.Equal(before, await data.State());
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData("Sales.Create")]
    [InlineData("Sales.Update")]
    [InlineData("Receipt.Post")]
    public Task Retained_valid_shape_result_with_wrong_document_identity_is_unavailable_without_reexecution(string kind) => Owned(async data =>
    {
        await data.GrantMutations(data.Http.Alice);
        var access = (await data.Http.Login("alice")).Access;
        await data.Context(access);
        var sales = kind.StartsWith("Sales.", StringComparison.Ordinal);
        var original = await data.Execute(new Intent(sales ? "Sales.Create" : "Receipt.Create", sales ? data.Sale() : data.Receipt()), access);
        var idField = sales ? "saleId" : "receiptId";
        var documentId = (string)original.Result[idField]!;
        if (kind == "Sales.Update") original = await data.Execute(new Intent(kind, Save(original.Result, true), documentId), access);
        if (kind == "Receipt.Post") original = await data.Execute(new Intent(kind, Post(original.Result), documentId), access);
        var damaged = new Intent(original.Intent.Kind, original.Intent.Body, original.Intent.Target);
        var wrongOutcome = (JObject)original.Reply.Json.DeepClone();
        wrongOutcome["data"]![idField] = (long.Parse(documentId, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
        // Real encoded shape, but its result ID contradicts the retained journal/source identity.
        await data.Http.Admin.ExecuteAsync(@"INSERT dbo.WebOperationJournal(ActorUserId,OperationId,DeploymentEpoch,ContractVersion,CodecVersion,OperationKind,RequestTargetId,RequestFingerprint,ResultDocumentId,Disposition,ResultBytes,CreatedAtUtc,CompletedAtUtc,ResultExpiresAtUtc)
SELECT ActorUserId,@damaged,DeploymentEpoch,ContractVersion,CodecVersion,OperationKind,RequestTargetId,RequestFingerprint,ResultDocumentId,Disposition,@bytes,CreatedAtUtc,CompletedAtUtc,ResultExpiresAtUtc
FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@original;", new
        {
            actor = data.Http.Alice, original = original.Intent.Key, damaged = damaged.Key,
            bytes = Encoding.UTF8.GetBytes(wrongOutcome.ToString(Formatting.None))
        });
        var before = await data.State();
        AssertStatus(await data.Status(damaged, access), damaged, data.Epoch, "RESULT_UNAVAILABLE", null);
        Error(await data.Send(damaged, access), 503, "Operation.ResultUnavailable");
        Assert.Equal(before, await data.State());
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Journal_completion_failure_rolls_back_Post_and_does_not_automatically_retry() => Owned(async data =>
    {
        await data.GrantMutations(data.Http.Alice);
        var access = (await data.Http.Login("alice")).Access;
        await data.Context(access);
        var receipt = await data.Execute(new Intent("Receipt.Create", data.Receipt()), access);
        var intent = new Intent("Receipt.Post", Post(receipt.Result), (string)receipt.Result["receiptId"]!);
        // A nontransactional test sequence observes attempts even if the surrounding UoW rolls back.
        await data.Http.Admin.ExecuteAsync("CREATE SEQUENCE dbo.Web4HttpFaultAttempts AS int START WITH 1 INCREMENT BY 1; EXEC(N'CREATE TRIGGER dbo.Web4HttpRejectFirstJournal ON dbo.WebOperationJournal AFTER INSERT AS BEGIN DECLARE @attempt int=NEXT VALUE FOR dbo.Web4HttpFaultAttempts; IF @attempt=1 THROW 51222,''Injected journal completion failure.'',1; END;');");
        var before = await data.State();
        var failed = await data.Send(intent, access);
        Error(failed, 500, "InternalError");
        Assert.DoesNotContain("Injected", failed.Body);
        Assert.Equal(before, await data.State());
        Assert.Equal(1, await data.Http.Admin.QuerySingleAsync<int>("SELECT CONVERT(int,current_value) FROM sys.sequences WHERE object_id=OBJECT_ID(N'dbo.Web4HttpFaultAttempts');"));
        Error(await data.Status(intent, access), 404, "Operation.NotObserved");
        Assert.Equal(before, await data.State());
        Assert.Equal(0, await data.Http.Admin.QuerySingleAsync<int>("SELECT (SELECT COUNT(*) FROM dbo.StockLedger)+(SELECT COUNT(*) FROM dbo.StockValuationEntries)+(SELECT COUNT(*) FROM dbo.StockBalances);"));
    });

    private static string[] Names(JToken value) => ((JObject)value).Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
    private static JObject Post(JToken detail) => new JObject { ["expectedVersion"] = detail["version"]!.DeepClone() };
    private static JObject Save(JToken detail, bool sales)
    {
        var names = sales ? new[] { "saleDate", "customerId", "warehouseId", "employeeId", "note" } : new[] { "receiptDate", "warehouseId", "employeeId", "note" };
        var lineNames = sales ? new[] { "saleLineId", "productId", "qty", "unitPrice", "isGift" } : new[] { "productId", "quantity", "unitCost" };
        var result = new JObject();
        foreach (var name in names) if (detail[name] != null) result[name] = detail[name]!.DeepClone();
        result["expectedVersion"] = detail["version"]!.DeepClone();
        result["lines"] = new JArray(detail["lines"]!.Select(line => new JObject(lineNames.Select(name => new JProperty(name, line[name]!.DeepClone())))));
        return result;
    }
    private static void NoCache(Reply reply)
    {
        Assert.Contains("no-store", string.Join(",", reply.Headers["Cache-Control"]));
        Assert.Contains("no-cache", string.Join(",", reply.Headers["Pragma"]));
        Assert.Equal("0", Assert.Single(reply.Headers["Expires"]));
    }
    private static JToken Success(Reply reply)
    {
        Assert.True(reply.Status == 200, "Expected 200; got " + reply.Status + ": " + reply.Body);
        Assert.Null(reply.Json["error"]);
        NoCache(reply);
        return reply.Json["data"]!;
    }
    private static JObject SuccessEnvelope(Reply reply) { Success(reply); return reply.Json; }
    private static void Error(Reply reply, int status, string code, bool cache = true)
    {
        Assert.True(reply.Status == status, "Expected " + status + "; got " + reply.Status + ": " + reply.Body);
        Assert.Equal(code, (string?)reply.Json["error"]!["code"]);
        Assert.Null(reply.Json["data"]);
        if (cache) NoCache(reply);
    }
    private static void AssertStatus(Reply reply, Intent intent, string epoch, string state, JObject? outcome)
    {
        var value = Success(reply);
        Assert.Equal(new[] { "deploymentEpoch", "method", "operationId", "operationKind", "outcome", "requestFingerprint", "requestTargetId", "routeScope", "state" }, Names(value));
        Assert.Equal(epoch, (string?)value["deploymentEpoch"]);
        Assert.Equal(intent.Key.ToString("D"), (string?)value["operationId"]);
        Assert.Equal(intent.Kind, (string?)value["operationKind"]);
        Assert.Equal(intent.Method.Method, (string?)value["method"]);
        Assert.Equal(intent.Scope, (string?)value["routeScope"]);
        Assert.Equal(intent.Target, (string?)value["requestTargetId"]);
        Assert.Equal(intent.Fingerprint, (string?)value["requestFingerprint"]);
        Assert.Equal(state, (string?)value["state"]);
        if (outcome == null) Assert.Equal(JTokenType.Null, value["outcome"]!.Type);
        else Assert.True(JToken.DeepEquals(outcome, value["outcome"]));
    }
    private static byte[] HashBytes(string hex) => Enumerable.Range(0, 32).Select(index => byte.Parse(hex.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();

    private sealed class Intent
    {
        public Intent(string kind, JObject body, string? target = null, Guid? key = null)
        {
            Kind = kind; Body = (JObject)body.DeepClone(); Target = target; Key = key ?? Guid.NewGuid();
            var sales = kind.StartsWith("Sales.", StringComparison.Ordinal);
            var root = sales ? "sales-receipts" : "goods-receipts";
            var suffix = kind.EndsWith(".Update", StringComparison.Ordinal) ? "/draft" : "/post";
            Method = kind.EndsWith(".Update", StringComparison.Ordinal) ? HttpMethod.Put : HttpMethod.Post;
            Path = target == null ? root : root + "/" + target + suffix;
            Scope = "/api/v1/" + root + (target == null ? "" : (sales ? "/{saleId}" : "/{receiptId}") + suffix);
        }
        public Guid Key { get; }
        public string Kind { get; }
        public JObject Body { get; }
        public string? Target { get; }
        public HttpMethod Method { get; }
        public string Path { get; }
        public string Scope { get; }
        public string Fingerprint
        {
            get
            {
                // Independent small fixture oracle; never invokes the production canonicalizer.
                // Normalize the bounded fixture decimals independently of output display scale.
                var body = (JObject)Body.DeepClone();
                if (!Kind.EndsWith(".Post", StringComparison.Ordinal) && body["note"] == null) body["note"] = JValue.CreateNull();
                if (body["lines"] is JArray lines)
                    foreach (var line in lines)
                        foreach (var field in new[] { "qty", "unitPrice", "quantity", "unitCost" })
                            if (line[field] != null)
                                line[field] = decimal.Parse((string)line[field]!, CultureInfo.InvariantCulture).ToString("0.0000", CultureInfo.InvariantCulture);
                var canonical = Sort(new JObject { ["contract"] = "web4-v1", ["operationKind"] = Kind, ["method"] = Method.Method,
                    ["routeScope"] = Scope, ["requestTargetId"] = Target == null ? JValue.CreateNull() : new JValue(Target), ["body"] = body }).ToString(Formatting.None);
                using var sha = SHA256.Create();
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
        private static JToken Sort(JToken value)
        {
            if (value is JObject obj) return new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, Sort(p.Value))));
            if (value is JArray array) return new JArray(array.Select(Sort));
            return value.DeepClone();
        }
    }
    private sealed class Completed
    {
        public Completed(Intent intent, Reply reply) { Intent = intent; Reply = reply; Result = Success(reply); }
        public Intent Intent { get; }
        public Reply Reply { get; }
        public JToken Result { get; }
    }
    private static async Task Owned(Func<Data, Task> body)
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? failure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().MigrateWeb(database.ConnectionString).Successful);
            using var http = await HttpScenario.Create(database.ConnectionString, web4Enabled: true);
            await body(await Data.Seed(http));
        }
        catch (Exception error) { failure = error; throw; }
        finally { try { await database.DisposeAsync(); } catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); } }
    }
    private sealed class Data
    {
        public HttpScenario Http { get; private set; } = null!;
        public string Epoch { get; private set; } = "";
        public int Customer { get; private set; }
        public int Warehouse { get; private set; }
        public int Employee { get; private set; }
        public int Product { get; private set; }
        public int OtherProduct { get; private set; }
        public static async Task<Data> Seed(HttpScenario http) => new Data
        {
            Http = http,
            Customer = await http.Admin.QuerySingleAsync<int>("INSERT dbo.Customers(Code,Name) OUTPUT INSERTED.CustomerId VALUES(N'C',N'Customer');"),
            Warehouse = await http.Admin.QuerySingleAsync<int>("INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'W',N'Warehouse');"),
            Employee = await http.Admin.QuerySingleAsync<int>("INSERT dbo.Employees(Code,Name) OUTPUT INSERTED.EmployeeId VALUES(N'E',N'Employee');"),
            Product = await http.Admin.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P',N'Product',N'EA');"),
            OtherProduct = await http.Admin.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'OTHER',N'Other Product',N'EA');")
        };
        public async Task GrantMutations(int actor)
        {
            foreach (var permission in new[] { "Sales.Receipts.Write", "Sales.Receipts.Post", "Inventory.GoodsReceipts.Write", "Inventory.GoodsReceipts.Post" })
                await Http.Admin.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@actor,@permission,1);", new { actor, permission });
        }
        public async Task Context(string access)
        {
            var context = Success(await Http.Get("operations/context", access));
            Assert.Equal(new[] { "actorUserId", "contractVersion", "deploymentEpoch" }, Names(context));
            Assert.Equal(Http.Alice, (int)context["actorUserId"]!);
            Assert.Equal("web4-v1", (string?)context["contractVersion"]);
            Epoch = (string)context["deploymentEpoch"]!;
            Assert.Equal((await Http.Admin.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;")).ToString("D"), Epoch);
        }
        public JObject SaleLine(int product, string quantity, string price, bool gift) => new JObject { ["saleLineId"] = "0", ["productId"] = product, ["qty"] = quantity, ["unitPrice"] = price, ["isGift"] = gift };
        public JObject Sale() => new JObject { ["saleDate"] = "2024-02-29", ["customerId"] = Customer, ["warehouseId"] = Warehouse, ["employeeId"] = Employee,
            ["note"] = "Bán hàng — hàng tặng", ["lines"] = new JArray(SaleLine(Product, "3.0000", "25.0000", false), SaleLine(Product, "2.0000", "25.0000", true), SaleLine(Product, "1.0000", "0.0000", false)) };
        public JObject ReceiptLine(int product, string quantity, string cost) => new JObject { ["productId"] = product, ["quantity"] = quantity, ["unitCost"] = cost };
        public JObject Receipt() => new JObject { ["receiptDate"] = "2024-02-29", ["warehouseId"] = Warehouse, ["employeeId"] = Employee,
            ["note"] = "Nhập kho", ["lines"] = new JArray(ReceiptLine(Product, "10.0000", "12.0000")) };
        public Task<Reply> Send(Intent intent, string? access, string? epoch = null) => Http.Request(intent.Method, intent.Path, intent.Body.ToString(Formatting.None), null, access,
            new Dictionary<string,string[]> { ["Idempotency-Key"] = new[] { intent.Key.ToString("D") }, ["X-MyDmsVn-Deployment-Epoch"] = new[] { epoch ?? Epoch } });
        public Task<Reply> Status(Intent intent, string? access, string? epoch = null, string? fingerprint = null) => Http.Request(HttpMethod.Get, "operations/" + intent.Key.ToString("D"), null, null, access,
            new Dictionary<string,string[]> { ["X-MyDmsVn-Deployment-Epoch"] = new[] { epoch ?? Epoch }, ["X-MyDmsVn-Operation-Fingerprint"] = new[] { fingerprint ?? intent.Fingerprint } });
        public async Task<Completed> Execute(Intent intent, string access, bool concurrent = false)
        {
            if (!concurrent) return new Completed(intent, await Send(intent, access));
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<Reply> Dispatch() { await gate.Task; return await Send(intent, access); }
            var first = Dispatch(); var second = Dispatch();
            gate.SetResult(true);
            // WhenAll drains BOTH real HTTP workers, including the failed sibling, before
            // assertions or fixture disposal. Request/SQL timeouts remain the existing finite ones.
            var replies = await Task.WhenAll(first, second);
            Success(replies[0]); Success(replies[1]);
            Assert.True(JToken.DeepEquals(replies[0].Json, replies[1].Json));
            return new Completed(intent, replies[0]);
        }
        public async Task Oracle()
        {
            Assert.Equal(1, await Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE Status='Posted' AND CreatedByUserId=@actor AND UpdatedByUserId=@actor AND PostedByUserId=@actor;", new { actor = Http.Alice }));
            Assert.Equal(1, await Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceipts WHERE Status='Posted' AND CreatedByUserId=@actor AND UpdatedByUserId=@actor AND PostedByUserId=@actor;", new { actor = Http.Alice }));
            Assert.Equal(2, await Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.GoodsReceiptLines;"));
            Assert.Equal(4, await Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceiptLines;"));
            Assert.Equal(new[] { 3m, 2m, 1m, 1m }, (await Http.Admin.QueryAsync<decimal>("SELECT Qty FROM dbo.SalesReceiptLines ORDER BY [LineNo];")).ToArray());
            Assert.Equal(new[] { 45m, 30m, 15m, 2.5m }, (await Http.Admin.QueryAsync<decimal>("SELECT CostAmount FROM dbo.SalesReceiptLines ORDER BY [LineNo];")).ToArray());
            Assert.Equal(new[] { 15m, 15m, 15m, 2.5m }, (await Http.Admin.QueryAsync<decimal>("SELECT PostedUnitCost FROM dbo.SalesReceiptLines ORDER BY [LineNo];")).ToArray());
            Assert.Equal(85m, await Http.Admin.QuerySingleAsync<decimal>("SELECT SUM(LineAmount) FROM dbo.SalesReceiptLines;"));
            Assert.Equal(-7.5m, await Http.Admin.QuerySingleAsync<decimal>("SELECT SUM(LineAmount-CostAmount) FROM dbo.SalesReceiptLines;"));
            Assert.Equal(0m, await Http.Admin.QuerySingleAsync<decimal>("SELECT LineAmount FROM dbo.SalesReceiptLines WHERE IsGift=1;"));
            Assert.Equal(25m, await Http.Admin.QuerySingleAsync<decimal>("SELECT UnitPrice FROM dbo.SalesReceiptLines WHERE IsGift=1;"));
            Assert.Equal(6, await Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
            Assert.Equal(6, await Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries;"));
            Assert.Equal(2, await Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId JOIN dbo.GoodsReceiptLines r ON r.ReceiptId=l.DocumentId AND r.ReceiptLineId=l.DocumentLineId WHERE l.DocumentType='GoodsReceipt' AND l.QuantityChange=r.Quantity AND v.ValueChange=r.Quantity*r.UnitCost;"));
            Assert.Equal(4, await Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId JOIN dbo.SalesReceiptLines s ON s.SaleId=l.DocumentId AND s.SaleLineId=l.DocumentLineId WHERE l.DocumentType='SalesReceipt' AND l.QuantityChange=-s.Qty AND v.ValueChange=-s.CostAmount;"));
            Assert.Equal(2, await Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockBalances;"));
            Assert.Equal(14m, await Http.Admin.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances WHERE ProductId=@product;", new { product = Product }));
            Assert.Equal(210m, await Http.Admin.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances WHERE ProductId=@product;", new { product = Product }));
            Assert.Equal(3m, await Http.Admin.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances WHERE ProductId=@product;", new { product = OtherProduct }));
            Assert.Equal(7.5m, await Http.Admin.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances WHERE ProductId=@product;", new { product = OtherProduct }));
        }
        public async Task<string> State()
        {
            var results = new List<string>();
            foreach (var source in new[]
            {
                "SalesReceipts ORDER BY SaleId", "SalesReceiptLines ORDER BY SaleLineId", "GoodsReceipts ORDER BY ReceiptId",
                "GoodsReceiptLines ORDER BY ReceiptLineId", "StockBalances ORDER BY WarehouseId,ProductId", "StockLedger ORDER BY LedgerId",
                "StockValuationEntries ORDER BY LedgerId", "WebOperationJournal ORDER BY ActorUserId,OperationId"
            }) results.Add(await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo." + source + " FOR JSON PATH);"));
            results.Add(await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT name,CONVERT(decimal(38,0),current_value) AS value FROM sys.sequences WHERE object_id IN (OBJECT_ID(N'dbo.SalesReceiptNumberSequence'),OBJECT_ID(N'dbo.GoodsReceiptNumberSequence')) ORDER BY name FOR JSON PATH);"));
            return string.Join("|", results);
        }
    }
}
