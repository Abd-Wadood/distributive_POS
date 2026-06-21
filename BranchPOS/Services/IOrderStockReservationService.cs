using BranchPOS.DTOs;
using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IOrderStockReservationService
{
    Task<OrderResultDto> ReserveForOrderAsync(Order order, CancellationToken cancellationToken = default);

    Task<OrderResultDto> ConsumeImmediatelyForOrderAsync(Order order, CancellationToken cancellationToken = default);

    Task<OrderResultDto> RestoreConsumedOrderAsync(int orderId, int branchId, string userId, string? reason = null, CancellationToken cancellationToken = default);

    Task<OrderResultDto> WasteConsumedOrderAsync(int orderId, int branchId, string userId, string? reason = null, CancellationToken cancellationToken = default);

    Task<OrderResultDto> ConsumeReservationAsync(int orderId, int branchId, string userId, CancellationToken cancellationToken = default);

    Task<OrderResultDto> ReleaseReservationAsync(int orderId, int branchId, string userId, string? reason = null, CancellationToken cancellationToken = default);

    Task<OrderResultDto> WasteReservationAsync(int orderId, int branchId, string userId, string? reason = null, CancellationToken cancellationToken = default);
}
