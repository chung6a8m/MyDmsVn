using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Security;
using System;
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

        [Fact]
        public async Task Local_client_maps_permission_service_exceptions_to_a_safe_response()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IPermissionAuthorizationService>(
                new ThrowingPermissionAuthorizationService());
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();

            using (var provider = services.BuildServiceProvider())
            {
                var response = await provider.GetRequiredService<IPermissionApiClient>()
                    .CheckAsync(PermissionKeys.CatalogProductsWrite, CancellationToken.None);

                Assert.False(response.IsSuccess);
                Assert.Equal("InternalError", response.Error!.Code);
                Assert.Equal("An unexpected error occurred.", response.Error.Message);
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

            public Task<bool> IsAllowedAsync(
                int userId,
                string permissionKey,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_isAllowed);
            }
        }

        private sealed class ThrowingPermissionAuthorizationService
            : IPermissionAuthorizationService
        {
            public Task<bool> IsAllowedAsync(
                string permissionKey,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("sensitive storage failure");
            }

            public Task<bool> IsAllowedAsync(
                int userId,
                string permissionKey,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("sensitive storage failure");
            }
        }
    }
}
