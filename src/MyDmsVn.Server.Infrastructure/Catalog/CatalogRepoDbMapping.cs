using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;
using RepoDb;

namespace MyDmsVn.Server.Infrastructure.Catalog;

internal sealed class CatalogRepoDbMapping : IRepoDbMapping
{
    public void Configure()
    {
        ConfigureProduct();
        ConfigureWarehouse();
        ConfigureEmployee();
        ConfigureCustomer();
    }

    private static void ConfigureProduct()
    {
        ClassMapper.Add<Product>("dbo.Products");
        PropertyMapper.Add<Product>(entity => entity.Id, "ProductId");
        PropertyMapper.Add<Product>(entity => entity.Code, "Code");
        PropertyMapper.Add<Product>(entity => entity.Name, "Name");
        PropertyMapper.Add<Product>(entity => entity.Unit, "Unit");
        PropertyMapper.Add<Product>(entity => entity.IsActive, "IsActive");
        PropertyMapper.Add<Product>(entity => entity.CreatedAtUtc, "CreatedAtUtc");
        PropertyMapper.Add<Product>(entity => entity.CreatedByUserId!, "CreatedByUserId");
        PropertyMapper.Add<Product>(entity => entity.UpdatedAtUtc!, "UpdatedAtUtc");
        PropertyMapper.Add<Product>(entity => entity.UpdatedByUserId!, "UpdatedByUserId");
        PrimaryMapper.Add<Product>(entity => entity.Id);
        IdentityMapper.Add<Product>(entity => entity.Id);
    }

    private static void ConfigureWarehouse()
    {
        ClassMapper.Add<Warehouse>("dbo.Warehouses");
        PropertyMapper.Add<Warehouse>(entity => entity.Id, "WarehouseId");
        PropertyMapper.Add<Warehouse>(entity => entity.Code, "Code");
        PropertyMapper.Add<Warehouse>(entity => entity.Name, "Name");
        PropertyMapper.Add<Warehouse>(entity => entity.Address!, "Address");
        PropertyMapper.Add<Warehouse>(entity => entity.IsActive, "IsActive");
        PropertyMapper.Add<Warehouse>(entity => entity.CreatedAtUtc, "CreatedAtUtc");
        PropertyMapper.Add<Warehouse>(entity => entity.CreatedByUserId!, "CreatedByUserId");
        PropertyMapper.Add<Warehouse>(entity => entity.UpdatedAtUtc!, "UpdatedAtUtc");
        PropertyMapper.Add<Warehouse>(entity => entity.UpdatedByUserId!, "UpdatedByUserId");
        PrimaryMapper.Add<Warehouse>(entity => entity.Id);
        IdentityMapper.Add<Warehouse>(entity => entity.Id);
    }

    private static void ConfigureEmployee()
    {
        ClassMapper.Add<Employee>("dbo.Employees");
        PropertyMapper.Add<Employee>(entity => entity.Id, "EmployeeId");
        PropertyMapper.Add<Employee>(entity => entity.Code, "Code");
        PropertyMapper.Add<Employee>(entity => entity.Name, "Name");
        PropertyMapper.Add<Employee>(entity => entity.Phone!, "Phone");
        PropertyMapper.Add<Employee>(entity => entity.UserId!, "UserId");
        PropertyMapper.Add<Employee>(entity => entity.IsActive, "IsActive");
        PropertyMapper.Add<Employee>(entity => entity.CreatedAtUtc, "CreatedAtUtc");
        PropertyMapper.Add<Employee>(entity => entity.CreatedByUserId!, "CreatedByUserId");
        PropertyMapper.Add<Employee>(entity => entity.UpdatedAtUtc!, "UpdatedAtUtc");
        PropertyMapper.Add<Employee>(entity => entity.UpdatedByUserId!, "UpdatedByUserId");
        PrimaryMapper.Add<Employee>(entity => entity.Id);
        IdentityMapper.Add<Employee>(entity => entity.Id);
    }

    private static void ConfigureCustomer()
    {
        ClassMapper.Add<Customer>("dbo.Customers");
        PropertyMapper.Add<Customer>(entity => entity.Id, "CustomerId");
        PropertyMapper.Add<Customer>(entity => entity.Code, "Code");
        PropertyMapper.Add<Customer>(entity => entity.Name, "Name");
        PropertyMapper.Add<Customer>(entity => entity.Address!, "Address");
        PropertyMapper.Add<Customer>(entity => entity.Phone!, "Phone");
        PropertyMapper.Add<Customer>(entity => entity.TaxCode!, "TaxCode");
        PropertyMapper.Add<Customer>(entity => entity.IsActive, "IsActive");
        PropertyMapper.Add<Customer>(entity => entity.CreatedAtUtc, "CreatedAtUtc");
        PropertyMapper.Add<Customer>(entity => entity.CreatedByUserId!, "CreatedByUserId");
        PropertyMapper.Add<Customer>(entity => entity.UpdatedAtUtc!, "UpdatedAtUtc");
        PropertyMapper.Add<Customer>(entity => entity.UpdatedByUserId!, "UpdatedByUserId");
        PrimaryMapper.Add<Customer>(entity => entity.Id);
        IdentityMapper.Add<Customer>(entity => entity.Id);
    }
}
