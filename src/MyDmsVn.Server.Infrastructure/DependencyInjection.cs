using Microsoft.Extensions.DependencyInjection;

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.Catalog;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddServerInfrastructure(this IServiceCollection services)
        {
            return services;
        }

        public static SqlPersistenceBuilder AddSqlPersistence(
            this IServiceCollection services,
            string connectionString)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));
            services.TryAddSingleton<IPasswordHasher, BcryptPasswordHasher>();
            services.TryAddSingleton<ILegacyPasswordVerifier, UnsupportedLegacyPasswordVerifier>();
            services.TryAddScoped<SqlIdentityStore>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRepoDbMapping, CatalogRepoDbMapping>());
            services.Replace(
                ServiceDescriptor.Scoped<IUserStore>(
                    serviceProvider => serviceProvider.GetRequiredService<SqlIdentityStore>()));
            services.Replace(
                ServiceDescriptor.Scoped<IPermissionStore>(
                    serviceProvider => serviceProvider.GetRequiredService<SqlIdentityStore>()));
            services.AddSingleton<RepoDbMappingInitializer>();
            services.AddScoped<IUnitOfWorkFactory>(serviceProvider =>
            {
                var registrations = serviceProvider
                    .GetServices<SqlRepositoryRegistration>()
                    .ToDictionary(
                        registration => registration.RepositoryType,
                        registration => registration.Create);
                return new SqlUnitOfWorkFactory(
                    serviceProvider.GetRequiredService<IDbConnectionFactory>(),
                    serviceProvider.GetRequiredService<RepoDbMappingInitializer>(),
                    serviceProvider,
                    registrations);
            });
            return new SqlPersistenceBuilder(services);
        }
    }
}
