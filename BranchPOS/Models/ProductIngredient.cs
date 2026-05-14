namespace BranchPOS.Models;

public class ProductIngredient
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int IngredientId { get; set; }

    public Ingredient? Ingredient { get; set; }

    public decimal QuantityRequired { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
