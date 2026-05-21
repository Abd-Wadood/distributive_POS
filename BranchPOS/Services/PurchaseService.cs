using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BranchPOS.Services;

public class PurchaseService : IPurchaseService
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IIdempotencyService _idempotencyService;
    private const decimal MaxPurchaseItemQuantity = 1_000_000m;

    public PurchaseService(AppDbContext context, IBranchContextService branchContextService, IIdempotencyService idempotencyService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _idempotencyService = idempotencyService;
    }

    public async Task<List<Purchase>> GetPurchasesAsync(CancellationToken cancellationToken = default)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        return await _context.Purchases
            .Include(x => x.Supplier)
            .Include(x => x.Items)
            .ThenInclude(x => x.InventoryItem)
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreatePurchaseAsync(CreatePurchaseDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.BranchId <= 0 || dto.UserSessionId <= 0 || string.IsNullOrWhiteSpace(dto.PerformedByUserId))
        {
            throw new BusinessException("Start or continue an active stock session before creating purchases.");
        }

        if (dto.TerminalId <= 0 || string.IsNullOrWhiteSpace(dto.TerminalCode))
        {
            throw new BusinessException("Terminal is not registered. Register this terminal before creating purchases.");
        }

        if (dto.Items.Count == 0 || dto.Items.Any(x => x.InventoryItemId <= 0 || x.Quantity <= 0 || x.UnitCost < 0))
        {
            throw new PosValidationException("Purchase must contain valid item quantities and costs.");
        }

        if (dto.Items.Any(x => x.Quantity > MaxPurchaseItemQuantity))
        {
            throw new PosValidationException("Purchase quantity is too large.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await ValidateActiveStockSessionAsync(dto, cancellationToken);
        var idempotencyHash = _idempotencyService.HashPayload(new
        {
            dto.BranchId,
            dto.UserSessionId,
            dto.PerformedByUserId,
            dto.TerminalId,
            dto.SupplierId,
            dto.InvoiceNumber,
                Items = dto.Items.OrderBy(x => x.InventoryItemId).Select(x => new { x.InventoryItemId, x.Quantity, x.UnitCost })
        });
        var idempotency = await _idempotencyService.BeginAsync("PurchaseCreate", dto.IdempotencyKey, idempotencyHash, dto.PerformedByUserId, dto.BranchId, dto.TerminalId, cancellationToken);
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

        var inventoryItemIds = dto.Items.Select(x => x.InventoryItemId).Distinct().ToList();
        var validInventoryItemIds = await _context.InventoryItems
            .Where(x => x.BranchId == dto.BranchId && x.IsActive && inventoryItemIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (validInventoryItemIds.Count != inventoryItemIds.Count)
        {
            throw new BusinessException("Selected inventory item does not belong to the active branch session.");
        }

        var purchase = new Purchase
        {
            BranchId = dto.BranchId,
            UserSessionId = dto.UserSessionId,
            PerformedByUserId = dto.PerformedByUserId,
            TerminalId = dto.TerminalId,
            TerminalCode = dto.TerminalCode,
            SupplierId = dto.SupplierId,
            InvoiceNumber = string.IsNullOrWhiteSpace(dto.InvoiceNumber) ? null : dto.InvoiceNumber.Trim(),
            IdempotencyKey = dto.IdempotencyKey
        };

        foreach (var item in dto.Items)
        {
            purchase.Items.Add(new PurchaseItem
            {
                BranchId = dto.BranchId,
                InventoryItemId = item.InventoryItemId,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost
            });
        }

        _context.Purchases.Add(purchase);
        await _context.SaveChangesAsync(cancellationToken);

        var stockRoom = await GetOrCreateLocationAsync(dto.BranchId, "Stock Room", cancellationToken);
        foreach (var item in dto.Items)
        {
            var stock = await LockStockAsync(dto.BranchId, item.InventoryItemId, stockRoom.Id, cancellationToken);
            if (stock is null)
            {
                stock = new InventoryStock
                {
                    BranchId = dto.BranchId,
                    InventoryItemId = item.InventoryItemId,
                    InventoryLocationId = stockRoom.Id
                };
                _context.InventoryStocks.Add(stock);
            }

            stock.AverageUnitCost = CalculateWeightedAverage(stock.Quantity, stock.AverageUnitCost, item.Quantity, item.UnitCost);
            stock.Quantity += item.Quantity;
            _context.InventoryMovements.Add(new InventoryMovement
            {
                BranchId = dto.BranchId,
                InventoryItemId = item.InventoryItemId,
                ToLocationId = stockRoom.Id,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                TotalCost = item.Quantity * item.UnitCost,
                MovementType = InventoryMovementType.Purchase,
                ReferenceType = nameof(Purchase),
                ReferenceId = purchase.Id,
                CreatedByUserId = dto.PerformedByUserId
            });
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new BusinessException("Inventory was already created for one of the selected ingredients. Refresh the page and try again.", innerException: ex);
        }

        await _idempotencyService.CompleteAsync(idempotency.Record, nameof(Purchase), purchase.Id, StatusCodes.Status200OK, purchase.Id.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return purchase.Id;
    }

    private async Task ValidateActiveStockSessionAsync(CreatePurchaseDto dto, CancellationToken cancellationToken)
    {
        var session = await _context.UserSessions.FirstOrDefaultAsync(x =>
            x.Id == dto.UserSessionId &&
            x.UserId == dto.PerformedByUserId &&
            x.BranchId == dto.BranchId &&
            (x.Status == SessionStatus.Active || x.Status == SessionStatus.Reopened), cancellationToken)
            ?? throw new BusinessException("Start or continue an active stock session before creating purchases.");

        if (!string.Equals(session.RoleName, "StockManager", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Active stock session is required for purchases.");
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

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private async Task<InventoryLocation> GetOrCreateLocationAsync(int branchId, string name, CancellationToken cancellationToken)
    {
        var location = await _context.InventoryLocations.FirstOrDefaultAsync(x => x.BranchId == branchId && x.Name == name, cancellationToken);
        if (location is not null)
        {
            return location;
        }

        location = new InventoryLocation { BranchId = branchId, Name = name };
        _context.InventoryLocations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);
        return location;
    }

    private async Task<InventoryStock?> LockStockAsync(int branchId, int inventoryItemId, int locationId, CancellationToken cancellationToken) =>
        await _context.InventoryStocks
            .FromSqlInterpolated($"SELECT *, xmin FROM \"InventoryStocks\" WHERE \"BranchId\" = {branchId} AND \"InventoryItemId\" = {inventoryItemId} AND \"InventoryLocationId\" = {locationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private static decimal CalculateWeightedAverage(decimal oldQuantity, decimal oldAverageCost, decimal purchasedQuantity, decimal unitCost)
    {
        if (purchasedQuantity <= 0)
        {
            return oldAverageCost;
        }

        var totalQuantity = oldQuantity + purchasedQuantity;
        return totalQuantity <= 0 ? unitCost : ((oldQuantity * oldAverageCost) + (purchasedQuantity * unitCost)) / totalQuantity;
    }
}
