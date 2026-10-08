using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using Xunit;

namespace MyDmsVn.Server.Application.Tests
{
    public sealed class ApplicationPipelineTests
    {
        [Fact]
        public async Task Server_application_registers_and_dispatches_typed_requests()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();

            using (var provider = services.BuildServiceProvider())
            {
                var sender = provider.GetRequiredService<ISender>();

                var result = await sender.Send(
                    new GetFoundationStatusQuery(),
                    CancellationToken.None);

                Assert.False(result.IsError);
                Assert.True(result.Value.IsReady);
                Assert.Equal("Local", result.Value.Runtime);
            }
        }
    }
}
