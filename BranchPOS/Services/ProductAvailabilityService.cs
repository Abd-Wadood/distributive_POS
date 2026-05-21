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
        var requirements = await _context.RecipeIngredients
            .AsNoTracking()
            .Where(x => x.Recipe!.BranchId == branchId && x.Recipe.ProductId == productId && x.Recipe.IsActive && x.Recipe.Product!.IsActive)
            .Select(x => new InventoryRequirement(x.InventoryItemId, x.QuantityRequired))
            .ToListAsync(cancellationToken);

        if (requirements.Count == 0)
        {
            return false;
        }

        var kitchenLocationId = await GetKitchenLocationIdAsync(branchId, cancellationToken);
        var inventoryItemIds = requirements.Select(x => x.InventoryItemId).ToList();
        var inventory = await _context.InventoryStocks
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.InventoryLocationId == kitchenLocationId && inventoryItemIds.Contains(x.InventoryItemId))
            .Select(x => new { x.InventoryItemId, x.Quantity })
            .ToDictionaryAsync(x => x.InventoryItemId, x => x.Quantity, cancellationToken);

        return requirements.All(x => inventory.TryGetValue(x.InventoryItemId, out var available) && available >= x.QuantityRequired * quantity);
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
        var requirements = await _context.RecipeIngredients
            .AsNoTracking()
            .Where(x => x.Recipe!.BranchId == branchId && x.Recipe.IsActive && productIds.Contains(x.Recipe.ProductId))
            .Select(x => new
            {
                x.Recipe!.ProductId,
                Requirement = new InventoryRequirement(x.InventoryItemId, x.QuantityRequired)
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
        var kitchenLocationId = await GetKitchenLocationIdAsync(branchId, cancellationToken);
        var inventoryItemIds = menu.Products
            .SelectMany(x => x.Requirements)
            .Select(x => x.InventoryItemId)
            .Distinct()
            .ToList();

        if (inventoryItemIds.Count == 0)
        {
            return [];
        }

        // Live inventory is intentionally not cached; final order completion also locks inventory rows in the DB.
        return await _context.InventoryStocks
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.InventoryLocationId == kitchenLocationId && inventoryItemIds.Contains(x.InventoryItemId))
            .Select(x => new { x.InventoryItemId, x.Quantity })
            .ToDictionaryAsync(x => x.InventoryItemId, x => x.Quantity, cancellationToken);
    }

    private static bool CanMakeOne(PosProductMenuItem product, Dictionary<int, decimal> inventory) =>
        product.IsActive &&
        product.Requirements.Count > 0 &&
        product.Requirements.All(x => inventory.TryGetValue(x.InventoryItemId, out var available) && available >= x.QuantityRequired);

    private async Task<int> GetKitchenLocationIdAsync(int branchId, CancellationToken cancellationToken)
    {
        var location = await _context.InventoryLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.Name == "Kitchen", cancellationToken);
        if (location is not null)
        {
            return location.Id;
        }

        location = new InventoryLocation { BranchId = branchId, Name = "Kitchen" };
        _context.InventoryLocations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);
        return location.Id;
    }

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
        public List<InventoryRequirement> Requirements { get; init; } = [];
    }

    private sealed record PosCategoryMenuItem(int Id, string Name);

    private sealed record InventoryRequirement(int InventoryItemId, decimal QuantityRequired);
}
