using System.ComponentModel.DataAnnotations;
using BranchPOS.Validation;

namespace BranchPOS.DTOs;

public class CreatePurchaseDto
{
    [Required]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int BranchId { get; set; }

    [Range(1, int.MaxValue)]
    public int UserSessionId { get; set; }

    [Required]
    public string PerformedByUserId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int TerminalId { get; set; }

    [Required]
    public string TerminalCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int SupplierId { get; set; }

    public string? InvoiceNumber { get; set; }

    [Required]
    [MinCollectionCount(1, ErrorMessage = "Purchase must contain at least one item.")]
    public List<PurchaseItemDto> Items { get; set; } = new();
}
