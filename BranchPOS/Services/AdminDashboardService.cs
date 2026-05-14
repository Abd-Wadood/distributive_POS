using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _context;

    public AdminDashboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        return new AdminDashboardViewModel
        {
            TotalUsers = await _context.Users.CountAsync(cancellationToken),
            ActiveBranches = await _context.Branches.CountAsync(x => x.IsActive, cancellationToken),
            TotalTerminals = await _context.Terminals.CountAsync(cancellationToken),
            ActiveSessions = await _context.UserSessions.CountAsync(x => x.Status == SessionStatus.Active, cancellationToken),
            TotalCategories = await _context.Categories.CountAsync(cancellationToken),
            TodayCompletedOrders = await _context.Orders.CountAsync(x =>
                x.OrderStatus == OrderStatus.Completed &&
                x.CompletedAt >= today &&
                x.CompletedAt < tomorrow, cancellationToken),
            TodaySalesTotal = await _context.Orders
                .Where(x => x.OrderStatus == OrderStatus.Completed && x.CompletedAt >= today && x.CompletedAt < tomorrow)
                .SumAsync(x => x.TotalAmount, cancellationToken),
            LowStockCount = await _context.Inventories.CountAsync(x => x.CurrentQuantity <= x.Ingredient!.MinimumStockLevel, cancellationToken),
            RecentSessions = await _context.UserSessions
                .Include(x => x.User)
                .Include(x => x.Branch)
                .OrderByDescending(x => x.StartedAt)
                .Take(8)
                .ToListAsync(cancellationToken)
        };
    }
}
