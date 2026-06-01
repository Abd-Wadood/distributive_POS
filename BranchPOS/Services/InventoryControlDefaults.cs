using BranchPOS.Models;

namespace BranchPOS.Services;

public static class InventoryControlDefaults
{
    public static void ApplyDefaults(InventoryItem item)
    {
        var mode = item.ConsumptionMode;
        item.IsExpenseOnly = mode == ConsumptionMode.ExpenseOnly;
        item.IsStockTracked = mode != ConsumptionMode.ExpenseOnly;
        item.AllowRecipeConsumption = mode == ConsumptionMode.RecipeConsumption;
        item.AllowManualConsumption = mode == ConsumptionMode.ManualKitchenIssue;
        item.AllowKitchenDispatch = mode is ConsumptionMode.RecipeConsumption or ConsumptionMode.ManualKitchenIssue;
        item.RequirePurchaseConversion = mode is ConsumptionMode.RecipeConsumption or ConsumptionMode.DirectSale;
        item.TrackingLevel = mode switch
        {
            ConsumptionMode.RecipeConsumption => TrackingLevel.High,
            ConsumptionMode.ManualKitchenIssue => TrackingLevel.Medium,
            ConsumptionMode.PeriodicCount => TrackingLevel.Low,
            ConsumptionMode.ExpenseOnly => TrackingLevel.Low,
            ConsumptionMode.DirectSale => TrackingLevel.High,
            _ => item.TrackingLevel
        };

        if (mode == ConsumptionMode.ExpenseOnly)
        {
            item.ReorderLevel = 0;
            item.MinimumKitchenLevel = null;
            item.MaximumKitchenLevel = null;
        }
    }

    public static bool CanUseInRecipe(InventoryItem item) =>
        item.IsStockTracked &&
        !item.IsExpenseOnly &&
        item.ConsumptionMode == ConsumptionMode.RecipeConsumption &&
        item.AllowRecipeConsumption;

    public static bool CanDispatchToKitchen(InventoryItem item) =>
        item.IsStockTracked &&
        !item.IsExpenseOnly &&
        item.AllowKitchenDispatch &&
        item.ConsumptionMode is ConsumptionMode.RecipeConsumption or ConsumptionMode.ManualKitchenIssue or ConsumptionMode.PeriodicCount;

    public static bool NeedsPurchaseConversion(InventoryItem item) =>
        item.IsStockTracked && item.RequirePurchaseConversion;
}
