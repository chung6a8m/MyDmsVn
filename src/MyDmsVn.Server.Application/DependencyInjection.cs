using Microsoft.Extensions.DependencyInjection;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
                    configuration.AddOpenBehavior(typeof(ExceptionHandlingBehavior<,>));
                    configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
                });
            services.AddValidatorsFromAssemblyContaining<GetFoundationStatusQuery>(
                includeInternalTypes: true);
            services.AddSingleton<IFoundationProbe, FoundationProbe>();
            services.TryAddSingleton<IApplicationExceptionReporter, TraceApplicationExceptionReporter>();
            return services;
        }
    }
}
