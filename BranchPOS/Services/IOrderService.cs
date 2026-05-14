using BranchPOS.DTOs;
using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IOrderService
{
    Task<List<Order>> GetOrdersAsync(CancellationToken cancellationToken = default);

    Task<List<Order>> GetDraftOrdersAsync(string cashierId, CancellationToken cancellationToken = default);

    Task<List<Order>> ResumeDraftOrdersAsync(int sessionId, CancellationToken cancellationToken = default);

    Task<int> CreateOrderAsync(CreateOrderDto dto, CancellationToken cancellationToken = default);

    Task<OrderResultDto> CreateDraftOrderAsync(DraftOrderDto dto, CancellationToken cancellationToken = default);

    Task<OrderResultDto> UpdateDraftOrderAsync(DraftOrderDto dto, CancellationToken cancellationToken = default);

    Task<OrderResultDto> FinalizeOrderAsync(CreateOrderDto dto, CancellationToken cancellationToken = default);

    Task CancelDraftOrderAsync(int orderId, string cashierId, CancellationToken cancellationToken = default);

    Task<Order?> GetReceiptAsync(int orderId, CancellationToken cancellationToken = default);
}
