using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using Xunit;

namespace MyDmsVn.Server.Application.Tests
{
    public sealed class ServerApplicationRegistrationTests
    {
        [Fact]
        public void Registered_foundation_probe_reports_ready()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();

            using (var provider = services.BuildServiceProvider())
            {
                var probe = provider.GetRequiredService<IFoundationProbe>();

                var status = probe.GetStatus();

                Assert.True(status.IsReady);
                Assert.Equal("Local", status.Runtime);
            }
        }
    }
}
