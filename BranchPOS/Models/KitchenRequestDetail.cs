using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class KitchenRequestDetail
{
    public int Id { get; set; }

    public int KitchenRequestId { get; set; }

    public KitchenRequest? KitchenRequest { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public decimal RequestedQuantity { get; set; }

    public decimal? ApprovedQuantity { get; set; }

    public decimal? DispatchedQuantity { get; set; }

    [MaxLength(300)]
    public string? Note { get; set; }
}
