using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IInventoryTransactionService
{
    Task<InventoryLocation> GetOrCreateLocationAsync(int branchId, string name, CancellationToken cancellationToken = default);

    Task<InventoryMutationResult> DebitAsync(
        int branchId,
        int inventoryItemId,
        int locationId,
        decimal quantityBase,
        string shortageItemName,
        string shortageUnit,
        string locationName,
        CancellationToken cancellationToken = default);

    Task<InventoryMutationResult> CreditAsync(
        int branchId,
        int inventoryItemId,
        int locationId,
        decimal quantityBase,
        decimal unitCostBase,
        CancellationToken cancellationToken = default);

    void AddMovement(InventoryMovementRequest request);
}
