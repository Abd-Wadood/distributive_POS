using BranchPOS.Models;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITerminalContextService _terminalContextService;
    private readonly ILoginSecurityService _loginSecurityService;
    private readonly IAuditLogService _auditLogService;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ITerminalContextService terminalContextService,
        ILoginSecurityService loginSecurityService,
        IAuditLogService auditLogService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _terminalContextService = terminalContextService;
        _loginSecurityService = loginSecurityService;
        _auditLogService = auditLogService;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        return View(await BuildLoginModelAsync(new LoginViewModel(), returnUrl));
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildLoginModelAsync(model, returnUrl));
        }

        var normalizedEmail = _userManager.NormalizeEmail(model.Email);
        if (await _loginSecurityService.IsBlockedAsync(normalizedEmail ?? model.Email, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "Too many login attempts. Please wait and try again.");
            return View(await BuildLoginModelAsync(model, returnUrl));
        }

        var user = await _userManager.Users.Include(x => x.Branch).FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);
        if (user is not null && !user.IsActive)
        {
            await _loginSecurityService.RecordFailureAsync(normalizedEmail ?? model.Email, user.Id, cancellationToken);
            ModelState.AddModelError(string.Empty, "Invalid login details or account temporarily locked.");
            return View(await BuildLoginModelAsync(model, returnUrl));
        }

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            user ??= await _userManager.Users.Include(x => x.Branch).FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);
            if (user is null)
            {
                await _signInManager.SignOutAsync();
                await _loginSecurityService.RecordFailureAsync(normalizedEmail ?? model.Email, null, cancellationToken);
                ModelState.AddModelError(string.Empty, "Invalid login details or account temporarily locked.");
                return View(await BuildLoginModelAsync(model, returnUrl));
            }

            await _loginSecurityService.RecordSuccessAsync(normalizedEmail ?? model.Email, user.Id, cancellationToken);

            var roles = await _userManager.GetRolesAsync(user);
            var isAdmin = roles.Contains("Admin");
            var needsBranchSession = roles.Contains("Cashier") || roles.Contains("StockManager");

            if (needsBranchSession)
            {
                if (!user.BranchId.HasValue)
                {
                    await _signInManager.SignOutAsync();
                    ModelState.AddModelError(string.Empty, "Your account has no branch assigned. Contact an administrator.");
                    return View(await BuildLoginModelAsync(model, returnUrl));
                }

                if (user.Branch is null || !user.Branch.IsActive)
                {
                    await _signInManager.SignOutAsync();
                    ModelState.AddModelError(string.Empty, "Your assigned branch is inactive. Contact an administrator.");
                    return View(await BuildLoginModelAsync(model, returnUrl));
                }

                var terminal = await _terminalContextService.GetCurrentTerminalAsync();
                if (terminal is null)
                {
                    return RedirectToAction("Index", "TerminalSetup", new { returnUrl = Url.Action("Index", "Sessions") });
                }

                return RedirectToAction("Index", "Sessions");
            }

            if (isAdmin)
            {
                await _auditLogService.LogSecurityAsync("AdminLogin", "Info", "Admin logged in.", user.Id, normalizedEmail, cancellationToken: cancellationToken);
                return RedirectToAction("Index", "Home");
            }

            return LocalRedirect(SafeLocalUrl(returnUrl) ?? Url.Action("Index", "Home")!);
        }

        await _loginSecurityService.RecordFailureAsync(normalizedEmail ?? model.Email, user?.Id, cancellationToken);
        if (result.IsLockedOut || user is not null && await _userManager.IsLockedOutAsync(user))
        {
            await _auditLogService.LogSecurityAsync("AccountLocked", "Critical", "Account was temporarily locked after failed login attempts.", user?.Id, normalizedEmail, cancellationToken: cancellationToken);
        }

        ModelState.AddModelError(string.Empty, "Invalid login details or account temporarily locked.");
        return View(await BuildLoginModelAsync(model, returnUrl));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();

    private async Task<LoginViewModel> BuildLoginModelAsync(LoginViewModel model, string? returnUrl)
    {
        var terminal = await _terminalContextService.GetCurrentTerminalAsync();
        model.ReturnUrl = returnUrl;
        model.HasRegisteredTerminal = terminal is not null;
        model.TerminalCode = terminal?.TerminalCode;
        model.TerminalName = terminal?.Name;
        model.TerminalBranchName = terminal?.Branch?.Name;
        return model;
    }

    private string? SafeLocalUrl(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? returnUrl : null;
}
