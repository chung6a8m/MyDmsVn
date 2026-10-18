using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;
using LinqToDB;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.IntegrationTests.Pilot;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[Collection(CatalogSqlCollectionFixture.CollectionName)]
public sealed class CatalogMappingIntegrationTests
{
    private readonly CatalogSqlCollectionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public CatalogMappingIntegrationTests(CatalogSqlCollectionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [SqlServerFact]
    public Task Catalog_write_repository_uses_database_creation_time_instead_of_entity_creation_time() =>
        _fixture.RunAsync(nameof(Catalog_write_repository_uses_database_creation_time_instead_of_entity_creation_time), _output, async (test, token) =>
        {
            var services = new ServiceCollection();
            services.AddSqlPersistence(test.ConnectionString);
            var provider = CatalogSqlCollectionFixture.Track(test, services.BuildServiceProvider());
            var unitOfWork = CatalogSqlCollectionFixture.Track(test, await provider
                .GetRequiredService<IUnitOfWorkFactory>()
                .CreateAsync(CancellationToken.None));
            unitOfWork.BeginTransaction();
            var context = Assert.IsAssignableFrom<ISqlExecutionContext>(unitOfWork);
            var transaction = Assert.IsAssignableFrom<IDbTransaction>(context.Transaction);
            var repository = unitOfWork.Repository<ICatalogWriteRepository>();
            var staleEntityCreationUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var product = new Product
            {
                Code = "SP-MIN",
                Name = "San pham",
                Unit = "Cai",
                CreatedAtUtc = staleEntityCreationUtc,
            };
            var warehouse = new Warehouse
            {
                Code = "KHO-MIN",
                Name = "Kho",
                CreatedAtUtc = staleEntityCreationUtc,
            };
            var employee = new Employee
            {
                Code = "NV-MIN",
                Name = "Nhan vien",
                CreatedAtUtc = staleEntityCreationUtc,
            };
            var customer = new Customer
            {
                Code = "KH-MIN",
                Name = "Khach hang",
                CreatedAtUtc = staleEntityCreationUtc,
            };
            var beforeInsertUtc = await context.Connection.QuerySingleAsync<DateTime>(
                "SELECT SYSUTCDATETIME();",
                transaction: transaction);

            var productId = await repository.InsertProductAsync(product, CancellationToken.None);
            var warehouseId = await repository.InsertWarehouseAsync(warehouse, CancellationToken.None);
            var employeeId = await repository.InsertEmployeeAsync(employee, CancellationToken.None);
            var customerId = await repository.InsertCustomerAsync(customer, CancellationToken.None);
            var afterInsertUtc = await context.Connection.QuerySingleAsync<DateTime>(
                "SELECT SYSUTCDATETIME();",
                transaction: transaction);

            var persistedDefaults = (await context.Connection.QueryAsync<CatalogDefaultRow>(
                "SELECT IsActive, CreatedAtUtc FROM dbo.Products WHERE ProductId = @productId " +
                "UNION ALL SELECT IsActive, CreatedAtUtc FROM dbo.Warehouses WHERE WarehouseId = @warehouseId " +
                "UNION ALL SELECT IsActive, CreatedAtUtc FROM dbo.Employees WHERE EmployeeId = @employeeId " +
                "UNION ALL SELECT IsActive, CreatedAtUtc FROM dbo.Customers WHERE CustomerId = @customerId;",
                new { productId, warehouseId, employeeId, customerId },
                transaction)).ToArray();

            Assert.Equal(4, persistedDefaults.Length);
            Assert.All(persistedDefaults, row =>
            {
                Assert.True(row.IsActive);
                Assert.InRange(row.CreatedAtUtc, beforeInsertUtc, afterInsertUtc);
            });
            unitOfWork.Rollback();
        });

    // The logical method name stays frozen because the accepted reuse adapter invokes it directly.
    // Its provider-specific body now exercises registered LINQ mappings, with independent native reads.
    [SqlServerFact]
    public Task Catalog_entities_round_trip_explicit_audit_values_through_registered_RepoDb_mappings() =>
        _fixture.RunAsync(nameof(Catalog_entities_round_trip_explicit_audit_values_through_registered_RepoDb_mappings), _output, async (test, token) =>
        {
            var services = new ServiceCollection(); services.AddSqlPersistence(test.ConnectionString);
            var provider = CatalogSqlCollectionFixture.Track(test, services.BuildServiceProvider());
            var unitOfWork = CatalogSqlCollectionFixture.Track(test, await provider.GetRequiredService<IUnitOfWorkFactory>().CreateAsync(token));
            unitOfWork.BeginTransaction();
            var context = Assert.IsAssignableFrom<ISqlExecutionContext>(unitOfWork);
            var connection = Assert.IsType<SqlConnection>(context.Connection);
            var transaction = Assert.IsType<SqlTransaction>(context.Transaction);
            var createdAtUtc = new DateTime(2026, 10, 9, 3, 4, 5, DateTimeKind.Utc).AddTicks(1_234_567);
            var updatedAtUtc = createdAtUtc.AddTicks(2_345_678);
            using (var db = provider.GetRequiredService<ILinqToDbSessionFactory>().Borrow(context, true))
            {
                var productId = await db.InsertWithInt32IdentityAsync(new Product
                { Code = "SP001", Name = "San pham 1", Unit = "Cai", IsActive = true, CreatedAtUtc = createdAtUtc, CreatedByUserId = 7, UpdatedAtUtc = updatedAtUtc, UpdatedByUserId = 8 }, token: token);
                Assert.True(Convert.ToInt32(productId) > 0);
                var warehouseId = await db.InsertWithInt32IdentityAsync(new Warehouse
                { Code = "KHO01", Name = "Kho chinh", Address = "Ha Noi", IsActive = true, CreatedAtUtc = createdAtUtc, CreatedByUserId = 7, UpdatedAtUtc = updatedAtUtc, UpdatedByUserId = 8 }, token: token);
                Assert.True(Convert.ToInt32(warehouseId) > 0);
                var employeeId = await db.InsertWithInt32IdentityAsync(new Employee
                { Code = "NV001", Name = "Nhan vien 1", Phone = "0900000000", UserId = null, IsActive = true, CreatedAtUtc = createdAtUtc, CreatedByUserId = 7, UpdatedAtUtc = updatedAtUtc, UpdatedByUserId = 8 }, token: token);
                Assert.True(Convert.ToInt32(employeeId) > 0);
                var customerId = await db.InsertWithInt32IdentityAsync(new Customer
                { Code = "KH001", Name = "Khach hang 1", Address = "Da Nang", Phone = "0911111111", TaxCode = "0101234567", IsActive = true, CreatedAtUtc = createdAtUtc, CreatedByUserId = 7, UpdatedAtUtc = updatedAtUtc, UpdatedByUserId = 8 }, token: token);
                Assert.True(Convert.ToInt32(customerId) > 0);
                Assert.Equal(4, await PilotSql.Scalar<int>(connection, transaction,
                    "SELECT (SELECT COUNT(*) FROM dbo.Products WHERE ProductId=@p AND Code=N'SP001') + " +
                    "(SELECT COUNT(*) FROM dbo.Warehouses WHERE WarehouseId=@w AND Address=N'Ha Noi') + " +
                    "(SELECT COUNT(*) FROM dbo.Employees WHERE EmployeeId=@e AND Phone=N'0900000000') + " +
                    "(SELECT COUNT(*) FROM dbo.Customers WHERE CustomerId=@c AND TaxCode=N'0101234567')",
                    PilotSql.P("@p", SqlDbType.Int, productId), PilotSql.P("@w", SqlDbType.Int, warehouseId), PilotSql.P("@e", SqlDbType.Int, employeeId), PilotSql.P("@c", SqlDbType.Int, customerId)));
                foreach (var table in new[] { "Products", "Warehouses", "Employees", "Customers" })
                    Assert.Equal(1, await PilotSql.Scalar<int>(connection, transaction,
                        "SELECT COUNT(*) FROM dbo." + table + " WHERE CreatedAtUtc=@created AND UpdatedAtUtc=@updated AND CreatedByUserId=7 AND UpdatedByUserId=8",
                        PilotSql.P("@created", SqlDbType.DateTime2, createdAtUtc), PilotSql.P("@updated", SqlDbType.DateTime2, updatedAtUtc)));
            }
            unitOfWork.Rollback();
        });

    private sealed class CatalogDefaultRow
    {
        public bool IsActive { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
