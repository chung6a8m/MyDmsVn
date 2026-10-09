using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Domain.Catalog;

namespace MyDmsVn.Server.Application.Catalog;

public interface ICatalogWriteRepository
{
    Task<int> InsertProductAsync(Product product, CancellationToken cancellationToken);

    Task<int> InsertWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken);

    Task<int> InsertEmployeeAsync(Employee employee, CancellationToken cancellationToken);

    Task<int> InsertCustomerAsync(Customer customer, CancellationToken cancellationToken);
}
