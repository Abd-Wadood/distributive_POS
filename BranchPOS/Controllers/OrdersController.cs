using System.Security.Claims;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BranchPOS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Cashier")]
public class OrdersController : Controller
{
    private readonly AppDbContext _context;
    private readonly IOrderService _orderService;
    private readonly IProductAvailabilityService _productAvailabilityService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITerminalContextService _terminalContextService;
    private readonly IErrorLoggingService _errorLoggingService;
    private readonly IIdempotencyService _idempotencyService;

    public OrdersController(
        AppDbContext context,
        IOrderService orderService,
        IProductAvailabilityService productAvailabilityService,
        IUserSessionService userSessionService,
        ITerminalContextService terminalContextService,
        IErrorLoggingService errorLoggingService,
        IIdempotencyService idempotencyService)
    {
        _context = context;
        _orderService = orderService;
        _productAvailabilityService = productAvailabilityService;
        _userSessionService = userSessionService;
        _terminalContextService = terminalContextService;
        _errorLoggingService = errorLoggingService;
        _idempotencyService = idempotencyService;
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        return View(await _orderService.GetOrdersAsync());
    }

    public async Task<IActionResult> Create()
    {
        var cashierId = GetCashierId();
        await _terminalContextService.RequireCurrentTerminalAsync();
        var activeSession = await _userSessionService.GetActiveSessionAsync(cashierId);
        if (activeSession is null)
        {
            return RedirectToAction("Index", "Sessions");
        }

        await TryCreateLowKitchenStockRecommendationsAsync(activeSession.BranchId, cashierId, activeSession.Id, activeSession.TerminalId);
        var products = await _productAvailabilityService.GetPosProductsAsync();
        var drafts = await _orderService.ResumeDraftOrdersAsync(activeSession.Id);
        var categories = await _productAvailabilityService.GetPosCategoriesAsync();

        return View(new PosOrderViewModel
        {
            Products = products,
            Categories = categories,
            DraftOrders = drafts.Select(x => new PosDraftOrderViewModel
            {
                Id = x.Id,
                OrderNumber = x.OrderNumber,
                OrderType = x.OrderType.ToString(),
                DiscountAmount = x.DiscountAmount,
                TableNumber = x.TableNumber,
                Notes = x.Notes,
                CustomerName = x.Customer?.Name,
                CustomerPhone = x.Customer?.PhoneNumber,
                CustomerAddress = x.Customer?.Address,
                Items = x.Items.Select(i => new PosDraftItemViewModel
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList()
            }).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Products()
    {
        var cashierId = GetCashierId();
        var terminal = await _terminalContextService.RequireCurrentTerminalFreshAsync();
        var session = await _userSessionService.GetActiveSessionAsync(cashierId);
        if (session is not null)
        {
            await TryCreateLowKitchenStockRecommendationsAsync(session.BranchId, cashierId, session.Id, terminal.Id);
        }
        return Json(await _productAvailabilityService.GetPosProductsAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft([FromBody] DraftOrderDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { success = false, message = GetModelStateMessage("Please correct the held order details and try again.") });
            }

            dto.CashierId = GetCashierId();
            if (string.IsNullOrWhiteSpace(dto.IdempotencyKey))
            {
                dto.IdempotencyKey = _idempotencyService.GetOrCreateKey();
            }
            var terminal = await _terminalContextService.RequireCurrentTerminalAsync();
            var session = await _userSessionService.GetActiveSessionAsync(dto.CashierId);
            if (session is null)
            {
                throw new InvalidOperationException("Start or continue a session before holding orders.");
            }
            dto.BranchId = session.BranchId;
            dto.UserSessionId = session.Id;
            dto.TerminalName = session.TerminalName;
            dto.TerminalId = terminal.Id;
            dto.TerminalCode = terminal.TerminalCode;
            var result = dto.DraftOrderId.HasValue
                ? await _orderService.UpdateDraftOrderAsync(dto)
                : await _orderService.CreateDraftOrderAsync(dto);

            return Json(new { success = true, draft = result });
        }
        catch (InvalidOperationException ex)
        {
            var message = ToUserMessage(ex);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message });
        }
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("OrderFinalizePolicy"), RequestSizeLimit(65536)]
    public async Task<IActionResult> Finalize([FromBody] CreateOrderDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { success = false, message = GetModelStateMessage("Please correct the order details and try again.") });
            }

