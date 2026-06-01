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
    private readonly IInventoryTransactionService _inventoryTransactionService;
    private const decimal MaxPurchaseItemQuantity = 1_000_000m;

    public PurchaseService(AppDbContext context, IBranchContextService branchContextService, IIdempotencyService idempotencyService, IInventoryTransactionService inventoryTransactionService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _idempotencyService = idempotencyService;
        _inventoryTransactionService = inventoryTransactionService;
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

        dto.InvoiceNumber = string.IsNullOrWhiteSpace(dto.InvoiceNumber) ? null : dto.InvoiceNumber.Trim();

        if (dto.SupplierId <= 0)
        {
            throw new PosValidationException("Supplier is required.");
        }

        if (!await _context.Suppliers.AnyAsync(x => x.Id == dto.SupplierId, cancellationToken))
        {
            throw new BusinessException("Selected supplier was not found.");
        }

        if (dto.InvoiceNumber is not null &&
            await _context.Purchases.AnyAsync(x => x.SupplierId == dto.SupplierId && x.InvoiceNumber == dto.InvoiceNumber, cancellationToken))
        {
            throw new BusinessException("This supplier invoice number has already been used.");
        }

        if (dto.Items.Count == 0)
        {
            throw new PosValidationException("Purchase must contain at least one item.");
        }

        if (dto.Items.Any(x => x.InventoryItemId <= 0 || x.PurchaseQuantity <= 0 || x.UnitCostPerPurchaseUnit <= 0))
        {
            throw new PosValidationException("Purchase must contain valid item quantities and costs.");
        }

        if (dto.Items.GroupBy(x => x.InventoryItemId).Any(x => x.Count() > 1))
        {
            throw new PosValidationException("Duplicate inventory items are not allowed in the same purchase.");
        }

        if (dto.Items.Any(x => x.PurchaseQuantity > MaxPurchaseItemQuantity))
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
                Items = dto.Items.OrderBy(x => x.InventoryItemId).Select(x => new { x.InventoryItemId, x.PurchaseQuantity, x.PurchaseUnitName, x.ConversionFactorToBase, x.UnitCostPerPurchaseUnit, x.TotalCost })
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
        var validInventoryItems = await _context.InventoryItems
            .Where(x => x.BranchId == dto.BranchId && x.IsActive && inventoryItemIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (validInventoryItems.Count != inventoryItemIds.Count)
        {
            throw new BusinessException("Selected inventory item does not belong to the active branch session.");
        }

        if (validInventoryItems.Values.Any(x => x.IsPreparedItem && !x.IsExpenseOnly))
        {
            throw new BusinessException("Prepared inventory items must be produced through preparation batches, not supplier purchases.");
        }

        var purchaseLines = dto.Items.Select(item =>
        {
            var inventoryItem = validInventoryItems[item.InventoryItemId];
            var purchaseUnitName = ResolvePurchaseUnitName(inventoryItem, item.PurchaseUnitName);
            var conversionFactor = inventoryItem.IsExpenseOnly
                ? 0m
                : ResolveConversionFactor(inventoryItem, purchaseUnitName, item.ConversionFactorToBase ?? inventoryItem.DefaultConversionFactorToBase);
            var baseQuantity = inventoryItem.IsExpenseOnly ? 0m : item.PurchaseQuantity * conversionFactor;
            var totalCost = item.PurchaseQuantity * item.UnitCostPerPurchaseUnit;
            if (totalCost < 0)
            {
                throw new PosValidationException($"Total cost cannot be negative for {inventoryItem.Name}.");
            }

            var unitCostBase = baseQuantity == 0 ? 0 : totalCost / baseQuantity;
            return new PurchaseLine(
                item.InventoryItemId,
                purchaseUnitName,
                item.PurchaseQuantity,
                conversionFactor,
                baseQuantity,
                item.UnitCostPerPurchaseUnit,
                unitCostBase,
                totalCost,
                inventoryItem.IsExpenseOnly,
                item.Notes);
        })
            .ToList();

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

        foreach (var item in purchaseLines)
        {
            purchase.Items.Add(new PurchaseItem
            {
                BranchId = dto.BranchId,
                InventoryItemId = item.InventoryItemId,
                PurchaseUnitName = item.PurchaseUnitName,
                PurchaseQuantity = item.PurchaseQuantity,
                ConversionFactorToBase = item.ConversionFactorToBase,
                BaseQuantity = item.BaseQuantity,
                UnitCostPerPurchaseUnit = item.UnitCostPerPurchaseUnit,
                UnitCostBase = item.UnitCostBase,
                TotalCost = item.TotalCost,
                IsExpenseOnly = item.IsExpenseOnly,
                Notes = item.Notes
            });
        }

        _context.Purchases.Add(purchase);
        await _context.SaveChangesAsync(cancellationToken);

        var stockRoom = await _inventoryTransactionService.GetOrCreateLocationAsync(dto.BranchId, "Stock Room", cancellationToken);
        foreach (var item in purchaseLines.Where(x => !x.IsExpenseOnly))
        {
            await _inventoryTransactionService.CreditAsync(dto.BranchId, item.InventoryItemId, stockRoom.Id, item.BaseQuantity, item.UnitCostBase, cancellationToken);
            _inventoryTransactionService.AddMovement(new InventoryMovementRequest(
                dto.BranchId,
                item.InventoryItemId,
                null,
                stockRoom.Id,
                item.BaseQuantity,
                item.UnitCostBase,
                item.TotalCost,
                InventoryMovementType.Purchase,
                nameof(Purchase),
                purchase.Id,
                dto.UserSessionId,
                dto.TerminalId,
                dto.IdempotencyKey,
                dto.PerformedByUserId));
        }

        if (purchaseLines.Any(x => x.IsExpenseOnly))
        {
            var category = await GetOrCreateExpenseOnlyCategoryAsync(dto.BranchId, cancellationToken);
            foreach (var item in purchaseLines.Where(x => x.IsExpenseOnly))
            {
                _context.OperationalExpenses.Add(new OperationalExpense
                {
                    BranchId = dto.BranchId,
                    ExpenseCategoryId = category.Id,
                    Amount = item.TotalCost,
                    ExpenseDate = DateTime.UtcNow.Date,
                    Description = $"Expense-only purchase item #{item.InventoryItemId} on purchase #{purchase.Id}",
                    CreatedByUserId = dto.PerformedByUserId
                });
            }
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

    private async Task<ExpenseCategory> GetOrCreateExpenseOnlyCategoryAsync(int branchId, CancellationToken cancellationToken)
    {
        const string categoryName = "Expense Only Purchases";
        var category = await _context.ExpenseCategories
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.Name == categoryName, cancellationToken);
        if (category is not null)
        {
            return category;
        }

        category = new ExpenseCategory { BranchId = branchId, Name = categoryName };
        _context.ExpenseCategories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);
        return category;
    }

    private static string ResolvePurchaseUnitName(InventoryItem inventoryItem, string? requestedPurchaseUnit)
    {
        if (inventoryItem.IsExpenseOnly)
        {
            return string.IsNullOrWhiteSpace(requestedPurchaseUnit)
                ? InventoryUnitCatalog.None
                : requestedPurchaseUnit.Trim();
        }

        var purchaseUnitName = string.IsNullOrWhiteSpace(requestedPurchaseUnit)
            ? inventoryItem.PurchaseUnitName?.Trim()
            : requestedPurchaseUnit.Trim();
        if (string.IsNullOrWhiteSpace(purchaseUnitName))
        {
            if (!InventoryControlDefaults.NeedsPurchaseConversion(inventoryItem))
            {
                return inventoryItem.BaseUnit;
            }

            throw new PosValidationException($"Default purchase unit is not configured for {inventoryItem.Name}. Edit the inventory item before purchasing it.");
        }

        return purchaseUnitName;
    }

    private static decimal ResolveConversionFactor(InventoryItem inventoryItem, string purchaseUnitName, decimal? requestedConversion)
    {
        if (!inventoryItem.IsStockTracked || inventoryItem.IsExpenseOnly)
        {
            return 0m;
        }

        if (string.Equals(purchaseUnitName, inventoryItem.BaseUnit, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var option = InventoryUnitCatalog.FindOption(inventoryItem.BaseUnit, purchaseUnitName);
        if (option is null)
        {
            throw new PosValidationException($"Purchase unit {purchaseUnitName} is not valid for {inventoryItem.Name}.");
        }

        var conversion = requestedConversion;
        if (option.IsFixedConversion)
        {
            if (!conversion.HasValue || conversion.Value != option.DefaultConversionFactorToBase)
            {
                throw new PosValidationException($"{option.DisplayName} must convert to {option.DefaultConversionFactorToBase:0.###} {inventoryItem.BaseUnit}.");
            }

            return option.DefaultConversionFactorToBase!.Value;
        }

        if (!conversion.HasValue || conversion.Value <= 0)
        {
            if (!InventoryControlDefaults.NeedsPurchaseConversion(inventoryItem))
            {
                return 1m;
            }

            throw new PosValidationException($"Conversion factor must be greater than zero for {inventoryItem.Name}.");
        }

        return conversion.Value;
    }

    private sealed record PurchaseLine(
        int InventoryItemId,
        string PurchaseUnitName,
        decimal PurchaseQuantity,
        decimal ConversionFactorToBase,
        decimal BaseQuantity,
        decimal UnitCostPerPurchaseUnit,
        decimal UnitCostBase,
        decimal TotalCost,
        bool IsExpenseOnly,
        string? Notes);
}
