using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager,Admin")]
public class InventoryReportsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IRestaurantInventoryService _restaurantInventoryService;

    public InventoryReportsController(AppDbContext context, IBranchContextService branchContextService, IRestaurantInventoryService restaurantInventoryService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _restaurantInventoryService = restaurantInventoryService;
    }

    public IActionResult Index() => View();

    public async Task<IActionResult> StockRoom()
    {
        await PopulatePreparedRecipeOutputQuantitiesAsync();
        return View("Stock", await _restaurantInventoryService.GetStockAsync("Stock Room"));
    }

    public async Task<IActionResult> Kitchen()
    {
        await PopulatePreparedRecipeOutputQuantitiesAsync();
        return View("Stock", await _restaurantInventoryService.GetStockAsync("Kitchen"));
    }

    public async Task<IActionResult> LowStock()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var stocks = await _context.InventoryStocks
            .Include(x => x.InventoryItem)
            .Include(x => x.InventoryLocation)
            .Where(x => x.BranchId == branchId && x.QuantityBase <= x.InventoryItem!.ReorderLevel)
            .OrderBy(x => x.InventoryLocation!.Name)
            .ThenBy(x => x.InventoryItem!.Name)
            .ToListAsync();
        return View(stocks);
    }

    public async Task<IActionResult> Movements()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var movements = await _context.InventoryMovements
            .Include(x => x.InventoryItem)
            .Include(x => x.FromLocation)
            .Include(x => x.ToLocation)
            .Include(x => x.CreatedByUser)
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync();
        return View(movements);
    }

    public async Task<IActionResult> KitchenRequests() => RedirectToAction("Index", "KitchenRequests");

    public async Task<IActionResult> Profit(DateTime? from, DateTime? to) => View(await _restaurantInventoryService.BuildProfitReportAsync(from, to));

    public async Task<IActionResult> ControlModes()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var items = await _context.InventoryItems
            .AsNoTracking()
            .Where(x => x.BranchId == branchId)
            .OrderBy(x => x.ConsumptionMode)
            .ThenBy(x => x.Name)
            .ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> ManualUsage()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var usages = await _context.ManualKitchenUsages
            .AsNoTracking()
            .Include(x => x.InventoryItem)
            .Include(x => x.CreatedByUser)
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.UsageDate)
            .ThenByDescending(x => x.Id)
            .Take(500)
            .ToListAsync();
        return View(usages);
    }

    public async Task<IActionResult> PeriodicCountVariance()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var counts = await _context.StockCounts
            .AsNoTracking()
            .Include(x => x.InventoryItem)
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.CountDate)
            .ThenByDescending(x => x.Id)
            .Take(500)
            .ToListAsync();
        return View(counts);
    }

    public async Task<IActionResult> ExpenseOnlyPurchases()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var lines = await _context.PurchaseItems
            .AsNoTracking()
            .Include(x => x.Purchase)
            .Include(x => x.InventoryItem)
            .Where(x => x.BranchId == branchId && x.IsExpenseOnly)
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync();
        return View(lines);
    }

    public async Task<IActionResult> RecipeConsumption()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var movements = await _context.InventoryMovements
            .AsNoTracking()
            .Include(x => x.InventoryItem)
            .Include(x => x.FromLocation)
            .Where(x =>
                x.BranchId == branchId &&
                x.MovementType == InventoryMovementType.Consumption &&
                x.ReferenceType == nameof(Order))
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync();
        return View(movements);
    }

    private async Task PopulatePreparedRecipeOutputQuantitiesAsync()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        ViewBag.PreparedRecipeOutputQuantities = await _context.PreparationRecipes
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.IsActive && x.OutputQuantityBase > 0)
            .GroupBy(x => x.OutputInventoryItemId)
            .ToDictionaryAsync(x => x.Key, x => x.First().OutputQuantityBase);
    }
}
