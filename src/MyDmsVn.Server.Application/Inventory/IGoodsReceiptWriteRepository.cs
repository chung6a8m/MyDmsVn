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

public sealed class GoodsReceiptPostSnapshot
{
    public GoodsReceiptPostSnapshot(
        IReadOnlyList<GoodsReceiptLine> lines,
        GoodsReceiptReferenceSnapshot references)
    {
        Lines = lines;
        References = references;
    }

    public IReadOnlyList<GoodsReceiptLine> Lines { get; }

    public GoodsReceiptReferenceSnapshot References { get; }
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

    Task<GoodsReceiptPostSnapshot> LockPostSnapshotAsync(
        GoodsReceipt header,
        CancellationToken cancellationToken);

    Task<bool> TryMarkPostedAsync(
        long receiptId,
        byte[] expectedVersion,
        System.DateTime postedAtUtc,
        int postedByUserId,
        CancellationToken cancellationToken);

    Task AcquirePostingGateAsync(CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<int, StockValuationState>> LockValuationBalancesAsync(
        int warehouseId, IReadOnlyCollection<int> productIds, CancellationToken cancellationToken);

    Task ApplyPostedInventoryAsync(
        GoodsReceipt header, IReadOnlyList<GoodsReceiptLine> lines,
        System.DateTime postedAtUtc, int postedByUserId, CancellationToken cancellationToken);

    Task<GoodsReceipt> ReplaceDraftAsync(
        GoodsReceipt header,
        IReadOnlyList<GoodsReceiptLine> lines,
        CancellationToken cancellationToken);
}
