using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Explicit packaged-host selector. Requires a published Release host and the operator's
// existing localhost:44321 IIS Express TLS binding. Never changes machine TLS/IIS config.
public sealed class WebHostIisExpressTests
{
    [WebHostSqlFact, Trait("Category", "WebHostIntegration")]
    public async Task Published_SystemWeb_host_warms_up_with_restricted_schema11_and_serves_safe_HTTP()
    {
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
        int customer = 0, warehouse = 0, employee = 0, product = 0;
        try
        {
            var migration = new DatabaseMigrationRunner().MigrateWeb(database.ConnectionString);
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
                customer = await connection.QuerySingleAsync<int>("INSERT dbo.Customers(Code,Name) OUTPUT INSERTED.CustomerId VALUES(N'SMOKE-C',N'Smoke Customer');");
                warehouse = await connection.QuerySingleAsync<int>("INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'SMOKE-W',N'Smoke Warehouse');");
                employee = await connection.QuerySingleAsync<int>("INSERT dbo.Employees(Code,Name) OUTPUT INSERTED.EmployeeId VALUES(N'SMOKE-E',N'Smoke Employee');");
                product = await connection.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'SMOKE-P',N'Smoke Product',N'EA');");
                await connection.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@warehouse,@product,20,300);", new { warehouse, product });
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
            Assert.Equal(200, root.Status); Assert.Equal(200, fallback.Status); Assert.Equal(root.Body, fallback.Body);
            Assert.Contains("text/html", fallback.ContentType);
            Assert.Equal(404, (await Request("https://localhost:44321/missing.js", thumbprint!)).Status);
            Assert.Equal(400, (await Request("http://localhost:18081/api/v1/health", thumbprint!)).Status);
            var crossOrigin = await Request("https://localhost:44321/api/v1/health", thumbprint!, origin: "https://outside.invalid");
            Assert.Equal(403, crossOrigin.Status);
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
            if (drained)
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
    private sealed class WebHostSqlFactAttribute : FactAttribute
    {
        public WebHostSqlFactAttribute()
        {
            foreach (var variable in new[] { SqlTestDatabase.ConnectionStringEnvironmentVariable, "MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY", "MYDMSVN_WEB_TEST_TLS_THUMBPRINT" })
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
                    Skip = "Packaged IIS Express SQL test requires explicit publish, TLS pin and nonproduction SQL configuration.";
        }
    }
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
        using (var reader = new StreamReader(response.GetResponseStream()))
            return new Reply((int)response.StatusCode, response.ContentType, await reader.ReadToEndAsync(), response.Headers["Location"], response.Headers["X-Correlation-ID"], response.Headers["Set-Cookie"]);
    }
    private sealed class Reply
    {
        public Reply(int status, string contentType, string body, string? location, string? correlation, string? setCookie) { Status = status; ContentType = contentType; Body = body; Location = location; Correlation = correlation; SetCookie = setCookie; }
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
