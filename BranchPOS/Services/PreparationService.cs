using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class PreparationService : IPreparationService
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IInventoryTransactionService _inventoryTransactionService;

    public PreparationService(AppDbContext context, IBranchContextService branchContextService, IIdempotencyService idempotencyService, IInventoryTransactionService inventoryTransactionService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _idempotencyService = idempotencyService;
        _inventoryTransactionService = inventoryTransactionService;
    }

    public async Task<int> CompletePreparationBatchAsync(CompletePreparationBatchDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.UserSessionId <= 0 || string.IsNullOrWhiteSpace(dto.CreatedByUserId))
        {
            throw new BusinessException("Start or continue an active stock session before completing preparation batches.");
        }

        if (dto.TerminalId <= 0 || string.IsNullOrWhiteSpace(dto.TerminalCode))
        {
            throw new BusinessException("Terminal is not registered. Register this terminal before completing preparation batches.");
        }

        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await ValidateActiveStockSessionAsync(dto, branchId, cancellationToken);
        var idempotencyHash = _idempotencyService.HashPayload(new
        {
            BranchId = branchId,
            dto.UserSessionId,
            dto.CreatedByUserId,
            dto.TerminalId,
            TerminalCode = dto.TerminalCode.Trim(),
            dto.PreparationRecipeId,
            dto.OutputQuantityBase,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()
        });
        var idempotency = await _idempotencyService.BeginAsync(
            "PreparationBatch.Complete",
            dto.IdempotencyKey,
            idempotencyHash,
            dto.CreatedByUserId,
            branchId,
            dto.TerminalId,
            cancellationToken);
        if (!idempotency.IsOwner)
        {
            if (!string.IsNullOrWhiteSpace(idempotency.ErrorMessage))
            {
                throw new BusinessException(idempotency.ErrorMessage);
            }

            if (idempotency.Record.ResourceId.HasValue)
            {
                await transaction.CommitAsync(cancellationToken);
                return idempotency.Record.ResourceId.Value;
            }

            throw new BusinessException("This request is already being processed. Please wait.");
        }

        var stockRoom = await _inventoryTransactionService.GetOrCreateLocationAsync(branchId, "Stock Room", cancellationToken);
        var recipe = await _context.PreparationRecipes
            .Include(x => x.OutputInventoryItem)
            .Include(x => x.Ingredients)
            .ThenInclude(x => x.InventoryItem)
            .FirstOrDefaultAsync(x => x.Id == dto.PreparationRecipeId && x.BranchId == branchId && x.IsActive, cancellationToken)
            ?? throw new PosNotFoundException("Preparation recipe was not found.");

        ValidateRecipe(recipe, branchId);

        var outputQuantity = dto.OutputQuantityBase ?? recipe.OutputQuantityBase;
        if (outputQuantity <= 0)
        {
            throw new BusinessException("Preparation output quantity must be greater than zero.");
        }

        if (string.Equals(recipe.OutputInventoryItem!.BaseUnit, "Piece", StringComparison.OrdinalIgnoreCase) &&
            outputQuantity != decimal.Truncate(outputQuantity))
        {
            throw new BusinessException("Preparation output quantity must be a whole number for piece-based prepared items.");
        }

        var ingredientScale = outputQuantity / recipe.OutputQuantityBase;
        var requiredIngredients = recipe.Ingredients
            .Select(x => new RequiredIngredient(
                x.InventoryItemId,
                x.InventoryItem?.Name ?? "Ingredient",
                x.InventoryItem?.BaseUnit ?? string.Empty,
                x.QuantityBase * ingredientScale))
            .OrderBy(x => x.InventoryItemId)
            .ToList();

        var ingredientStocks = new Dictionary<int, InventoryStock>();
        foreach (var ingredient in requiredIngredients)
        {
            var stock = await LockStockAsync(branchId, ingredient.InventoryItemId, stockRoom.Id, cancellationToken)
                ?? throw new BusinessException($"Not enough stock room quantity for {ingredient.Name}. Required: {ingredient.Quantity:0.###} {ingredient.Unit}, Available: 0 {ingredient.Unit}.");

            if (stock.QuantityBase < ingredient.Quantity)
            {
                throw new BusinessException($"Not enough stock room quantity for {ingredient.Name}. Required: {ingredient.Quantity:0.###} {ingredient.Unit}, Available: {stock.QuantityBase:0.###} {ingredient.Unit}.");
            }

            ingredientStocks.Add(ingredient.InventoryItemId, stock);
        }

        var batch = new PreparationBatch
        {
            BranchId = branchId,
            PreparationRecipeId = recipe.Id,
            OutputInventoryItemId = recipe.OutputInventoryItemId,
            LocationId = stockRoom.Id,
            OutputQuantityBase = outputQuantity,
            Status = PreparationBatchStatus.Completed,
            UserSessionId = dto.UserSessionId,
            TerminalId = dto.TerminalId,
            TerminalCode = dto.TerminalCode.Trim(),
            IdempotencyKey = dto.IdempotencyKey,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()
        };
        _context.PreparationBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);

        decimal totalOutputCost = 0;
        foreach (var ingredient in requiredIngredients)
        {
            var stock = ingredientStocks[ingredient.InventoryItemId];
            var totalCost = ingredient.Quantity * stock.AverageUnitCostBase;
            totalOutputCost += totalCost;
            var mutation = await _inventoryTransactionService.DebitAsync(
                branchId,
                ingredient.InventoryItemId,
                stockRoom.Id,
                ingredient.Quantity,
                ingredient.Name,
                ingredient.Unit,
                "stock room",
                cancellationToken);
            _inventoryTransactionService.AddMovement(new InventoryMovementRequest(
                branchId,
                ingredient.InventoryItemId,
                stockRoom.Id,
                null,
                ingredient.Quantity,
                mutation.AverageUnitCostBase,
                totalCost,
                InventoryMovementType.Consumption,
                nameof(PreparationBatch),
                batch.Id,
                dto.UserSessionId,
                dto.TerminalId,
                dto.IdempotencyKey,
                dto.CreatedByUserId));
        }

        var outputUnitCost = totalOutputCost / outputQuantity;
        await _inventoryTransactionService.CreditAsync(branchId, recipe.OutputInventoryItemId, stockRoom.Id, outputQuantity, outputUnitCost, cancellationToken);
        _inventoryTransactionService.AddMovement(new InventoryMovementRequest(
            branchId,
            recipe.OutputInventoryItemId,
            null,
            stockRoom.Id,
            outputQuantity,
            outputUnitCost,
            totalOutputCost,
            InventoryMovementType.Production,
            nameof(PreparationBatch),
            batch.Id,
            dto.UserSessionId,
            dto.TerminalId,
            dto.IdempotencyKey,
            dto.CreatedByUserId));

        await _context.SaveChangesAsync(cancellationToken);
        await _idempotencyService.CompleteAsync(idempotency.Record, nameof(PreparationBatch), batch.Id, StatusCodes.Status200OK, batch.Id.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return batch.Id;
    }

    private async Task ValidateActiveStockSessionAsync(CompletePreparationBatchDto dto, int branchId, CancellationToken cancellationToken)
    {
        var session = await _context.UserSessions.FirstOrDefaultAsync(x =>
            x.Id == dto.UserSessionId &&
            x.UserId == dto.CreatedByUserId &&
            x.BranchId == branchId &&
            (x.Status == SessionStatus.Active || x.Status == SessionStatus.Reopened), cancellationToken);

        if (session is null)
        {
            throw new BusinessException("Start or continue an active stock session before completing preparation batches.");
        }

        if (!string.Equals(session.RoleName, "StockManager", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(session.RoleName, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Active stock session is required for completing preparation batches.");
        }

        if (session.TerminalId != dto.TerminalId ||
            !string.Equals(session.TerminalCode, dto.TerminalCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Terminal does not match the active stock session. Resume the correct session and try again.");
        }

        var terminalIsActive = await _context.Terminals.AnyAsync(x =>
            x.Id == dto.TerminalId &&
            x.BranchId == branchId &&
            x.TerminalCode == dto.TerminalCode &&
            x.IsActive, cancellationToken);

        if (!terminalIsActive)
        {
            throw new BusinessException("Terminal is not registered or is inactive. Register this terminal or contact an administrator.");
        }
    }

    private static void ValidateRecipe(PreparationRecipe recipe, int branchId)
    {
        if (recipe.OutputInventoryItem is null || recipe.OutputInventoryItem.BranchId != branchId)
        {
            throw new BusinessException("Preparation output item does not belong to the active branch.");
        }

        if (!recipe.OutputInventoryItem.IsActive)
        {
            throw new BusinessException("Preparation output item must be active.");
        }

        if (!recipe.OutputInventoryItem.IsPreparedItem)
        {
            throw new BusinessException("Preparation output item must be marked as a prepared item.");
        }

        if (recipe.OutputQuantityBase <= 0)
        {
            throw new BusinessException("Preparation output quantity must be greater than zero.");
        }

        if (recipe.Ingredients.Count == 0)
        {
            throw new BusinessException("Preparation recipe must contain at least one ingredient.");
        }

        if (recipe.Ingredients.GroupBy(x => x.InventoryItemId).Any(x => x.Count() > 1))
        {
            throw new BusinessException("Preparation recipe cannot contain duplicate input ingredients.");
        }

        foreach (var ingredient in recipe.Ingredients)
        {
            if (ingredient.QuantityBase <= 0)
            {
                throw new BusinessException("Preparation ingredient quantities must be greater than zero.");
            }

            if (ingredient.InventoryItemId == recipe.OutputInventoryItemId)
            {
                throw new BusinessException("Preparation input ingredient cannot be the same as the output item.");
            }

            if (ingredient.InventoryItem is null || ingredient.InventoryItem.BranchId != branchId)
            {
                throw new BusinessException("Preparation recipe contains an inventory item outside the active branch.");
            }

            if (!ingredient.InventoryItem.IsActive)
            {
                throw new BusinessException("Preparation input ingredient must be active.");
            }
        }
    }

    private async Task<InventoryStock?> LockStockAsync(int branchId, int inventoryItemId, int locationId, CancellationToken cancellationToken) =>
        await _context.InventoryStocks
            .FromSqlInterpolated($"SELECT *, xmin FROM \"InventoryStocks\" WHERE \"BranchId\" = {branchId} AND \"InventoryItemId\" = {inventoryItemId} AND \"InventoryLocationId\" = {locationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private sealed record RequiredIngredient(int InventoryItemId, string Name, string Unit, decimal Quantity);
}
