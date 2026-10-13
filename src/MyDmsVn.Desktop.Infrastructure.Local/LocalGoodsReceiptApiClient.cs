using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Inventory;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalGoodsReceiptApiClient : IGoodsReceiptApiClient
    {
        private readonly LocalRequestDispatcher _dispatcher;

        public LocalGoodsReceiptApiClient(LocalRequestDispatcher dispatcher) =>
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(
            SaveGoodsReceiptRequest request,
            CancellationToken cancellationToken) =>
            SendAsync(new CreateGoodsReceiptDraftCommand(request), cancellationToken);

        public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(
            UpdateGoodsReceiptDraftRequest request,
            CancellationToken cancellationToken) =>
            SendAsync(new UpdateGoodsReceiptDraftCommand(request), cancellationToken);

        public Task<ApiResponse<GoodsReceiptDto>> GetAsync(
            long id,
            CancellationToken cancellationToken) =>
            SendAsync(new GetGoodsReceiptByIdQuery(id), cancellationToken);

        public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(
            GoodsReceiptListRequest request,
            CancellationToken cancellationToken) =>
            SendAsync(new ListGoodsReceiptsQuery(request), cancellationToken);

        public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(
            PostGoodsReceiptRequest request,
            CancellationToken cancellationToken) =>
            SendAsync(new PostGoodsReceiptCommand(request), cancellationToken);

        private async Task<ApiResponse<T>> SendAsync<T>(
            ApplicationRequest<T> request,
            CancellationToken cancellationToken)
        {
            return await _dispatcher.SendAsync(request, "Request", cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
