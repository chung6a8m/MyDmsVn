using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddLocalDesktopAdapter(this IServiceCollection services)
        {
            services.TryAddSingleton<IDesktopNotificationService, DesktopNotificationCenter>();
            services.TryAddSingleton<LocalDesktopSession>();
            services.TryAddSingleton<IDesktopSession>(
                provider => provider.GetRequiredService<LocalDesktopSession>());
            services.RemoveAll<ICurrentUserAccessor>();
            services.AddSingleton<ICurrentUserAccessor>(
                provider => provider.GetRequiredService<LocalDesktopSession>());
            services.AddTransient<IFoundationApiClient, LocalFoundationApiClient>();
            services.AddTransient<IIdentityApiClient, LocalIdentityApiClient>();
            return services;
        }
    }
}
