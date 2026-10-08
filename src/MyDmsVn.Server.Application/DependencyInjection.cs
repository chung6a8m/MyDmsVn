using Microsoft.Extensions.DependencyInjection;

using FluentValidation;

namespace MyDmsVn.Server.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddServerApplication(this IServiceCollection services)
        {
            services.AddMediatR(
                configuration =>
                {
                    configuration.RegisterServicesFromAssemblyContaining<GetFoundationStatusQuery>();
                    configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
                });
            services.AddValidatorsFromAssemblyContaining<GetFoundationStatusQuery>(
                includeInternalTypes: true);
            services.AddSingleton<IFoundationProbe, FoundationProbe>();
            return services;
        }
    }
}
