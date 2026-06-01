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

    [Display(Name = "Direct sale inventory item")]
    public int? DirectInventoryItemId { get; set; }

    [Display(Name = "Direct quantity per sale")]
    [Range(typeof(decimal), "0.001", "1000000000", ErrorMessage = "Direct sale quantity must be greater than zero.")]
    public decimal? DirectQuantityBase { get; set; }

    public List<SelectListItem> Categories { get; set; } = new();

    public List<RecipeItemQuantityViewModel> RecipeItems { get; set; } = new();

    public List<InventoryItem> InventoryItems { get; set; } = new();
}

public class RecipeItemQuantityViewModel
{
    public int InventoryItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal? QuantityRequired { get; set; }

    public static RecipeItemQuantityViewModel FromInventoryItem(InventoryItem item, decimal quantity = 0) =>
        new()
        {
            InventoryItemId = item.Id,
            Name = item.Name,
            Unit = item.BaseUnit,
            QuantityRequired = quantity
        };
}
