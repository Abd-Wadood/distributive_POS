namespace BranchPOS.ViewModels;

public class ProfitReportViewModel
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public decimal SalesRevenue { get; set; }

    public decimal IngredientCost { get; set; }

    public decimal InventoryLoss { get; set; }

    public decimal OperationalExpenses { get; set; }

    public decimal NetProfit { get; set; }

    public decimal StockRoomWasteCost { get; set; }

    public decimal KitchenWasteCost { get; set; }

    public decimal MissingStockCost { get; set; }

    public decimal ExpiredStockCost { get; set; }

    public decimal DamagedStockCost { get; set; }

    public decimal SpillageCost { get; set; }

    public decimal CorrectionIncreaseTotal { get; set; }

    public decimal CorrectionDecreaseTotal { get; set; }
}
