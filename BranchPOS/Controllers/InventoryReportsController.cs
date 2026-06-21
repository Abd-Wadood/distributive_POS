using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using BranchPOS.ViewModels;
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
        return View("Stock", await _restaurantInventoryService.GetStockAsync("Stock Room"));
    }

    public async Task<IActionResult> Kitchen()
    {
        return View("Stock", await _restaurantInventoryService.GetStockAsync("Kitchen"));
    }

    public async Task<IActionResult> LowStock()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var stocks = await _context.InventoryStocks
            .Include(x => x.InventoryItem)
            .Include(x => x.InventoryLocation)
            .Where(x => x.BranchId == branchId && x.QuantityBase - x.ReservedQuantityBase <= x.InventoryItem!.ReorderLevel)
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
        await Task.CompletedTask;
        return RedirectToAction(nameof(Index));
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
        await Task.CompletedTask;
        return RedirectToAction(nameof(Index));
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
                x.MovementType == InventoryMovementType.ConsumeReservation &&
                x.ReferenceType == nameof(Order))
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync();
        return View(movements);
    }

    public async Task<IActionResult> Reservations()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var activeReservationTotals = await _context.OrderInventoryReservations
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.Status == OrderInventoryReservationStatus.Active)
            .GroupBy(x => x.InventoryStockId)
            .Select(x => new { InventoryStockId = x.Key, Quantity = x.Sum(y => y.RequiredQuantityBase) })
            .ToDictionaryAsync(x => x.InventoryStockId, x => x.Quantity);

        var stockIds = activeReservationTotals.Keys.ToList();
        var stocks = await _context.InventoryStocks
            .AsNoTracking()
            .Include(x => x.InventoryItem)
            .Include(x => x.InventoryLocation)
            .Where(x => x.BranchId == branchId && (x.ReservedQuantityBase > 0 || stockIds.Contains(x.Id)))
            .OrderBy(x => x.InventoryLocation!.Name)
            .ThenBy(x => x.InventoryItem!.Name)
            .ToListAsync();

        var overdueCutoff = DateTime.UtcNow.AddHours(-2);
        var overdueOrders = await _context.Orders
            .AsNoTracking()
            .Include(x => x.Cashier)
            .Where(x =>
                x.BranchId == branchId &&
                x.OrderStatus == OrderStatus.Pending &&
                x.InventoryState == OrderInventoryState.Reserved &&
                x.CreatedAt < overdueCutoff)
            .OrderBy(x => x.CreatedAt)
            .Take(200)
            .Select(x => new OverdueReservedOrderViewModel
            {
                OrderId = x.Id,
                OrderNumber = x.OrderNumber,
                CreatedAt = x.CreatedAt,
                TotalAmount = x.TotalAmount,
                CashierName = x.Cashier == null ? x.CashierId : x.Cashier.Email ?? x.Cashier.UserName ?? x.CashierId
            })
            .ToListAsync();

        return View(new ReservationAuditViewModel
        {
            Rows = stocks.Select(x => new ReservationAuditRowViewModel
            {
                ItemName = x.InventoryItem?.Name ?? "",
                LocationName = x.InventoryLocation?.Name ?? "",
                StockReservedQuantity = x.ReservedQuantityBase,
                ActiveReservationQuantity = activeReservationTotals.GetValueOrDefault(x.Id)
            }).ToList(),
            OverdueOrders = overdueOrders
        });
    }

}
