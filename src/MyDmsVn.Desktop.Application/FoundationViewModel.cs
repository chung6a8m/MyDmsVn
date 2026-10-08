using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Desktop.Application
{
    public sealed class FoundationViewModel
    {
        private readonly IFoundationApiClient _apiClient;

        public FoundationViewModel(IFoundationApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public bool IsReady { get; private set; }

        public string? Runtime { get; private set; }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var status = await _apiClient.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            IsReady = status.IsReady;
            Runtime = status.Runtime;
        }
    }
}
