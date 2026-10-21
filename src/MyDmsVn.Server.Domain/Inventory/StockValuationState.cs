namespace MyDmsVn.Server.Domain.Inventory;

public sealed class StockValuationState
{
    public StockValuationState(decimal quantity, decimal inventoryValue)
    {
        InventoryValuationMath.SumQuantities(new[] { quantity });
        InventoryValuationMath.SumAmounts(new[] { inventoryValue });
        if (quantity == 0 && inventoryValue != 0)
            throw new System.ArgumentException("Zero quantity requires zero inventory value.");
        Quantity = quantity;
        InventoryValue = inventoryValue;
    }

    public decimal Quantity { get; }
    public decimal InventoryValue { get; }
}
