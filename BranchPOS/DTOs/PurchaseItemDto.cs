namespace BranchPOS.DTOs;

public class PurchaseItemDto
{
    public int IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }
}
