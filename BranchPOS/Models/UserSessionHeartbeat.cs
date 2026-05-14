namespace BranchPOS.Models;

public class UserSessionHeartbeat
{
    public int Id { get; set; }

    public int UserSessionId { get; set; }

    public UserSession? UserSession { get; set; }

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public string TerminalName { get; set; } = string.Empty;
}
