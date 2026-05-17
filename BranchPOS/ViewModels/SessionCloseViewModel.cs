using BranchPOS.Models;

namespace BranchPOS.ViewModels;

public class SessionCloseViewModel
{
    public UserSession Session { get; set; } = new();

    public string IdempotencyKey { get; set; } = string.Empty;

    public int TotalOrders { get; set; }

    public int CompletedOrdersCount { get; set; }

    public int DraftOrdersCount { get; set; }

    public int PendingOrdersCount { get; set; }

    public int UnknownFinalizeOrdersCount { get; set; }

    public decimal TotalSalesAmount { get; set; }

    public decimal ExpectedClosingCash { get; set; }

    public decimal CountedClosingCash { get; set; }

    public decimal Difference => CountedClosingCash - ExpectedClosingCash;

    public string ConfirmationText { get; set; } = string.Empty;
}
