using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Domain.Sales;

namespace MyDmsVn.Server.Application.Sales;

public sealed class SalesCatalogReference
{
    public SalesCatalogReference(CatalogReferenceState state, string? displayName) { State = state; DisplayName = displayName; }
    public CatalogReferenceState State { get; }
    public string? DisplayName { get; }
}

public sealed class SalesProductReference
{
    public SalesProductReference(CatalogReferenceState state, string? displayName, string? unit) { State = state; DisplayName = displayName; Unit = unit; }
    public CatalogReferenceState State { get; }
    public string? DisplayName { get; }
    public string? Unit { get; }
}

public sealed class SalesReceiptReferenceSnapshot
{
    public SalesReceiptReferenceSnapshot(SalesCatalogReference customer, SalesCatalogReference warehouse,
        SalesCatalogReference employee, IReadOnlyDictionary<int, SalesProductReference> products)
    {
        Customer = customer ?? throw new ArgumentNullException(nameof(customer));
        Warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
        Employee = employee ?? throw new ArgumentNullException(nameof(employee));
        if (products == null) throw new ArgumentNullException(nameof(products));
        var copy = new Dictionary<int, SalesProductReference>();
        foreach (var pair in products) copy.Add(pair.Key, pair.Value ?? throw new ArgumentException("Product reference is required.", nameof(products)));
        Products = new ReadOnlyDictionary<int, SalesProductReference>(copy);
    }
    public SalesCatalogReference Customer { get; }
    public SalesCatalogReference Warehouse { get; }
    public SalesCatalogReference Employee { get; }
    public IReadOnlyDictionary<int, SalesProductReference> Products { get; }
}

public interface ISalesReceiptWriteRepository
{
    Task<SalesReceiptReferenceSnapshot> GetReferenceSnapshotAsync(int customerId, int warehouseId, int employeeId,
        IReadOnlyCollection<int> productIds, CancellationToken cancellationToken);
    Task<SalesReceipt> InsertDraftAsync(SalesReceipt header, IReadOnlyList<SalesReceiptLine> lines, CancellationToken cancellationToken);
    Task<SalesReceipt?> LockHeaderAsync(long saleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SalesReceiptLine>> LoadLinesAsync(long saleId, CancellationToken cancellationToken);
    Task<SalesReceipt> ReplaceDraftAsync(SalesReceipt header, IReadOnlyList<SalesReceiptLine> lines, CancellationToken cancellationToken);
    Task AcquirePostingGateAsync(CancellationToken cancellationToken);
}
