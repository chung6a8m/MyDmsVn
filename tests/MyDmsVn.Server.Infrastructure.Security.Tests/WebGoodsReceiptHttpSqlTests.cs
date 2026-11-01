using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using HttpScenario = MyDmsVn.Server.Infrastructure.IntegrationTests.WebAuthHttpSqlTests.Scenario;
using Reply = MyDmsVn.Server.Infrastructure.IntegrationTests.WebAuthHttpSqlTests.Reply;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Production OWIN, authentication, Application and SQL with the existing restricted Web
// database user. Administrative SQL only arranges fixtures and independently observes rows.
public sealed class WebGoodsReceiptHttpSqlTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Restricted_HTTP_create_replace_post_and_queries_match_physical_SQL_and_Local() => Owned(async data =>
    {
        await data.GrantAll();
        var access = (await data.Http.Login("alice")).Access;
        var created = Success(await data.Send(HttpMethod.Post, "goods-receipts", data.Body(), access));
        var id = (string)created["receiptId"]!;
        Assert.Equal(JTokenType.String, created["receiptId"]!.Type);
        Assert.Equal(JTokenType.Null, created["warehouseDisplayName"]!.Type);
        var oldLine = (string)created["lines"]![0]!["receiptLineId"]!;
        var save = Save(created);
        save["lines"] = new JArray(data.Line(data.OtherProduct, "2.0000", "3.0000"), data.Line(data.Product, "3.0000", "11.0000"));
        save["note"] = "Đã thay dòng";
        var updated = Success(await data.Send(HttpMethod.Put, "goods-receipts/" + id + "/draft", save, access));
        Assert.NotEqual((string?)created["version"], (string?)updated["version"]);
        Assert.DoesNotContain(updated["lines"]!, line => (string?)line["receiptLineId"] == oldLine);
        Assert.Equal(new[] { data.OtherProduct, data.Product }, updated["lines"]!.Select(line => (int)line["productId"]!).ToArray());
        Assert.Equal(new[] { 1, 2 }, updated["lines"]!.Select(line => (int)line["lineNumber"]!).ToArray());
        Assert.Equal(0, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
        Assert.Equal(0, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockBalances;"));
        Assert.Equal(new[] { data.OtherProduct, data.Product }, (await data.Http.Admin.QueryAsync<int>("SELECT ProductId FROM dbo.GoodsReceiptLines ORDER BY [LineNo];")).ToArray());
        var before = await data.State();
        Error(await data.Send(HttpMethod.Put, "goods-receipts/" + id + "/draft", Save(created), access), 409, "GoodsReceipt.ConcurrencyConflict");
        Error(await data.Send(HttpMethod.Post, "goods-receipts/" + id + "/post", new JObject { ["expectedVersion"] = created["version"]!.DeepClone() }, access), 409, "GoodsReceipt.ConcurrencyConflict");
        Assert.Equal(before, await data.State());
        var ack = Success(await data.Send(HttpMethod.Post, "goods-receipts/" + id + "/post", new JObject { ["expectedVersion"] = updated["version"]!.DeepClone() }, access));
        Assert.Equal(new[] { "postedAtUtc", "receiptId", "status" }, ((JObject)ack).Properties().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal("Posted", (string?)ack["status"]);
        Assert.Equal(2, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId JOIN dbo.GoodsReceiptLines r ON r.ReceiptLineId=l.DocumentLineId AND r.ReceiptId=l.DocumentId WHERE l.DocumentType='GoodsReceipt' AND l.QuantityChange=r.Quantity AND v.ValueChange=r.Quantity*r.UnitCost;"));
        Assert.Equal(5m, await data.Http.Admin.QuerySingleAsync<decimal>("SELECT SUM(Quantity) FROM dbo.StockBalances;"));
        Assert.Equal(39m, await data.Http.Admin.QuerySingleAsync<decimal>("SELECT SUM(InventoryValue) FROM dbo.StockBalances;"));
        Assert.Equal(data.Http.Alice, await data.Http.Admin.QuerySingleAsync<int>("SELECT PostedByUserId FROM dbo.GoodsReceipts;"));
        var detail = Success(await data.Send(HttpMethod.Get, "goods-receipts/" + id, null, access));
        Assert.Equal("W - Warehouse", (string?)detail["warehouseDisplayName"]);
        var list = Success(await data.Send(HttpMethod.Get, "goods-receipts?pageNumber=1&pageSize=1&status=Posted", null, access));
        Assert.Equal("1", (string?)list["totalCount"]);
        Assert.True(JToken.DeepEquals(detail, list["items"]![0]));
        var balances = Success(await data.Send(HttpMethod.Get, "inventory/balances?pageNumber=1&pageSize=200", null, access));
        Assert.Equal(39m, balances["items"]!.Sum(row => Number(row["inventoryValue"]!)));
        var card = Success(await data.Send(HttpMethod.Get, data.CardPath, null, access));
        var movement = Assert.Single(card["items"]!);
        Assert.Equal(id, (string?)movement["documentId"]);
        Assert.Equal((string?)detail["receiptNumber"], (string?)movement["documentNumber"]);
        Assert.Equal(3m, Number(movement["quantityChange"]!));
        Assert.Equal(33m, Number(movement["valueChange"]!));
        var posted = await data.State();
        Error(await data.Send(HttpMethod.Post, "goods-receipts/" + id + "/post", new JObject { ["expectedVersion"] = detail["version"]!.DeepClone() }, access), 409, "GoodsReceipt.AlreadyPosted");
        Assert.Equal(posted, await data.State());

        var services = new ServiceCollection();
        services.AddServerApplication(); services.AddSqlPersistence(data.Http.Admin.ConnectionString); services.AddLocalDesktopAdapter();
        using var local = services.BuildServiceProvider();
        Assert.True((await local.GetRequiredService<IIdentityApiClient>().LoginAsync(new LoginRequest("alice", HttpScenario.Password), CancellationToken.None)).IsSuccess);
        var localDetail = await local.GetRequiredService<IGoodsReceiptApiClient>().GetAsync(long.Parse(id, CultureInfo.InvariantCulture), CancellationToken.None);
        Assert.True(localDetail.IsSuccess);
        Assert.Equal(localDetail.Data!.Version, (string?)detail["version"]);
        Assert.Equal(localDetail.Data.Lines.Select(line => line.Id.ToString(CultureInfo.InvariantCulture)), detail["lines"]!.Select(line => (string)line["receiptLineId"]!));
        var localCard = await local.GetRequiredService<IInventoryApiClient>().GetStockCardAsync(new StockCardQuery(data.Warehouse, data.Product, null, null, 1, 200), CancellationToken.None);
        Assert.True(localCard.IsSuccess);
        Assert.Equal(Assert.Single(localCard.Data!.Items).ValueChange, Number(movement["valueChange"]!));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Independent_permissions_revocation_and_Post_ack_without_Read_preserve_SQL() => Owned(async data =>
    {
        await data.Grant(data.Http.Alice, "Inventory.GoodsReceipts.Write");
        await data.Grant(data.Http.Bob, "Inventory.StockCard.Read");
        var alice = (await data.Http.Login("alice")).Access;
        var bob = (await data.Http.Login("bob")).Access;
        var created = Success(await data.Send(HttpMethod.Post, "goods-receipts", data.Body(), alice));
        var path = "goods-receipts/" + (string)created["receiptId"]!;
        Error(await data.Send(HttpMethod.Get, path, null, alice), 403, "Auth.Forbidden");
        var before = await data.State();
        Error(await data.Send(HttpMethod.Post, "goods-receipts", data.Body(), bob), 403, "Auth.Forbidden");
        Error(await data.Send(HttpMethod.Put, path + "/draft", Save(created), bob), 403, "Auth.Forbidden");
        Error(await data.Send(HttpMethod.Post, path + "/post", new JObject { ["expectedVersion"] = created["version"]!.DeepClone() }, alice), 403, "Auth.Forbidden");
        Assert.Equal(before, await data.State());
        await data.Grant(data.Http.Alice, "Inventory.GoodsReceipts.Post");
        var ack = Success(await data.Send(HttpMethod.Post, path + "/post", new JObject { ["expectedVersion"] = created["version"]!.DeepClone() }, alice));
        Assert.Null(ack["version"]);
        Error(await data.Send(HttpMethod.Get, path, null, alice), 403, "Auth.Forbidden");
        Assert.Single(Success(await data.Send(HttpMethod.Get, data.CardPath, null, bob))["items"]!);
        Error(await data.Send(HttpMethod.Get, path, null, bob), 403, "Auth.Forbidden");
        Error(await data.Send(HttpMethod.Get, "inventory/balances?pageNumber=1&pageSize=200", null, bob), 403, "Auth.Forbidden");
        await data.Http.Admin.ExecuteAsync("UPDATE dbo.UserPermissions SET Granted=0 WHERE UserId=@actor;", new { actor = data.Http.Alice });
        var posted = await data.State();
        Error(await data.Send(HttpMethod.Post, "goods-receipts", data.Body(), alice), 403, "Auth.Forbidden");
        Assert.Equal(posted, await data.State());
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Duplicate_products_and_bad_precision_leave_SQL_unchanged_and_200_unique_lines_roundtrip() => Owned(async data =>
    {
        await data.GrantAll();
        var access = (await data.Http.Login("alice")).Access;
        var before = await data.State();
        var duplicate = data.Body(); duplicate["lines"] = new JArray(data.Line(data.Product, "1", "1"), data.Line(data.Product, "2", "2"));
        var invalid = await data.Send(HttpMethod.Post, "goods-receipts", duplicate, access);
        Error(invalid, 400, "ValidationError");
        Assert.Contains(invalid.Json["error"]!["details"]!, detail => (string?)detail["field"] == "lines[1].productId" && (string?)detail["code"] == "GoodsReceipt.DuplicateProduct");
        var precision = data.Body(); precision["lines"]![0]!["quantity"] = "0.00001";
        Error(await data.Send(HttpMethod.Post, "goods-receipts", precision, access), 400, "ValidationError");
        var empty = data.Body(); empty["lines"] = new JArray();
        Error(await data.Send(HttpMethod.Post, "goods-receipts", empty, access), 400, "ValidationError");
        Assert.Equal(before, await data.State());
        await data.Http.Admin.ExecuteAsync("DBCC CHECKIDENT ('dbo.GoodsReceipts', RESEED, 9007199254740993) WITH NO_INFOMSGS; DBCC CHECKIDENT ('dbo.GoodsReceiptLines', RESEED, 9007199254740993) WITH NO_INFOMSGS;");
        var lines = new JArray();
        for (var i = 0; i < 200; i++)
        {
            var product = await data.Http.Admin.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(@code,N'Batch',N'EA');", new { code = "B" + i.ToString(CultureInfo.InvariantCulture) });
            lines.Add(data.Line(product, "0.0001", "999999999999999.9999"));
        }
        var oversized = data.Body();
        oversized["lines"] = new JArray(lines.Select(line => line.DeepClone()).Concat(new[] { data.Line(data.OtherProduct, "1", "1") }));
        Error(await data.Send(HttpMethod.Post, "goods-receipts", oversized, access), 400, "ValidationError");
        Assert.Equal(before, await data.State());
        var body = data.Body(); body["lines"] = lines;
        var created = Success(await data.Send(HttpMethod.Post, "goods-receipts", body, access));
        Assert.Equal("9007199254740993", (string?)created["receiptId"]);
        Assert.Equal(200, created["lines"]!.Count());
        Assert.Equal(Enumerable.Range(1, 200), created["lines"]!.Select(line => (int)line["lineNumber"]!));
        Assert.All(created["lines"]!, line => { Assert.True(long.Parse((string)line["receiptLineId"]!, CultureInfo.InvariantCulture) > 9007199254740992L); Assert.Equal(999999999999999.9999m, Number(line["unitCost"]!)); });
        Assert.Equal(200, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.GoodsReceiptLines;"));
        Assert.Equal(0, await data.Http.Admin.QuerySingleAsync<int>("SELECT (SELECT COUNT(*) FROM dbo.StockBalances)+(SELECT COUNT(*) FROM dbo.StockLedger)+(SELECT COUNT(*) FROM dbo.StockValuationEntries);"));
        var createIds = created["lines"]!.Select(line => (string)line["receiptLineId"]!).ToArray();
        var replacement = Save(created);
        replacement["lines"] = new JArray(created["lines"]!.Reverse().Select(line => data.Line((int)line["productId"]!, "0.0002", "999999999999999.9999")));
        var updated = Success(await data.Send(HttpMethod.Put, "goods-receipts/" + (string)created["receiptId"]! + "/draft", replacement, access));
        Assert.NotEqual((string?)created["version"], (string?)updated["version"]);
        Assert.Equal(200, updated["lines"]!.Count());
        Assert.Equal(Enumerable.Range(1, 200), updated["lines"]!.Select(line => (int)line["lineNumber"]!));
        Assert.Equal(created["lines"]!.Reverse().Select(line => (int)line["productId"]!), updated["lines"]!.Select(line => (int)line["productId"]!));
        Assert.All(updated["lines"]!, line => Assert.DoesNotContain((string)line["receiptLineId"]!, createIds));
        Assert.Equal(updated["lines"]!.Select(line => (int)line["productId"]!), await data.Http.Admin.QueryAsync<int>("SELECT ProductId FROM dbo.GoodsReceiptLines ORDER BY [LineNo];"));
        Assert.Equal(200, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.GoodsReceiptLines WHERE Quantity=0.0002 AND UnitCost=999999999999999.9999;"));
        Assert.Equal(Convert.FromBase64String((string)updated["version"]!), await data.Http.Admin.QuerySingleAsync<byte[]>("SELECT RowVersion FROM dbo.GoodsReceipts;"));
        Assert.Equal(1, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE Status='Draft' AND CreatedByUserId=@actor AND UpdatedByUserId=@actor AND PostedAtUtc IS NULL AND PostedByUserId IS NULL;", new { actor = data.Http.Alice }));
        Assert.Equal(0, await data.Http.Admin.QuerySingleAsync<int>("SELECT (SELECT COUNT(*) FROM dbo.StockBalances)+(SELECT COUNT(*) FROM dbo.StockLedger)+(SELECT COUNT(*) FROM dbo.StockValuationEntries);"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Missing_baseline_receipt_grants_fail_Create_and_minimal_grants_restore_it() => Owned(async data =>
    {
        await data.GrantAll();
        var access = (await data.Http.Login("alice")).Access;
        // Reproduce the B01 source gap using the exact pre-WEB3 object permissions.
        await data.Http.Admin.ExecuteAsync("REVOKE SELECT,INSERT,UPDATE ON dbo.GoodsReceipts FROM MyDmsVnWebRuntimeRole; REVOKE SELECT,INSERT,DELETE ON dbo.GoodsReceiptLines FROM MyDmsVnWebRuntimeRole; REVOKE UPDATE ON dbo.GoodsReceiptNumberSequence FROM MyDmsVnWebRuntimeRole;");
        var before = await data.State();
        var denied = await data.Send(HttpMethod.Post, "goods-receipts", data.Body(), access);
        Assert.Equal(500, denied.Status);
        Assert.DoesNotContain("permission was denied", denied.Body.ToLowerInvariant());
        Assert.Equal(before, await data.State());
        await data.Http.Admin.ExecuteAsync("GRANT SELECT,INSERT,UPDATE ON dbo.GoodsReceipts TO MyDmsVnWebRuntimeRole; GRANT SELECT,INSERT,DELETE ON dbo.GoodsReceiptLines TO MyDmsVnWebRuntimeRole; GRANT UPDATE ON dbo.GoodsReceiptNumberSequence TO MyDmsVnWebRuntimeRole;");
        Success(await data.Send(HttpMethod.Post, "goods-receipts", data.Body(), access));
        Assert.Equal(1, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.GoodsReceipts;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Failed_Post_valuation_write_rolls_back_header_ledger_and_balances() => Owned(async data =>
    {
        await data.GrantAll();
        var access = (await data.Http.Login("alice")).Access;
        var created = Success(await data.Send(HttpMethod.Post, "goods-receipts", data.Body(), access));
        var before = await data.State();
        await data.Http.Admin.ExecuteAsync("DENY INSERT ON dbo.StockValuationEntries TO WebHttpProbe;");
        var failure = await data.Send(HttpMethod.Post, "goods-receipts/" + (string)created["receiptId"]! + "/post", new JObject { ["expectedVersion"] = created["version"]!.DeepClone() }, access);
        Assert.Equal(500, failure.Status);
        Assert.DoesNotContain("StockValuationEntries", failure.Body);
        Assert.Equal(before, await data.State());
    });

    private static decimal Number(JToken value) { Assert.Equal(JTokenType.String, value.Type); return decimal.Parse((string)value!, CultureInfo.InvariantCulture); }
    private static JToken Success(Reply reply) { Assert.True(reply.Status == 200, "Expected 200; got " + reply.Status + ": " + reply.Body); return reply.Json["data"]!; }
    private static void Error(Reply reply, int status, string code) { Assert.Equal(status, reply.Status); Assert.Equal(code, (string?)reply.Json["error"]!["code"]); }
    private static JObject Save(JToken detail)
    {
        var body = new JObject();
        foreach (var name in new[] { "receiptDate", "warehouseId", "employeeId", "note" }) body[name] = detail[name]!.DeepClone();
        body["expectedVersion"] = detail["version"]!.DeepClone();
        body["lines"] = new JArray(detail["lines"]!.Select(line => new JObject { ["productId"] = line["productId"]!.DeepClone(), ["quantity"] = line["quantity"]!.DeepClone(), ["unitCost"] = line["unitCost"]!.DeepClone() }));
        return body;
    }
    private static async Task Owned(Func<Data, Task> body)
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? failure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().MigrateWeb(database.ConnectionString).Successful);
            using var http = await HttpScenario.Create(database.ConnectionString);
            await body(await Data.Seed(http));
        }
        catch (Exception error) { failure = error; throw; }
        finally { try { await database.DisposeAsync(); } catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); } }
    }
    private sealed class Data
    {
        public HttpScenario Http { get; private set; } = null!;
        public int Warehouse { get; private set; }
        public int Employee { get; private set; }
        public int Product { get; private set; }
        public int OtherProduct { get; private set; }
        public static async Task<Data> Seed(HttpScenario http) => new Data
        {
            Http = http,
            Warehouse = await http.Admin.QuerySingleAsync<int>("INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'W',N'Warehouse');"),
            Employee = await http.Admin.QuerySingleAsync<int>("INSERT dbo.Employees(Code,Name) OUTPUT INSERTED.EmployeeId VALUES(N'E',N'Employee');"),
            Product = await http.Admin.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'P',N'Product',N'EA');"),
            OtherProduct = await http.Admin.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'O',N'Other',N'EA');"),
        };
        public string CardPath => "inventory/stock-card?pageNumber=1&pageSize=200&warehouseId=" + Warehouse.ToString(CultureInfo.InvariantCulture) + "&productId=" + Product.ToString(CultureInfo.InvariantCulture);
        public async Task Grant(int actor, params string[] keys) { foreach (var key in keys) await Http.Admin.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@actor,@key,1);", new { actor, key }); }
        public Task GrantAll() => Grant(Http.Alice, "Inventory.GoodsReceipts.Read", "Inventory.GoodsReceipts.Write", "Inventory.GoodsReceipts.Post", "Inventory.Balances.Read", "Inventory.StockCard.Read");
        public JObject Line(int product, string quantity, string cost) => new JObject { ["productId"] = product, ["quantity"] = quantity, ["unitCost"] = cost };
        public JObject Body() => new JObject { ["receiptDate"] = "2024-02-29", ["warehouseId"] = Warehouse, ["employeeId"] = Employee, ["note"] = "Nhập thử", ["lines"] = new JArray(Line(Product, "1.0000", "10.0000")) };
        public Task<Reply> Send(HttpMethod method, string path, JToken? body, string? access) => Http.Request(method, path, body?.ToString(Formatting.None), null, access);
        public async Task<string> State() => string.Join("|", new[]
        {
            await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.GoodsReceipts ORDER BY ReceiptId FOR JSON PATH);"),
            await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.GoodsReceiptLines ORDER BY ReceiptLineId FOR JSON PATH);"),
            await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.StockBalances ORDER BY WarehouseId,ProductId FOR JSON PATH);"),
            await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.StockLedger ORDER BY LedgerId FOR JSON PATH);"),
            await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.StockValuationEntries ORDER BY LedgerId FOR JSON PATH);"),
        });
    }
}
