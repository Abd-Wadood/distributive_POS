namespace BranchPOS.Models;

public class RecipeIngredient
{
    public int Id { get; set; }

    public int RecipeId { get; set; }

    public Recipe? Recipe { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public decimal QuantityRequired { get; set; }
}
