using BranchPOS.Data;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class RestaurantInventoryService : IRestaurantInventoryService
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IInventoryTransactionService _inventoryTransactionService;
    private readonly IIdempotencyService _idempotencyService;

    public RestaurantInventoryService(AppDbContext context, IBranchContextService branchContextService, IInventoryTransactionService inventoryTransactionService, IIdempotencyService idempotencyService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _inventoryTransactionService = inventoryTransactionService;
        _idempotencyService = idempotencyService;
    }

    public async Task<List<InventoryStock>> GetStockAsync(string locationName, CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var location = await _inventoryTransactionService.GetOrCreateLocationAsync(branchId, locationName, cancellationToken);
        return await _context.InventoryStocks
            .Include(x => x.InventoryItem)
            .Include(x => x.InventoryLocation)
            .Where(x => x.BranchId == branchId && x.InventoryLocationId == location.Id)
            .OrderBy(x => x.InventoryItem!.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task DispatchKitchenRequestAsync(int requestId, string userId, Dictionary<int, decimal>? quantitiesToSend = null, string? managerNotes = null, int? userSessionId = null, int? terminalId = null, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var stockRoom = await _inventoryTransactionService.GetOrCreateLocationAsync(branchId, "Stock Room", cancellationToken);
        var kitchen = await _inventoryTransactionService.GetOrCreateLocationAsync(branchId, "Kitchen", cancellationToken);

        var request = await _context.KitchenRequests
            .Include(x => x.Details)
            .ThenInclude(x => x.InventoryItem)
            .FirstOrDefaultAsync(x => x.Id == requestId && x.BranchId == branchId, cancellationToken)
            ?? throw new PosNotFoundException("Kitchen request was not found.");

        if (request.Status == KitchenRequestStatus.Dispatched)
        {
            throw new BusinessException("This kitchen request has already been dispatched.");
        }

        if (request.Status is not KitchenRequestStatus.Approved and not KitchenRequestStatus.Pending and not KitchenRequestStatus.PendingManagerReview and not KitchenRequestStatus.PartiallyDispatched)
        {
            throw new BusinessException("Only pending, approved, or partially dispatched kitchen requests can be dispatched.");
        }

        idempotencyKey ??= $"dispatch-{requestId}";
        var idempotencyHash = _idempotencyService.HashPayload(new
        {
            RequestId = requestId,
            BranchId = branchId,
            UserId = userId,
            UserSessionId = userSessionId,
            TerminalId = terminalId,
            ManagerNotes = managerNotes,
            Details = request.Details.OrderBy(x => x.Id).Select(x => new
            {
                x.Id,
                x.InventoryItemId,
                Quantity = quantitiesToSend != null && quantitiesToSend.TryGetValue(x.Id, out var requested)
                    ? requested
                    : x.ApprovedQuantity
            })
        });
        var idempotency = await _idempotencyService.BeginAsync("KitchenRequest.Dispatch", idempotencyKey, idempotencyHash, userId, branchId, terminalId, cancellationToken);
        if (!idempotency.IsOwner)
        {
            if (!string.IsNullOrWhiteSpace(idempotency.ErrorMessage))
            {
                throw new BusinessException(idempotency.ErrorMessage);
            }

            if (idempotency.Record.Status == IdempotencyStatus.Completed)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            throw new BusinessException("This request is already being processed. Please wait.");
        }

        var movedAny = false;
        foreach (var detail in request.Details.OrderBy(x => x.InventoryItemId))
        {
            var alreadyDispatched = detail.DispatchedQuantity ?? 0;
            var quantity = quantitiesToSend != null && quantitiesToSend.TryGetValue(detail.Id, out var quantityToSend)
                ? quantityToSend
                : (detail.ApprovedQuantity ?? detail.RequestedQuantity) - alreadyDispatched;
            if (quantity <= 0)
            {
                continue;
            }

            if (detail.InventoryItem?.BranchId != branchId)
            {
                throw new BusinessException("Kitchen request contains an inventory item outside the active branch.");
            }

            if (detail.InventoryItem is null || !InventoryControlDefaults.CanDispatchToKitchen(detail.InventoryItem))
            {
                throw new BusinessException($"{detail.InventoryItem?.Name ?? "This item"} cannot be dispatched to the kitchen under its consumption mode.");
            }

            var fromStock = await LockStockAsync(branchId, detail.InventoryItemId, stockRoom.Id, cancellationToken)
                ?? throw new BusinessException($"Not enough stock room quantity for {detail.InventoryItem?.Name}. Required: {quantity:0.###} {detail.InventoryItem?.BaseUnit}, Available: 0 {detail.InventoryItem?.BaseUnit}.");

            if (fromStock.QuantityBase < quantity)
            {
                throw new BusinessException($"Not enough stock room quantity for {detail.InventoryItem?.Name}. Required: {quantity:0.###} {detail.InventoryItem?.BaseUnit}, Available: {fromStock.QuantityBase:0.###} {detail.InventoryItem?.BaseUnit}.");
            }

            var debit = await _inventoryTransactionService.DebitAsync(
                branchId,
                detail.InventoryItemId,
                stockRoom.Id,
                quantity,
                detail.InventoryItem?.Name ?? "Ingredient",
                detail.InventoryItem?.BaseUnit ?? string.Empty,
                "stock room",
                cancellationToken);
            await _inventoryTransactionService.CreditAsync(branchId, detail.InventoryItemId, kitchen.Id, quantity, debit.AverageUnitCostBase, cancellationToken);
            movedAny = true;
            detail.ApprovedQuantity = alreadyDispatched + quantity;
            detail.DispatchedQuantity = alreadyDispatched + quantity;
            detail.Status = detail.DispatchedQuantity >= detail.RequestedQuantity
                ? KitchenRequestDetailStatus.Dispatched
                : KitchenRequestDetailStatus.PartiallyDispatched;
            _inventoryTransactionService.AddMovement(new InventoryMovementRequest(
                branchId,
                detail.InventoryItemId,
                stockRoom.Id,
                kitchen.Id,
                quantity,
                debit.AverageUnitCostBase,
                quantity * debit.AverageUnitCostBase,
                InventoryMovementType.StockRoomToKitchenDispatch,
                nameof(KitchenRequest),
                request.Id,
                userSessionId,
                terminalId,
                idempotencyKey,
                userId,
                detail.Id));
        }

        if (!movedAny)
        {
            throw new BusinessException("Enter at least one quantity greater than zero to dispatch.");
        }

        request.Status = request.Details.All(x => (x.DispatchedQuantity ?? 0) >= x.RequestedQuantity)
            ? KitchenRequestStatus.Dispatched
            : KitchenRequestStatus.PartiallyDispatched;
        request.ApprovedByUserId = userId;
        request.ReviewedByUserId = userId;
        request.DispatchedByUserId = userId;
        request.ApprovedAt ??= DateTime.UtcNow;
        request.ReviewedAt = DateTime.UtcNow;
        request.DispatchedAt = DateTime.UtcNow;
        request.ManagerNotes = string.IsNullOrWhiteSpace(managerNotes) ? request.ManagerNotes : managerNotes.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        await _idempotencyService.CompleteAsync(idempotency.Record, nameof(KitchenRequest), request.Id, StatusCodes.Status200OK, request.RequestNumber, cancellationToken);
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
            .Where(x => x.BranchId == branchId && (x.MovementType == InventoryMovementType.Consumption || x.MovementType == InventoryMovementType.ManualConsumption));
        var wastageMovements = _context.InventoryMovements.AsNoTracking()
            .Where(x => x.BranchId == branchId && x.MovementType == InventoryMovementType.Wastage);
        if (fromUtc.HasValue)
        {
            movements = movements.Where(x => x.CreatedAt >= fromUtc.Value);
            wastageMovements = wastageMovements.Where(x => x.CreatedAt >= fromUtc.Value);
        }
        if (toUtc.HasValue)
        {
            movements = movements.Where(x => x.CreatedAt < toUtc.Value);
            wastageMovements = wastageMovements.Where(x => x.CreatedAt < toUtc.Value);
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

        var adjustments = _context.InventoryAdjustments.AsNoTracking()
            .Where(x => x.BranchId == branchId && x.Status == InventoryAdjustmentStatus.Approved);
        if (fromUtc.HasValue)
        {
            adjustments = adjustments.Where(x => x.ApprovedAt >= fromUtc.Value);
        }
        if (toUtc.HasValue)
        {
            adjustments = adjustments.Where(x => x.ApprovedAt < toUtc.Value);
        }

        var salesRevenue = await orders.SumAsync(x => x.TotalAmount, cancellationToken);
        var ingredientCost = await movements.SumAsync(x => x.TotalCost, cancellationToken);
        var operationalExpenses = await expenses.SumAsync(x => x.Amount, cancellationToken);
        var stockRoomWasteCost = await adjustments
            .Where(x => x.LocationType == InventoryLocationType.StockRoom && x.AdjustmentType == InventoryAdjustmentType.Waste)
            .SumAsync(x => x.TotalCost, cancellationToken);
        var kitchenWasteCost = await adjustments
            .Where(x => x.LocationType == InventoryLocationType.Kitchen && x.AdjustmentType == InventoryAdjustmentType.Waste)
            .SumAsync(x => x.TotalCost, cancellationToken);
        var missingStockCost = await adjustments
            .Where(x => x.AdjustmentType == InventoryAdjustmentType.Missing)
            .SumAsync(x => x.TotalCost, cancellationToken);
        var expiredStockCost = await adjustments
            .Where(x => x.AdjustmentType == InventoryAdjustmentType.Expired)
            .SumAsync(x => x.TotalCost, cancellationToken);
        var damagedStockCost = await adjustments
            .Where(x => x.AdjustmentType == InventoryAdjustmentType.Damaged)
            .SumAsync(x => x.TotalCost, cancellationToken);
        var spillageCost = await adjustments
            .Where(x => x.AdjustmentType == InventoryAdjustmentType.Spillage)
            .SumAsync(x => x.TotalCost, cancellationToken);
        var manualWastageCost = await wastageMovements.SumAsync(x => x.TotalCost, cancellationToken);
        var correctionIncreaseTotal = await adjustments
            .Where(x => x.AdjustmentType == InventoryAdjustmentType.CorrectionIncrease)
            .SumAsync(x => x.TotalCost, cancellationToken);
        var correctionDecreaseTotal = await adjustments
            .Where(x => x.AdjustmentType == InventoryAdjustmentType.CorrectionDecrease)
            .SumAsync(x => x.TotalCost, cancellationToken);
        var inventoryLoss = stockRoomWasteCost + kitchenWasteCost + manualWastageCost + missingStockCost + expiredStockCost + damagedStockCost + spillageCost + correctionDecreaseTotal - correctionIncreaseTotal;
        return new ProfitReportViewModel
        {
            From = from,
            To = to,
            SalesRevenue = salesRevenue,
            IngredientCost = ingredientCost,
            InventoryLoss = inventoryLoss,
            OperationalExpenses = operationalExpenses,
            NetProfit = salesRevenue - ingredientCost - inventoryLoss - operationalExpenses,
            StockRoomWasteCost = stockRoomWasteCost,
            KitchenWasteCost = kitchenWasteCost,
            MissingStockCost = missingStockCost,
            ExpiredStockCost = expiredStockCost,
            DamagedStockCost = damagedStockCost,
            SpillageCost = spillageCost,
            CorrectionIncreaseTotal = correctionIncreaseTotal,
            CorrectionDecreaseTotal = correctionDecreaseTotal
        };
    }

    private async Task<InventoryStock?> LockStockAsync(int branchId, int inventoryItemId, int locationId, CancellationToken cancellationToken) =>
        await _context.InventoryStocks
            .FromSqlInterpolated($"SELECT *, xmin FROM \"InventoryStocks\" WHERE \"BranchId\" = {branchId} AND \"InventoryItemId\" = {inventoryItemId} AND \"InventoryLocationId\" = {locationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

}
