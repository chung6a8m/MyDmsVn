using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace MyDmsVn.Server.Infrastructure.Catalog;

internal sealed class SqlCatalogWriteRepository : ICatalogWriteRepository
{
    private readonly ISqlExecutionContext _context;

    public SqlCatalogWriteRepository(ISqlExecutionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<int> InsertProductAsync(Product product, CancellationToken cancellationToken)
    {
        return InsertWithConflictTranslationAsync(
            product,
            CatalogRepoDbWriteFields.ProductCreate,
            cancellationToken);
    }

    public Task<Product?> UpdateProductAsync(Product product, CancellationToken cancellationToken)
    {
        return UpdateWithConflictTranslationAsync(
            product,
            CatalogRepoDbWriteFields.ProductUpdate,
            product.Id,
            cancellationToken);
    }

    public Task<bool> SetProductActiveAsync(
        int id,
        bool isActive,
        DateTime updatedAtUtc,
        int? updatedByUserId,
        CancellationToken cancellationToken)
    {
        return UpdateAsync(
            new Product
            {
                Id = id,
                IsActive = isActive,
                UpdatedAtUtc = updatedAtUtc,
                UpdatedByUserId = updatedByUserId,
            },
            CatalogRepoDbWriteFields.ProductActiveUpdate,
            cancellationToken);
    }

    public Task<int> InsertWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        return InsertWithConflictTranslationAsync(
            warehouse, CatalogRepoDbWriteFields.WarehouseCreate, cancellationToken);
    }

    public Task<Warehouse?> UpdateWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken) =>
        UpdateWithConflictTranslationAsync(warehouse, CatalogRepoDbWriteFields.WarehouseUpdate, warehouse.Id, cancellationToken);

    public Task<bool> SetWarehouseActiveAsync(int id, bool isActive, DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken) =>
        UpdateAsync(new Warehouse { Id = id, IsActive = isActive, UpdatedAtUtc = updatedAtUtc, UpdatedByUserId = updatedByUserId }, CatalogRepoDbWriteFields.WarehouseActiveUpdate, cancellationToken);

    public Task<int> InsertEmployeeAsync(Employee employee, CancellationToken cancellationToken)
    {
        return InsertEmployeeWithConflictTranslationAsync(employee, cancellationToken);
    }

    public Task<Employee?> UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken) =>
        UpdateEmployeeWithConflictTranslationAsync(employee, cancellationToken);

    public Task<bool> SetEmployeeActiveAsync(int id, bool isActive, DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken) =>
        UpdateAsync(new Employee { Id = id, IsActive = isActive, UpdatedAtUtc = updatedAtUtc, UpdatedByUserId = updatedByUserId }, CatalogRepoDbWriteFields.EmployeeActiveUpdate, cancellationToken);

    public Task<int> InsertCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        return InsertWithConflictTranslationAsync(
            customer, CatalogRepoDbWriteFields.CustomerCreate, cancellationToken);
    }

    public Task<Customer?> UpdateCustomerAsync(Customer customer, CancellationToken cancellationToken) =>
        UpdateWithConflictTranslationAsync(customer, CatalogRepoDbWriteFields.CustomerUpdate, customer.Id, cancellationToken);

    public Task<bool> SetCustomerActiveAsync(int id, bool isActive, DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken) =>
        UpdateAsync(new Customer { Id = id, IsActive = isActive, UpdatedAtUtc = updatedAtUtc, UpdatedByUserId = updatedByUserId }, CatalogRepoDbWriteFields.CustomerActiveUpdate, cancellationToken);

    private Task<int> InsertAsync<TEntity>(
        TEntity entity,
        IEnumerable<Field> fields,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        var transaction = _context.Transaction
            ?? throw new InvalidOperationException("Catalog writes require an active transaction.");
        return _context.Connection.InsertAsync<TEntity, int>(
            entity,
            fields: fields,
            transaction: transaction,
            cancellationToken: cancellationToken);
    }

    private async Task<int> InsertWithConflictTranslationAsync<TEntity>(
        TEntity entity,
        IEnumerable<Field> fields,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        try
        {
            return await InsertAsync(entity, fields, cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number == 2601 || exception.Number == 2627)
        {
            throw new CatalogWriteConflictException(CatalogWriteConflict.DuplicateCode);
        }
    }

    private async Task<int> InsertEmployeeWithConflictTranslationAsync(Employee employee, CancellationToken cancellationToken)
    {
        try
        {
            return await InsertAsync(employee, CatalogRepoDbWriteFields.EmployeeCreate, cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number == 2601 || exception.Number == 2627)
        {
            var conflict = exception.Message.IndexOf("UX_Employees_UserId", StringComparison.OrdinalIgnoreCase) >= 0
                ? CatalogWriteConflict.EmployeeUserAlreadyLinked
                : CatalogWriteConflict.DuplicateCode;
            throw new CatalogWriteConflictException(conflict);
        }
        catch (SqlException exception) when (exception.Number == 547)
        {
            throw new CatalogWriteConflictException(CatalogWriteConflict.EmployeeUserNotFound);
        }
    }

    private async Task<Employee?> UpdateEmployeeWithConflictTranslationAsync(Employee employee, CancellationToken cancellationToken)
    {
        try
        {
            return await UpdateAndReloadAsync(employee, CatalogRepoDbWriteFields.EmployeeUpdate, employee.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number == 2601 || exception.Number == 2627)
        {
            var conflict = exception.Message.IndexOf("UX_Employees_UserId", StringComparison.OrdinalIgnoreCase) >= 0
                ? CatalogWriteConflict.EmployeeUserAlreadyLinked
                : CatalogWriteConflict.DuplicateCode;
            throw new CatalogWriteConflictException(conflict);
        }
        catch (SqlException exception) when (exception.Number == 547)
        {
            throw new CatalogWriteConflictException(CatalogWriteConflict.EmployeeUserNotFound);
        }
    }

    private async Task<TEntity?> UpdateWithConflictTranslationAsync<TEntity>(
        TEntity entity,
        IEnumerable<Field> fields,
        object primaryKey,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        try
        {
            return await UpdateAndReloadAsync(entity, fields, primaryKey, cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number == 2601 || exception.Number == 2627)
        {
            throw new CatalogWriteConflictException(CatalogWriteConflict.DuplicateCode);
        }
    }

    private async Task<TEntity?> UpdateAndReloadAsync<TEntity>(
        TEntity entity,
        IEnumerable<Field> fields,
        object primaryKey,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var transaction = _context.Transaction
            ?? throw new InvalidOperationException("Catalog writes require an active transaction.");
        var affected = await _context.Connection.UpdateAsync(
            entity,
            fields: fields,
            transaction: transaction,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (affected != 1)
        {
            return null;
        }

        var rows = await _context.Connection.QueryAsync<TEntity>(
            primaryKey,
            transaction: transaction,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return rows.SingleOrDefault();
    }

    private async Task<bool> UpdateAsync<TEntity>(
        TEntity entity,
        IEnumerable<Field> fields,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var transaction = _context.Transaction
            ?? throw new InvalidOperationException("Catalog writes require an active transaction.");
        var affected = await _context.Connection.UpdateAsync(
            entity,
            fields: fields,
            transaction: transaction,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }
}
