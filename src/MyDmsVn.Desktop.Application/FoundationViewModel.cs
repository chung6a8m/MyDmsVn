using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

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
            var response = await _apiClient
                .GetStatusAsync(new FoundationStatusRequest("Desktop"), cancellationToken)
                .ConfigureAwait(false);
            IsReady = response.IsSuccess && response.Data!.IsReady;
            Runtime = response.IsSuccess ? response.Data!.Runtime : null;
        }
    }
}
