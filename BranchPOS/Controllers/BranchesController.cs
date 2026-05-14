using BranchPOS.Data;
using BranchPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin")]
public class BranchesController : Controller
{
    private readonly AppDbContext _context;

    public BranchesController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var branches = await _context.Branches.OrderBy(x => x.Name).ToListAsync();
        return View(branches);
    }

    public IActionResult Create()
    {
        return View(new Branch { IsActive = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Branch branch)
    {
        if (!ModelState.IsValid)
        {
            return View(branch);
        }

        branch.BranchCode = branch.BranchCode.Trim().ToUpperInvariant();
        branch.Name = branch.Name.Trim();

        if (await _context.Branches.AnyAsync(x => x.BranchCode == branch.BranchCode))
        {
            ModelState.AddModelError(nameof(branch.BranchCode), "Branch code already exists.");
            return View(branch);
        }

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var branch = await _context.Branches.FindAsync(id);
        return branch is null ? NotFound() : View(branch);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Branch branch)
    {
        if (id != branch.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(branch);
        }

        var existing = await _context.Branches.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        var normalizedCode = branch.BranchCode.Trim().ToUpperInvariant();
        if (await _context.Branches.AnyAsync(x => x.Id != id && x.BranchCode == normalizedCode))
        {
            ModelState.AddModelError(nameof(branch.BranchCode), "Branch code already exists.");
            return View(branch);
        }

        existing.BranchCode = normalizedCode;
        existing.Name = branch.Name.Trim();
        existing.Address = string.IsNullOrWhiteSpace(branch.Address) ? null : branch.Address.Trim();
        existing.Phone = string.IsNullOrWhiteSpace(branch.Phone) ? null : branch.Phone.Trim();
        existing.IsActive = branch.IsActive;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var branch = await _context.Branches.FindAsync(id);
        if (branch is null)
        {
            return NotFound();
        }

        branch.IsActive = !branch.IsActive;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
