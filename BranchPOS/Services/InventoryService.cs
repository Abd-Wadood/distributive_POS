using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private const decimal MaxInventoryMovementQuantity = 1_000_000m;

    public InventoryService(AppDbContext context, IBranchContextService branchContextService)
    {
        _context = context;
        _branchContextService = branchContextService;
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
            throw new InvalidOperationException("Active stock session is required for inventory adjustments.");
        }

        if (dto.TerminalId <= 0 || string.IsNullOrWhiteSpace(dto.TerminalCode))
        {
            throw new InvalidOperationException("Terminal is not registered.");
        }

        if (dto.IngredientId <= 0)
        {
            throw new InvalidOperationException("Selected ingredient is invalid.");
        }

        if (dto.QuantityChanged == 0)
        {
            throw new InvalidOperationException("Adjustment quantity cannot be zero.");
        }

        if (Math.Abs(dto.QuantityChanged) > MaxInventoryMovementQuantity)
        {
            throw new InvalidOperationException("Adjustment quantity is too large.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await ValidateActiveStockSessionAsync(dto, cancellationToken);

        var ingredientExists = await _context.Ingredients.AnyAsync(x =>
            x.Id == dto.IngredientId &&
            x.BranchId == dto.BranchId, cancellationToken);
        if (!ingredientExists)
        {
            throw new InvalidOperationException("Selected ingredient does not belong to the active branch session.");
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
            throw new InvalidOperationException("Adjustment would make stock negative.");
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
            TransactionType = InventoryTransactionType.Adjustment,
            QuantityChanged = dto.QuantityChanged
        });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ValidateActiveStockSessionAsync(InventoryAdjustmentDto dto, CancellationToken cancellationToken)
    {
        var session = await _context.UserSessions.FirstOrDefaultAsync(x =>
            x.Id == dto.UserSessionId &&
            x.UserId == dto.PerformedByUserId &&
            x.BranchId == dto.BranchId &&
            x.Status == SessionStatus.Active, cancellationToken)
            ?? throw new InvalidOperationException("Active stock session is required for inventory adjustments.");

        if (!string.Equals(session.RoleName, "StockManager", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(session.RoleName, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Active stock session is required for inventory adjustments.");
        }

        if (session.TerminalId != dto.TerminalId ||
            !string.Equals(session.TerminalCode, dto.TerminalCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Terminal does not match the active stock session.");
        }

        var terminalIsActive = await _context.Terminals.AnyAsync(x =>
            x.Id == dto.TerminalId &&
            x.BranchId == dto.BranchId &&
            x.TerminalCode == dto.TerminalCode &&
            x.IsActive, cancellationToken);

        if (!terminalIsActive)
        {
            throw new InvalidOperationException("Terminal is not registered or is inactive.");
        }
    }
}
