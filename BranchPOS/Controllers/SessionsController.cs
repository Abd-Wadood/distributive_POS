using System.Security.Claims;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BranchPOS.Controllers;

[Authorize]
public class SessionsController : Controller
{
    private readonly IBranchService _branchService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITerminalContextService _terminalContextService;
    private readonly IErrorLoggingService _errorLoggingService;

    public SessionsController(IBranchService branchService, IUserSessionService userSessionService, ITerminalContextService terminalContextService, IErrorLoggingService errorLoggingService)
    {
        _branchService = branchService;
        _userSessionService = userSessionService;
        _terminalContextService = terminalContextService;
        _errorLoggingService = errorLoggingService;
    }

    [Authorize(Roles = "Cashier,StockManager")]
    public async Task<IActionResult> Index()
    {
        await _userSessionService.MarkInterruptedSessionsAsync();
        var userId = GetUserId();
        var branches = await _branchService.GetBranchesForUserAsync(userId);
        var active = await _userSessionService.GetActiveSessionAsync(userId);
        var interrupted = await _userSessionService.GetInterruptedSessionAsync(userId);
        var role = User.IsInRole("Cashier") ? "Cashier" : User.IsInRole("StockManager") ? "StockManager" : "Admin";
        var terminal = await _terminalContextService.RequireCurrentTerminalAsync();
        var canAccessTerminalBranch = branches.Any(x => x.Id == terminal.BranchId);

        return View(new SessionStartViewModel
        {
            ActiveSession = active,
            InterruptedSession = interrupted,
            RoleName = role,
            BranchId = active?.BranchId ?? interrupted?.BranchId ?? terminal.BranchId,
            TerminalName = terminal.Name,
            TerminalCode = terminal.TerminalCode,
            TerminalBranchName = terminal.Branch?.Name ?? "",
            CanStartSession = canAccessTerminalBranch,
            StartSessionBlockReason = canAccessTerminalBranch
                ? null
                : $"This browser is registered as terminal {terminal.TerminalCode} for branch {terminal.Branch?.Name}, but this user is not assigned to that branch.",
            Branches = branches.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Cashier,StockManager")]
    public async Task<IActionResult> Start(SessionStartViewModel model)
    {
        var userId = GetUserId();
        var activeSession = await _userSessionService.GetActiveSessionAsync(userId);
        if (activeSession is not null)
        {
            TempData["Error"] = $"You already have an active session ({activeSession.SessionCode}). Continue or end it before starting a new session.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var terminal = await _terminalContextService.RequireCurrentTerminalAsync();
            await _userSessionService.StartSessionAsync(new StartSessionDto
            {
                UserId = userId,
                BranchId = terminal.BranchId,
                RoleName = model.RoleName,
                TerminalId = terminal.Id,
                TerminalCode = terminal.TerminalCode,
                TerminalName = terminal.Name,
                Notes = model.Notes
            });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ToUserMessage(ex);
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException ex)
        {
            _errorLoggingService.LogException(HttpContext, ex, "You do not have permission to start a session for this branch.");
            TempData["Error"] = "You do not have permission to start a session for this branch.";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Cashier,StockManager")]
    public async Task<IActionResult> Continue(int sessionId)
    {
        try
        {
            await _terminalContextService.RequireCurrentTerminalAsync();
            await _userSessionService.ContinueSessionAsync(sessionId, GetUserId());
            return RedirectToAction("Index", "Home");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ToUserMessage(ex);
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException ex)
        {
            _errorLoggingService.LogException(HttpContext, ex, "You do not have permission to resume this session.");
            TempData["Error"] = "You do not have permission to resume this session.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Cashier,StockManager")]
    public async Task<IActionResult> End(int sessionId)
    {
        try
        {
            await _terminalContextService.RequireCurrentTerminalAsync();
            await _userSessionService.EndSessionAsync(sessionId, GetUserId());

            if (User.IsInRole("Admin"))
            {
                return RedirectToAction(nameof(Summary), new { id = sessionId });
            }

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ToUserMessage(ex);
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException ex)
        {
            _errorLoggingService.LogException(HttpContext, ex, "You do not have permission to end this session.");
            TempData["Error"] = "You do not have permission to end this session.";
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Summary(int id)
    {
        return View(await _userSessionService.GetSessionSummaryAsync(id));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Cashier,StockManager")]
    public async Task<IActionResult> Heartbeat(int sessionId, string terminalName)
    {
        await _userSessionService.HeartbeatAsync(sessionId, terminalName);
        await _terminalContextService.HeartbeatAsync(GetUserId(), sessionId);
        return Ok();
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user was not found.");

    private string ToUserMessage(InvalidOperationException ex)
    {
        var message = ex is BranchPosException branchPosException ? branchPosException.UserMessage : ex.Message;
        _errorLoggingService.LogException(HttpContext, ex, message);
        return message;
    }
}
