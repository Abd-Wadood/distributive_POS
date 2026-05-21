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

    public RecipesController(AppDbContext context, IBranchContextService branchContextService)
    {
        _context = context;
        _branchContextService = branchContextService;
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
                .FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId.Value && x.IsActive);
        }

        recipe ??= new Recipe { BranchId = branchId, ProductId = productId ?? 0 };
        while (recipe.Ingredients.Count < 8)
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
        model.Ingredients = model.Ingredients.Where(x => x.InventoryItemId > 0 && x.QuantityRequired > 0).ToList();
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
            return View(model);
        }

        var productExists = await _context.Products.AnyAsync(x => x.Id == model.ProductId && x.BranchId == branchId && x.IsActive);
        if (!productExists)
        {
            return NotFound();
        }

        var inventoryItemIds = model.Ingredients.Select(x => x.InventoryItemId).Distinct().ToList();
        var validInventoryItemCount = await _context.InventoryItems.CountAsync(x => x.BranchId == branchId && x.IsActive && inventoryItemIds.Contains(x.Id));
        if (validInventoryItemCount != inventoryItemIds.Count)
        {
            ModelState.AddModelError(string.Empty, "One or more recipe inventory items do not belong to the active branch.");
            await PopulateListsAsync(branchId);
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
                QuantityRequired = ingredient.Sum(x => x.QuantityRequired)
            });
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateListsAsync(int branchId)
    {
        ViewBag.Products = await _context.Products
            .Where(x => x.BranchId == branchId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
        ViewBag.InventoryItems = await _context.InventoryItems
            .Where(x => x.BranchId == branchId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.Unit})", x.Id.ToString()))
            .ToListAsync();
    }
}
