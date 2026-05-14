using BranchPOS.Models;
using Microsoft.AspNetCore.Identity;

namespace BranchPOS.Services;

public class IdentitySeedService : IIdentitySeedService
{
    private static readonly string[] Roles = ["Admin", "StockManager", "Cashier"];

    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public IdentitySeedService(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
    }

    public async Task SeedAsync()
    {
        foreach (var role in Roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var email = _configuration["SeedAdmin:Email"] ?? "admin@branchpos.local";
        var password = _configuration["SeedAdmin:Password"] ?? "Admin12345";
        var admin = await _userManager.FindByEmailAsync(email);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "Branch Administrator",
                BranchId = 1,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
            }
        }

        if (!await _userManager.IsInRoleAsync(admin, "Admin"))
        {
            await _userManager.AddToRoleAsync(admin, "Admin");
        }

        var changed = false;
        if (!admin.BranchId.HasValue)
        {
            admin.BranchId = 1;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(admin.FullName))
        {
            admin.FullName = "Branch Administrator";
            changed = true;
        }
        if (changed)
        {
            await _userManager.UpdateAsync(admin);
        }
    }
}
