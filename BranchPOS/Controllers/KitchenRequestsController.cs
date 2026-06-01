using BranchPOS.Data;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager,Admin")]
public class KitchenRequestsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IRestaurantInventoryService _restaurantInventoryService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITerminalContextService _terminalContextService;

    public KitchenRequestsController(
        AppDbContext context,
        IBranchContextService branchContextService,
        IRestaurantInventoryService restaurantInventoryService,
        IUserSessionService userSessionService,
        ITerminalContextService terminalContextService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _restaurantInventoryService = restaurantInventoryService;
        _userSessionService = userSessionService;
        _terminalContextService = terminalContextService;
    }

    public async Task<IActionResult> Index()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var requests = await _context.KitchenRequests
            .Include(x => x.RequestedByUser)
            .Include(x => x.ApprovedByUser)
            .Include(x => x.ReviewedByUser)
            .Include(x => x.DispatchedByUser)
            .Include(x => x.Details)
            .ThenInclude(x => x.InventoryItem)
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        await BackfillMissingManualSnapshotsAsync(branchId, requests);
        return View(requests);
    }

    public async Task<IActionResult> Create() => View(await BuildCreateModelAsync(new KitchenRequest()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KitchenRequest request)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        RemoveServerAssignedCreateFieldsFromModelState();
        request.Details = request.Details.Where(x => x.InventoryItemId > 0 && x.RequestedQuantity > 0).ToList();
        RemoveDetailFieldsFromModelState();
        if (request.Details.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one requested item.");
        }

        var inventoryItemIds = request.Details.Select(x => x.InventoryItemId).Distinct().ToList();
        if (inventoryItemIds.Count > 0)
        {
            var validCount = await _context.InventoryItems.CountAsync(x =>
                x.BranchId == branchId &&
                x.IsActive &&
                x.IsStockTracked &&
                !x.IsExpenseOnly &&
                x.AllowKitchenDispatch &&
                inventoryItemIds.Contains(x.Id));
            if (validCount != inventoryItemIds.Count)
            {
                ModelState.AddModelError(string.Empty, "One or more requested inventory items do not belong to the active branch.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildCreateModelAsync(request));
        }

        request.BranchId = branchId;
        request.RequestSource = KitchenRequestSource.Manual;
        request.AutoReason = KitchenRequestAutoReason.None;
        request.RequestedByUserId = GetUserId();
        request.RequestNumber = $"KR-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        request.Status = KitchenRequestStatus.Pending;
        var kitchen = await GetOrCreateLocationAsync(branchId, "Kitchen");
        request.KitchenLocationId = kitchen.Id;
        foreach (var detail in request.Details)
        {
            detail.RequestSource = KitchenRequestSource.Manual;
            detail.KitchenLocationId = kitchen.Id;
            detail.RecommendedQuantity = detail.RequestedQuantity;
            detail.Status = KitchenRequestDetailStatus.PendingManagerReview;
        }

        await PopulateDetailStockSnapshotsAsync(branchId, kitchen.Id, request.Details);
        _context.KitchenRequests.Add(request);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private void RemoveServerAssignedCreateFieldsFromModelState()
    {
        ModelState.Remove(nameof(KitchenRequest.BranchId));
        ModelState.Remove(nameof(KitchenRequest.RequestNumber));
        ModelState.Remove(nameof(KitchenRequest.Status));
        ModelState.Remove(nameof(KitchenRequest.RequestSource));
        ModelState.Remove(nameof(KitchenRequest.AutoReason));
        ModelState.Remove(nameof(KitchenRequest.KitchenLocationId));
        ModelState.Remove(nameof(KitchenRequest.RequestedByUserId));
        ModelState.Remove(nameof(KitchenRequest.CreatedByTerminalId));
        ModelState.Remove(nameof(KitchenRequest.CreatedBySessionId));
        ModelState.Remove(nameof(KitchenRequest.CreatedAt));

        foreach (var key in ModelState.Keys.Where(x =>
            x.EndsWith($".{nameof(KitchenRequestDetail.KitchenRequestId)}", StringComparison.Ordinal) ||
            x.EndsWith($".{nameof(KitchenRequestDetail.KitchenLocationId)}", StringComparison.Ordinal) ||
            x.EndsWith($".{nameof(KitchenRequestDetail.RequestSource)}", StringComparison.Ordinal) ||
            x.EndsWith($".{nameof(KitchenRequestDetail.RecommendedQuantity)}", StringComparison.Ordinal) ||
            x.EndsWith($".{nameof(KitchenRequestDetail.CurrentKitchenQuantityAtRequest)}", StringComparison.Ordinal) ||
            x.EndsWith($".{nameof(KitchenRequestDetail.MinimumKitchenLevelAtRequest)}", StringComparison.Ordinal) ||
            x.EndsWith($".{nameof(KitchenRequestDetail.PendingRequestQuantity)}", StringComparison.Ordinal) ||
            x.EndsWith($".{nameof(KitchenRequestDetail.StockRoomAvailableAtRequest)}", StringComparison.Ordinal) ||
            x.EndsWith($".{nameof(KitchenRequestDetail.Status)}", StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(key);
        }
    }

    private void RemoveDetailFieldsFromModelState()
    {
        foreach (var key in ModelState.Keys.Where(x => x.StartsWith($"{nameof(KitchenRequest.Details)}[", StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(key);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, Dictionary<int, decimal> approvedQuantities)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var request = await _context.KitchenRequests.Include(x => x.Details).FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId);
        if (request is null)
        {
            return NotFound();
        }

        if (request.Status is not KitchenRequestStatus.Pending and not KitchenRequestStatus.PendingManagerReview)
        {
            TempData["Error"] = "Only pending requests can be approved.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var detail in request.Details)
        {
            var approved = approvedQuantities.TryGetValue(detail.Id, out var value) ? value : detail.RequestedQuantity;
            if (approved < 0)
            {
                TempData["Error"] = "Approved quantity cannot be negative.";
                return RedirectToAction(nameof(Index));
            }

            detail.ApprovedQuantity = approved;
            detail.Status = approved > 0 ? KitchenRequestDetailStatus.Approved : KitchenRequestDetailStatus.Rejected;
        }

        request.Status = KitchenRequestStatus.Approved;
        request.ApprovedByUserId = GetUserId();
        request.ReviewedByUserId = request.ApprovedByUserId;
        request.ApprovedAt = DateTime.UtcNow;
        request.ReviewedAt = request.ApprovedAt;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var request = await _context.KitchenRequests.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId);
        if (request is not null && request.Status is KitchenRequestStatus.Pending or KitchenRequestStatus.PendingManagerReview or KitchenRequestStatus.Approved)
        {
            request.Status = KitchenRequestStatus.Rejected;
            request.ApprovedByUserId = GetUserId();
            request.ReviewedByUserId = request.ApprovedByUserId;
            request.ApprovedAt = DateTime.UtcNow;
            request.ReviewedAt = request.ApprovedAt;
            foreach (var detail in await _context.KitchenRequestDetails.Where(x => x.KitchenRequestId == id).ToListAsync())
            {
                detail.Status = KitchenRequestDetailStatus.Rejected;
            }
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispatch(int id, Dictionary<int, decimal> quantitiesToSend, string? managerNotes)
    {
        try
        {
            var userId = GetUserId();
            var session = await _userSessionService.GetActiveSessionAsync(userId)
                ?? throw new BusinessException("Start or continue an active stock session before dispatching kitchen requests.");
            var terminal = await _terminalContextService.RequireCurrentTerminalFreshAsync();
            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                idempotencyKey = $"dispatch-{id}-{terminal.Id}-{session.Id}";
            }

            var quantitiesToSendBase = await ConvertDispatchQuantitiesToBaseAsync(id, quantitiesToSend);
            await _restaurantInventoryService.DispatchKitchenRequestAsync(id, userId, quantitiesToSendBase, managerNotes, session.Id, terminal.Id, idempotencyKey);
            TempData["Message"] = "Kitchen request dispatched.";
        }
        catch (BranchPosException ex)
        {
            TempData["Error"] = ex.UserMessage;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<Dictionary<int, decimal>> ConvertDispatchQuantitiesToBaseAsync(int requestId, Dictionary<int, decimal> quantitiesToSend)
    {
        if (quantitiesToSend.Count == 0)
        {
            return quantitiesToSend;
        }

        var details = await _context.KitchenRequestDetails
            .Include(x => x.InventoryItem)
            .Where(x => x.KitchenRequestId == requestId && quantitiesToSend.Keys.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var converted = new Dictionary<int, decimal>();
        foreach (var quantity in quantitiesToSend)
        {
            if (!details.TryGetValue(quantity.Key, out var detail) || detail.InventoryItem is null)
            {
                continue;
            }

            var factor = detail.InventoryItem.DefaultConversionFactorToBase.GetValueOrDefault(1m);
            if (factor <= 0)
            {
                factor = 1m;
            }

            converted[quantity.Key] = quantity.Value * factor;
        }

        return converted;
    }

    private async Task<KitchenRequest> BuildCreateModelAsync(KitchenRequest request)
    {
        ViewBag.InventoryItems = await GetInventoryItemsAsync();
        while (request.Details.Count < 5)
        {
            request.Details.Add(new KitchenRequestDetail());
        }
        return request;
    }

    private async Task<List<SelectListItem>> GetInventoryItemsAsync()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        return await _context.InventoryItems
            .Where(x => x.BranchId == branchId && x.IsActive && x.IsStockTracked && !x.IsExpenseOnly && x.AllowKitchenDispatch)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.BaseUnit})", x.Id.ToString()))
            .ToListAsync();
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

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user was not found.");

    private async Task BackfillMissingManualSnapshotsAsync(int branchId, List<KitchenRequest> requests)
    {
        var details = requests
            .Where(x => x.RequestSource == KitchenRequestSource.Manual &&
                x.Status is KitchenRequestStatus.Pending or KitchenRequestStatus.PendingManagerReview or KitchenRequestStatus.Approved)
            .SelectMany(x => x.Details)
            .Where(x =>
                x.StockRoomAvailableAtRequest == 0 &&
                x.CurrentKitchenQuantityAtRequest == 0 &&
                x.MinimumKitchenLevelAtRequest == 0)
            .ToList();

        if (details.Count == 0)
        {
            return;
        }

        var kitchen = await GetOrCreateLocationAsync(branchId, "Kitchen");
        await PopulateDetailStockSnapshotsAsync(branchId, kitchen.Id, details);
        await _context.SaveChangesAsync();
    }

    private async Task PopulateDetailStockSnapshotsAsync(int branchId, int kitchenLocationId, IReadOnlyCollection<KitchenRequestDetail> details)
    {
        if (details.Count == 0)
        {
            return;
        }

        var stockRoom = await GetOrCreateLocationAsync(branchId, "Stock Room");
        var inventoryItemIds = details.Select(x => x.InventoryItemId).Distinct().ToList();
        var inventoryItems = await _context.InventoryItems
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && inventoryItemIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        var kitchenStocks = await _context.InventoryStocks
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.InventoryLocationId == kitchenLocationId && inventoryItemIds.Contains(x.InventoryItemId))
            .ToDictionaryAsync(x => x.InventoryItemId, x => x.QuantityBase);
        var stockRoomStocks = await _context.InventoryStocks
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.InventoryLocationId == stockRoom.Id && inventoryItemIds.Contains(x.InventoryItemId))
            .ToDictionaryAsync(x => x.InventoryItemId, x => x.QuantityBase);

        foreach (var detail in details)
        {
            if (!inventoryItems.TryGetValue(detail.InventoryItemId, out var item))
            {
                continue;
            }

            detail.CurrentKitchenQuantityAtRequest = kitchenStocks.GetValueOrDefault(detail.InventoryItemId);
            detail.MinimumKitchenLevelAtRequest = item.MinimumKitchenLevel ?? item.ReorderLevel;
            detail.StockRoomAvailableAtRequest = stockRoomStocks.GetValueOrDefault(detail.InventoryItemId);
        }
    }
}
