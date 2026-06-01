using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class StockCountService : IStockCountService
{
    private readonly AppDbContext _context;
    private readonly IInventoryTransactionService _inventoryTransactionService;

    public StockCountService(AppDbContext context, IInventoryTransactionService inventoryTransactionService)
    {
        _context = context;
        _inventoryTransactionService = inventoryTransactionService;
    }

    public async Task<StockCount> CreateAsync(CreateStockCountDto dto, string userId, int branchId, CancellationToken cancellationToken = default)
    {
        if (!dto.LocationType.HasValue)
        {
            throw new PosValidationException("Location is required.");
        }

        var item = await _context.InventoryItems
            .FirstOrDefaultAsync(x => x.Id == dto.InventoryItemId && x.BranchId == branchId && x.IsActive, cancellationToken)
            ?? throw new PosNotFoundException("Inventory item was not found.");
        if (!item.IsStockTracked || item.IsExpenseOnly)
        {
            throw new BusinessException("Expense-only items do not have stock balances to count.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var location = await _inventoryTransactionService.GetOrCreateLocationAsync(branchId, InventoryAdjustmentService.ToLocationName(dto.LocationType.Value), cancellationToken);
        var stock = await _context.InventoryStocks
            .FromSqlInterpolated($"SELECT *, xmin FROM \"InventoryStocks\" WHERE \"BranchId\" = {branchId} AND \"InventoryItemId\" = {item.Id} AND \"InventoryLocationId\" = {location.Id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var systemQuantity = stock?.QuantityBase ?? 0m;
        var difference = dto.CountedQuantity - systemQuantity;
        var unitCost = stock?.AverageUnitCostBase ?? await GetLastUnitCostAsync(branchId, item.Id, cancellationToken);

        var count = new StockCount
        {
            BranchId = branchId,
            CountDate = DateTime.SpecifyKind(dto.CountDate.Date, DateTimeKind.Utc),
            LocationType = dto.LocationType.Value,
            InventoryItemId = item.Id,
            SystemQuantity = systemQuantity,
            CountedQuantity = dto.CountedQuantity,
            DifferenceQuantity = difference,
            Reason = dto.Reason.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedByUserId = userId
        };
        _context.StockCounts.Add(count);
        await _context.SaveChangesAsync(cancellationToken);

        if (difference > 0)
        {
            await _inventoryTransactionService.CreditAsync(branchId, item.Id, location.Id, difference, unitCost, cancellationToken);
            _inventoryTransactionService.AddMovement(new InventoryMovementRequest(branchId, item.Id, null, location.Id, difference, unitCost, difference * unitCost, InventoryMovementType.Adjustment, nameof(StockCount), count.Id, null, null, $"stock-count-{count.Id}", userId, Note: count.Reason));
        }
        else if (difference < 0)
        {
            var decrease = Math.Abs(difference);
            var mutation = await _inventoryTransactionService.DebitAsync(branchId, item.Id, location.Id, decrease, item.Name, item.BaseUnit, InventoryAdjustmentService.ToLocationName(dto.LocationType.Value), cancellationToken);
            _inventoryTransactionService.AddMovement(new InventoryMovementRequest(branchId, item.Id, location.Id, null, decrease, mutation.AverageUnitCostBase, decrease * mutation.AverageUnitCostBase, InventoryMovementType.Adjustment, nameof(StockCount), count.Id, null, null, $"stock-count-{count.Id}", userId, Note: count.Reason));
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return count;
    }

    public Task<List<StockCount>> GetRecentAsync(int branchId, CancellationToken cancellationToken = default) =>
        _context.StockCounts
            .AsNoTracking()
            .Include(x => x.InventoryItem)
            .Include(x => x.CreatedByUser)
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.CountDate)
            .ThenByDescending(x => x.Id)
            .Take(200)
            .ToListAsync(cancellationToken);

    private async Task<decimal> GetLastUnitCostAsync(int branchId, int itemId, CancellationToken cancellationToken) =>
        await _context.PurchaseItems
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.InventoryItemId == itemId && x.UnitCostBase > 0)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (decimal?)x.UnitCostBase)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;
}
