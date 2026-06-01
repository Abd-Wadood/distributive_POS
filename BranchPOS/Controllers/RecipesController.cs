using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager,Admin")]
public class RecipesController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IPosMenuCacheInvalidator _posMenuCacheInvalidator;

    public RecipesController(AppDbContext context, IBranchContextService branchContextService, IPosMenuCacheInvalidator posMenuCacheInvalidator)
    {
        _context = context;
        _branchContextService = branchContextService;
        _posMenuCacheInvalidator = posMenuCacheInvalidator;
    }

    public async Task<IActionResult> Index()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var recipes = await _context.Recipes
            .Include(x => x.Product)
            .Include(x => x.Ingredients)
            .ThenInclude(x => x.InventoryItem)
            .Where(x => x.BranchId == branchId)
            .OrderBy(x => x.Product!.Name)
            .ToListAsync();
        return View(recipes);
    }

    public async Task<IActionResult> Edit(int? productId)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        Recipe? recipe = null;
        if (productId.HasValue)
        {
            recipe = await _context.Recipes
                .Include(x => x.Ingredients)
                .ThenInclude(x => x.InventoryItem)
                .FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId.Value && x.IsActive);
        }

        recipe ??= new Recipe { BranchId = branchId, ProductId = productId ?? 0 };
        if (recipe.Ingredients.Count == 0)
        {
            recipe.Ingredients.Add(new RecipeIngredient());
        }

        await PopulateListsAsync(branchId);
        return View(recipe);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Recipe model)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        model.Ingredients = NormalizeIngredients(model.Ingredients);
        if (model.ProductId <= 0)
        {
            ModelState.AddModelError(nameof(model.ProductId), "Product is required.");
        }
        if (model.Ingredients.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one recipe ingredient.");
        }
        if (!ModelState.IsValid)
        {
            await PopulateListsAsync(branchId);
            EnsureOneIngredientRow(model);
            return View(model);
        }

        var productExists = await _context.Products.AnyAsync(x => x.Id == model.ProductId && x.BranchId == branchId && x.IsActive && x.DirectInventoryItemId == null);
        if (!productExists)
        {
            return NotFound();
        }

        var inventoryItems = await LoadValidInventoryItemsAsync(branchId, model.Ingredients.Select(x => x.InventoryItemId));
        if (inventoryItems.Count != model.Ingredients.Select(x => x.InventoryItemId).Distinct().Count())
        {
            ModelState.AddModelError(string.Empty, "One or more recipe inventory items do not belong to the active branch.");
            await PopulateListsAsync(branchId);
            EnsureOneIngredientRow(model);
            return View(model);
        }

        var recipe = await _context.Recipes.Include(x => x.Ingredients).FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == model.ProductId && x.IsActive);
        if (recipe is null)
        {
            recipe = new Recipe { BranchId = branchId, ProductId = model.ProductId, IsActive = true };
            _context.Recipes.Add(recipe);
        }
        else
        {
            recipe.Ingredients.Clear();
        }

        foreach (var ingredient in model.Ingredients.GroupBy(x => x.InventoryItemId))
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                InventoryItemId = ingredient.Key,
                QuantityRequiredBase = ingredient.Sum(x => x.QuantityRequiredBase),
                DisplayQuantity = ingredient.Sum(x => x.QuantityRequiredBase),
                DisplayUnit = inventoryItems[ingredient.Key].BaseUnit
            });
        }

        await _context.SaveChangesAsync();
        _posMenuCacheInvalidator.Invalidate();
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateListsAsync(int branchId)
    {
        ViewBag.Products = await _context.Products
            .Where(x => x.BranchId == branchId && x.IsActive && x.DirectInventoryItemId == null)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
        ViewBag.InventoryItems = await _context.InventoryItems
            .Where(x => x.BranchId == branchId && x.IsActive && x.IsStockTracked && !x.IsExpenseOnly && x.AllowRecipeConsumption && x.ConsumptionMode == ConsumptionMode.RecipeConsumption)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.BaseUnit})", x.Id.ToString()))
            .ToListAsync();
        ViewBag.InventoryItemModels = await _context.InventoryItems
            .Where(x => x.BranchId == branchId && x.IsActive && x.IsStockTracked && !x.IsExpenseOnly && x.AllowRecipeConsumption && x.ConsumptionMode == ConsumptionMode.RecipeConsumption)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    private List<RecipeIngredient> NormalizeIngredients(List<RecipeIngredient> ingredients)
    {
        var normalized = new List<RecipeIngredient>();
        for (var i = 0; i < ingredients.Count; i++)
        {
            var ingredient = ingredients[i];
            var isEmpty = ingredient.InventoryItemId <= 0 && ingredient.QuantityRequiredBase <= 0;
            if (isEmpty)
            {
                continue;
            }

            if (ingredient.InventoryItemId <= 0)
            {
                ModelState.AddModelError($"Ingredients[{i}].InventoryItemId", "Inventory item is required.");
            }

            if (ingredient.QuantityRequiredBase <= 0)
            {
                ModelState.AddModelError($"Ingredients[{i}].QuantityRequiredBase", "Required quantity must be greater than zero.");
            }

            normalized.Add(ingredient);
        }

        if (normalized.GroupBy(x => x.InventoryItemId).Any(x => x.Key > 0 && x.Count() > 1))
        {
            ModelState.AddModelError(string.Empty, "Duplicate inventory item rows are not allowed.");
        }

        return normalized;
    }

    private async Task<Dictionary<int, InventoryItem>> LoadValidInventoryItemsAsync(int branchId, IEnumerable<int> inventoryItemIds)
    {
        var ids = inventoryItemIds.Where(x => x > 0).Distinct().ToList();
        return await _context.InventoryItems
            .Where(x =>
                x.BranchId == branchId &&
                x.IsActive &&
                x.IsStockTracked &&
                !x.IsExpenseOnly &&
                x.AllowRecipeConsumption &&
                x.ConsumptionMode == ConsumptionMode.RecipeConsumption &&
                ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
    }

    private static void EnsureOneIngredientRow(Recipe recipe)
    {
        if (recipe.Ingredients.Count == 0)
        {
            recipe.Ingredients.Add(new RecipeIngredient());
        }
    }
}
