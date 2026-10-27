using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Testing.Sql;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class DirectSalesMaintenanceTests
{
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Target_sleeping_session_and_master_transaction_are_refused_until_owned_workers_drain()
    {
        var db = await Create9();
        try
        {
            using var c = new SqlConnection(db.ConnectionString);
            await c.OpenAsync();
            using (var sleeper = new SqlConnection(db.ConnectionString))
            {
                await sleeper.OpenAsync();
                Assert.Equal("Migration.WritersNotDrained", Assert.Throws<DatabaseMigrationPreflightException>(() => Schema9MaintenanceValidator.Validate(c)).Code);
            }
            var master = new SqlConnectionStringBuilder(db.ConnectionString) { InitialCatalog = "master", Pooling = false }.ConnectionString;
            using (var worker = new SqlConnection(master))
            {
                await worker.OpenAsync();
                using var tx = worker.BeginTransaction();
                using var cmd = worker.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT [" + db.DatabaseName + "].dbo.Products(Code,Name,Unit) VALUES(N'MAINT',N'Owned transaction',N'EA');";
                await cmd.ExecuteNonQueryAsync();
                try { Assert.Equal("Migration.WritersNotDrained", Assert.Throws<DatabaseMigrationPreflightException>(() => Schema9MaintenanceValidator.Validate(c)).Code); }
                finally { tx.Rollback(); }
            }
            Schema9MaintenanceValidator.Validate(c);
        }
        finally { await db.DisposeAsync(); }
    }
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Restricted_maintenance_requires_full_DMV_visibility_and_supported_SQL_login_inventory()
    {
        var db = await Create9();
        try
        {
            using var c = new SqlConnection(db.ConnectionString);
            await c.OpenAsync();
            await c.ExecuteAsync("CREATE USER T01LimitedAdmin WITHOUT LOGIN; GRANT CONTROL ON DATABASE::[" + db.DatabaseName + "] TO T01LimitedAdmin; ALTER ROLE db_owner ADD MEMBER T01LimitedAdmin; EXECUTE AS USER=N'T01LimitedAdmin';");
            try { Assert.Equal("Migration.MaintenanceVisibilityRequired", Assert.Throws<DatabaseMigrationPreflightException>(() => Schema9MaintenanceValidator.Validate(c)).Code); }
            finally { await c.ExecuteAsync("REVERT;"); }
            Assert.Equal("Migration.UnsupportedMaintenanceIdentity", Assert.Throws<DatabaseMigrationPreflightException>(() => Schema9MaintenanceValidator.Validate(c)).Code);
            await c.ExecuteAsync("ALTER ROLE db_owner DROP MEMBER T01LimitedAdmin; DROP USER T01LimitedAdmin;");
            Schema9MaintenanceValidator.Validate(c);
        }
        finally { await db.DisposeAsync(); }
    }
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Restricted_mode_fences_preexisting_master_SQL_writer_and_reconnect_and_retains_exclusion()
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? scenarioFailure = null;
        var masterCs = new SqlConnectionStringBuilder(db.ConnectionString) { InitialCatalog = "master", Pooling = false }.ConnectionString;
        var login = new OwnedTestLogin(masterCs);
        try
        {
            DirectSalesSchemaCompatibilityTests.Install9(db.ConnectionString);
            using var admin = new SqlConnection(db.ConnectionString);
            await admin.OpenAsync();
            await db.VerifyOwnershipAsync(admin, CancellationToken.None);
            await login.CreateAsync();
            await admin.ExecuteAsync("CREATE USER T01Runtime FOR LOGIN [" + login.Name + "]; GRANT SELECT,INSERT ON dbo.Products TO T01Runtime;");
            var runtimeCs = login.RuntimeConnectionString("master");
            using (var runtime = new SqlConnection(runtimeCs))
            {
                await runtime.OpenAsync();
                Assert.Equal(0, await runtime.QuerySingleAsync<int>("SELECT COUNT(*) FROM [" + db.DatabaseName + "].dbo.Products;"));
                await admin.ExecuteAsync("ALTER DATABASE CURRENT SET RESTRICTED_USER;");
                Assert.Equal("Migration.WritersNotDrained", Assert.Throws<DatabaseMigrationPreflightException>(() => Schema9MaintenanceValidator.Validate(admin)).Code);
                await Assert.ThrowsAsync<SqlException>(() => runtime.ExecuteAsync("INSERT [" + db.DatabaseName + "].dbo.Products(Code,Name,Unit) VALUES(N'BLOCKED',N'Blocked',N'EA');"));
                var targetCs = new SqlConnectionStringBuilder(runtimeCs) { InitialCatalog = db.DatabaseName }.ConnectionString;
                using var reconnect = new SqlConnection(targetCs);
                await Assert.ThrowsAsync<SqlException>(() => reconnect.OpenAsync());
                Assert.Equal(1, await runtime.QuerySingleAsync<int>("SELECT 1;"));
            }
            Schema9MaintenanceValidator.Validate(admin);
            await admin.ExecuteAsync("ALTER ROLE db_owner ADD MEMBER T01Runtime;");
            Assert.Equal("Migration.ElevatedWriterIdentity", Assert.Throws<DatabaseMigrationPreflightException>(() => Schema9MaintenanceValidator.Validate(admin)).Code);
            await admin.ExecuteAsync("ALTER ROLE db_owner DROP MEMBER T01Runtime; DROP USER T01Runtime;");
            admin.Close();
            var result = new DatabaseMigrationRunner().Migrate(db.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
        }
        catch (Exception error) { scenarioFailure = error; }
        finally
        {
            // Each resource owns its finite cleanup budget. Neither failure may skip the other.
            await CleanupAfterScenarioAsync(scenarioFailure, () => login.CleanupVerifiedAsync(), () => db.DisposeAsync());
        }
    }
    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData("SINGLE_USER")]
    [InlineData("READ_ONLY")]
    public async Task Exact9_singleuser_or_readonly_mode_is_not_supported_maintenance(string mode)
    {
        var db = await Create9();
        try
        {
            using var c = new SqlConnection(db.ConnectionString);
            await c.OpenAsync();
            await db.VerifyOwnershipAsync(c, CancellationToken.None);
            if (mode == "SINGLE_USER") await c.ExecuteAsync("ALTER DATABASE CURRENT SET SINGLE_USER;");
            else
            {
                c.ChangeDatabase("master");
                await c.ExecuteAsync("ALTER DATABASE [" + db.DatabaseName + "] SET READ_ONLY WITH NO_WAIT;");
                c.ChangeDatabase(db.DatabaseName);
            }
            Assert.Equal("Migration.MaintenanceAccessRequired", Assert.Throws<DatabaseMigrationPreflightException>(() => Schema9MaintenanceValidator.Validate(c)).Code);
            Assert.Equal(9, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        finally { await db.DisposeAsync(); }
    }
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Login_cleanup_failure_does_not_skip_owned_database_teardown_or_mask_scenario_failure()
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        var masterCs = new SqlConnectionStringBuilder(db.ConnectionString) { InitialCatalog = "master", Pooling = false }.ConnectionString;
        var login = new OwnedTestLogin(masterCs);
        Exception? testFailure = null;
        try
        {
            await login.CreateAsync();
            var scenario = new InvalidOperationException("Expected scenario failure after login creation.");
            var cleanup = new InvalidOperationException("Expected failure after login ownership verification.");
            var databaseCleanupAttempted = false;
            var ownershipVerified = false;
            var observed = await Record.ExceptionAsync(() => CleanupAfterScenarioAsync(scenario,
                () => login.CleanupVerifiedAsync(() => { ownershipVerified = true; throw cleanup; }),
                async () => { databaseCleanupAttempted = true; await db.DisposeAsync(); }));
            Assert.True(ownershipVerified);
            Assert.True(databaseCleanupAttempted, "Database cleanup was skipped after login cleanup failed.");
            Assert.True(login.IsQuarantined);
            var combined = Assert.IsType<AggregateException>(observed);
            Assert.Contains(scenario, combined.InnerExceptions);
            Assert.Contains(cleanup, combined.InnerExceptions);
            using var master = new SqlConnection(masterCs);
            Assert.Null(await master.QuerySingleAsync<int?>("SELECT DB_ID(@name);", new { name = db.DatabaseName }));
            // Explicit recovery of this injected failure still re-verifies exact generated name/SID and drain.
            await login.CleanupVerifiedAsync();
            Assert.False(login.IsQuarantined);
        }
        catch (Exception error) { testFailure = error; }
        finally
        {
            await CleanupAfterScenarioAsync(testFailure, () => login.CleanupVerifiedAsync(), () => db.DisposeAsync());
        }
    }

    [Fact]
    public async Task Both_cleanup_failures_preserve_the_original_scenario_and_each_independent_error()
    {
        var scenario = new InvalidOperationException("Expected original scenario.");
        var login = new InvalidOperationException("Expected login cleanup error.");
        var database = new InvalidOperationException("Expected database cleanup error.");
        var attempted = new List<string>();
        var combined = await Assert.ThrowsAsync<AggregateException>(() => CleanupAfterScenarioAsync(scenario,
            () => { attempted.Add("login"); return Task.FromException(login); },
            () => { attempted.Add("database"); return Task.FromException(database); }));
        Assert.Equal(new[] { "login", "database" }, attempted);
        Assert.Equal(new Exception[] { scenario, login, database }, combined.InnerExceptions);
    }
    // Kept test-local; no global resource runner or production fault API.
    private static async Task CleanupAfterScenarioAsync(Exception? scenario, Func<Task> loginCleanup, Func<Task> databaseCleanup)
    {
        var failures = new List<Exception>();
        if (scenario != null) failures.Add(scenario);
        try { await loginCleanup(); } catch (Exception error) { failures.Add(error); }
        try { await databaseCleanup(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Scenario and owned resource cleanup failed.", failures);
    }

    private sealed class OwnedTestLogin
    {
        private readonly string _masterConnectionString;
        private readonly string _password = "T01!" + Guid.NewGuid().ToString("N");
        // Allocate identity before CREATE: a lost CREATE response cannot lose the tracked ownership SID.
        private readonly byte[] _sid = Guid.NewGuid().ToByteArray();
        private bool _creationAttempted;
        private string? _serverIdentity;
        public OwnedTestLogin(string masterConnectionString) => _masterConnectionString = masterConnectionString;
        public string Name { get; } = "SAL_T01_" + Guid.NewGuid().ToString("N");
        public bool IsQuarantined { get; private set; }
        public string RuntimeConnectionString(string database) => new SqlConnectionStringBuilder(_masterConnectionString)
        {
            InitialCatalog = database,
            IntegratedSecurity = false,
            Authentication = SqlAuthenticationMethod.NotSpecified,
            UserID = Name,
            Password = _password,
            Pooling = false,
        }.ConnectionString;
        public async Task CreateAsync()
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            using var connection = new SqlConnection(_masterConnectionString);
            await connection.OpenAsync(budget.Token);
            using (budget.Token.Register(connection.Close))
            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = 30;
                command.CommandText = "SELECT CONVERT(nvarchar(256),SERVERPROPERTY(N'ServerName'));";
                _serverIdentity = await command.ExecuteScalarAsync(budget.Token) as string;
                if (string.IsNullOrWhiteSpace(_serverIdentity)) throw new InvalidOperationException("Cannot establish login creation instance identity.");
                command.CommandText = "CREATE LOGIN [" + Name + "] WITH PASSWORD=N'" + _password + "', SID=0x" +
                    BitConverter.ToString(_sid).Replace("-", string.Empty) + ", CHECK_POLICY=OFF;";
                _creationAttempted = true;
                try { await command.ExecuteNonQueryAsync(budget.Token); }
                catch { IsQuarantined = true; throw; }
            }
        }
        public async Task CleanupVerifiedAsync(Action? afterOwnershipVerified = null)
        {
            if (!_creationAttempted) return;
            using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            using var connection = new SqlConnection(_masterConnectionString);
            try
            {
                await connection.OpenAsync(budget.Token);
                using (budget.Token.Register(connection.Close))
                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = 30;
                    command.CommandText = "SELECT CONVERT(nvarchar(256),SERVERPROPERTY(N'ServerName'));";
                    var serverIdentity = await command.ExecuteScalarAsync(budget.Token) as string;
                    if (string.IsNullOrWhiteSpace(_serverIdentity) || !string.Equals(_serverIdentity, serverIdentity, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Login cleanup instance changed or is unknown; retain quarantine without mutation.");
                    command.Parameters.AddWithValue("@name", Name);
                    command.Parameters.AddWithValue("@sid", _sid);
                    command.CommandText = "SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name) THEN 0 " +
                        "WHEN EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name AND sid=@sid AND type='S') THEN 1 ELSE 2 END;";
                    var identity = Convert.ToInt32(await command.ExecuteScalarAsync(budget.Token));
                    if (identity == 0) { IsQuarantined = false; return; }
                    if (identity != 1) throw new InvalidOperationException("Owned login identity changed; retain quarantine without mutation.");
                    command.CommandText = "SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE original_security_id=@sid OR security_id=@sid;";
                    if (Convert.ToInt32(await command.ExecuteScalarAsync(budget.Token)) != 0)
                        throw new InvalidOperationException("Owned login sessions have not drained; retain quarantine without mutation.");
                    afterOwnershipVerified?.Invoke();
                    // Recheck ownership/drain immediately in the mutation batch; never drop by prefix/name alone.
                    command.CommandText = "IF NOT EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name AND sid=@sid AND type='S') " +
                        "THROW 51998,'Owned login identity changed.',1; " +
                        "IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE original_security_id=@sid OR security_id=@sid) " +
                        "THROW 51998,'Owned login sessions have not drained.',1; DROP LOGIN [" + Name + "];";
                    await command.ExecuteNonQueryAsync(budget.Token);
                    IsQuarantined = false;
                }
            }
            catch { IsQuarantined = true; throw; }
        }
    }
    private static async Task<SqlTestDatabase> Create9()
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            DirectSalesSchemaCompatibilityTests.Install9(db.ConnectionString);
            using var c = new SqlConnection(db.ConnectionString);
            await c.OpenAsync();
            await db.VerifyOwnershipAsync(c, CancellationToken.None);
            await c.ExecuteAsync("ALTER DATABASE CURRENT SET RESTRICTED_USER;");
            return db;
        }
        catch { await db.DisposeAsync(); throw; }
    }
}
