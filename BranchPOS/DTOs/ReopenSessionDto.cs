namespace BranchPOS.DTOs;

public class ReopenSessionDto
{
    public int SessionId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int TerminalId { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
