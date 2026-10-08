using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class FoundationViewModelTests
    {
        [Fact]
        public async Task InitializeAsync_reflects_ready_status()
        {
            var viewModel = new FoundationViewModel(new ReadyApiClient());

            await viewModel.InitializeAsync(CancellationToken.None);

            Assert.True(viewModel.IsReady);
            Assert.Equal("Local", viewModel.Runtime);
        }

        private sealed class ReadyApiClient : IFoundationApiClient
        {
            public Task<FoundationStatus> GetStatusAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new FoundationStatus(true, "Local"));
            }
        }
    }
}
