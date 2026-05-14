using BranchPOS.Models;

namespace BranchPOS.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }

    public int ActiveBranches { get; set; }

    public int TotalTerminals { get; set; }

    public int ActiveSessions { get; set; }

    public int TotalCategories { get; set; }

    public decimal TodaySalesTotal { get; set; }

    public int TodayCompletedOrders { get; set; }

    public int LowStockCount { get; set; }

    public List<UserSession> RecentSessions { get; set; } = new();
}
