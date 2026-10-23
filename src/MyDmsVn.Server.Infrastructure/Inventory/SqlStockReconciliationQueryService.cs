using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Domain.Sales;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Inventory;

internal sealed class SqlStockReconciliationQueryService : IStockReconciliationQueryService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILinqToDbSessionFactory _sessions;

    public SqlStockReconciliationQueryService(IDbConnectionFactory connectionFactory, ILinqToDbSessionFactory sessions)
    {
        _connectionFactory = connectionFactory;
        _sessions = sessions;
    }

    public async Task<IReadOnlyList<StockReconciliationDifference>> FindDifferencesAsync(CancellationToken token)
    {
        using var connection = await _connectionFactory.OpenConnectionAsync(token).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        // Use the actual transaction connection, including when a recording factory wraps IDbConnection.
        var context = new ReconciliationContext(transaction);
        await InventoryValuationCoordinator.AcquireGateAsync(context, true, token).ConfigureAwait(false);
        using var db = _sessions.Borrow(context, true);
        var ledger = await db.GetTable<StockLedgerEntry>().TableHint("TABLOCK").TableHint("HOLDLOCK")
            .ToArrayAsync(token).ConfigureAwait(false);
        var valuations = await db.GetTable<StockValuationEntry>().TableHint("TABLOCK").TableHint("HOLDLOCK")
            .ToArrayAsync(token).ConfigureAwait(false);
        var balances = await db.GetTable<StockBalance>().TableHint("TABLOCK").TableHint("HOLDLOCK")
            .ToArrayAsync(token).ConfigureAwait(false);
        var receipts = await db.GetTable<GoodsReceipt>().ToArrayAsync(token).ConfigureAwait(false);
        var receiptLines = await db.GetTable<GoodsReceiptLine>().ToArrayAsync(token).ConfigureAwait(false);
        var sales = await db.GetTable<SalesReceipt>().ToArrayAsync(token).ConfigureAwait(false);
        var saleLines = await db.GetTable<SalesReceiptLine>().ToArrayAsync(token).ConfigureAwait(false);
        // Keep analysis cancellable while the owned Exclusive gate is held, including lookup construction.
        IEnumerable<T> Observe<T>(IEnumerable<T> items)
        {
            token.ThrowIfCancellationRequested();
            foreach (var item in items)
            {
                token.ThrowIfCancellationRequested();
                yield return item;
            }
            token.ThrowIfCancellationRequested();
        }
        var rows = new SortedDictionary<Tuple<int, int>, StockReconciliationDifference>();
        StockReconciliationDifference Row(int warehouse, int product)
        {
            token.ThrowIfCancellationRequested();
            var key = Tuple.Create(warehouse, product);
            if (!rows.TryGetValue(key, out var row))
            {
                row = new StockReconciliationDifference { WarehouseId = warehouse, ProductId = product };
                rows.Add(key, row);
            }
            return row;
        }
        void Issue(int warehouse, int product, string code) => Row(warehouse, product).AddIssue(code);
        void Check(int warehouse, int product, Action action)
        {
            token.ThrowIfCancellationRequested();
            try { action(); }
            catch (Exception error) when (error is ArgumentException || error is OverflowException)
            { Issue(warehouse, product, "NumericOutOfRange"); }
        }
        var values = Observe(valuations).ToLookup(x => x.LedgerId);
        var movements = Observe(ledger).ToLookup(x => Tuple.Create(x.DocumentType, x.DocumentLineId));
        var receiptHeaders = Observe(receipts).ToDictionary(x => x.Id);
        var receiptDetails = Observe(receiptLines).ToDictionary(x => x.Id);
        var saleHeaders = Observe(sales).ToDictionary(x => x.SaleId);
        var saleDetails = Observe(saleLines).ToDictionary(x => x.SaleLineId);
        var receiptLinesByReceipt = Observe(receiptLines).ToLookup(x => x.ReceiptId);
        var saleLinesBySale = Observe(saleLines).ToLookup(x => x.SaleId);
        foreach (var group in Observe(Observe(ledger).GroupBy(x => Tuple.Create(x.WarehouseId, x.ProductId))))
        {
            var row = Row(group.Key.Item1, group.Key.Item2);
            row.HasLedger = true;
            Check(row.WarehouseId, row.ProductId, () =>
            {
                row.LedgerQuantity = InventoryValuationMath.SumSignedQuantities(Observe(group).Select(x => x.QuantityChange));
                row.LedgerValue = InventoryValuationMath.SumSignedAmounts(Observe(group).SelectMany(x => Observe(values[x.Id])).Select(x => x.ValueChange));
            });
        }
        foreach (var balance in Observe(balances))
        {
            var row = Row(balance.WarehouseId, balance.ProductId);
            row.HasBalance = true;
            row.BalanceQuantity = balance.Quantity;
            row.BalanceValue = balance.InventoryValue;
            Check(balance.WarehouseId, balance.ProductId, () => { _ = new StockValuationState(balance.Quantity, balance.InventoryValue); });
        }
        foreach (var entry in Observe(ledger))
        {
            var companions = Observe(values[entry.Id]).ToArray();
            if (companions.Length != 1) Issue(entry.WarehouseId, entry.ProductId, "ValuationCounterpart");
            var value = companions.Length == 1 ? (decimal?)companions[0].ValueChange : null;
            if (entry.DocumentType == "GoodsReceipt")
            {
                if (!receiptHeaders.TryGetValue(entry.DocumentId, out var header) ||
                    !receiptDetails.TryGetValue(entry.DocumentLineId, out var line) ||
                    header.Status != "Posted" || line.ReceiptId != header.Id || header.WarehouseId != entry.WarehouseId ||
                    line.ProductId != entry.ProductId || entry.QuantityChange != line.Quantity || entry.QuantityChange <= 0 ||
                    header.PostedAtUtc != entry.PostedAtUtc || header.PostedByUserId != entry.PostedByUserId)
                    Issue(entry.WarehouseId, entry.ProductId, "ReceiptSourceMismatch");
                else Check(entry.WarehouseId, entry.ProductId, () =>
                {
                    if (value != InventoryValuationMath.CalculateReceiptValue(line.Quantity, line.UnitCost))
                        Issue(entry.WarehouseId, entry.ProductId, "ReceiptValueMismatch");
                });
            }
            else if (entry.DocumentType == "SalesReceipt")
            {
                if (!saleHeaders.TryGetValue(entry.DocumentId, out var header) ||
                    !saleDetails.TryGetValue(entry.DocumentLineId, out var line) ||
                    header.Status != "Posted" || line.SaleId != header.SaleId || header.WarehouseId != entry.WarehouseId ||
                    line.ProductId != entry.ProductId || entry.QuantityChange != -line.Qty || entry.QuantityChange >= 0 ||
                    header.PostedAtUtc != entry.PostedAtUtc || header.PostedByUserId != entry.PostedByUserId ||
                    !line.CostAmount.HasValue || !line.PostedUnitCost.HasValue || value != -line.CostAmount)
                    Issue(entry.WarehouseId, entry.ProductId, "SaleSourceMismatch");
            }
            else Issue(entry.WarehouseId, entry.ProductId, "UnsupportedMovement");
        }
        var ledgerIds = new HashSet<long>(Observe(ledger).Select(x => x.Id));
        foreach (var orphan in Observe(valuations).Where(x => !ledgerIds.Contains(x.LedgerId)))
            Issue(0, 0, "OrphanValuation");
        foreach (var header in Observe(receipts).Where(x => x.Status == "Posted"))
        {
            var lines = Observe(receiptLinesByReceipt[header.Id]).ToArray();
            if (lines.Length == 0 || lines.Length > 200) Issue(header.WarehouseId, 0, "ReceiptLineCount");
            Check(header.WarehouseId, lines.FirstOrDefault()?.ProductId ?? 0, () =>
                InventoryValuationMath.SumAmounts(Observe(lines).Select(x => InventoryValuationMath.CalculateReceiptValue(x.Quantity, x.UnitCost))));
            foreach (var line in Observe(lines))
                if (Observe(movements[Tuple.Create("GoodsReceipt", line.Id)]).Take(2).Count() != 1)
                    Issue(header.WarehouseId, line.ProductId, "ReceiptMovementCounterpart");
        }
        foreach (var header in Observe(sales).Where(x => x.Status == "Posted"))
        {
            var lines = Observe(saleLinesBySale[header.SaleId]).ToArray();
            if (lines.Length == 0 || lines.Length > 200) Issue(header.WarehouseId, 0, "SaleLineCount");
            foreach (var line in Observe(lines))
            {
                if (Observe(movements[Tuple.Create("SalesReceipt", line.SaleLineId)]).Take(2).Count() != 1)
                    Issue(header.WarehouseId, line.ProductId, "SaleMovementCounterpart");
                if (!line.CostAmount.HasValue || !line.PostedUnitCost.HasValue || line.PostedUnitCost < 0 ||
                    line.PostedUnitCost > InventoryValuationMath.MaximumUnitCost ||
                    (line.PostedUnitCost.HasValue && decimal.Round(line.PostedUnitCost.Value, 8) != line.PostedUnitCost))
                    Issue(header.WarehouseId, line.ProductId, "SaleCostSnapshot");
                Check(header.WarehouseId, line.ProductId, () =>
                {
                    if (line.LineAmount != InventoryValuationMath.CalculateRevenue(line.Qty, line.UnitPrice, line.IsGift))
                        Issue(header.WarehouseId, line.ProductId, "SaleRevenueMismatch");
                });
            }
            Check(header.WarehouseId, lines.FirstOrDefault()?.ProductId ?? 0, () =>
            {
                InventoryValuationMath.SumAmounts(Observe(lines).Select(x => x.LineAmount));
                InventoryValuationMath.SumAmounts(Observe(lines).Select(x => x.CostAmount ?? 0));
            });
            foreach (var group in Observe(Observe(lines).GroupBy(x => x.ProductId)))
            {
                Check(header.WarehouseId, group.Key, () =>
                {
                    InventoryValuationMath.SumQuantities(Observe(group).Select(x => x.Qty));
                    InventoryValuationMath.SumAmounts(Observe(group).Select(x => x.CostAmount ?? 0));
                });
                if (Observe(group).Select(x => x.PostedUnitCost).Distinct().Take(2).Count() != 1)
                    Issue(header.WarehouseId, group.Key, "SaleGroupSnapshot");
            }
        }
        foreach (var row in Observe(rows.Values))
        {
            if (!row.HasLedger || !row.HasBalance) row.AddIssue("StockKeyCounterpart");
            if (row.LedgerQuantity != row.BalanceQuantity) row.AddIssue("QuantityMismatch");
            if (row.LedgerValue != row.BalanceValue) row.AddIssue("ValueMismatch");
        }
        var differences = Observe(rows.Values).Where(x => x.Issues.Count != 0).ToArray();
        token.ThrowIfCancellationRequested();
        transaction.Commit();
        token.ThrowIfCancellationRequested();
        return differences;
    }

    private sealed class ReconciliationContext : ISqlExecutionContext
    {
        private readonly IDbTransaction _transaction;
        public ReconciliationContext(IDbTransaction transaction) => _transaction = transaction;
        public IDbConnection Connection => _transaction.Connection ?? throw new InvalidOperationException("Reconciliation transaction completed.");
        public IDbTransaction Transaction => _transaction;
    }
}
