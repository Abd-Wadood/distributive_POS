using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class PreparationBatch
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int PreparationRecipeId { get; set; }

    public PreparationRecipe? PreparationRecipe { get; set; }

    public int OutputInventoryItemId { get; set; }

    public InventoryItem? OutputInventoryItem { get; set; }

    public int LocationId { get; set; }

    public InventoryLocation? Location { get; set; }

    public decimal OutputQuantityBase { get; set; }

    public PreparationBatchStatus Status { get; set; } = PreparationBatchStatus.Completed;

    public int? UserSessionId { get; set; }

    public UserSession? UserSession { get; set; }

    public int? TerminalId { get; set; }

    public Terminal? Terminal { get; set; }

    [MaxLength(40)]
    public string? TerminalCode { get; set; }

    [MaxLength(120)]
    public string? IdempotencyKey { get; set; }

    public string? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
