using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Inventory;

internal sealed class SqlStockReconciliationQueryService : IStockReconciliationQueryService
{
    private const string AcquireReconciliationLocksSql =
        "SELECT TOP (1) LedgerId FROM dbo.StockLedger WITH (TABLOCK, HOLDLOCK) ORDER BY LedgerId; " +
        "SELECT TOP (1) WarehouseId FROM dbo.StockBalances WITH (TABLOCK, HOLDLOCK) " +
        "ORDER BY WarehouseId, ProductId;";

    private const string ReconciliationSql =
        "WITH LedgerTotals AS (" +
        "SELECT WarehouseId, ProductId, SUM(QuantityChange) AS LedgerQuantity " +
        "FROM dbo.StockLedger GROUP BY WarehouseId, ProductId) " +
        "SELECT COALESCE(ledger.WarehouseId, balance.WarehouseId) AS WarehouseId, " +
        "COALESCE(ledger.ProductId, balance.ProductId) AS ProductId, " +
        "CAST(CASE WHEN ledger.WarehouseId IS NULL THEN 0 ELSE 1 END AS bit) AS HasLedger, " +
        "CAST(CASE WHEN balance.WarehouseId IS NULL THEN 0 ELSE 1 END AS bit) AS HasBalance, " +
        "COALESCE(ledger.LedgerQuantity, 0) AS LedgerQuantity, " +
        "COALESCE(balance.Quantity, 0) AS BalanceQuantity " +
        "FROM LedgerTotals ledger " +
        "FULL OUTER JOIN dbo.StockBalances balance " +
        "ON balance.WarehouseId = ledger.WarehouseId AND balance.ProductId = ledger.ProductId " +
        "WHERE ledger.WarehouseId IS NULL OR balance.WarehouseId IS NULL " +
        "OR COALESCE(ledger.LedgerQuantity, 0) <> COALESCE(balance.Quantity, 0) " +
        "ORDER BY COALESCE(ledger.WarehouseId, balance.WarehouseId), " +
        "COALESCE(ledger.ProductId, balance.ProductId);";

    private readonly IDbConnectionFactory _connectionFactory;

    public SqlStockReconciliationQueryService(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<StockReconciliationDifference>> FindDifferencesAsync(
        CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        var lockCommand = new CommandDefinition(
            AcquireReconciliationLocksSql,
            transaction: transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(lockCommand).ConfigureAwait(false);
        var command = new CommandDefinition(
            ReconciliationSql,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var differences = (await connection
            .QueryAsync<StockReconciliationDifference>(command)
            .ConfigureAwait(false))
            .ToArray();
        transaction.Commit();
        return differences;
    }
}
