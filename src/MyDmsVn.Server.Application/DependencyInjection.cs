using Microsoft.Extensions.DependencyInjection;

namespace MyDmsVn.Server.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddServerApplication(this IServiceCollection services)
        {
            services.AddSingleton<IFoundationProbe, FoundationProbe>();
            return services;
        }
    }
}
