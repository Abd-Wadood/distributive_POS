using System.ComponentModel.DataAnnotations;

namespace BranchPOS.DTOs;

public class CreateManualKitchenUsageDto
{
    [Required]
    public DateTime UsageDate { get; set; } = DateTime.UtcNow.Date;

    public int? UserSessionId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Inventory item is required.")]
    public int InventoryItemId { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal OpeningKitchenQuantity { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal ReceivedFromStockRoomQuantity { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal ClosingKitchenQuantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
