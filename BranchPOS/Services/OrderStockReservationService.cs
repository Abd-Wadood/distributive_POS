using System.Data;
using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BranchPOS.Services;

public class OrderStockReservationService : IOrderStockReservationService
{
    private readonly AppDbContext _context;
    private readonly IInventoryTransactionService _inventoryTransactionService;

    public OrderStockReservationService(AppDbContext context, IInventoryTransactionService inventoryTransactionService)
    {
        _context = context;
        _inventoryTransactionService = inventoryTransactionService;
    }

    public Task<OrderResultDto> ReserveForOrderAsync(Order order, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionIfNeededAsync(() => ReserveForOrderCoreAsync(order, cancellationToken), cancellationToken);

    public Task<OrderResultDto> ConsumeImmediatelyForOrderAsync(Order order, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionIfNeededAsync(() => ConsumeImmediatelyForOrderCoreAsync(order, cancellationToken), cancellationToken);

    public Task<OrderResultDto> RestoreConsumedOrderAsync(int orderId, int branchId, string userId, string? reason = null, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionIfNeededAsync(() => RestoreConsumedOrderCoreAsync(orderId, branchId, userId, reason, cancellationToken), cancellationToken);

    public Task<OrderResultDto> WasteConsumedOrderAsync(int orderId, int branchId, string userId, string? reason = null, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionIfNeededAsync(() => WasteConsumedOrderCoreAsync(orderId, branchId, userId, reason, cancellationToken), cancellationToken);

    public Task<OrderResultDto> ConsumeReservationAsync(int orderId, int branchId, string userId, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionIfNeededAsync(() => ProcessReservationCoreAsync(orderId, branchId, userId, ReservationAction.Consume, null, cancellationToken), cancellationToken);

    public Task<OrderResultDto> ReleaseReservationAsync(int orderId, int branchId, string userId, string? reason = null, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionIfNeededAsync(() => ProcessReservationCoreAsync(orderId, branchId, userId, ReservationAction.Release, reason, cancellationToken), cancellationToken);

    public Task<OrderResultDto> WasteReservationAsync(int orderId, int branchId, string userId, string? reason = null, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionIfNeededAsync(() => ProcessReservationCoreAsync(orderId, branchId, userId, ReservationAction.Waste, reason, cancellationToken), cancellationToken);

    private async Task<OrderResultDto> ReserveForOrderCoreAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.Id <= 0)
        {
            throw new BusinessException("Order must be saved before stock can be reserved.");
        }

        if (order.InventoryState == OrderInventoryState.Reserved)
        {
            var hasActiveReservation = await _context.OrderInventoryReservations
                .AnyAsync(x => x.OrderId == order.Id && x.Status == OrderInventoryReservationStatus.Active, cancellationToken);
            if (!hasActiveReservation)
            {
                throw new BusinessException("Reservation record is missing. Manager review required.");
            }

            return ToResult(order);
        }

        if (order.InventoryState != OrderInventoryState.None)
        {
            throw new BusinessException("Reservation record is missing or already processed. Manager review required.");
        }

        var kitchen = await _inventoryTransactionService.GetOrCreateLocationAsync(order.BranchId, "Kitchen", cancellationToken);
        var requirements = await BuildRequirementsAsync(order, kitchen.Id, cancellationToken);
        if (requirements.Count == 0)
        {
            order.OrderStatus = OrderStatus.Pending;
            order.InventoryState = OrderInventoryState.None;
            order.CompletedAt = null;
            await _context.SaveChangesAsync(cancellationToken);
            return ToResult(order);
        }

        foreach (var requirement in requirements.OrderBy(x => x.Key))
        {
            var stock = await LockStockAsync(order.BranchId, requirement.Key, kitchen.Id, cancellationToken);
            if (stock is null)
            {
                throw new BusinessException($"Not enough kitchen stock for {requirement.Value.Name}. Available: 0, required: {requirement.Value.Quantity:0.###}.");
            }

            var available = stock.QuantityBase - stock.ReservedQuantityBase;
            if (available < requirement.Value.Quantity)
            {
                throw new BusinessException($"Not enough kitchen stock for {requirement.Value.Name}. Available: {available:0.###} {requirement.Value.Unit}, required: {requirement.Value.Quantity:0.###} {requirement.Value.Unit}.");
            }

            stock.ReservedQuantityBase += requirement.Value.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;
            var reservation = new OrderInventoryReservation
            {
                BranchId = order.BranchId,
                OrderId = order.Id,
                InventoryStockId = stock.Id,
                InventoryItemId = stock.InventoryItemId,
                InventoryLocationId = stock.InventoryLocationId,
                RequiredQuantityBase = requirement.Value.Quantity,
                Status = OrderInventoryReservationStatus.Active,
                IdempotencyKey = order.ClientRequestId ?? order.IdempotencyKey
            };
            _context.OrderInventoryReservations.Add(reservation);
            AddReservationMovement(
                order,
                stock,
                requirement.Value.Quantity,
                stock.AverageUnitCostBase,
                InventoryMovementType.ReserveForOrder,
                null,
                stock.InventoryLocationId,
                "Reserved for pending order.",
                "reserve");
        }

        order.OrderStatus = OrderStatus.Pending;
        order.InventoryState = OrderInventoryState.Reserved;
        order.CompletedAt = null;
        await _context.SaveChangesAsync(cancellationToken);
        return ToResult(order);
    }

    private async Task<OrderResultDto> ConsumeImmediatelyForOrderCoreAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.Id <= 0)
        {
            throw new BusinessException("Order must be saved before stock can be consumed.");
        }

        if (order.InventoryState == OrderInventoryState.Consumed)
        {
            return ToResult(order);
        }

        if (order.InventoryState != OrderInventoryState.None)
        {
            throw new BusinessException("This order inventory has already been processed. Manager review required.");
        }

        var kitchen = await _inventoryTransactionService.GetOrCreateLocationAsync(order.BranchId, "Kitchen", cancellationToken);
        var requirements = await BuildRequirementsAsync(order, kitchen.Id, cancellationToken);
        foreach (var requirement in requirements.OrderBy(x => x.Key))
        {
            var stock = await LockStockAsync(order.BranchId, requirement.Key, kitchen.Id, cancellationToken);
            if (stock is null)
            {
                throw new BusinessException($"Not enough kitchen stock for {requirement.Value.Name}. Available: 0, required: {requirement.Value.Quantity:0.###}.");
            }

            var available = stock.QuantityBase - stock.ReservedQuantityBase;
            if (available < requirement.Value.Quantity)
            {
                throw new BusinessException($"Not enough kitchen stock for {requirement.Value.Name}. Available: {available:0.###} {requirement.Value.Unit}, required: {requirement.Value.Quantity:0.###} {requirement.Value.Unit}.");
            }

            stock.QuantityBase -= requirement.Value.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;
            AddReservationMovement(
                order,
                stock,
                requirement.Value.Quantity,
                stock.AverageUnitCostBase,
                InventoryMovementType.ConsumeReservation,
                stock.InventoryLocationId,
                null,
                "Consumed immediately when order was punched.",
                "sale-consume");
        }

        var now = DateTime.UtcNow;
        order.OrderStatus = OrderStatus.Completed;
        order.InventoryState = OrderInventoryState.Consumed;
        order.SentToKitchenAt = now;
        order.CompletedAt = now;
        AddPrintJob(order, PrintJobType.KOT, "Kitchen", "Kitchen token created when order was punched.");
        AddPrintJob(order, PrintJobType.CustomerBill, "Counter", "Customer receipt created when order was punched.");
        await _context.SaveChangesAsync(cancellationToken);
        return ToResult(order);
    }

    private async Task<OrderResultDto> RestoreConsumedOrderCoreAsync(
        int orderId,
        int branchId,
        string userId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.BranchId == branchId, cancellationToken)
            ?? throw new PosNotFoundException("Order was not found.");

        if (order.InventoryState == OrderInventoryState.Restored && order.OrderStatus == OrderStatus.Cancelled)
        {
            return ToResult(order);
        }

        if (order.InventoryState == OrderInventoryState.Reserved)
        {
            return await ProcessReservationCoreAsync(orderId, branchId, userId, ReservationAction.Release, reason, cancellationToken);
        }

        if (order.InventoryState == OrderInventoryState.None)
        {
            var cancellationTime = DateTime.UtcNow;
            order.OrderStatus = OrderStatus.Cancelled;
            order.InventoryState = OrderInventoryState.Restored;
            order.PaymentStatus = PaymentStatus.Cancelled;
            order.CancelledAt = cancellationTime;
            order.CancelledByUserId = userId;
            order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled before preparation. No stock had been consumed." : reason.Trim();
            order.InventoryCorrectionType = "RestoreStock";
            await _context.SaveChangesAsync(cancellationToken);
            return ToResult(order);
        }

        if (order.InventoryState != OrderInventoryState.Consumed)
        {
            throw new BusinessException("Only consumed orders can be restored.");
        }

        if (order.OrderStatus is OrderStatus.Cancelled or OrderStatus.CancelledAfterPreparation or OrderStatus.CancelledAsWaste)
        {
            throw new BusinessException("This order cannot be restored from its current state.");
        }

        var kitchen = await _inventoryTransactionService.GetOrCreateLocationAsync(order.BranchId, "Kitchen", cancellationToken);
        var requirements = await BuildRequirementsAsync(order, kitchen.Id, cancellationToken);
        foreach (var requirement in requirements.OrderBy(x => x.Key))
        {
            var stock = await LockStockAsync(order.BranchId, requirement.Key, kitchen.Id, cancellationToken);
            if (stock is null)
            {
                stock = new InventoryStock
                {
                    BranchId = order.BranchId,
                    InventoryItemId = requirement.Key,
                    InventoryLocationId = kitchen.Id
                };
                _context.InventoryStocks.Add(stock);
                await _context.SaveChangesAsync(cancellationToken);
                stock = await LockStockAsync(order.BranchId, requirement.Key, kitchen.Id, cancellationToken)
                    ?? throw new BusinessException("Inventory balance could not be restored. Please retry.");
            }

            stock.QuantityBase += requirement.Value.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;
            AddReservationMovement(
                order,
                stock,
                requirement.Value.Quantity,
                stock.AverageUnitCostBase,
                InventoryMovementType.CancelReturn,
                null,
                stock.InventoryLocationId,
                string.IsNullOrWhiteSpace(reason) ? "Order cancellation restore." : reason.Trim(),
                "cancel-return");
        }

        var now = DateTime.UtcNow;
        order.OrderStatus = OrderStatus.Cancelled;
        order.InventoryState = OrderInventoryState.Restored;
        order.PaymentStatus = PaymentStatus.Cancelled;
        order.CancelledAt = now;
        order.CancelledByUserId = userId;
        order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled and stock restored." : reason.Trim();
        order.InventoryCorrectionType = "RestoreStock";
        await _context.SaveChangesAsync(cancellationToken);
        return ToResult(order);
    }

    private async Task<OrderResultDto> WasteConsumedOrderCoreAsync(
        int orderId,
        int branchId,
        string userId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == orderId && x.BranchId == branchId, cancellationToken)
            ?? throw new PosNotFoundException("Order was not found.");

        if (order.InventoryState == OrderInventoryState.Wasted && order.OrderStatus is OrderStatus.CancelledAsWaste or OrderStatus.CancelledAfterPreparation)
        {
            return ToResult(order);
        }

        if (order.InventoryState == OrderInventoryState.Reserved)
        {
            return await ProcessReservationCoreAsync(orderId, branchId, userId, ReservationAction.Waste, reason, cancellationToken);
        }

        if (order.InventoryState == OrderInventoryState.None)
        {
            var cancellationTime = DateTime.UtcNow;
            order.OrderStatus = OrderStatus.CancelledAsWaste;
            order.InventoryState = OrderInventoryState.Wasted;
            order.PaymentStatus = PaymentStatus.Cancelled;
            order.CancelledAt = cancellationTime;
            order.CancelledByUserId = userId;
            order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled as prepared. No stock change was required." : reason.Trim();
            order.InventoryCorrectionType = "Waste";
            await _context.SaveChangesAsync(cancellationToken);
            return ToResult(order);
        }

        if (order.InventoryState != OrderInventoryState.Consumed)
        {
            throw new BusinessException("Only consumed orders can be cancelled as waste.");
        }

        if (order.OrderStatus is OrderStatus.Cancelled or OrderStatus.CancelledAfterPreparation or OrderStatus.CancelledAsWaste)
        {
            throw new BusinessException("This order cannot be cancelled as waste from its current state.");
        }

        var now = DateTime.UtcNow;
        order.OrderStatus = OrderStatus.CancelledAsWaste;
        order.InventoryState = OrderInventoryState.Wasted;
        order.PaymentStatus = PaymentStatus.Cancelled;
        order.CancelledAt = now;
        order.CancelledByUserId = userId;
        order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled as waste after stock was consumed." : reason.Trim();
        order.InventoryCorrectionType = "Waste";
        await _context.SaveChangesAsync(cancellationToken);
        return ToResult(order);
    }

    private async Task<OrderResultDto> ProcessReservationCoreAsync(
        int orderId,
        int branchId,
        string userId,
        ReservationAction action,
        string? reason,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(x => x.InventoryReservations)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.BranchId == branchId, cancellationToken)
            ?? throw new PosNotFoundException("Order was not found.");

        if (action == ReservationAction.Consume &&
            order.OrderStatus == OrderStatus.Completed &&
            order.InventoryState == OrderInventoryState.Consumed)
        {
            return ToResult(order);
        }

        if (action == ReservationAction.Release &&
            order.OrderStatus == OrderStatus.Cancelled &&
            order.InventoryState == OrderInventoryState.Released)
        {
            return ToResult(order);
        }

        if (action == ReservationAction.Waste &&
            order.OrderStatus == OrderStatus.CancelledAfterPreparation &&
            order.InventoryState == OrderInventoryState.Wasted)
        {
            return ToResult(order);
        }

        if (order.OrderStatus != OrderStatus.Pending)
        {
            throw new BusinessException(order.OrderStatus switch
            {
                OrderStatus.Completed => "This order has already been completed.",
                OrderStatus.Cancelled or OrderStatus.CancelledAfterPreparation => "This order has already been cancelled.",
                _ => "Only pending reserved orders can be processed."
            });
        }

        if (order.InventoryState == OrderInventoryState.None)
        {
            return await ProcessNoReservationOrderAsync(order, userId, action, reason, cancellationToken);
        }

        if (order.InventoryState != OrderInventoryState.Reserved)
        {
            throw new BusinessException("Reservation record is missing or already processed. Manager review required.");
        }

        var activeReservations = order.InventoryReservations
            .Where(x => x.Status == OrderInventoryReservationStatus.Active)
            .OrderBy(x => x.InventoryStockId)
            .ToList();
        if (activeReservations.Count == 0)
        {
            throw new BusinessException("No active reservation was found for this order. Manager review required.");
        }

        foreach (var reservation in activeReservations)
        {
            var stock = await LockStockByIdAsync(reservation.InventoryStockId, cancellationToken)
                ?? throw new BusinessException("Reservation record is missing or already processed. Manager review required.");

            if (stock.ReservedQuantityBase < reservation.RequiredQuantityBase)
            {
                throw new BusinessException("Reservation record is missing or already processed. Manager review required.");
            }

            if (action is ReservationAction.Consume or ReservationAction.Waste &&
                stock.QuantityBase < reservation.RequiredQuantityBase)
            {
                throw new BusinessException("Stock changed while processing this order. Please try again.");
            }

            stock.ReservedQuantityBase -= reservation.RequiredQuantityBase;
            if (action is ReservationAction.Consume or ReservationAction.Waste)
            {
                stock.QuantityBase -= reservation.RequiredQuantityBase;
            }
            stock.UpdatedAt = DateTime.UtcNow;

            reservation.Status = action switch
            {
                ReservationAction.Release => OrderInventoryReservationStatus.Released,
                ReservationAction.Waste => OrderInventoryReservationStatus.Wasted,
                _ => OrderInventoryReservationStatus.Consumed
            };
            var processedAt = DateTime.UtcNow;
            reservation.ReleasedAt = action == ReservationAction.Release ? processedAt : reservation.ReleasedAt;
            reservation.ConsumedAt = action == ReservationAction.Consume ? processedAt : reservation.ConsumedAt;
            reservation.WastedAt = action == ReservationAction.Waste ? processedAt : reservation.WastedAt;

            var movementType = action switch
            {
                ReservationAction.Release => InventoryMovementType.ReleaseReservation,
                ReservationAction.Waste => InventoryMovementType.WasteReservation,
                _ => InventoryMovementType.ConsumeReservation
            };
            AddReservationMovement(
                order,
                stock,
                reservation.RequiredQuantityBase,
                stock.AverageUnitCostBase,
                movementType,
                stock.InventoryLocationId,
                null,
                action switch
                {
                    ReservationAction.Release => reason ?? "Released pending order reservation.",
                    ReservationAction.Waste => reason ?? "Wasted prepared order reservation.",
                    _ => "Consumed pending order reservation."
                },
                action.ToString().ToLowerInvariant());
        }

        var now = DateTime.UtcNow;
        switch (action)
        {
            case ReservationAction.Release:
                order.OrderStatus = OrderStatus.Cancelled;
                order.InventoryState = OrderInventoryState.Released;
                order.CancelledAt = now;
                order.CancelledByUserId = userId;
                order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled before preparation." : reason.Trim();
                break;
            case ReservationAction.Waste:
                order.OrderStatus = OrderStatus.CancelledAfterPreparation;
                order.InventoryState = OrderInventoryState.Wasted;
                order.CancelledAt = now;
                order.CancelledByUserId = userId;
                order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled after preparation." : reason.Trim();
                break;
            default:
                order.OrderStatus = OrderStatus.Completed;
                order.InventoryState = OrderInventoryState.Consumed;
                order.CompletedAt = now;
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ToResult(order);
    }

    private async Task<OrderResultDto> ProcessNoReservationOrderAsync(
        Order order,
        string userId,
        ReservationAction action,
        string? reason,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        switch (action)
        {
            case ReservationAction.Consume:
                order.OrderStatus = OrderStatus.Completed;
                order.InventoryState = OrderInventoryState.Consumed;
                order.CompletedAt = now;
                break;
            case ReservationAction.Release:
                order.OrderStatus = OrderStatus.Cancelled;
                order.InventoryState = OrderInventoryState.Released;
                order.CancelledAt = now;
                order.CancelledByUserId = userId;
                order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled before preparation." : reason.Trim();
                break;
            default:
                throw new BusinessException("No active reservation was found for this order. Manager review required.");
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ToResult(order);
    }

    private async Task<Dictionary<int, RequiredInventoryItem>> BuildRequirementsAsync(Order order, int kitchenLocationId, CancellationToken cancellationToken)
    {
        var orderItems = order.Items.Count > 0
            ? order.Items.ToList()
            : await _context.OrderItems.Where(x => x.OrderId == order.Id).ToListAsync(cancellationToken);

        var productIds = orderItems.Select(x => x.ProductId).Distinct().ToList();
        if (productIds.Count == 0)
        {
            return [];
        }

        var products = await _context.Products
            .Include(x => x.DirectInventoryItem)
            .Include(x => x.Recipes.Where(r => r.IsActive))
            .ThenInclude(x => x.Ingredients)
            .ThenInclude(x => x.InventoryItem)
            .Where(x => x.BranchId == order.BranchId && productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var requirements = new Dictionary<int, RequiredInventoryItem>();
        foreach (var orderItem in orderItems)
        {
            if (!products.TryGetValue(orderItem.ProductId, out var product))
            {
                throw new PosNotFoundException("One or more products are unavailable. Refresh the POS screen and try again.");
            }

            var recipe = product.Recipes.FirstOrDefault(x => x.IsActive);
            if (recipe is null || recipe.Ingredients.Count == 0)
            {
                continue;
            }

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.InventoryItem is null || !ingredient.InventoryItem.IsStockTracked || ingredient.InventoryItem.IsExpenseOnly)
                {
                    continue;
                }

                AddRequirement(
                    requirements,
                    ingredient.InventoryItemId,
                    ingredient.InventoryItem.Name,
                    ingredient.InventoryItem.BaseUnit,
                    ingredient.QuantityRequiredBase * orderItem.Quantity);
            }
        }

        _ = kitchenLocationId;
        return requirements;
    }

    private static void AddRequirement(Dictionary<int, RequiredInventoryItem> requirements, int inventoryItemId, string name, string unit, decimal quantity)
    {
        if (quantity <= 0)
        {
            return;
        }

        if (requirements.TryGetValue(inventoryItemId, out var existing))
        {
            requirements[inventoryItemId] = existing with { Quantity = existing.Quantity + quantity };
        }
        else
        {
            requirements[inventoryItemId] = new RequiredInventoryItem(name, unit, quantity);
        }
    }

    private async Task<InventoryStock?> LockStockAsync(int branchId, int inventoryItemId, int locationId, CancellationToken cancellationToken) =>
        await EnsurePostgresProvider().InventoryStocks
            .FromSqlInterpolated($"SELECT *, xmin FROM \"InventoryStocks\" WHERE \"BranchId\" = {branchId} AND \"InventoryItemId\" = {inventoryItemId} AND \"InventoryLocationId\" = {locationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<InventoryStock?> LockStockByIdAsync(int inventoryStockId, CancellationToken cancellationToken) =>
        await EnsurePostgresProvider().InventoryStocks
            .FromSqlInterpolated($"SELECT *, xmin FROM \"InventoryStocks\" WHERE \"Id\" = {inventoryStockId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private AppDbContext EnsurePostgresProvider()
    {
        if (!(_context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            throw new BusinessException("Stock reservation row locking is configured for PostgreSQL. Configure provider-specific locking before processing reservations.");
        }

        return _context;
    }

    private void AddReservationMovement(
        Order order,
        InventoryStock stock,
        decimal quantity,
        decimal unitCost,
        InventoryMovementType movementType,
        int? fromLocationId,
        int? toLocationId,
        string note,
        string keySuffix)
    {
        var totalCost = movementType is InventoryMovementType.ReserveForOrder or InventoryMovementType.ReleaseReservation
            ? 0m
            : quantity * unitCost;
        _inventoryTransactionService.AddMovement(new InventoryMovementRequest(
            order.BranchId,
            stock.InventoryItemId,
            fromLocationId,
            toLocationId,
            quantity,
            unitCost,
            totalCost,
            movementType,
            nameof(Order),
            order.Id,
            order.UserSessionId,
            order.TerminalId,
            BuildMovementKey(order, keySuffix, stock.Id),
            order.CashierId,
            Note: note));
    }

    private void AddPrintJob(Order order, PrintJobType printType, string target, string reason)
    {
        var payload = JsonSerializer.Serialize(new
        {
            order.Id,
            order.OrderNumber,
            order.OrderType,
            order.PaymentStatus,
            order.TotalAmount,
            Items = order.Items.Select(x => new
            {
                x.ProductNameSnapshot,
                x.Quantity,
                x.UnitPrice,
                x.LineTotal
            }),
            Reason = reason
        });

        _context.PrintJobs.Add(new PrintJob
        {
            BranchId = order.BranchId,
            TerminalId = order.TerminalId > 0 ? order.TerminalId : null,
            OrderId = order.Id,
            PrintType = printType,
            PrinterTarget = target,
            PayloadJson = payload,
            Status = PrintJobStatus.Pending,
            CreatedByUserId = order.CashierId
        });
    }

    private static string BuildMovementKey(Order order, string suffix, int stockId)
    {
        var baseKey = order.ClientRequestId ?? order.IdempotencyKey ?? $"order-{order.Id}";
        var key = $"{baseKey}:{suffix}:stock-{stockId}";
        return key.Length <= 120 ? key : key[..120];
    }

    private async Task<OrderResultDto> ExecuteInTransactionIfNeededAsync(Func<Task<OrderResultDto>> action, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is not null)
        {
            return await action();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    private static OrderResultDto ToResult(Order order) =>
        new()
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Subtotal = order.Subtotal,
            DiscountAmount = order.DiscountAmount,
            TotalAmount = order.TotalAmount,
            Status = order.OrderStatus.ToString(),
            InventoryState = order.InventoryState.ToString(),
            PaymentStatus = order.PaymentStatus.ToString()
        };

    private enum ReservationAction
    {
        Consume,
        Release,
        Waste
    }

    private sealed record RequiredInventoryItem(string Name, string Unit, decimal Quantity);
}
