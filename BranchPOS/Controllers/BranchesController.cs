using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin")]
public class BranchesController : Controller
{
    private readonly AppDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public BranchesController(AppDbContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
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

        CleanBranchFields(branch);

        if (await _context.Branches.AnyAsync(x => x.BranchCode == branch.BranchCode))
        {
            ModelState.AddModelError(nameof(branch.BranchCode), "Branch code already exists.");
            return View(branch);
        }

        _context.Branches.Add(branch);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex) when (DatabaseErrorTranslator.IsUniqueViolation(ex))
        {
            ModelState.AddModelError(nameof(branch.BranchCode), DatabaseErrorTranslator.ToUserException(ex, "Branch code already exists.").UserMessage);
            return View(branch);
        }

        await _auditLogService.LogAsync("BranchCreated", nameof(Branch), branch.Id.ToString(), null,
            new { branch.BranchCode, branch.Name, branch.Address, branch.Phone, branch.IsActive },
            branch.Id);
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

        CleanBranchFields(branch);
        var normalizedCode = branch.BranchCode;
        if (await _context.Branches.AnyAsync(x => x.Id != id && x.BranchCode == normalizedCode))
        {
            ModelState.AddModelError(nameof(branch.BranchCode), "Branch code already exists.");
            return View(branch);
        }

        var oldValues = new { existing.BranchCode, existing.Name, existing.Address, existing.Phone, existing.IsActive };
        existing.BranchCode = normalizedCode;
        existing.Name = branch.Name;
        existing.Address = branch.Address;
        existing.Phone = branch.Phone;
        existing.IsActive = branch.IsActive;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex) when (DatabaseErrorTranslator.IsUniqueViolation(ex))
        {
            ModelState.AddModelError(nameof(branch.BranchCode), DatabaseErrorTranslator.ToUserException(ex, "Branch code already exists.").UserMessage);
            return View(branch);
        }

        await _auditLogService.LogAsync("BranchUpdated", nameof(Branch), existing.Id.ToString(), oldValues,
            new { existing.BranchCode, existing.Name, existing.Address, existing.Phone, existing.IsActive },
            existing.Id);
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

        var oldValues = new { branch.IsActive };
        branch.IsActive = !branch.IsActive;
        await _context.SaveChangesAsync();
        await _auditLogService.LogAsync("BranchToggled", nameof(Branch), branch.Id.ToString(), oldValues,
            new { branch.IsActive }, branch.Id);
        return RedirectToAction(nameof(Index));
    }

    public static void CleanBranchFields(Branch branch)
    {
        branch.BranchCode = branch.BranchCode.Trim().ToUpperInvariant();
        branch.Name = branch.Name.Trim();
        branch.Address = string.IsNullOrWhiteSpace(branch.Address) ? null : branch.Address.Trim();
        branch.Phone = string.IsNullOrWhiteSpace(branch.Phone) ? null : branch.Phone.Trim();
    }
}
