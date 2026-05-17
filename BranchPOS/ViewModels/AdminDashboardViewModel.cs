namespace BranchPOS.ViewModels;

public class AdminDashboardViewModel
{
    public DateTime GeneratedAtUtc { get; set; }

    public List<DashboardAlertViewModel> Alerts { get; set; } = new();

    public List<DashboardMetricCardViewModel> MetricCards { get; set; } = new();

    public List<BranchHealthViewModel> BranchHealth { get; set; } = new();

    public List<TerminalHealthViewModel> TerminalHealth { get; set; } = new();

    public List<SessionMonitorViewModel> RecentSessions { get; set; } = new();

    public SalesSnapshotViewModel SalesSnapshot { get; set; } = new();

    public List<InventoryRiskViewModel> InventoryRisks { get; set; } = new();

    public SecuritySummaryViewModel SecuritySummary { get; set; } = new();
}

public class DashboardAlertViewModel
{
    public string Severity { get; set; } = "Info";

    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string? ActionUrl { get; set; }
}

public class DashboardMetricCardViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Hint { get; set; } = string.Empty;

    public string Badge { get; set; } = "Info";
}

public class BranchHealthViewModel
{
    public int BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public int ActiveTerminals { get; set; }

    public int OnlineTerminals { get; set; }

    public int ActiveSessions { get; set; }

    public int TodayOrders { get; set; }

    public decimal TodaySales { get; set; }

    public int LowStockCount { get; set; }

    public string Status { get; set; } = "Healthy";
}

public class TerminalHealthViewModel
{
    public string TerminalCode { get; set; } = string.Empty;

    public string TerminalName { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public string Status { get; set; } = "Offline";

    public string? CurrentUser { get; set; }

    public string? CurrentSessionCode { get; set; }
}

public class SessionMonitorViewModel
{
    public int SessionId { get; set; }

    public string SessionCode { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public string TerminalName { get; set; } = string.Empty;

    public string TerminalCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public DateTime? LastHeartbeatAt { get; set; }

    public int CompletedOrdersCount { get; set; }

    public int DraftOrdersCount { get; set; }
}

public class SalesSnapshotViewModel
{
    public decimal TodaySales { get; set; }

    public int TodayCompletedOrders { get; set; }

    public int TodayCancelledOrders { get; set; }

    public decimal AverageOrderValue { get; set; }

    public List<SalesByBranchViewModel> SalesByBranch { get; set; } = new();

    public List<HourlySalesViewModel> HourlySales { get; set; } = new();
}

public class SalesByBranchViewModel
{
    public string BranchName { get; set; } = string.Empty;

    public int OrdersCount { get; set; }

    public decimal SalesTotal { get; set; }
}

public class HourlySalesViewModel
{
    public int Hour { get; set; }

    public int OrdersCount { get; set; }

    public decimal SalesTotal { get; set; }

    public int PercentOfPeak { get; set; }
}

public class InventoryRiskViewModel
{
    public string BranchName { get; set; } = string.Empty;

    public string IngredientName { get; set; } = string.Empty;

    public string UnitType { get; set; } = string.Empty;

    public decimal CurrentQuantity { get; set; }

    public decimal MinimumStockLevel { get; set; }

    public string Severity { get; set; } = "Warning";
}

public class SecuritySummaryViewModel
{
    public int FailedLoginsToday { get; set; }

    public int LockedAccounts { get; set; }

    public int RateLimitHitsToday { get; set; }

    public int SuspiciousIpsToday { get; set; }

    public int RepeatedLoginFailuresByUsername { get; set; }

    public int RepeatedLoginFailuresByIp { get; set; }

    public int TerminalHeartbeatSpamCount { get; set; }

    public int BlockedReportSpamCount { get; set; }

    public int UnauthorizedAccessToday { get; set; }

    public int AdminChangesToday { get; set; }

    public int TerminalChangesToday { get; set; }

    public bool HasAuditLogs { get; set; }
}
