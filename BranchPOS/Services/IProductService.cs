using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IProductService
{
    Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken = default);

    Task<Product?> GetProductAsync(int id, CancellationToken cancellationToken = default);

    Task CreateProductAsync(Product product, Dictionary<int, decimal> ingredientQuantities, CancellationToken cancellationToken = default);

    Task UpdateProductAsync(Product product, Dictionary<int, decimal> ingredientQuantities, CancellationToken cancellationToken = default);
}
