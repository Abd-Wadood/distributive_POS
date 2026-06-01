using BranchPOS.DTOs;
using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IStockCountService
{
    Task<StockCount> CreateAsync(CreateStockCountDto dto, string userId, int branchId, CancellationToken cancellationToken = default);

    Task<List<StockCount>> GetRecentAsync(int branchId, CancellationToken cancellationToken = default);
}
