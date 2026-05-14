using BranchPOS.Data;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin")]
public class TerminalsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchService _branchService;

    public TerminalsController(AppDbContext context, IBranchService branchService)
    {
        _context = context;
        _branchService = branchService;
    }

    public async Task<IActionResult> Index()
    {
        return View(await BuildModelAsync(new TerminalCreateViewModel()));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "NewTerminal")] TerminalCreateViewModel model)
    {
        model.TerminalCode = TerminalContextService.NormalizeCode(model.TerminalCode);
        if (string.IsNullOrWhiteSpace(model.TerminalCode))
        {
            ModelState.AddModelError(nameof(model.TerminalCode), "Terminal code is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Terminal name is required.");
        }

        if (await _context.Terminals.AnyAsync(x => x.TerminalCode == model.TerminalCode))
        {
            ModelState.AddModelError(nameof(model.TerminalCode), "Terminal code already exists.");
        }

        try
        {
            await _branchService.EnsureBranchAccessAsync(User.GetUserId(), model.BranchId);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.BranchId), ex.Message);
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildModelAsync(model));
        }

        _context.Terminals.Add(new BranchPOS.Models.Terminal
        {
            TerminalCode = model.TerminalCode,
            BranchId = model.BranchId,
            Name = model.Name.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(model.IpAddress) ? null : model.IpAddress.Trim()
        });
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var terminal = await _context.Terminals.FirstOrDefaultAsync(x => x.Id == id);
        if (terminal is null)
        {
            return NotFound();
        }

        await _branchService.EnsureBranchAccessAsync(User.GetUserId(), terminal.BranchId);
        terminal.IsActive = !terminal.IsActive;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<TerminalAdminViewModel> BuildModelAsync(TerminalCreateViewModel createModel)
    {
        var branches = await _branchService.GetBranchesForUserAsync(User.GetUserId());
        createModel.Branches = branches
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == createModel.BranchId))
            .ToList();
        if (createModel.BranchId <= 0)
        {
            createModel.BranchId = branches.FirstOrDefault()?.Id ?? 1;
        }

        return new TerminalAdminViewModel
        {
            NewTerminal = createModel,
            Terminals = await _context.Terminals
                .Include(x => x.Branch)
                .OrderBy(x => x.Branch!.Name)
                .ThenBy(x => x.TerminalCode)
                .ToListAsync(),
            Heartbeats = await _context.TerminalHeartbeats
                .Include(x => x.Terminal)
                .Include(x => x.Branch)
                .Include(x => x.CurrentUser)
                .Include(x => x.CurrentSession)
                .OrderByDescending(x => x.LastSeenAt)
                .ToListAsync()
        };
    }
}

internal static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? throw new InvalidOperationException("Authenticated user was not found.");
}
