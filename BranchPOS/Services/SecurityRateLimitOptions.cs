namespace BranchPOS.Services;

public class SecurityRateLimitOptions
{
    public int LoginIpPermitLimit { get; set; } = 20;

    public int LoginFailedPermitLimit { get; set; } = 5;

    public int LoginIpFailedPermitLimit { get; set; } = 25;

    public int OrderFinalizePermitLimit { get; set; } = 2;

    public int ProductSearchPermitLimit { get; set; } = 20;

    public int SessionStartPermitLimit { get; set; } = 3;

    public int TerminalHeartbeatPermitLimit { get; set; } = 2;

    public int ReportsPermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;

    public int OrderFinalizeWindowSeconds { get; set; } = 1;

    public int HeartbeatWindowSeconds { get; set; } = 30;

    public int MaxReportPageSize { get; set; } = 200;

    public int DefaultReportPageSize { get; set; } = 100;

    public int MaxProductSearchResults { get; set; } = 50;

    public int ProductSearchMinimumLength { get; set; } = 2;
}
