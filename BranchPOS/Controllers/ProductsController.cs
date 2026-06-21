using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager,Cashier")]
public class ProductsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;
    private readonly IBranchContextService _branchContextService;
    private readonly SecurityRateLimitOptions _limits;

    public ProductsController(AppDbContext context, IProductService productService, IBranchContextService branchContextService, IOptions<SecurityRateLimitOptions> limits)
    {
        _context = context;
        _productService = productService;
        _branchContextService = branchContextService;
        _limits = limits.Value;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!string.Equals(context.ActionDescriptor.RouteValues["action"], nameof(Search), StringComparison.OrdinalIgnoreCase) &&
            !User.IsInRole("StockManager"))
        {
            context.Result = Forbid();
            return;
        }

        base.OnActionExecuting(context);
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productService.GetProductsAsync();
        return View(products);
    }

    [HttpGet, Authorize(Roles = "Cashier,StockManager"), EnableRateLimiting("ProductSearchPolicy")]
    public async Task<IActionResult> Search(string? q, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var term = (q ?? string.Empty).Trim();
        if (term.Length > 0 && term.Length < _limits.ProductSearchMinimumLength)
        {
            return Json(new { success = true, products = Array.Empty<object>(), page, pageSize = 0, total = 0 });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, _limits.MaxProductSearchResults);
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var query = _context.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.BranchId == branchId && x.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%") || EF.Functions.ILike(x.Category!.Name, $"%{term}%"));
        }

        var total = await query.CountAsync(cancellationToken);
        var products = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Name,
                CategoryName = x.Category == null ? "" : x.Category.Name,
                x.Price
            })
            .ToListAsync(cancellationToken);

        return Json(new { success = true, products, page, pageSize, total });
    }

    [Authorize(Roles = "StockManager")]
    public async Task<IActionResult> Create()
    {
        return View(await BuildProductModelAsync(new ProductEditViewModel()));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "StockManager")]
    public async Task<IActionResult> Create(ProductEditViewModel model)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var recipeItems = await ValidateRecipeItemsAsync(model, branchId);
        ClearLegacyDirectSaleFields(model);

        if (!ModelState.IsValid)
        {
            return View(await BuildProductModelAsync(model));
        }

        try
        {
            await _productService.CreateProductAsync(
                new Product
                {
                    Name = model.Name,
                    Price = model.Price,
                    CategoryId = model.CategoryId,
                    DirectInventoryItemId = null,
                    DirectQuantityBase = null
                },
                recipeItems.ToDictionary(x => x.InventoryItemId, x => x.QuantityRequired!.Value));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildProductModelAsync(model));
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "StockManager")]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _productService.GetProductAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        var model = new ProductEditViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            CategoryId = product.CategoryId,
            DirectInventoryItemId = null,
            DirectQuantityBase = null
        };

        return View(await BuildProductModelAsync(model, product));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "StockManager")]
    public async Task<IActionResult> Edit(int id, ProductEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var recipeItems = await ValidateRecipeItemsAsync(model, branchId);
        ClearLegacyDirectSaleFields(model);

        if (!ModelState.IsValid)
        {
            return View(await BuildProductModelAsync(model));
        }

        try
        {
            await _productService.UpdateProductAsync(
                new Product
                {
                    Id = model.Id,
                    Name = model.Name,
                    Price = model.Price,
                    CategoryId = model.CategoryId,
                    DirectInventoryItemId = null,
                    DirectQuantityBase = null
                },
                recipeItems.ToDictionary(x => x.InventoryItemId, x => x.QuantityRequired!.Value));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildProductModelAsync(model));
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<ProductEditViewModel> BuildProductModelAsync(ProductEditViewModel model, Product? product = null)
    {
        model.Categories = await _context.Categories
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.CategoryId))
            .ToListAsync();

        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        model.InventoryItems = await _context.InventoryItems
            .Where(x => x.BranchId == branchId && x.IsActive)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.BaseUnit)
            .ToListAsync();

        if (product is not null)
        {
            model.RecipeItems = product.Recipes
                .FirstOrDefault(x => x.IsActive)?
                .Ingredients
                .OrderBy(x => x.InventoryItem!.Name)
                .Select(x => RecipeItemQuantityViewModel.FromInventoryItem(x.InventoryItem!, x.QuantityRequiredBase))
                .ToList() ?? [];
            if (model.RecipeItems.Count == 0 &&
                product.DirectInventoryItem is not null &&
                product.DirectQuantityBase is > 0m)
            {
                model.RecipeItems.Add(RecipeItemQuantityViewModel.FromInventoryItem(product.DirectInventoryItem, product.DirectQuantityBase.Value));
            }
        }

        FillRecipeItemDisplayFields(model, model.InventoryItems.ToDictionary(x => x.Id));
        EnsureOneRecipeItemRow(model);

        return model;
    }

    private async Task<List<RecipeItemQuantityViewModel>> ValidateRecipeItemsAsync(ProductEditViewModel model, int branchId)
    {
        if (!await _context.Categories.AnyAsync(x => x.Id == model.CategoryId))
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Category is required.");
        }

        var inventoryIds = model.RecipeItems.Select(x => x.InventoryItemId).Where(x => x > 0).Distinct().ToList();
        var inventoryItems = await _context.InventoryItems
            .Where(x =>
                x.BranchId == branchId &&
                x.IsActive &&
                x.IsStockTracked &&
                !x.IsExpenseOnly &&
                inventoryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        var normalized = new List<RecipeItemQuantityViewModel>();

        for (var i = 0; i < model.RecipeItems.Count; i++)
        {
            var row = model.RecipeItems[i];
            var isEmpty = row.InventoryItemId <= 0 && !row.QuantityRequired.HasValue;
            if (isEmpty)
            {
                continue;
            }

            if (row.InventoryItemId <= 0)
            {
                ModelState.AddModelError($"RecipeItems[{i}].InventoryItemId", "Inventory item is required.");
            }
            else if (!inventoryItems.TryGetValue(row.InventoryItemId, out var item))
            {
                ModelState.AddModelError($"RecipeItems[{i}].InventoryItemId", "Inventory item must be active and belong to the active branch.");
            }
            else
            {
                row.Name = item.Name;
                row.Unit = item.BaseUnit;
            }

            if (!row.QuantityRequired.HasValue || row.QuantityRequired <= 0)
            {
                ModelState.AddModelError($"RecipeItems[{i}].QuantityRequired", "Quantity required per product must be greater than zero.");
            }

            normalized.Add(row);
        }

        if (normalized.GroupBy(x => x.InventoryItemId).Any(x => x.Key > 0 && x.Count() > 1))
        {
            ModelState.AddModelError(string.Empty, "Duplicate inventory item rows are not allowed.");
        }

        return normalized;
    }

    private static void ClearLegacyDirectSaleFields(ProductEditViewModel model)
    {
        model.DirectInventoryItemId = null;
        model.DirectQuantityBase = null;
    }

    private static void FillRecipeItemDisplayFields(ProductEditViewModel model, Dictionary<int, InventoryItem> inventoryItems)
    {
        foreach (var row in model.RecipeItems)
        {
            if (row.InventoryItemId > 0 && inventoryItems.TryGetValue(row.InventoryItemId, out var item))
            {
                row.Name = item.Name;
                row.Unit = item.BaseUnit;
            }
        }
    }

    private static void EnsureOneRecipeItemRow(ProductEditViewModel model)
    {
        if (model.RecipeItems.Count == 0)
        {
            model.RecipeItems.Add(new RecipeItemQuantityViewModel());
        }
    }
}
