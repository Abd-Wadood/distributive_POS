using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager,Admin")]
public class PreparationRecipesController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;

    public PreparationRecipesController(AppDbContext context, IBranchContextService branchContextService)
    {
        _context = context;
        _branchContextService = branchContextService;
    }

    public async Task<IActionResult> Index()
    {
        await Task.CompletedTask;
        return RedirectToAction("Index", "InventoryItems");
    }

    public async Task<IActionResult> Edit(int? id)
    {
        await Task.CompletedTask;
        return RedirectToAction("Index", "InventoryItems");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PreparationRecipe model)
    {
        await Task.CompletedTask;
        return RedirectToAction("Index", "InventoryItems");
    }

    private async Task<Dictionary<int, InventoryItem>> ValidateRecipeModelAsync(PreparationRecipe model, int branchId)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Recipe name is required.");
        }

        if (model.OutputInventoryItemId <= 0)
        {
            ModelState.AddModelError(nameof(model.OutputInventoryItemId), "Output item is required.");
        }

        if (model.OutputQuantityBase <= 0)
        {
            ModelState.AddModelError(nameof(model.OutputQuantityBase), "Output quantity must be greater than zero.");
        }

        if (model.Ingredients.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one input ingredient.");
        }

        if (model.Ingredients.GroupBy(x => x.InventoryItemId).Any(x => x.Count() > 1))
        {
            ModelState.AddModelError(string.Empty, "Duplicate ingredient rows are not allowed.");
        }

        if (model.Ingredients.Any(x => x.InventoryItemId == model.OutputInventoryItemId))
        {
            ModelState.AddModelError(string.Empty, "Input ingredient cannot be the same as the output item.");
        }

        var itemIds = model.Ingredients.Select(x => x.InventoryItemId).Append(model.OutputInventoryItemId).Where(x => x > 0).Distinct().ToList();
        var items = new Dictionary<int, InventoryItem>();
        if (itemIds.Count > 0)
        {
            items = await _context.InventoryItems
                .Where(x => x.BranchId == branchId && x.IsActive && itemIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);
            var validCount = items.Count;
            if (validCount != itemIds.Count)
            {
                ModelState.AddModelError(string.Empty, "One or more inventory items do not belong to the active branch.");
            }

            foreach (var ingredient in model.Ingredients)
            {
                if (items.TryGetValue(ingredient.InventoryItemId, out var item) && (item.IsExpenseOnly || !item.IsStockTracked))
                {
                    ModelState.AddModelError(string.Empty, $"{item.Name} cannot be used in prepared inventory because it is not stock tracked.");
                }
            }

            if (items.TryGetValue(model.OutputInventoryItemId, out var outputItem) && !outputItem.IsPreparedItem)
            {
                ModelState.AddModelError(nameof(model.OutputInventoryItemId), "Output item must be marked as a prepared item.");
            }
        }

        return items;
    }

    private async Task PopulateInventoryItemsAsync(int branchId)
    {
        ViewBag.InventoryItems = await _context.InventoryItems
            .Where(x => x.BranchId == branchId && x.IsActive && x.IsStockTracked && !x.IsExpenseOnly)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.BaseUnit})", x.Id.ToString()))
            .ToListAsync();
        ViewBag.InventoryItemModels = await _context.InventoryItems
            .Where(x => x.BranchId == branchId && x.IsActive && x.IsStockTracked && !x.IsExpenseOnly)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    private List<PreparationRecipeIngredient> NormalizeIngredients(List<PreparationRecipeIngredient> ingredients)
    {
        var normalized = new List<PreparationRecipeIngredient>();
        for (var i = 0; i < ingredients.Count; i++)
        {
            var ingredient = ingredients[i];
            var isEmpty = ingredient.InventoryItemId <= 0 && ingredient.QuantityBase <= 0;
            if (isEmpty)
            {
                continue;
            }

            if (ingredient.InventoryItemId <= 0)
            {
                ModelState.AddModelError($"Ingredients[{i}].InventoryItemId", "Input item is required.");
            }

            if (ingredient.QuantityBase <= 0)
            {
                ModelState.AddModelError($"Ingredients[{i}].QuantityBase", "Required quantity must be greater than zero.");
            }

            normalized.Add(ingredient);
        }

        return normalized;
    }

    private static void EnsureOneIngredientRow(PreparationRecipe recipe)
    {
        if (recipe.Ingredients.Count == 0)
        {
            recipe.Ingredients.Add(new PreparationRecipeIngredient());
        }
    }

}
