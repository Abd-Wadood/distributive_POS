using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        return View(new InventoryItem
        {
            BaseUnit = InventoryUnitCatalog.Gram,
            PurchaseUnitName = "Gram",
            DefaultConversionFactorToBase = 1m,
            ConsumptionMode = ConsumptionMode.ManualKitchenIssue
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryItem item)
    {
        Clean(item);
        ValidateUnitFields(item);
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        item.BranchId = branchId;
        if (ModelState.IsValid)
        {
            await ValidateUniqueInventoryItemAsync(item, branchId);
        }

        if (!ModelState.IsValid)
        {
            PopulateUnitCatalog();
            return View(item);
        }

        _context.InventoryItems.Add(item);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateInventoryItemException(ex))
        {
            _context.Entry(item).State = EntityState.Detached;
            AddDuplicateInventoryItemError(item);
            PopulateUnitCatalog();
            return View(item);
        }

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
        item.BranchId = branchId;
        if (ModelState.IsValid)
        {
            await ValidateUniqueInventoryItemAsync(item, branchId, id);
        }

        if (!ModelState.IsValid)
        {
            PopulateUnitCatalog();
            return View(item);
        }

        var existing = await _context.InventoryItems.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Name = item.Name;
        existing.BaseUnit = item.BaseUnit;
        existing.PurchaseUnitName = string.IsNullOrWhiteSpace(item.PurchaseUnitName) ? null : item.PurchaseUnitName.Trim();
        existing.DefaultConversionFactorToBase = item.DefaultConversionFactorToBase;
        existing.ConsumptionMode = item.ConsumptionMode;
        existing.TrackingLevel = item.TrackingLevel;
        existing.AllowRecipeConsumption = item.AllowRecipeConsumption;
        existing.AllowManualConsumption = item.AllowManualConsumption;
        existing.AllowKitchenDispatch = item.AllowKitchenDispatch;
        existing.RequirePurchaseConversion = item.RequirePurchaseConversion;
        existing.IsStockTracked = item.IsStockTracked;
        existing.IsExpenseOnly = item.IsExpenseOnly;
        existing.ExpiryTrackingRequired = item.ExpiryTrackingRequired;
        existing.BatchTrackingRequired = item.BatchTrackingRequired;
        existing.ReorderLevel = item.ReorderLevel;
        existing.MinimumKitchenLevel = item.MinimumKitchenLevel;
        existing.MaximumKitchenLevel = item.MaximumKitchenLevel;
        existing.IsActive = item.IsActive;
        existing.IsPreparedItem = item.IsPreparedItem;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateInventoryItemException(ex))
        {
            _context.Entry(existing).State = EntityState.Detached;
            AddDuplicateInventoryItemError(item);
            PopulateUnitCatalog();
            return View(item);
        }

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
        item.Name = (item.Name ?? string.Empty).Trim();
        InventoryControlDefaults.ApplyDefaults(item);
        if (item.IsExpenseOnly)
        {
            item.BaseUnit = InventoryUnitCatalog.None;
            item.PurchaseUnitName = null;
            item.DefaultConversionFactorToBase = null;
            return;
        }

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

        if (item.MinimumKitchenLevel.HasValue && item.MinimumKitchenLevel < 0)
        {
            item.MinimumKitchenLevel = 0;
        }

        if (item.MaximumKitchenLevel.HasValue && item.MaximumKitchenLevel < 0)
        {
            item.MaximumKitchenLevel = 0;
        }
    }

    private void ValidateUnitFields(InventoryItem item)
    {
        if (item.IsExpenseOnly)
        {
            return;
        }

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

    private async Task ValidateUniqueInventoryItemAsync(InventoryItem item, int branchId, int? excludingId = null)
    {
        var duplicateExists = await _context.InventoryItems.AsNoTracking().AnyAsync(x =>
            x.BranchId == branchId &&
            x.Name == item.Name &&
            x.BaseUnit == item.BaseUnit &&
            (!excludingId.HasValue || x.Id != excludingId.Value));

        if (duplicateExists)
        {
            AddDuplicateInventoryItemError(item);
        }
    }

    private void AddDuplicateInventoryItemError(InventoryItem item)
    {
        ModelState.AddModelError(nameof(InventoryItem.Name), $"An inventory item named '{item.Name}' already exists with base unit '{item.BaseUnit}'.");
    }

    private static bool IsDuplicateInventoryItemException(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException { ConstraintName: "UX_InventoryItems_BranchId_Name_BaseUnit" };
    }

    private void PopulateUnitCatalog()
    {
        ViewBag.BaseUnits = InventoryUnitCatalog.SupportedBaseUnits;
        ViewBag.UnitOptions = InventoryUnitCatalog.GetOptions();
        ViewBag.ConsumptionModes = Enum.GetValues<ConsumptionMode>();
    }
}
