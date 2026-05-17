using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BranchPOS.ViewModels;

public class PurchaseCreateViewModel
{
    public string IdempotencyKey { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Supplier is required.")]
    public int SupplierId { get; set; }

    [StringLength(80)]
    public string? InvoiceNumber { get; set; }

    public List<SelectListItem> Suppliers { get; set; } = new();

    public List<SelectListItem> Ingredients { get; set; } = new();

    public List<PurchaseItemInputModel> Items { get; set; } = [new(), new(), new(), new(), new()];
}

public class PurchaseItemInputModel
{
    public int IngredientId { get; set; }

    [Range(typeof(decimal), "0", "1000000", ErrorMessage = "Quantity must be greater than zero when an ingredient is selected.")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Unit cost cannot be negative.")]
    public decimal UnitCost { get; set; }
}
