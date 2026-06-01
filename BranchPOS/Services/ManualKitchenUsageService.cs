using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class ManualKitchenUsageService : IManualKitchenUsageService
{
    private readonly AppDbContext _context;
    private readonly IInventoryTransactionService _inventoryTransactionService;

    public ManualKitchenUsageService(AppDbContext context, IInventoryTransactionService inventoryTransactionService)
    {
        _context = context;
        _inventoryTransactionService = inventoryTransactionService;
    }

    public async Task<ManualKitchenUsage> CreateAsync(CreateManualKitchenUsageDto dto, string userId, int branchId, CancellationToken cancellationToken = default)
    {
        var item = await _context.InventoryItems
            .FirstOrDefaultAsync(x => x.Id == dto.InventoryItemId && x.BranchId == branchId && x.IsActive, cancellationToken)
            ?? throw new PosNotFoundException("Inventory item was not found.");
        if (item.ConsumptionMode != ConsumptionMode.ManualKitchenIssue || !item.AllowManualConsumption || !item.IsStockTracked || item.IsExpenseOnly)
        {
            throw new BusinessException("Only ManualKitchenIssue items can be entered in manual kitchen usage.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var kitchen = await _inventoryTransactionService.GetOrCreateLocationAsync(branchId, "Kitchen", cancellationToken);
        var stock = await _context.InventoryStocks
            .FromSqlInterpolated($"SELECT *, xmin FROM \"InventoryStocks\" WHERE \"BranchId\" = {branchId} AND \"InventoryItemId\" = {item.Id} AND \"InventoryLocationId\" = {kitchen.Id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        var currentKitchenQuantity = stock?.QuantityBase ?? 0m;
        var actualUsed = currentKitchenQuantity - dto.ClosingKitchenQuantity;
        if (actualUsed < 0)
        {
            throw new PosValidationException("Closing kitchen quantity cannot be greater than current kitchen stock. Dispatch stock or run a stock count first.");
        }

        var usage = new ManualKitchenUsage
        {
            BranchId = branchId,
            UsageDate = DateTime.SpecifyKind(dto.UsageDate.Date, DateTimeKind.Utc),
            UserSessionId = dto.UserSessionId,
            InventoryItemId = item.Id,
            OpeningKitchenQuantity = dto.OpeningKitchenQuantity,
            ReceivedFromStockRoomQuantity = dto.ReceivedFromStockRoomQuantity,
            ClosingKitchenQuantity = dto.ClosingKitchenQuantity,
            WastedQuantity = 0m,
            ActualUsedQuantity = actualUsed,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedByUserId = userId
        };
        _context.ManualKitchenUsages.Add(usage);
        await _context.SaveChangesAsync(cancellationToken);

        if (actualUsed > 0)
        {
            var mutation = await _inventoryTransactionService.DebitAsync(branchId, item.Id, kitchen.Id, actualUsed, item.Name, item.BaseUnit, "kitchen", cancellationToken);
            _inventoryTransactionService.AddMovement(new InventoryMovementRequest(
                branchId,
                item.Id,
                kitchen.Id,
                null,
                actualUsed,
                mutation.AverageUnitCostBase,
                actualUsed * mutation.AverageUnitCostBase,
                InventoryMovementType.ManualConsumption,
                nameof(ManualKitchenUsage),
                usage.Id,
                dto.UserSessionId,
                null,
                $"manual-usage-{usage.Id}",
                userId,
                Note: usage.Notes));
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return usage;
    }

    public Task<List<ManualKitchenUsage>> GetRecentAsync(int branchId, CancellationToken cancellationToken = default) =>
        _context.ManualKitchenUsages
            .AsNoTracking()
            .Include(x => x.InventoryItem)
            .Include(x => x.CreatedByUser)
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.UsageDate)
            .ThenByDescending(x => x.Id)
            .Take(200)
            .ToListAsync(cancellationToken);
}
