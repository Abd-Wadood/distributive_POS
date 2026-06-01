using BranchPOS.DTOs;
using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IManualKitchenUsageService
{
    Task<ManualKitchenUsage> CreateAsync(CreateManualKitchenUsageDto dto, string userId, int branchId, CancellationToken cancellationToken = default);

    Task<List<ManualKitchenUsage>> GetRecentAsync(int branchId, CancellationToken cancellationToken = default);
}
