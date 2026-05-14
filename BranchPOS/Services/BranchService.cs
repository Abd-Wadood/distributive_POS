using BranchPOS.Data;
using BranchPOS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class BranchService : IBranchService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public BranchService(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<Branch>> GetBranchesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User not found.");
        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            return await _context.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        }

        if (!user.BranchId.HasValue)
        {
            return [];
        }

        return await _context.Branches.Where(x => x.Id == user.BranchId.Value && x.IsActive).ToListAsync(cancellationToken);
    }

    public async Task EnsureBranchAccessAsync(string userId, int branchId, CancellationToken cancellationToken = default)
    {
        var branches = await GetBranchesForUserAsync(userId, cancellationToken);
        if (branches.All(x => x.Id != branchId))
        {
            throw new UnauthorizedAccessException("User cannot access this branch.");
        }
    }
}
