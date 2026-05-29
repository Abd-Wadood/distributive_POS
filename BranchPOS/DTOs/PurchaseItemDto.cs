using System.ComponentModel.DataAnnotations;

namespace BranchPOS.DTOs;

public class PurchaseItemDto : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int InventoryItemId { get; set; }

    [Range(typeof(decimal), "0.001", "1000000")]
    public decimal PurchaseQuantity { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal UnitCostPerPurchaseUnit { get; set; }

    [StringLength(80)]
    public string? PurchaseUnitName { get; set; }

    [Range(typeof(decimal), "0.001", "1000000000")]
    public decimal? ConversionFactorToBase { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal? TotalCost { get; set; }

    public decimal Quantity
    {
        get => PurchaseQuantity;
        set => PurchaseQuantity = value;
    }

    public decimal UnitCost
    {
        get => UnitCostPerPurchaseUnit;
        set => UnitCostPerPurchaseUnit = value;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ConversionFactorToBase.HasValue && string.IsNullOrWhiteSpace(PurchaseUnitName))
        {
            yield return new ValidationResult(
                "Purchase unit is required when a conversion factor is supplied.",
                new[] { nameof(PurchaseUnitName) });
        }
    }
}
