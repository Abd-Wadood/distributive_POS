namespace BranchPOS.Models;

public class Inventory
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int IngredientId { get; set; }

    public Ingredient? Ingredient { get; set; }

    public decimal CurrentQuantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
