using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Owin.Testing;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.HttpHost;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Actual OWIN routes + Application handlers + SQL stores. Each opened runtime connection
// executes as the provisioned restricted database user; admin is reserved for arrangement/oracles.
// This proves the HTTP/SQL boundary, not System.Web hosting or a browser's cookie enforcement.
public sealed class WebAuthHttpSqlTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Login_me_rotate_logout_rejects_the_issued_unexpired_JWT() => Owned(async test =>
    {
        var first = await test.Login("alice");
        Assert.Equal(test.Alice, (int)first.Json["data"]!["user"]!["userId"]!);
        AssertCookie(first, cleared: false);
        Assert.True(first.Body.IndexOf(first.Cookie, StringComparison.Ordinal) < 0, "The raw refresh token appeared in JSON.");
        Assert.Null(first.Json["data"]!["refreshToken"]);
        var token = first.Access;
        Assert.Equal(200, (await test.Get("auth/me", token)).Status);
        var next = await test.Post("auth/refresh", cookie: first.Cookie);
        Assert.Equal(200, next.Status);
        Assert.NotEqual(first.Cookie, next.Cookie);
        AssertCookie(next, cleared: false);
        Assert.Equal(200, (await test.Get("auth/me", next.Access)).Status);
        var logout = await test.Post("auth/logout", cookie: next.Cookie);
        Assert.Equal(200, logout.Status);
        AssertCookie(logout, cleared: true);
        Assert.Equal(401, (await test.Get("auth/me", token)).Status);
        Assert.Equal(401, (await test.Get("auth/me", next.Access)).Status);
        Assert.Equal(401, (await test.Post("auth/refresh", cookie: next.Cookie)).Status);
        Assert.Equal(200, (await test.Post("auth/logout", cookie: next.Cookie)).Status);
        Assert.Equal(1, await test.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebAuthSessions WHERE RevokedAtUtc IS NOT NULL;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Login_reuses_password_rehash_through_restricted_SQL_permissions() => Owned(async test =>
    {
        var oldHash = new BcryptPasswordHasher(10).Hash(Scenario.Password);
        await test.Admin.ExecuteAsync("UPDATE dbo.Users SET PasswordHash=@oldHash WHERE UserId=@id;", new { oldHash, id = test.Alice });
        var signedIn = await test.Login("alice");
        Assert.Equal(200, (await test.Get("auth/me", signedIn.Access)).Status);
        var newHash = await test.Admin.QuerySingleAsync<string>("SELECT PasswordHash FROM dbo.Users WHERE UserId=@id;", new { id = test.Alice });
        Assert.True(!string.Equals(oldHash, newHash, StringComparison.Ordinal), "The legacy work factor was not rehashed.");
        var hasher = new BcryptPasswordHasher();
        Assert.False(hasher.NeedsRehash(newHash));
        Assert.True(hasher.Verify(Scenario.Password, newHash));
        Assert.Equal(test.Alice, await test.Admin.QuerySingleAsync<int>("SELECT UpdatedByUserId FROM dbo.Users WHERE UserId=@id;", new { id = test.Alice }));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Disabled_account_denies_login_and_existing_access_and_refresh() => Owned(async test =>
    {
        var login = await test.Login("alice");
        await test.Admin.ExecuteAsync("UPDATE dbo.Users SET IsActive=0 WHERE UserId=@id;", new { id = test.Alice });
        Assert.Equal(401, (await test.Get("auth/me", login.Access)).Status);
        Assert.Equal(401, (await test.Post("auth/refresh", cookie: login.Cookie)).Status);
        var disabled = await test.Post("auth/login", Scenario.Credentials("alice", Scenario.Password));
        var wrong = await test.Post("auth/login", Scenario.Credentials("bob", "wrong-password"));
        var unknown = await test.Post("auth/login", Scenario.Credentials("missing", "wrong-password"));
        foreach (var denied in new[] { disabled, wrong, unknown })
        {
            Assert.Equal(401, denied.Status);
            Assert.Equal("Auth.InvalidCredentials", (string?)denied.Json["error"]!["code"]);
            Assert.Null(denied.Json["data"]);
            Assert.Empty(denied.SetCookies);
            Assert.DoesNotContain(Scenario.Password, denied.Body);
        }
        Assert.Equal(1, await test.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebAuthSessions;"));
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Concurrent_refresh_has_one_child_and_replay_revokes_only_that_actor_family() => Owned(async test =>
    {
        var alice = await test.Login("alice");
        var bob = await test.Login("bob");
        var requests = new[] { test.Post("auth/refresh", cookie: alice.Cookie), test.Post("auth/refresh", cookie: alice.Cookie) };
        var replies = await Task.WhenAll(requests);
        Assert.Equal(new[] { 200, 401 }, replies.Select(reply => reply.Status).OrderBy(status => status).ToArray());
        var winner = replies.Single(reply => reply.Status == 200);
        Assert.Equal(401, (await test.Get("auth/me", winner.Access)).Status);
        Assert.Equal(401, (await test.Post("auth/refresh", cookie: winner.Cookie)).Status);
        Assert.Equal(200, (await test.Get("auth/me", bob.Access)).Status);
        Assert.Equal(200, (await test.Post("auth/refresh", cookie: bob.Cookie)).Status);
        Assert.Equal(2, await test.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens t JOIN dbo.WebAuthSessions s ON s.SessionId=t.SessionId WHERE s.UserId=@id;", new { id = test.Alice }));
        Assert.Contains(test.Audit.Entries, entry => entry.DocumentType == "WebAuthSessionReplay" && entry.UserId == test.Alice && entry.Outcome == SecurityAuditOutcome.Denied);
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Capabilities_use_independent_current_SQL_permissions_and_request_actors() => Owned(async test =>
    {
        await test.Admin.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@alice,N'Sales.Receipts.Write',1),(@alice,N'Sales.Receipts.Post',0),(@bob,N'Sales.Receipts.Read',1);", new { alice = test.Alice, bob = test.Bob });
        var alice = await test.Login("alice");
        var bob = await test.Login("bob");
        for (var round = 0; round < 3; round++)
        {
            var replies = await Task.WhenAll(test.Get("auth/me", alice.Access), test.Get("auth/me", bob.Access),
                test.Get("auth/capabilities?feature=sales", alice.Access), test.Get("auth/capabilities?feature=sales", bob.Access));
            Assert.Equal(test.Alice, (int)replies[0].Json["data"]!["userId"]!);
            Assert.Equal(test.Bob, (int)replies[1].Json["data"]!["userId"]!);
            AssertCapabilities(replies[2], false, true, false);
            AssertCapabilities(replies[3], true, false, false);
        }
        await test.Admin.ExecuteAsync("UPDATE dbo.UserPermissions SET Granted=0 WHERE UserId=@alice AND PermissionKey=N'Sales.Receipts.Write';", new { alice = test.Alice });
        // Existing Post deny is intentionally changed separately, proving there is no grant cached in JWT.
        await test.Admin.ExecuteAsync("UPDATE dbo.UserPermissions SET Granted=1 WHERE UserId=@alice AND PermissionKey=N'Sales.Receipts.Post';", new { alice = test.Alice });
        AssertCapabilities(await test.Get("auth/capabilities?feature=sales", alice.Access), false, false, true);
        AssertCapabilities(await test.Get("auth/capabilities?feature=sales", bob.Access), true, false, false);
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Session_persistence_failure_returns_no_access_or_cookie_and_rolls_back() => Owned(async test =>
    {
        await test.Admin.ExecuteAsync("CREATE TRIGGER dbo.WebAuthHttpRejectIssue ON dbo.WebRefreshTokens AFTER INSERT AS BEGIN THROW 51992,'Injected failure.',1; END;");
        var denied = await test.Post("auth/login", Scenario.Credentials("alice", Scenario.Password));
        Assert.Equal(500, denied.Status);
        Assert.Null(denied.Json["data"]);
        Assert.Empty(denied.SetCookies);
        Assert.DoesNotContain("Injected failure", denied.Body);
        Assert.DoesNotContain(Scenario.Password, denied.Body);
        Assert.Equal(0, await test.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebAuthSessions;"));
        Assert.Equal(0, await test.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Login_and_replay_audit_omit_password_access_and_raw_refresh() => Owned(async test =>
    {
        var login = await test.Login("alice");
        test.Audit.ThrowOnRefreshSuccess = true;
        var rotated = await test.Post("auth/refresh", cookie: login.Cookie);
        Assert.Equal(200, rotated.Status);
        Assert.Equal(1, test.Audit.RefreshFailures);
        Assert.Equal(2, await test.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens;"));
        Assert.Equal(1, await test.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebRefreshTokens WHERE ConsumedAtUtc IS NOT NULL;"));
        Assert.Equal(200, (await test.Get("auth/me", rotated.Access)).Status);
        // Emulate losing the rotated cookie response: retrying the old cookie must revoke
        // the committed family even though its success audit sink failed after commit.
        Assert.Equal(401, (await test.Post("auth/refresh", cookie: login.Cookie)).Status);
        Assert.Equal(401, (await test.Get("auth/me", rotated.Access)).Status);
        Assert.Equal(1, await test.Admin.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebAuthSessions WHERE RevokedAtUtc IS NOT NULL;"));
        var serialized = JsonConvert.SerializeObject(test.Audit.Entries);
        Assert.Contains(test.Audit.Entries, entry => entry.Action == SecurityAuditAction.Authentication && entry.UserId == test.Alice);
        Assert.Contains(test.Audit.Entries, entry => entry.DocumentType == "WebAuthSessionReplay");
        foreach (var secret in new[] { Scenario.Password, login.Access, login.Cookie, rotated.Access, rotated.Cookie })
            Assert.False(serialized.Contains(secret), "An auth secret appeared in the audit sink.");
        var hash = await test.Admin.QuerySingleAsync<byte[]>("SELECT TOP(1) TokenHash FROM dbo.WebRefreshTokens;");
        Assert.Equal(32, hash.Length);
    });

    private static void AssertCapabilities(Reply reply, bool read, bool write, bool post)
    {
        Assert.Equal(200, reply.Status);
        Assert.Equal(read, (bool)reply.Json["data"]!["read"]!);
        Assert.Equal(write, (bool)reply.Json["data"]!["write"]!);
        Assert.Equal(post, (bool)reply.Json["data"]!["post"]!);
    }

    private static void AssertCookie(Reply reply, bool cleared)
    {
        var cookie = Assert.Single(reply.SetCookies);
        Assert.StartsWith("__Host-mydmsvn-rt=", cookie);
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        Assert.Contains("path=/", cookie.ToLowerInvariant());
        Assert.DoesNotContain("domain=", cookie.ToLowerInvariant());
        if (cleared) Assert.Contains("max-age=0", cookie.ToLowerInvariant());
        else Assert.True(Guid.TryParseExact(reply.Cookie, "D", out _));
    }

    private static async Task Owned(Func<Scenario, Task> body)
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? failure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().MigrateWeb(database.ConnectionString).Successful);
            using var scenario = await Scenario.Create(database.ConnectionString);
            await body(scenario);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try { await database.DisposeAsync(); }
            catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); }
        }
    }

    private sealed class Scenario : IDisposable
    {
        private const string Origin = "https://web2.mydmsvn.test";
        public const string Password = "HttpSql_test-only-password_2026!";
        public SqlConnection Admin { get; }
        public AuditSink Audit { get; } = new AuditSink();
        public int Alice { get; private set; }
        public int Bob { get; private set; }
        private ServiceProvider? _provider;
        private TestServer? _server;
        private Scenario(string connectionString) { Admin = new SqlConnection(connectionString); }
        public static async Task<Scenario> Create(string connectionString)
        {
            var test = new Scenario(connectionString);
            try
            {
                await test.Admin.OpenAsync();
                await test.Admin.ExecuteAsync("CREATE USER WebHttpProbe WITHOUT LOGIN; CREATE USER WebHttpMigration WITHOUT LOGIN;");
                await test.Admin.ExecuteAsync(ReadProvisioning().Replace("$(WebRuntimeUser)", "WebHttpProbe").Replace("$(MigrationUser)", "WebHttpMigration"));
                var hash = new BcryptPasswordHasher().Hash(Password);
                foreach (var name in new[] { "alice", "bob" })
                {
                    var id = await test.Admin.QuerySingleAsync<int>("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(@name,@normalized,@name,N'Local',@hash,N'',N'BCrypt');", new { name, normalized = name.ToUpperInvariant(), hash });
                    if (name == "alice") test.Alice = id; else test.Bob = id;
                }
                var factory = new RestrictedFactory(connectionString);
                using (var runtime = (SqlConnection)await factory.OpenConnectionAsync(CancellationToken.None))
                    await new SqlWebRuntimePrincipalValidator().ValidateConnectionAsync(runtime, CancellationToken.None);
                var services = new ServiceCollection();
                services.AddServerApplication().AddHttpRequestIdentity();
                services.AddSqlPersistence(connectionString);
                services.Replace(ServiceDescriptor.Singleton<IDbConnectionFactory>(factory));
                services.Replace(ServiceDescriptor.Singleton<ISecurityAuditSink>(test.Audit));
                test._provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                var options = new WebHostOptions(Origin, Origin, "http-sql-tests", "sql-test", new Dictionary<string, byte[]> { ["sql-test"] = Encoding.UTF8.GetBytes("test-only-key-for-real-sql-http-auth-boundary") });
                test._server = TestServer.Create(app => Web2Pipeline.Configure(app, options, test._provider));
                return test;
            }
            catch { test.Dispose(); throw; }
        }
        public static string Credentials(string username, string password) => JsonConvert.SerializeObject(new { username, password });
        public async Task<Reply> Login(string username)
        {
            var reply = await Post("auth/login", Credentials(username, Password));
            Assert.Equal(200, reply.Status);
            Assert.False(string.IsNullOrEmpty(reply.Access));
            return reply;
        }
        public Task<Reply> Get(string path, string access) => Request(HttpMethod.Get, path, null, null, access);
        public Task<Reply> Post(string path, string? body = null, string? cookie = null) => Request(HttpMethod.Post, path, body, cookie, null);
        private async Task<Reply> Request(HttpMethod method, string path, string? body, string? cookie, string? access)
        {
            using var request = new HttpRequestMessage(method, Origin + "/api/v1/" + path);
            request.Headers.TryAddWithoutValidation("Origin", Origin);
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", "http-sql-test");
            if (cookie != null) request.Headers.TryAddWithoutValidation("Cookie", "__Host-mydmsvn-rt=" + cookie);
            if (access != null) request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + access);
            if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            using var response = await _server!.HttpClient.SendAsync(request, timeout.Token);
            return new Reply((int)response.StatusCode, await response.Content.ReadAsStringAsync(), response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToArray() : Array.Empty<string>());
        }
        public void Dispose() { _server?.Dispose(); _provider?.Dispose(); Admin.Dispose(); }
        private static string ReadProvisioning()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName, "deployment/sql/Provision-MyDmsVnWebPrincipals.sql");
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            throw new FileNotFoundException("Provision-MyDmsVnWebPrincipals.sql");
        }
    }
    private sealed class Reply
    {
        public Reply(int status, string body, string[] cookies) { Status = status; Body = body; SetCookies = cookies; Json = JObject.Parse(body); }
        public int Status { get; }
        public string Body { get; }
        public JObject Json { get; }
        public string[] SetCookies { get; }
        public string Access => (string?)Json["data"]?["accessToken"] ?? "";
        public string Cookie => SetCookies.Single().Split(';')[0].Substring("__Host-mydmsvn-rt=".Length);
    }
    private sealed class RestrictedFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        public RestrictedFactory(string connectionString) { _connectionString = connectionString; }
        public IDbConnection OpenConnection() => OpenConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
        public async Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(_connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                using var command = connection.CreateCommand();
                command.CommandTimeout = 30;
                command.CommandText = "EXECUTE AS USER=N'WebHttpProbe';";
                await command.ExecuteNonQueryAsync(cancellationToken);
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }
    }
    private sealed class AuditSink : ISecurityAuditSink
    {
        private int _refreshFailures;
        public bool ThrowOnRefreshSuccess { get; set; }
        public int RefreshFailures => _refreshFailures;
        public ConcurrentQueue<SecurityAuditEntry> Entries { get; } = new ConcurrentQueue<SecurityAuditEntry>();
        public Task WriteAsync(SecurityAuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Enqueue(entry);
            if (ThrowOnRefreshSuccess && entry.DocumentType == "WebAuthRefresh" && entry.Outcome == SecurityAuditOutcome.Succeeded)
            {
                Interlocked.Increment(ref _refreshFailures);
                throw new InvalidOperationException("Injected audit sink failure.");
            }
            return Task.CompletedTask;
        }
    }
}
