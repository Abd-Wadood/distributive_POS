using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class KitchenRequest
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    [MaxLength(40)]
    public string RequestNumber { get; set; } = string.Empty;

    public KitchenRequestStatus Status { get; set; } = KitchenRequestStatus.Pending;

    public string? RequestedByUserId { get; set; }

    public ApplicationUser? RequestedByUser { get; set; }

    public string? ApprovedByUserId { get; set; }

    public ApplicationUser? ApprovedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; set; }

    public DateTime? DispatchedAt { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public List<KitchenRequestDetail> Details { get; set; } = new();
}
