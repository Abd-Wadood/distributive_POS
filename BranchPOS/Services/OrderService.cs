using System.Data;
using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly ICustomerService _customerService;
    private readonly IUserSessionService _userSessionService;
    private readonly IBranchContextService _branchContextService;
    private readonly IIdempotencyService _idempotencyService;
    private const int MaxOrderItemQuantity = 10000;

    public OrderService(AppDbContext context, ICustomerService customerService, IUserSessionService userSessionService, IBranchContextService branchContextService, IIdempotencyService idempotencyService)
    {
        _context = context;
        _customerService = customerService;
        _userSessionService = userSessionService;
        _branchContextService = branchContextService;
        _idempotencyService = idempotencyService;
    }

    public async Task<List<Order>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        return await _context.Orders
            .Include(x => x.Cashier)
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Order>> GetDraftOrdersAsync(string cashierId, CancellationToken cancellationToken = default) =>
        _context.Orders
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .Where(x => x.CashierId == cashierId && x.OrderStatus == OrderStatus.Draft)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<List<Order>> ResumeDraftOrdersAsync(int sessionId, CancellationToken cancellationToken = default) =>
        _context.Orders
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .Where(x => x.UserSessionId == sessionId && x.OrderStatus == OrderStatus.Draft)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<int> CreateOrderAsync(CreateOrderDto dto, CancellationToken cancellationToken = default)
    {
        var result = await FinalizeOrderAsync(dto, cancellationToken);
        return result.OrderId;
    }

    public async Task<OrderResultDto> CreateDraftOrderAsync(DraftOrderDto dto, CancellationToken cancellationToken = default)
    {
        dto.DraftOrderId = null;
        return await SaveDraftAsync(dto, cancellationToken);
    }

    public Task<OrderResultDto> UpdateDraftOrderAsync(DraftOrderDto dto, CancellationToken cancellationToken = default) =>
        SaveDraftAsync(dto, cancellationToken);

    public async Task<OrderResultDto> FinalizeOrderAsync(CreateOrderDto dto, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {

            var session = await RequireActiveSessionAsync(dto.CashierId, dto.UserSessionId, cancellationToken);
            ValidateTerminal(dto, session);
            await ValidateActiveTerminalAsync(session, cancellationToken);
            dto.BranchId = session.BranchId;
            dto.UserSessionId = session.Id;
            dto.TerminalName = session.TerminalName;
            dto.TerminalId = session.TerminalId;
            dto.TerminalCode = session.TerminalCode;
            dto.Customer.BranchId = session.BranchId;
            var idempotencyHash = _idempotencyService.HashPayload(new
            {
                dto.DraftOrderId,
                dto.CashierId,
                dto.BranchId,
                dto.UserSessionId,
                dto.TerminalId,
                dto.OrderType,
                dto.DiscountAmount,
                dto.TableNumber,
                dto.Notes,
                Customer = dto.Customer,
                Items = dto.Items.OrderBy(x => x.ProductId).Select(x => new { x.ProductId, x.Quantity })
            });
            var idempotency = await _idempotencyService.BeginAsync("OrderFinalize", dto.IdempotencyKey, idempotencyHash, dto.CashierId, dto.BranchId, dto.TerminalId, cancellationToken);
            if (!idempotency.IsOwner)
            {
                if (!string.IsNullOrWhiteSpace(idempotency.ErrorMessage))
                {
                    throw new BusinessException(idempotency.ErrorMessage);
                }

                if (idempotency.Record.Status == IdempotencyStatus.Completed && idempotency.Record.ResourceId.HasValue)
                {
                    var existingOrder = await _context.Orders.FirstOrDefaultAsync(x => x.Id == idempotency.Record.ResourceId.Value, cancellationToken)
                        ?? throw new PosNotFoundException("The previous order result was not found. Refresh and try again.");
                    await transaction.CommitAsync(cancellationToken);
                    return ToResult(existingOrder);
                }

                throw new BusinessException("This request is already being processed. Please wait.");
            }

            var orderType = ParseOrderType(dto.OrderType);
            ValidateCustomerRules(orderType, dto.Customer);
            var requestedItems = NormalizeItems(dto.Items);
            var customer = await _customerService.CreateOrUpdateCustomerAsync(dto.Customer, cancellationToken);
            var pricedItems = await BuildPricedItemsAsync(requestedItems, dto.BranchId, cancellationToken);
            var requiredIngredients = BuildRequiredIngredients(pricedItems);
            var lockedInventories = await LockInventoriesAsync(requiredIngredients.Keys, dto.BranchId, cancellationToken);

            ValidateStock(requiredIngredients, lockedInventories);

            var order = dto.DraftOrderId.HasValue
                ? await _context.Orders.Include(x => x.Items).FirstOrDefaultAsync(x =>
                    x.Id == dto.DraftOrderId.Value &&
                    x.CashierId == dto.CashierId &&
                    x.BranchId == dto.BranchId &&
                    x.UserSessionId == dto.UserSessionId &&
                    x.OrderStatus == OrderStatus.Draft, cancellationToken)
                : null;

            if (dto.DraftOrderId.HasValue && order is null)
            {
                throw new PosNotFoundException("Draft order was not found. Refresh the order list and try again.");
            }

            order ??= new Order
            {
                CashierId = dto.CashierId,
                BranchId = dto.BranchId,
                UserSessionId = dto.UserSessionId,
                OrderNumber = await GenerateOrderNumberAsync(dto.BranchId, cancellationToken),
                IdempotencyKey = dto.IdempotencyKey
            };
            order.IdempotencyKey ??= dto.IdempotencyKey;

            ApplyOrderFields(order, orderType, OrderStatus.Completed, customer, dto);
            ReplaceOrderItems(order, pricedItems);
            order.CompletedAt = DateTime.UtcNow;

            if (order.Id == 0)
            {
                _context.Orders.Add(order);
            }

            await _context.SaveChangesAsync(cancellationToken);

            foreach (var required in requiredIngredients)
            {
                var inventory = lockedInventories.Single(x => x.IngredientId == required.Key);
                inventory.CurrentQuantity -= required.Value;
                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    BranchId = dto.BranchId,
                    IngredientId = required.Key,
                    UserSessionId = dto.UserSessionId,
                    PerformedByUserId = dto.CashierId,
                    TerminalId = dto.TerminalId,
                    TerminalCode = dto.TerminalCode,
                    IdempotencyKey = $"{dto.IdempotencyKey}:{required.Key}",
                    TransactionType = InventoryTransactionType.Sale,
                    QuantityChanged = -required.Value,
                    ReferenceId = order.Id
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _idempotencyService.CompleteAsync(idempotency.Record, nameof(Order), order.Id, StatusCodes.Status200OK, order.OrderNumber, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(order);
        }
        catch (Exception ex) when (ex is BranchPosException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
        catch (Exception ex) when (ex is not BranchPosException && DatabaseErrorTranslator.IsConcurrencyFailure(ex))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw DatabaseErrorTranslator.ToUserException(ex, "Order could not be completed. Please retry.");
        }
        catch (Exception ex) when (ex is not BranchPosException && DatabaseErrorTranslator.IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw DatabaseErrorTranslator.ToUserException(ex, "Order could not be completed. Please retry.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task CancelDraftOrderAsync(int orderId, string cashierId, CancellationToken cancellationToken = default)
    {
        var session = await RequireActiveSessionAsync(cashierId, 0, cancellationToken);
        var order = await _context.Orders.FirstOrDefaultAsync(x =>
            x.Id == orderId &&
            x.CashierId == cashierId &&
            x.BranchId == session.BranchId &&
            x.UserSessionId == session.Id &&
            x.OrderStatus == OrderStatus.Draft, cancellationToken);

        if (order is null)
        {
            throw new PosNotFoundException("Draft order was not found. Refresh the order list and try again.");
        }

        order.OrderStatus = OrderStatus.Cancelled;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Order?> GetReceiptAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        return await _context.Orders
            .Include(x => x.Cashier)
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.BranchId == branchId, cancellationToken);
    }

    private async Task<OrderResultDto> SaveDraftAsync(DraftOrderDto dto, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var session = await RequireActiveSessionAsync(dto.CashierId, dto.UserSessionId, cancellationToken);
        ValidateTerminal(dto, session);
        await ValidateActiveTerminalAsync(session, cancellationToken);
        dto.BranchId = session.BranchId;
        dto.UserSessionId = session.Id;
        dto.TerminalName = session.TerminalName;
        dto.TerminalId = session.TerminalId;
        dto.TerminalCode = session.TerminalCode;
        dto.Customer.BranchId = session.BranchId;
        var orderType = ParseOrderType(dto.OrderType);
        ValidateCustomerRules(orderType, dto.Customer, allowEmptyCart: true);
        var requestedItems = NormalizeItems(dto.Items, allowEmpty: true);
        var customer = await _customerService.CreateOrUpdateCustomerAsync(dto.Customer, cancellationToken);
        var pricedItems = requestedItems.Count == 0
            ? new List<PricedOrderItem>()
            : await BuildPricedItemsAsync(requestedItems, dto.BranchId, cancellationToken, requireRecipe: false);

        var order = dto.DraftOrderId.HasValue
            ? await _context.Orders.Include(x => x.Items).FirstOrDefaultAsync(x =>
                x.Id == dto.DraftOrderId.Value &&
                x.CashierId == dto.CashierId &&
                x.BranchId == dto.BranchId &&
                x.UserSessionId == dto.UserSessionId &&
                x.OrderStatus == OrderStatus.Draft, cancellationToken)
            : null;

        if (dto.DraftOrderId.HasValue && order is null)
        {
            throw new PosNotFoundException("Draft order was not found. Refresh the order list and try again.");
        }

        order ??= new Order
        {
            CashierId = dto.CashierId,
            BranchId = dto.BranchId,
            UserSessionId = dto.UserSessionId,
            OrderNumber = await GenerateOrderNumberAsync(dto.BranchId, cancellationToken)
        };

        ApplyOrderFields(order, orderType, OrderStatus.Draft, customer, dto);
        ReplaceOrderItems(order, pricedItems);
        order.CompletedAt = null;

        if (order.Id == 0)
        {
            _context.Orders.Add(order);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(order);
    }

    private static List<OrderItemDto> NormalizeItems(IEnumerable<OrderItemDto> items, bool allowEmpty = false)
    {
        var itemList = items.ToList();
        if (itemList.Any(x => x.ProductId <= 0))
        {
            throw new PosValidationException("One of the selected products is invalid. Refresh the POS screen and try again.");
        }

        if (itemList.Any(x => x.Quantity <= 0))
        {
            throw new PosValidationException("Order item quantity must be greater than zero.");
        }

        if (itemList.Any(x => x.Quantity > MaxOrderItemQuantity))
        {
            throw new PosValidationException("Order item quantity is too large.");
        }

        var normalized = itemList
            .GroupBy(x => x.ProductId)
            .Select(x => new OrderItemDto { ProductId = x.Key, Quantity = x.Sum(y => y.Quantity) })
            .ToList();

        if (!allowEmpty && normalized.Count == 0)
        {
            throw new PosValidationException("Order must contain at least one item.");
        }

        return normalized;
    }

    private async Task<List<PricedOrderItem>> BuildPricedItemsAsync(List<OrderItemDto> requestedItems, int branchId, CancellationToken cancellationToken, bool requireRecipe = true)
    {
        var productIds = requestedItems.Select(x => x.ProductId).ToList();
        var products = await _context.Products
            .Include(x => x.ProductIngredients)
            .Where(x => x.BranchId == branchId && x.IsActive && productIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (products.Count != productIds.Count)
        {
            throw new PosNotFoundException("One or more products are unavailable. Refresh the POS screen and try again.");
        }

        var pricedItems = new List<PricedOrderItem>();
        foreach (var requestedItem in requestedItems)
        {
            var product = products.Single(x => x.Id == requestedItem.ProductId);
            if (requireRecipe && product.ProductIngredients.Count == 0)
            {
                throw new BusinessException($"{product.Name} cannot be sold because its recipe is incomplete.");
            }

            pricedItems.Add(new PricedOrderItem(product, requestedItem.Quantity));
        }

        return pricedItems;
    }

    private static Dictionary<int, decimal> BuildRequiredIngredients(List<PricedOrderItem> pricedItems)
    {
        var requiredIngredients = new Dictionary<int, decimal>();
        foreach (var pricedItem in pricedItems)
        {
            if (pricedItem.Product.ProductIngredients.Count == 0)
            {
                throw new BusinessException($"{pricedItem.Product.Name} cannot be sold because its recipe is incomplete.");
            }

            foreach (var recipeItem in pricedItem.Product.ProductIngredients)
            {
                requiredIngredients.TryAdd(recipeItem.IngredientId, 0);
                requiredIngredients[recipeItem.IngredientId] += recipeItem.QuantityRequired * pricedItem.Quantity;
            }
        }

        return requiredIngredients;
    }

    private async Task<List<Inventory>> LockInventoriesAsync(IEnumerable<int> ingredientIds, int branchId, CancellationToken cancellationToken)
    {
        var lockedInventories = new List<Inventory>();
        foreach (var ingredientId in ingredientIds.OrderBy(x => x))
        {
            var inventory = await _context.Inventories
                .FromSqlInterpolated($"SELECT * FROM \"Inventories\" WHERE \"BranchId\" = {branchId} AND \"IngredientId\" = {ingredientId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (inventory is null)
            {
                throw new BusinessException("Product recipe is missing an inventory record. Ask a StockManager to review ingredients.");
            }

            lockedInventories.Add(inventory);
        }

        return lockedInventories;
    }

    private static void ValidateStock(Dictionary<int, decimal> requiredIngredients, List<Inventory> lockedInventories)
    {
        foreach (var required in requiredIngredients)
        {
            var inventory = lockedInventories.Single(x => x.IngredientId == required.Key);
            if (inventory.CurrentQuantity < required.Value)
            {
                throw new BusinessException("Not enough stock to complete this order.");
            }
        }
    }

    private static void ApplyOrderFields(Order order, OrderType orderType, OrderStatus status, Customer? customer, CreateOrderDto dto)
    {
        order.Customer = customer;
        order.CustomerId = customer?.Id > 0 ? customer.Id : null;
        order.BranchId = dto.BranchId;
        order.UserSessionId = dto.UserSessionId;
        order.TerminalName = string.IsNullOrWhiteSpace(dto.TerminalName) ? null : dto.TerminalName.Trim();
        order.TerminalId = dto.TerminalId;
        order.TerminalCode = dto.TerminalCode;
        order.OrderType = orderType;
        order.OrderStatus = status;
        order.TableNumber = string.IsNullOrWhiteSpace(dto.TableNumber) ? null : dto.TableNumber.Trim();
        order.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        order.DiscountAmount = Math.Max(0, dto.DiscountAmount);
    }

    private static void ReplaceOrderItems(Order order, List<PricedOrderItem> pricedItems)
    {
        order.Items.Clear();
        order.Subtotal = 0;

        foreach (var pricedItem in pricedItems)
        {
            var lineTotal = pricedItem.Product.Price * pricedItem.Quantity;
            order.Items.Add(new OrderItem
            {
                ProductId = pricedItem.Product.Id,
                BranchId = order.BranchId,
                ProductNameSnapshot = pricedItem.Product.Name,
                Quantity = pricedItem.Quantity,
                UnitPrice = pricedItem.Product.Price,
                LineTotal = lineTotal
            });
            order.Subtotal += lineTotal;
        }

        if (order.DiscountAmount > order.Subtotal)
        {
            throw new PosValidationException("Discount cannot be greater than subtotal.");
        }

        order.TotalAmount = order.Subtotal - order.DiscountAmount;
    }

    private static OrderType ParseOrderType(string? value) =>
        Enum.TryParse<OrderType>(value, ignoreCase: true, out var orderType) ? orderType : OrderType.Takeaway;

    private static void ValidateCustomerRules(OrderType orderType, CustomerDto customer, bool allowEmptyCart = false)
    {
        if (orderType == OrderType.Delivery)
        {
            if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
            {
                throw new PosValidationException("Customer phone is required for delivery orders.");
            }

            if (string.IsNullOrWhiteSpace(customer.Address))
            {
                throw new PosValidationException("Customer address is required for delivery orders.");
            }
        }
    }

    private Task<string> GenerateOrderNumberAsync(int branchId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return Task.FromResult($"POS-{now:yyyyMMdd}-B{branchId}-{now:HHmmssfff}-{suffix}");
    }

    private async Task<UserSession> RequireActiveSessionAsync(string userId, int userSessionId, CancellationToken cancellationToken)
    {
        var activeSession = await _userSessionService.GetActiveSessionAsync(userId, cancellationToken)
            ?? throw new BusinessException("Start or continue an active cashier session before creating orders.");

        if (!string.Equals(activeSession.RoleName, "Cashier", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Active cashier session is required for order finalization.");
        }

        if (activeSession.Status is not SessionStatus.Active and not SessionStatus.Reopened)
        {
            throw new BusinessException("This cashier session is closing or unavailable. Continue or resolve the session before creating orders.");
        }

        if (userSessionId > 0 && activeSession.Id != userSessionId)
        {
            throw new BusinessException("Order session does not match the active user session. Resume the correct session and try again.");
        }

        return activeSession;
    }

    private async Task ValidateActiveTerminalAsync(UserSession session, CancellationToken cancellationToken)
    {
        var terminalIsActive = await _context.Terminals.AnyAsync(x =>
            x.Id == session.TerminalId &&
            x.BranchId == session.BranchId &&
            x.TerminalCode == session.TerminalCode &&
            x.IsActive, cancellationToken);

        if (!terminalIsActive)
        {
            throw new BusinessException("Terminal is not registered or is inactive. Register this terminal or contact an administrator.");
        }
    }

    private static void ValidateTerminal(CreateOrderDto dto, UserSession session)
    {
        if (session.TerminalId <= 0 || string.IsNullOrWhiteSpace(session.TerminalCode))
        {
            throw new BusinessException("Active session has no registered terminal. End this session and start a new one on a registered terminal.");
        }

        if (dto.TerminalId <= 0 || string.IsNullOrWhiteSpace(dto.TerminalCode))
        {
            throw new BusinessException("Terminal identity is required. Register this terminal before continuing.");
        }

        if (dto.TerminalId != session.TerminalId)
        {
            throw new BusinessException("Terminal does not match the active user session. End or resume the correct session for this terminal.");
        }

        if (!string.Equals(dto.TerminalCode, session.TerminalCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Terminal does not match the active user session. End or resume the correct session for this terminal.");
        }
    }

    private static OrderResultDto ToResult(Order order) =>
        new()
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Subtotal = order.Subtotal,
            DiscountAmount = order.DiscountAmount,
            TotalAmount = order.TotalAmount
        };

    private sealed record PricedOrderItem(Product Product, int Quantity);
}
