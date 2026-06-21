namespace BranchPOS.ViewModels;

public class ReservationAuditViewModel
{
    public List<ReservationAuditRowViewModel> Rows { get; set; } = new();

    public List<OverdueReservedOrderViewModel> OverdueOrders { get; set; } = new();
}

public class ReservationAuditRowViewModel
{
    public string ItemName { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public decimal StockReservedQuantity { get; set; }

    public decimal ActiveReservationQuantity { get; set; }

    public decimal Difference => StockReservedQuantity - ActiveReservationQuantity;
}

public class OverdueReservedOrderViewModel
{
    public int OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public decimal TotalAmount { get; set; }

    public string CashierName { get; set; } = string.Empty;
}
