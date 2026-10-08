using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalFoundationApiClient : IFoundationApiClient
    {
        private readonly ISender _sender;

        public LocalFoundationApiClient(ISender sender)
        {
            _sender = sender;
        }

        public async Task<ApiResponse<FoundationStatus>> GetStatusAsync(
            FoundationStatusRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _sender
                .Send(new GetFoundationStatusQuery(request.ClientName), cancellationToken)
                .ConfigureAwait(false);
            return ApiResponseMapper.Map(result);
        }
    }
}
