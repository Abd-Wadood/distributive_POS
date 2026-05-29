using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BranchPOS.Models;

public class PurchaseItem
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int PurchaseId { get; set; }

    public Purchase? Purchase { get; set; }

    public int? InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    [MaxLength(80)]
    public string PurchaseUnitName { get; set; } = string.Empty;

    public decimal PurchaseQuantity { get; set; }

    public decimal ConversionFactorToBase { get; set; }

    public decimal BaseQuantity { get; set; }

    public decimal UnitCostPerPurchaseUnit { get; set; }

    public decimal UnitCostBase { get; set; }

    public decimal TotalCost { get; set; }

    [NotMapped]
    public decimal Quantity
    {
        get => PurchaseQuantity;
        set => PurchaseQuantity = value;
    }

    [NotMapped]
    public decimal UnitCost
    {
        get => UnitCostPerPurchaseUnit;
        set => UnitCostPerPurchaseUnit = value;
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
