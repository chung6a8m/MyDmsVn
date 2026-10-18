using Microsoft.Extensions.DependencyInjection;

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.Catalog;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Inventory;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Server.Infrastructure.Security;

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
            services.TryAddSingleton<LinqToDbMappingSchemaProvider>();
            services.TryAddSingleton<ILinqToDbSessionFactory, LinqToDbSessionFactory>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILinqToDbMapping, CatalogLinqToDbMapping>());
            services.TryAddSingleton<IPasswordHasher, BcryptPasswordHasher>();
            services.TryAddSingleton<ILegacyPasswordVerifier, UnsupportedLegacyPasswordVerifier>();
            services.TryAddScoped<SqlIdentityStore>();
            services.TryAddScoped<SqlSecurityAdministrationStore>();
            services.TryAddScoped<ISecurityAdministrationStore>(
                serviceProvider => serviceProvider.GetRequiredService<SqlSecurityAdministrationStore>());
            services.TryAddScoped<IPermissionAssignmentImportStore>(
                serviceProvider => serviceProvider.GetRequiredService<SqlSecurityAdministrationStore>());
            services.TryAddScoped<IProductQueryService, SqlProductQueryService>();
            services.TryAddScoped<IWarehouseQueryService, SqlWarehouseQueryService>();
            services.TryAddScoped<IEmployeeQueryService, SqlEmployeeQueryService>();
            services.TryAddScoped<ICustomerQueryService, SqlCustomerQueryService>();
            services.TryAddScoped<IGoodsReceiptQueryService, SqlGoodsReceiptQueryService>();
            services.TryAddScoped<IInventoryQueryService, SqlInventoryQueryService>();
            services.TryAddScoped<IStockReconciliationQueryService, SqlStockReconciliationQueryService>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILinqToDbMapping, InventoryLinqToDbMapping>());
            services.Replace(
                ServiceDescriptor.Scoped<IUserStore>(
                    serviceProvider => serviceProvider.GetRequiredService<SqlIdentityStore>()));
            services.Replace(
                ServiceDescriptor.Scoped<IUserAccountValidator>(
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
            return new SqlPersistenceBuilder(services)
                .AddRepository<ICatalogWriteRepository, SqlCatalogWriteRepository>()
                .AddRepository<IGoodsReceiptWriteRepository, SqlGoodsReceiptWriteRepository>()
                .AddRepository<ISecurityAdministrationRepository, SqlSecurityAdministrationRepository>()
                .AddRepository<IPermissionAssignmentImportRepository, SqlSecurityAdministrationRepository>();
        }
    }
}
