using System.Security.Claims;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;

namespace BranchPOS.Controllers;

[Authorize]
public class SessionsController : Controller
{
    private readonly IBranchService _branchService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITerminalContextService _terminalContextService;
    private readonly IErrorLoggingService _errorLoggingService;
    private readonly IIdempotencyService _idempotencyService;

    public SessionsController(IBranchService branchService, IUserSessionService userSessionService, ITerminalContextService terminalContextService, IErrorLoggingService errorLoggingService, IIdempotencyService idempotencyService)
    {
        _branchService = branchService;
        _userSessionService = userSessionService;
        _terminalContextService = terminalContextService;
        _errorLoggingService = errorLoggingService;
        _idempotencyService = idempotencyService;
    }

    [Authorize(Roles = "Cashier,StockManager")]
    public async Task<IActionResult> Index()
    {
        await _userSessionService.MarkAbandonedSessionsAsync();
        var userId = GetUserId();
        var branches = await _branchService.GetBranchesForUserAsync(userId);
        var active = await _userSessionService.GetActiveSessionAsync(userId);
        var abandoned = await _userSessionService.GetAbandonedSessionAsync(userId);
        var role = User.IsInRole("Cashier") ? "Cashier" : User.IsInRole("StockManager") ? "StockManager" : "Admin";
        var terminal = await _terminalContextService.RequireCurrentTerminalAsync();
        var canAccessTerminalBranch = branches.Any(x => x.Id == terminal.BranchId);

        return View(new SessionStartViewModel
        {
            ActiveSession = active,
            AbandonedSession = abandoned,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RoleName = role,
            BranchId = active?.BranchId ?? abandoned?.BranchId ?? terminal.BranchId,
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

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Cashier,StockManager"), EnableRateLimiting("SessionStartPolicy"), RequestSizeLimit(32768)]
    public async Task<IActionResult> Start(SessionStartViewModel model)
    {
        var userId = GetUserId();

        try
        {
            var terminal = await _terminalContextService.RequireCurrentTerminalFreshAsync();
            var role = User.IsInRole("Cashier") ? "Cashier" : "StockManager";
            var session = await _userSessionService.StartSessionAsync(new StartSessionDto
            {
                UserId = userId,
                IdempotencyKey = string.IsNullOrWhiteSpace(model.IdempotencyKey) ? _idempotencyService.GetOrCreateKey() : model.IdempotencyKey,
                BranchId = terminal.BranchId,
                RoleName = role,
                TerminalId = terminal.Id,
                TerminalCode = terminal.TerminalCode,
                TerminalName = terminal.Name,
                OpeningCashAmount = model.OpeningCashAmount,
                Notes = model.Notes
            });
            if (session.StartedAt < DateTime.UtcNow.AddSeconds(-5))
            {
                TempData["Message"] = $"Continuing existing session {session.SessionCode}.";
            }
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

    [HttpGet, Authorize(Roles = "Cashier,StockManager,Admin")]
    public async Task<IActionResult> Close(int sessionId)
    {
        try
        {
            var model = await _userSessionService.GetCloseSessionAsync(sessionId, GetUserId(), IsManagerOrAdmin());
            model.IdempotencyKey = Guid.NewGuid().ToString("N");
            return View(model);
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

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> PendingCloseApprovals(CancellationToken cancellationToken)
    {
        var approvals = await _userSessionService.GetPendingCloseApprovalsAsync(cancellationToken);
        return View(approvals);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveClose(int sessionId, string idempotencyKey, CancellationToken cancellationToken)
    {
        try
        {
            var terminal = await _terminalContextService.RequireCurrentTerminalFreshAsync(cancellationToken);
            var session = await _userSessionService.ApprovePendingCloseAsync(
                sessionId,
                GetUserId(),
                terminal.Id,
                terminal.TerminalCode,
                idempotencyKey,
                cancellationToken);

            TempData["Success"] = $"Session {session.SessionCode} closed.";
            return RedirectToAction(nameof(PendingCloseApprovals));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ToUserMessage(ex);
            return RedirectToAction(nameof(PendingCloseApprovals));
        }
        catch (UnauthorizedAccessException ex)
        {
            _errorLoggingService.LogException(HttpContext, ex, "You do not have permission to approve this close request.");
            TempData["Error"] = "You do not have permission to approve this close request.";
            return RedirectToAction(nameof(PendingCloseApprovals));
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Cashier,StockManager,Admin")]
    public async Task<IActionResult> Close(SessionCloseViewModel model, bool forceClose = false)
    {
        try
        {
            var terminal = await _terminalContextService.RequireCurrentTerminalFreshAsync();
            var session = await _userSessionService.CloseSessionAsync(new CloseSessionDto
            {
                SessionId = model.Session.Id,
                IdempotencyKey = string.IsNullOrWhiteSpace(model.IdempotencyKey) ? _idempotencyService.GetOrCreateKey() : model.IdempotencyKey,
                UserId = GetUserId(),
                TerminalId = terminal.Id,
                TerminalCode = terminal.TerminalCode,
                CountedClosingCash = model.CountedClosingCash,
                ConfirmationText = model.ConfirmationText,
                IsManagerOrAdmin = IsManagerOrAdmin(),
                ForceClose = forceClose
            });

            TempData["Success"] = $"Session {session.SessionCode} closed.";
            return RedirectToAction(nameof(Summary), new { id = session.Id });
        }
        catch (InvalidOperationException ex)
        {
            var message = ToUserMessage(ex);
            ModelState.AddModelError(string.Empty, message);
            try
            {
                var closeModel = await _userSessionService.GetCloseSessionAsync(model.Session.Id, GetUserId(), IsManagerOrAdmin());
                closeModel.CountedClosingCash = model.CountedClosingCash;
                closeModel.ConfirmationText = model.ConfirmationText;
                closeModel.IdempotencyKey = model.IdempotencyKey;
                return View(closeModel);
            }
            catch
            {
                TempData["Error"] = message;
                return RedirectToAction(nameof(Index));
            }
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "StockManager,Admin")]
    public async Task<IActionResult> Reopen(int sessionId, string reason)
    {
        try
        {
            var terminal = await _terminalContextService.RequireCurrentTerminalFreshAsync();
            var session = await _userSessionService.ReopenSessionAsync(new ReopenSessionDto
            {
                SessionId = sessionId,
                UserId = GetUserId(),
                TerminalId = terminal.Id,
                TerminalCode = terminal.TerminalCode,
                Reason = reason
            });
            TempData["Success"] = $"Session {session.SessionCode} reopened.";
            return RedirectToAction(nameof(Summary), new { id = session.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ToUserMessage(ex);
            return RedirectToAction(nameof(Summary), new { id = sessionId });
        }
    }

    [Authorize(Roles = "Cashier,StockManager,Admin")]
    public async Task<IActionResult> Summary(int id)
    {
        return View(await _userSessionService.GetSessionSummaryAsync(id));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Cashier,StockManager"), EnableRateLimiting("TerminalHeartbeatPolicy"), RequestSizeLimit(8192)]
    public async Task<IActionResult> Heartbeat(int sessionId, string terminalName)
    {
        await _userSessionService.HeartbeatAsync(sessionId, terminalName);
        await _terminalContextService.HeartbeatAsync(GetUserId(), sessionId);
        return Ok();
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user was not found.");

    private bool IsManagerOrAdmin() =>
        User.IsInRole("Admin") || User.IsInRole("StockManager");

    private string ToUserMessage(InvalidOperationException ex)
    {
        var message = ex is BranchPosException branchPosException ? branchPosException.UserMessage : ex.Message;
        _errorLoggingService.LogException(HttpContext, ex, message);
        return message;
    }
}
