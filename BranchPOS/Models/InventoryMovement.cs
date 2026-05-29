using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    public decimal QuantityBase { get; set; }

    public decimal? UnitCostBase { get; set; }

    public decimal TotalCost { get; set; }

    [NotMapped]
    public decimal Quantity
    {
        get => QuantityBase;
        set => QuantityBase = value;
    }

    [NotMapped]
    public decimal? UnitCost
    {
        get => UnitCostBase;
        set => UnitCostBase = value;
    }

    public InventoryMovementType MovementType { get; set; }

    [MaxLength(80)]
    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    public int? KitchenRequestDetailId { get; set; }

    public KitchenRequestDetail? KitchenRequestDetail { get; set; }

    public int? UserSessionId { get; set; }

    public UserSession? UserSession { get; set; }

    public int? TerminalId { get; set; }

    public Terminal? Terminal { get; set; }

    [MaxLength(120)]
    public string? IdempotencyKey { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
