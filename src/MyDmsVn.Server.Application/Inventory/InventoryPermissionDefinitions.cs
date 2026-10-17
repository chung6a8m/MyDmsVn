using System.Collections.Generic;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Server.Application.Inventory
{
    public static class InventoryPermissionKeys
    {
        public const string GoodsReceiptsRead = "Inventory.GoodsReceipts.Read";
        public const string GoodsReceiptsWrite = "Inventory.GoodsReceipts.Write";
        public const string GoodsReceiptsPost = "Inventory.GoodsReceipts.Post";
        public const string BalancesRead = "Inventory.Balances.Read";
        public const string StockCardRead = "Inventory.StockCard.Read";
    }

    public static class InventoryPermissionDefinitions
    {
        public static IReadOnlyList<PermissionDefinition> All { get; } =
            new[]
            {
                Definition(InventoryPermissionKeys.GoodsReceiptsRead, "Read goods receipts."),
                Definition(InventoryPermissionKeys.GoodsReceiptsWrite, "Create and update draft goods receipts."),
                Definition(InventoryPermissionKeys.GoodsReceiptsPost, "Post goods receipts."),
                Definition(InventoryPermissionKeys.BalancesRead, "Read inventory balances."),
                Definition(InventoryPermissionKeys.StockCardRead, "Read stock-card movements."),
            };

        private static PermissionDefinition Definition(string key, string description)
        {
            return new PermissionDefinition(
                key,
                "Inventory",
                description);
        }
    }
}
