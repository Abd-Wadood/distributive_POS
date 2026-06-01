using System.Security.Claims;
using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager,Admin")]
public class ManualKitchenUsageController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IManualKitchenUsageService _manualKitchenUsageService;
    private readonly IUserSessionService _userSessionService;

    public ManualKitchenUsageController(
        AppDbContext context,
        IBranchContextService branchContextService,
        IManualKitchenUsageService manualKitchenUsageService,
        IUserSessionService userSessionService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _manualKitchenUsageService = manualKitchenUsageService;
        _userSessionService = userSessionService;
    }

    public async Task<IActionResult> Index()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        await PopulateItemsAsync(branchId);
        ViewBag.RecentUsages = await _manualKitchenUsageService.GetRecentAsync(branchId);
        return View(new CreateManualKitchenUsageDto { UsageDate = DateTime.UtcNow.Date });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateManualKitchenUsageDto dto)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        if (!ModelState.IsValid)
        {
            await PopulateItemsAsync(branchId);
            ViewBag.RecentUsages = await _manualKitchenUsageService.GetRecentAsync(branchId);
            return View("Index", dto);
        }

        try
        {
            var userId = GetUserId();
            var session = await _userSessionService.GetActiveSessionAsync(userId);
            dto.UserSessionId = session?.Id;
            await _manualKitchenUsageService.CreateAsync(dto, userId, branchId);
            TempData["Message"] = "Manual kitchen usage recorded.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is BranchPosException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, ex is BranchPosException branchPosException ? branchPosException.UserMessage : ex.Message);
            await PopulateItemsAsync(branchId);
            ViewBag.RecentUsages = await _manualKitchenUsageService.GetRecentAsync(branchId);
            return View("Index", dto);
        }
    }

    private async Task PopulateItemsAsync(int branchId)
    {
        ViewBag.InventoryItems = await _context.InventoryItems
            .AsNoTracking()
            .Where(x =>
                x.BranchId == branchId &&
                x.IsActive &&
                x.IsStockTracked &&
                !x.IsExpenseOnly &&
                x.AllowManualConsumption &&
                x.ConsumptionMode == ConsumptionMode.ManualKitchenIssue)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.BaseUnit})", x.Id.ToString()))
            .ToListAsync();
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user was not found.");
}
