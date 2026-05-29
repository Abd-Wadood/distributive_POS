using System.ComponentModel.DataAnnotations;

namespace BranchPOS.DTOs;

public class RejectInventoryAdjustmentDto
{
    public int AdjustmentId { get; set; }

    [Required, MaxLength(500)]
    public string RejectionReason { get; set; } = string.Empty;
}
