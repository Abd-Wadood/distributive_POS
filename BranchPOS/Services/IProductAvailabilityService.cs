using BranchPOS.ViewModels;
using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IProductAvailabilityService
{
    Task<bool> CanMakeProductAsync(int productId, int quantity, CancellationToken cancellationToken = default);

    Task<HashSet<int>> GetUnavailableProductsAsync(CancellationToken cancellationToken = default);

    Task<List<PosProductViewModel>> GetPosProductsAsync(CancellationToken cancellationToken = default);

    Task<List<Category>> GetPosCategoriesAsync(CancellationToken cancellationToken = default);
}
