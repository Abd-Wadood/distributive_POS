namespace BranchPOS.DTOs;

public class CreateOrderDto
{
    public int? DraftOrderId { get; set; }

    public string CashierId { get; set; } = string.Empty;

    public int BranchId { get; set; }

    public int UserSessionId { get; set; }

    public string TerminalName { get; set; } = string.Empty;

    public int TerminalId { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    public string OrderType { get; set; } = "Takeaway";

    public decimal DiscountAmount { get; set; }

    public string? TableNumber { get; set; }

    public string? Notes { get; set; }

    public CustomerDto Customer { get; set; } = new();

    public List<OrderItemDto> Items { get; set; } = new();
}

public class CustomerDto
{
    public int BranchId { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Name { get; set; }

    public string? Address { get; set; }
}
