namespace BranchPOS.Services;

public class DashboardOptions
{
    public int CacheSeconds { get; set; } = 30;

    public int TerminalOnlineSeconds { get; set; } = 60;

    public int TerminalStaleSeconds { get; set; } = 300;

    public int MaxRecentSessions { get; set; } = 10;

    public int MaxAlerts { get; set; } = 10;

    public int MaxInventoryRisks { get; set; } = 10;

    public int MaxTerminals { get; set; } = 10;
}
