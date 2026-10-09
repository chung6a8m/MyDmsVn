using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;
using RepoDb;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class CatalogMappingIntegrationTests
{
    [SqlServerFact]
    public async Task Catalog_entities_round_trip_through_registered_RepoDb_mappings()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());

            var services = new ServiceCollection();
            services.AddSqlPersistence(database.ConnectionString);
            using var provider = services.BuildServiceProvider();
            using var unitOfWork = await provider
                .GetRequiredService<IUnitOfWorkFactory>()
                .CreateAsync(CancellationToken.None);
            unitOfWork.BeginTransaction();
            var context = Assert.IsAssignableFrom<ISqlExecutionContext>(unitOfWork);
            var transaction = Assert.IsAssignableFrom<IDbTransaction>(context.Transaction);
            var createdAtUtc = new DateTime(2026, 10, 9, 3, 4, 5, DateTimeKind.Utc)
                .AddTicks(1_234_567);
            var updatedAtUtc = createdAtUtc.AddTicks(2_345_678);

            var productId = await context.Connection.InsertAsync<Product, int>(
                new Product
                {
                    Code = "SP001",
                    Name = "San pham 1",
                    Unit = "Cai",
                    IsActive = true,
                    CreatedAtUtc = createdAtUtc,
                    CreatedByUserId = 7,
                    UpdatedAtUtc = updatedAtUtc,
                    UpdatedByUserId = 8,
                },
                transaction: transaction,
                cancellationToken: CancellationToken.None);
            var warehouseId = await context.Connection.InsertAsync<Warehouse, int>(
                new Warehouse
                {
                    Code = "KHO01",
                    Name = "Kho chinh",
                    Address = "Ha Noi",
                    IsActive = true,
                    CreatedAtUtc = createdAtUtc,
                    CreatedByUserId = 7,
                    UpdatedAtUtc = updatedAtUtc,
                    UpdatedByUserId = 8,
                },
                transaction: transaction,
                cancellationToken: CancellationToken.None);
            var employeeId = await context.Connection.InsertAsync<Employee, int>(
                new Employee
                {
                    Code = "NV001",
                    Name = "Nhan vien 1",
                    Phone = "0900000000",
                    UserId = null,
                    IsActive = true,
                    CreatedAtUtc = createdAtUtc,
                    CreatedByUserId = 7,
                    UpdatedAtUtc = updatedAtUtc,
                    UpdatedByUserId = 8,
                },
                transaction: transaction,
                cancellationToken: CancellationToken.None);
            var customerId = await context.Connection.InsertAsync<Customer, int>(
                new Customer
                {
                    Code = "KH001",
                    Name = "Khach hang 1",
                    Address = "Da Nang",
                    Phone = "0911111111",
                    TaxCode = "0101234567",
                    IsActive = true,
                    CreatedAtUtc = createdAtUtc,
                    CreatedByUserId = 7,
                    UpdatedAtUtc = updatedAtUtc,
                    UpdatedByUserId = 8,
                },
                transaction: transaction,
                cancellationToken: CancellationToken.None);

            var mappedRows = await context.Connection.QuerySingleAsync<int>(
                "SELECT " +
                "(SELECT COUNT(*) FROM dbo.Products WHERE ProductId = @productId AND Code = N'SP001') + " +
                "(SELECT COUNT(*) FROM dbo.Warehouses WHERE WarehouseId = @warehouseId AND Address = N'Ha Noi') + " +
                "(SELECT COUNT(*) FROM dbo.Employees WHERE EmployeeId = @employeeId AND Phone = N'0900000000') + " +
                "(SELECT COUNT(*) FROM dbo.Customers WHERE CustomerId = @customerId AND TaxCode = N'0101234567');",
                new { productId, warehouseId, employeeId, customerId },
                transaction);

            Assert.Equal(4, mappedRows);

            var auditRows = (await context.Connection.QueryAsync<CatalogAuditRow>(
                "SELECT CreatedAtUtc, UpdatedAtUtc FROM dbo.Products WHERE ProductId = @productId " +
                "UNION ALL SELECT CreatedAtUtc, UpdatedAtUtc FROM dbo.Warehouses WHERE WarehouseId = @warehouseId " +
                "UNION ALL SELECT CreatedAtUtc, UpdatedAtUtc FROM dbo.Employees WHERE EmployeeId = @employeeId " +
                "UNION ALL SELECT CreatedAtUtc, UpdatedAtUtc FROM dbo.Customers WHERE CustomerId = @customerId;",
                new { productId, warehouseId, employeeId, customerId },
                transaction)).ToArray();
            Assert.Equal(4, auditRows.Length);
            Assert.All(auditRows, row =>
            {
                Assert.Equal(createdAtUtc, row.CreatedAtUtc);
                Assert.Equal(updatedAtUtc, row.UpdatedAtUtc);
            });
            unitOfWork.Rollback();
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private sealed class CatalogAuditRow
    {
        public DateTime CreatedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }
}
