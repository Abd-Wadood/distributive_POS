namespace BranchPOS.Models;

public enum InventoryMovementType
{
    Purchase,
    Transfer,
    StockRoomToKitchenDispatch,
    Consumption,
    Production,
    Waste,
    Adjustment
}
