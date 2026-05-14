namespace BranchPOS.Models;

public class PurchaseItem
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int PurchaseId { get; set; }

    public Purchase? Purchase { get; set; }

    public int IngredientId { get; set; }

    public Ingredient? Ingredient { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
