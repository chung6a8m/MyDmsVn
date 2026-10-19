using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Catalog;

internal sealed class SqlCatalogWriteRepository(ISqlExecutionContext context, ILinqToDbSessionFactory sessions) : ICatalogWriteRepository
{
    public Task<int> InsertProductAsync(Product product, CancellationToken cancellationToken) =>
        TranslateAsync(async () =>
        {
            if (product is null) throw new ArgumentNullException(nameof(product));
            cancellationToken.ThrowIfCancellationRequested();
            using var db = sessions.Borrow(context, requireTransaction: true);
            // Explicit create allowlist: identity/server time/update audit stay database-owned.
            return await db.GetTable<Product>()
                .Value(e => e.Code, product.Code)
                .Value(e => e.Name, product.Name)
                .Value(e => e.Unit, product.Unit)
                .Value(e => e.IsActive, product.IsActive)
                .Value(e => e.CreatedByUserId, product.CreatedByUserId)
                .InsertWithInt32IdentityAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Product insert returned no identity.");
        }, employee: false);

    public Task<Product?> UpdateProductAsync(Product product, CancellationToken cancellationToken) =>
        TranslateAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var db = sessions.Borrow(context, requireTransaction: true);
            // Ordinary edits cannot overwrite creation audit or reactivate an inactive row.
            var affected = await db.GetTable<Product>().Where(e => e.Id == product.Id)
                .Set(e => e.Code, product.Code)
                .Set(e => e.Name, product.Name)
                .Set(e => e.Unit, product.Unit)
                .Set(e => e.UpdatedAtUtc, product.UpdatedAtUtc)
                .Set(e => e.UpdatedByUserId, product.UpdatedByUserId)
                .UpdateAsync(cancellationToken).ConfigureAwait(false);
            return affected == 1
                ? await db.GetTable<Product>().SingleAsync(e => e.Id == product.Id, cancellationToken).ConfigureAwait(false)
                : null;
        }, employee: false);

    public async Task<bool> SetProductActiveAsync(int id, bool isActive, DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = sessions.Borrow(context, requireTransaction: true);
        return await db.GetTable<Product>().Where(e => e.Id == id)
            .Set(e => e.IsActive, isActive)
            .Set(e => e.UpdatedAtUtc, updatedAtUtc)
            .Set(e => e.UpdatedByUserId, updatedByUserId)
            .UpdateAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public Task<int> InsertWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken) =>
        TranslateAsync(async () =>
        {
            if (warehouse is null) throw new ArgumentNullException(nameof(warehouse));
            cancellationToken.ThrowIfCancellationRequested();
            using var db = sessions.Borrow(context, requireTransaction: true);
            // Explicit create allowlist: identity/server time/update audit stay database-owned.
            return await db.GetTable<Warehouse>()
                .Value(e => e.Code, warehouse.Code)
                .Value(e => e.Name, warehouse.Name)
                .Value(e => e.Address, warehouse.Address)
                .Value(e => e.IsActive, warehouse.IsActive)
                .Value(e => e.CreatedByUserId, warehouse.CreatedByUserId)
                .InsertWithInt32IdentityAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Warehouse insert returned no identity.");
        }, employee: false);

    public Task<Warehouse?> UpdateWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken) =>
        TranslateAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var db = sessions.Borrow(context, requireTransaction: true);
            // Ordinary edits cannot overwrite creation audit or reactivate an inactive row.
            var affected = await db.GetTable<Warehouse>().Where(e => e.Id == warehouse.Id)
                .Set(e => e.Code, warehouse.Code)
                .Set(e => e.Name, warehouse.Name)
                .Set(e => e.Address, warehouse.Address)
                .Set(e => e.UpdatedAtUtc, warehouse.UpdatedAtUtc)
                .Set(e => e.UpdatedByUserId, warehouse.UpdatedByUserId)
                .UpdateAsync(cancellationToken).ConfigureAwait(false);
            return affected == 1
                ? await db.GetTable<Warehouse>().SingleAsync(e => e.Id == warehouse.Id, cancellationToken).ConfigureAwait(false)
                : null;
        }, employee: false);

    public async Task<bool> SetWarehouseActiveAsync(int id, bool isActive, DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = sessions.Borrow(context, requireTransaction: true);
        return await db.GetTable<Warehouse>().Where(e => e.Id == id)
            .Set(e => e.IsActive, isActive)
            .Set(e => e.UpdatedAtUtc, updatedAtUtc)
            .Set(e => e.UpdatedByUserId, updatedByUserId)
            .UpdateAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public Task<int> InsertEmployeeAsync(Employee employee, CancellationToken cancellationToken) =>
        TranslateAsync(async () =>
        {
            if (employee is null) throw new ArgumentNullException(nameof(employee));
            cancellationToken.ThrowIfCancellationRequested();
            using var db = sessions.Borrow(context, requireTransaction: true);
            // Explicit create allowlist: identity/server time/update audit stay database-owned.
            return await db.GetTable<Employee>()
                .Value(e => e.Code, employee.Code)
                .Value(e => e.Name, employee.Name)
                .Value(e => e.Phone, employee.Phone)
                .Value(e => e.UserId, employee.UserId)
                .Value(e => e.IsActive, employee.IsActive)
                .Value(e => e.CreatedByUserId, employee.CreatedByUserId)
                .InsertWithInt32IdentityAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Employee insert returned no identity.");
        }, employee: true);

    public Task<Employee?> UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken) =>
        TranslateAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var db = sessions.Borrow(context, requireTransaction: true);
            // Ordinary edits cannot overwrite creation audit or reactivate an inactive row.
            var affected = await db.GetTable<Employee>().Where(e => e.Id == employee.Id)
                .Set(e => e.Code, employee.Code)
                .Set(e => e.Name, employee.Name)
                .Set(e => e.Phone, employee.Phone)
                .Set(e => e.UserId, employee.UserId)
                .Set(e => e.UpdatedAtUtc, employee.UpdatedAtUtc)
                .Set(e => e.UpdatedByUserId, employee.UpdatedByUserId)
                .UpdateAsync(cancellationToken).ConfigureAwait(false);
            return affected == 1
                ? await db.GetTable<Employee>().SingleAsync(e => e.Id == employee.Id, cancellationToken).ConfigureAwait(false)
                : null;
        }, employee: true);

    public async Task<bool> SetEmployeeActiveAsync(int id, bool isActive, DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = sessions.Borrow(context, requireTransaction: true);
        return await db.GetTable<Employee>().Where(e => e.Id == id)
            .Set(e => e.IsActive, isActive)
            .Set(e => e.UpdatedAtUtc, updatedAtUtc)
            .Set(e => e.UpdatedByUserId, updatedByUserId)
            .UpdateAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public Task<int> InsertCustomerAsync(Customer customer, CancellationToken cancellationToken) =>
        TranslateAsync(async () =>
        {
            if (customer is null) throw new ArgumentNullException(nameof(customer));
            cancellationToken.ThrowIfCancellationRequested();
            using var db = sessions.Borrow(context, requireTransaction: true);
            // Explicit create allowlist: identity/server time/update audit stay database-owned.
            return await db.GetTable<Customer>()
                .Value(e => e.Code, customer.Code)
                .Value(e => e.Name, customer.Name)
                .Value(e => e.Address, customer.Address)
                .Value(e => e.Phone, customer.Phone)
                .Value(e => e.TaxCode, customer.TaxCode)
                .Value(e => e.IsActive, customer.IsActive)
                .Value(e => e.CreatedByUserId, customer.CreatedByUserId)
                .InsertWithInt32IdentityAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Customer insert returned no identity.");
        }, employee: false);

    public Task<Customer?> UpdateCustomerAsync(Customer customer, CancellationToken cancellationToken) =>
        TranslateAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var db = sessions.Borrow(context, requireTransaction: true);
            // Ordinary edits cannot overwrite creation audit or reactivate an inactive row.
            var affected = await db.GetTable<Customer>().Where(e => e.Id == customer.Id)
                .Set(e => e.Code, customer.Code)
                .Set(e => e.Name, customer.Name)
                .Set(e => e.Address, customer.Address)
                .Set(e => e.Phone, customer.Phone)
                .Set(e => e.TaxCode, customer.TaxCode)
                .Set(e => e.UpdatedAtUtc, customer.UpdatedAtUtc)
                .Set(e => e.UpdatedByUserId, customer.UpdatedByUserId)
                .UpdateAsync(cancellationToken).ConfigureAwait(false);
            return affected == 1
                ? await db.GetTable<Customer>().SingleAsync(e => e.Id == customer.Id, cancellationToken).ConfigureAwait(false)
                : null;
        }, employee: false);

    public async Task<bool> SetCustomerActiveAsync(int id, bool isActive, DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = sessions.Borrow(context, requireTransaction: true);
        return await db.GetTable<Customer>().Where(e => e.Id == id)
            .Set(e => e.IsActive, isActive)
            .Set(e => e.UpdatedAtUtc, updatedAtUtc)
            .Set(e => e.UpdatedByUserId, updatedByUserId)
            .UpdateAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    private static async Task<T> TranslateAsync<T>(Func<Task<T>> operation, bool employee)
    {
        try { return await operation().ConfigureAwait(false); }
        catch (SqlException error) when (error.Number == 2601 || error.Number == 2627)
        {
            var conflict = employee && error.Message.IndexOf("UX_Employees_UserId", StringComparison.OrdinalIgnoreCase) >= 0
                ? CatalogWriteConflict.EmployeeUserAlreadyLinked
                : CatalogWriteConflict.DuplicateCode;
            throw new CatalogWriteConflictException(conflict);
        }
        catch (SqlException error) when (employee && error.Number == 547)
        {
            throw new CatalogWriteConflictException(CatalogWriteConflict.EmployeeUserNotFound);
        }
    }
}
