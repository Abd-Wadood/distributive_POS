namespace BranchPOS.Services;

public class PosOperationalOptions
{
    public TimeSpan SessionStaleTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan HeartbeatWriteInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan TerminalOfflineThreshold { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan TerminalCookieExpiration { get; set; } = TimeSpan.FromDays(180);

    public bool RequireSecureTerminalCookieInProduction { get; set; } = true;

    public bool AllowSessionResumeFromDifferentTerminal { get; set; }
}
