using BranchPOS.DTOs;

namespace BranchPOS.Services;

public interface IPreparationService
{
    Task<int> CompletePreparationBatchAsync(CompletePreparationBatchDto dto, CancellationToken cancellationToken = default);
}
