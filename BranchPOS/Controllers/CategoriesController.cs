using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin")]
public class CategoriesController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPosMenuCacheInvalidator _posMenuCacheInvalidator;

    public CategoriesController(AppDbContext context, IPosMenuCacheInvalidator posMenuCacheInvalidator)
    {
        _context = context;
        _posMenuCacheInvalidator = posMenuCacheInvalidator;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _context.Categories.OrderBy(x => x.Name).ToListAsync();
        return View(categories);
    }

    public IActionResult Create()
    {
        return View(new Category());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        if (!ModelState.IsValid)
        {
            return View(category);
        }

        category.Name = category.Name.Trim();
        if (await _context.Categories.AnyAsync(x => x.Name == category.Name))
        {
            ModelState.AddModelError(nameof(category.Name), "Category already exists.");
            return View(category);
        }

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        _posMenuCacheInvalidator.Invalidate();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        return category is null ? NotFound() : View(category);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category category)
    {
        if (id != category.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(category);
        }

        var existing = await _context.Categories.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        var name = category.Name.Trim();
        if (await _context.Categories.AnyAsync(x => x.Id != id && x.Name == name))
        {
            ModelState.AddModelError(nameof(category.Name), "Category already exists.");
            return View(category);
        }

        existing.Name = name;
        await _context.SaveChangesAsync();
        _posMenuCacheInvalidator.Invalidate();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id);
        if (category is null)
        {
            return NotFound();
        }

        var hasProducts = await _context.Products.AnyAsync(x => x.CategoryId == id);
        if (hasProducts)
        {
            TempData["Error"] = "Category is used by products and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        _posMenuCacheInvalidator.Invalidate();
        TempData["Message"] = "Category deleted.";
        return RedirectToAction(nameof(Index));
    }
}
