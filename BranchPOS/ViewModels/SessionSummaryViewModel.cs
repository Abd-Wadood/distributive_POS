using BranchPOS.Models;

namespace BranchPOS.ViewModels;

public class SessionSummaryViewModel
{
    public UserSession Session { get; set; } = new();

    public int CompletedOrdersCount { get; set; }

    public decimal TotalSalesAmount { get; set; }

    public int CancelledOrdersCount { get; set; }

    public int ActiveDraftOrdersCount { get; set; }

    public int PurchasesCount { get; set; }

    public decimal TotalPurchaseAmount { get; set; }

    public int InventoryAdjustmentsCount { get; set; }

    public int LowStockWarnings { get; set; }
}
