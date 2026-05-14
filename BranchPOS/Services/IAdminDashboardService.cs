using BranchPOS.ViewModels;

namespace BranchPOS.Services;

public interface IAdminDashboardService
{
    Task<AdminDashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default);
}
