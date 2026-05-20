using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace BranchPOS.Services;

public class ProductAvailabilityService : IProductAvailabilityService
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IMemoryCache _cache;
    private readonly IPosMenuCacheInvalidator _cacheInvalidator;
    private static readonly TimeSpan PosMenuCacheTtl = TimeSpan.FromMinutes(5);

    public ProductAvailabilityService(AppDbContext context, IBranchContextService branchContextService, IMemoryCache cache, IPosMenuCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _branchContextService = branchContextService;
        _cache = cache;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<bool> CanMakeProductAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return false;
        }

        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var requirements = await _context.ProductIngredients
            .AsNoTracking()
            .Where(x => x.Product!.BranchId == branchId && x.ProductId == productId && x.Product.IsActive)
            .Select(x => new IngredientRequirement(x.IngredientId, x.QuantityRequired))
            .ToListAsync(cancellationToken);

        if (requirements.Count == 0)
        {
            return false;
        }

        var ingredientIds = requirements.Select(x => x.IngredientId).ToList();
        var inventory = await _context.Inventories
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && ingredientIds.Contains(x.IngredientId))
            .Select(x => new { x.IngredientId, x.CurrentQuantity })
            .ToDictionaryAsync(x => x.IngredientId, x => x.CurrentQuantity, cancellationToken);

        return requirements.All(x => inventory.TryGetValue(x.IngredientId, out var available) && available >= x.QuantityRequired * quantity);
    }

    public async Task<HashSet<int>> GetUnavailableProductsAsync(CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var menu = await GetCachedMenuAsync(branchId, cancellationToken);
        var inventory = await GetCurrentInventoryAsync(branchId, menu, cancellationToken);

        return menu.Products
            .Where(product => !CanMakeOne(product, inventory))
            .Select(x => x.Id)
            .ToHashSet();
    }

    public async Task<List<PosProductViewModel>> GetPosProductsAsync(CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var menu = await GetCachedMenuAsync(branchId, cancellationToken);
        var inventory = await GetCurrentInventoryAsync(branchId, menu, cancellationToken);

        return menu.Products
            .OrderBy(x => x.CategoryName)
            .ThenBy(x => x.Name)
            .Select(x => new PosProductViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Price = x.Price,
                CategoryId = x.CategoryId,
                CategoryName = x.CategoryName,
                IsActive = x.IsActive,
                ImagePath = x.ImagePath,
                IsAvailable = CanMakeOne(x, inventory)
            })
            .ToList();
    }

    public async Task<List<Category>> GetPosCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var menu = await GetCachedMenuAsync(branchId, cancellationToken);
        return menu.Categories
            .Select(x => new Category
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToList();
    }

    private async Task<PosMenuCacheItem> GetCachedMenuAsync(int branchId, CancellationToken cancellationToken)
    {
        var cacheKey = $"pos-menu:v1:branch:{branchId}";
        if (_cache.TryGetValue(cacheKey, out PosMenuCacheItem? cached) && cached is not null)
        {
            return cached;
        }

        var products = await _context.Products
            .AsNoTracking()
            .Where(x => x.BranchId == branchId)
            .Select(x => new PosProductMenuItem(
                x.Id,
                x.Name,
                x.Price,
                x.CategoryId,
                x.Category == null ? "" : x.Category.Name,
                x.IsActive,
                null))
            .ToListAsync(cancellationToken);

        var productIds = products.Select(x => x.Id).ToList();
        var requirements = await _context.ProductIngredients
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .Select(x => new
            {
                x.ProductId,
                Requirement = new IngredientRequirement(x.IngredientId, x.QuantityRequired)
            })
            .ToListAsync(cancellationToken);

        var requirementsByProduct = requirements
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Requirement).ToList());

        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new PosCategoryMenuItem(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        var menu = new PosMenuCacheItem(
            products.Select(x => x with
            {
                Requirements = requirementsByProduct.TryGetValue(x.Id, out var productRequirements)
                    ? productRequirements
                    : []
            }).ToList(),
            categories);

        _cache.Set(cacheKey, menu, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = PosMenuCacheTtl
        }.AddExpirationToken(new CancellationChangeToken(_cacheInvalidator.CurrentToken)));

        return menu;
    }

    private async Task<Dictionary<int, decimal>> GetCurrentInventoryAsync(int branchId, PosMenuCacheItem menu, CancellationToken cancellationToken)
    {
        var ingredientIds = menu.Products
            .SelectMany(x => x.Requirements)
            .Select(x => x.IngredientId)
            .Distinct()
            .ToList();

        if (ingredientIds.Count == 0)
        {
            return [];
        }

        // Live inventory is intentionally not cached; final order completion also locks inventory rows in the DB.
        return await _context.Inventories
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && ingredientIds.Contains(x.IngredientId))
            .Select(x => new { x.IngredientId, x.CurrentQuantity })
            .ToDictionaryAsync(x => x.IngredientId, x => x.CurrentQuantity, cancellationToken);
    }

    private static bool CanMakeOne(PosProductMenuItem product, Dictionary<int, decimal> inventory) =>
        product.IsActive &&
        product.Requirements.Count > 0 &&
        product.Requirements.All(x => inventory.TryGetValue(x.IngredientId, out var available) && available >= x.QuantityRequired);

    private sealed record PosMenuCacheItem(List<PosProductMenuItem> Products, List<PosCategoryMenuItem> Categories);

    private sealed record PosProductMenuItem(
        int Id,
        string Name,
        decimal Price,
        int CategoryId,
        string CategoryName,
        bool IsActive,
        string? ImagePath)
    {
        public List<IngredientRequirement> Requirements { get; init; } = [];
    }

    private sealed record PosCategoryMenuItem(int Id, string Name);

    private sealed record IngredientRequirement(int IngredientId, decimal QuantityRequired);
}
