using System.ComponentModel.DataAnnotations;

namespace BranchPOS.DTOs;

public class InventoryAdjustmentDto
{
    public string IdempotencyKey { get; set; } = string.Empty;

    public int BranchId { get; set; }

    public int UserSessionId { get; set; }

    public string PerformedByUserId { get; set; } = string.Empty;

    public int TerminalId { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int IngredientId { get; set; }

    [Range(typeof(decimal), "-1000000", "1000000")]
    public decimal QuantityChanged { get; set; }

    public string? Reason { get; set; }
}
