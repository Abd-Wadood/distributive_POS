using BranchPOS.Exceptions;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin")]
public class TerminalsController : Controller
{
    private readonly ITerminalService _terminalService;
    private readonly IErrorLoggingService _errorLoggingService;

    public TerminalsController(ITerminalService terminalService, IErrorLoggingService errorLoggingService)
    {
        _terminalService = terminalService;
        _errorLoggingService = errorLoggingService;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _terminalService.BuildAdminModelAsync(new TerminalCreateViewModel(), User.GetUserId()));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "NewTerminal")] TerminalCreateViewModel model)
    {
        try
        {
            await _terminalService.CreateAsync(model, User.GetUserId());
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ToUserMessage(ex));
            return View("Index", await _terminalService.BuildAdminModelAsync(model, User.GetUserId()));
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            return View(await _terminalService.BuildEditModelAsync(id, User.GetUserId()));
        }
        catch (PosNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TerminalEditViewModel model)
    {
        try
        {
            await _terminalService.UpdateAsync(id, model, User.GetUserId());
            return RedirectToAction(nameof(Index));
        }
        catch (PosNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ToUserMessage(ex));
            return View(await _terminalService.BuildEditModelAsync(model.Id, User.GetUserId()));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        try
        {
            await _terminalService.ToggleAsync(id, User.GetUserId());
            return RedirectToAction(nameof(Index));
        }
        catch (PosNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ToUserMessage(ex);
            return RedirectToAction(nameof(Index));
        }
    }

    private string ToUserMessage(InvalidOperationException ex)
    {
        var message = ex is BranchPosException branchPosException ? branchPosException.UserMessage : ex.Message;
        _errorLoggingService.LogException(HttpContext, ex, message);
        return message;
    }
}

internal static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? throw new InvalidOperationException("Authenticated user was not found.");
}
