using System.Collections.Generic;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Server.Application.Catalog
{
    public static class CatalogPermissionKeys
    {
        public const string ProductsRead = "Catalog.Products.Read";
        public const string ProductsWrite = "Catalog.Products.Write";
        public const string WarehousesRead = "Catalog.Warehouses.Read";
        public const string WarehousesWrite = "Catalog.Warehouses.Write";
        public const string EmployeesRead = "Catalog.Employees.Read";
        public const string EmployeesWrite = "Catalog.Employees.Write";
        public const string CustomersRead = "Catalog.Customers.Read";
        public const string CustomersWrite = "Catalog.Customers.Write";
    }

    public static class CatalogPermissionDefinitions
    {
        public static IReadOnlyList<PermissionDefinition> All { get; } =
            new[]
            {
                Definition(CatalogPermissionKeys.ProductsRead, "Read products."),
                Definition(CatalogPermissionKeys.ProductsWrite, "Create and update products."),
                Definition(CatalogPermissionKeys.WarehousesRead, "Read warehouses."),
                Definition(CatalogPermissionKeys.WarehousesWrite, "Create and update warehouses."),
                Definition(CatalogPermissionKeys.EmployeesRead, "Read employees."),
                Definition(CatalogPermissionKeys.EmployeesWrite, "Create and update employees."),
                Definition(CatalogPermissionKeys.CustomersRead, "Read customers."),
                Definition(CatalogPermissionKeys.CustomersWrite, "Create and update customers."),
            };

        private static PermissionDefinition Definition(string key, string description)
        {
            return new PermissionDefinition(
                key,
                "Catalog",
                description,
                PermissionLifecycleStatus.Active,
                AdministratorGrantPolicy.Eligible);
        }
    }
}
