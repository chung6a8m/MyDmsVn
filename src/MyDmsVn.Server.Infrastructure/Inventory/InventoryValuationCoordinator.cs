using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Inventory;

// Operation state belongs to this repository's exact current transaction, never to a connection pool/session.
internal sealed class InventoryValuationCoordinator
{
    internal const string GateResource = "MyDmsVn.InventoryValuation.v1";
    private readonly ISqlExecutionContext _context;
    private readonly ILinqToDbSessionFactory _sessions;
    private readonly Dictionary<Tuple<int, int>, StockBalance?> _locked = new();
    private IDbTransaction? _admittedTransaction;
    private bool _movementStarted;
    private Tuple<int, int>? _lastKey;

    public InventoryValuationCoordinator(ISqlExecutionContext context, ILinqToDbSessionFactory sessions)
    {
        _context = context;
        _sessions = sessions;
    }

    public async Task AcquirePostingGateAsync(CancellationToken token)
    {
        if (_admittedTransaction != null) { RequireGate(); return; }
        await AcquireGateAsync(_context, false, token).ConfigureAwait(false);
        _admittedTransaction = _context.Transaction;
    }

    internal static async Task AcquireGateAsync(ISqlExecutionContext context, bool exclusive, CancellationToken token)
    {
        var transaction = context.Transaction ?? throw new InvalidOperationException("Valuation requires an active transaction.");
        // sp_getapplock is SQL Server-specific; this native command uses only the caller-owned current transaction.
        using var command = InventorySql.Command(context.Connection, transaction,
            "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@resource, @LockMode=@mode, " +
            "@LockOwner='Transaction', @DbPrincipal='public', @LockTimeout=15000; SELECT @result;");
        InventorySql.AddText(command, "@resource", GateResource);
        InventorySql.AddText(command, "@mode", exclusive ? "Exclusive" : "Shared");
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false));
        if (result < 0) throw new InvalidOperationException("Inventory valuation gate acquisition failed (" + result + ").");
    }

    public async Task<IReadOnlyDictionary<int, StockValuationState>> LockBalancesAsync(int warehouseId,
        IReadOnlyCollection<int> productIds, CancellationToken token)
    {
        RequireGate();
        if (warehouseId <= 0 || productIds == null || productIds.Any(id => id <= 0))
            throw new ArgumentException("Positive stock keys are required.");
        var result = new Dictionary<int, StockValuationState>();
        using var db = _sessions.Borrow(_context, true);
        foreach (var productId in productIds.Distinct().OrderBy(id => id))
        {
            var key = Tuple.Create(warehouseId, productId);
            if (!_locked.TryGetValue(key, out var row))
            {
                if (_movementStarted || (_lastKey != null && Comparer<Tuple<int, int>>.Default.Compare(key, _lastKey) <= 0))
                    throw new InvalidOperationException("All stock keys must be locked in ascending order before movements.");
                row = await db.GetTable<StockBalance>().TableHint("UPDLOCK").TableHint("HOLDLOCK")
                    .TableHint("INDEX", "PK_StockBalances")
                    .SingleOrDefaultAsync(x => x.WarehouseId == warehouseId && x.ProductId == productId, token).ConfigureAwait(false);
                _locked.Add(key, row);
                _lastKey = key;
            }
            result.Add(productId, new StockValuationState(row?.Quantity ?? 0, row?.InventoryValue ?? 0));
        }
        return result;
    }

    public async Task AppendMovementAsync(StockLedgerEntry entry, decimal valueChange, CancellationToken token)
    {
        RequireKey(entry.WarehouseId, entry.ProductId);
        InventoryValuationMath.SumSignedQuantities(new[] { entry.QuantityChange });
        InventoryValuationMath.SumSignedAmounts(new[] { valueChange });
        if (!((entry.DocumentType == "GoodsReceipt" && entry.QuantityChange > 0 && valueChange >= 0) ||
              (entry.DocumentType == "SalesReceipt" && entry.QuantityChange < 0 && valueChange <= 0)))
            throw new ArgumentException("Unsupported inventory movement or sign.");
        _movementStarted = true;
        using var db = _sessions.Borrow(_context, true);
        entry.Id = await db.GetTable<StockLedgerEntry>()
            .Value(x => x.WarehouseId, entry.WarehouseId).Value(x => x.ProductId, entry.ProductId)
            .Value(x => x.DocumentType, entry.DocumentType).Value(x => x.DocumentId, entry.DocumentId)
            .Value(x => x.DocumentLineId, entry.DocumentLineId).Value(x => x.QuantityChange, entry.QuantityChange)
            .Value(x => x.PostedAtUtc, entry.PostedAtUtc).Value(x => x.PostedByUserId, entry.PostedByUserId)
            .InsertWithInt64IdentityAsync(token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The stock ledger insert returned no identity.");
        if (await db.GetTable<StockValuationEntry>().Value(x => x.LedgerId, entry.Id)
            .Value(x => x.ValueChange, valueChange).InsertAsync(token).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("The valuation companion was not inserted.");
    }

    internal IReadOnlyDictionary<int, StockValuationState> GetLockedBalances(int warehouseId, IEnumerable<int> productIds)
    {
        return productIds.Distinct().ToDictionary(id => id, id =>
        {
            var row = _locked[RequireKey(warehouseId, id)];
            return new StockValuationState(row?.Quantity ?? 0, row?.InventoryValue ?? 0);
        });
    }

    public async Task ApplyBalanceAsync(int warehouseId, int productId, decimal quantityChange,
        decimal valueChange, CancellationToken token)
    {
        var key = RequireKey(warehouseId, productId);
        var row = _locked[key];
        var quantity = InventoryValuationMath.SumSignedQuantities(new[] { row?.Quantity ?? 0, quantityChange });
        var value = InventoryValuationMath.SumSignedAmounts(new[] { row?.InventoryValue ?? 0, valueChange });
        _ = new StockValuationState(quantity, value);
        using var db = _sessions.Borrow(_context, true);
        var affected = row == null
            ? await db.GetTable<StockBalance>().Value(x => x.WarehouseId, warehouseId).Value(x => x.ProductId, productId)
                .Value(x => x.Quantity, quantity).Value(x => x.InventoryValue, value).InsertAsync(token).ConfigureAwait(false)
            : await db.GetTable<StockBalance>().Where(x => x.WarehouseId == warehouseId && x.ProductId == productId)
                .Set(x => x.Quantity, x => x.Quantity + quantityChange)
                .Set(x => x.InventoryValue, x => x.InventoryValue + valueChange).UpdateAsync(token).ConfigureAwait(false);
        if (affected != 1) throw new InvalidOperationException("The locked stock balance was not written.");
        _locked[key] = new StockBalance { WarehouseId = warehouseId, ProductId = productId, Quantity = quantity, InventoryValue = value };
    }

    private Tuple<int, int> RequireKey(int warehouseId, int productId)
    {
        RequireGate();
        var key = Tuple.Create(warehouseId, productId);
        if (!_locked.ContainsKey(key)) throw new InvalidOperationException("The stock key has not been locked.");
        return key;
    }

    private void RequireGate()
    {
        if (_admittedTransaction == null || !ReferenceEquals(_admittedTransaction, _context.Transaction))
            throw new InvalidOperationException("The current transaction has no posting gate.");
    }
}
