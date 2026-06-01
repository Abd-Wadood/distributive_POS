using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BranchPOS.Models;

public class InventoryItem
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string BaseUnit { get; set; } = "Piece";

    [MaxLength(80)]
    public string? PurchaseUnitName { get; set; }

    [Range(typeof(decimal), "0.001", "1000000000")]
    public decimal? DefaultConversionFactorToBase { get; set; }

    public ConsumptionMode ConsumptionMode { get; set; } = ConsumptionMode.RecipeConsumption;

    public TrackingLevel TrackingLevel { get; set; } = TrackingLevel.High;

    public bool AllowRecipeConsumption { get; set; } = true;

    public bool AllowManualConsumption { get; set; }

    public bool AllowKitchenDispatch { get; set; } = true;

    public bool RequirePurchaseConversion { get; set; } = true;

    public bool IsStockTracked { get; set; } = true;

    public bool IsExpenseOnly { get; set; }

    public bool ExpiryTrackingRequired { get; set; }

    public bool BatchTrackingRequired { get; set; }

    public decimal ReorderLevel { get; set; }

    public decimal? MinimumKitchenLevel { get; set; }

    public decimal? MaximumKitchenLevel { get; set; }

    [NotMapped]
    public string Unit
    {
        get => BaseUnit;
        set => BaseUnit = value;
    }

    public bool IsActive { get; set; } = true;

    public bool IsPreparedItem { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InventoryStock> Stocks { get; set; } = new List<InventoryStock>();

    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
