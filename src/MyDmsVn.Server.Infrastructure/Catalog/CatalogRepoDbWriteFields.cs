using System.Collections.Generic;
using MyDmsVn.Server.Domain.Catalog;
using RepoDb;

namespace MyDmsVn.Server.Infrastructure.Catalog;

internal static class CatalogRepoDbWriteFields
{
    internal static readonly IEnumerable<Field> ProductCreate = Field.Parse<Product>(entity => new
    {
        entity.Code,
        entity.Name,
        entity.Unit,
        entity.IsActive,
        entity.CreatedByUserId,
    });

    internal static readonly IEnumerable<Field> WarehouseCreate = Field.Parse<Warehouse>(entity => new
    {
        entity.Code,
        entity.Name,
        entity.Address,
        entity.IsActive,
        entity.CreatedByUserId,
    });

    internal static readonly IEnumerable<Field> EmployeeCreate = Field.Parse<Employee>(entity => new
    {
        entity.Code,
        entity.Name,
        entity.Phone,
        entity.UserId,
        entity.IsActive,
        entity.CreatedByUserId,
    });

    internal static readonly IEnumerable<Field> CustomerCreate = Field.Parse<Customer>(entity => new
    {
        entity.Code,
        entity.Name,
        entity.Address,
        entity.Phone,
        entity.TaxCode,
        entity.IsActive,
        entity.CreatedByUserId,
    });
}
