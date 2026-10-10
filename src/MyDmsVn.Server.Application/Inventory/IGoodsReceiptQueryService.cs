using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application.Inventory;

public interface IGoodsReceiptQueryService
{
    Task<PagedResult<GoodsReceiptDto>> ListAsync(
        GoodsReceiptListRequest request,
        CancellationToken cancellationToken);

    Task<GoodsReceiptDto?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken);
}
