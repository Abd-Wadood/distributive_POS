using BranchPOS.Data;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class RestaurantInventoryService : IRestaurantInventoryService
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;

    public RestaurantInventoryService(AppDbContext context, IBranchContextService branchContextService)
    {
        _context = context;
        _branchContextService = branchContextService;
    }

    public async Task<List<InventoryStock>> GetStockAsync(string locationName, CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var location = await GetOrCreateLocationAsync(branchId, locationName, cancellationToken);
        return await _context.InventoryStocks
            .Include(x => x.InventoryItem)
            .Include(x => x.InventoryLocation)
            .Where(x => x.BranchId == branchId && x.InventoryLocationId == location.Id)
            .OrderBy(x => x.InventoryItem!.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task DispatchKitchenRequestAsync(int requestId, string userId, CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var stockRoom = await GetOrCreateLocationAsync(branchId, "Stock Room", cancellationToken);
        var kitchen = await GetOrCreateLocationAsync(branchId, "Kitchen", cancellationToken);

        var request = await _context.KitchenRequests
            .Include(x => x.Details)
            .ThenInclude(x => x.InventoryItem)
            .FirstOrDefaultAsync(x => x.Id == requestId && x.BranchId == branchId, cancellationToken)
            ?? throw new PosNotFoundException("Kitchen request was not found.");

        if (request.Status == KitchenRequestStatus.Dispatched)
        {
            throw new BusinessException("This kitchen request has already been dispatched.");
        }

        if (request.Status != KitchenRequestStatus.Approved)
        {
            throw new BusinessException("Only approved kitchen requests can be dispatched.");
        }

        foreach (var detail in request.Details.OrderBy(x => x.InventoryItemId))
        {
            var quantity = detail.ApprovedQuantity ?? 0;
            if (quantity <= 0)
            {
                continue;
            }

            if (detail.InventoryItem?.BranchId != branchId)
            {
                throw new BusinessException("Kitchen request contains an inventory item outside the active branch.");
            }

            var fromStock = await LockStockAsync(branchId, detail.InventoryItemId, stockRoom.Id, cancellationToken)
                ?? throw new BusinessException($"Insufficient stock room stock: {detail.InventoryItem?.Name} required {quantity:0.###}, available 0.");

            if (fromStock.Quantity < quantity)
            {
                throw new BusinessException($"Insufficient stock room stock: {detail.InventoryItem?.Name} required {quantity:0.###}, available {fromStock.Quantity:0.###}.");
            }

            var toStock = await LockStockAsync(branchId, detail.InventoryItemId, kitchen.Id, cancellationToken);
            if (toStock is null)
            {
                toStock = new InventoryStock
                {
                    BranchId = branchId,
                    InventoryItemId = detail.InventoryItemId,
                    InventoryLocationId = kitchen.Id,
                    AverageUnitCost = fromStock.AverageUnitCost
                };
                _context.InventoryStocks.Add(toStock);
            }

            fromStock.Quantity -= quantity;
            toStock.AverageUnitCost = CalculateWeightedAverage(toStock.Quantity, toStock.AverageUnitCost, quantity, fromStock.AverageUnitCost);
            toStock.Quantity += quantity;
            detail.DispatchedQuantity = quantity;
            _context.InventoryMovements.Add(new InventoryMovement
            {
                BranchId = branchId,
                InventoryItemId = detail.InventoryItemId,
                FromLocationId = stockRoom.Id,
                ToLocationId = kitchen.Id,
                Quantity = quantity,
                UnitCost = fromStock.AverageUnitCost,
                TotalCost = quantity * fromStock.AverageUnitCost,
                MovementType = InventoryMovementType.Transfer,
                ReferenceType = nameof(KitchenRequest),
                ReferenceId = request.Id,
                CreatedByUserId = userId
            });
        }

        request.Status = KitchenRequestStatus.Dispatched;
        request.DispatchedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ProfitReportViewModel> BuildProfitReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var fromUtc = from?.ToUniversalTime();
        var toUtc = to?.ToUniversalTime();

        var orders = _context.Orders.AsNoTracking()
            .Where(x => x.BranchId == branchId && x.OrderStatus == OrderStatus.Completed);
        if (fromUtc.HasValue)
        {
            orders = orders.Where(x => x.CompletedAt >= fromUtc.Value);
        }
        if (toUtc.HasValue)
        {
            orders = orders.Where(x => x.CompletedAt < toUtc.Value);
        }

        var movements = _context.InventoryMovements.AsNoTracking()
            .Where(x => x.BranchId == branchId && x.MovementType == InventoryMovementType.Consumption);
        if (fromUtc.HasValue)
        {
            movements = movements.Where(x => x.CreatedAt >= fromUtc.Value);
        }
        if (toUtc.HasValue)
        {
            movements = movements.Where(x => x.CreatedAt < toUtc.Value);
        }

        var expenses = _context.OperationalExpenses.AsNoTracking().Where(x => x.BranchId == branchId);
        if (fromUtc.HasValue)
        {
            expenses = expenses.Where(x => x.ExpenseDate >= fromUtc.Value.Date);
        }
        if (toUtc.HasValue)
        {
            expenses = expenses.Where(x => x.ExpenseDate < toUtc.Value.Date);
        }

        var salesRevenue = await orders.SumAsync(x => x.TotalAmount, cancellationToken);
        var ingredientCost = await movements.SumAsync(x => x.TotalCost, cancellationToken);
        var operationalExpenses = await expenses.SumAsync(x => x.Amount, cancellationToken);
        return new ProfitReportViewModel
        {
            From = from,
            To = to,
            SalesRevenue = salesRevenue,
            IngredientCost = ingredientCost,
            OperationalExpenses = operationalExpenses,
            NetProfit = salesRevenue - ingredientCost - operationalExpenses
        };
    }

    private async Task<InventoryLocation> GetOrCreateLocationAsync(int branchId, string name, CancellationToken cancellationToken)
    {
        var location = await _context.InventoryLocations.FirstOrDefaultAsync(x => x.BranchId == branchId && x.Name == name, cancellationToken);
        if (location is not null)
        {
            return location;
        }

        location = new InventoryLocation { BranchId = branchId, Name = name };
        _context.InventoryLocations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);
        return location;
    }

    private async Task<InventoryStock?> LockStockAsync(int branchId, int inventoryItemId, int locationId, CancellationToken cancellationToken) =>
        await _context.InventoryStocks
            .FromSqlInterpolated($"SELECT *, xmin FROM \"InventoryStocks\" WHERE \"BranchId\" = {branchId} AND \"InventoryItemId\" = {inventoryItemId} AND \"InventoryLocationId\" = {locationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private static decimal CalculateWeightedAverage(decimal oldQuantity, decimal oldAverageCost, decimal incomingQuantity, decimal incomingCost)
    {
        var totalQuantity = oldQuantity + incomingQuantity;
        return totalQuantity <= 0 ? incomingCost : ((oldQuantity * oldAverageCost) + (incomingQuantity * incomingCost)) / totalQuantity;
    }
}
