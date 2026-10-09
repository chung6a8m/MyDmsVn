using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Domain.Catalog;

namespace MyDmsVn.Server.Application.Catalog;

public enum CatalogWriteConflict
{
    DuplicateCode = 0,
    EmployeeUserAlreadyLinked = 1,
    EmployeeUserNotFound = 2,
}

public sealed class CatalogWriteConflictException : System.Exception
{
    public CatalogWriteConflictException(CatalogWriteConflict conflict)
        : base($"Catalog write conflict: {conflict}.")
    {
        Conflict = conflict;
    }

    public CatalogWriteConflict Conflict { get; }
}

public interface ICatalogWriteRepository
{
    Task<int> InsertProductAsync(Product product, CancellationToken cancellationToken);

    Task<bool> UpdateProductAsync(Product product, CancellationToken cancellationToken);

    Task<bool> SetProductActiveAsync(
        int id,
        bool isActive,
        System.DateTime updatedAtUtc,
        int? updatedByUserId,
        CancellationToken cancellationToken);

    Task<int> InsertWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken);

    Task<bool> UpdateWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken);

    Task<bool> SetWarehouseActiveAsync(int id, bool isActive, System.DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken);

    Task<int> InsertEmployeeAsync(Employee employee, CancellationToken cancellationToken);

    Task<bool> UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken);

    Task<bool> SetEmployeeActiveAsync(int id, bool isActive, System.DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken);

    Task<int> InsertCustomerAsync(Customer customer, CancellationToken cancellationToken);

    Task<bool> UpdateCustomerAsync(Customer customer, CancellationToken cancellationToken);

    Task<bool> SetCustomerActiveAsync(int id, bool isActive, System.DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken);
}
