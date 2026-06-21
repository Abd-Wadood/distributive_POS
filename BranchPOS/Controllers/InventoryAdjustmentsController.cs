using BranchPOS.DTOs;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Cashier,StockManager,Admin")]
public class InventoryAdjustmentsController : Controller
{
    private readonly IInventoryAdjustmentService _adjustmentService;
    private readonly IBranchContextService _branchContextService;
    private readonly BranchPOS.Data.AppDbContext _context;

    public InventoryAdjustmentsController(
        IInventoryAdjustmentService adjustmentService,
        IBranchContextService branchContextService,
        BranchPOS.Data.AppDbContext context)
    {
        _adjustmentService = adjustmentService;
        _branchContextService = branchContextService;
        _context = context;
    }

    public async Task<IActionResult> Index(
        InventoryLocationType? locationType,
        InventoryAdjustmentType? adjustmentType,
        InventoryAdjustmentStatus? status,
        DateTime? from,
        DateTime? to)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        ViewBag.LocationType = locationType;
        ViewBag.AdjustmentType = adjustmentType;
        ViewBag.Status = status;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.CanReview = CanReviewAdjustments();
        return View(await _adjustmentService.GetAdjustmentsAsync(branchId, locationType, adjustmentType, status, from, to));
    }

    public async Task<IActionResult> Create(
        int? inventoryItemId,
        InventoryLocationType? locationType,
        InventoryAdjustmentType? adjustmentType,
        string? reason)
    {
        await PopulateCreateLookupsAsync();
        return View(new CreateInventoryAdjustmentDto
        {
            InventoryItemId = inventoryItemId ?? 0,
            LocationType = locationType,
            AdjustmentType = adjustmentType,
            Reason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateInventoryAdjustmentDto dto)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        if (!ModelState.IsValid)
        {
            await PopulateCreateLookupsAsync();
            return View(dto);
        }

        try
        {
            await _adjustmentService.CreateAdjustmentAsync(dto, GetUserId(), branchId);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is BranchPOS.Exceptions.BranchPosException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateCreateLookupsAsync();
            return View(dto);
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var adjustment = await _adjustmentService.GetAdjustmentByIdAsync(id, branchId);
        if (adjustment is null)
        {
            return NotFound();
        }

        ViewBag.CanReview = CanReviewAdjustments();
        return View(adjustment);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "StockManager,Admin")]
    public async Task<IActionResult> Approve(ApproveInventoryAdjustmentDto dto)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        await _adjustmentService.ApproveAdjustmentAsync(dto, GetUserId(), branchId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "StockManager,Admin")]
    public async Task<IActionResult> Reject(RejectInventoryAdjustmentDto dto)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        await _adjustmentService.RejectAdjustmentAsync(dto, GetUserId(), branchId);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> WastageSummary(DateTime? from, DateTime? to)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        var adjustments = await _adjustmentService.GetAdjustmentsAsync(branchId, null, null, InventoryAdjustmentStatus.Approved, from, to);
        return View(adjustments);
    }

    private async Task PopulateCreateLookupsAsync()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var inventoryItems = await _context.InventoryItems
            .Where(x => x.BranchId == branchId && x.IsActive && x.IsStockTracked && !x.IsExpenseOnly)
            .OrderBy(x => x.Name)
            .ToListAsync();

        ViewBag.InventoryItems = inventoryItems
            .Select(x => new SelectListItem($"{x.Name} ({x.BaseUnit})", x.Id.ToString()))
            .ToList();
        ViewBag.InventoryItemBaseUnits = inventoryItems.ToDictionary(x => x.Id, x => x.BaseUnit);
    }

    private bool CanReviewAdjustments() =>
        User.IsInRole("StockManager") || User.IsInRole("Admin");

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user was not found.");
}
