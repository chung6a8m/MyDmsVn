using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Infrastructure;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class DesktopCompositionTests
    {
        [Fact]
        public async Task Local_desktop_services_resolve_api_without_database_configuration()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddServerInfrastructure();
            services.AddLocalDesktopAdapter();

            using (var provider = services.BuildServiceProvider())
            {
                var client = provider.GetRequiredService<IFoundationApiClient>();

                var status = await client.GetStatusAsync(CancellationToken.None);

                Assert.True(status.IsReady);
                Assert.Equal("Local", status.Runtime);
            }
        }

        [Fact]
        public async Task Local_api_honors_pre_canceled_request()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddServerInfrastructure();
            services.AddLocalDesktopAdapter();

            using (var provider = services.BuildServiceProvider())
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var client = provider.GetRequiredService<IFoundationApiClient>();

                await Assert.ThrowsAsync<TaskCanceledException>(
                    () => client.GetStatusAsync(cancellation.Token));
            }
        }
    }
}
