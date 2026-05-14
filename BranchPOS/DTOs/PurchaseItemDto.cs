using System.ComponentModel.DataAnnotations;

namespace BranchPOS.DTOs;

public class PurchaseItemDto
{
    [Range(1, int.MaxValue)]
    public int IngredientId { get; set; }

    [Range(typeof(decimal), "0.001", "1000000")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal UnitCost { get; set; }
}
