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

    public KitchenRequestsController(AppDbContext context, IBranchContextService branchContextService, IRestaurantInventoryService restaurantInventoryService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _restaurantInventoryService = restaurantInventoryService;
    }

    public async Task<IActionResult> Index()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var requests = await _context.KitchenRequests
            .Include(x => x.RequestedByUser)
            .Include(x => x.ApprovedByUser)
            .Include(x => x.Details)
            .ThenInclude(x => x.InventoryItem)
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return View(requests);
    }

    public async Task<IActionResult> Create() => View(await BuildCreateModelAsync(new KitchenRequest()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KitchenRequest request)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        request.Details = request.Details.Where(x => x.InventoryItemId > 0 && x.RequestedQuantity > 0).ToList();
        if (request.Details.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one requested item.");
        }

        var inventoryItemIds = request.Details.Select(x => x.InventoryItemId).Distinct().ToList();
        if (inventoryItemIds.Count > 0)
        {
            var validCount = await _context.InventoryItems.CountAsync(x => x.BranchId == branchId && x.IsActive && inventoryItemIds.Contains(x.Id));
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
        request.RequestedByUserId = GetUserId();
        request.RequestNumber = $"KR-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        request.Status = KitchenRequestStatus.Pending;
        _context.KitchenRequests.Add(request);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
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

        if (request.Status != KitchenRequestStatus.Pending)
        {
            TempData["Error"] = "Only pending requests can be approved.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var detail in request.Details)
        {
            var approved = approvedQuantities.TryGetValue(detail.Id, out var value) ? value : detail.RequestedQuantity;
            if (approved < 0 || approved > detail.RequestedQuantity)
            {
                TempData["Error"] = "Approved quantity must be between zero and the requested quantity.";
                return RedirectToAction(nameof(Index));
            }

            detail.ApprovedQuantity = approved;
        }

        request.Status = KitchenRequestStatus.Approved;
        request.ApprovedByUserId = GetUserId();
        request.ApprovedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var request = await _context.KitchenRequests.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId);
        if (request is not null && request.Status == KitchenRequestStatus.Pending)
        {
            request.Status = KitchenRequestStatus.Rejected;
            request.ApprovedByUserId = GetUserId();
            request.ApprovedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispatch(int id)
    {
        try
        {
            await _restaurantInventoryService.DispatchKitchenRequestAsync(id, GetUserId());
            TempData["Message"] = "Kitchen request dispatched.";
        }
        catch (BranchPosException ex)
        {
            TempData["Error"] = ex.UserMessage;
        }

        return RedirectToAction(nameof(Index));
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
            .Where(x => x.BranchId == branchId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.Unit})", x.Id.ToString()))
            .ToListAsync();
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user was not found.");
}
