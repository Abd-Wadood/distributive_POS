using BranchPOS.DTOs;
using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IInventoryAdjustmentService
{
    Task<InventoryAdjustmentResultDto> CreateAdjustmentAsync(CreateInventoryAdjustmentDto dto, string userId, int branchId, CancellationToken cancellationToken = default);

    Task<InventoryAdjustmentResultDto> ApproveAdjustmentAsync(ApproveInventoryAdjustmentDto dto, string userId, int branchId, CancellationToken cancellationToken = default);

    Task<InventoryAdjustmentResultDto> RejectAdjustmentAsync(RejectInventoryAdjustmentDto dto, string userId, int branchId, CancellationToken cancellationToken = default);

    Task<List<InventoryAdjustmentResultDto>> GetAdjustmentsAsync(
        int branchId,
        InventoryLocationType? locationType,
        InventoryAdjustmentType? adjustmentType,
        InventoryAdjustmentStatus? status,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<InventoryAdjustmentResultDto?> GetAdjustmentByIdAsync(int id, int branchId, CancellationToken cancellationToken = default);
}
