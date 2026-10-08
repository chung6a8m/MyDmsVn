using System.Threading;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
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

                var response = await client.GetStatusAsync(
                    new FoundationStatusRequest("Desktop"),
                    CancellationToken.None);

                Assert.True(response.IsSuccess);
                Assert.True(response.Data!.IsReady);
                Assert.Equal("Local", response.Data.Runtime);
            }
        }

        [Fact]
        public async Task Local_api_returns_validation_failure_as_api_response()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddServerInfrastructure();
            services.AddLocalDesktopAdapter();

            using (var provider = services.BuildServiceProvider())
            {
                var client = provider.GetRequiredService<IFoundationApiClient>();

                var response = await client.GetStatusAsync(
                    new FoundationStatusRequest(string.Empty),
                    CancellationToken.None);

                Assert.False(response.IsSuccess);
                Assert.Equal("ValidationError", response.Error!.Code);
                Assert.Equal("clientName", response.Error.Details[0].Field);
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

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => client.GetStatusAsync(
                        new FoundationStatusRequest("Desktop"),
                        cancellation.Token));
            }
        }
    }
}
