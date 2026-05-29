using System.Security.Claims;
using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager,Admin")]
public class PreparedStockController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IPreparationService _preparationService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITerminalContextService _terminalContextService;
    private readonly IIdempotencyService _idempotencyService;

    public PreparedStockController(
        AppDbContext context,
        IBranchContextService branchContextService,
        IPreparationService preparationService,
        IUserSessionService userSessionService,
        ITerminalContextService terminalContextService,
        IIdempotencyService idempotencyService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _preparationService = preparationService;
        _userSessionService = userSessionService;
        _terminalContextService = terminalContextService;
        _idempotencyService = idempotencyService;
    }

    public async Task<IActionResult> Add(int? preparationRecipeId, int? recipeId)
    {
        if (await _userSessionService.GetActiveSessionAsync(GetUserId()) is null)
        {
            return RedirectToAction("Index", "Sessions");
        }

        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        return View(await BuildBatchModelAsync(branchId, preparationRecipeId ?? recipeId, null, null, Guid.NewGuid().ToString("N")));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(PreparationBatchViewModel model)
    {
        var session = await _userSessionService.GetActiveSessionAsync(GetUserId());
        if (session is null)
        {
            TempData["Error"] = "Start or continue an active stock session before completing preparation batches.";
            return RedirectToAction("Index", "Sessions");
        }

        if (!model.PreparationRecipeId.HasValue)
        {
            ModelState.AddModelError(nameof(model.PreparationRecipeId), "Preparation recipe is required.");
        }

        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var recipe = model.PreparationRecipeId.HasValue
            ? await _context.PreparationRecipes
                .Include(x => x.OutputInventoryItem)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == model.PreparationRecipeId.Value && x.BranchId == branchId && x.IsActive)
            : null;

        if (model.PreparationRecipeId.HasValue && recipe is null)
        {
            ModelState.AddModelError(nameof(model.PreparationRecipeId), "Preparation recipe was not found.");
        }

        if (recipe?.OutputInventoryItem is not null)
        {
            var usesRecipeOutputCount = !string.Equals(recipe.OutputInventoryItem.BaseUnit, "Piece", StringComparison.OrdinalIgnoreCase);
            if (usesRecipeOutputCount)
            {
                if (!model.PreparedItemCount.HasValue || model.PreparedItemCount <= 0)
                {
                    ModelState.AddModelError(nameof(model.PreparedItemCount), "Prepared quantity must be greater than zero.");
                }
                else
                {
                    model.OutputQuantityBase = model.PreparedItemCount.Value * recipe.OutputQuantityBase;
                }
            }
            else if (!model.OutputQuantityBase.HasValue || model.OutputQuantityBase <= 0)
            {
                ModelState.AddModelError(nameof(model.OutputQuantityBase), "Actual output quantity must be greater than zero.");
            }

            if (!recipe.OutputInventoryItem.IsActive || !recipe.OutputInventoryItem.IsPreparedItem)
            {
                ModelState.AddModelError(nameof(model.PreparationRecipeId), "Selected recipe output item is not available.");
            }

            if (!usesRecipeOutputCount &&
                model.OutputQuantityBase.HasValue &&
                model.OutputQuantityBase.Value != decimal.Truncate(model.OutputQuantityBase.Value))
            {
                ModelState.AddModelError(nameof(model.OutputQuantityBase), "Actual output quantity must be a whole number for piece-based prepared items.");
            }
        }
        else if (!model.OutputQuantityBase.HasValue || model.OutputQuantityBase <= 0)
        {
            ModelState.AddModelError(nameof(model.OutputQuantityBase), "Actual output quantity must be greater than zero.");
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildBatchModelAsync(branchId, model.PreparationRecipeId, model.Notes, model.OutputQuantityBase, model.IdempotencyKey));
        }

        try
        {
            var terminal = await _terminalContextService.RequireCurrentTerminalFreshAsync();
            await _preparationService.CompletePreparationBatchAsync(new CompletePreparationBatchDto
            {
                IdempotencyKey = string.IsNullOrWhiteSpace(model.IdempotencyKey) ? _idempotencyService.GetOrCreateKey() : model.IdempotencyKey,
                UserSessionId = session.Id,
                TerminalId = terminal.Id,
                TerminalCode = terminal.TerminalCode,
                PreparationRecipeId = model.PreparationRecipeId!.Value,
                OutputQuantityBase = model.OutputQuantityBase!.Value,
                CreatedByUserId = GetUserId(),
                Notes = model.Notes
            });
            TempData["Message"] = "Prepared item added to Stock Room.";
            return RedirectToAction("StockRoom", "InventoryReports");
        }
        catch (BranchPosException ex)
        {
            TempData["Error"] = ex.UserMessage;
            return View(await BuildBatchModelAsync(branchId, model.PreparationRecipeId, model.Notes, model.OutputQuantityBase, model.IdempotencyKey));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return View(await BuildBatchModelAsync(branchId, model.PreparationRecipeId, model.Notes, model.OutputQuantityBase, model.IdempotencyKey));
        }
    }

    private async Task<PreparationBatchViewModel> BuildBatchModelAsync(int branchId, int? selectedRecipeId, string? notes, decimal? outputQuantityBase, string? idempotencyKey)
    {
        var recipes = await _context.PreparationRecipes
            .Include(x => x.OutputInventoryItem)
            .Where(x => x.BranchId == branchId && x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var model = new PreparationBatchViewModel
        {
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey,
            PreparationRecipeId = selectedRecipeId,
            OutputQuantityBase = outputQuantityBase,
            Notes = notes,
            Recipes = recipes.Select(x => new SelectListItem($"{x.Name} -> {x.OutputInventoryItem!.Name}", x.Id.ToString(), x.Id == selectedRecipeId)).ToList()
        };

        if (selectedRecipeId.HasValue)
        {
            model.SelectedRecipe = await _context.PreparationRecipes
                .Include(x => x.OutputInventoryItem)
                .Include(x => x.Ingredients)
                .ThenInclude(x => x.InventoryItem)
                .FirstOrDefaultAsync(x => x.Id == selectedRecipeId.Value && x.BranchId == branchId && x.IsActive);

            if (model.SelectedRecipe is not null)
            {
                model.OutputQuantityBase ??= model.SelectedRecipe.OutputQuantityBase;
                model.OutputUnit = model.SelectedRecipe.OutputInventoryItem?.BaseUnit ?? string.Empty;
                model.OutputItemName = model.SelectedRecipe.OutputInventoryItem?.Name ?? string.Empty;
                model.StandardRecipeOutputQuantity = model.SelectedRecipe.OutputQuantityBase;
                model.StandardRecipeOutputUnit = model.SelectedRecipe.OutputInventoryItem?.BaseUnit ?? string.Empty;
                model.UsesRecipeOutputCount = !string.Equals(model.OutputUnit, "Piece", StringComparison.OrdinalIgnoreCase);
                model.PreparedItemCount = model.UsesRecipeOutputCount && model.SelectedRecipe.OutputQuantityBase > 0
                    ? model.OutputQuantityBase / model.SelectedRecipe.OutputQuantityBase
                    : null;
                var scale = model.SelectedRecipe.OutputQuantityBase > 0
                    ? model.OutputQuantityBase.GetValueOrDefault() / model.SelectedRecipe.OutputQuantityBase
                    : 0;
                var stockRoom = await GetOrCreateLocationAsync(branchId, "Stock Room");
                var ingredientIds = model.SelectedRecipe.Ingredients.Select(x => x.InventoryItemId).ToList();
                var stocks = await _context.InventoryStocks
                    .AsNoTracking()
                    .Where(x => x.BranchId == branchId && x.InventoryLocationId == stockRoom.Id && ingredientIds.Contains(x.InventoryItemId))
                    .ToDictionaryAsync(x => x.InventoryItemId, x => x.QuantityBase);

                model.Ingredients = model.SelectedRecipe.Ingredients
                    .OrderBy(x => x.InventoryItem!.Name)
                    .Select(x => new PreparationIngredientAvailabilityViewModel
                    {
                        InventoryItemName = x.InventoryItem!.Name,
                        BaseUnit = x.InventoryItem.BaseUnit,
                        RecipeQuantity = x.QuantityBase,
                        RequiredQuantity = x.QuantityBase * scale,
                        AvailableQuantity = stocks.TryGetValue(x.InventoryItemId, out var available) ? available : 0
                    })
                    .ToList();
            }
        }

        return model;
    }

    private async Task<InventoryLocation> GetOrCreateLocationAsync(int branchId, string name)
    {
        var location = await _context.InventoryLocations.FirstOrDefaultAsync(x => x.BranchId == branchId && x.Name == name);
        if (location is not null)
        {
            return location;
        }

        location = new InventoryLocation { BranchId = branchId, Name = name };
        _context.InventoryLocations.Add(location);
        await _context.SaveChangesAsync();
        return location;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user was not found.");
}
