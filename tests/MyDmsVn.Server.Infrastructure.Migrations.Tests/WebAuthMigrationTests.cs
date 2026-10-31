using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using DbUp;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class WebAuthMigrationTests
{
    private static Task<SqlTestDatabase> Create() => SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Fresh11_exact_noop_and_desktop_refusal()
    {
        var db = await Create();
        Exception? scenarioFailure = null;
        try
        {
            var result = new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            Assert.Equal(11, result.Scripts.Count());
            using var c = new SqlConnection(db.ConnectionString); await c.OpenAsync();
            var fingerprint = await SqlSchemaDefinitionFingerprint.ReadAsync(c, CancellationToken.None);
            Assert.True(fingerprint == SqlSchemaDefinitionFingerprint.Schema11, fingerprint);
            Assert.True((await new SqlWebSchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
            Assert.Equal(SchemaCompatibilityStatus.TooNew, (await new SqlSchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).Status);
            Assert.Empty(new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString).Scripts);
            Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().Migrate(db.ConnectionString));
        }
        catch (Exception error) { scenarioFailure = error; throw; }
        finally
        {
            try { await db.DisposeAsync(); }
            catch (Exception cleanup) when (scenarioFailure is not null)
            {
                throw new AggregateException(scenarioFailure, cleanup);
            }
        }
    }
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Exact10_requires_restricted_drained_maintenance_before_explicit_upgrade()
    {
        var db = await Create();
        Exception? scenarioFailure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().Migrate(db.ConnectionString).Successful);
            Assert.Equal("Migration.MaintenanceAccessRequired", Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString)).Code);
            using (var c = new SqlConnection(db.ConnectionString))
            {
                await c.OpenAsync(); await db.VerifyOwnershipAsync(c, CancellationToken.None);
                await c.ExecuteAsync("ALTER DATABASE CURRENT SET RESTRICTED_USER;");
                Assert.Equal("Migration.WritersNotDrained", Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString)).Code);
            }
            var result = new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString()); Assert.Single(result.Scripts);
        }
        catch (Exception error) { scenarioFailure = error; throw; }
        finally
        {
            try { await db.DisposeAsync(); }
            catch (Exception cleanup) when (scenarioFailure is not null)
            {
                throw new AggregateException(scenarioFailure, cleanup);
            }
        }
    }
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Exact9_is_refused_without_applying10_or11()
    {
        var db = await Create();
        Exception? scenarioFailure = null;
        try
        {
            DirectSalesSchemaCompatibilityTests.Install9(db.ConnectionString);
            Assert.Equal("Migration.WebUnsupportedSource", Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString)).Code);
            using var c = new SqlConnection(db.ConnectionString);
            Assert.Equal(9, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        catch (Exception error) { scenarioFailure = error; throw; }
        finally
        {
            try { await db.DisposeAsync(); }
            catch (Exception cleanup) when (scenarioFailure is not null)
            {
                throw new AggregateException(scenarioFailure, cleanup);
            }
        }
    }
    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData("DROP INDEX IX_WebRefreshTokens_SessionId ON dbo.WebRefreshTokens;")]
    [InlineData("ALTER TABLE dbo.WebAuthSessions ADD Unexpected int NULL;")]
    [InlineData("ALTER TABLE dbo.WebRefreshTokens NOCHECK CONSTRAINT FK_WebRefreshTokens_Sessions;")]
    [InlineData("INSERT dbo.SchemaVersions(ScriptName,Applied) VALUES(N'unknown',SYSUTCDATETIME());")]
    [InlineData("INSERT dbo.SchemaVersions(ScriptName,Applied) SELECT TOP(1) ScriptName,SYSUTCDATETIME() FROM dbo.SchemaVersions;")]
    [InlineData("INSERT dbo.SchemaVersions(ScriptName,Applied) VALUES(N'012_Future.sql',SYSUTCDATETIME());")]
    public async Task Drift_unknown_duplicate_future_history_are_refused_without_repair(string mutation)
    {
        var db = await Create();
        Exception? scenarioFailure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString).Successful);
            using var c = new SqlConnection(db.ConnectionString); await c.ExecuteAsync(mutation);
            var before = await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;");
            Assert.False((await new SqlWebSchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
            Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString));
            Assert.Equal(before, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        catch (Exception error) { scenarioFailure = error; throw; }
        finally
        {
            try { await db.DisposeAsync(); }
            catch (Exception cleanup) when (scenarioFailure is not null)
            {
                throw new AggregateException(scenarioFailure, cleanup);
            }
        }
    }
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Interrupted011_rolls_back_ddl_and_journal_and_exact10_is_retained()
    {
        var db = await Create();
        Exception? scenarioFailure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().Migrate(db.ConnectionString).Successful);
            var assembly = typeof(DatabaseMigrationRunner).Assembly;
            using var reader = new StreamReader(assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Scripts.011_WebAuthSessions.sql", StringComparison.Ordinal)))!);
            var text = reader.ReadToEnd();
            var failed = DeployChanges.To.SqlDatabase(db.ConnectionString).WithScript("011_WebAuthSessions.sql", text + "\nGO\nTHROW 51999, 'Expected interruption.', 1;")
                .JournalToSqlTable("dbo", "SchemaVersions").WithTransactionPerScript().LogToNowhere().Build().PerformUpgrade();
            Assert.False(failed.Successful);
            using var c = new SqlConnection(db.ConnectionString);
            Assert.Equal(10, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
            Assert.Equal(0, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN (N'WebAuthSessions',N'WebRefreshTokens');"));
            Assert.True((await new SqlSchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
        }
        catch (Exception error) { scenarioFailure = error; throw; }
        finally
        {
            try { await db.DisposeAsync(); }
            catch (Exception cleanup) when (scenarioFailure is not null)
            {
                throw new AggregateException(scenarioFailure, cleanup);
            }
        }
    }
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Explicit_web_runner_obeys_same_migration_lock()
    {
        var db = await Create();
        Exception? scenarioFailure = null;
        try
        {
            using var c = new SqlConnection(db.ConnectionString); await c.OpenAsync();
            await c.ExecuteAsync("EXEC sys.sp_getapplock @Resource=N'MyDmsVn.SchemaMigration',@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=0;");
            Assert.Throws<DatabaseMigrationConcurrencyException>(() => new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString));
            c.Close();
            Assert.True(new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString).Successful);
        }
        catch (Exception error) { scenarioFailure = error; throw; }
        finally
        {
            try { await db.DisposeAsync(); }
            catch (Exception cleanup) when (scenarioFailure is not null)
            {
                throw new AggregateException(scenarioFailure, cleanup);
            }
        }
    }

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Explicit_web_cli_installs11_default_cli_refuses11_and_web_cli_noops()
    {
        var db = await Create();
        Exception? scenarioFailure = null;
        try
        {
            foreach (var entry in new[] { new { Argument="--web", Exit=0, Marker="11 script" }, new { Argument="", Exit=4, Marker="Migration.SchemaTooNew" }, new { Argument="--web", Exit=0, Marker="0 script" } })
            {
                var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "MyDmsVn.Server.DbMigrator.exe"), entry.Argument)
                { UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true, CreateNoWindow=true };
                start.EnvironmentVariables[SqlConnectionConfigurationLoader.MigrationConnectionStringEnvironmentVariable] = db.ConnectionString;
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000)) { process.Kill(); process.WaitForExit(); Assert.Fail("Migrator subprocess exceeded30seconds."); }
                Assert.Equal(entry.Exit, process.ExitCode);
                var text = (await output) + (await error); Assert.Contains(entry.Marker, text, StringComparison.Ordinal);
                Assert.DoesNotContain(db.ConnectionString, text, StringComparison.Ordinal);
            }
        }
        catch (Exception error) { scenarioFailure = error; throw; }
        finally
        {
            try { await db.DisposeAsync(); }
            catch (Exception cleanup) when (scenarioFailure is not null)
            {
                throw new AggregateException(scenarioFailure, cleanup);
            }
        }
    }

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Frozen_exact10_definition_is_required_before_011_and_no_partial_web_artifacts_are_adopted()
    {
        var db = await Create();
        Exception? scenarioFailure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().Migrate(db.ConnectionString).Successful);
            using var c = new SqlConnection(db.ConnectionString);
            await c.ExecuteAsync("ALTER TABLE dbo.Users ADD Unexpected int NULL;");
            Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString));
            await c.ExecuteAsync("ALTER TABLE dbo.Users DROP COLUMN Unexpected; CREATE TABLE dbo.WebAuthSessions(SessionId uniqueidentifier NOT NULL);");
            Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString));
            Assert.Equal(10, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
            Assert.Equal(0, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name=N'WebRefreshTokens';"));
        }
        catch (Exception error) { scenarioFailure = error; throw; }
        finally
        {
            try { await db.DisposeAsync(); }
            catch (Exception cleanup) when (scenarioFailure is not null)
            {
                throw new AggregateException(scenarioFailure, cleanup);
            }
        }
    }

}
