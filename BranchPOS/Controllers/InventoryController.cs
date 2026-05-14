using BranchPOS.DTOs;
using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin,StockManager")]
public class InventoryController : Controller
{
    private readonly IInventoryService _inventoryService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITerminalContextService _terminalContextService;

    public InventoryController(IInventoryService inventoryService, IUserSessionService userSessionService, ITerminalContextService terminalContextService)
    {
        _inventoryService = inventoryService;
        _userSessionService = userSessionService;
        _terminalContextService = terminalContextService;
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
        if (await _userSessionService.GetActiveSessionAsync(GetUserId()) is null)
        {
            return RedirectToAction("Index", "Sessions");
        }
        return View(await _inventoryService.GetInventoryAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(InventoryAdjustmentDto dto)
    {
        try
        {
            var session = await _userSessionService.GetActiveSessionAsync(GetUserId());
            if (session is null)
            {
                throw new InvalidOperationException("Start or continue a stock session before adjusting inventory.");
            }

            dto.BranchId = session.BranchId;
            dto.UserSessionId = session.Id;
            dto.PerformedByUserId = GetUserId();
            var terminal = await _terminalContextService.RequireCurrentTerminalAsync();
            dto.TerminalId = terminal.Id;
            dto.TerminalCode = terminal.TerminalCode;
            await _inventoryService.AdjustInventoryAsync(dto);
            TempData["Message"] = "Inventory adjusted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user was not found.");
}
