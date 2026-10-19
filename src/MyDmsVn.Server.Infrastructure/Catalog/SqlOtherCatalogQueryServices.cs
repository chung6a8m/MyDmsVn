using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Catalog;

internal sealed class SqlWarehouseQueryService(ILinqToDbSessionFactory sessions) : IWarehouseQueryService
{
    public Task<PagedResult<WarehouseDto>> ListAsync(CatalogListRequest request, CancellationToken token) =>
        SqlCatalogPageReader.ReadAsync(sessions, "dbo.Warehouses", "WarehouseId", "WarehouseId AS Id, Code, Name, Address, IsActive",
            request, row => new WarehouseDto(row.GetInt32(0), row.GetString(1), row.GetString(2), row.IsDBNull(3) ? null : row.GetString(3), row.GetBoolean(4)), token);

    public async Task<WarehouseDto?> GetByIdAsync(int id, CancellationToken token)
    {
        using var db = await sessions.OpenOwnedAsync(token).ConfigureAwait(false);
        return await db.GetTable<Warehouse>().Where(e => e.Id == id)
            .Select(e => new WarehouseDto(e.Id, e.Code, e.Name, e.Address, e.IsActive))
            .SingleOrDefaultAsync(token).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(CatalogLookupRequest request, CancellationToken token)
    {
        using var db = await sessions.OpenOwnedAsync(token).ConfigureAwait(false);
        var search = SqlCatalogPageReader.NormalizeSearch(request.Search);
        return await db.GetTable<Warehouse>()
            .Where(e => e.IsActive && (search == null || e.Code.Contains(search) || e.Name.Contains(search)))
            .OrderBy(e => e.Code).ThenBy(e => e.Id).Take(request.Limit)
            .Select(e => new CatalogLookupDto(e.Id, e.Code, e.Name, e.IsActive))
            .ToArrayAsync(token).ConfigureAwait(false);
    }
}

internal sealed class SqlEmployeeQueryService(ILinqToDbSessionFactory sessions) : IEmployeeQueryService
{
    public Task<PagedResult<EmployeeDto>> ListAsync(CatalogListRequest request, CancellationToken token) =>
        SqlCatalogPageReader.ReadAsync(sessions, "dbo.Employees", "EmployeeId", "EmployeeId AS Id, Code, Name, Phone, UserId, IsActive",
            request, row => new EmployeeDto(row.GetInt32(0), row.GetString(1), row.GetString(2), row.IsDBNull(3) ? null : row.GetString(3), row.IsDBNull(4) ? (int?)null : row.GetInt32(4), row.GetBoolean(5)), token);

    public async Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken token)
    {
        using var db = await sessions.OpenOwnedAsync(token).ConfigureAwait(false);
        return await db.GetTable<Employee>().Where(e => e.Id == id)
            .Select(e => new EmployeeDto(e.Id, e.Code, e.Name, e.Phone, e.UserId, e.IsActive))
            .SingleOrDefaultAsync(token).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(CatalogLookupRequest request, CancellationToken token)
    {
        using var db = await sessions.OpenOwnedAsync(token).ConfigureAwait(false);
        var search = SqlCatalogPageReader.NormalizeSearch(request.Search);
        return await db.GetTable<Employee>()
            .Where(e => e.IsActive && (search == null || e.Code.Contains(search) || e.Name.Contains(search)))
            .OrderBy(e => e.Code).ThenBy(e => e.Id).Take(request.Limit)
            .Select(e => new CatalogLookupDto(e.Id, e.Code, e.Name, e.IsActive))
            .ToArrayAsync(token).ConfigureAwait(false);
    }
}

internal sealed class SqlCustomerQueryService(ILinqToDbSessionFactory sessions) : ICustomerQueryService
{
    public Task<PagedResult<CustomerDto>> ListAsync(CatalogListRequest request, CancellationToken token) =>
        SqlCatalogPageReader.ReadAsync(sessions, "dbo.Customers", "CustomerId", "CustomerId AS Id, Code, Name, Address, Phone, TaxCode, IsActive",
            request, row => new CustomerDto(row.GetInt32(0), row.GetString(1), row.GetString(2), row.IsDBNull(3) ? null : row.GetString(3), row.IsDBNull(4) ? null : row.GetString(4), row.IsDBNull(5) ? null : row.GetString(5), row.GetBoolean(6)), token);

    public async Task<CustomerDto?> GetByIdAsync(int id, CancellationToken token)
    {
        using var db = await sessions.OpenOwnedAsync(token).ConfigureAwait(false);
        return await db.GetTable<Customer>().Where(e => e.Id == id)
            .Select(e => new CustomerDto(e.Id, e.Code, e.Name, e.Address, e.Phone, e.TaxCode, e.IsActive))
            .SingleOrDefaultAsync(token).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(CatalogLookupRequest request, CancellationToken token)
    {
        using var db = await sessions.OpenOwnedAsync(token).ConfigureAwait(false);
        var search = SqlCatalogPageReader.NormalizeSearch(request.Search);
        return await db.GetTable<Customer>()
            .Where(e => e.IsActive && (search == null || e.Code.Contains(search) || e.Name.Contains(search)))
            .OrderBy(e => e.Code).ThenBy(e => e.Id).Take(request.Limit)
            .Select(e => new CatalogLookupDto(e.Id, e.Code, e.Name, e.IsActive))
            .ToArrayAsync(token).ConfigureAwait(false);
    }
}
