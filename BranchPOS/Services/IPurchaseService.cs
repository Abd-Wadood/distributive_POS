using BranchPOS.DTOs;
using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IPurchaseService
{
    Task<List<Purchase>> GetPurchasesAsync(CancellationToken cancellationToken = default);

    Task<int> CreatePurchaseAsync(CreatePurchaseDto dto, CancellationToken cancellationToken = default);
}
