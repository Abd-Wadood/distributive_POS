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

    public KitchenRequestSource RequestSource { get; set; } = KitchenRequestSource.Manual;

    public KitchenRequestAutoReason AutoReason { get; set; } = KitchenRequestAutoReason.None;

    public int? KitchenLocationId { get; set; }

    public InventoryLocation? KitchenLocation { get; set; }

    public string? RequestedByUserId { get; set; }

    public ApplicationUser? RequestedByUser { get; set; }

    public string? ApprovedByUserId { get; set; }

    public ApplicationUser? ApprovedByUser { get; set; }

    public string? ReviewedByUserId { get; set; }

    public ApplicationUser? ReviewedByUser { get; set; }

    public string? DispatchedByUserId { get; set; }

    public ApplicationUser? DispatchedByUser { get; set; }

    public int? CreatedByTerminalId { get; set; }

    public Terminal? CreatedByTerminal { get; set; }

    public int? CreatedBySessionId { get; set; }

    public UserSession? CreatedBySession { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime? DispatchedAt { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    [MaxLength(500)]
    public string? ManagerNotes { get; set; }

    public List<KitchenRequestDetail> Details { get; set; } = new();
}
