using BranchPOS.ViewModels;

namespace BranchPOS.Services;

public interface IReportService
{
    Task<ReportingViewModel> BuildReportAsync(
        int page,
        int? pageSize,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);
}
