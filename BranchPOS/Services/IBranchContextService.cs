namespace BranchPOS.Services;

public interface IBranchContextService
{
    Task<int> GetCurrentBranchIdAsync(CancellationToken cancellationToken = default);

    Task EnsureUserCanAccessBranchAsync(int branchId, CancellationToken cancellationToken = default);
}
