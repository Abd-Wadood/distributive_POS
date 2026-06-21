using System.ComponentModel.DataAnnotations;
using BranchPOS.Validation;

namespace BranchPOS.DTOs;

public class CreateOrderDto
{
    [Required]
    public string IdempotencyKey { get; set; } = string.Empty;

    public string? ClientRequestId { get; set; }

    public int? DraftOrderId { get; set; }

    public string CashierId { get; set; } = string.Empty;

    public int BranchId { get; set; }

    public int UserSessionId { get; set; }

    public string TerminalName { get; set; } = string.Empty;

    public int TerminalId { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    [Required]
    public string OrderType { get; set; } = "Takeaway";

    [Range(0, 999999)]
    public decimal DiscountAmount { get; set; }

    public string? TableNumber { get; set; }

    public string? Notes { get; set; }

    [Required]
    public CustomerDto Customer { get; set; } = new();

    [Required]
    [MinCollectionCount(1, ErrorMessage = "Order must contain at least one item.")]
    public List<OrderItemDto> Items { get; set; } = new();
}

public class CustomerDto
{
    public int BranchId { get; set; }

    [RegularExpression(@"^\d{11}$", ErrorMessage = "Customer phone number must be exactly 11 digits.")]
    public string? PhoneNumber { get; set; }

    public string? Name { get; set; }

    public string? Address { get; set; }
}
