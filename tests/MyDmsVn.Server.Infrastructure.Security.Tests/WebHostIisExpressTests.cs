using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using MyDmsVn.Desktop.Tests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Explicit packaged-host selector. Requires a published Release host and the operator's
// existing localhost:44321 IIS Express TLS binding. Never changes machine TLS/IIS config.
public sealed partial class WebHostIisExpressTests
{
    [WebHostSqlFact, Trait("Category", "WebHostIntegration")]
    public Task Published_SystemWeb_host_warms_up_with_restricted_schema11_and_serves_safe_HTTP() => Run(false);

    [WebBrowserSqlFact, Trait("Category", "WebBrowserIntegration")]
    public Task Published_React_browser_uses_real_HTTPS_SQL_and_atomic_stock() => Run(true);

    [WebBrowserSqlFact, Trait("Category", "WebBrowserIntegration")]
    public Task Published_React_Receipt_browser_uses_real_HTTPS_SQL_and_independent_receipt_stock() => Run(true, receiptPilot: true);

    [WebBrowserSqlFact, Trait("Category", "WebBrowserIntegration")]
    public Task Published_React_integrated_browser_preserves_same_key_QV_COGS_and_warehouse_isolation() => Run(true, receiptPilot: true, integratedPilot: true);

    [WebBrowserSqlFact, Trait("Category", "WebBrowserIntegration")]
    public Task Published_React_WEB4_recovery_preserves_journal_and_physical_stock() => Run(true, receiptPilot: true, integratedPilot: true, recoveryPilot: true);

