using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application.Catalog;

public interface IProductQueryService
{
    Task<PagedResult<ProductDto>> ListAsync(
        CatalogListRequest request,
        CancellationToken cancellationToken);

    Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(
        CatalogLookupRequest request,
        CancellationToken cancellationToken);
}

public interface IWarehouseQueryService
{
    Task<PagedResult<WarehouseDto>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken);
    Task<WarehouseDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken);
}

public interface IEmployeeQueryService
{
    Task<PagedResult<EmployeeDto>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken);
    Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken);
}

public interface ICustomerQueryService
{
    Task<PagedResult<CustomerDto>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken);
    Task<CustomerDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken);
}
