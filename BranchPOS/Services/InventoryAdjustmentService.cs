using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BranchPOS.Services;

public class InventoryAdjustmentService : IInventoryAdjustmentService
{
    private readonly AppDbContext _context;
    private readonly IInventoryTransactionService _inventoryTransactionService;
    private readonly PosOperationalOptions _options;

    public InventoryAdjustmentService(
        AppDbContext context,
        IInventoryTransactionService inventoryTransactionService,
        IOptions<PosOperationalOptions> options)
    {
        _context = context;
        _inventoryTransactionService = inventoryTransactionService;
        _options = options.Value;
    }

    public async Task<InventoryAdjustmentResultDto> CreateAdjustmentAsync(CreateInventoryAdjustmentDto dto, string userId, int branchId, CancellationToken cancellationToken = default)
    {
        ValidateCreateDto(dto);
        var item = await GetItemAsync(dto.InventoryItemId, branchId, cancellationToken);
        var locationType = dto.LocationType!.Value;
        var adjustmentType = dto.AdjustmentType!.Value;
        var conversion = ConvertToBase(dto.Quantity, dto.UnitName, item);
        var location = await GetLocationAsync(branchId, locationType, cancellationToken);
        var unitCost = await GetUnitCostAsync(branchId, item.Id, location.Id, cancellationToken);
        var adjustment = new InventoryAdjustment
        {
            BranchId = branchId,
            InventoryItemId = item.Id,
            LocationType = locationType,
            AdjustmentType = adjustmentType,
            QuantityBaseUnit = conversion.QuantityBase,
            DisplayQuantity = dto.Quantity,
            DisplayUnitName = conversion.DisplayUnitName,
            UnitCost = unitCost,
            TotalCost = conversion.QuantityBase * unitCost,
            Reason = dto.Reason.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        adjustment.Status = ShouldAutoApprove(adjustment)
            ? InventoryAdjustmentStatus.Approved
            : InventoryAdjustmentStatus.Pending;
        if (adjustment.Status == InventoryAdjustmentStatus.Approved)
        {
            adjustment.ApprovedByUserId = userId;
            adjustment.ApprovedAt = DateTime.UtcNow;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _context.InventoryAdjustments.Add(adjustment);
        await _context.SaveChangesAsync(cancellationToken);
        if (adjustment.Status == InventoryAdjustmentStatus.Approved)
        {
            await ApplyApprovedAdjustmentAsync(adjustment, item, location, userId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetRequiredResultAsync(adjustment.Id, branchId, cancellationToken);
    }

    public async Task<InventoryAdjustmentResultDto> ApproveAdjustmentAsync(ApproveInventoryAdjustmentDto dto, string userId, int branchId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var adjustment = await _context.InventoryAdjustments
            .Include(x => x.InventoryItem)
            .FirstOrDefaultAsync(x => x.Id == dto.AdjustmentId && x.BranchId == branchId, cancellationToken)
            ?? throw new PosNotFoundException("Inventory adjustment was not found.");

        if (adjustment.Status != InventoryAdjustmentStatus.Pending)
        {
            throw new BusinessException("Only pending adjustments can be approved.");
        }

        var location = await GetLocationAsync(branchId, adjustment.LocationType, cancellationToken);
        adjustment.Status = InventoryAdjustmentStatus.Approved;
        adjustment.ApprovedByUserId = userId;
        adjustment.ApprovedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            adjustment.Notes = string.IsNullOrWhiteSpace(adjustment.Notes)
                ? dto.Notes.Trim()
                : $"{adjustment.Notes} Approval note: {dto.Notes.Trim()}";
        }

        await ApplyApprovedAdjustmentAsync(adjustment, adjustment.InventoryItem!, location, userId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetRequiredResultAsync(adjustment.Id, branchId, cancellationToken);
    }

    public async Task<InventoryAdjustmentResultDto> RejectAdjustmentAsync(RejectInventoryAdjustmentDto dto, string userId, int branchId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.RejectionReason))
        {
            throw new PosValidationException("Rejection reason is required.");
        }

        var adjustment = await _context.InventoryAdjustments
            .FirstOrDefaultAsync(x => x.Id == dto.AdjustmentId && x.BranchId == branchId, cancellationToken)
            ?? throw new PosNotFoundException("Inventory adjustment was not found.");

        if (adjustment.Status != InventoryAdjustmentStatus.Pending)
        {
            throw new BusinessException("Only pending adjustments can be rejected.");
        }

        adjustment.Status = InventoryAdjustmentStatus.Rejected;
        adjustment.RejectedByUserId = userId;
        adjustment.RejectedAt = DateTime.UtcNow;
        adjustment.RejectionReason = dto.RejectionReason.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        return await GetRequiredResultAsync(adjustment.Id, branchId, cancellationToken);
    }

    public async Task<List<InventoryAdjustmentResultDto>> GetAdjustmentsAsync(
        int branchId,
        InventoryLocationType? locationType,
        InventoryAdjustmentType? adjustmentType,
        InventoryAdjustmentStatus? status,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var query = AdjustmentResultQuery(branchId);
        if (locationType.HasValue)
        {
            query = query.Where(x => x.LocationType == locationType.Value);
        }
        if (adjustmentType.HasValue)
        {
            query = query.Where(x => x.AdjustmentType == adjustmentType.Value);
        }
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value.ToUniversalTime());
        }
        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt < to.Value.ToUniversalTime());
        }

        return await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync(cancellationToken);
    }

    public Task<InventoryAdjustmentResultDto?> GetAdjustmentByIdAsync(int id, int branchId, CancellationToken cancellationToken = default) =>
        AdjustmentResultQuery(branchId).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    private async Task<InventoryAdjustmentResultDto> GetRequiredResultAsync(int id, int branchId, CancellationToken cancellationToken) =>
        await GetAdjustmentByIdAsync(id, branchId, cancellationToken)
        ?? throw new PosNotFoundException("Inventory adjustment was not found.");

    private IQueryable<InventoryAdjustmentResultDto> AdjustmentResultQuery(int branchId) =>
        _context.InventoryAdjustments
            .AsNoTracking()
            .Include(x => x.InventoryItem)
            .Include(x => x.CreatedByUser)
            .Include(x => x.ApprovedByUser)
            .Include(x => x.RejectedByUser)
            .Where(x => x.BranchId == branchId)
            .Select(x => new InventoryAdjustmentResultDto
            {
                Id = x.Id,
                PublicId = x.PublicId,
                InventoryItemId = x.InventoryItemId,
                InventoryItemName = x.InventoryItem == null ? "" : x.InventoryItem.Name,
                LocationType = x.LocationType,
                AdjustmentType = x.AdjustmentType,
                Status = x.Status,
                QuantityBaseUnit = x.QuantityBaseUnit,
                DisplayQuantity = x.DisplayQuantity,
                DisplayUnitName = x.DisplayUnitName,
                UnitCost = x.UnitCost,
                TotalCost = x.TotalCost,
                Reason = x.Reason,
                Notes = x.Notes,
                CreatedByUserName = x.CreatedByUser == null ? x.CreatedByUserId : x.CreatedByUser.Email ?? x.CreatedByUser.UserName ?? x.CreatedByUserId,
                CreatedAt = x.CreatedAt,
                ApprovedByUserName = x.ApprovedByUser == null ? null : x.ApprovedByUser.Email ?? x.ApprovedByUser.UserName,
                ApprovedAt = x.ApprovedAt,
                RejectedByUserName = x.RejectedByUser == null ? null : x.RejectedByUser.Email ?? x.RejectedByUser.UserName,
                RejectedAt = x.RejectedAt,
                RejectionReason = x.RejectionReason
            });

    private async Task ApplyApprovedAdjustmentAsync(InventoryAdjustment adjustment, InventoryItem item, InventoryLocation location, string userId, CancellationToken cancellationToken)
    {
        var isDecrease = IsDecrease(adjustment.AdjustmentType);
        InventoryMutationResult mutation;
        if (isDecrease)
        {
            mutation = await _inventoryTransactionService.DebitAsync(
                adjustment.BranchId,
                adjustment.InventoryItemId,
                location.Id,
                adjustment.QuantityBaseUnit,
                item.Name,
                item.BaseUnit,
                ToDisplayName(adjustment.LocationType),
                cancellationToken);
        }
        else
        {
            mutation = await _inventoryTransactionService.CreditAsync(
                adjustment.BranchId,
                adjustment.InventoryItemId,
                location.Id,
                adjustment.QuantityBaseUnit,
                adjustment.UnitCost,
                cancellationToken);
        }

        _inventoryTransactionService.AddMovement(new InventoryMovementRequest(
            adjustment.BranchId,
            adjustment.InventoryItemId,
            isDecrease ? location.Id : null,
            isDecrease ? null : location.Id,
            adjustment.QuantityBaseUnit,
            adjustment.UnitCost,
            adjustment.TotalCost,
            adjustment.AdjustmentType == InventoryAdjustmentType.Waste ? InventoryMovementType.Waste : InventoryMovementType.Adjustment,
            nameof(InventoryAdjustment),
            adjustment.Id,
            null,
            null,
            $"adjustment-{adjustment.Id}",
            userId,
            Note: $"{adjustment.AdjustmentType}: {adjustment.Reason}"));
    }

    private async Task<InventoryItem> GetItemAsync(int inventoryItemId, int branchId, CancellationToken cancellationToken) =>
        await _context.InventoryItems.FirstOrDefaultAsync(x => x.Id == inventoryItemId && x.BranchId == branchId && x.IsActive, cancellationToken)
        ?? throw new PosNotFoundException("Inventory item was not found for this branch.");

    private async Task<InventoryLocation> GetLocationAsync(int branchId, InventoryLocationType locationType, CancellationToken cancellationToken) =>
        await _inventoryTransactionService.GetOrCreateLocationAsync(branchId, ToLocationName(locationType), cancellationToken);

    private async Task<decimal> GetUnitCostAsync(int branchId, int inventoryItemId, int locationId, CancellationToken cancellationToken)
    {
        var stockCost = await _context.InventoryStocks
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.InventoryItemId == inventoryItemId && x.InventoryLocationId == locationId)
            .Select(x => (decimal?)x.AverageUnitCostBase)
            .FirstOrDefaultAsync(cancellationToken);
        if (stockCost.HasValue && stockCost.Value > 0)
        {
            return stockCost.Value;
        }

        return await _context.PurchaseItems
            .AsNoTracking()
            .Where(x => x.BranchId == branchId && x.InventoryItemId == inventoryItemId && x.UnitCostBase > 0)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (decimal?)x.UnitCostBase)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;
    }

    private bool ShouldAutoApprove(InventoryAdjustment adjustment)
    {
        if (adjustment.AdjustmentType is InventoryAdjustmentType.Missing or InventoryAdjustmentType.CorrectionIncrease or InventoryAdjustmentType.CorrectionDecrease)
        {
            return false;
        }

        return adjustment.TotalCost <= _options.InventoryAdjustmentAutoApprovalCostThreshold &&
            adjustment.QuantityBaseUnit <= _options.InventoryAdjustmentAutoApprovalQuantityThresholdBase;
    }

    private static AdjustmentConversion ConvertToBase(decimal quantity, string? unitName, InventoryItem item)
    {
        var displayUnit = string.IsNullOrWhiteSpace(unitName) ? item.BaseUnit : unitName.Trim();
        if (string.Equals(displayUnit, item.BaseUnit, StringComparison.OrdinalIgnoreCase))
        {
            return new AdjustmentConversion(quantity, item.BaseUnit);
        }

        if (string.Equals(displayUnit, item.PurchaseUnitName, StringComparison.OrdinalIgnoreCase) &&
            item.DefaultConversionFactorToBase.HasValue &&
            item.DefaultConversionFactorToBase.Value > 0)
        {
            return new AdjustmentConversion(quantity * item.DefaultConversionFactorToBase.Value, displayUnit);
        }

        var option = InventoryUnitCatalog.FindOption(item.BaseUnit, displayUnit)
            ?? throw new PosValidationException($"Unit {displayUnit} is not valid for {item.Name}.");
        if (!option.DefaultConversionFactorToBase.HasValue || option.DefaultConversionFactorToBase.Value <= 0)
        {
            throw new PosValidationException($"Unit {displayUnit} needs a configured conversion factor on the inventory item.");
        }

        return new AdjustmentConversion(quantity * option.DefaultConversionFactorToBase.Value, displayUnit);
    }

    private static void ValidateCreateDto(CreateInventoryAdjustmentDto dto)
    {
        if (dto.InventoryItemId <= 0)
        {
            throw new PosValidationException("Inventory item is required.");
        }
        if (!dto.LocationType.HasValue)
        {
            throw new PosValidationException("Location is required.");
        }
        if (!dto.AdjustmentType.HasValue)
        {
            throw new PosValidationException("Adjustment type is required.");
        }
        if (dto.Quantity <= 0)
        {
            throw new PosValidationException("Quantity must be greater than zero.");
        }
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new PosValidationException("Reason is required.");
        }
    }

    private static bool IsDecrease(InventoryAdjustmentType type) =>
        type is InventoryAdjustmentType.Waste
            or InventoryAdjustmentType.Missing
            or InventoryAdjustmentType.Expired
            or InventoryAdjustmentType.Damaged
            or InventoryAdjustmentType.Spillage
            or InventoryAdjustmentType.CorrectionDecrease;

    public static string ToLocationName(InventoryLocationType locationType) =>
        locationType == InventoryLocationType.StockRoom ? "Stock Room" : "Kitchen";

    private static string ToDisplayName(InventoryLocationType locationType) =>
        locationType == InventoryLocationType.StockRoom ? "stock room" : "kitchen";

    private sealed record AdjustmentConversion(decimal QuantityBase, string DisplayUnitName);
}
