namespace BranchPOS.DTOs;

public class InventoryAdjustmentDto
{
    public int BranchId { get; set; }

    public int UserSessionId { get; set; }

    public string PerformedByUserId { get; set; } = string.Empty;

    public int TerminalId { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    public int IngredientId { get; set; }

    public decimal QuantityChanged { get; set; }

    public string? Reason { get; set; }
}
