using Microsoft.Extensions.DependencyInjection;

using System;
using System.Linq;
using MyDmsVn.Server.Application.Persistence;
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
            services.AddSingleton<RepoDbMappingInitializer>();
            services.AddSingleton<IUnitOfWorkFactory>(serviceProvider =>
            {
                var registrations = serviceProvider
                    .GetServices<SqlRepositoryRegistration>()
                    .ToDictionary(
                        registration => registration.RepositoryType,
                        registration => registration.Create);
                return new SqlUnitOfWorkFactory(
                    serviceProvider.GetRequiredService<IDbConnectionFactory>(),
                    serviceProvider.GetRequiredService<RepoDbMappingInitializer>(),
                    serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                    registrations);
            });
            return new SqlPersistenceBuilder(services);
        }
    }
}
