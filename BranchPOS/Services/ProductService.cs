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
            .Include(x => x.ProductIngredients)
            .ThenInclude(x => x.Ingredient)
            .Where(x => x.BranchId == branchId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        return await _context.Products
            .Include(x => x.ProductIngredients)
            .ThenInclude(x => x.Ingredient)
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.Id == id, cancellationToken);
    }

    public async Task CreateProductAsync(Product product, Dictionary<int, decimal> ingredientQuantities, CancellationToken cancellationToken = default)
    {
        product.BranchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        foreach (var pair in ingredientQuantities.Where(x => x.Value > 0))
        {
            product.ProductIngredients.Add(new ProductIngredient
            {
                IngredientId = pair.Key,
                QuantityRequired = pair.Value
            });
        }

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);
        _posMenuCacheInvalidator.Invalidate();
    }

    public async Task UpdateProductAsync(Product product, Dictionary<int, decimal> ingredientQuantities, CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var existing = await _context.Products
            .Include(x => x.ProductIngredients)
            .FirstOrDefaultAsync(x => x.Id == product.Id && x.BranchId == branchId, cancellationToken);

        if (existing is null)
        {
            throw new InvalidOperationException("Product not found.");
        }

        existing.Name = product.Name;
        existing.Price = product.Price;
        existing.CategoryId = product.CategoryId;
        existing.ProductIngredients.Clear();

        foreach (var pair in ingredientQuantities.Where(x => x.Value > 0))
        {
            existing.ProductIngredients.Add(new ProductIngredient
            {
                IngredientId = pair.Key,
                QuantityRequired = pair.Value
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        _posMenuCacheInvalidator.Invalidate();
    }
}
