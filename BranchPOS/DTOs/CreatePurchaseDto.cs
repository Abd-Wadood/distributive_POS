namespace BranchPOS.DTOs;

public class CreatePurchaseDto
{
    public string IdempotencyKey { get; set; } = string.Empty;

    public int BranchId { get; set; }

    public int UserSessionId { get; set; }

    public string PerformedByUserId { get; set; } = string.Empty;

    public int TerminalId { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    public int SupplierId { get; set; }

    public string? InvoiceNumber { get; set; }

    public List<PurchaseItemDto> Items { get; set; } = new();
}
