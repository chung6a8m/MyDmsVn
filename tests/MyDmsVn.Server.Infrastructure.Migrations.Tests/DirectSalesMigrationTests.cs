using System;
using System.Threading.Tasks;
using System.Threading;
using MyDmsVn.Server.Infrastructure.Operations;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Testing.Sql;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class DirectSalesMigrationTests
{
    [SqlServerFact(SqlTestCategory.MigrationSchema)]
    public async Task Fresh_install_has_sales_valuation_and_required_inventory_value()
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(result.Successful, result.Error?.ToString());
            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(3, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name IN (N'SalesReceipts',N'SalesReceiptLines',N'StockValuationEntries');"));
            Assert.Equal(1, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StockBalances') AND name=N'InventoryValue' AND precision=28 AND scale=4 AND is_nullable=0 AND default_object_id=0;"));
            Assert.Equal(10, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
            Assert.Equal(1, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.SalesReceiptLines') AND name=N'IX_SalesReceiptLines_ProductId_SaleId' AND is_unique=0;"));
            await connection.OpenAsync();
            var fingerprint = await SqlSchemaDefinitionFingerprint.ReadAsync(connection, CancellationToken.None);
            Assert.True(SqlSchemaDefinitionFingerprint.Schema10 == fingerprint, fingerprint);
            Assert.True((await new SqlSchemaCompatibilityChecker(database.ConnectionString).CheckAsync(CancellationToken.None)).IsCompatible);
        }
        finally { await database.DisposeAsync(); }
    }
}
