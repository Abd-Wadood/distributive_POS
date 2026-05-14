using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class Ingredient
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string UnitType { get; set; } = string.Empty;

    public decimal MinimumStockLevel { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Inventory? Inventory { get; set; }

    public ICollection<ProductIngredient> ProductIngredients { get; set; } = new List<ProductIngredient>();

    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
}
