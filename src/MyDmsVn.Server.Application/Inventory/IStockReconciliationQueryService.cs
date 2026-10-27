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
    public decimal LedgerValue { get; set; }
    public decimal BalanceValue { get; set; }
    private readonly List<string> _issues = new List<string>();
    public IReadOnlyList<string> Issues => _issues.AsReadOnly();
    public void AddIssue(string issue) { if (!_issues.Contains(issue)) _issues.Add(issue); }
}

public interface IStockReconciliationQueryService
{
    Task<IReadOnlyList<StockReconciliationDifference>> FindDifferencesAsync(
        CancellationToken cancellationToken);
}
