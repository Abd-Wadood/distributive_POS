using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin,StockManager")]
public class IngredientsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;

    public IngredientsController(AppDbContext context, IBranchContextService branchContextService)
    {
        _context = context;
        _branchContextService = branchContextService;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (User.IsInRole("Cashier") && !User.IsInRole("Admin"))
        {
            context.Result = Forbid();
            return;
        }

        base.OnActionExecuting(context);
    }

    public async Task<IActionResult> Index()
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var ingredients = await _context.Ingredients.Where(x => x.BranchId == branchId).Include(x => x.Inventory).OrderBy(x => x.Name).ToListAsync();
        return View(ingredients);
    }

    public IActionResult Create() => View(new Ingredient());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Ingredient ingredient)
    {
        if (!ModelState.IsValid)
        {
            return View(ingredient);
        }

        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        ingredient.BranchId = branchId;
        _context.Ingredients.Add(ingredient);
        ingredient.Inventory = new Inventory { BranchId = branchId, CurrentQuantity = 0 };
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var ingredient = await _context.Ingredients.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId);
        return ingredient is null ? NotFound() : View(ingredient);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Ingredient ingredient)
    {
        if (id != ingredient.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(ingredient);
        }

        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var existing = await _context.Ingredients.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Name = ingredient.Name;
        existing.UnitType = ingredient.UnitType;
        existing.MinimumStockLevel = ingredient.MinimumStockLevel;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
