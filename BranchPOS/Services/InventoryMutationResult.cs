namespace BranchPOS.Services;

public sealed record InventoryMutationResult(decimal PreviousQuantity, decimal NewQuantity, decimal AverageUnitCostBase);
