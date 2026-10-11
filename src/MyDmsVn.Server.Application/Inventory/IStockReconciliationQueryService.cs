using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Inventory;

public sealed class StockReconciliationDifference
{
    public int WarehouseId { get; set; }

    public int ProductId { get; set; }

    public bool HasLedger { get; set; }

    public bool HasBalance { get; set; }

    public decimal LedgerQuantity { get; set; }

    public decimal BalanceQuantity { get; set; }
}

public interface IStockReconciliationQueryService
{
    Task<IReadOnlyList<StockReconciliationDifference>> FindDifferencesAsync(
        CancellationToken cancellationToken);
}
