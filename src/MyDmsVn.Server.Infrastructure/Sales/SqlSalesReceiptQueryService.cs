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
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Sales;

internal sealed class SqlSalesReceiptQueryService : ISalesReceiptQueryService
{
    private readonly ILinqToDbSessionFactory _sessions;
    public SqlSalesReceiptQueryService(ILinqToDbSessionFactory sessions) => _sessions = sessions;
    public async Task<SalesReceiptListPage> ListAsync(SalesReceiptListRequest request, CancellationToken cancellationToken)
    {
        // Validate even direct query-service calls before admitting an owned connection.
        var cursor = SalesReceiptCursor.Parse(request);
        request = cursor.Request;
        cancellationToken.ThrowIfCancellationRequested();
        using var db = await _sessions.OpenOwnedAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        var lines = db.GetTable<SalesReceiptLine>();
        var customers = db.GetTable<Customer>();
        IQueryable<SalesReceipt> filtered = db.GetTable<SalesReceipt>();
        if (request.FromDate.HasValue) filtered = filtered.Where(sale => sale.SaleDate >= request.FromDate.Value);
        if (request.ToDate.HasValue) filtered = filtered.Where(sale => sale.SaleDate <= request.ToDate.Value);
        if (request.CustomerId.HasValue) filtered = filtered.Where(sale => sale.CustomerId == request.CustomerId.Value);
        if (request.WarehouseId.HasValue) filtered = filtered.Where(sale => sale.WarehouseId == request.WarehouseId.Value);
        if (request.EmployeeId.HasValue) filtered = filtered.Where(sale => sale.EmployeeId == request.EmployeeId.Value);
        if (request.Status != null) filtered = filtered.Where(sale => sale.Status == request.Status);
        if (request.ProductId.HasValue) filtered = filtered.Where(sale => lines.Any(line => line.SaleId == sale.SaleId && line.ProductId == request.ProductId.Value));
        if (request.Search != null)
        {
            var search = request.Search;
            filtered = filtered.Where(sale => sale.SaleNumber.Contains(search) || (sale.Note != null && sale.Note.Contains(search)) ||
                customers.Any(customer => customer.Id == sale.CustomerId && (customer.Code.Contains(search) || customer.Name.Contains(search))));
        }
        var pageQuery = filtered;
        if (cursor.Anchor.HasValue)
        {
            var anchor = cursor.Anchor.Value;
            pageQuery = cursor.IsPrevious ? pageQuery.Where(sale => sale.SaleId > anchor) : pageQuery.Where(sale => sale.SaleId < anchor);
        }
        var ordered = cursor.IsPrevious ? pageQuery.OrderBy(sale => sale.SaleId) : pageQuery.OrderByDescending(sale => sale.SaleId);
        // Limit headers before joining their one-to-one catalog labels. Historical inactive labels remain visible.
        var selected = ordered.Take(request.PageSize + 1);
        var projection = from sale in selected
                         join customer in customers on sale.CustomerId equals customer.Id
                         join warehouse in db.GetTable<Warehouse>() on sale.WarehouseId equals warehouse.Id
                         join employee in db.GetTable<Employee>() on sale.EmployeeId equals employee.Id
                         select new
                         {
                             sale.SaleId,
                             sale.SaleNumber,
                             sale.SaleDate,
                             sale.CustomerId,
                             CustomerCode = customer.Code,
                             CustomerName = customer.Name,
                             sale.WarehouseId,
                             WarehouseCode = warehouse.Code,
                             WarehouseName = warehouse.Name,
                             sale.EmployeeId,
                             EmployeeCode = employee.Code,
                             EmployeeName = employee.Name,
                             sale.Status
                         };
        var headers = cursor.IsPrevious
            ? await projection.OrderBy(row => row.SaleId).ToListAsync(cancellationToken).ConfigureAwait(false)
            : await projection.OrderByDescending(row => row.SaleId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var lookAhead = headers.Count > request.PageSize;
        if (lookAhead) headers.RemoveAt(headers.Count - 1);
        if (cursor.IsPrevious) headers.Reverse();
        cancellationToken.ThrowIfCancellationRequested();
        if (headers.Count == 0)
        {
            var empty = new SalesReceiptListPage(Array.Empty<SalesReceiptListRow>(), null, null);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return empty;
        }
        var ids = headers.Select(header => header.SaleId).ToArray();
        var totals = await lines.Where(line => ids.Contains(line.SaleId)).GroupBy(line => line.SaleId)
            .Select(group => new { SaleId = group.Key, LineCount = group.Count(), Total = (decimal?)group.Sum(line => line.LineAmount) })
            .ToDictionaryAsync(row => row.SaleId, cancellationToken).ConfigureAwait(false);
        var first = headers[0].SaleId;
        var last = headers[headers.Count - 1].SaleId;
        var hasNext = cursor.IsPrevious ? await filtered.AnyAsync(sale => sale.SaleId < last, cancellationToken).ConfigureAwait(false) : lookAhead;
        var hasPrevious = cursor.IsPrevious ? lookAhead : cursor.Anchor.HasValue && await filtered.AnyAsync(sale => sale.SaleId > first, cancellationToken).ConfigureAwait(false);
        var items = headers.Select(header =>
        {
            var total = totals.TryGetValue(header.SaleId, out var aggregate) ? aggregate.Total ?? 0m : 0m;
            if (total < 0 || total > SalesContractLimits.MaximumAmount) throw new InvalidOperationException("Invalid persisted Sales summary amount.");
            return new SalesReceiptListRow(header.SaleId, header.SaleNumber, header.SaleDate, header.CustomerId, header.CustomerCode + " - " + header.CustomerName,
                header.WarehouseId, header.WarehouseCode + " - " + header.WarehouseName, header.EmployeeId, header.EmployeeCode + " - " + header.EmployeeName, header.Status, aggregate?.LineCount ?? 0, total);
        }).ToArray();
        var result = new SalesReceiptListPage(items, hasNext ? cursor.Encode(last, false) : null, hasPrevious ? cursor.Encode(first, true) : null);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
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
