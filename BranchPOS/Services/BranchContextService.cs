using System.Security.Claims;
using BranchPOS.Data;
using BranchPOS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class BranchContextService : IBranchContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBranchService _branchService;

    public BranchContextService(
        IHttpContextAccessor httpContextAccessor,
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        IBranchService branchService)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
        _userManager = userManager;
        _branchService = branchService;
    }

    public async Task<int> GetCurrentBranchIdAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var activeSession = await _context.UserSessions
            .Where(x => x.UserId == userId && x.Status == SessionStatus.Active)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeSession is not null)
        {
            return activeSession.BranchId;
        }

        var user = await _userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User not found.");
        return user.BranchId ?? 1;
    }

    public async Task EnsureUserCanAccessBranchAsync(int branchId, CancellationToken cancellationToken = default) =>
        await _branchService.EnsureBranchAccessAsync(GetUserId(), branchId, cancellationToken);

    private string GetUserId() =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user was not found.");
}
