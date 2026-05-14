namespace BranchPOS.Models;

public class TerminalHeartbeat
{
    public int Id { get; set; }

    public int TerminalId { get; set; }

    public Terminal? Terminal { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public string? CurrentUserId { get; set; }

    public ApplicationUser? CurrentUser { get; set; }

    public int? CurrentSessionId { get; set; }

    public UserSession? CurrentSession { get; set; }
}
