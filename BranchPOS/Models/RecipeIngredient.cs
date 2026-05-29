using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BranchPOS.Models;

public class RecipeIngredient
{
    public int Id { get; set; }

    public int RecipeId { get; set; }

    public Recipe? Recipe { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public decimal QuantityRequiredBase { get; set; }

    public decimal? DisplayQuantity { get; set; }

    [MaxLength(40)]
    public string? DisplayUnit { get; set; }

    [NotMapped]
    public decimal QuantityRequired
    {
        get => QuantityRequiredBase;
        set => QuantityRequiredBase = value;
    }
}
