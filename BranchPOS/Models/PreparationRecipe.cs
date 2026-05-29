using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class PreparationRecipe
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    public int OutputInventoryItemId { get; set; }

    public InventoryItem? OutputInventoryItem { get; set; }

    public decimal OutputQuantityBase { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<PreparationRecipeIngredient> Ingredients { get; set; } = new();
}
