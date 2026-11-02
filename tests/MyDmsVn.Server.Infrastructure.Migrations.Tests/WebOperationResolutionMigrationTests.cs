using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbUp;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationResolutionMigrationTests
{
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public Task Exact12_upgrade_preserves_existing_journal_bytes_tombstones_and_epoch() => Owned(async db =>
    {
        Install(db.ConnectionString, 12);
        string before; Guid epoch;
        using (var sql = new SqlConnection(db.ConnectionString))
        {
            await sql.OpenAsync(); await db.VerifyOwnershipAsync(sql, CancellationToken.None);
            epoch = await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;");
            await sql.ExecuteAsync(@"DECLARE @n datetime2(7)=SYSUTCDATETIME();
INSERT dbo.WebOperationJournal VALUES(1,NEWID(),@epoch,1,1,1,NULL,CONVERT(binary(32),0x07),41,'COMMITTED',0x7B7D,@n,@n,DATEADD(day,7,@n));
INSERT dbo.WebOperationJournal VALUES(1,NEWID(),@epoch,1,1,1,NULL,CONVERT(binary(32),0x08),NULL,'CANCELLED',NULL,@n,@n,NULL);", new { epoch });
            before = await sql.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.WebOperationJournal ORDER BY OperationId FOR JSON PATH,INCLUDE_NULL_VALUES);");
            await sql.ExecuteAsync("ALTER DATABASE CURRENT SET RESTRICTED_USER;");
        }
        Assert.True(new DatabaseMigrationRunner().MigrateWeb5(db.ConnectionString).Successful);
        using var after = new SqlConnection(db.ConnectionString);
        Assert.Equal(epoch, await after.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;"));
        Assert.Equal(before, await after.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.WebOperationJournal ORDER BY OperationId FOR JSON PATH,INCLUDE_NULL_VALUES);"));
        Assert.Equal(0, await after.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
        Assert.True((await new SqlWeb5SchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
    });

    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData("DISABLE TRIGGER dbo.TR_WebOperationJournal_TerminalAuthority ON dbo.WebOperationJournal;")]
    [InlineData("DISABLE TRIGGER dbo.TR_WebOperationTerminalAudit_Immutable ON dbo.WebOperationTerminalAudit;")]
    public Task Disabled_terminal_authority_or_audit_guard_refuses_exact13_startup_and_noop(string mutation) => Owned(async db =>
    {
        Assert.True(new DatabaseMigrationRunner().MigrateWeb5(db.ConnectionString).Successful);
        using var sql = new SqlConnection(db.ConnectionString); await sql.ExecuteAsync(mutation);
        Assert.False((await new SqlWeb5SchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
        Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().MigrateWeb5(db.ConnectionString));
        Assert.Equal(13, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        Assert.Equal(0, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
    });

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public Task Fresh13_is_exact_restart_is_noop_and_older_server_modes_refuse() => Owned(async db =>
    {
        var runner = new DatabaseMigrationRunner();
        var installed = runner.MigrateWeb5(db.ConnectionString);
        Assert.True(installed.Successful); Assert.Equal(13, installed.Scripts.Count());
        Assert.True((await new SqlWeb5SchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
        using var sql = new SqlConnection(db.ConnectionString);
        var epoch = await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;");
        Assert.Empty(runner.MigrateWeb5(db.ConnectionString).Scripts);
        Assert.Equal(epoch, await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;"));
        Assert.Equal(SchemaCompatibilityStatus.TooNew, (await new SqlWebSchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).Status);
        Assert.Equal(SchemaCompatibilityStatus.TooNew, (await new SqlWebSchema11CompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).Status);
        Assert.Equal(SchemaCompatibilityStatus.TooNew, (await new SqlSchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).Status);
        Assert.Throws<DatabaseMigrationPreflightException>(() => runner.MigrateWeb(db.ConnectionString));
        Assert.Throws<DatabaseMigrationPreflightException>(() => runner.Migrate(db.ConnectionString));
    });

    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public Task Allowed_source_requires_restricted_drained_maintenance_and_preserves_existing_epoch(int source) => Owned(async db =>
    {
        Install(db.ConnectionString, source);
        var runner = new DatabaseMigrationRunner();
        Assert.Equal("Migration.MaintenanceAccessRequired", Assert.Throws<DatabaseMigrationPreflightException>(() => runner.MigrateWeb5(db.ConnectionString)).Code);
        Guid? epoch = null;
        using (var sql = new SqlConnection(db.ConnectionString))
        {
            await sql.OpenAsync(); await db.VerifyOwnershipAsync(sql, CancellationToken.None);
            if (source == 12) epoch = await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;");
            await sql.ExecuteAsync("ALTER DATABASE CURRENT SET RESTRICTED_USER;");
            Assert.Equal("Migration.WritersNotDrained", Assert.Throws<DatabaseMigrationPreflightException>(() => runner.MigrateWeb5(db.ConnectionString)).Code);
            Assert.Equal(source, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        var result = runner.MigrateWeb5(db.ConnectionString);
        Assert.True(result.Successful); Assert.Equal(13 - source, result.Scripts.Count());
        Assert.True((await new SqlWeb5SchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
        using var after = new SqlConnection(db.ConnectionString);
        if (epoch.HasValue) Assert.Equal(epoch.Value, await after.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;"));
        Assert.Empty(runner.MigrateWeb5(db.ConnectionString).Scripts);
    });

    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData("ALTER TABLE dbo.WebOperationJournal ADD Spoof int NULL;")]
    [InlineData("DISABLE TRIGGER dbo.TR_WebOperationJournal_Immutable ON dbo.WebOperationJournal;")]
    [InlineData("CREATE TABLE dbo.WebOperationTerminalAudit(Spoof int NOT NULL);")]
    [InlineData("INSERT dbo.SchemaVersions(ScriptName,Applied) VALUES(N'013_Spoof.sql',SYSUTCDATETIME());")]
    [InlineData("INSERT dbo.SchemaVersions(ScriptName,Applied) VALUES(N'014_Future.sql',SYSUTCDATETIME());")]
    [InlineData("INSERT dbo.SchemaVersions(ScriptName,Applied) SELECT TOP(1) ScriptName,SYSUTCDATETIME() FROM dbo.SchemaVersions;")]
    public Task Drift_partial_or_forged_history_never_adopts_or_repairs_source(string mutation) => Owned(async db =>
    {
        Install(db.ConnectionString, 12);
        using var sql = new SqlConnection(db.ConnectionString);
        await sql.OpenAsync(); await sql.ExecuteAsync(mutation);
        var before = await sql.QuerySingleAsync<string>("SELECT ScriptName,Applied FROM dbo.SchemaVersions ORDER BY ScriptName,Applied FOR JSON PATH;");
        sql.Close();
        Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().MigrateWeb5(db.ConnectionString));
        Assert.Equal(before, await sql.QuerySingleAsync<string>("SELECT ScriptName,Applied FROM dbo.SchemaVersions ORDER BY ScriptName,Applied FOR JSON PATH;"));
        Assert.False((await new SqlWeb5SchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
    });

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public Task Interrupted013_rolls_back_objects_and_history_then_exact12_can_upgrade() => Owned(async db =>
    {
        Install(db.ConnectionString, 12);
        var assembly = typeof(DatabaseMigrationRunner).Assembly;
        var name = assembly.GetManifestResourceNames().Single(value => value.Contains(".Scripts.013_"));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        var failed = DeployChanges.To.SqlDatabase(db.ConnectionString).WithScript(name.Substring(name.IndexOf(".Scripts.", StringComparison.Ordinal) + 9), reader.ReadToEnd() + "\nGO\nTHROW 51998, 'Expected interruption.',1;")
            .JournalToSqlTable("dbo", "SchemaVersions").WithTransactionPerScript().LogToNowhere().Build().PerformUpgrade();
        Assert.False(failed.Successful);
        Assert.True((await new SqlWebSchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
        using (var sql = new SqlConnection(db.ConnectionString))
        {
            await sql.OpenAsync(); await db.VerifyOwnershipAsync(sql, CancellationToken.None);
            Assert.Equal(12, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
            Assert.Equal(0, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.objects WHERE name IN(N'WebOperationTerminalAudit',N'RecordWebOperationNotExecuted');"));
            await sql.ExecuteAsync("ALTER DATABASE CURRENT SET RESTRICTED_USER;");
        }
        Assert.True(new DatabaseMigrationRunner().MigrateWeb5(db.ConnectionString).Successful);
        Assert.True((await new SqlWeb5SchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
    });

    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public Task Web5_upgrade_obeys_existing_migration_lock() => Owned(async db =>
    {
        using (var holder = new SqlConnection(db.ConnectionString))
        {
            await holder.OpenAsync();
            await holder.ExecuteAsync("EXEC sys.sp_getapplock @Resource=N'MyDmsVn.SchemaMigration',@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=0;");
            Assert.Throws<DatabaseMigrationConcurrencyException>(() => new DatabaseMigrationRunner().MigrateWeb5(db.ConnectionString));
        }
        Assert.True(new DatabaseMigrationRunner().MigrateWeb5(db.ConnectionString).Successful);
    });

    private static void Install(string connection, int count)
    {
        if (count == 10) { Assert.True(new DatabaseMigrationRunner().Migrate(connection).Successful); return; }
        if (count == 12) { Assert.True(new DatabaseMigrationRunner().MigrateWeb(connection).Successful); return; }
        Assert.True(new DatabaseMigrationRunner().Migrate(connection).Successful);
        Assert.True(DeployChanges.To.SqlDatabase(connection).WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrationRunner).Assembly, name => name.EndsWith(".Scripts.011_WebAuthSessions.sql", StringComparison.Ordinal))
            .JournalToSqlTable("dbo", "SchemaVersions").WithTransactionPerScript().LogToNowhere().Build().PerformUpgrade().Successful);
    }

    private static async Task Owned(Func<SqlTestDatabase, Task> body)
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? failure = null;
        try { await body(db); }
        catch (Exception error) { failure = error; throw; }
        finally { try { await db.DisposeAsync(); } catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); } }
    }
}
