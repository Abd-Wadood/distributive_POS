using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BranchPOS.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private const string AdminTerminalCode = "MAIN-01";

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly DashboardOptions _options;

    public AdminDashboardService(AppDbContext context, IMemoryCache cache, IOptions<DashboardOptions> options)
    {
        _context = context;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<AdminDashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var todayLocal = DateTime.Now.Date;
        var cacheKey = $"admin-dashboard:{todayLocal:yyyyMMdd}";
        if (_cache.TryGetValue(cacheKey, out AdminDashboardViewModel? cached) && cached is not null)
        {
            return cached;
        }

        var now = DateTime.UtcNow;
        var today = todayLocal.ToUniversalTime();
        var tomorrow = today.AddDays(1);
        var onlineCutoff = now.AddSeconds(-_options.TerminalOnlineSeconds);
        var staleCutoff = now.AddSeconds(-_options.TerminalStaleSeconds);
        var oldSessionCutoff = now.AddHours(-12);

        var branchRows = await _context.Branches
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new BranchBaseRow(x.Id, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);

        var activeBranchIds = branchRows.Where(x => x.IsActive).Select(x => x.Id).ToHashSet();

        var terminalGroups = await _context.Terminals
            .AsNoTracking()
            .Where(x => x.TerminalCode != AdminTerminalCode)
            .GroupBy(x => x.BranchId)
            .Select(x => new TerminalGroupRow
            {
                BranchId = x.Key,
                Total = x.Count(),
                Active = x.Count(t => t.IsActive)
            })
            .ToListAsync(cancellationToken);

        var onlineTerminalGroups = await _context.TerminalHeartbeats
            .AsNoTracking()
            .Where(x => x.LastSeenAt >= onlineCutoff)
            .Where(x => x.TerminalCode != AdminTerminalCode)
            .GroupBy(x => x.BranchId)
            .Select(x => new OnlineTerminalGroupRow { BranchId = x.Key, Online = x.Count() })
            .ToListAsync(cancellationToken);

        var activeSessionGroups = await _context.UserSessions
            .AsNoTracking()
            .Where(x => x.Status == SessionStatus.Active || x.Status == SessionStatus.Reopened)
            .GroupBy(x => x.BranchId)
            .Select(x => new CountByBranchRow { BranchId = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);

        var todayOrderGroups = await _context.Orders
            .AsNoTracking()
            .Where(x => x.CompletedAt >= today && x.CompletedAt < tomorrow)
            .GroupBy(x => x.BranchId)
            .Select(x => new TodayOrderGroupRow
            {
                BranchId = x.Key,
                Completed = x.Count(o => o.OrderStatus == OrderStatus.Completed),
                Cancelled = x.Count(o => o.OrderStatus == OrderStatus.Cancelled),
                Sales = x.Where(o => o.OrderStatus == OrderStatus.Completed).Sum(o => o.TotalAmount)
            })
            .ToListAsync(cancellationToken);

        var lowStockGroups = await _context.InventoryStocks
            .AsNoTracking()
            .Where(x => x.QuantityBase <= x.InventoryItem!.ReorderLevel)
            .GroupBy(x => x.BranchId)
            .Select(x => new CountByBranchRow { BranchId = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);

        var userCounts = await _context.Users
            .AsNoTracking()
            .GroupBy(x => x.IsActive)
            .Select(x => new { IsActive = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);

        var activeBranches = branchRows.Count(x => x.IsActive);
        var totalTerminals = terminalGroups.Sum(x => x.Total);
        var activeTerminals = terminalGroups.Sum(x => x.Active);
        var onlineTerminals = onlineTerminalGroups.Sum(x => x.Online);
        var staleOrOfflineTerminals = Math.Max(0, activeTerminals - onlineTerminals);
        var activeSessions = activeSessionGroups.Sum(x => x.Count);
        var abandonedSessions = await _context.UserSessions.AsNoTracking().CountAsync(x => x.Status == SessionStatus.Abandoned, cancellationToken);
        var pendingCloseApprovals = await _context.UserSessions.AsNoTracking().CountAsync(x => x.Status == SessionStatus.ClosingPending && x.RequiresManagerApproval, cancellationToken);
        var todayCompletedOrders = todayOrderGroups.Sum(x => x.Completed);
        var todayCancelledOrders = todayOrderGroups.Sum(x => x.Cancelled);
        var todaySales = todayOrderGroups.Sum(x => x.Sales);
        var lowStockCount = lowStockGroups.Sum(x => x.Count);
        var activeUsers = userCounts.FirstOrDefault(x => x.IsActive)?.Count ?? 0;
        var inactiveUsers = userCounts.FirstOrDefault(x => !x.IsActive)?.Count ?? 0;

        var branchHealth = BuildBranchHealth(branchRows, terminalGroups, onlineTerminalGroups, activeSessionGroups, todayOrderGroups, lowStockGroups);
        var terminalHealth = await GetTerminalHealthAsync(onlineCutoff, staleCutoff, cancellationToken);
        var recentSessions = await GetRecentSessionsAsync(cancellationToken);
        var salesSnapshot = await GetSalesSnapshotAsync(today, tomorrow, todaySales, todayCompletedOrders, todayCancelledOrders, cancellationToken);
        var inventoryRisks = await GetInventoryRisksAsync(cancellationToken);
        var securitySummary = await GetSecuritySummaryAsync(today, tomorrow, cancellationToken);

        var model = new AdminDashboardViewModel
        {
            GeneratedAtUtc = now,
            BranchHealth = branchHealth,
            TerminalHealth = terminalHealth,
            RecentSessions = recentSessions,
            SalesSnapshot = salesSnapshot,
            InventoryRisks = inventoryRisks,
            SecuritySummary = securitySummary,
            MetricCards =
            {
                new() { Title = "Online terminals", Value = onlineTerminals.ToString(), Hint = $"{activeTerminals} active terminals", Badge = onlineTerminals == activeTerminals ? "Healthy" : "Warning" },
                new() { Title = "Offline/stale terminals", Value = staleOrOfflineTerminals.ToString(), Hint = "Need terminal check-in", Badge = staleOrOfflineTerminals == 0 ? "Healthy" : "Critical" },
                new() { Title = "Active branches", Value = activeBranches.ToString(), Hint = $"{branchRows.Count} total branches", Badge = "Info" },
                new() { Title = "Active sessions", Value = activeSessions.ToString(), Hint = $"{abandonedSessions} abandoned", Badge = abandonedSessions == 0 ? "Healthy" : "Warning" },
                new() { Title = "Today's orders", Value = todayCompletedOrders.ToString(), Hint = $"{todayCancelledOrders} cancelled", Badge = "Info" },
                new() { Title = "Today's sales", Value = todaySales.ToString("C"), Hint = "Completed orders only", Badge = "Healthy" },
                new() { Title = "Low stock items", Value = lowStockCount.ToString(), Hint = "At or below minimum", Badge = lowStockCount == 0 ? "Healthy" : "Critical" },
                new() { Title = "Users", Value = activeUsers.ToString(), Hint = $"{inactiveUsers} inactive", Badge = inactiveUsers == 0 ? "Healthy" : "Info" }
            }
        };

        model.Alerts = await BuildAlertsAsync(
            staleOrOfflineTerminals,
            terminalHealth,
            abandonedSessions,
            pendingCloseApprovals,
            oldSessionCutoff,
            inventoryRisks,
            securitySummary,
            cancellationToken);

        _cache.Set(cacheKey, model, TimeSpan.FromSeconds(Math.Max(5, _options.CacheSeconds)));
        return model;
    }

    private List<BranchHealthViewModel> BuildBranchHealth(
        List<BranchBaseRow> branches,
        IEnumerable<TerminalGroupRow> terminalGroups,
        IEnumerable<OnlineTerminalGroupRow> onlineTerminalGroups,
        IEnumerable<CountByBranchRow> activeSessionGroups,
        IEnumerable<TodayOrderGroupRow> todayOrderGroups,
        IEnumerable<CountByBranchRow> lowStockGroups)
    {
        return branches
            .Where(x => x.IsActive)
            .Select(branch =>
            {
                var activeTerminals = terminalGroups.FirstOrDefault(x => x.BranchId == branch.Id)?.Active ?? 0;
                var onlineTerminals = onlineTerminalGroups.FirstOrDefault(x => x.BranchId == branch.Id)?.Online ?? 0;
                var activeSessions = activeSessionGroups.FirstOrDefault(x => x.BranchId == branch.Id)?.Count ?? 0;
                var todayOrders = todayOrderGroups.FirstOrDefault(x => x.BranchId == branch.Id);
                var lowStock = lowStockGroups.FirstOrDefault(x => x.BranchId == branch.Id)?.Count ?? 0;
                var status = lowStock > 0 || (activeTerminals > 0 && onlineTerminals == 0)
                    ? "Critical"
                    : onlineTerminals < activeTerminals || activeSessions == 0
                        ? "Warning"
                        : "Healthy";

                return new BranchHealthViewModel
                {
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    ActiveTerminals = activeTerminals,
                    OnlineTerminals = onlineTerminals,
                    ActiveSessions = activeSessions,
                    TodayOrders = todayOrders?.Completed ?? 0,
                    TodaySales = todayOrders?.Sales ?? 0m,
                    LowStockCount = lowStock,
                    Status = status
                };
            })
            .ToList();
    }

    private async Task<List<TerminalHealthViewModel>> GetTerminalHealthAsync(DateTime onlineCutoff, DateTime staleCutoff, CancellationToken cancellationToken)
    {
        var terminals = await _context.Terminals
            .AsNoTracking()
            .Where(x => x.TerminalCode != AdminTerminalCode)
            .Select(x => new TerminalHealthViewModel
            {
                TerminalCode = x.TerminalCode,
                TerminalName = x.Name,
                BranchName = x.Branch!.Name,
                IsActive = x.IsActive,
                LastSeenAt = _context.TerminalHeartbeats
                    .Where(h => h.TerminalId == x.Id)
                    .Select(h => (DateTime?)h.LastSeenAt)
                    .FirstOrDefault(),
                CurrentUser = _context.TerminalHeartbeats
                    .Where(h => h.TerminalId == x.Id)
                    .Select(h => h.CurrentUser == null ? null : h.CurrentUser.Email)
                    .FirstOrDefault(),
                CurrentSessionCode = _context.TerminalHeartbeats
                    .Where(h => h.TerminalId == x.Id)
                    .Select(h => h.CurrentSession == null ? null : h.CurrentSession.SessionCode)
                    .FirstOrDefault()
            })
            .OrderByDescending(x => x.LastSeenAt.HasValue)
            .ThenByDescending(x => x.LastSeenAt)
            .ThenBy(x => x.TerminalCode)
            .Take(_options.MaxTerminals)
            .ToListAsync(cancellationToken);

        foreach (var terminal in terminals)
        {
            terminal.Status = !terminal.IsActive
                ? "Inactive"
                : terminal.LastSeenAt is null || terminal.LastSeenAt < staleCutoff
                    ? "Offline"
                    : terminal.LastSeenAt < onlineCutoff
                        ? "Stale"
                        : "Online";
        }

        return terminals;
    }

    private async Task<List<SessionMonitorViewModel>> GetRecentSessionsAsync(CancellationToken cancellationToken)
    {
        var sessions = await _context.UserSessions
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAt)
            .Take(_options.MaxRecentSessions)
            .Select(x => new SessionMonitorViewModel
            {
                SessionId = x.Id,
                SessionCode = x.SessionCode,
                UserEmail = x.User == null ? x.UserId : x.User.Email ?? x.UserId,
                RoleName = x.RoleName,
                BranchName = x.Branch == null ? "" : x.Branch.Name,
                TerminalName = x.TerminalName,
                TerminalCode = x.TerminalCode,
                Status = x.Status.ToString(),
                StartedAt = x.StartedAt,
                EndedAt = x.EndedAt,
                LastHeartbeatAt = _context.UserSessionHeartbeats
                    .Where(h => h.UserSessionId == x.Id)
                    .Select(h => (DateTime?)h.LastSeenAt)
                    .FirstOrDefault(),
                CompletedOrdersCount = _context.Orders.Count(o => o.UserSessionId == x.Id && o.OrderStatus == OrderStatus.Completed),
                DraftOrdersCount = _context.Orders.Count(o => o.UserSessionId == x.Id && o.OrderStatus == OrderStatus.Draft)
            })
            .ToListAsync(cancellationToken);

        return sessions;
    }

    private async Task<SalesSnapshotViewModel> GetSalesSnapshotAsync(DateTime today, DateTime tomorrow, decimal todaySales, int completedOrders, int cancelledOrders, CancellationToken cancellationToken)
    {
        var salesByBranch = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderStatus == OrderStatus.Completed && x.CompletedAt >= today && x.CompletedAt < tomorrow)
            .GroupBy(x => new { x.BranchId, x.Branch!.Name })
            .Select(x => new SalesByBranchViewModel
            {
                BranchName = x.Key.Name,
                OrdersCount = x.Count(),
                SalesTotal = x.Sum(o => o.TotalAmount)
            })
            .OrderByDescending(x => x.SalesTotal)
            .Take(10)
            .ToListAsync(cancellationToken);

        var hourly = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderStatus == OrderStatus.Completed && x.CompletedAt >= today && x.CompletedAt < tomorrow)
            .GroupBy(x => x.CompletedAt!.Value.Hour)
            .Select(x => new HourlySalesViewModel
            {
                Hour = x.Key,
                OrdersCount = x.Count(),
                SalesTotal = x.Sum(o => o.TotalAmount)
            })
            .OrderBy(x => x.Hour)
            .ToListAsync(cancellationToken);

        var peak = hourly.Count == 0 ? 0m : hourly.Max(x => x.SalesTotal);
        foreach (var row in hourly)
        {
            row.PercentOfPeak = peak <= 0 ? 0 : Math.Max(4, (int)Math.Round(row.SalesTotal / peak * 100));
        }

        return new SalesSnapshotViewModel
        {
            TodaySales = todaySales,
            TodayCompletedOrders = completedOrders,
            TodayCancelledOrders = cancelledOrders,
            AverageOrderValue = completedOrders == 0 ? 0 : todaySales / completedOrders,
            SalesByBranch = salesByBranch,
            HourlySales = hourly
        };
    }

    private async Task<List<InventoryRiskViewModel>> GetInventoryRisksAsync(CancellationToken cancellationToken)
    {
        return await _context.InventoryStocks
            .AsNoTracking()
            .Where(x => x.QuantityBase <= x.InventoryItem!.ReorderLevel)
            .OrderBy(x => x.InventoryItem!.ReorderLevel == 0 ? 0 : x.QuantityBase / x.InventoryItem.ReorderLevel)
            .ThenBy(x => x.QuantityBase)
            .Select(x => new InventoryRiskViewModel
            {
                BranchName = x.Branch == null ? "" : x.Branch.Name,
                IngredientName = x.InventoryItem == null ? "" : $"{x.InventoryItem.Name} ({x.InventoryLocation!.Name})",
                UnitType = x.InventoryItem == null ? "" : x.InventoryItem.BaseUnit,
                CurrentQuantity = x.QuantityBase,
                MinimumStockLevel = x.InventoryItem == null ? 0 : x.InventoryItem.ReorderLevel,
                Severity = x.QuantityBase <= 0 ? "Critical" : "Warning"
            })
            .Take(_options.MaxInventoryRisks)
            .ToListAsync(cancellationToken);
    }

    private async Task<SecuritySummaryViewModel> GetSecuritySummaryAsync(DateTime today, DateTime tomorrow, CancellationToken cancellationToken)
    {
        var hasAuditLogs = await _context.AuditLogs.AsNoTracking().AnyAsync(cancellationToken);
        return new SecuritySummaryViewModel
        {
            HasAuditLogs = hasAuditLogs,
            FailedLoginsToday = await _context.AuditLogs
                .AsNoTracking()
                .CountAsync(x => x.CreatedAt >= today && x.CreatedAt < tomorrow && x.EventType == "LoginFailed", cancellationToken),
            LockedAccounts = await _context.Users
                .AsNoTracking()
                .CountAsync(x => x.LockoutEnd != null && x.LockoutEnd > DateTimeOffset.UtcNow, cancellationToken),
            RateLimitHitsToday = await _context.AuditLogs
                .AsNoTracking()
                .CountAsync(x => x.CreatedAt >= today && x.CreatedAt < tomorrow && x.EventType == "RateLimitHit", cancellationToken),
            SuspiciousIpsToday = await _context.AuditLogs
                .AsNoTracking()
                .Where(x => x.CreatedAt >= today && x.CreatedAt < tomorrow && x.EventType == "LoginFailed" && x.IpAddress != null)
                .GroupBy(x => x.IpAddress)
                .CountAsync(x => x.Count() >= 5, cancellationToken),
            RepeatedLoginFailuresByUsername = await _context.AuditLogs
                .AsNoTracking()
                .Where(x => x.CreatedAt >= today && x.CreatedAt < tomorrow && x.EventType == "LoginFailed" && x.AttemptedUserName != null)
                .GroupBy(x => x.AttemptedUserName)
                .CountAsync(x => x.Count() >= 5, cancellationToken),
            RepeatedLoginFailuresByIp = await _context.AuditLogs
                .AsNoTracking()
                .Where(x => x.CreatedAt >= today && x.CreatedAt < tomorrow && x.EventType == "LoginFailed" && x.IpAddress != null)
                .GroupBy(x => x.IpAddress)
                .CountAsync(x => x.Count() >= 5, cancellationToken),
            TerminalHeartbeatSpamCount = await _context.AuditLogs
                .AsNoTracking()
                .CountAsync(x => x.CreatedAt >= today && x.CreatedAt < tomorrow && x.EventType == "RateLimitHit" && x.Message != null && x.Message.Contains("TerminalHeartbeatPolicy"), cancellationToken),
            BlockedReportSpamCount = await _context.AuditLogs
                .AsNoTracking()
                .CountAsync(x => x.CreatedAt >= today && x.CreatedAt < tomorrow && x.EventType == "RateLimitHit" && x.Message != null && x.Message.Contains("ReportsPolicy"), cancellationToken),
            UnauthorizedAccessToday = 0,
            AdminChangesToday = await _context.AuditLogs
                .AsNoTracking()
                .CountAsync(x => x.CreatedAt >= today && x.CreatedAt < tomorrow &&
                    (x.Action.StartsWith("Branch") || x.Action.StartsWith("Terminal") || x.Action.StartsWith("Session")), cancellationToken),
            TerminalChangesToday = await _context.AuditLogs
                .AsNoTracking()
                .CountAsync(x => x.CreatedAt >= today && x.CreatedAt < tomorrow && x.Action.StartsWith("Terminal"), cancellationToken)
        };
    }

    private async Task<List<DashboardAlertViewModel>> BuildAlertsAsync(
        int staleOrOfflineTerminals,
        List<TerminalHealthViewModel> terminals,
        int abandonedSessions,
        int pendingCloseApprovals,
        DateTime oldSessionCutoff,
        List<InventoryRiskViewModel> inventoryRisks,
        SecuritySummaryViewModel security,
        CancellationToken cancellationToken)
    {
        var alerts = new List<DashboardAlertViewModel>();

        if (staleOrOfflineTerminals > 0)
        {
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "Critical",
                Title = "Terminal connectivity needs attention",
                Detail = $"{staleOrOfflineTerminals} active terminal(s) are stale or offline.",
                ActionUrl = "/Terminals"
            });
        }

        var staleTerminal = terminals.FirstOrDefault(x => x.Status is "Stale" or "Offline");
        if (staleTerminal is not null)
        {
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = staleTerminal.Status == "Offline" ? "Critical" : "Warning",
                Title = $"{staleTerminal.TerminalCode} is {staleTerminal.Status.ToLowerInvariant()}",
                Detail = $"{staleTerminal.TerminalName} at {staleTerminal.BranchName} last checked in {FormatRelativeTime(staleTerminal.LastSeenAt)}.",
                ActionUrl = "/Terminals"
            });
        }

        if (abandonedSessions > 0)
        {
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "Warning",
                Title = "Abandoned sessions",
                Detail = $"{abandonedSessions} session(s) need review or continuation."
            });
        }

        if (pendingCloseApprovals > 0)
        {
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "Warning",
                Title = "Session close approvals pending",
                Detail = $"{pendingCloseApprovals} close request(s) need admin approval.",
                ActionUrl = "/Sessions/PendingCloseApprovals"
            });
        }

        var oldActiveSessions = await _context.UserSessions
            .AsNoTracking()
            .CountAsync(x => (x.Status == SessionStatus.Active || x.Status == SessionStatus.Reopened) && x.StartedAt < oldSessionCutoff, cancellationToken);
        if (oldActiveSessions > 0)
        {
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "Warning",
                Title = "Long-running active sessions",
                Detail = $"{oldActiveSessions} active session(s) have been open for more than 12 hours."
            });
        }

        var criticalStock = inventoryRisks.Count(x => x.Severity == "Critical");
        if (criticalStock > 0)
        {
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "Critical",
                Title = "Critical stock risk",
                Detail = $"{criticalStock} item(s) are at or below zero stock."
            });
        }
        else if (inventoryRisks.Count > 0)
        {
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "Warning",
                Title = "Low stock warning",
                Detail = $"{inventoryRisks.Count} item(s) are below their minimum stock level."
            });
        }

        if (security.TerminalChangesToday > 0)
        {
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "Info",
                Title = "Terminal administration changes today",
                Detail = $"{security.TerminalChangesToday} terminal change(s) recorded in audit logs.",
                ActionUrl = "/Terminals"
            });
        }

        return alerts
            .OrderBy(x => x.Severity == "Critical" ? 0 : x.Severity == "Warning" ? 1 : 2)
            .Take(_options.MaxAlerts)
            .ToList();
    }

    private static string FormatRelativeTime(DateTime? utc)
    {
        if (utc is null)
        {
            return "never";
        }

        var elapsed = DateTime.UtcNow - utc.Value;
        if (elapsed.TotalMinutes < 1)
        {
            return "just now";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{(int)elapsed.TotalMinutes} minutes ago";
        }

        return $"{(int)elapsed.TotalHours} hours ago";
    }

    private sealed record BranchBaseRow(int Id, string Name, bool IsActive);

    private sealed record TerminalGroupRow
    {
        public int BranchId { get; init; }

        public int Total { get; init; }

        public int Active { get; init; }
    }

    private sealed record OnlineTerminalGroupRow
    {
        public int BranchId { get; init; }

        public int Online { get; init; }
    }

    private sealed record CountByBranchRow
    {
        public int BranchId { get; init; }

        public int Count { get; init; }
    }

    private sealed record TodayOrderGroupRow
    {
        public int BranchId { get; init; }

        public int Completed { get; init; }

        public int Cancelled { get; init; }

        public decimal Sales { get; init; }
    }
}
