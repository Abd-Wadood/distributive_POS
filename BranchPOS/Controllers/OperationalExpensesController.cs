using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager,Admin")]
public class OperationalExpensesController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;

    public OperationalExpensesController(AppDbContext context, IBranchContextService branchContextService)
    {
        _context = context;
        _branchContextService = branchContextService;
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var expenses = _context.OperationalExpenses.Include(x => x.ExpenseCategory).Where(x => x.BranchId == branchId);
        if (from.HasValue)
        {
            expenses = expenses.Where(x => x.ExpenseDate >= from.Value.ToUniversalTime().Date);
        }
        if (to.HasValue)
        {
            expenses = expenses.Where(x => x.ExpenseDate < to.Value.ToUniversalTime().Date);
        }

        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(await expenses.OrderByDescending(x => x.ExpenseDate).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        await PopulateCategoriesAsync(branchId);
        return View(new OperationalExpense { ExpenseDate = DateTime.UtcNow.Date });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OperationalExpense expense)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        if (expense.Amount < 0)
        {
            ModelState.AddModelError(nameof(expense.Amount), "Amount cannot be negative.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(branchId);
            return View(expense);
        }

        expense.BranchId = branchId;
        expense.CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        expense.Description = string.IsNullOrWhiteSpace(expense.Description) ? null : expense.Description.Trim();
        _context.OperationalExpenses.Add(expense);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Categories()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        return View(await _context.ExpenseCategories.Where(x => x.BranchId == branchId).OrderBy(x => x.Name).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(ExpenseCategory category)
    {
        category.Name = category.Name.Trim();
        if (!string.IsNullOrWhiteSpace(category.Name))
        {
            category.BranchId = await _branchContextService.GetCurrentBranchIdAsync();
            _context.ExpenseCategories.Add(category);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Categories));
    }

    private async Task PopulateCategoriesAsync(int branchId)
    {
        ViewBag.Categories = await _context.ExpenseCategories
            .Where(x => x.BranchId == branchId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
    }
}
