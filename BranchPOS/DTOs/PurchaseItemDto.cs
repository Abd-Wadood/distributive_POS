using System.ComponentModel.DataAnnotations;

namespace BranchPOS.DTOs;

public class PurchaseItemDto
{
    public int InventoryItemId { get; set; }

    [Range(typeof(decimal), "0.001", "1000000")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal UnitCost { get; set; }
}
