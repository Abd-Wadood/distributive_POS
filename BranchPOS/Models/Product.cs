using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class Product
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public int? DirectInventoryItemId { get; set; }

    public InventoryItem? DirectInventoryItem { get; set; }

    public decimal? DirectQuantityBase { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
