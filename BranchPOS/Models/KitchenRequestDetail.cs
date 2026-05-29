using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class KitchenRequestDetail
{
    public int Id { get; set; }

    public int KitchenRequestId { get; set; }

    public KitchenRequest? KitchenRequest { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public int? KitchenLocationId { get; set; }

    public InventoryLocation? KitchenLocation { get; set; }

    public KitchenRequestSource RequestSource { get; set; } = KitchenRequestSource.Manual;

    public decimal RequestedQuantity { get; set; }

    public decimal? ApprovedQuantity { get; set; }

    public decimal? DispatchedQuantity { get; set; }

    public decimal CurrentKitchenQuantityAtRequest { get; set; }

    public decimal MinimumKitchenLevelAtRequest { get; set; }

    public decimal RecommendedQuantity { get; set; }

    public decimal PendingRequestQuantity { get; set; }

    public decimal StockRoomAvailableAtRequest { get; set; }

    public KitchenRequestDetailStatus Status { get; set; } = KitchenRequestDetailStatus.PendingManagerReview;

    [MaxLength(300)]
    public string? Note { get; set; }
}
