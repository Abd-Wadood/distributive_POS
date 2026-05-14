using BranchPOS.Data;
using BranchPOS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class ProductAvailabilityService : IProductAvailabilityService
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;

    public ProductAvailabilityService(AppDbContext context, IBranchContextService branchContextService)
    {
        _context = context;
        _branchContextService = branchContextService;
    }

    public async Task<bool> CanMakeProductAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return false;
        }

        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var product = await _context.Products
            .Include(x => x.ProductIngredients)
            .ThenInclude(x => x.Ingredient)
            .ThenInclude(x => x!.Inventory)
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.Id == productId, cancellationToken);

        return product is not null &&
               product.ProductIngredients.Count > 0 &&
               product.ProductIngredients.All(x => (x.Ingredient?.Inventory?.CurrentQuantity ?? 0) >= x.QuantityRequired * quantity);
    }

    public async Task<HashSet<int>> GetUnavailableProductsAsync(CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var products = await _context.Products
            .Include(x => x.ProductIngredients)
            .ThenInclude(x => x.Ingredient)
            .ThenInclude(x => x!.Inventory)
            .Where(x => x.BranchId == branchId)
            .ToListAsync(cancellationToken);

        return products
            .Where(product => product.ProductIngredients.Count == 0 ||
                              product.ProductIngredients.Any(recipe => (recipe.Ingredient?.Inventory?.CurrentQuantity ?? 0) < recipe.QuantityRequired))
            .Select(x => x.Id)
            .ToHashSet();
    }

    public async Task<List<PosProductViewModel>> GetPosProductsAsync(CancellationToken cancellationToken = default)
    {
        var unavailable = await GetUnavailableProductsAsync(cancellationToken);
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        return await _context.Products
            .Include(x => x.Category)
            .Where(x => x.BranchId == branchId)
            .OrderBy(x => x.Category!.Name)
            .ThenBy(x => x.Name)
            .Select(x => new PosProductViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Price = x.Price,
                CategoryId = x.CategoryId,
                CategoryName = x.Category!.Name,
                IsAvailable = !unavailable.Contains(x.Id)
            })
            .ToListAsync(cancellationToken);
    }
}
