using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BranchPOS.ViewModels;

public class PurchaseCreateViewModel
{
    public string IdempotencyKey { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Supplier is required.")]
    [Display(Name = "Supplier")]
    public int SupplierId { get; set; }

    [StringLength(80)]
    public string? InvoiceNumber { get; set; }

    public List<SelectListItem> Suppliers { get; set; } = new();

    public List<SelectListItem> InventoryItems { get; set; } = new();

    public List<PurchaseItemInputModel> Items { get; set; } = [new()];
}

public class PurchaseItemInputModel
{
    public int InventoryItemId { get; set; }

    [Range(typeof(decimal), "0.001", "1000000", ErrorMessage = "Purchase quantity must be greater than zero.")]
    public decimal? PurchaseQuantity { get; set; }

    [StringLength(80)]
    public string? PurchaseUnitName { get; set; }

    [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Conversion factor must be greater than zero when an ingredient is selected.")]
    public decimal? ConversionFactorToBase { get; set; }

    [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "Unit cost must be greater than zero.")]
    public decimal? UnitCostPerPurchaseUnit { get; set; }

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Total cost cannot be negative.")]
    public decimal? TotalCost { get; set; }

    public decimal? Quantity
    {
        get => PurchaseQuantity;
        set => PurchaseQuantity = value;
    }

    public decimal? UnitCost
    {
        get => UnitCostPerPurchaseUnit;
        set => UnitCostPerPurchaseUnit = value;
    }
}
