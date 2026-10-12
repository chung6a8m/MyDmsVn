using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application.Inventory;

public interface IInventoryQueryService
{
    Task<PagedResult<StockBalanceRow>> GetStockBalancesAsync(
        StockBalanceQuery request,
        CancellationToken cancellationToken);

    Task<PagedResult<StockCardRow>> GetStockCardAsync(
        StockCardQuery request,
        CancellationToken cancellationToken);
}
