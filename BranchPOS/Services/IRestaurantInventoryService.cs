using BranchPOS.Models;
using BranchPOS.ViewModels;

namespace BranchPOS.Services;

public interface IRestaurantInventoryService
{
    Task DispatchKitchenRequestAsync(int requestId, string userId, Dictionary<int, decimal>? quantitiesToSend = null, string? managerNotes = null, int? userSessionId = null, int? terminalId = null, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    Task<List<InventoryStock>> GetStockAsync(string locationName, CancellationToken cancellationToken = default);

    Task<ProfitReportViewModel> BuildProfitReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}
