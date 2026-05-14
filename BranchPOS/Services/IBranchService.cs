using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IBranchService
{
    Task<List<Branch>> GetBranchesForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task EnsureBranchAccessAsync(string userId, int branchId, CancellationToken cancellationToken = default);
}
