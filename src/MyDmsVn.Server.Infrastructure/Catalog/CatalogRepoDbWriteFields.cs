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

    internal static readonly IEnumerable<Field> ProductUpdate = Field.Parse<Product>(entity => new
    {
        entity.Code,
        entity.Name,
        entity.Unit,
        entity.UpdatedAtUtc,
        entity.UpdatedByUserId,
    });

    internal static readonly IEnumerable<Field> ProductActiveUpdate = Field.Parse<Product>(entity => new
    {
        entity.IsActive,
        entity.UpdatedAtUtc,
        entity.UpdatedByUserId,
    });

    internal static readonly IEnumerable<Field> WarehouseCreate = Field.Parse<Warehouse>(entity => new
    {
        entity.Code,
        entity.Name,
        entity.Address,
        entity.IsActive,
        entity.CreatedByUserId,
    });

    internal static readonly IEnumerable<Field> WarehouseUpdate = Field.Parse<Warehouse>(entity => new
    { entity.Code, entity.Name, entity.Address, entity.UpdatedAtUtc, entity.UpdatedByUserId });
    internal static readonly IEnumerable<Field> WarehouseActiveUpdate = Field.Parse<Warehouse>(entity => new
    { entity.IsActive, entity.UpdatedAtUtc, entity.UpdatedByUserId });

    internal static readonly IEnumerable<Field> EmployeeCreate = Field.Parse<Employee>(entity => new
    {
        entity.Code,
        entity.Name,
        entity.Phone,
        entity.UserId,
        entity.IsActive,
        entity.CreatedByUserId,
    });

    internal static readonly IEnumerable<Field> EmployeeUpdate = Field.Parse<Employee>(entity => new
    { entity.Code, entity.Name, entity.Phone, entity.UserId, entity.UpdatedAtUtc, entity.UpdatedByUserId });
    internal static readonly IEnumerable<Field> EmployeeActiveUpdate = Field.Parse<Employee>(entity => new
    { entity.IsActive, entity.UpdatedAtUtc, entity.UpdatedByUserId });

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

    internal static readonly IEnumerable<Field> CustomerUpdate = Field.Parse<Customer>(entity => new
    { entity.Code, entity.Name, entity.Address, entity.Phone, entity.TaxCode, entity.UpdatedAtUtc, entity.UpdatedByUserId });
    internal static readonly IEnumerable<Field> CustomerActiveUpdate = Field.Parse<Customer>(entity => new
    { entity.IsActive, entity.UpdatedAtUtc, entity.UpdatedByUserId });
}
