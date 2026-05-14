namespace BranchPOS.DTOs;

public class StartSessionDto
{
    public string UserId { get; set; } = string.Empty;

    public int BranchId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string TerminalName { get; set; } = string.Empty;

    public int TerminalId { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
