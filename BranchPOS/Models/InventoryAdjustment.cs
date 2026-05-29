using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class InventoryAdjustment
{
    public int Id { get; set; }

    public Guid PublicId { get; set; } = Guid.NewGuid();

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public InventoryLocationType LocationType { get; set; }

    public InventoryAdjustmentType AdjustmentType { get; set; }

    public InventoryAdjustmentStatus Status { get; set; } = InventoryAdjustmentStatus.Pending;

    public decimal QuantityBaseUnit { get; set; }

    [MaxLength(80)]
    public string? DisplayUnitName { get; set; }

    public decimal? DisplayQuantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? ApprovedByUserId { get; set; }

    public ApplicationUser? ApprovedByUser { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public DateTime? RejectedAt { get; set; }

    public string? RejectedByUserId { get; set; }

    public ApplicationUser? RejectedByUser { get; set; }

    public bool IsSynced { get; set; }

    public DateTime? SyncedAt { get; set; }
}
