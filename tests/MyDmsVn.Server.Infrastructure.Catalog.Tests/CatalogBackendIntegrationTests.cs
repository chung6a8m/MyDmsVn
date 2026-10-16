using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class CatalogBackendIntegrationTests
{
    [SqlServerFact]
    public async Task Inactive_catalog_rows_remain_historically_queryable_but_are_excluded_from_lookups()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());

            using var provider = CreateProvider(database.ConnectionString, new AllowAllPermissionStore());
            using var scope = provider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var product = await sender.Send(
                new CreateProductCommand(new SaveProductRequest("P-HISTORY", "Historical product", "Each")),
                CancellationToken.None);
            var warehouse = await sender.Send(
                new CreateWarehouseCommand(new SaveWarehouseRequest("W-HISTORY", "Historical warehouse", null)),
                CancellationToken.None);
            var employee = await sender.Send(
                new CreateEmployeeCommand(new SaveEmployeeRequest("E-HISTORY", "Historical employee", null, null)),
                CancellationToken.None);
            var customer = await sender.Send(
                new CreateCustomerCommand(new SaveCustomerRequest("C-HISTORY", "Historical customer", null, null, null)),
                CancellationToken.None);
            Assert.All(
                new[] { product.IsError, warehouse.IsError, employee.IsError, customer.IsError },
                Assert.False);

            Assert.False((await sender.Send(new SetProductActiveCommand(product.Value.Id, false), CancellationToken.None)).IsError);
            Assert.False((await sender.Send(new SetWarehouseActiveCommand(warehouse.Value.Id, false), CancellationToken.None)).IsError);
            Assert.False((await sender.Send(new SetEmployeeActiveCommand(employee.Value.Id, false), CancellationToken.None)).IsError);
            Assert.False((await sender.Send(new SetCustomerActiveCommand(customer.Value.Id, false), CancellationToken.None)).IsError);

            var productGet = await sender.Send(new GetProductByIdQuery(product.Value.Id), CancellationToken.None);
            var warehouseGet = await sender.Send(new GetWarehouseByIdQuery(warehouse.Value.Id), CancellationToken.None);
            var employeeGet = await sender.Send(new GetEmployeeByIdQuery(employee.Value.Id), CancellationToken.None);
            var customerGet = await sender.Send(new GetCustomerByIdQuery(customer.Value.Id), CancellationToken.None);
            Assert.All(
                new[] { productGet.Value.IsActive, warehouseGet.Value.IsActive, employeeGet.Value.IsActive, customerGet.Value.IsActive },
                Assert.False);

            var listRequest = new CatalogListRequest("HISTORY", 1, 25, includeInactive: true);
            Assert.Single((await sender.Send(new ListProductsQuery(listRequest), CancellationToken.None)).Value.Items);
            Assert.Single((await sender.Send(new ListWarehousesQuery(listRequest), CancellationToken.None)).Value.Items);
            Assert.Single((await sender.Send(new ListEmployeesQuery(listRequest), CancellationToken.None)).Value.Items);
            Assert.Single((await sender.Send(new ListCustomersQuery(listRequest), CancellationToken.None)).Value.Items);

            var lookupRequest = new CatalogLookupRequest("HISTORY", 25);
            Assert.Empty((await sender.Send(new LookupProductsQuery(lookupRequest), CancellationToken.None)).Value);
            Assert.Empty((await sender.Send(new LookupWarehousesQuery(lookupRequest), CancellationToken.None)).Value);
            Assert.Empty((await sender.Send(new LookupEmployeesQuery(lookupRequest), CancellationToken.None)).Value);
            Assert.Empty((await sender.Send(new LookupCustomersQuery(lookupRequest), CancellationToken.None)).Value);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Explicitly_denied_catalog_write_leaves_business_tables_unchanged()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());

            using var provider = CreateProvider(database.ConnectionString, new ExplicitDenyPermissionStore());
            using var scope = provider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var result = await sender.Send(
                new CreateProductCommand(new SaveProductRequest("P-DENIED", "Denied product", "Each")),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal(ErrorOr.ErrorType.Forbidden, result.FirstError.Type);
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(database.ConnectionString);
            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Products;"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Catalog_backend_crud_queries_audit_and_conflicts_use_real_sql_boundaries()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());

            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddSqlPersistence(database.ConnectionString);
            services.Replace(ServiceDescriptor.Scoped<ICurrentUserAccessor>(
                _ => new TestCurrentUserAccessor()));
            services.Replace(ServiceDescriptor.Scoped<IPermissionStore>(
                _ => new AllowAllPermissionStore()));
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var productB = await sender.Send(
                new CreateProductCommand(new SaveProductRequest("SP002", "Beta", "Cái")),
                CancellationToken.None);
            var productA = await sender.Send(
                new CreateProductCommand(new SaveProductRequest("SP001", "Alpha", "Hộp")),
                CancellationToken.None);
            var warehouse = await sender.Send(
                new CreateWarehouseCommand(new SaveWarehouseRequest("KHO01", "Kho chính", "Hà Nội")),
                CancellationToken.None);
            var employee = await sender.Send(
                new CreateEmployeeCommand(new SaveEmployeeRequest("NV01", "Nhân viên", "0901", null)),
                CancellationToken.None);
            var customer = await sender.Send(
                new CreateCustomerCommand(new SaveCustomerRequest("KH01", "Khách hàng", "Huế", "0902", "TAX01")),
                CancellationToken.None);

            Assert.All(new[] { productA.IsError, productB.IsError, warehouse.IsError, employee.IsError, customer.IsError }, Assert.False);

            var page = await sender.Send(
                new ListProductsQuery(new CatalogListRequest(null, 1, 1, includeInactive: true)),
                CancellationToken.None);
            Assert.False(page.IsError);
            Assert.Equal(2, page.Value.TotalCount);
            Assert.Equal("SP001", page.Value.Items.Single().Code);

            var update = await sender.Send(
                new UpdateProductCommand(productA.Value.Id, new SaveProductRequest("SP001", "Alpha mới", "Thùng")),
                CancellationToken.None);
            var deactivate = await sender.Send(
                new SetProductActiveCommand(productA.Value.Id, false),
                CancellationToken.None);
            var lookup = await sender.Send(
                new LookupProductsQuery(new CatalogLookupRequest("SP", 10)),
                CancellationToken.None);
            var get = await sender.Send(new GetProductByIdQuery(productA.Value.Id), CancellationToken.None);

            Assert.False(update.IsError);
            Assert.False(deactivate.IsError);
            Assert.False(lookup.IsError);
            Assert.DoesNotContain(lookup.Value, item => item.Id == productA.Value.Id);
            Assert.False(get.IsError);
            Assert.False(get.Value.IsActive);
            Assert.Equal("Alpha mới", get.Value.Name);

            var updateInactive = await sender.Send(
                new UpdateProductCommand(productA.Value.Id, new SaveProductRequest("SP001", "Vẫn ngưng", "Thùng")),
                CancellationToken.None);
            Assert.False(updateInactive.IsError);
            Assert.False(updateInactive.Value.IsActive);

            var duplicate = await sender.Send(
                new CreateProductCommand(new SaveProductRequest("sp002", "Trùng", "Cái")),
                CancellationToken.None);
            Assert.True(duplicate.IsError);
            Assert.Equal("Product.DuplicateCode", duplicate.FirstError.Code);

            var warehouseUpdate = await sender.Send(
                new UpdateWarehouseCommand(
                    warehouse.Value.Id,
                    new SaveWarehouseRequest("KHO01", "Kho cập nhật", "Đà Nẵng")),
                CancellationToken.None);
            var warehouseLookup = await sender.Send(
                new LookupWarehousesQuery(new CatalogLookupRequest("KHO", 10)),
                CancellationToken.None);
            var employeeUpdate = await sender.Send(
                new UpdateEmployeeCommand(
                    employee.Value.Id,
                    new SaveEmployeeRequest("NV01", "Nhân viên mới", "0903", null)),
                CancellationToken.None);
            var employeeLookup = await sender.Send(
                new LookupEmployeesQuery(new CatalogLookupRequest("NV", 10)),
                CancellationToken.None);
            var customerUpdate = await sender.Send(
                new UpdateCustomerCommand(
                    customer.Value.Id,
                    new SaveCustomerRequest("KH01", "Khách mới", "Huế", "0904", "TAX02")),
                CancellationToken.None);
            var customerInactive = await sender.Send(
                new SetCustomerActiveCommand(customer.Value.Id, false),
                CancellationToken.None);
            var customerLookup = await sender.Send(
                new LookupCustomersQuery(new CatalogLookupRequest("KH", 10)),
                CancellationToken.None);

            Assert.False(warehouseUpdate.IsError);
            Assert.Single(warehouseLookup.Value);
            Assert.False(employeeUpdate.IsError);
            Assert.Single(employeeLookup.Value);
            Assert.False(customerUpdate.IsError);
            Assert.False(customerInactive.IsError);
            Assert.Empty(customerLookup.Value);

            var warehouseGet = await sender.Send(
                new GetWarehouseByIdQuery(warehouse.Value.Id), CancellationToken.None);
            var employeePage = await sender.Send(
                new ListEmployeesQuery(new CatalogListRequest("mới", 1, 25, true)),
                CancellationToken.None);
            var customerGet = await sender.Send(
                new GetCustomerByIdQuery(customer.Value.Id), CancellationToken.None);
            Assert.Equal("Kho cập nhật", warehouseGet.Value.Name);
            Assert.Single(employeePage.Value.Items);
            Assert.False(customerGet.Value.IsActive);

            using var setupConnection = new Microsoft.Data.SqlClient.SqlConnection(database.ConnectionString);
            var linkedUserId = await setupConnection.QuerySingleAsync<int>(
                "INSERT dbo.Users (Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm, IsActive) " +
                "OUTPUT INSERTED.UserId VALUES (N'linked', N'LINKED', N'Linked', N'Local', N'hash', N'', N'BCrypt', 1);");
            var linkedEmployee = await sender.Send(
                new CreateEmployeeCommand(new SaveEmployeeRequest("NV02", "Liên kết", null, linkedUserId)),
                CancellationToken.None);
            var duplicateLink = await sender.Send(
                new CreateEmployeeCommand(new SaveEmployeeRequest("NV03", "Trùng liên kết", null, linkedUserId)),
                CancellationToken.None);
            var missingUser = await sender.Send(
                new CreateEmployeeCommand(new SaveEmployeeRequest("NV04", "Sai user", null, int.MaxValue)),
                CancellationToken.None);
            Assert.False(linkedEmployee.IsError);
            Assert.Equal("Employee.UserAlreadyLinked", duplicateLink.FirstError.Code);
            Assert.Equal("Employee.UserNotFound", missingUser.FirstError.Code);

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(database.ConnectionString);
            var audit = await connection.QuerySingleAsync<CatalogAudit>(
                "SELECT CreatedByUserId, UpdatedByUserId, UpdatedAtUtc FROM dbo.Products WHERE ProductId = @id;",
                new { id = productA.Value.Id });
            Assert.Equal(42, audit.CreatedByUserId);
            Assert.Equal(42, audit.UpdatedByUserId);
            Assert.NotNull(audit.UpdatedAtUtc);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private sealed class TestCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser Current { get; } =
            CurrentUser.Authenticated(42, "operator", "Operator", true);
    }

    private sealed class AllowAllPermissionStore : IPermissionStore
    {
        public Task<PermissionSnapshot> GetSnapshotAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PermissionSnapshot(true, false));
    }

    private sealed class ExplicitDenyPermissionStore : IPermissionStore
    {
        public Task<PermissionSnapshot> GetSnapshotAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PermissionSnapshot(false, true));
    }

    private static ServiceProvider CreateProvider(
        string connectionString,
        IPermissionStore permissionStore)
    {
        var services = new ServiceCollection();
        services.AddServerApplication();
        services.AddSqlPersistence(connectionString);
        services.Replace(ServiceDescriptor.Scoped<ICurrentUserAccessor>(
            _ => new TestCurrentUserAccessor()));
        services.Replace(ServiceDescriptor.Scoped<IPermissionStore>(_ => permissionStore));
        return services.BuildServiceProvider();
    }

    private sealed class CatalogAudit
    {
        public int? CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
