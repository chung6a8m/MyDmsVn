using Microsoft.Extensions.DependencyInjection;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Server.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddServerApplication(
            this IServiceCollection services,
            string? configuredSuperuserUsername = null)
        {
            if (!string.IsNullOrWhiteSpace(configuredSuperuserUsername))
            {
                services.TryAddSingleton(
                    new SuperuserIdentityPolicy(configuredSuperuserUsername));
            }
            services.AddMediatR(
                configuration =>
                {
                    configuration.RegisterServicesFromAssemblyContaining<GetFoundationStatusQuery>();
                    configuration.AddOpenBehavior(typeof(ExceptionHandlingBehavior<,>));
                    configuration.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
                    configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
                });
            services.AddValidatorsFromAssemblyContaining<GetFoundationStatusQuery>(
                includeInternalTypes: true);
            services.AddSingleton<IFoundationProbe, FoundationProbe>();
            services.TryAddSingleton<IApplicationExceptionReporter, TraceApplicationExceptionReporter>();
            services.TryAddScoped<ICurrentUserAccessor, AnonymousCurrentUserAccessor>();
            services.TryAddScoped<IUserAccountValidator, RejectingUserAccountValidator>();
            services.TryAddSingleton<IUtcClock, SystemUtcClock>();
            services.TryAddSingleton<ISecurityAuditSink, NoOpSecurityAuditSink>();
            services.TryAddSingleton<ISecurityAuditFailureReporter, TraceSecurityAuditFailureReporter>();
            services.TryAddSingleton<IRequestCorrelationAccessor, EmptyRequestCorrelationAccessor>();
            services.TryAddSingleton<ISecurityAuditWriter, SecurityAuditWriter>();
            services.TryAddScoped<IPermissionStore, DenyAllPermissionStore>();
            services.TryAddSingleton<IPermissionRegistry>(
                PermissionRegistryComposition.CreateDefault());
            services.TryAddSingleton<PermissionAssignmentImportParser>();
            services.TryAddScoped<IPermissionAuthorizationService, PermissionAuthorizationService>();
            return services;
        }
    }
}
