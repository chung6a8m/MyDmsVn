using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

internal sealed partial class GuiSqlEnvironment
{
    internal static async Task RunSchemaTransitionAsync(string package, string schema9Package,
        Func<GuiSqlEnvironment, Task> body)
    {
        GuiTestSafety.ThrowIfPoisoned();
        var fixture = await CreateCoreAsync(package, schema9Package);
        Exception? failure = null;
        try { await body(fixture); }
        catch (Exception error) { failure = error; }
        try { await fixture.DisposeAsync(); }
        catch (Exception cleanup) { throw failure == null ? cleanup : new AggregateException(failure, cleanup); }
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal async Task<GuiProcessResult> RunPackagedMigratorAsync(string package, bool runtime = false)
    {
        var executable = Path.Combine(package, "admin-migrator-net48-win-x86", "MyDmsVn.Server.DbMigrator.exe");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
        PackagedGuiAcceptanceTests.ClearSqlEnvironment(start);
        start.EnvironmentVariables["MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING"] = runtime ? _runtime : _migration;
        var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(90));
        File.AppendAllText(_resourceFile + ".migrator.txt", "EXIT=" + result.ExitCode + ";CLEANUP_VERIFIED=" + result.CleanupVerified + Environment.NewLine +
            result.StandardOutput + Environment.NewLine + result.StandardError + Environment.NewLine);
        return result;
    }

    internal async Task AddPartial010Async()
    {
        await ExecuteAsync(_migration, "ALTER TABLE dbo.StockBalances ADD InventoryValue decimal(28,4) NULL;");
        await AssertPartial010UnchangedAsync();
    }

    internal async Task AssertPartial010UnchangedAsync()
    {
        Assert.Equal(9, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        Assert.Equal(0, await ScalarAsync<int>(_migration, "SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo') AND name IN(N'SalesReceipts',N'SalesReceiptLines',N'StockValuationEntries');"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentType='GoodsReceipt' AND QuantityChange=20;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockBalances WHERE Quantity=20 AND InventoryValue IS NULL;"));
    }

    internal async Task UpgradeOwnedSchema9Async(string package)
    {
        // Verify every generated SQL-login identity before temporarily granting only
        // migration visibility. Runtime remains limited and the original DB is owned.
        await VerifySeparationAsync();
        var migration = _logins.Single(login => new SqlConnectionStringBuilder(_migration).UserID == login.Name);
        var major = await ScalarAsync<int>(_administration, "SELECT CONVERT(int,SERVERPROPERTY('ProductMajorVersion'));");
        var visibility = "VIEW SERVER STATE, VIEW ANY DEFINITION" + (major >= 16 ? ", VIEW SERVER PERFORMANCE STATE" : "");
        await ExecuteAsync(_administration, "GRANT " + visibility + " TO [" + migration.Name + "];");
        Exception? migrationFailure = null;
        try
        {
            var writerMaster = new SqlConnectionStringBuilder(_runtime) { InitialCatalog = "master" }.ConnectionString;
            using (var writer = new SqlConnection(writerMaster))
            {
                await writer.OpenAsync();
                using var query = new SqlCommand("SELECT COUNT(*) FROM [" + _database.DatabaseName + "].dbo.Products;", writer) { CommandTimeout = 15 };
                Assert.Equal(1, Convert.ToInt32(await query.ExecuteScalarAsync()));
                await ExecuteAsync(_migration, "ALTER DATABASE CURRENT SET RESTRICTED_USER;");
                await Assert.ThrowsAsync<SqlException>(() => query.ExecuteScalarAsync());
                using var reconnect = new SqlConnection(_runtime);
                await Assert.ThrowsAsync<SqlException>(() => reconnect.OpenAsync());
                var refused = await RunPackagedMigratorAsync(package);
                Assert.True(refused.CleanupVerified);
                Assert.NotEqual(0, refused.ExitCode);
                Assert.Contains("Migration.WritersNotDrained", refused.StandardError + refused.StandardOutput);
            }
            Assert.Equal(9, await ScalarAsync<int>(_migration, "SELECT COUNT(*) FROM dbo.SchemaVersions;"));
            Assert.Equal(1, await ScalarAsync<int>(_migration, "SELECT COUNT(*) FROM dbo.StockLedger;"));
            var upgraded = await RunPackagedMigratorAsync(package);
            Assert.True(upgraded.CleanupVerified);
            Assert.True(upgraded.ExitCode == 0, "Packaged exact9 upgrade failed; inspect sanitized migrator outcome.");
            _expectedSchema = 10;
            Assert.Equal(1, await ScalarAsync<int>(_migration, "SELECT COUNT(*) FROM sys.databases WHERE database_id=DB_ID() AND user_access=2;"));
            Assert.Equal(10, await ScalarAsync<int>(_migration, "SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        }
        catch (Exception error) { migrationFailure = error; }
        try
        {
            await ExecuteAsync(_administration, "REVOKE " + visibility + " FROM [" + migration.Name + "];");
        }
        catch (Exception cleanup) { throw migrationFailure == null ? cleanup : new AggregateException(migrationFailure, cleanup); }
        if (migrationFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(migrationFailure).Throw();
        // Operator/test owner restores access only after actual migration/drain proof;
        // product migrator never changes access mode or kills clients.
        await ExecuteAsync(_migration, "ALTER DATABASE CURRENT SET MULTI_USER;");
        await VerifySeparationAsync();
        await AssertSalesInventoryAsync(20m, 300m, 0);
        Assert.Equal(1, await ScalarAsync<int>(_oracle, @"
SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.GoodsReceiptLines s ON l.DocumentLineId=s.ReceiptLineId
JOIN dbo.GoodsReceipts h ON h.ReceiptId=s.ReceiptId JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId
WHERE l.DocumentType='GoodsReceipt' AND l.DocumentId=h.ReceiptId AND l.QuantityChange=20 AND s.Quantity=20 AND s.UnitCost=15
AND v.ValueChange=300 AND l.PostedAtUtc=h.PostedAtUtc AND l.PostedByUserId=h.PostedByUserId;"));
        var noOp = await RunPackagedMigratorAsync(package);
        Assert.True(noOp.CleanupVerified);
        Assert.True(noOp.ExitCode == 0, noOp.StandardOutput + noOp.StandardError);
        await AssertSalesInventoryAsync(20m, 300m, 0);
        Assert.Equal(10, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.SchemaVersions;"));
    }

    internal async Task ApplySchema10RuntimeGrantsAsync()
    {
        var grants = File.ReadAllText(Path.Combine(RepositoryRoot(), "deployment", "sql", "Provision-MyDmsVnPrincipals.sql"))
            .Replace("$(RuntimeUser)", new SqlConnectionStringBuilder(_runtime).UserID)
            .Replace("$(MigrationUser)", new SqlConnectionStringBuilder(_migration).UserID);
        await ExecuteAsync(_database.ConnectionString, grants);
        await VerifySeparationAsync();
    }
}
