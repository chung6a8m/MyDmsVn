using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalFoundationApiClient : IFoundationApiClient
    {
        private readonly IFoundationProbe _probe;

        public LocalFoundationApiClient(IFoundationProbe probe)
        {
            _probe = probe;
        }

        public Task<FoundationStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<FoundationStatus>(cancellationToken);
            }

            return Task.FromResult(_probe.GetStatus());
        }
    }
}
