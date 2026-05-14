using BranchPOS.Models;
using BranchPOS.ViewModels;
using BranchPOS.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _context;

    public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.Include(x => x.Branch).OrderBy(x => x.Email).ToListAsync();
        var model = new List<UserListItemViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            model.Add(new UserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                UserName = user.UserName,
                BranchName = user.Branch?.Name,
                Roles = roles.Count == 0 ? "No role" : string.Join(", ", roles),
                IsActive = user.IsActive
            });
        }

        return View(model);
    }

    public async Task<IActionResult> Create()
    {
        return View(await BuildCreateModelAsync(new UserCreateViewModel()));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildCreateModelAsync(model));
        }

        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Selected role does not exist.");
            return View(await BuildCreateModelAsync(model));
        }

        if ((model.Role == "Cashier" || model.Role == "StockManager") && !model.BranchId.HasValue)
        {
            ModelState.AddModelError(nameof(model.BranchId), "Cashiers and stock managers must be assigned to a branch.");
            return View(await BuildCreateModelAsync(model));
        }

        if (model.BranchId.HasValue && !await _context.Branches.AnyAsync(x => x.Id == model.BranchId.Value && x.IsActive))
        {
            ModelState.AddModelError(nameof(model.BranchId), "Selected branch is not active.");
            return View(await BuildCreateModelAsync(model));
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            BranchId = model.Role == "Admin" ? model.BranchId : model.BranchId!.Value,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(await BuildCreateModelAsync(model));
        }

        await _userManager.AddToRoleAsync(user, model.Role);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> AssignRole(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        ViewBag.Roles = await _roleManager.Roles.OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name!, x.Name!)).ToListAsync();
        ViewBag.UserRoles = await _userManager.GetRolesAsync(user);
        ViewBag.Branches = await _context.Branches
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == user.BranchId))
            .ToListAsync();
        return View(user);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(string id, string role, int? branchId)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (!await _roleManager.RoleExistsAsync(role))
        {
            return BadRequest();
        }

        if ((role == "Cashier" || role == "StockManager") && !branchId.HasValue)
        {
            ModelState.AddModelError(nameof(branchId), "Cashiers and stock managers must be assigned to a branch.");
        }

        if (branchId.HasValue && !await _context.Branches.AnyAsync(x => x.Id == branchId.Value && x.IsActive))
        {
            ModelState.AddModelError(nameof(branchId), "Selected branch is not active.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Roles = await _roleManager.Roles.OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name!, x.Name!, x.Name == role)).ToListAsync();
            ViewBag.UserRoles = await _userManager.GetRolesAsync(user);
            ViewBag.Branches = await _context.Branches
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == branchId))
                .ToListAsync();
            return View(user);
        }

        var existingRoles = await _userManager.GetRolesAsync(user);
        if (existingRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, existingRoles);
        }

        user.BranchId = branchId;
        await _userManager.UpdateAsync(user);
        await _userManager.AddToRoleAsync(user, role);
        TempData["Message"] = "User role and branch assignment updated. Ask the user to log out and sign in again.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        TempData["Message"] = user.IsActive ? "User activated." : "User deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var hasHistory =
            await _context.Orders.AnyAsync(x => x.CashierId == id) ||
            await _context.UserSessions.AnyAsync(x => x.UserId == id) ||
            await _context.Purchases.AnyAsync(x => x.PerformedByUserId == id) ||
            await _context.InventoryTransactions.AnyAsync(x => x.PerformedByUserId == id);

        if (hasHistory)
        {
            user.IsActive = false;
            await _userManager.UpdateAsync(user);
            TempData["Message"] = "User has operational history, so they were deactivated instead of deleted.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.DeleteAsync(user);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
            ? "User deleted."
            : string.Join(" ", result.Errors.Select(x => x.Description));

        return RedirectToAction(nameof(Index));
    }

    private async Task<UserCreateViewModel> BuildCreateModelAsync(UserCreateViewModel model)
    {
        model.Roles = await _roleManager.Roles
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name!, x.Name!, x.Name == model.Role))
            .ToListAsync();
        model.Branches = await _context.Branches
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.BranchId))
            .ToListAsync();
        return model;
    }
}
