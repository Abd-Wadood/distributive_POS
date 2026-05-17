using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IIdempotencyService _idempotencyService;
    private const decimal MaxInventoryMovementQuantity = 1_000_000m;

    public InventoryService(AppDbContext context, IBranchContextService branchContextService, IIdempotencyService idempotencyService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _idempotencyService = idempotencyService;
    }

    public async Task<List<Inventory>> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        return await _context.Inventories
            .Include(x => x.Ingredient)
            .Where(x => x.BranchId == branchId)
            .OrderBy(x => x.Ingredient!.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AdjustInventoryAsync(InventoryAdjustmentDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.BranchId <= 0 || dto.UserSessionId <= 0 || string.IsNullOrWhiteSpace(dto.PerformedByUserId))
        {
            throw new BusinessException("Start or continue an active stock session before adjusting inventory.");
        }

        if (dto.TerminalId <= 0 || string.IsNullOrWhiteSpace(dto.TerminalCode))
        {
            throw new BusinessException("Terminal is not registered. Register this terminal before adjusting stock.");
        }

        if (dto.IngredientId <= 0)
        {
            throw new PosValidationException("Selected ingredient is invalid.");
        }

        if (dto.QuantityChanged == 0)
        {
            throw new PosValidationException("Adjustment quantity cannot be zero.");
        }

        if (Math.Abs(dto.QuantityChanged) > MaxInventoryMovementQuantity)
        {
            throw new PosValidationException("Adjustment quantity is too large.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await ValidateActiveStockSessionAsync(dto, cancellationToken);
        var idempotencyHash = _idempotencyService.HashPayload(new
        {
            dto.BranchId,
            dto.UserSessionId,
            dto.PerformedByUserId,
            dto.TerminalId,
            dto.IngredientId,
            dto.QuantityChanged,
            dto.Reason
        });
        var idempotency = await _idempotencyService.BeginAsync("InventoryAdjustment", dto.IdempotencyKey, idempotencyHash, dto.PerformedByUserId, dto.BranchId, dto.TerminalId, cancellationToken);
        if (!idempotency.IsOwner)
        {
            if (!string.IsNullOrWhiteSpace(idempotency.ErrorMessage))
            {
                throw new BusinessException(idempotency.ErrorMessage);
            }

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var ingredientExists = await _context.Ingredients.AnyAsync(x =>
            x.Id == dto.IngredientId &&
            x.BranchId == dto.BranchId, cancellationToken);
        if (!ingredientExists)
        {
            throw new BusinessException("Selected ingredient does not belong to the active branch session.");
        }

        var inventory = await _context.Inventories
            .FromSqlInterpolated($"SELECT * FROM \"Inventories\" WHERE \"BranchId\" = {dto.BranchId} AND \"IngredientId\" = {dto.IngredientId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (inventory is null)
        {
            inventory = new Inventory { BranchId = dto.BranchId, IngredientId = dto.IngredientId };
            _context.Inventories.Add(inventory);
        }

        if (inventory.CurrentQuantity + dto.QuantityChanged < 0)
        {
            throw new BusinessException("Adjustment would make stock negative.");
        }

        inventory.CurrentQuantity += dto.QuantityChanged;
        _context.InventoryTransactions.Add(new InventoryTransaction
        {
            BranchId = dto.BranchId,
            IngredientId = dto.IngredientId,
            UserSessionId = dto.UserSessionId,
            PerformedByUserId = dto.PerformedByUserId,
            TerminalId = dto.TerminalId,
            TerminalCode = dto.TerminalCode,
            IdempotencyKey = dto.IdempotencyKey,
            TransactionType = InventoryTransactionType.Adjustment,
            QuantityChanged = dto.QuantityChanged
        });

        await _context.SaveChangesAsync(cancellationToken);
        var transactionId = await _context.InventoryTransactions
            .Where(x => x.IdempotencyKey == dto.IdempotencyKey)
            .Select(x => x.Id)
            .FirstAsync(cancellationToken);
        await _idempotencyService.CompleteAsync(idempotency.Record, nameof(InventoryTransaction), transactionId, StatusCodes.Status200OK, transactionId.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ValidateActiveStockSessionAsync(InventoryAdjustmentDto dto, CancellationToken cancellationToken)
    {
        var session = await _context.UserSessions.FirstOrDefaultAsync(x =>
            x.Id == dto.UserSessionId &&
            x.UserId == dto.PerformedByUserId &&
            x.BranchId == dto.BranchId &&
            (x.Status == SessionStatus.Active || x.Status == SessionStatus.Reopened), cancellationToken)
            ?? throw new BusinessException("Start or continue an active stock session before adjusting inventory.");

        if (!string.Equals(session.RoleName, "StockManager", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Active stock session is required for inventory adjustments.");
        }

        if (session.TerminalId != dto.TerminalId ||
            !string.Equals(session.TerminalCode, dto.TerminalCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Terminal does not match the active stock session. Resume the correct session and try again.");
        }

        var terminalIsActive = await _context.Terminals.AnyAsync(x =>
            x.Id == dto.TerminalId &&
            x.BranchId == dto.BranchId &&
            x.TerminalCode == dto.TerminalCode &&
            x.IsActive, cancellationToken);

        if (!terminalIsActive)
        {
            throw new BusinessException("Terminal is not registered or is inactive. Register this terminal or contact an administrator.");
        }
    }
}
