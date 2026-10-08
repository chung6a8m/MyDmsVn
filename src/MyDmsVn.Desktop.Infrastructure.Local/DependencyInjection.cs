using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddLocalDesktopAdapter(this IServiceCollection services)
        {
            services.AddSingleton<IFoundationApiClient, LocalFoundationApiClient>();
            return services;
        }
    }
}
