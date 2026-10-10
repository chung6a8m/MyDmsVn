using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Domain.Inventory;

namespace MyDmsVn.Server.Application.Inventory;

public enum CatalogReferenceState
{
    Missing = 0,
    Inactive = 1,
    Active = 2,
}

public sealed class GoodsReceiptReferenceSnapshot
{
    public GoodsReceiptReferenceSnapshot(
        CatalogReferenceState warehouse,
        CatalogReferenceState employee,
        IReadOnlyDictionary<int, CatalogReferenceState> products)
    {
        Warehouse = warehouse;
        Employee = employee;
        Products = products;
    }

    public CatalogReferenceState Warehouse { get; }

    public CatalogReferenceState Employee { get; }

    public IReadOnlyDictionary<int, CatalogReferenceState> Products { get; }
}

public interface IGoodsReceiptWriteRepository
{
    Task<GoodsReceiptReferenceSnapshot> GetReferenceSnapshotAsync(
        int warehouseId,
        int employeeId,
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken);

    Task<GoodsReceipt> InsertDraftAsync(
        GoodsReceipt header,
        IReadOnlyList<GoodsReceiptLine> lines,
        CancellationToken cancellationToken);

    Task<GoodsReceipt?> LockHeaderAsync(
        long receiptId,
        CancellationToken cancellationToken);

    Task<GoodsReceipt> ReplaceDraftAsync(
        GoodsReceipt header,
        IReadOnlyList<GoodsReceiptLine> lines,
        CancellationToken cancellationToken);
}
