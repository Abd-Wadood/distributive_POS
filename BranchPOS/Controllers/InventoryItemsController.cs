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

    public IActionResult Create()
    {
        PopulateUnitCatalog();
        return View(new InventoryItem { BaseUnit = InventoryUnitCatalog.Gram, PurchaseUnitName = "Gram", DefaultConversionFactorToBase = 1m });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryItem item)
    {
        Clean(item);
        ValidateUnitFields(item);
        if (!ModelState.IsValid)
        {
            PopulateUnitCatalog();
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
        if (item is null)
        {
            return NotFound();
        }

        PopulateUnitCatalog();
        return View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, InventoryItem item)
    {
        if (id != item.Id)
        {
            return BadRequest();
        }

        Clean(item);
        ValidateUnitFields(item);
        if (!ModelState.IsValid)
        {
            PopulateUnitCatalog();
            return View(item);
        }

        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var existing = await _context.InventoryItems.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Name = item.Name;
        existing.BaseUnit = item.BaseUnit;
        existing.PurchaseUnitName = string.IsNullOrWhiteSpace(item.PurchaseUnitName) ? null : item.PurchaseUnitName.Trim();
        existing.DefaultConversionFactorToBase = item.DefaultConversionFactorToBase;
        existing.ReorderLevel = item.ReorderLevel;
        existing.IsActive = item.IsActive;
        existing.IsPreparedItem = item.IsPreparedItem;
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
        item.BaseUnit = InventoryUnitCatalog.NormalizeBaseUnit(item.BaseUnit);
        item.PurchaseUnitName = string.IsNullOrWhiteSpace(item.PurchaseUnitName) ? null : item.PurchaseUnitName.Trim();
        if (item.IsPreparedItem &&
            item.BaseUnit == InventoryUnitCatalog.Piece &&
            (string.IsNullOrWhiteSpace(item.PurchaseUnitName) || item.PurchaseUnitName == InventoryUnitCatalog.Piece) &&
            (!item.DefaultConversionFactorToBase.HasValue || item.DefaultConversionFactorToBase <= 0))
        {
            item.PurchaseUnitName = InventoryUnitCatalog.Piece;
            item.DefaultConversionFactorToBase = 1m;
        }

        if (item.ReorderLevel < 0)
        {
            item.ReorderLevel = 0;
        }
    }

    private void ValidateUnitFields(InventoryItem item)
    {
        try
        {
            item.DefaultConversionFactorToBase = InventoryUnitCatalog.ValidateAndNormalize(item.BaseUnit, item.PurchaseUnitName, item.DefaultConversionFactorToBase);
            item.PurchaseUnitName = InventoryUnitCatalog.FindOption(item.BaseUnit, item.PurchaseUnitName)?.DisplayName;
        }
        catch (Exceptions.PosValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.UserMessage);
        }
    }

    private void PopulateUnitCatalog()
    {
        ViewBag.BaseUnits = InventoryUnitCatalog.SupportedBaseUnits;
        ViewBag.UnitOptions = InventoryUnitCatalog.GetOptions();
    }
}
