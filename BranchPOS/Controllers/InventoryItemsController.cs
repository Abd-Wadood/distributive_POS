using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager,Admin")]
public class InventoryItemsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;

    public InventoryItemsController(AppDbContext context, IBranchContextService branchContextService)
    {
        _context = context;
        _branchContextService = branchContextService;
    }

    public async Task<IActionResult> Index()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var items = await _context.InventoryItems.Where(x => x.BranchId == branchId).OrderBy(x => x.Name).ToListAsync();
        return View(items);
    }

    public IActionResult Create() => View(new InventoryItem());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryItem item)
    {
        Clean(item);
        if (!ModelState.IsValid)
        {
            return View(item);
        }

        item.BranchId = await _branchContextService.GetCurrentBranchIdAsync();
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var item = await _context.InventoryItems.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, InventoryItem item)
    {
        if (id != item.Id)
        {
            return BadRequest();
        }

        Clean(item);
        if (!ModelState.IsValid)
        {
            return View(item);
        }

        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var existing = await _context.InventoryItems.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Name = item.Name;
        existing.Unit = item.Unit;
        existing.ReorderLevel = item.ReorderLevel;
        existing.IsActive = item.IsActive;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var item = await _context.InventoryItems.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId);
        if (item is not null)
        {
            item.IsActive = false;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private static void Clean(InventoryItem item)
    {
        item.Name = item.Name.Trim();
        item.Unit = item.Unit.Trim();
        if (item.ReorderLevel < 0)
        {
            item.ReorderLevel = 0;
        }
    }
}
