using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Inventory;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalInventoryApiClient : IInventoryApiClient
    {
        private readonly LocalRequestDispatcher _dispatcher;

        public LocalInventoryApiClient(LocalRequestDispatcher dispatcher) =>
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        public Task<ApiResponse<PagedResult<StockBalanceRow>>> GetStockBalancesAsync(
            StockBalanceQuery request,
            CancellationToken cancellationToken) =>
            SendAsync(new GetStockBalancesQuery(request), cancellationToken);

        public Task<ApiResponse<PagedResult<StockCardRow>>> GetStockCardAsync(
            StockCardQuery request,
            CancellationToken cancellationToken) =>
            SendAsync(new GetStockCardQuery(request), cancellationToken);

        private async Task<ApiResponse<T>> SendAsync<T>(
            ApplicationRequest<T> request,
            CancellationToken cancellationToken)
        {
            return await _dispatcher.SendAsync(request, "Request", cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
