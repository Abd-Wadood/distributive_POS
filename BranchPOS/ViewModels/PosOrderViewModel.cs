using BranchPOS.Models;

namespace BranchPOS.ViewModels;

public class PosOrderViewModel
{
    public List<PosProductViewModel> Products { get; set; } = new();

    public List<Category> Categories { get; set; } = new();

    public List<PosDraftOrderViewModel> DraftOrders { get; set; } = new();
}

public class PosProductViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public bool IsAvailable { get; set; }

    public bool IsActive { get; set; }

    public string? ImagePath { get; set; }
}

public class PosDraftOrderViewModel
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string OrderType { get; set; } = "Takeaway";

    public decimal DiscountAmount { get; set; }

    public string? TableNumber { get; set; }

    public string? Notes { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? CustomerAddress { get; set; }

    public List<PosDraftItemViewModel> Items { get; set; } = new();
}

public class PosDraftItemViewModel
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }
}
