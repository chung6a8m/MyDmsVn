using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalFoundationApiClient : IFoundationApiClient
    {
        private readonly LocalRequestDispatcher _dispatcher;

        public LocalFoundationApiClient(LocalRequestDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public async Task<ApiResponse<FoundationStatus>> GetStatusAsync(
            FoundationStatusRequest request,
            CancellationToken cancellationToken)
        {
            return await _dispatcher.SendAsync(
                new GetFoundationStatusQuery(request.ClientName),
                null,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
