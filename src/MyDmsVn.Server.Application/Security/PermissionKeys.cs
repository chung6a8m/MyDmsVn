using System;
using System.Collections.Generic;

namespace MyDmsVn.Server.Application.Security
{
    public static class PermissionKeys
    {
        public const string CatalogProductsRead = "Catalog.Products.Read";
        public const string CatalogProductsWrite = "Catalog.Products.Write";
        public const string CatalogWarehousesRead = "Catalog.Warehouses.Read";
        public const string CatalogWarehousesWrite = "Catalog.Warehouses.Write";
        public const string CatalogEmployeesRead = "Catalog.Employees.Read";
        public const string CatalogEmployeesWrite = "Catalog.Employees.Write";
        public const string CatalogCustomersRead = "Catalog.Customers.Read";
        public const string CatalogCustomersWrite = "Catalog.Customers.Write";
        public const string InventoryGoodsReceiptsRead = "Inventory.GoodsReceipts.Read";
        public const string InventoryGoodsReceiptsWrite = "Inventory.GoodsReceipts.Write";
        public const string InventoryGoodsReceiptsPost = "Inventory.GoodsReceipts.Post";
        public const string InventoryBalancesRead = "Inventory.Balances.Read";
        public const string InventoryStockCardRead = "Inventory.StockCard.Read";
        public const string SecurityUsersManage = "Security.Users.Manage";
        public const string SecurityRolesManage = "Security.Roles.Manage";

        private static readonly HashSet<string> Declared = new HashSet<string>(
            new[]
            {
                CatalogProductsRead,
                CatalogProductsWrite,
                CatalogWarehousesRead,
                CatalogWarehousesWrite,
                CatalogEmployeesRead,
                CatalogEmployeesWrite,
                CatalogCustomersRead,
                CatalogCustomersWrite,
                InventoryGoodsReceiptsRead,
                InventoryGoodsReceiptsWrite,
                InventoryGoodsReceiptsPost,
                InventoryBalancesRead,
                InventoryStockCardRead,
                SecurityUsersManage,
                SecurityRolesManage,
            },
            StringComparer.Ordinal);

        public static bool IsDeclared(string permissionKey)
        {
            return permissionKey != null && Declared.Contains(permissionKey);
        }
    }
}
