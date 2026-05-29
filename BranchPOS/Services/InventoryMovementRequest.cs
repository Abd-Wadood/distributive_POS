using BranchPOS.Models;

namespace BranchPOS.Services;

public sealed record InventoryMovementRequest(
    int BranchId,
    int InventoryItemId,
    int? FromLocationId,
    int? ToLocationId,
    decimal QuantityBase,
    decimal? UnitCostBase,
    decimal TotalCost,
    InventoryMovementType MovementType,
    string ReferenceType,
    int ReferenceId,
    int? UserSessionId,
    int? TerminalId,
    string? IdempotencyKey,
    string? CreatedByUserId,
    int? KitchenRequestDetailId = null,
    string? Note = null);
