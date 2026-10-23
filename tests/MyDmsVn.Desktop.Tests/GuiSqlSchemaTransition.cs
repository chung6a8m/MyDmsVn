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
    private string? _schema9SourceLedgerSnapshot;
    private string? _schema9JournalSnapshot;
    private string? _schema9BalanceFactsSnapshot;
    private string? _schema9BalanceTokenSnapshot;

    // Fixed owned-fixture facts, deterministic key order and base64 rowversion bytes.
    // Source rows include their original identities, all audit fields and opaque token.
    private const string LegacySourceLedgerExpression = @"
CONVERT(nvarchar(max),(SELECT * FROM dbo.GoodsReceipts ORDER BY ReceiptId FOR XML RAW('receipt'),BINARY BASE64,ROOT('receipts')))+
CONVERT(nvarchar(max),(SELECT * FROM dbo.GoodsReceiptLines ORDER BY ReceiptLineId FOR XML RAW('line'),BINARY BASE64,ROOT('receiptLines')))+
CONVERT(nvarchar(max),(SELECT * FROM dbo.StockLedger ORDER BY LedgerId FOR XML RAW('entry'),BINARY BASE64,ROOT('ledger')))";
    private const string LegacyJournalSnapshotSql = @"
SELECT CONVERT(nvarchar(max),(SELECT TOP(9) Id,ScriptName,Applied FROM dbo.SchemaVersions ORDER BY Id
FOR XML RAW('script'),ROOT('legacyJournal')));";
    private const string BalanceFactsSnapshotSql = @"
SELECT CONVERT(nvarchar(max),(SELECT WarehouseId,ProductId,Quantity FROM dbo.StockBalances ORDER BY WarehouseId,ProductId
FOR XML RAW('balance'),ROOT('balances')));";
    private const string BalanceTokenSnapshotSql = @"
SELECT CONVERT(nvarchar(max),(SELECT WarehouseId,ProductId,RowVersion FROM dbo.StockBalances ORDER BY WarehouseId,ProductId
FOR XML RAW('balance'),BINARY BASE64,ROOT('balanceTokens')));";
    private const string Schema10NoOpSnapshotSql = "SELECT " + LegacySourceLedgerExpression + @"+
CONVERT(nvarchar(max),(SELECT * FROM dbo.StockBalances ORDER BY WarehouseId,ProductId FOR XML RAW('balance'),BINARY BASE64,ROOT('balances')))+
CONVERT(nvarchar(max),(SELECT * FROM dbo.StockValuationEntries ORDER BY LedgerId FOR XML RAW('value'),ROOT('valuation')))+
CONVERT(nvarchar(max),(SELECT * FROM dbo.SalesReceipts ORDER BY SaleId FOR XML RAW('sale'),BINARY BASE64,ROOT('sales')))+
CONVERT(nvarchar(max),(SELECT * FROM dbo.SalesReceiptLines ORDER BY SaleLineId FOR XML RAW('line'),BINARY BASE64,ROOT('saleLines')))+
CONVERT(nvarchar(max),(SELECT Id,ScriptName,Applied FROM dbo.SchemaVersions ORDER BY Id FOR XML RAW('script'),ROOT('journal')));";

    internal async Task CaptureLegacySchema9Async()
    {
        Assert.Equal(9, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.GoodsReceipts;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.GoodsReceiptLines;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockLedger;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockBalances;"));
        _schema9SourceLedgerSnapshot = await ScalarAsync<string>(_oracle, "SELECT " + LegacySourceLedgerExpression + ";");
        _schema9JournalSnapshot = await ScalarAsync<string>(_oracle, LegacyJournalSnapshotSql);
        _schema9BalanceFactsSnapshot = await ScalarAsync<string>(_oracle, BalanceFactsSnapshotSql);
        _schema9BalanceTokenSnapshot = await ScalarAsync<string>(_oracle, BalanceTokenSnapshotSql);
    }

    internal async Task AssertLegacyPreservedAsync(bool upgraded, bool restricted = false)
    {
        Assert.False(string.IsNullOrEmpty(_schema9SourceLedgerSnapshot));
        Assert.False(string.IsNullOrEmpty(_schema9JournalSnapshot));
        Assert.False(string.IsNullOrEmpty(_schema9BalanceFactsSnapshot));
        Assert.False(string.IsNullOrEmpty(_schema9BalanceTokenSnapshot));
        // Restricted maintenance admits the owned migration identity; the original
        // read-only-oracle snapshot was captured before entering maintenance.
        var connection = restricted ? _migration : _oracle;
        Assert.Equal(_schema9SourceLedgerSnapshot, await ScalarAsync<string>(connection, "SELECT " + LegacySourceLedgerExpression + ";"));
        Assert.Equal(_schema9JournalSnapshot, await ScalarAsync<string>(connection, LegacyJournalSnapshotSql));
        Assert.Equal(_schema9BalanceFactsSnapshot, await ScalarAsync<string>(connection, BalanceFactsSnapshotSql));
        var token = await ScalarAsync<string>(connection, BalanceTokenSnapshotSql);
        if (upgraded) Assert.NotEqual(_schema9BalanceTokenSnapshot, token); // Exactly the bootstrap balance UPDATE may replace this token.
        else Assert.Equal(_schema9BalanceTokenSnapshot, token);
        Assert.Equal(upgraded ? 10 : 9, await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.SchemaVersions;"));
    }

    internal Task<string> ReadSchema10SnapshotAsync() => ScalarAsync<string>(_oracle, Schema10NoOpSnapshotSql);

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
        await CaptureLegacySchema9Async();
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
                await AssertLegacyPreservedAsync(upgraded: false, restricted: true);
            }
            Assert.Equal(9, await ScalarAsync<int>(_migration, "SELECT COUNT(*) FROM dbo.SchemaVersions;"));
            Assert.Equal(1, await ScalarAsync<int>(_migration, "SELECT COUNT(*) FROM dbo.StockLedger;"));
            var upgraded = await RunPackagedMigratorAsync(package);
            Assert.True(upgraded.CleanupVerified);
            Assert.True(upgraded.ExitCode == 0, "Packaged exact9 upgrade failed; inspect sanitized migrator outcome.");
            Assert.Contains("Migration.Completed:", upgraded.StandardOutput);
            Assert.Contains(" 1 script.", upgraded.StandardOutput);
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
        await AssertLegacyPreservedAsync(upgraded: true);
        Assert.Equal(1, await ScalarAsync<int>(_oracle, @"
SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.GoodsReceiptLines s ON l.DocumentLineId=s.ReceiptLineId
JOIN dbo.GoodsReceipts h ON h.ReceiptId=s.ReceiptId JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId
WHERE l.DocumentType='GoodsReceipt' AND l.DocumentId=h.ReceiptId AND l.QuantityChange=20 AND s.Quantity=20 AND s.UnitCost=15
AND v.ValueChange=300 AND l.PostedAtUtc=h.PostedAtUtc AND l.PostedByUserId=h.PostedByUserId;"));
        var beforeNoOp = await ReadSchema10SnapshotAsync();
        var noOp = await RunPackagedMigratorAsync(package);
        Assert.True(noOp.CleanupVerified);
        Assert.True(noOp.ExitCode == 0, noOp.StandardOutput + noOp.StandardError);
        Assert.Contains("Migration.Completed:", noOp.StandardOutput);
        Assert.Contains(" 0 script.", noOp.StandardOutput);
        Assert.Equal(beforeNoOp, await ReadSchema10SnapshotAsync());
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
