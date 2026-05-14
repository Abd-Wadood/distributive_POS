using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager")]
public class ProductsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;
    private readonly IBranchContextService _branchContextService;

    public ProductsController(AppDbContext context, IProductService productService, IBranchContextService branchContextService)
    {
        _context = context;
        _productService = productService;
        _branchContextService = branchContextService;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!User.IsInRole("StockManager"))
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
