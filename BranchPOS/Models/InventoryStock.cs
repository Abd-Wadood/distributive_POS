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

    public decimal Quantity { get; set; }

    public decimal AverageUnitCost { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
