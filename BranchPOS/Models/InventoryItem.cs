using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class InventoryItem
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Unit { get; set; } = string.Empty;

    public decimal ReorderLevel { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InventoryStock> Stocks { get; set; } = new List<InventoryStock>();

    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
