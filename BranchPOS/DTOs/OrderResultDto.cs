namespace BranchPOS.DTOs;

public class OrderResultDto
{
    public int OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public string InventoryState { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = string.Empty;
}
