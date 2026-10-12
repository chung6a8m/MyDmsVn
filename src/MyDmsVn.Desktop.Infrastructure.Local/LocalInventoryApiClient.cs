using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Inventory;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalInventoryApiClient : IInventoryApiClient
    {
        private readonly ISender _sender;

        public LocalInventoryApiClient(ISender sender) =>
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));

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
            var result = await _sender.Send(request, cancellationToken).ConfigureAwait(false);
            return ApiResponseMapper.Map(result, "Request");
        }
    }
}
