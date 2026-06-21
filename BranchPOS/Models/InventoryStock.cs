using System.ComponentModel.DataAnnotations.Schema;

namespace BranchPOS.Models;

public class InventoryStock
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public int InventoryLocationId { get; set; }

    public InventoryLocation? InventoryLocation { get; set; }

    public decimal QuantityBase { get; set; }

    public decimal ReservedQuantityBase { get; set; }

    public decimal AverageUnitCostBase { get; set; }

    [NotMapped]
    public decimal AvailableQuantityBase => QuantityBase - ReservedQuantityBase;

    [NotMapped]
    public decimal Quantity
    {
        get => QuantityBase;
        set => QuantityBase = value;
    }

    [NotMapped]
    public decimal AverageUnitCost
    {
        get => AverageUnitCostBase;
        set => AverageUnitCostBase = value;
    }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
