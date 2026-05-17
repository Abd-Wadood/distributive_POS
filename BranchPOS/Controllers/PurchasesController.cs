using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager")]
public class PurchasesController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPurchaseService _purchaseService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITerminalContextService _terminalContextService;
    private readonly IErrorLoggingService _errorLoggingService;
    private readonly IIdempotencyService _idempotencyService;

    public PurchasesController(AppDbContext context, IPurchaseService purchaseService, IUserSessionService userSessionService, ITerminalContextService terminalContextService, IErrorLoggingService errorLoggingService, IIdempotencyService idempotencyService)
    {
        _context = context;
        _purchaseService = purchaseService;
        _userSessionService = userSessionService;
        _terminalContextService = terminalContextService;
        _errorLoggingService = errorLoggingService;
        _idempotencyService = idempotencyService;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!User.IsInRole("StockManager"))
        {
            context.Result = Forbid();
            return;
        }

        base.OnActionExecuting(context);
    }

    public async Task<IActionResult> Index()
    {
        if (await _userSessionService.GetActiveSessionAsync(GetUserId()) is null)
        {
            return RedirectToAction("Index", "Sessions");
        }
        return View(await _purchaseService.GetPurchasesAsync());
    }

    public async Task<IActionResult> Create()
    {
        var session = await _userSessionService.GetActiveSessionAsync(GetUserId());
        if (session is null)
        {
            return RedirectToAction("Index", "Sessions");
        }

        await EnsureDefaultSupplierAsync();
        return View(await BuildModelAsync(new PurchaseCreateViewModel { IdempotencyKey = Guid.NewGuid().ToString("N") }, session.BranchId));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseCreateViewModel model)
    {
        var session = await _userSessionService.GetActiveSessionAsync(GetUserId());
        if (session is null)
        {
            TempData["Error"] = "Start or continue an active stock session before creating purchases.";
            return RedirectToAction("Index", "Sessions");
        }

        try
        {
            var terminal = await _terminalContextService.RequireCurrentTerminalAsync();
            var dto = new CreatePurchaseDto
            {
                IdempotencyKey = string.IsNullOrWhiteSpace(model.IdempotencyKey) ? _idempotencyService.GetOrCreateKey() : model.IdempotencyKey,
                BranchId = session.BranchId,
                UserSessionId = session.Id,
                PerformedByUserId = GetUserId(),
                TerminalId = terminal.Id,
                TerminalCode = terminal.TerminalCode,
                SupplierId = model.SupplierId,
                InvoiceNumber = model.InvoiceNumber,
                Items = model.Items
                    .Where(x => x.IngredientId > 0 && x.Quantity > 0)
                    .Select(x => new PurchaseItemDto { IngredientId = x.IngredientId, Quantity = x.Quantity, UnitCost = x.UnitCost })
                    .ToList()
            };
            await _purchaseService.CreatePurchaseAsync(dto);
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            var message = ex is BranchPosException branchPosException ? branchPosException.UserMessage : ex.Message;
            _errorLoggingService.LogException(HttpContext, ex, message);
            ModelState.AddModelError(string.Empty, message);
            return View(await BuildModelAsync(model, session.BranchId));
        }
    }

    private async Task<PurchaseCreateViewModel> BuildModelAsync(PurchaseCreateViewModel model, int branchId)
    {
        model.Suppliers = await _context.Suppliers
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.SupplierId))
            .ToListAsync();
        model.Ingredients = await _context.Ingredients
            .Where(x => x.BranchId == branchId)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.UnitType})", x.Id.ToString()))
            .ToListAsync();
        return model;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user was not found.");

    private async Task EnsureDefaultSupplierAsync()
    {
        if (!await _context.Suppliers.AnyAsync())
        {
            _context.Suppliers.Add(new Supplier { Name = "Default Supplier" });
            await _context.SaveChangesAsync();
        }
    }
}
