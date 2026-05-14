using BranchPOS.Models;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITerminalContextService _terminalContextService;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ITerminalContextService terminalContextService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _terminalContextService = terminalContextService;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        return View(await BuildLoginModelAsync(new LoginViewModel(), returnUrl));
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildLoginModelAsync(model, returnUrl));
        }

        var user = await _userManager.Users.Include(x => x.Branch).FirstOrDefaultAsync(x => x.Email == model.Email);
        if (user is not null && !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "This account is inactive. Contact an administrator.");
            return View(await BuildLoginModelAsync(model, returnUrl));
        }

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            user ??= await _userManager.Users.Include(x => x.Branch).FirstOrDefaultAsync(x => x.Email == model.Email);
            if (user is null)
            {
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(await BuildLoginModelAsync(model, returnUrl));
            }

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
                return RedirectToAction("Index", "Home");
            }

            return LocalRedirect(SafeLocalUrl(returnUrl) ?? Url.Action("Index", "Home")!);
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
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
