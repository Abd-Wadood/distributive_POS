using BranchPOS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace BranchPOS.ViewModels;

public class ProductEditViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 999999)]
    public decimal Price { get; set; }

    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    public List<SelectListItem> Categories { get; set; } = new();

    public List<IngredientQuantityViewModel> Ingredients { get; set; } = new();
}

public class IngredientQuantityViewModel
{
    public int IngredientId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string UnitType { get; set; } = string.Empty;

    public decimal QuantityRequired { get; set; }

    public static IngredientQuantityViewModel FromIngredient(Ingredient ingredient, decimal quantity = 0) =>
        new()
        {
            IngredientId = ingredient.Id,
            Name = ingredient.Name,
            UnitType = ingredient.UnitType,
            QuantityRequired = quantity
        };
}
