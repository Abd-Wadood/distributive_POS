using BranchPOS.Data;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class InventoryTransactionService : IInventoryTransactionService
{
    private readonly AppDbContext _context;

    public InventoryTransactionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryLocation> GetOrCreateLocationAsync(int branchId, string name, CancellationToken cancellationToken = default)
    {
        var location = await _context.InventoryLocations
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.Name == name, cancellationToken);
        if (location is not null)
        {
            return location;
        }

        location = new InventoryLocation { BranchId = branchId, Name = name };
        _context.InventoryLocations.Add(location);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DatabaseErrorTranslator.IsUniqueViolation(ex))
        {
            _context.Entry(location).State = EntityState.Detached;
            location = await _context.InventoryLocations
                .FirstAsync(x => x.BranchId == branchId && x.Name == name, cancellationToken);
        }

        return location;
    }

    public async Task<InventoryMutationResult> DebitAsync(
        int branchId,
        int inventoryItemId,
        int locationId,
        decimal quantityBase,
        string shortageItemName,
        string shortageUnit,
        string locationName,
        CancellationToken cancellationToken = default)
    {
        if (quantityBase <= 0)
        {
            throw new PosValidationException("Inventory deduction quantity must be greater than zero.");
        }

        var stock = await LockStockAsync(branchId, inventoryItemId, locationId, cancellationToken);
        var available = stock?.QuantityBase ?? 0m;
        if (stock is null || available < quantityBase)
        {
            throw new BusinessException(
                $"Not enough {locationName} quantity for {shortageItemName}. Required: {quantityBase:0.###} {shortageUnit}, Available: {available:0.###} {shortageUnit}.");
        }

        var updatedAt = DateTime.UtcNow;
        var affected = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "InventoryStocks"
            SET "QuantityBase" = "QuantityBase" - {quantityBase}, "UpdatedAt" = {updatedAt}
            WHERE "BranchId" = {branchId}
              AND "InventoryItemId" = {inventoryItemId}
              AND "InventoryLocationId" = {locationId}
              AND "QuantityBase" >= {quantityBase}
            """, cancellationToken);

        if (affected != 1)
        {
            var current = await _context.InventoryStocks
                .AsNoTracking()
                .Where(x => x.BranchId == branchId && x.InventoryItemId == inventoryItemId && x.InventoryLocationId == locationId)
                .Select(x => (decimal?)x.QuantityBase)
                .SingleOrDefaultAsync(cancellationToken) ?? 0m;
            throw new BusinessException(
                $"Not enough {locationName} quantity for {shortageItemName}. Required: {quantityBase:0.###} {shortageUnit}, Available: {current:0.###} {shortageUnit}.");
        }

        _context.Entry(stock).State = EntityState.Detached;
        return new InventoryMutationResult(available, available - quantityBase, stock.AverageUnitCostBase);
    }

    public async Task<InventoryMutationResult> CreditAsync(
        int branchId,
        int inventoryItemId,
        int locationId,
        decimal quantityBase,
        decimal unitCostBase,
        CancellationToken cancellationToken = default)
    {
        if (quantityBase <= 0)
        {
            throw new PosValidationException("Inventory increase quantity must be greater than zero.");
        }

        var stock = await LockStockAsync(branchId, inventoryItemId, locationId, cancellationToken);
        if (stock is null)
        {
            stock = new InventoryStock
            {
                BranchId = branchId,
                InventoryItemId = inventoryItemId,
                InventoryLocationId = locationId,
                QuantityBase = quantityBase,
                AverageUnitCostBase = unitCostBase
            };
            _context.InventoryStocks.Add(stock);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return new InventoryMutationResult(0, quantityBase, unitCostBase);
            }
            catch (DbUpdateException ex) when (DatabaseErrorTranslator.IsUniqueViolation(ex))
            {
                _context.Entry(stock).State = EntityState.Detached;
                stock = await LockStockAsync(branchId, inventoryItemId, locationId, cancellationToken)
                    ?? throw new BusinessException("Inventory balance could not be created. Please retry.");
            }
        }

        var previousQuantity = stock.QuantityBase;
        var weightedAverage = CalculateWeightedAverage(previousQuantity, stock.AverageUnitCostBase, quantityBase, unitCostBase);
        var updatedAt = DateTime.UtcNow;
        var affected = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "InventoryStocks"
            SET "QuantityBase" = "QuantityBase" + {quantityBase},
                "AverageUnitCostBase" = {weightedAverage},
                "UpdatedAt" = {updatedAt}
            WHERE "BranchId" = {branchId}
              AND "InventoryItemId" = {inventoryItemId}
              AND "InventoryLocationId" = {locationId}
            """, cancellationToken);

        if (affected != 1)
        {
            throw new BusinessException("Inventory balance could not be updated. Please retry.");
        }

        _context.Entry(stock).State = EntityState.Detached;
        return new InventoryMutationResult(previousQuantity, previousQuantity + quantityBase, weightedAverage);
    }

    public void AddMovement(InventoryMovementRequest request)
    {
        if (request.QuantityBase <= 0)
        {
            throw new PosValidationException("Inventory movement quantity must be greater than zero.");
        }

        _context.InventoryMovements.Add(new InventoryMovement
        {
            BranchId = request.BranchId,
            InventoryItemId = request.InventoryItemId,
            FromLocationId = request.FromLocationId,
            ToLocationId = request.ToLocationId,
            QuantityBase = request.QuantityBase,
            UnitCostBase = request.UnitCostBase,
            TotalCost = request.TotalCost,
            MovementType = request.MovementType,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
            KitchenRequestDetailId = request.KitchenRequestDetailId,
            UserSessionId = request.UserSessionId,
            TerminalId = request.TerminalId,
            IdempotencyKey = request.IdempotencyKey,
            CreatedByUserId = request.CreatedByUserId,
            Note = request.Note
        });
    }

    private async Task<InventoryStock?> LockStockAsync(int branchId, int inventoryItemId, int locationId, CancellationToken cancellationToken) =>
        await _context.InventoryStocks
            .FromSqlInterpolated($"SELECT *, xmin FROM \"InventoryStocks\" WHERE \"BranchId\" = {branchId} AND \"InventoryItemId\" = {inventoryItemId} AND \"InventoryLocationId\" = {locationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private static decimal CalculateWeightedAverage(decimal oldQuantity, decimal oldAverageCost, decimal incomingQuantity, decimal incomingCost)
    {
        var totalQuantity = oldQuantity + incomingQuantity;
        return totalQuantity <= 0 ? incomingCost : ((oldQuantity * oldAverageCost) + (incomingQuantity * incomingCost)) / totalQuantity;
    }
}
