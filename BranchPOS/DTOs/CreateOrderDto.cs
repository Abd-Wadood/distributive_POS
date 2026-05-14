using System.ComponentModel.DataAnnotations;

namespace BranchPOS.DTOs;

public class CreateOrderDto
{
    public int? DraftOrderId { get; set; }

    [Required]
    public string CashierId { get; set; } = string.Empty;

    public int BranchId { get; set; }

    public int UserSessionId { get; set; }

    public string TerminalName { get; set; } = string.Empty;

    public int TerminalId { get; set; }

    [Required]
    public string TerminalCode { get; set; } = string.Empty;

    [Required]
    public string OrderType { get; set; } = "Takeaway";

    [Range(0, 999999)]
    public decimal DiscountAmount { get; set; }

    public string? TableNumber { get; set; }

    public string? Notes { get; set; }

    [Required]
    public CustomerDto Customer { get; set; } = new();

    [MinLength(1)]
    public List<OrderItemDto> Items { get; set; } = new();
}

public class CustomerDto
{
    public int BranchId { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    public string? Name { get; set; }

    public string? Address { get; set; }
}
