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

    public async Task<IActionResult> StockRoom() => View("Stock", await _restaurantInventoryService.GetStockAsync("Stock Room"));

    public async Task<IActionResult> Kitchen() => View("Stock", await _restaurantInventoryService.GetStockAsync("Kitchen"));

    public async Task<IActionResult> LowStock()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var stocks = await _context.InventoryStocks
            .Include(x => x.InventoryItem)
            .Include(x => x.InventoryLocation)
            .Where(x => x.BranchId == branchId && x.Quantity <= x.InventoryItem!.ReorderLevel)
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
}
