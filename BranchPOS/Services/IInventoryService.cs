using BranchPOS.DTOs;
using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IInventoryService
{
    Task<List<Inventory>> GetInventoryAsync(CancellationToken cancellationToken = default);

    Task AdjustInventoryAsync(InventoryAdjustmentDto dto, CancellationToken cancellationToken = default);
}
