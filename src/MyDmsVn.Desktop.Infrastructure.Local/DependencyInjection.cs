using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddLocalDesktopAdapter(this IServiceCollection services)
        {
            services.AddLocalRequestDiagnostics();
            services.TryAddSingleton<IDesktopNotificationService, DesktopNotificationCenter>();
            services.TryAddSingleton<IMessenger>(_ => new WeakReferenceMessenger());
            services.TryAddSingleton<IAsyncDelay, SystemAsyncDelay>();
            services.TryAddSingleton<IUiDispatcher, ImmediateUiDispatcher>();
            services.TryAddSingleton<IMonotonicClock, SystemMonotonicClock>();
            services.TryAddSingleton(DesktopSessionOptions.Default);
            services.TryAddSingleton<LocalDesktopSession>();
            services.TryAddSingleton<IDesktopSession>(
                provider => provider.GetRequiredService<LocalDesktopSession>());
            services.TryAddSingleton<IDesktopSessionActivity>(
                provider => provider.GetRequiredService<LocalDesktopSession>());
            services.RemoveAll<ICurrentUserAccessor>();
            services.AddScoped<LocalRequestCurrentUserAccessor>();
            services.AddScoped<ICurrentUserAccessor>(
                provider => provider.GetRequiredService<LocalRequestCurrentUserAccessor>());
            services.AddTransient<IFoundationApiClient, LocalFoundationApiClient>();
            services.AddTransient<IIdentityApiClient, LocalIdentityApiClient>();
            services.AddTransient<IPermissionApiClient, LocalPermissionApiClient>();
            services.AddTransient<IPermissionCatalogApiClient, LocalPermissionCatalogApiClient>();
            services.AddTransient<IProductApiClient, LocalProductApiClient>();
            services.AddTransient<IWarehouseApiClient, LocalWarehouseApiClient>();
            services.AddTransient<IEmployeeApiClient, LocalEmployeeApiClient>();
            services.AddTransient<ICustomerApiClient, LocalCustomerApiClient>();
            services.AddTransient<IGoodsReceiptApiClient, LocalGoodsReceiptApiClient>();
            services.AddTransient<IInventoryApiClient, LocalInventoryApiClient>();
            services.AddTransient<ProductCatalogLookupSource>();
            services.AddTransient<WarehouseCatalogLookupSource>();
            services.AddTransient<EmployeeCatalogLookupSource>();
            services.AddTransient<CustomerCatalogLookupSource>();
            return services;
        }
    }
}
