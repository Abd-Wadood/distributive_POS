using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class ManualKitchenUsage
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public DateTime UsageDate { get; set; } = DateTime.UtcNow.Date;

    public int? UserSessionId { get; set; }

    public UserSession? UserSession { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public decimal OpeningKitchenQuantity { get; set; }

    public decimal ReceivedFromStockRoomQuantity { get; set; }

    public decimal ClosingKitchenQuantity { get; set; }

    public decimal WastedQuantity { get; set; }

    public decimal ActualUsedQuantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
