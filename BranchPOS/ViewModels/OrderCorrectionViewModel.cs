using BranchPOS.Models;

namespace BranchPOS.ViewModels;

public class OrderCorrectionViewModel
{
    public string? SearchOrderNumber { get; set; }

    public string? Message { get; set; }

    public OrderCorrectionOrderViewModel? Order { get; set; }
}

public class OrderCorrectionOrderViewModel
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public OrderType OrderType { get; set; }

    public OrderStatus OrderStatus { get; set; }

    public OrderInventoryState InventoryState { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string CashierName { get; set; } = string.Empty;

    public string TerminalName { get; set; } = string.Empty;

    public string? SessionCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SentToKitchenAt { get; set; }

    public decimal TotalAmount { get; set; }

    public List<OrderCorrectionLineViewModel> Items { get; set; } = new();

    public List<OrderCorrectionIngredientViewModel> ConsumedInventory { get; set; } = new();

    public List<OrderCorrectionPrintJobViewModel> PrintJobs { get; set; } = new();
}

public class OrderCorrectionLineViewModel
{
    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }
}

public class OrderCorrectionIngredientViewModel
{
    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
}

public class OrderCorrectionPrintJobViewModel
{
    public string PrintType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
