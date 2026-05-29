using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class PreparationRecipeIngredient
{
    public int Id { get; set; }

    public int PreparationRecipeId { get; set; }

    public PreparationRecipe? PreparationRecipe { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public decimal QuantityBase { get; set; }

    public decimal? DisplayQuantity { get; set; }

    [MaxLength(40)]
    public string? DisplayUnit { get; set; }
}
