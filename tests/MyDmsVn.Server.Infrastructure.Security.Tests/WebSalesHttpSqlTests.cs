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

// Reuses WEB2-04's real login/OWIN/restricted database-user harness. Administrative SQL
// arranges references and independently observes results; every HTTP business call is production.
public sealed class WebSalesHttpSqlTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Actual_login_create_update_post_preserves_duplicate_gift_prices_costs_and_Local_parity() => Owned(async data =>
    {
        await data.GrantAll();
        var access = (await data.Http.Login("alice")).Access;
        var created = Success(await data.Send(HttpMethod.Post, "sales-receipts", data.Body(), access));
        var id = (string)created["saleId"]!;
        var firstLines = (JArray)created["lines"]!;
        Assert.Equal(3, firstLines.Count);
        Assert.Equal(3, firstLines.Select(line => (string)line["saleLineId"]!).Distinct().Count());
        Assert.All(firstLines, line => Assert.Equal(data.Product, (int)line["productId"]!));
        Assert.Equal(75m, Number(created["totalAmount"]!));
        Assert.Equal("2024-02-29", (string?)created["saleDate"]);
        Assert.Equal("Draft", (string?)created["status"]);
        Assert.Null(created["totalCost"]);
        var update = Save(created);
        update["note"] = "Đã cập nhật — hàng tặng giữ giá";
        ((JArray)update["lines"]!)[0]!["unitPrice"] = "26.0000";
        var updated = Success(await data.Send(HttpMethod.Put, "sales-receipts/" + id + "/draft", update, access));
        Assert.Equal(firstLines.Select(line => (string)line["saleLineId"]!), updated["lines"]!.Select(line => (string)line["saleLineId"]!));
        Assert.Equal(78m, Number(updated["totalAmount"]!));
        Assert.NotEqual((string?)created["version"], (string?)updated["version"]);
        var beforeConflict = await data.State();
        Error(await data.Send(HttpMethod.Put, "sales-receipts/" + id + "/draft", Save(created), access), 409, "SalesReceipt.ConcurrencyConflict");
        Assert.Equal(beforeConflict, await data.State());
        await data.Stock(20m, 300m);
        var ack = Success(await data.Send(HttpMethod.Post, "sales-receipts/" + id + "/post", new JObject { ["expectedVersion"] = updated["version"]!.DeepClone() }, access));
        Assert.Equal(new[] { "postedAtUtc", "saleId", "status", "version" }, ((JObject)ack).Properties().Select(property => property.Name).OrderBy(name => name).ToArray());
        var detail = Success(await data.Send(HttpMethod.Get, "sales-receipts/" + id, null, access));
        Assert.Equal("Posted", (string?)detail["status"]);
        Assert.Equal(90m, Number(detail["totalCost"]!));
        Assert.Equal(-12m, Number(detail["grossProfit"]!));
        Assert.All(detail["lines"]!, line => Assert.Equal(15m, Number(line["postedUnitCost"]!)));
        Assert.Equal(new[] { 45m, 30m, 15m }, detail["lines"]!.Select(line => Number(line["costAmount"]!)).ToArray());
        Assert.Equal(25m, Number(detail["lines"]![1]!["unitPrice"]!));
        Assert.Equal(0m, Number(detail["lines"]![1]!["lineAmount"]!));
        Assert.Equal(14m, await data.Http.Admin.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances;"));
        Assert.Equal(210m, await data.Http.Admin.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances;"));
        Assert.Equal(3, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId JOIN dbo.SalesReceiptLines s ON s.SaleLineId=l.DocumentLineId AND s.SaleId=l.DocumentId WHERE l.DocumentType='SalesReceipt' AND l.QuantityChange=-s.Qty AND v.ValueChange=-s.CostAmount;"));
        Assert.Equal(data.Http.Alice, await data.Http.Admin.QuerySingleAsync<int>("SELECT PostedByUserId FROM dbo.SalesReceipts;"));
        var postedState = await data.State();
        Error(await data.Send(HttpMethod.Post, "sales-receipts/" + id + "/post", new JObject { ["expectedVersion"] = ack["version"]!.DeepClone() }, access), 409, "SalesReceipt.AlreadyPosted");
        Assert.Equal(postedState, await data.State());

        // Independent production Local adapter/session, authenticating via the same LoginCommand;
        // it is never registered in or used as HTTP request identity.
        var services = new ServiceCollection();
        services.AddServerApplication(); services.AddSqlPersistence(data.Http.Admin.ConnectionString); services.AddLocalDesktopAdapter();
        using var local = services.BuildServiceProvider();
        var login = await local.GetRequiredService<IIdentityApiClient>().LoginAsync(new LoginRequest("alice", HttpScenario.Password), CancellationToken.None);
        Assert.True(login.IsSuccess);
        var localDetail = await local.GetRequiredService<ISalesReceiptApiClient>().GetAsync(long.Parse(id, CultureInfo.InvariantCulture), CancellationToken.None);
        Assert.True(localDetail.IsSuccess);
        Assert.Equal(localDetail.Data!.TotalCost, Number(detail["totalCost"]!));
        Assert.Equal(localDetail.Data.TotalAmount, Number(detail["totalAmount"]!));
        Assert.Equal(localDetail.Data.Lines.Select(line => line.SaleLineId.ToString(CultureInfo.InvariantCulture)), detail["lines"]!.Select(line => (string)line["saleLineId"]!));
        Assert.Equal(localDetail.Data.Lines.Select(line => line.CostAmount!.Value), detail["lines"]!.Select(line => Number(line["costAmount"]!)));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Two_hundred_lines_and_int64_maximum_identities_roundtrip_as_exact_strings() => Owned(async data =>
    {
        await data.GrantAll();
        await data.Http.Admin.ExecuteAsync("DBCC CHECKIDENT ('dbo.SalesReceipts', RESEED, 9223372036854775807) WITH NO_INFOMSGS; DBCC CHECKIDENT ('dbo.SalesReceiptLines', RESEED, 9223372036854775608) WITH NO_INFOMSGS;");
        var access = (await data.Http.Login("alice")).Access;
        var body = data.Body();
        body["lines"] = new JArray(Enumerable.Range(0, 200).Select(_ => data.Line("0.0001", "1.0000", false)));
        var detail = Success(await data.Send(HttpMethod.Post, "sales-receipts", body, access));
        Assert.Equal("9223372036854775807", (string?)detail["saleId"]);
        Assert.Equal(200, detail["lines"]!.Count());
        Assert.Equal(Enumerable.Range(1, 200), detail["lines"]!.Select(line => (int)line["lineNo"]!));
        Assert.Equal("9223372036854775807", (string?)detail["lines"]![199]!["saleLineId"]);
        Assert.Equal(200, detail["lines"]!.Select(line => (string)line["saleLineId"]!).Distinct().Count());
        Assert.Equal(0.0200m, Number(detail["totalAmount"]!));
        Assert.All(detail["lines"]!, line =>
        {
            Assert.Equal(JTokenType.String, line["saleLineId"]!.Type);
            Assert.True(long.Parse((string)line["saleLineId"]!, CultureInfo.InvariantCulture) > 9007199254740992L);
            Assert.Equal(0.0001m, Number(line["qty"]!));
        });
        var updated = Success(await data.Send(HttpMethod.Put, "sales-receipts/9223372036854775807/draft", Save(detail), access));
        var reloaded = Success(await data.Send(HttpMethod.Get, "sales-receipts/9223372036854775807", null, access));
        Assert.True(JToken.DeepEquals(updated, reloaded));
        Assert.Equal(200, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceiptLines;"));
        Assert.Equal(0.0200m, await data.Http.Admin.QuerySingleAsync<decimal>("SELECT SUM(LineAmount) FROM dbo.SalesReceiptLines;"));
        var list = Success(await data.Send(HttpMethod.Get, "sales-receipts", null, access));
        Assert.Equal("9223372036854775807", (string?)list["items"]![0]!["saleId"]);
        var maximumGift = Save(reloaded);
        maximumGift["lines"] = new JArray(maximumGift["lines"]![199]!.DeepClone());
        maximumGift["lines"]![0]!["qty"] = "99999999999999.9999";
        maximumGift["lines"]![0]!["unitPrice"] = "999999999999999.9999";
        maximumGift["lines"]![0]!["isGift"] = true;
        var gift = Success(await data.Send(HttpMethod.Put, "sales-receipts/9223372036854775807/draft", maximumGift, access));
        Assert.Equal("9223372036854775807", (string?)gift["lines"]![0]!["saleLineId"]);
        Assert.Equal(99999999999999.9999m, Number(gift["lines"]![0]!["qty"]!));
        Assert.Equal(999999999999999.9999m, Number(gift["lines"]![0]!["unitPrice"]!));
        Assert.Equal(0m, Number(gift["totalAmount"]!));
        Assert.Equal(99999999999999.9999m, await data.Http.Admin.QuerySingleAsync<decimal>("SELECT Qty FROM dbo.SalesReceiptLines;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Write_and_Post_do_not_require_Read_and_Post_ack_commits_before_Get_403() => Owned(async data =>
    {
        await data.Grant(data.Http.Alice, "Sales.Receipts.Write");
        await data.Grant(data.Http.Bob, "Sales.Receipts.Read");
        var alice = (await data.Http.Login("alice")).Access;
        var bob = (await data.Http.Login("bob")).Access;
        var created = Success(await data.Send(HttpMethod.Post, "sales-receipts", data.Body(), alice));
        var id = (string)created["saleId"]!;
        Error(await data.Send(HttpMethod.Get, "sales-receipts/" + id, null, alice), 403, "Auth.Forbidden");
        var before = await data.State();
        Error(await data.Send(HttpMethod.Post, "sales-receipts", data.Body(), bob), 403, "Auth.Forbidden");
        Error(await data.Send(HttpMethod.Put, "sales-receipts/" + id + "/draft", Save(created), bob), 403, "Auth.Forbidden");
        Error(await data.Send(HttpMethod.Post, "sales-receipts/" + id + "/post", new JObject { ["expectedVersion"] = created["version"]!.DeepClone() }, bob), 403, "Auth.Forbidden");
        Assert.Equal(before, await data.State());
        var updated = Success(await data.Send(HttpMethod.Put, "sales-receipts/" + id + "/draft", Save(created), alice));
        var beforeDeniedPost = await data.State();
        Error(await data.Send(HttpMethod.Post, "sales-receipts/" + id + "/post", new JObject { ["expectedVersion"] = updated["version"]!.DeepClone() }, alice), 403, "Auth.Forbidden");
        Assert.Equal(beforeDeniedPost, await data.State());
        await data.Grant(data.Http.Alice, "Sales.Receipts.Post");
        await data.Stock(20m, 300m);
        var ack = Success(await data.Send(HttpMethod.Post, "sales-receipts/" + id + "/post", new JObject { ["expectedVersion"] = updated["version"]!.DeepClone() }, alice));
        Assert.Equal("Posted", (string?)ack["status"]);
        Assert.Null(ack["lines"]);
        Error(await data.Send(HttpMethod.Get, "sales-receipts/" + id, null, alice), 403, "Auth.Forbidden");
        Assert.Equal("Posted", (string?)Success(await data.Send(HttpMethod.Get, "sales-receipts/" + id, null, bob))["status"]);
        Assert.Equal(data.Http.Alice, await data.Http.Admin.QuerySingleAsync<int>("SELECT CreatedByUserId FROM dbo.SalesReceipts;"));
        Assert.Equal(data.Http.Alice, await data.Http.Admin.QuerySingleAsync<int>("SELECT UpdatedByUserId FROM dbo.SalesReceipts;"));
        Assert.Equal(data.Http.Alice, await data.Http.Admin.QuerySingleAsync<int>("SELECT PostedByUserId FROM dbo.SalesReceipts;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Catalog_permissions_are_independent_and_inactive_labels_remain_in_Sales_detail() => Owned(async data =>
    {
        await data.Grant(data.Http.Alice, "Sales.Receipts.Read", "Sales.Receipts.Write");
        var access = (await data.Http.Login("alice")).Access;
        var saved = Success(await data.Send(HttpMethod.Post, "sales-receipts", data.Body(), access));
        foreach (var feature in new[] { "Products", "Customers", "Warehouses", "Employees" })
        {
            var route = "catalog/" + feature.ToLowerInvariant() + "/lookup?limit=25";
            Error(await data.Send(HttpMethod.Get, route, null, access), 403, "Auth.Forbidden");
            await data.Grant(data.Http.Alice, "Catalog." + feature + ".Read");
            var rows = Success(await data.Send(HttpMethod.Get, route, null, access));
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Equal(new[] { "code", "id", "isActive", "name" }, ((JObject)row).Properties().Select(property => property.Name).OrderBy(name => name).ToArray()));
            await data.Http.Admin.ExecuteAsync("UPDATE dbo." + feature + " SET IsActive=0;");
            Assert.Empty(Success(await data.Send(HttpMethod.Get, route, null, access)));
        }
        var detail = Success(await data.Send(HttpMethod.Get, "sales-receipts/" + (string)saved["saleId"]!, null, access));
        foreach (var feature in new[] { "customer", "warehouse", "employee" })
        {
            Assert.False((bool)detail[feature + "IsActive"]!);
            Assert.Contains(" - ", (string)detail[feature + "DisplayName"]!);
        }
        Assert.All(detail["lines"]!, line => { Assert.False((bool)line["productIsActive"]!); Assert.Equal("P - Product", (string?)line["productDisplayName"]); });
        var before = await data.State();
        Error(await data.Send(HttpMethod.Put, "sales-receipts/" + (string)saved["saleId"]! + "/draft", Save(saved), access), 400, "ValidationError");
        Assert.Equal(before, await data.State());
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Product_Any_filter_retains_all_line_totals_and_keysets_are_bound_to_filters() => Owned(async data =>
    {
        await data.GrantAll();
        var access = (await data.Http.Login("alice")).Access;
        var mixed = data.Body();
        mixed["lines"]![2]!["productId"] = data.OtherProduct;
        mixed["lines"]![2]!["unitPrice"] = "10.0000";
        var first = Success(await data.Send(HttpMethod.Post, "sales-receipts", mixed, access));
        var second = Success(await data.Send(HttpMethod.Post, "sales-receipts", mixed, access));
        var other = data.Body();
        foreach (var line in other["lines"]!) line["productId"] = data.OtherProduct;
        Success(await data.Send(HttpMethod.Post, "sales-receipts", other, access));
        var query = "sales-receipts?productId=" + data.Product.ToString(CultureInfo.InvariantCulture) + "&pageSize=1";
        var page = Success(await data.Send(HttpMethod.Get, query, null, access));
        Assert.Single(page["items"]!);
        Assert.Equal((string?)second["saleId"], (string?)page["items"]![0]!["saleId"]);
        Assert.Equal(85m, Number(page["items"]![0]!["totalAmount"]!));
        Assert.Equal(3, (int)page["items"]![0]!["lineCount"]!);
        var cursor = (string)page["nextCursor"]!;
        var next = Success(await data.Send(HttpMethod.Get, query + "&cursor=" + Uri.EscapeDataString(cursor), null, access));
        Assert.Single(next["items"]!);
        Assert.Equal((string?)first["saleId"], (string?)next["items"]![0]!["saleId"]);
        Assert.Null(next["nextCursor"]);
        var previous = Success(await data.Send(HttpMethod.Get, query + "&cursor=" + Uri.EscapeDataString((string)next["previousCursor"]!), null, access));
        Assert.Equal((string?)second["saleId"], (string?)previous["items"]![0]!["saleId"]);
        Error(await data.Send(HttpMethod.Get, "sales-receipts?productId=" + data.OtherProduct.ToString(CultureInfo.InvariantCulture) + "&pageSize=1&cursor=" + Uri.EscapeDataString(cursor), null, access), 400, "ValidationError");
        Error(await data.Send(HttpMethod.Get, "sales-receipts/9223372036854775807", null, access), 404, "SalesReceipt.NotFound");
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Malformed_and_unauthorized_writes_never_change_SQL_state() => Owned(async data =>
    {
        await data.GrantAll();
        var access = (await data.Http.Login("alice")).Access;
        var before = await data.State();
        var bodies = new[]
        {
            new Action<JObject>(body => body["lines"]![0]!["qty"] = 1),
            body => body["lines"]![0]!["saleLineId"] = "1",
            body => body["lines"]![0]!["unitPrice"] = "1e2",
            body => body["saleDate"] = "2024-02-30",
            body => body["actorUserId"] = data.Http.Bob,
            body => body["lines"]![0]!["costAmount"] = "0",
            body => body["lines"] = new JArray(),
            body => body["lines"] = new JArray(Enumerable.Range(0, 201).Select(_ => data.Line("1", "1", false))),
        };
        foreach (var mutate in bodies)
        {
            var body = data.Body(); mutate(body);
            Error(await data.Send(HttpMethod.Post, "sales-receipts", body, access), 400, "ValidationError");
            Assert.Equal(before, await data.State());
        }
        Error(await data.Send(HttpMethod.Post, "sales-receipts", data.Body(), null), 401, "Auth.Unauthorized");
        Assert.Equal(before, await data.State());
        var malformed = data.Body().ToString(Formatting.None).Replace("\"saleDate\":\"2024-02-29\"", "\"saleDate\":\"2024-02-29\",\"saleDate\":\"2024-03-01\"");
        Error(await data.Http.Request(HttpMethod.Post, "sales-receipts", malformed, null, access), 400, "ValidationError");
        Assert.Equal(before, await data.State());
        var invalidReference = data.Body();
        invalidReference["lines"]![1]!["productId"] = int.MaxValue;
        var invalid = await data.Send(HttpMethod.Post, "sales-receipts", invalidReference, access);
        Error(invalid, 400, "ValidationError");
        Assert.Contains(invalid.Json["error"]!["details"]!, detail => (string?)detail["field"] == "lines[1].productId");
        Assert.Equal(before, await data.State());
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Failed_Post_rolls_back_once_without_automatic_mutating_retry() => Owned(async data =>
    {
        await data.GrantAll();
        var access = (await data.Http.Login("alice")).Access;
        var draft = Success(await data.Send(HttpMethod.Post, "sales-receipts", data.Body(), access));
        await data.Stock(20m, 300m);
        // A test-owned nontransactional sequence observes attempts even when business SQL rolls back.
        // The first insertion fails; an unsafe automatic retry would instead succeed on attempt two.
        await data.Http.Admin.ExecuteAsync("CREATE SEQUENCE dbo.WebSalesHttpFaultAttempts AS int START WITH 1 INCREMENT BY 1; EXEC(N'CREATE TRIGGER dbo.WebSalesHttpRejectFirstLedger ON dbo.StockLedger AFTER INSERT AS BEGIN DECLARE @attempt int=NEXT VALUE FOR dbo.WebSalesHttpFaultAttempts; IF @attempt=1 THROW 51222,''Injected first Post failure.'',1; END;');");
        var before = await data.State();
        var denied = await data.Send(HttpMethod.Post, "sales-receipts/" + (string)draft["saleId"]! + "/post", new JObject { ["expectedVersion"] = draft["version"]!.DeepClone() }, access);
        Error(denied, 500, "InternalError");
        Assert.DoesNotContain("Injected first", denied.Body);
        Assert.Equal(before, await data.State());
        Assert.Equal(1, await data.Http.Admin.QuerySingleAsync<int>("SELECT CONVERT(int,current_value) FROM sys.sequences WHERE object_id=OBJECT_ID(N'dbo.WebSalesHttpFaultAttempts');"));
        Assert.Equal(0, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
        Assert.Equal(0, await data.Http.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries;"));
    });

    private static decimal Number(JToken token)
    {
        Assert.Equal(JTokenType.String, token.Type);
        return decimal.Parse((string)token!, CultureInfo.InvariantCulture);
    }
    private static JToken Success(Reply reply)
    {
        Assert.Equal(200, reply.Status);
        Assert.Null(reply.Json["error"]);
        return reply.Json["data"]!;
    }
    private static void Error(Reply reply, int status, string code)
    {
        Assert.Equal(status, reply.Status);
        Assert.Equal(code, (string?)reply.Json["error"]!["code"]);
        Assert.Null(reply.Json["data"]);
    }
    private static JObject Save(JToken detail)
    {
        var body = new JObject();
        foreach (var name in new[] { "saleDate", "customerId", "warehouseId", "employeeId", "note" })
            if (detail[name] != null) body[name] = detail[name]!.DeepClone();
        body["expectedVersion"] = detail["version"]!.DeepClone();
        body["lines"] = new JArray(detail["lines"]!.Select(line => new JObject(line.Children<JProperty>()
            .Where(property => new[] { "saleLineId", "productId", "qty", "unitPrice", "isGift" }.Contains(property.Name))
            .Select(property => new JProperty(property.Name, property.Value.DeepClone())))));
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
            var data = await Data.Seed(http);
            await body(data);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try { await database.DisposeAsync(); }
            catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); }
        }
    }
    private sealed class Data
    {
        public HttpScenario Http { get; private set; } = null!;
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
            OtherProduct = await http.Admin.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'OTHER',N'Other Product',N'BOX');"),
        };
        public async Task Grant(int actor, params string[] permissions)
        {
            foreach (var key in permissions)
                await Http.Admin.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@actor,@key,1);", new { actor, key });
        }
        public Task GrantAll() => Grant(Http.Alice, "Sales.Receipts.Read", "Sales.Receipts.Write", "Sales.Receipts.Post", "Catalog.Products.Read", "Catalog.Customers.Read", "Catalog.Warehouses.Read", "Catalog.Employees.Read");
        public JObject Line(string qty, string price, bool gift) => new JObject { ["saleLineId"] = "0", ["productId"] = Product, ["qty"] = qty, ["unitPrice"] = price, ["isGift"] = gift };
        public JObject Body() => new JObject
        {
            ["saleDate"] = "2024-02-29", ["customerId"] = Customer, ["warehouseId"] = Warehouse, ["employeeId"] = Employee,
            ["note"] = "Phiếu thử — hàng tặng", ["lines"] = new JArray(Line("3.0000", "25.0000", false), Line("2.0000", "25.0000", true), Line("1.0000", "0.0000", false)),
        };
        public Task<Reply> Send(HttpMethod method, string path, JToken? body, string? access) => Http.Request(method, path, body?.ToString(Formatting.None), null, access);
        public Task Stock(decimal quantity, decimal value) => Http.Admin.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@warehouse,@product,@quantity,@value);", new { warehouse = Warehouse, product = Product, quantity, value });
        public async Task<string> State() => string.Join("|", new[]
        {
            await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.SalesReceipts ORDER BY SaleId FOR JSON PATH);"),
            await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.SalesReceiptLines ORDER BY SaleLineId FOR JSON PATH);"),
            await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.StockBalances ORDER BY WarehouseId,ProductId FOR JSON PATH);"),
            await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.StockLedger ORDER BY LedgerId FOR JSON PATH);"),
            await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.StockValuationEntries ORDER BY LedgerId FOR JSON PATH);"),
            await Http.Admin.QuerySingleAsync<string>("SELECT (SELECT name,CONVERT(decimal(38,0),current_value) AS value FROM sys.sequences WHERE object_id IN (OBJECT_ID(N'dbo.SalesReceiptNumberSequence'),OBJECT_ID(N'dbo.GoodsReceiptNumberSequence')) ORDER BY name FOR JSON PATH);"),
        });
    }
}
