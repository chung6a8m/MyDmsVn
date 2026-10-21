using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Domain.Inventory;
using MyDmsVn.Server.Domain.Sales;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Sales;

internal sealed class SqlSalesReceiptQueryService : ISalesReceiptQueryService
{
    private readonly ILinqToDbSessionFactory _sessions;
    public SqlSalesReceiptQueryService(ILinqToDbSessionFactory sessions) => _sessions = sessions;
    public async Task<SalesReceiptDto?> GetByIdAsync(long saleId, CancellationToken cancellationToken)
    {
        using var db = await _sessions.OpenOwnedAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        var header = await db.GetTable<SalesReceipt>().SingleOrDefaultAsync(row => row.SaleId == saleId, cancellationToken).ConfigureAwait(false);
        if (header == null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        var lines = await db.GetTable<SalesReceiptLine>().Where(row => row.SaleId == saleId).OrderBy(row => row.LineNo)
            .ThenBy(row => row.SaleLineId).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var references = await SalesReferenceLoader.LoadAsync(db, header.CustomerId, header.WarehouseId, header.EmployeeId,
            lines.Select(line => line.ProductId).Distinct().ToArray(), cancellationToken, orderedProductLocks: false).ConfigureAwait(false);
        var posted = header.Status == SalesReceiptStatuses.Posted;
        if (header.Version.Length != 8 || lines.Length < 1 || lines.Length > 200 ||
            lines.Select((line, index) => line.LineNo != index + 1 || line.SaleLineId <= 0).Any(invalid => invalid))
            throw new InvalidOperationException("Invalid Sales detail identity or order.");
        foreach (var line in lines)
        {
            if (line.LineAmount != InventoryValuationMath.CalculateRevenue(line.Qty, line.UnitPrice, line.IsGift) ||
                string.IsNullOrWhiteSpace(line.Unit) || line.Unit.Length > 32 || line.Unit.IndexOf('\0') >= 0 ||
                (posted && (!line.PostedUnitCost.HasValue || line.PostedUnitCost < 0 || line.PostedUnitCost > InventoryValuationMath.MaximumUnitCost ||
                    decimal.Round(line.PostedUnitCost.Value, 8) != line.PostedUnitCost.Value)))
                throw new InvalidOperationException("Invalid persisted Sales amount, Unit or cost.");
        }
        foreach (var group in lines.GroupBy(line => line.ProductId)) InventoryValuationMath.SumQuantities(group.Select(line => line.Qty));
        var revenue = InventoryValuationMath.SumAmounts(lines.Select(line => line.LineAmount));
        decimal? cost = posted ? InventoryValuationMath.SumAmounts(lines.Select(line => line.CostAmount ?? throw new InvalidOperationException("Posted cost is required."))) : (decimal?)null;
        var result = new SalesReceiptDto(header.SaleId, header.SaleNumber, header.SaleDate, header.CustomerId, header.WarehouseId, header.EmployeeId,
            header.Status, header.Note, header.PostedAtUtc, Convert.ToBase64String(header.Version), lines.Select(line => new SalesReceiptLineDto(
                line.SaleLineId, line.LineNo, line.ProductId, line.Unit, line.Qty, line.UnitPrice, line.IsGift, line.LineAmount, line.PostedUnitCost, line.CostAmount,
                references.Products[line.ProductId].DisplayName, references.Products[line.ProductId].State == CatalogReferenceState.Active)).ToArray(), revenue,
            cost, cost.HasValue ? InventoryValuationMath.SumSignedAmounts(new[] { revenue, -cost.Value }) : (decimal?)null,
            references.Customer.DisplayName, references.Customer.State == CatalogReferenceState.Active,
            references.Warehouse.DisplayName, references.Warehouse.State == CatalogReferenceState.Active,
            references.Employee.DisplayName, references.Employee.State == CatalogReferenceState.Active);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
