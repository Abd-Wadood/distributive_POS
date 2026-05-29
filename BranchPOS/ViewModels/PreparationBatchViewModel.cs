using BranchPOS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BranchPOS.ViewModels;

public class PreparationBatchViewModel
{
    public string IdempotencyKey { get; set; } = string.Empty;

    public int? PreparationRecipeId { get; set; }

    public decimal? OutputQuantityBase { get; set; }

    public decimal? PreparedItemCount { get; set; }

    public bool UsesRecipeOutputCount { get; set; }

    public string OutputUnit { get; set; } = string.Empty;

    public string OutputItemName { get; set; } = string.Empty;

    public decimal? StandardRecipeOutputQuantity { get; set; }

    public string StandardRecipeOutputUnit { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public List<SelectListItem> Recipes { get; set; } = [];

    public PreparationRecipe? SelectedRecipe { get; set; }

    public List<PreparationIngredientAvailabilityViewModel> Ingredients { get; set; } = [];
}

public class PreparationIngredientAvailabilityViewModel
{
    public string InventoryItemName { get; set; } = string.Empty;

    public string BaseUnit { get; set; } = string.Empty;

    public decimal RequiredQuantity { get; set; }

    public decimal RecipeQuantity { get; set; }

    public decimal AvailableQuantity { get; set; }

    public bool HasEnoughStock => AvailableQuantity >= RequiredQuantity;
}
