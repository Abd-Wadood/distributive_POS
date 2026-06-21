namespace BranchPOS.Models;

public enum InventoryMovementType
{
    Purchase,
    Transfer,
    StockRoomToKitchenDispatch,
    Consumption,
    ManualConsumption,
    Production,
    Waste,
    Wastage,
    Adjustment,
    ReserveForOrder,
    ReleaseReservation,
    ConsumeReservation,
    WasteReservation,
    SaleConsumption,
    CancelReturn,
    WasteFromConsumedOrder
}
