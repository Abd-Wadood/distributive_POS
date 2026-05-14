using Microsoft.AspNetCore.Mvc.Rendering;

namespace BranchPOS.ViewModels;

public class PurchaseCreateViewModel
{
    public int SupplierId { get; set; }

    public List<SelectListItem> Suppliers { get; set; } = new();

    public List<SelectListItem> Ingredients { get; set; } = new();

    public List<PurchaseItemInputModel> Items { get; set; } = [new(), new(), new(), new(), new()];
}

public class PurchaseItemInputModel
{
    public int IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }
}
