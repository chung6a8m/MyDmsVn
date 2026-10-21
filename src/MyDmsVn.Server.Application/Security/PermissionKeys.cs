using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Inventory;

namespace MyDmsVn.Server.Application.Security
{
    public static class PermissionKeys
    {
        public const string SalesReceiptsRead = Sales.SalesPermissionKeys.ReceiptsRead;
        public const string SalesReceiptsWrite = Sales.SalesPermissionKeys.ReceiptsWrite;
        public const string SalesReceiptsPost = Sales.SalesPermissionKeys.ReceiptsPost;
        public const string CatalogProductsRead = CatalogPermissionKeys.ProductsRead;
        public const string CatalogProductsWrite = CatalogPermissionKeys.ProductsWrite;
        public const string CatalogWarehousesRead = CatalogPermissionKeys.WarehousesRead;
        public const string CatalogWarehousesWrite = CatalogPermissionKeys.WarehousesWrite;
        public const string CatalogEmployeesRead = CatalogPermissionKeys.EmployeesRead;
        public const string CatalogEmployeesWrite = CatalogPermissionKeys.EmployeesWrite;
        public const string CatalogCustomersRead = CatalogPermissionKeys.CustomersRead;
        public const string CatalogCustomersWrite = CatalogPermissionKeys.CustomersWrite;
        public const string InventoryGoodsReceiptsRead = InventoryPermissionKeys.GoodsReceiptsRead;
        public const string InventoryGoodsReceiptsWrite = InventoryPermissionKeys.GoodsReceiptsWrite;
        public const string InventoryGoodsReceiptsPost = InventoryPermissionKeys.GoodsReceiptsPost;
        public const string InventoryBalancesRead = InventoryPermissionKeys.BalancesRead;
        public const string InventoryStockCardRead = InventoryPermissionKeys.StockCardRead;
        public const string SecurityUsersManage = SecurityPermissionKeys.UsersManage;
        public const string SecurityRolesManage = SecurityPermissionKeys.RolesManage;
        public const string SecurityPermissionsRead = SecurityPermissionKeys.PermissionsRead;
    }
}
