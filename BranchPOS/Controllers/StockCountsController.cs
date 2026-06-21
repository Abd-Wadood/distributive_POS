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
public class StockCountsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IStockCountService _stockCountService;

    public StockCountsController(AppDbContext context, IBranchContextService branchContextService, IStockCountService stockCountService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _stockCountService = stockCountService;
    }

    public async Task<IActionResult> Index()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        await PopulateItemsAsync(branchId);
        ViewBag.RecentCounts = await _stockCountService.GetRecentAsync(branchId);
        return View(new CreateStockCountDto { CountDate = DateTime.UtcNow.Date, Reason = "Physical count" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateStockCountDto dto)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        if (!ModelState.IsValid)
        {
            await PopulateItemsAsync(branchId);
            ViewBag.RecentCounts = await _stockCountService.GetRecentAsync(branchId);
            return View("Index", dto);
        }

        try
        {
            await _stockCountService.CreateAsync(dto, GetUserId(), branchId);
            TempData["Message"] = "Stock count saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is BranchPosException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, ex is BranchPosException branchPosException ? branchPosException.UserMessage : ex.Message);
            await PopulateItemsAsync(branchId);
            ViewBag.RecentCounts = await _stockCountService.GetRecentAsync(branchId);
            return View("Index", dto);
        }
    }

    private async Task PopulateItemsAsync(int branchId)
    {
        ViewBag.InventoryItems = await _context.InventoryItems
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.IsActive && x.IsStockTracked && !x.IsExpenseOnly)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.BaseUnit})", x.Id.ToString()))
            .ToListAsync();
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user was not found.");
}
