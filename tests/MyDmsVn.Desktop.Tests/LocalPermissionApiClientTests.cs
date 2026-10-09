using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class LocalPermissionApiClientTests
    {
        [Fact]
        public async Task Local_client_preserves_the_application_permission_decision()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IPermissionAuthorizationService>(
                new StubPermissionAuthorizationService(isAllowed: true));
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();

            using (var provider = services.BuildServiceProvider())
            {
                var response = await provider.GetRequiredService<IPermissionApiClient>()
                    .CheckAsync(PermissionKeys.CatalogProductsWrite, CancellationToken.None);

                Assert.True(response.IsSuccess);
                Assert.Equal(PermissionKeys.CatalogProductsWrite, response.Data!.PermissionKey);
                Assert.True(response.Data.IsAllowed);
            }
        }

        private sealed class StubPermissionAuthorizationService
            : IPermissionAuthorizationService
        {
            private readonly bool _isAllowed;

            public StubPermissionAuthorizationService(bool isAllowed)
            {
                _isAllowed = isAllowed;
            }

            public Task<bool> IsAllowedAsync(
                string permissionKey,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_isAllowed);
            }
        }
    }
}
