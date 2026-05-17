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
        if (!ModelState.IsValid)
        {
            return View(await BuildProductModelAsync(model));
        }

        await _productService.CreateProductAsync(
            new Product { Name = model.Name, Price = model.Price, CategoryId = model.CategoryId },
            model.Ingredients.ToDictionary(x => x.IngredientId, x => x.QuantityRequired));

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
            CategoryId = product.CategoryId
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

        if (!ModelState.IsValid)
        {
            return View(await BuildProductModelAsync(model));
        }

        await _productService.UpdateProductAsync(
            new Product { Id = model.Id, Name = model.Name, Price = model.Price, CategoryId = model.CategoryId },
            model.Ingredients.ToDictionary(x => x.IngredientId, x => x.QuantityRequired));

        return RedirectToAction(nameof(Index));
    }

    private async Task<ProductEditViewModel> BuildProductModelAsync(ProductEditViewModel model, Product? product = null)
    {
        model.Categories = await _context.Categories
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.CategoryId))
            .ToListAsync();

        var existingQuantities = product?.ProductIngredients.ToDictionary(x => x.IngredientId, x => x.QuantityRequired) ?? [];
        var branchId = await _branchContextService.GetCurrentBranchIdAsync();
        var ingredients = await _context.Ingredients
            .Where(x => x.BranchId == branchId)
            .OrderBy(x => x.Name)
            .ToListAsync();
        model.Ingredients = ingredients
            .Select(x => IngredientQuantityViewModel.FromIngredient(x, existingQuantities.TryGetValue(x.Id, out var quantity) ? quantity : 0))
            .ToList();

        return model;
    }
}
