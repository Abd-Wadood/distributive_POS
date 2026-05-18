using BranchPOS.Data;
using BranchPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin")]
public class SuppliersController : Controller
{
    private readonly AppDbContext _context;

    public SuppliersController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var suppliers = await _context.Suppliers
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return View(suppliers);
    }

    public IActionResult Create()
    {
        return View(new Supplier());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Supplier supplier, CancellationToken cancellationToken)
    {
        supplier.Name = supplier.Name?.Trim() ?? string.Empty;
        supplier.Phone = string.IsNullOrWhiteSpace(supplier.Phone) ? null : supplier.Phone.Trim();

        if (string.IsNullOrWhiteSpace(supplier.Name))
        {
            ModelState.AddModelError(nameof(supplier.Name), "Supplier name is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(supplier);
        }

        var normalizedName = supplier.Name.ToUpperInvariant();
        if (await _context.Suppliers.AnyAsync(x => x.Name.ToUpper() == normalizedName, cancellationToken))
        {
            ModelState.AddModelError(nameof(supplier.Name), "Supplier already exists.");
            return View(supplier);
        }

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["Message"] = "Supplier created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FindAsync([id], cancellationToken);
        return supplier is null ? NotFound() : View(supplier);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Supplier supplier, CancellationToken cancellationToken)
    {
        if (id != supplier.Id)
        {
            return BadRequest();
        }

        supplier.Name = supplier.Name?.Trim() ?? string.Empty;
        supplier.Phone = string.IsNullOrWhiteSpace(supplier.Phone) ? null : supplier.Phone.Trim();

        if (string.IsNullOrWhiteSpace(supplier.Name))
        {
            ModelState.AddModelError(nameof(supplier.Name), "Supplier name is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(supplier);
        }

        var existing = await _context.Suppliers.FindAsync([id], cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var normalizedName = supplier.Name.ToUpperInvariant();
        if (await _context.Suppliers.AnyAsync(x => x.Id != id && x.Name.ToUpper() == normalizedName, cancellationToken))
        {
            ModelState.AddModelError(nameof(supplier.Name), "Supplier already exists.");
            return View(supplier);
        }

        existing.Name = supplier.Name;
        existing.Phone = supplier.Phone;
        await _context.SaveChangesAsync(cancellationToken);

        TempData["Message"] = "Supplier updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (supplier is null)
        {
            return NotFound();
        }

        var hasPurchases = await _context.Purchases.AnyAsync(x => x.SupplierId == id, cancellationToken);
        if (hasPurchases)
        {
            TempData["Error"] = "Supplier is used by purchases and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _context.Suppliers.Remove(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["Message"] = "Supplier deleted.";
        return RedirectToAction(nameof(Index));
    }
}
