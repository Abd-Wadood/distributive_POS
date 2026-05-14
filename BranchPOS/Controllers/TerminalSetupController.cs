using BranchPOS.Data;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[AllowAnonymous]
public class TerminalSetupController : Controller
{
    private readonly AppDbContext _context;

    public TerminalSetupController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? returnUrl = null)
    {
        var terminals = await _context.Terminals
            .Include(x => x.Branch)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Branch!.Name)
            .ThenBy(x => x.TerminalCode)
            .ToListAsync();

        return View(new TerminalSetupViewModel
        {
            ReturnUrl = returnUrl,
            Terminals = terminals
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(TerminalSetupViewModel model)
    {
        var terminalCode = TerminalContextService.NormalizeCode(model.TerminalCode);
        var terminal = await _context.Terminals.FirstOrDefaultAsync(x => x.TerminalCode == terminalCode && x.IsActive);
        if (terminal is null)
        {
            ModelState.AddModelError(nameof(model.TerminalCode), "Terminal is not registered or is inactive.");
            model.Terminals = await _context.Terminals
                .Include(x => x.Branch)
                .Where(x => x.IsActive)
                .OrderBy(x => x.Branch!.Name)
                .ThenBy(x => x.TerminalCode)
                .ToListAsync();
            return View("Index", model);
        }

        Response.Cookies.Append(TerminalContextService.TerminalCodeCookieName, terminal.TerminalCode, new CookieOptions
        {
            IsEssential = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });

        var returnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : Url.Action("Login", "Account");
        return Redirect(returnUrl!);
    }
}
