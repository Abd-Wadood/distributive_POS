using System.ComponentModel.DataAnnotations;

namespace BranchPOS.DTOs;

public class CompletePreparationBatchDto
{
    [Required]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int UserSessionId { get; set; }

    [Range(1, int.MaxValue)]
    public int TerminalId { get; set; }

    [Required]
    public string TerminalCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PreparationRecipeId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999")]
    public decimal? OutputQuantityBase { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
