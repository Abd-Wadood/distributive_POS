using BranchPOS.Models;

namespace BranchPOS.DTOs;

public class InventoryAdjustmentResultDto
{
    public int Id { get; set; }

    public Guid PublicId { get; set; }

    public int InventoryItemId { get; set; }

    public string InventoryItemName { get; set; } = string.Empty;

    public InventoryLocationType LocationType { get; set; }

    public InventoryAdjustmentType AdjustmentType { get; set; }

    public InventoryAdjustmentStatus Status { get; set; }

    public decimal QuantityBaseUnit { get; set; }

    public decimal? DisplayQuantity { get; set; }

    public string? DisplayUnitName { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string CreatedByUserName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string? ApprovedByUserName { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectedByUserName { get; set; }

    public DateTime? RejectedAt { get; set; }

    public string? RejectionReason { get; set; }
}
