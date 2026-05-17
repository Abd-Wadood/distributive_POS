namespace BranchPOS.DTOs;

public class CloseSessionDto
{
    public string IdempotencyKey { get; set; } = string.Empty;

    public int SessionId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int TerminalId { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    public decimal CountedClosingCash { get; set; }

    public string ConfirmationText { get; set; } = string.Empty;

    public bool IsManagerOrAdmin { get; set; }

    public bool ForceClose { get; set; }
}