            dto.CashierId = GetCashierId();
            if (string.IsNullOrWhiteSpace(dto.IdempotencyKey))
            {
                dto.IdempotencyKey = _idempotencyService.GetOrCreateKey();
            }
            var terminal = await _terminalContextService.RequireCurrentTerminalFreshAsync();
            var session = await _userSessionService.GetActiveSessionAsync(dto.CashierId);
            if (session is null)
            {
                throw new InvalidOperationException("Start or continue a session before finalizing orders.");
            }
            dto.BranchId = session.BranchId;
            dto.UserSessionId = session.Id;
            dto.TerminalName = session.TerminalName;
            dto.TerminalId = terminal.Id;
            dto.TerminalCode = terminal.TerminalCode;
            var result = await _orderService.FinalizeOrderAsync(dto);
            var request = await TryCreateAutoKitchenRequestAsync(dto, string.Empty);
            return Json(new
            {
                success = true,
                receiptUrl = Url.Action(nameof(Receipt), new { id = result.OrderId }),
                order = result,
                notification = request is null ? null : $"Kitchen stock is low. Request {request.RequestNumber} has been sent to stock manager."
            });
        }
        catch (InvalidOperationException ex)
        {
            var message = ToUserMessage(ex);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            var request = await TryCreateAutoKitchenRequestAsync(dto, message);
            if (request is not null)
            {
                message = $"{message} Kitchen stock is low. Request {request.RequestNumber} has been sent to stock manager for review.";
            }

            return Json(new
            {
                success = false,
                message,
                kitchenRequestId = request?.Id,
                kitchenRequestNumber = request?.RequestNumber
            });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelDraft([FromBody] CancelDraftRequest request)
    {
        try
        {
            await _orderService.CancelDraftOrderAsync(request.OrderId, GetCashierId());
            return Json(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            var message = ToUserMessage(ex);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message });
        }
    }

    public async Task<IActionResult> Receipt(int id)
    {
        var order = await _orderService.GetReceiptAsync(id);
        return order is null ? NotFound() : View(order);
    }

    private string GetCashierId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("Authenticated user was not found.");

    private string ToUserMessage(InvalidOperationException ex)
    {
        var message = ex is BranchPosException branchPosException ? branchPosException.UserMessage : ex.Message;
        _errorLoggingService.LogException(HttpContext, ex, message);
        return message;
    }

    private string GetModelStateMessage(string fallback)
    {
        if (ModelState.TryGetValue(nameof(CreateOrderDto.BranchId), out var branchState) &&
            branchState.Errors.Any())
        {
            return "Start or continue an active cashier session before finalizing the sale.";
        }

        if (ModelState.TryGetValue(nameof(CreateOrderDto.UserSessionId), out var sessionState) &&
            sessionState.Errors.Any())
        {
            return "Start or continue an active cashier session before finalizing the sale.";
        }

        if (ModelState.TryGetValue(nameof(CreateOrderDto.TerminalId), out var terminalState) &&
            terminalState.Errors.Any())
        {
            return "This terminal is not registered or is not linked to the active cashier session.";
        }

        var firstError = ModelState.Values
            .SelectMany(x => x.Errors)
            .Select(x => x.ErrorMessage)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return string.IsNullOrWhiteSpace(firstError) ? fallback : firstError;
    }

    private async Task<KitchenRequest?> TryCreateAutoKitchenRequestAsync(CreateOrderDto dto, string failureMessage)
    {
        var branchId = dto.BranchId > 0
            ? dto.BranchId
            : await _context.UserSessions
                .AsNoTracking()
                .Where(x => x.UserId == dto.CashierId && (x.Status == SessionStatus.Active || x.Status == SessionStatus.Reopened))
                .Select(x => x.BranchId)
                .FirstOrDefaultAsync();

        if (dto.Items.Count == 0 ||
            branchId <= 0)
        {
            return null;
        }

        var requestedItems = dto.Items
            .Where(x => x.ProductId > 0 && x.Quantity > 0)
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(y => y.Quantity) })
            .ToList();
        if (requestedItems.Count == 0)
        {
            return null;
        }

        var productIds = requestedItems.Select(x => x.ProductId).ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Include(x => x.Recipes.Where(r => r.IsActive))
            .ThenInclude(x => x.Ingredients)
            .ThenInclude(x => x.InventoryItem)
            .Where(x => x.BranchId == branchId && x.IsActive && productIds.Contains(x.Id))
            .ToListAsync();

        var requiredByItem = new Dictionary<int, AutoKitchenRequestLine>();
        foreach (var requestedItem in requestedItems)
        {
            var product = products.FirstOrDefault(x => x.Id == requestedItem.ProductId);
            var recipe = product?.Recipes.FirstOrDefault(x => x.IsActive);
            if (product is null || recipe is null)
            {
                continue;
            }

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.InventoryItem is null)
                {
                    continue;
                }

                if (!InventoryControlDefaults.CanUseInRecipe(ingredient.InventoryItem))
                {
                    continue;
                }

                var required = ingredient.QuantityRequiredBase * requestedItem.Quantity;
                if (requiredByItem.TryGetValue(ingredient.InventoryItemId, out var existing))
                {
                    requiredByItem[ingredient.InventoryItemId] = existing with { RequiredQuantity = existing.RequiredQuantity + required };
                }
                else
                {
                    requiredByItem[ingredient.InventoryItemId] = new AutoKitchenRequestLine(
                        ingredient.InventoryItemId,
                        ingredient.InventoryItem.Name,
                        ingredient.InventoryItem.BaseUnit,
                        required);
                }
            }
        }

        return requiredByItem.Count == 0
            ? null
            : await CreateAutoKitchenRequestAsync(branchId, requiredByItem, dto.CashierId, dto.UserSessionId, dto.TerminalId, KitchenRequestAutoReason.OrderTriggered);
    }

    private async Task<KitchenRequest?> TryCreateLowKitchenStockRecommendationsAsync(int branchId, string userId, int? userSessionId, int? terminalId)
    {
        if (branchId <= 0)
        {
            return null;
        }

        var recipeIngredients = await _context.RecipeIngredients
            .AsNoTracking()
            .Include(x => x.InventoryItem)
            .Where(x =>
                x.Recipe!.BranchId == branchId &&
                x.Recipe.IsActive &&
                x.Recipe.Product!.IsActive &&
                x.InventoryItem != null &&
                x.InventoryItem.IsStockTracked &&
                !x.InventoryItem.IsExpenseOnly &&
                x.InventoryItem.AllowRecipeConsumption &&
                x.InventoryItem.ConsumptionMode == ConsumptionMode.RecipeConsumption)
            .ToListAsync();

        var requiredByItem = recipeIngredients
            .Where(x => x.InventoryItem is not null)
            .GroupBy(x => x.InventoryItemId)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var item = x.First().InventoryItem!;
                    return new AutoKitchenRequestLine(
                        item.Id,
                        item.Name,
                        item.BaseUnit,
                        x.Sum(y => y.QuantityRequiredBase));
                });

        return requiredByItem.Count == 0
            ? null
            : await CreateAutoKitchenRequestAsync(branchId, requiredByItem, userId, userSessionId, terminalId, KitchenRequestAutoReason.BelowMinimum);
    }

    private async Task<KitchenRequest?> CreateAutoKitchenRequestAsync(
        int branchId,
        Dictionary<int, AutoKitchenRequestLine> requiredByItem,
        string userId,
        int? userSessionId,
        int? terminalId,
        KitchenRequestAutoReason autoReason)
    {
        var kitchen = await GetOrCreateLocationAsync(branchId, "Kitchen");
        var stockRoom = await GetOrCreateLocationAsync(branchId, "Stock Room");
        var inventoryItemIds = requiredByItem.Keys.ToList();
        var inventoryItems = await _context.InventoryItems
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && inventoryItemIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        var kitchenStocks = await _context.InventoryStocks
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.InventoryLocationId == kitchen.Id && inventoryItemIds.Contains(x.InventoryItemId))
            .ToDictionaryAsync(x => x.InventoryItemId, x => x.QuantityBase);
        var stockRoomStocks = await _context.InventoryStocks
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.InventoryLocationId == stockRoom.Id && inventoryItemIds.Contains(x.InventoryItemId))
            .ToDictionaryAsync(x => x.InventoryItemId, x => x.QuantityBase);
        var pendingAutoQuantities = await _context.KitchenRequestDetails
            .AsNoTracking()
            .Where(x =>
                x.KitchenLocationId == kitchen.Id &&
                x.RequestSource == KitchenRequestSource.Auto &&
                (x.Status == KitchenRequestDetailStatus.PendingManagerReview || x.Status == KitchenRequestDetailStatus.Approved) &&
                inventoryItemIds.Contains(x.InventoryItemId))
            .GroupBy(x => x.InventoryItemId)
            .Select(x => new { InventoryItemId = x.Key, Quantity = x.Sum(y => y.RequestedQuantity - (y.DispatchedQuantity ?? 0)) })
            .ToDictionaryAsync(x => x.InventoryItemId, x => x.Quantity);

        var shortageLines = requiredByItem.Values
            .Select(x =>
            {
                var kitchenAvailable = kitchenStocks.TryGetValue(x.InventoryItemId, out var available) ? available : 0;
                var item = inventoryItems[x.InventoryItemId];
                var minimum = item.MinimumKitchenLevel ?? Math.Max(item.ReorderLevel, x.RequiredQuantity);
                var target = Math.Max(minimum, x.RequiredQuantity);
                var pending = pendingAutoQuantities.TryGetValue(x.InventoryItemId, out var pendingQuantity) ? pendingQuantity : 0;
                var shortage = Math.Max(0, target - kitchenAvailable - pending);
                var stockRoomAvailable = stockRoomStocks.TryGetValue(x.InventoryItemId, out var stockRoomQuantity) ? stockRoomQuantity : 0;
                var requested = stockRoomAvailable > 0 ? Math.Min(shortage, stockRoomAvailable) : shortage;
                return x with
                {
                    RequestedQuantity = requested,
                    CurrentKitchenQuantity = kitchenAvailable,
                    MinimumKitchenLevel = minimum,
                    PendingRequestQuantity = pending,
                    StockRoomAvailableQuantity = stockRoomAvailable
                };
            })
            .Where(x => x.RequestedQuantity > 0)
            .OrderBy(x => x.Name)
            .ToList();

        if (shortageLines.Count == 0)
        {
            return null;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var pendingRequest = await _context.KitchenRequests
            .Include(x => x.Details)
            .FirstOrDefaultAsync(x =>
                x.BranchId == branchId &&
                x.KitchenLocationId == kitchen.Id &&
                x.RequestSource == KitchenRequestSource.Auto &&
                (x.Status == KitchenRequestStatus.PendingManagerReview || x.Status == KitchenRequestStatus.Approved) &&
                x.Note == AutoKitchenRequestNote);

        var created = false;
        var request = pendingRequest;
        if (request is null)
        {
            created = true;
            request = new KitchenRequest
            {
                BranchId = branchId,
                RequestNumber = await GenerateKitchenRequestNumberAsync(),
                Status = KitchenRequestStatus.PendingManagerReview,
                RequestSource = KitchenRequestSource.Auto,
                AutoReason = autoReason,
                KitchenLocationId = kitchen.Id,
                RequestedByUserId = userId,
                CreatedByTerminalId = terminalId > 0 ? terminalId : null,
                CreatedBySessionId = userSessionId > 0 ? userSessionId : null,
                Note = AutoKitchenRequestNote
            };
            _context.KitchenRequests.Add(request);
        }

        foreach (var line in shortageLines)
        {
            var existingDetail = request.Details.FirstOrDefault(x => x.InventoryItemId == line.InventoryItemId);
            if (existingDetail is null)
            {
                request.Details.Add(new KitchenRequestDetail
                {
                    InventoryItemId = line.InventoryItemId,
                    KitchenLocationId = kitchen.Id,
                    RequestSource = KitchenRequestSource.Auto,
                    RequestedQuantity = line.RequestedQuantity,
                    CurrentKitchenQuantityAtRequest = line.CurrentKitchenQuantity,
                    MinimumKitchenLevelAtRequest = line.MinimumKitchenLevel,
                    RecommendedQuantity = line.RequestedQuantity,
                    PendingRequestQuantity = line.PendingRequestQuantity,
                    StockRoomAvailableAtRequest = line.StockRoomAvailableQuantity,
                    Status = KitchenRequestDetailStatus.PendingManagerReview,
                    Note = $"Auto recommendation from POS shortage. Required: {line.RequiredQuantity:0.###} {line.Unit}."
                });
            }
            else if (line.RequestedQuantity > existingDetail.RequestedQuantity)
            {
                existingDetail.RequestedQuantity = line.RequestedQuantity;
                existingDetail.RecommendedQuantity = line.RequestedQuantity;
                existingDetail.CurrentKitchenQuantityAtRequest = line.CurrentKitchenQuantity;
                existingDetail.MinimumKitchenLevelAtRequest = line.MinimumKitchenLevel;
                existingDetail.PendingRequestQuantity = line.PendingRequestQuantity;
                existingDetail.StockRoomAvailableAtRequest = line.StockRoomAvailableQuantity;
                existingDetail.Status = KitchenRequestDetailStatus.PendingManagerReview;
                existingDetail.Note = $"Auto recommendation from POS shortage. Required: {line.RequiredQuantity:0.###} {line.Unit}.";
            }
        }

        if (created || _context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return request;
    }

    private async Task<InventoryLocation> GetOrCreateLocationAsync(int branchId, string name)
    {
        var location = await _context.InventoryLocations.FirstOrDefaultAsync(x => x.BranchId == branchId && x.Name == name);
        if (location is not null)
        {
            return location;
        }

        location = new InventoryLocation { BranchId = branchId, Name = name };
        _context.InventoryLocations.Add(location);
        await _context.SaveChangesAsync();
        return location;
    }

    private async Task<string> GenerateKitchenRequestNumberAsync()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var requestNumber = $"KR-POS-{DateTime.UtcNow:yyyyMMddHHmmss}-{suffix}";
            if (!await _context.KitchenRequests.AnyAsync(x => x.RequestNumber == requestNumber))
            {
                return requestNumber;
            }
        }

        return $"KR-POS-{Guid.NewGuid():N}"[..40];
    }

    private const string AutoKitchenRequestNote = "Auto-generated from POS low stock.";

    private sealed record AutoKitchenRequestLine(
        int InventoryItemId,
        string Name,
        string Unit,
        decimal RequiredQuantity)
    {
        public decimal RequestedQuantity { get; init; }
        public decimal CurrentKitchenQuantity { get; init; }
        public decimal MinimumKitchenLevel { get; init; }
        public decimal PendingRequestQuantity { get; init; }
        public decimal StockRoomAvailableQuantity { get; init; }
    }
}

public class CancelDraftRequest
{
    public int OrderId { get; set; }
}
