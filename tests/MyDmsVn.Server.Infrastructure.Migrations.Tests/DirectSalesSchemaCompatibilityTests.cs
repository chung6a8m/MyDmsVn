using System;
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
public sealed class DirectSalesSchemaCompatibilityTests
{
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Exact9_runtime_refuses_and_restricted_admin_upgrade_preserves_mode_and_noop()
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            Install9(db.ConnectionString);
            using var c = new SqlConnection(db.ConnectionString);
            await c.OpenAsync();
            Assert.True((await SqlSchemaCompatibilityChecker.CheckExactSchema9Async(c, CancellationToken.None)).IsCompatible);
            Assert.Equal(SchemaCompatibilityStatus.TooOld, (await new SqlSchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).Status);
            await db.VerifyOwnershipAsync(c, CancellationToken.None);
            await c.ExecuteAsync("ALTER DATABASE CURRENT SET RESTRICTED_USER;");
            c.Close();
            var result = new DatabaseMigrationRunner().Migrate(db.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            Assert.Single(result.Scripts);
            Assert.Empty(new DatabaseMigrationRunner().Migrate(db.ConnectionString).Scripts);
            await c.OpenAsync();
            Assert.Equal("RESTRICTED_USER", await c.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(60),DATABASEPROPERTYEX(DB_NAME(),'UserAccess'));"));
            Assert.Equal(SchemaCompatibilityStatus.TooNew, (await SqlSchemaCompatibilityChecker.CheckExactSchema9Async(c, CancellationToken.None)).Status);
        }
        finally { await db.DisposeAsync(); }
    }
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Exact9_multiuser_is_refused_without_schema_mutation()
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            Install9(db.ConnectionString);
            var ex = Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().Migrate(db.ConnectionString));
            Assert.Equal("Migration.MaintenanceAccessRequired", ex.Code);
            using var c = new SqlConnection(db.ConnectionString);
            Assert.Equal(9, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        finally { await db.DisposeAsync(); }
    }
    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task Every_old_prefix_remains_recovery_required_without_automatic_upgrade(int count)
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var prefix = SchemaCompatibilityPolicy.Schema9Scripts.Take(count).ToArray();
            var install = DeployChanges.To.SqlDatabase(db.ConnectionString).WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrationRunner).Assembly,
                n => prefix.Any(s => n.EndsWith(".Scripts." + s, StringComparison.Ordinal))).JournalToSqlTable("dbo", "SchemaVersions").WithTransactionPerScript().LogToNowhere().Build().PerformUpgrade();
            Assert.True(install.Successful, install.Error?.ToString());
            var ex = Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().Migrate(db.ConnectionString));
            Assert.Equal("Migration.UnsupportedExistingDatabase", ex.Code);
            using var c = new SqlConnection(db.ConnectionString);
            Assert.Equal(count, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        finally { await db.DisposeAsync(); }
    }

    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData("column")]
    [InlineData("check")]
    [InlineData("untrusted")]
    [InlineData("unique-product")]
    [InlineData("index")]
    [InlineData("foreign-key")]
    [InlineData("sequence")]
    [InlineData("trigger-disabled")]
    [InlineData("trigger-definition")]
    public async Task Every_schema10_metadata_class_is_checked_before_noop_or_runtime(string drift)
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var install = new DatabaseMigrationRunner().Migrate(db.ConnectionString);
            Assert.True(install.Successful, install.Error?.ToString());
            using var c = new SqlConnection(db.ConnectionString);
            string sql;
            switch (drift)
            {
                case "column": sql = "ALTER TABLE dbo.StockBalances ALTER COLUMN InventoryValue decimal(28,4) NULL;"; break;
                case "check": sql = "ALTER TABLE dbo.SalesReceiptLines DROP CONSTRAINT CK_SalesReceiptLines_Cost; ALTER TABLE dbo.SalesReceiptLines ADD CONSTRAINT CK_SalesReceiptLines_Cost CHECK (1=1);"; break;
                case "untrusted": sql = "ALTER TABLE dbo.StockBalances NOCHECK CONSTRAINT CK_StockBalances_EmptyValue;"; break;
                case "unique-product": sql = "CREATE UNIQUE INDEX TestProductUniqueness ON dbo.SalesReceiptLines(SaleId,ProductId);"; break;
                case "index": sql = "DROP INDEX IX_SalesReceiptLines_ProductId_SaleId ON dbo.SalesReceiptLines;"; break;
                case "foreign-key": sql = "ALTER TABLE dbo.StockValuationEntries DROP CONSTRAINT FK_StockValuationEntries_StockLedger;"; break;
                case "sequence": sql = "ALTER SEQUENCE dbo.SalesReceiptNumberSequence MAXVALUE 9999999998;"; break;
                case "trigger-disabled": sql = "DISABLE TRIGGER dbo.TR_StockValuationEntries_AppendOnly ON dbo.StockValuationEntries;"; break;
                case "trigger-definition": sql = "EXEC(N'ALTER TRIGGER dbo.TR_StockValuationEntries_AppendOnly ON dbo.StockValuationEntries INSTEAD OF UPDATE,DELETE AS RETURN;');"; break;
                default: throw new ArgumentOutOfRangeException(nameof(drift));
            }
            await c.ExecuteAsync(sql);
            Assert.False((await new SqlSchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
            Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().Migrate(db.ConnectionString));
            Assert.Equal(10, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        finally { await db.DisposeAsync(); }
    }
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Partial010_column_with_exact9_journal_is_refused_as_partial_before_mutation()
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            Install9(db.ConnectionString);
            using var c = new SqlConnection(db.ConnectionString);
            await c.ExecuteAsync("ALTER TABLE dbo.StockBalances ADD InventoryValue decimal(28,4) NULL;");
            Assert.Equal(SchemaCompatibilityStatus.Partial, (await new SqlSchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).Status);
            Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().Migrate(db.ConnectionString));
            Assert.Equal(9, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        finally { await db.DisposeAsync(); }
    }
    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData("extra-index")]
    [InlineData("missing-index")]
    [InlineData("module")]
    public async Task Exact9_complete_definition_inventory_rejects_drift_beyond_historical_per_artifact_checks(string drift)
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            Install9(db.ConnectionString);
            using var c = new SqlConnection(db.ConnectionString);
            await c.OpenAsync();
            var sql = drift == "extra-index" ? "CREATE INDEX TestExtraIndex ON dbo.Products(Name);" :
                drift == "missing-index" ? "DROP INDEX IX_StockLedger_ProductId ON dbo.StockLedger;" :
                "EXEC(N'ALTER PROCEDURE dbo.ReadSecurityPermissionImportTarget AS SELECT 1;');";
            await c.ExecuteAsync(sql);
            Assert.False((await SqlSchemaCompatibilityChecker.CheckExactSchema9Async(c, CancellationToken.None)).IsCompatible);
            Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().Migrate(db.ConnectionString));
            Assert.Equal(9, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        finally { await db.DisposeAsync(); }
    }
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Exact9_definition_fingerprint_is_frozen_independently_from_runtime10()
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            Install9(db.ConnectionString);
            using var c = new SqlConnection(db.ConnectionString);
            await c.OpenAsync();
            var actual = await SqlSchemaDefinitionFingerprint.ReadAsync(c, CancellationToken.None);
            Assert.True(SqlSchemaDefinitionFingerprint.Schema9 == actual, actual);
        }
        finally { await db.DisposeAsync(); }
    }
    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData(9, "NO CACHE")]
    [InlineData(9, "CACHE 100")]
    [InlineData(10, "NO CACHE")]
    [InlineData(10, "CACHE 100")]
    public async Task Cache_policy_changes_preserve_exact9_upgrade_and_exact10_runtime_and_noop(int version, string cachePolicy)
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            if (version == 9) Install9(db.ConnectionString);
            else Assert.True(new DatabaseMigrationRunner().Migrate(db.ConnectionString).Successful);
            using var c = new SqlConnection(db.ConnectionString);
            await c.OpenAsync();
            await db.VerifyOwnershipAsync(c, CancellationToken.None);
            await c.ExecuteAsync("ALTER SEQUENCE dbo.GoodsReceiptNumberSequence " + cachePolicy + ";");
            if (version == 10) await c.ExecuteAsync("ALTER SEQUENCE dbo.SalesReceiptNumberSequence " + cachePolicy + ";");
            var compatibility = version == 9
                ? await SqlSchemaCompatibilityChecker.CheckExactSchema9Async(c, CancellationToken.None)
                : await new SqlSchemaCompatibilityChecker(db.ConnectionString).CheckConnectionAsync(c, CancellationToken.None);
            Assert.True(compatibility.IsCompatible, compatibility.Code);
            if (version == 9) await c.ExecuteAsync("ALTER DATABASE CURRENT SET RESTRICTED_USER;");
            c.Close();
            var result = new DatabaseMigrationRunner().Migrate(db.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            Assert.Equal(version == 9 ? 1 : 0, result.Scripts.Count());
            Assert.True((await new SqlSchemaCompatibilityChecker(db.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
        }
        finally { await db.DisposeAsync(); }
    }

    [SqlServerTheory(SqlTestCategory.MigrationSchema)]
    [InlineData(9, "increment")]
    [InlineData(9, "cycle")]
    [InlineData(9, "bounds")]
    [InlineData(10, "increment")]
    [InlineData(10, "cycle")]
    [InlineData(10, "bounds")]
    public async Task Semantic_sequence_drift_remains_refused_for_both_manifests(int version, string drift)
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            if (version == 9) Install9(db.ConnectionString);
            else Assert.True(new DatabaseMigrationRunner().Migrate(db.ConnectionString).Successful);
            using var c = new SqlConnection(db.ConnectionString);
            await c.OpenAsync();
            var change = drift == "increment" ? "INCREMENT BY 2" : drift == "cycle" ? "CYCLE" : "MAXVALUE 9999999998";
            await c.ExecuteAsync("ALTER SEQUENCE dbo.GoodsReceiptNumberSequence " + change + ";");
            var compatibility = version == 9
                ? await SqlSchemaCompatibilityChecker.CheckExactSchema9Async(c, CancellationToken.None)
                : await new SqlSchemaCompatibilityChecker(db.ConnectionString).CheckConnectionAsync(c, CancellationToken.None);
            Assert.False(compatibility.IsCompatible);
            Assert.Throws<DatabaseMigrationPreflightException>(() => new DatabaseMigrationRunner().Migrate(db.ConnectionString));
        }
        finally { await db.DisposeAsync(); }
    }
    internal static void Install9(string cs)
    {
        var result = DeployChanges.To.SqlDatabase(cs).WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrationRunner).Assembly,
            n => n.Contains(".Scripts.") && !n.EndsWith("011_WebAuthSessions.sql", StringComparison.Ordinal) && SchemaCompatibilityPolicy.Schema9Scripts.Any(s => n.EndsWith(".Scripts." + s, StringComparison.Ordinal)))
            .JournalToSqlTable("dbo", "SchemaVersions").WithTransactionPerScript().LogToNowhere().Build().PerformUpgrade();
        Assert.True(result.Successful, result.Error?.ToString());
        Assert.Equal(9, result.Scripts.Count());
    }
}
