using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class InventoryMovement
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public int? FromLocationId { get; set; }

    public InventoryLocation? FromLocation { get; set; }

    public int? ToLocationId { get; set; }

    public InventoryLocation? ToLocation { get; set; }

    public decimal Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    public InventoryMovementType MovementType { get; set; }

    [MaxLength(80)]
    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
