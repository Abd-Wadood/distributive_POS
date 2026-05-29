using BranchPOS.Data;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IPosMenuCacheInvalidator _posMenuCacheInvalidator;

    public ProductService(AppDbContext context, IBranchContextService branchContextService, IPosMenuCacheInvalidator posMenuCacheInvalidator)
    {
        _context = context;
        _branchContextService = branchContextService;
        _posMenuCacheInvalidator = posMenuCacheInvalidator;
    }

    public async Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        return await _context.Products
            .Include(x => x.Category)
            .Include(x => x.Recipes.Where(r => r.IsActive))
            .ThenInclude(x => x.Ingredients)
            .ThenInclude(x => x.InventoryItem)
            .Where(x => x.BranchId == branchId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        return await _context.Products
            .Include(x => x.Recipes.Where(r => r.IsActive))
            .ThenInclude(x => x.Ingredients)
            .ThenInclude(x => x.InventoryItem)
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.Id == id, cancellationToken);
    }

    public async Task CreateProductAsync(Product product, Dictionary<int, decimal> recipeItemQuantities, CancellationToken cancellationToken = default)
    {
        product.BranchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var normalized = NormalizeRecipeQuantities(recipeItemQuantities);
        var inventoryItems = await LoadValidInventoryItemsAsync(product.BranchId, normalized.Keys, cancellationToken);
        if (normalized.Count > 0)
        {
            var recipe = new Recipe { BranchId = product.BranchId, Product = product, IsActive = true };
            foreach (var pair in normalized)
            {
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    InventoryItemId = pair.Key,
                    QuantityRequiredBase = pair.Value,
                    DisplayQuantity = pair.Value,
                    DisplayUnit = inventoryItems[pair.Key].BaseUnit
                });
            }

            product.Recipes.Add(recipe);
        }

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);
        _posMenuCacheInvalidator.Invalidate();
    }

    public async Task UpdateProductAsync(Product product, Dictionary<int, decimal> recipeItemQuantities, CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var existing = await _context.Products
            .Include(x => x.Recipes.Where(r => r.IsActive))
            .ThenInclude(x => x.Ingredients)
            .FirstOrDefaultAsync(x => x.Id == product.Id && x.BranchId == branchId, cancellationToken);

        if (existing is null)
        {
            throw new InvalidOperationException("Product not found.");
        }

        existing.Name = product.Name;
        existing.Price = product.Price;
        existing.CategoryId = product.CategoryId;
        var normalized = NormalizeRecipeQuantities(recipeItemQuantities);
        var inventoryItems = await LoadValidInventoryItemsAsync(branchId, normalized.Keys, cancellationToken);

        var recipe = existing.Recipes.FirstOrDefault(x => x.IsActive);
        if (recipe is null && normalized.Count > 0)
        {
            recipe = new Recipe { BranchId = branchId, ProductId = existing.Id, IsActive = true };
            existing.Recipes.Add(recipe);
        }

        if (recipe is not null)
        {
            recipe.BranchId = branchId;
            recipe.Ingredients.Clear();
            foreach (var pair in normalized)
            {
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    InventoryItemId = pair.Key,
                    QuantityRequiredBase = pair.Value,
                    DisplayQuantity = pair.Value,
                    DisplayUnit = inventoryItems[pair.Key].BaseUnit
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _posMenuCacheInvalidator.Invalidate();
    }

    private static Dictionary<int, decimal> NormalizeRecipeQuantities(Dictionary<int, decimal> recipeItemQuantities)
    {
        if (recipeItemQuantities.Any(x => x.Key <= 0 || x.Value <= 0))
        {
            throw new InvalidOperationException("Recipe ingredients must have an inventory item and a quantity greater than zero.");
        }

        return recipeItemQuantities;
    }

    private async Task<Dictionary<int, InventoryItem>> LoadValidInventoryItemsAsync(int branchId, IEnumerable<int> inventoryItemIds, CancellationToken cancellationToken)
    {
        var ids = inventoryItemIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var items = await _context.InventoryItems
            .Where(x => x.BranchId == branchId && x.IsActive && ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (items.Count != ids.Count)
        {
            throw new InvalidOperationException("One or more recipe inventory items do not belong to the active branch.");
        }

        return items;
    }
}
