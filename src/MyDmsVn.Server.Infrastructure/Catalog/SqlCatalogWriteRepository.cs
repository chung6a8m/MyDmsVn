using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;
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
        return InsertAsync(product, CatalogRepoDbWriteFields.ProductCreate, cancellationToken);
    }

    public Task<int> InsertWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        return InsertAsync(warehouse, CatalogRepoDbWriteFields.WarehouseCreate, cancellationToken);
    }

    public Task<int> InsertEmployeeAsync(Employee employee, CancellationToken cancellationToken)
    {
        return InsertAsync(employee, CatalogRepoDbWriteFields.EmployeeCreate, cancellationToken);
    }

    public Task<int> InsertCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        return InsertAsync(customer, CatalogRepoDbWriteFields.CustomerCreate, cancellationToken);
    }

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
}