    private static async Task Run(bool browser, bool receiptPilot = false, bool integratedPilot = false, bool recoveryPilot = false)
    {
        GuiTestSafety.ThrowIfPoisoned();
        if (browser) Assert.Contains(Environment.GetEnvironmentVariable("MYDMSVN_WEB_PILOT_PROJECT"), new[] { "chromium", "edge" });
        var published = Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY");
        var thumbprint = Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_TLS_THUMBPRINT");
        Assert.False(string.IsNullOrWhiteSpace(published));
        Assert.False(string.IsNullOrWhiteSpace(thumbprint));
        Assert.True(File.Exists(Path.Combine(published!, "Web.config")));
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS Express", "iisexpress.exe");
        Assert.True(File.Exists(executable));
        Assert.DoesNotContain(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners(), endpoint => endpoint.Port == 44321 || endpoint.Port == 18081);
        var admin = Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var database = await SqlTestDatabase.CreateAsync(admin);
        var login = new OwnedLogin(admin);
        Process? process = null;
        var failures = new List<Exception>();
        var drained = true;
        int customer = 0, warehouse = 0, employee = 0, product = 0, receiptProduct = 0;
        IntegratedCatalog? integrated = null;
        try
        {
            var migration = recoveryPilot ? new DatabaseMigrationRunner().MigrateWeb(database.ConnectionString) : FrozenWeb11Migration.Install(database.ConnectionString);
            Assert.True(migration.Successful, "Fresh web migration must succeed.");
            await login.CreateAsync();
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync("CREATE USER [" + login.Name + "] FOR LOGIN [" + login.Name + "]; CREATE USER WebSmokeMigration WITHOUT LOGIN;");
                await connection.ExecuteAsync(File.ReadAllText(FindRootFile("deployment/sql/Provision-MyDmsVnWebPrincipals.sql"))
                    .Replace("$(WebRuntimeUser)", login.Name).Replace("$(MigrationUser)", "WebSmokeMigration"));
                await connection.ExecuteAsync("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) VALUES(N'web.smoke',N'WEB.SMOKE',N'Web smoke',N'Local',@hash,N'',N'BCrypt'); " +
                    "INSERT dbo.SystemSecuritySettings(Id,SuperuserUserId,BoundNormalizedUsername) SELECT 1,UserId,N'WEB.SMOKE' FROM dbo.Users WHERE NormalizedUsername=N'WEB.SMOKE';",
                    new { hash = new BcryptPasswordHasher().Hash("IisSmoke_test-only-password_2026!") });
                customer = await connection.QuerySingleAsync<int>("INSERT dbo.Customers(Code,Name) OUTPUT INSERTED.CustomerId VALUES(N'PILOT-C',N'Pilot Customer');");
                warehouse = await connection.QuerySingleAsync<int>("INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'PILOT-W',N'Pilot Warehouse');");
                employee = await connection.QuerySingleAsync<int>("INSERT dbo.Employees(Code,Name) OUTPUT INSERTED.EmployeeId VALUES(N'PILOT-E',N'Pilot Employee');");
                product = await connection.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'PILOT-P',N'Pilot Product',N'EA');");
                await connection.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@warehouse,@product,@quantity,@value);", new { warehouse, product, quantity = browser ? 1000 : 20, value = browser ? 15000 : 300 });
                if (browser) await SeedBrowserActors(connection);
                if (receiptPilot)
                {
                    receiptProduct = await connection.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'WEB3-RP',N'Web3 Receipt Product',N'EA');");
                    await SeedReceiptActors(connection);
                }
                if (integratedPilot) integrated = await SeedIntegratedActorsAndCatalog(connection);
            }
            var runDirectory = Path.Combine(Path.GetDirectoryName(published!)!, "iis-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runDirectory);
            var configuration = CreateConfiguration(runDirectory, published!);
            var start = new ProcessStartInfo(executable, "/config:\"" + configuration + "\" /site:Web2Smoke /systray:false")
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            };
            var key = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(key);
            var inherited = new List<string>();
            foreach (string name in start.EnvironmentVariables.Keys) if (name.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase)) inherited.Add(name);
            foreach (var name in inherited) start.EnvironmentVariables.Remove(name);
            start.EnvironmentVariables["MYDMSVN_WEB_ORIGIN"] = "https://localhost:44321";
            start.EnvironmentVariables["MYDMSVN_WEB_OPERATION_MODE"] = recoveryPilot ? "web4" : "legacy-web11";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_ISSUER"] = "https://localhost:44321";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_AUDIENCE"] = "web-smoke";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_ACTIVE_KID"] = "smoke";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_KEYS"] = "smoke:" + Convert.ToBase64String(key);
            start.EnvironmentVariables["MYDMSVN_WEB_SQL_CONNECTION_STRING"] = login.ConnectionString(database.DatabaseName);
            start.EnvironmentVariables["MYDMSVN_WEB_SUPERUSER_USERNAME"] = "web.smoke";
            process = new Process { StartInfo = start };
            process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { };
            Assert.True(process.Start()); drained = false;
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            Reply? health = null;
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while (DateTime.UtcNow < deadline && !process.HasExited)
            {
                try { health = await Request("https://localhost:44321/api/v1/health", thumbprint!); if (health.Status == 200) break; }
                catch (WebException) { }
                await Task.Delay(250);
            }
            Assert.NotNull(health);
            Assert.False(process.HasExited);
            Assert.Equal(200, health!.Status);
            Assert.Contains("application/json", health.ContentType);
            Assert.Equal("{\"data\":{\"status\":\"ok\"}}", health.Body);
            Assert.Equal("iis-smoke", health.Correlation);
            if (browser)
            {
                foreach (var type in new[] { (Extension: "*.js", Mime: "text/javascript"), (Extension: "*.css", Mime: "text/css"), (Extension: "*.woff2", Mime: "font/woff2") })
                {
                    var file = Directory.GetFiles(Path.Combine(published!, "wwwroot", "assets"), type.Extension).First();
                    var asset = await Request("https://localhost:44321/assets/" + Path.GetFileName(file), thumbprint!);
                    Assert.True(asset.Status == 200, "Published asset failed: extension=" + type.Extension + "; status=" + asset.Status + "; contentType=" + asset.ContentType);
                    Assert.Contains(type.Mime, asset.ContentType);
                    Assert.Equal(File.ReadAllBytes(file), asset.Bytes);
                }
                foreach (var deniedPath in new[] { "/assets/missing.js", "/Web.config", "/bin/MyDmsVn.Server.HttpHost.dll" })
                    Assert.Equal(404, (await Request("https://localhost:44321" + deniedPath, thumbprint!)).Status);
                Assert.Equal(401, (await Request("https://localhost:44321/api/v1/missing", thumbprint!)).Status);
                await RunBrowser(database.ConnectionString, runDirectory, customer, warehouse, employee, product, receiptProduct, integrated, recoveryPilot);
            }
            else
            {
                var denied = await Request("https://localhost:44321/api/v1/private", thumbprint!);
                Assert.Equal(401, denied.Status); Assert.Null(denied.Location);
                Assert.Contains("application/json", denied.ContentType);
                Assert.Contains("Auth.Unauthorized", denied.Body);
                var invalid = await Request("https://localhost:44321/api/v1/health", thumbprint!, "Bearer invalid-token");
                Assert.Equal(401, invalid.Status); Assert.DoesNotContain("invalid-token", invalid.Body);
                var credentials = JsonConvert.SerializeObject(new { username = "web.smoke", password = "IisSmoke_test-only-password_2026!" });
                var missingOrigin = await Request("https://localhost:44321/api/v1/auth/login", thumbprint!, method: "POST", body: credentials, csrf: true);
                Assert.Equal(403, missingOrigin.Status);
                var signedIn = await Request("https://localhost:44321/api/v1/auth/login", thumbprint!, origin: "https://localhost:44321", method: "POST", body: credentials, csrf: true);
                Assert.Equal(200, signedIn.Status);
                AssertRefreshCookie(signedIn, cleared: false);
                var bearer = "Bearer " + (string)JObject.Parse(signedIn.Body)["data"]!["accessToken"]!;
                var me = await Request("https://localhost:44321/api/v1/auth/me", thumbprint!, bearer);
                Assert.Equal(200, me.Status);
                Assert.Equal("web.smoke", (string?)JObject.Parse(me.Body)["data"]!["username"]);
                var lookup = await Request("https://localhost:44321/api/v1/catalog/products/lookup?limit=25", thumbprint!, bearer);
                Assert.Equal(200, lookup.Status);
                Assert.Contains(JObject.Parse(lookup.Body)["data"]!, row => (int)row["id"]! == product);
                var saleBody = new JObject
                {
                    ["saleDate"] = "2024-02-29", ["customerId"] = customer, ["warehouseId"] = warehouse, ["employeeId"] = employee,
                    ["lines"] = new JArray(
                        new JObject { ["saleLineId"] = "0", ["productId"] = product, ["qty"] = "3.0000", ["unitPrice"] = "25.0000", ["isGift"] = false },
                        new JObject { ["saleLineId"] = "0", ["productId"] = product, ["qty"] = "2.0000", ["unitPrice"] = "25.0000", ["isGift"] = true }),
                };
                var created = await Request("https://localhost:44321/api/v1/sales-receipts", thumbprint!, bearer, "https://localhost:44321", "POST", saleBody.ToString(Formatting.None), csrf: true);
                Assert.Equal(200, created.Status);
                var sale = JObject.Parse(created.Body)["data"]!;
                Assert.Equal(JTokenType.String, sale["saleId"]!.Type);
                Assert.Equal(JTokenType.String, sale["lines"]![0]!["saleLineId"]!.Type);
                Assert.Equal(JTokenType.String, sale["totalAmount"]!.Type);
                var saleUrl = "https://localhost:44321/api/v1/sales-receipts/" + (string)sale["saleId"]!;
                var fetched = await Request(saleUrl, thumbprint!, bearer);
                Assert.Equal(200, fetched.Status);
                Assert.True(JToken.DeepEquals(NormalizeDraftDecimals(sale), NormalizeDraftDecimals(JObject.Parse(fetched.Body)["data"]!)));
                saleBody["expectedVersion"] = sale["version"]!.DeepClone();
                saleBody["note"] = "System.Web smoke update";
                for (var index = 0; index < 2; index++) saleBody["lines"]![index]!["saleLineId"] = sale["lines"]![index]!["saleLineId"]!.DeepClone();
                var updated = await Request(saleUrl + "/draft", thumbprint!, bearer, "https://localhost:44321", "PUT", saleBody.ToString(Formatting.None), csrf: true);
                Assert.True(updated.Status == 200, "Sales PUT failed: status=" + updated.Status + "; contentType=" + updated.ContentType + "; correlation=" + updated.Correlation + "; error=" + (updated.Status >= 400 ? updated.Body : "<omitted>"));
                var posted = await Request(saleUrl + "/post", thumbprint!, bearer, "https://localhost:44321", "POST",
                    new JObject { ["expectedVersion"] = JObject.Parse(updated.Body)["data"]!["version"]!.DeepClone() }.ToString(Formatting.None), csrf: true);
                Assert.Equal(200, posted.Status);
                var ack = JObject.Parse(posted.Body)["data"]!;
                Assert.Equal("Posted", (string?)ack["status"]);
                Assert.Null(ack["lines"]);
                var postedDetail = await Request(saleUrl, thumbprint!, bearer);
                Assert.Equal(200, postedDetail.Status);
                var postedJson = JObject.Parse(postedDetail.Body)["data"]!;
                Assert.Equal(JTokenType.String, postedJson["totalCost"]!.Type);
                Assert.Equal(JTokenType.String, postedJson["lines"]![0]!["postedUnitCost"]!.Type);
                using (var oracle = new SqlConnection(database.ConnectionString))
                {
                    await oracle.OpenAsync();
                    Assert.Equal(15m, await oracle.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances;"));
                    Assert.Equal(225m, await oracle.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances;"));
                    Assert.Equal(2, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentType='SalesReceipt';"));
                }
                var crossRefresh = await Request("https://localhost:44321/api/v1/auth/refresh", thumbprint!, origin: "https://outside.invalid", method: "POST", cookie: signedIn.Cookie, csrf: true);
                Assert.Equal(403, crossRefresh.Status);
                var rotated = await Request("https://localhost:44321/api/v1/auth/refresh", thumbprint!, origin: "https://localhost:44321", method: "POST", cookie: signedIn.Cookie, csrf: true);
                Assert.Equal(200, rotated.Status);
                AssertRefreshCookie(rotated, cleared: false);
                Assert.NotEqual(signedIn.Cookie, rotated.Cookie);
                var rotatedBearer = "Bearer " + (string)JObject.Parse(rotated.Body)["data"]!["accessToken"]!;
                Assert.Equal(200, (await Request("https://localhost:44321/api/v1/auth/me", thumbprint!, rotatedBearer)).Status);
                var admitted = await Request("https://localhost:44321/api/v1/private", thumbprint!, bearer);
                Assert.Equal(404, admitted.Status); Assert.Contains("application/json", admitted.ContentType);
                Assert.Null(admitted.Location); Assert.DoesNotContain("<html", admitted.Body);
                var signedOut = await Request("https://localhost:44321/api/v1/auth/logout", thumbprint!, origin: "https://localhost:44321", method: "POST", cookie: rotated.Cookie, csrf: true);
                Assert.Equal(200, signedOut.Status);
                AssertRefreshCookie(signedOut, cleared: true);
                Assert.Equal(401, (await Request("https://localhost:44321/api/v1/private", thumbprint!, bearer)).Status);
                Assert.Equal(401, (await Request("https://localhost:44321/api/v1/auth/me", thumbprint!, rotatedBearer)).Status);
                Assert.Equal(401, (await Request("https://localhost:44321/api/v1/auth/refresh", thumbprint!, origin: "https://localhost:44321", method: "POST", cookie: rotated.Cookie, csrf: true)).Status);
                var root = await Request("https://localhost:44321/", thumbprint!);
                var fallback = await Request("https://localhost:44321/sales/receipt/42", thumbprint!);
                Assert.Equal(200, root.Status); Assert.Equal(200, fallback.Status);
                Assert.NotEqual(root.Body, fallback.Body);
                string WithoutNonce(string html) => Regex.Replace(html, "<meta name=\"mydmsvn-style-nonce\" content=\"[^\"]+\" />", "");
                Assert.Equal(WithoutNonce(root.Body), WithoutNonce(fallback.Body));
                Assert.Contains("text/html", fallback.ContentType);
                Assert.Equal(404, (await Request("https://localhost:44321/missing.js", thumbprint!)).Status);
                Assert.Equal(400, (await Request("http://localhost:18081/api/v1/health", thumbprint!)).Status);
                var crossOrigin = await Request("https://localhost:44321/api/v1/health", thumbprint!, origin: "https://outside.invalid");
                Assert.Equal(403, crossOrigin.Status);
            }
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        // A closed stdin pipe does not prove the worker exited. Always run the
                        // bounded wait/kill/drain below before touching its SQL resources.
                        try { process.StandardInput.WriteLine("q"); }
                        catch (IOException) { }
                        catch (InvalidOperationException) { }
                        if (!process.WaitForExit(10000)) process.Kill();
                        if (!process.WaitForExit(10000)) throw new InvalidOperationException("Owned IIS Express worker did not drain; retain SQL resources.");
                    }
                    drained = true;
                }
                catch (Exception error) { failures.Add(error); }
                finally { process.Dispose(); }
            }
            if (drained && !GuiTestSafety.IsPoisoned)
            {
                var loginDrained = false;
                try { await login.CleanupAsync(); loginDrained = true; } catch (Exception error) { failures.Add(error); }
                if (loginDrained) try { await database.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
            }
        }
        if (failures.Count > 0) throw new AggregateException("Packaged host scenario or owned cleanup failed.", failures);
    }

    private static string FindRootFile(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }
    private static JToken NormalizeDraftDecimals(JToken detail)
    {
        var result = detail.DeepClone();
        void Normalize(JToken owner, string name)
        {
            Assert.Equal(JTokenType.String, owner[name]!.Type);
            var value = decimal.Parse((string)owner[name]!, CultureInfo.InvariantCulture);
            Assert.Equal(value, decimal.Round(value, 4));
            owner[name] = value.ToString("F4", CultureInfo.InvariantCulture);
        }
        // The contract permits shorter exact decimal forms. Gift revenue is initially "0",
        // while SQL decimal(28,4) materializes "0.0000"; IDs/versions remain untouched.
        Normalize(result, "totalAmount");
        foreach (var line in result["lines"]!)
            foreach (var name in new[] { "qty", "unitPrice", "lineAmount" }) Normalize(line, name);
        return result;
    }
    private class WebHostSqlFactAttribute : FactAttribute
    {
        public WebHostSqlFactAttribute()
        {
            foreach (var variable in new[] { SqlTestDatabase.ConnectionStringEnvironmentVariable, "MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY", "MYDMSVN_WEB_TEST_TLS_THUMBPRINT" })
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
                    Skip = "Packaged IIS Express SQL test requires explicit publish, TLS pin and nonproduction SQL configuration.";
        }
    }
    private sealed class WebBrowserSqlFactAttribute : WebHostSqlFactAttribute
    {
        public WebBrowserSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY")))
                Skip = "Real browser pilot requires an explicit frontend checkout with built artifacts and Playwright.";
        }
    }

    private const string BrowserPassword = "BrowserPilot_test-only-password_2026!";
    private static async Task SeedBrowserActors(SqlConnection connection)
    {
        var hash = new BcryptPasswordHasher().Hash(BrowserPassword);
        foreach (var name in new[] { "alice", "bob", "poster" })
        {
            var id = await connection.QuerySingleAsync<int>("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(@name,@normalized,@name,N'Local',@hash,N'',N'BCrypt');", new { name, normalized = name.ToUpperInvariant(), hash });
            foreach (var key in new[] { "Catalog.Products.Read", "Catalog.Customers.Read", "Catalog.Warehouses.Read", "Catalog.Employees.Read", "Sales.Receipts.Read", "Sales.Receipts.Write", "Sales.Receipts.Post" })
            {
                var grant = key.StartsWith("Catalog.", StringComparison.Ordinal) || name == "alice" || (name == "bob" ? key == "Sales.Receipts.Read" : key != "Sales.Receipts.Read");
                await connection.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@id,@key,@grant);", new { id, key, grant });
            }
        }
    }

    private static async Task SeedReceiptActors(SqlConnection connection)
    {
        var hash = new BcryptPasswordHasher().Hash(BrowserPassword);
        foreach (var name in new[] { "receiptEditor", "receiptPoster", "receiptReader" })
        {
            var id = await connection.QuerySingleAsync<int>("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(@name,@normalized,@name,N'Local',@hash,N'',N'BCrypt');", new { name, normalized = name.ToUpperInvariant(), hash });
            var permissions = new List<string> { "Catalog.Products.Read", "Catalog.Customers.Read", "Catalog.Warehouses.Read", "Catalog.Employees.Read" };
            if (name == "receiptEditor") permissions.AddRange(new[] { "Sales.Receipts.Read", "Sales.Receipts.Write", "Sales.Receipts.Post", "Inventory.GoodsReceipts.Read", "Inventory.GoodsReceipts.Write", "Inventory.GoodsReceipts.Post" });
            if (name == "receiptPoster") permissions.AddRange(new[] { "Inventory.GoodsReceipts.Write", "Inventory.GoodsReceipts.Post" });
            if (name == "receiptReader") permissions.Add("Inventory.GoodsReceipts.Read");
            foreach (var key in permissions)
                await connection.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@id,@key,1);", new { id, key });
        }
    }

    private static async Task RunBrowser(string connectionString, string runDirectory, int customer, int warehouse, int employee, int product, int receiptProduct, IntegratedCatalog? integrated, bool recoveryPilot)
    {
        var checkout = Path.GetFullPath(Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY")!);
        var node = Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_NODE") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
        var cli = Path.Combine(checkout, "node_modules", "@playwright", "test", "cli.js");
        Assert.True(File.Exists(node)); Assert.True(File.Exists(cli));
        Assert.True(File.Exists(Path.Combine(checkout, "playwright.pilot.config.ts")));
        var manifestPath = Path.Combine(runDirectory, "browser-manifest.json");
        var resultPath = Path.Combine(runDirectory, "browser-result.json");
        var certificatePath = Path.Combine(runDirectory, "localhost-public.pem");
        object Actor(string username) => new { username, password = BrowserPassword };
        var manifest = new { origin = "https://localhost:44321", actors = new { alice = Actor("alice"), bob = Actor("bob"), poster = Actor("poster") }, catalog = new { customerId = customer, warehouseId = warehouse, employeeId = employee, productId = product }, resultPath };
        var manifestJson = JObject.FromObject(manifest);
        if (receiptProduct != 0)
        {
            Assert.True(File.Exists(Path.Combine(checkout, "tests", "pilot", "web3-receipt-live.spec.ts")));
            manifestJson["mode"] = "web3-receipt";
            foreach (var name in new[] { "receiptEditor", "receiptPoster", "receiptReader" }) manifestJson["actors"]![name] = JObject.FromObject(Actor(name));
            manifestJson["receiptCatalog"] = JObject.FromObject(new { warehouseId = warehouse, employeeId = employee, productId = receiptProduct });
        }
        if (integrated != null)
        {
            Assert.True(File.Exists(Path.Combine(checkout, "tests", "pilot", "web3-integrated-live.spec.ts")));
            manifestJson["mode"] = "web3-integrated";
            foreach (var name in new[] { "integratedEditor", "balanceReader", "cardReader" }) manifestJson["actors"]![name] = JObject.FromObject(Actor(name));
            manifestJson["integratedCatalog"] = JObject.FromObject(new { warehouseId = integrated.Warehouse, otherWarehouseId = integrated.OtherWarehouse, employeeId = employee, customerId = customer, productId = integrated.Product });
        }
        WebRecoveryPilotControl? recovery = null;
        if (recoveryPilot)
        {
            Assert.True(File.Exists(Path.Combine(checkout, "tests", "pilot", "web4-recovery-live.spec.ts")));
            recovery = await WebRecoveryPilotControl.Start(connectionString);
            manifestJson["mode"] = "web4-recovery";
            manifestJson["recovery"] = recovery.Manifest;
        }
        Exception? browserFailure = null;
        try
        {
            File.WriteAllText(manifestPath, manifestJson.ToString(Formatting.None), new UTF8Encoding(false));
            using (var certificates = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                certificates.Open(OpenFlags.ReadOnly);
                using var certificate = certificates.Certificates.Find(X509FindType.FindByThumbprint, Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_TLS_THUMBPRINT"), false).Cast<X509Certificate2>().Single();
                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                Assert.True(chain.Build(certificate), "The explicitly pinned local certificate must already be OS-trusted.");
                File.WriteAllText(certificatePath, "-----BEGIN CERTIFICATE-----\n" + Convert.ToBase64String(certificate.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks) + "\n-----END CERTIFICATE-----\n", new UTF8Encoding(false));
            }
            var project = Environment.GetEnvironmentVariable("MYDMSVN_WEB_PILOT_PROJECT");
            Assert.Contains(project, new[] { "chromium", "edge" });
            var start = new ProcessStartInfo(node, "\"" + cli + "\" test --config playwright.pilot.config.ts --workers=1 --max-failures=1 --project=" + project)
            { WorkingDirectory = checkout, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var name in start.EnvironmentVariables.Keys.Cast<string>().Where(name => name.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.EnvironmentVariables.Remove(name);
            start.EnvironmentVariables["MYDMSVN_WEB_PILOT_MANIFEST"] = manifestPath;
            // Node uses its own CA set; add only the already trusted explicit fixture cert.
            // The real browser keeps ordinary platform TLS verification; neither ignores errors.
            start.EnvironmentVariables["NODE_EXTRA_CA_CERTS"] = certificatePath;
            GuiProcessResult result;
            try { result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromMinutes(5)); }
            catch (GuiProcessException error)
            {
                File.WriteAllText(Path.Combine(runDirectory, "browser-output.log"), Sanitize(recovery?.Redact(error.StandardOutput + error.StandardError) ?? error.StandardOutput + error.StandardError), new UTF8Encoding(false));
                throw new InvalidOperationException("Browser process failed; cleanup verified=" + error.CleanupVerified + ". Inspect the sanitized local browser-output.log.");
            }
            File.WriteAllText(Path.Combine(runDirectory, "browser-output.log"), Sanitize(recovery?.Redact(result.StandardOutput + result.StandardError) ?? result.StandardOutput + result.StandardError), new UTF8Encoding(false));
            Assert.True(result.ExitCode == 0, "Browser pilot failed; inspect the local browser-output.log (redact before sharing).");
            if (recovery != null)
            {
                await recovery.Stop();
                await VerifyRecoveryStock(connectionString, JObject.Parse(File.ReadAllText(resultPath)), recovery.Epochs, warehouse, product, receiptProduct, integrated!);
                return;
            }
            var posted = JArray.Parse(File.ReadAllText(resultPath));
            using var oracle = new SqlConnection(connectionString); await oracle.OpenAsync();
            VerifyPilotResultIdentities(posted,
                (await oracle.QueryAsync<long>("SELECT ReceiptId FROM dbo.GoodsReceipts WHERE Status='Posted';")).ToArray(),
                (await oracle.QueryAsync<long>("SELECT SaleId FROM dbo.SalesReceipts WHERE Status='Posted';")).ToArray(), integrated != null);
            var integratedRows = posted.Where(row => (string?)row["scenario"] == "integrated").ToArray();
            posted = new JArray(posted.Where(row => (string?)row["scenario"] == null));
            var receipts = posted.Where(row => (string?)row["documentType"] == "GoodsReceipt").ToArray();
            if (receiptProduct == 0) Assert.Empty(receipts);
            else Assert.NotEmpty(receipts);
            Assert.All(posted, row => Assert.True(row["documentType"] == null || (string?)row["documentType"] == "GoodsReceipt", "Unknown browser result document type."));
            posted = new JArray(posted.Where(row => row["documentType"] == null));
            Assert.NotEmpty(posted);
            Assert.Equal(posted.Count, posted.Select(row => (string)row["saleId"]!).Distinct().Count());
            decimal quantity = 0, cost = 0;
            foreach (var row in posted)
            {
                var id = long.Parse((string)row["saleId"]!, CultureInfo.InvariantCulture);
                var qty = decimal.Parse((string)row["quantity"]!, CultureInfo.InvariantCulture);
                var amount = decimal.Parse((string)row["cost"]!, CultureInfo.InvariantCulture);
                Assert.Equal("Posted", await oracle.QuerySingleAsync<string>("SELECT Status FROM dbo.SalesReceipts WHERE SaleId=@id;", new { id }));
                Assert.Equal(qty, await oracle.QuerySingleAsync<decimal>("SELECT SUM(Qty) FROM dbo.SalesReceiptLines WHERE SaleId=@id;", new { id }));
                Assert.Equal(amount, await oracle.QuerySingleAsync<decimal>("SELECT SUM(CostAmount) FROM dbo.SalesReceiptLines WHERE SaleId=@id;", new { id }));
                quantity += qty; cost += amount;
            }
            Assert.Equal(posted.Count, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceipts s WHERE s.Status='Posted' AND EXISTS(SELECT 1 FROM dbo.SalesReceiptLines l WHERE l.SaleId=s.SaleId AND l.ProductId=@product);", new { product }));
            var salesKeyFilter = receiptProduct == 0 ? "" : " WHERE WarehouseId=@warehouse AND ProductId=@product";
            Assert.Equal(1000m - quantity, await oracle.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances" + salesKeyFilter + ";", new { warehouse, product }));
            Assert.Equal(15000m - cost, await oracle.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances" + salesKeyFilter + ";", new { warehouse, product }));
            var salesLedgerKeyFilter = receiptProduct == 0 ? "" : " AND l.WarehouseId=@warehouse AND l.ProductId=@product";
            Assert.Equal(quantity, -(await oracle.QuerySingleAsync<decimal>("SELECT SUM(l.QuantityChange) FROM dbo.StockLedger l WHERE l.DocumentType='SalesReceipt'" + salesLedgerKeyFilter + ";", new { warehouse, product })));
            Assert.Equal(cost, -(await oracle.QuerySingleAsync<decimal>("SELECT SUM(v.ValueChange) FROM dbo.StockValuationEntries v JOIN dbo.StockLedger l ON l.LedgerId=v.LedgerId WHERE l.DocumentType='SalesReceipt'" + salesLedgerKeyFilter + ";", new { warehouse, product })));
            Assert.Equal(await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceiptLines l JOIN dbo.SalesReceipts s ON s.SaleId=l.SaleId WHERE s.Status='Posted';"),
                await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId JOIN dbo.SalesReceiptLines s ON s.SaleLineId=l.DocumentLineId AND s.SaleId=l.DocumentId WHERE l.DocumentType='SalesReceipt' AND l.QuantityChange=-s.Qty AND v.ValueChange=-s.CostAmount;"));
            if (receiptProduct != 0) await VerifyReceiptStock(oracle, receipts, warehouse, receiptProduct);
            if (integrated != null) await VerifyIntegratedStock(oracle, integratedRows, integrated);
            await VerifyAllPilotMovements(oracle, warehouse, product, receiptProduct, integrated);
        }
        catch (Exception error) { browserFailure = error; throw; }
        finally
        {
            try { if (recovery != null) await recovery.Stop(); }
            catch (Exception cleanup) when (browserFailure != null) { throw new AggregateException("Browser scenario and recovery control drain failed.", browserFailure, cleanup); }
            finally { File.Delete(manifestPath); File.Delete(certificatePath); }
        }
    }
    private static async Task VerifyReceiptStock(SqlConnection oracle, JToken[] receipts, int warehouse, int product)
    {
        var ids = VerifyReceiptResultIdentities(receipts, (await oracle.QueryAsync<long>("SELECT r.ReceiptId FROM dbo.GoodsReceipts r WHERE r.Status='Posted' AND EXISTS(SELECT 1 FROM dbo.GoodsReceiptLines l WHERE l.ReceiptId=r.ReceiptId AND l.ProductId=@product);", new { product })).ToArray());
        decimal totalQuantity = 0, totalValue = 0;
        var totalLines = 0;
        for (var index = 0; index < receipts.Length; index++)
        {
            var row = receipts[index];
            var id = ids[index];
            var expectedQuantity = decimal.Parse((string)row["quantity"]!, CultureInfo.InvariantCulture);
            var expectedValue = decimal.Parse((string)row["value"]!, CultureInfo.InvariantCulture);
            var actor = (string)row["actor"]!;
            Assert.Contains(actor, new[] { "receiptEditor", "receiptPoster" });
            var header = await oracle.QuerySingleAsync<(string Status, int Warehouse, int Actor, string Username)>(
                "SELECT r.Status,r.WarehouseId,r.PostedByUserId,u.Username FROM dbo.GoodsReceipts r JOIN dbo.Users u ON u.UserId=r.PostedByUserId WHERE r.ReceiptId=@id AND r.PostedAtUtc IS NOT NULL;", new { id });
            Assert.Equal("Posted", header.Status);
            Assert.Equal(warehouse, header.Warehouse);
            Assert.Equal(actor, header.Username);
            // Read physical source numbers and calculate independently, without Application valuation/projection helpers.
            var lines = (await oracle.QueryAsync<(long Id, int Product, decimal Quantity, decimal UnitCost)>(
                "SELECT ReceiptLineId,ProductId,Quantity,UnitCost FROM dbo.GoodsReceiptLines WHERE ReceiptId=@id ORDER BY [LineNo];", new { id })).ToArray();
            Assert.NotEmpty(lines);
            decimal quantity = 0, value = 0;
            foreach (var line in lines)
            {
                Assert.Equal(product, line.Product);
                Assert.True(line.Quantity > 0 && line.UnitCost >= 0);
                var amount = decimal.Round(line.Quantity * line.UnitCost, 4, MidpointRounding.AwayFromZero);
                var movement = await oracle.QuerySingleAsync<(int Warehouse, int Product, decimal Quantity, decimal Value, int Actor)>(
                    "SELECT l.WarehouseId,l.ProductId,l.QuantityChange,v.ValueChange,l.PostedByUserId FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId JOIN dbo.GoodsReceipts r ON r.ReceiptId=l.DocumentId AND r.PostedAtUtc=l.PostedAtUtc WHERE l.DocumentType='GoodsReceipt' AND l.DocumentId=@id AND l.DocumentLineId=@line;", new { id, line = line.Id });
                Assert.Equal((warehouse, product, line.Quantity, amount, header.Actor), movement);
                quantity += line.Quantity; value += amount;
            }
            Assert.Equal(lines.Length, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentType='GoodsReceipt' AND DocumentId=@id;", new { id }));
            Assert.Equal(expectedQuantity, quantity); Assert.Equal(expectedValue, value);
            totalQuantity += quantity; totalValue += value; totalLines += lines.Length;
        }
        Assert.Equal(totalLines, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentType='GoodsReceipt' AND WarehouseId=@warehouse AND ProductId=@product;", new { warehouse, product }));
        Assert.Equal(totalQuantity, await oracle.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances WHERE WarehouseId=@warehouse AND ProductId=@product;", new { warehouse, product }));
        Assert.Equal(totalValue, await oracle.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances WHERE WarehouseId=@warehouse AND ProductId=@product;", new { warehouse, product }));
        Assert.Equal(totalQuantity, await oracle.QuerySingleAsync<decimal>("SELECT SUM(QuantityChange) FROM dbo.StockLedger WHERE DocumentType='GoodsReceipt' AND WarehouseId=@warehouse AND ProductId=@product;", new { warehouse, product }));
        Assert.Equal(totalValue, await oracle.QuerySingleAsync<decimal>("SELECT SUM(v.ValueChange) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId WHERE l.DocumentType='GoodsReceipt' AND l.WarehouseId=@warehouse AND l.ProductId=@product;", new { warehouse, product }));
    }
    internal static long[] VerifyReceiptResultIdentities(JToken[] receipts, long[] postedIds)
    {
        var ids = new long[receipts.Length];
        var distinct = new HashSet<long>();
        for (var index = 0; index < receipts.Length; index++)
        {
            var token = receipts[index]["receiptId"];
            var text = token?.Type == JTokenType.String ? token.Value<string>() : null;
            if (text == null || !Regex.IsMatch(text, "\\A[1-9][0-9]{0,18}\\z", RegexOptions.CultureInvariant) ||
                !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                throw new InvalidDataException("Receipt result ID must be a canonical positive int64 string.");
            if (!distinct.Add(id)) throw new InvalidDataException("Duplicate numeric receipt result ID.");
            ids[index] = id;
        }
        if (!distinct.SetEquals(postedIds)) throw new InvalidDataException("Receipt result IDs must exactly match SQL Posted receipt IDs.");
        return ids;
    }
    private static string Sanitize(string output) => Regex.Replace(
        Regex.Replace(output.Replace(BrowserPassword, "<redacted>"), "(?i)(authorization: Bearer )[^\\s]+", "$1<redacted>"),
        "(?i)(__Host-mydmsvn-rt=)[^;\\s]+", "$1<redacted>");
    private static void AssertRefreshCookie(Reply reply, bool cleared)
    {
        Assert.NotNull(reply.SetCookie);
        var cookie = reply.SetCookie!.ToLowerInvariant();
        Assert.StartsWith("__host-mydmsvn-rt=", cookie);
        Assert.Contains("httponly", cookie); Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie); Assert.Contains("path=/", cookie);
        Assert.DoesNotContain("domain=", cookie);
        if (cleared) Assert.Contains("max-age=0", cookie);
        else Assert.True(Guid.TryParseExact(reply.Cookie, "D", out _));
    }
    private static string CreateConfiguration(string runDirectory, string published)
    {
        var source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IISExpress/config/applicationhost.config");
        var xml = new XmlDocument(); xml.Load(source);
        var sites = xml.SelectSingleNode("/configuration/system.applicationHost/sites")!;
        foreach (XmlNode site in sites.SelectNodes("site")!) sites.RemoveChild(site);
        var current = xml.CreateElement("site"); current.SetAttribute("name", "Web2Smoke"); current.SetAttribute("id", "1");
        var app = xml.CreateElement("application"); app.SetAttribute("path", "/"); app.SetAttribute("applicationPool", "Clr4IntegratedAppPool");
        var directory = xml.CreateElement("virtualDirectory"); directory.SetAttribute("path", "/"); directory.SetAttribute("physicalPath", Path.GetFullPath(published));
        app.AppendChild(directory); current.AppendChild(app);
        var bindings = xml.CreateElement("bindings");
        foreach (var endpoint in new[] { (Protocol: "https", Binding: "*:44321:localhost"), (Protocol: "http", Binding: "*:18081:localhost") })
        {
            var binding = xml.CreateElement("binding"); binding.SetAttribute("protocol", endpoint.Protocol); binding.SetAttribute("bindingInformation", endpoint.Binding); bindings.AppendChild(binding);
        }
        current.AppendChild(bindings); sites.AppendChild(current);
        var path = Path.Combine(runDirectory, "applicationhost.config"); xml.Save(path); return path;
    }
    private static async Task<Reply> Request(string url, string thumbprint, string? authorization = null, string? origin = null,
        string method = "GET", string? body = null, string? cookie = null, bool csrf = false)
    {
        var request = (HttpWebRequest)WebRequest.Create(url);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var abort = timeout.Token.Register(request.Abort);
        request.AllowAutoRedirect = false; request.Timeout = 10000; request.ReadWriteTimeout = 10000; request.KeepAlive = false;
        request.Method = method;
        request.Headers["X-Correlation-ID"] = "iis-smoke";
        if (authorization != null) request.Headers["Authorization"] = authorization;
        if (origin != null) request.Headers["Origin"] = origin;
        if (cookie != null) request.Headers["Cookie"] = "__Host-mydmsvn-rt=" + cookie;
        if (csrf) { request.Headers["X-Requested-With"] = "XMLHttpRequest"; request.Headers["Sec-Fetch-Site"] = "same-origin"; }
        // Test-only pin for the explicit existing localhost TLS certificate; no runtime callback.
        request.ServerCertificateValidationCallback = (_, certificate, _, _) => certificate != null &&
            string.Equals(certificate.GetCertHashString(), thumbprint, StringComparison.OrdinalIgnoreCase);
        if (method == "POST" || method == "PUT")
        {
            var bytes = body == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);
            request.ContentType = "application/json; charset=utf-8"; request.ContentLength = bytes.Length;
            if (bytes.Length > 0) using (var stream = await request.GetRequestStreamAsync()) await stream.WriteAsync(bytes, 0, bytes.Length);
        }
        HttpWebResponse response;
        try { response = (HttpWebResponse)await request.GetResponseAsync(); }
        catch (WebException exception) when (exception.Response is HttpWebResponse) { response = (HttpWebResponse)exception.Response; }
        using (response)
        using (var bytes = new MemoryStream())
        {
            await response.GetResponseStream().CopyToAsync(bytes);
            return new Reply((int)response.StatusCode, response.ContentType, bytes.ToArray(), response.Headers["Location"], response.Headers["X-Correlation-ID"], response.Headers["Set-Cookie"]);
        }
    }
    private sealed class Reply
    {
        public Reply(int status, string contentType, byte[] bytes, string? location, string? correlation, string? setCookie) { Status = status; ContentType = contentType; Bytes = bytes; Body = Encoding.UTF8.GetString(bytes); Location = location; Correlation = correlation; SetCookie = setCookie; }
        public byte[] Bytes { get; }
        public int Status { get; } public string ContentType { get; } public string Body { get; } public string? Location { get; } public string? Correlation { get; }
        public string? SetCookie { get; }
        public string Cookie => SetCookie!.Split(';')[0].Substring("__Host-mydmsvn-rt=".Length);
    }
    private sealed class OwnedLogin
    {
        private readonly string _admin;
        private readonly byte[] _sid = Guid.NewGuid().ToByteArray();
        private readonly string _password = "Web2!" + Guid.NewGuid().ToString("N");
        private string? _instance;
        private bool _attempted;
        public OwnedLogin(string admin) { _admin = admin; }
        public string Name { get; } = "WEB2_Smoke_" + Guid.NewGuid().ToString("N");
        public string ConnectionString(string database) => new SqlConnectionStringBuilder(_admin) { InitialCatalog = database, IntegratedSecurity = false, Authentication = SqlAuthenticationMethod.NotSpecified, UserID = Name, Password = _password, Pooling = false }.ConnectionString;
        public async Task CreateAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var connection = new SqlConnection(_admin); await connection.OpenAsync(timeout.Token);
            using var command = connection.CreateCommand(); command.CommandTimeout = 30;
            command.CommandText = "SELECT CONVERT(nvarchar(256),SERVERPROPERTY(N'ServerName'));";
            _instance = (string)await command.ExecuteScalarAsync(timeout.Token);
            if (string.IsNullOrWhiteSpace(_instance)) throw new InvalidOperationException("Test login instance identity is missing.");
            command.CommandText = "CREATE LOGIN [" + Name + "] WITH PASSWORD=N'" + _password + "', SID=0x" + BitConverter.ToString(_sid).Replace("-", "") + ", CHECK_POLICY=OFF;";
            _attempted = true; await command.ExecuteNonQueryAsync(timeout.Token);
        }
        public async Task CleanupAsync()
        {
            if (!_attempted) return;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var connection = new SqlConnection(_admin); await connection.OpenAsync(timeout.Token);
            using var command = connection.CreateCommand(); command.CommandTimeout = 30;
            command.CommandText = "SELECT CONVERT(nvarchar(256),SERVERPROPERTY(N'ServerName'));";
            if (!string.Equals(_instance, (string)await command.ExecuteScalarAsync(timeout.Token), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test login instance changed; retain SQL resources.");
            command.Parameters.AddWithValue("@name", Name); command.Parameters.AddWithValue("@sid", _sid);
            command.CommandText = "IF EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name) BEGIN " +
                "IF NOT EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name AND sid=@sid AND type='S') THROW 51998,'Test login identity changed.',1; " +
                "IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE original_security_id=@sid OR security_id=@sid) THROW 51998,'Owned login sessions have not drained.',1; " +
                "DROP LOGIN [" + Name + "]; END;";
            await command.ExecuteNonQueryAsync(timeout.Token);
        }
    }
}
