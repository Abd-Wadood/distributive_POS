using BranchPOS.Controllers;
using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;

namespace BranchPOS.Tests;

public sealed class PosEdgeCaseIntegrationTests : IAsyncLifetime
{
    private const string CashierId = "cashier-1";
    private const string StockManagerId = "stock-1";
    private const string BranchTwoCashierId = "cashier-2";

    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("POS_TEST_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=Offline_POS_EdgeTests;Username=postgres;Password=123";

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        await SeedCoreAsync(context);
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Two_simultaneous_orders_for_one_available_inventory_item_allow_exactly_one_sale()
    {
        await using var setup = CreateContext();
        var (productId, itemId) = await CreateProductWithKitchenStockAsync(setup, "Burger", 1m, 1m);

        var results = await Task.WhenAll(
            FinalizeInNewContextAsync(OrderDto(productId, customerPhone: "2001")),
            FinalizeInNewContextAsync(OrderDto(productId, customerPhone: "2002")));

        Assert.Single(results, x => x.Succeeded);
        Assert.Single(results, x => !x.Succeeded);

        await using var verify = CreateContext();
        var kitchen = await KitchenLocationAsync(verify);
        var stock = await verify.InventoryStocks.SingleAsync(x => x.InventoryItemId == itemId && x.InventoryLocationId == kitchen.Id);
        Assert.Equal(0m, stock.QuantityBase);
        Assert.Equal(1, await verify.InventoryMovements.CountAsync(x =>
            x.InventoryItemId == itemId &&
            x.MovementType == InventoryMovementType.Consumption &&
            x.QuantityBase == 1m));
        Assert.DoesNotContain(await verify.InventoryStocks.ToListAsync(), x => x.QuantityBase < 0);
    }

    [Fact]
    public async Task Cashier_finalize_auto_creates_kitchen_request_when_kitchen_stock_is_low()
    {
        await using var context = CreateContext();
        var stockRoom = await StockRoomLocationAsync(context);
        var kitchen = await KitchenLocationAsync(context);
        var dough = new InventoryItem { Id = 181, BranchId = 1, Name = "Auto Request Dough", BaseUnit = "Gram", PurchaseUnitName = "Gram", DefaultConversionFactorToBase = 1m, ReorderLevel = 100m, IsPreparedItem = true };
        context.InventoryItems.Add(dough);
        context.Products.Add(ProductWithRecipe(181, "Auto Request Pizza", dough, 250m, price: 100m));
        context.InventoryStocks.AddRange(
            new InventoryStock { BranchId = 1, InventoryItem = dough, InventoryLocationId = kitchen.Id, QuantityBase = 100m, AverageUnitCostBase = 0.5m },
            new InventoryStock { BranchId = 1, InventoryItem = dough, InventoryLocationId = stockRoom.Id, QuantityBase = 2000m, AverageUnitCostBase = 0.5m });
        await context.SaveChangesAsync();

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, CashierId),
                new Claim(ClaimTypes.Role, "Cashier")
            }, "TestAuth"))
        };
        var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        var cacheInvalidator = new PosMenuCacheInvalidator();
        var controller = new OrdersController(
            context,
            OrderService(context),
            new ProductAvailabilityService(context, new TestBranchContext(1), cache, cacheInvalidator),
            new StaticUserSessionService(context, 1),
            new StaticTerminalContextService(context, "MAIN-01"),
            new TestErrorLoggingService(),
            new TestIdempotencyService())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = await controller.Finalize(OrderDto(181, quantity: 2, customerPhone: "1810"));

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        var request = await context.KitchenRequests
            .Include(x => x.Details)
            .SingleAsync(x => x.RequestedByUserId == CashierId && x.Status == KitchenRequestStatus.PendingManagerReview);
        Assert.Contains("sent to stock manager", json.Value!.GetType().GetProperty("message")!.GetValue(json.Value)!.ToString());
        Assert.StartsWith("KR-POS-", request.RequestNumber);
        Assert.Equal("Auto-generated from POS low stock.", request.Note);
        Assert.Equal(KitchenRequestSource.Auto, request.RequestSource);
        var detail = Assert.Single(request.Details);
        Assert.Equal(dough.Id, detail.InventoryItemId);
        Assert.Equal(400m, detail.RequestedQuantity);
        Assert.Equal(400m, detail.RecommendedQuantity);
        Assert.Equal(100m, detail.CurrentKitchenQuantityAtRequest);
        Assert.Equal(0, await context.InventoryMovements.CountAsync(x => x.ReferenceType == nameof(KitchenRequest) && x.ReferenceId == request.Id));
    }

    [Fact]
    public async Task Simultaneous_orders_for_products_sharing_inventory_item_keep_stock_and_movements_correct()
    {
        await using var setup = CreateContext();
        var item = new InventoryItem { Id = 201, BranchId = 1, Name = "Dough", BaseUnit = "Gram", ReorderLevel = 1 };
        var pizza = ProductWithRecipe(201, "Pizza", item, 1m, price: 100m);
        var roll = ProductWithRecipe(202, "Roll", item, 1m, price: 50m);
        setup.InventoryItems.Add(item);
        setup.Products.AddRange(pizza, roll);
        setup.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = 2, Quantity = 2m, AverageUnitCost = 12m });
        await setup.SaveChangesAsync();

        var results = await Task.WhenAll(
            FinalizeInNewContextAsync(OrderDto(201, customerPhone: "3001")),
            FinalizeInNewContextAsync(OrderDto(202, customerPhone: "3002")));

        Assert.All(results, x => Assert.True(x.Succeeded, x.Error));

        await using var verify = CreateContext();
        var kitchen = await KitchenLocationAsync(verify);
        Assert.Equal(0m, (await verify.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        Assert.Equal(2m, await verify.InventoryMovements
            .Where(x => x.InventoryItemId == item.Id && x.MovementType == InventoryMovementType.Consumption)
            .SumAsync(x => x.QuantityBase));
    }

    [Fact]
    public async Task Failed_finalization_rolls_back_order_items_inventory_and_consumption_movements()
    {
        await using var context = CreateContext();
        var (productId, itemId) = await CreateProductWithKitchenStockAsync(context, "Combo", 2m, 1m);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            OrderService(context).FinalizeOrderAsync(OrderDto(productId)));

        var kitchen = await KitchenLocationAsync(context);
        Assert.Empty(await context.Orders.Where(x => x.OrderStatus == OrderStatus.Completed).ToListAsync());
        Assert.Empty(await context.InventoryMovements.Where(x => x.MovementType == InventoryMovementType.Consumption).ToListAsync());
        Assert.Equal(1m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == itemId && x.InventoryLocationId == kitchen.Id)).QuantityBase);
    }

    [Fact]
    public async Task Draft_completed_cancelled_and_receipt_rules_are_enforced_without_deducting_until_finalize()
    {
        await using var context = CreateContext();
        var (productId, itemId) = await CreateProductWithKitchenStockAsync(context, "Sandwich", 1m, 2m);
        var stockRoom = await StockRoomLocationAsync(context);
        var kitchen = await KitchenLocationAsync(context);
        var service = OrderService(context);

        var draft = await service.CreateDraftOrderAsync(DraftDto(productId));
        Assert.Equal(2m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == itemId && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        Assert.Empty(await context.InventoryMovements.Where(x => x.MovementType == InventoryMovementType.Consumption).ToListAsync());

        var completed = await service.FinalizeOrderAsync(OrderDto(productId, draftId: draft.OrderId));
        Assert.Equal(1m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == itemId && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        Assert.Equal(1, await context.InventoryMovements.CountAsync(x => x.MovementType == InventoryMovementType.Consumption));
        Assert.NotNull(await service.GetReceiptAsync(completed.OrderId));

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            service.FinalizeOrderAsync(OrderDto(productId, draftId: completed.OrderId, customerPhone: "3020")));

        var cancelledDraft = await service.CreateDraftOrderAsync(DraftDto(productId, customerPhone: "3021"));
        await service.CancelDraftOrderAsync(cancelledDraft.OrderId, CashierId);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            service.FinalizeOrderAsync(OrderDto(productId, draftId: cancelledDraft.OrderId, customerPhone: "3022")));
    }

    [Fact]
    public async Task Pending_inventory_adjustment_does_not_change_stock_until_approved()
    {
        await using var context = CreateContext();
        var stockRoom = await StockRoomLocationAsync(context);
        var item = new InventoryItem { Id = 9010, BranchId = 1, Name = "Adjustment Flour", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        context.InventoryItems.Add(item);
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = stockRoom.Id, QuantityBase = 5000m, AverageUnitCostBase = 0.25m });
        await context.SaveChangesAsync();

        var service = InventoryAdjustmentService(context);
        var created = await service.CreateAdjustmentAsync(new CreateInventoryAdjustmentDto
        {
            InventoryItemId = item.Id,
            LocationType = InventoryLocationType.StockRoom,
            AdjustmentType = InventoryAdjustmentType.Missing,
            Quantity = 1m,
            UnitName = "Kg",
            Reason = "Physical count short"
        }, StockManagerId, 1);

        Assert.Equal(InventoryAdjustmentStatus.Pending, created.Status);
        Assert.Equal(5000m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);

        await service.ApproveAdjustmentAsync(new ApproveInventoryAdjustmentDto { AdjustmentId = created.Id }, StockManagerId, 1);

        Assert.Equal(4000m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        Assert.Equal(250m, await context.InventoryMovements.Where(x => x.ReferenceType == nameof(InventoryAdjustment)).SumAsync(x => x.TotalCost));
    }

    [Fact]
    public async Task Rejected_inventory_adjustment_never_changes_stock()
    {
        await using var context = CreateContext();
        var kitchen = await KitchenLocationAsync(context);
        var item = new InventoryItem { Id = 9011, BranchId = 1, Name = "Adjustment Sauce", BaseUnit = "ML", PurchaseUnitName = "Liter", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        context.InventoryItems.Add(item);
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = kitchen.Id, QuantityBase = 2000m, AverageUnitCostBase = 0.1m });
        await context.SaveChangesAsync();

        var service = InventoryAdjustmentService(context);
        var created = await service.CreateAdjustmentAsync(new CreateInventoryAdjustmentDto
        {
            InventoryItemId = item.Id,
            LocationType = InventoryLocationType.Kitchen,
            AdjustmentType = InventoryAdjustmentType.CorrectionDecrease,
            Quantity = 500m,
            UnitName = "ML",
            Reason = "Count correction"
        }, StockManagerId, 1);

        await service.RejectAdjustmentAsync(new RejectInventoryAdjustmentDto { AdjustmentId = created.Id, RejectionReason = "Count verified" }, StockManagerId, 1);

        Assert.Equal(2000m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        Assert.Empty(await context.InventoryMovements.Where(x => x.ReferenceType == nameof(InventoryAdjustment)).ToListAsync());
    }

    [Fact]
    public async Task Correction_increase_adds_stock_only_after_approval()
    {
        await using var context = CreateContext();
        var kitchen = await KitchenLocationAsync(context);
        var item = new InventoryItem { Id = 9012, BranchId = 1, Name = "Adjustment Bun", BaseUnit = "Piece", PurchaseUnitName = "Piece", DefaultConversionFactorToBase = 1m, ReorderLevel = 10 };
        context.InventoryItems.Add(item);
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = kitchen.Id, QuantityBase = 3m, AverageUnitCostBase = 20m });
        await context.SaveChangesAsync();

        var service = InventoryAdjustmentService(context);
        var created = await service.CreateAdjustmentAsync(new CreateInventoryAdjustmentDto
        {
            InventoryItemId = item.Id,
            LocationType = InventoryLocationType.Kitchen,
            AdjustmentType = InventoryAdjustmentType.CorrectionIncrease,
            Quantity = 2m,
            UnitName = "Piece",
            Reason = "Count found extra"
        }, StockManagerId, 1);

        Assert.Equal(3m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        await service.ApproveAdjustmentAsync(new ApproveInventoryAdjustmentDto { AdjustmentId = created.Id }, StockManagerId, 1);

        Assert.Equal(5m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
    }

    [Fact]
    public async Task Inventory_adjustment_none_unit_defaults_to_item_base_unit()
    {
        await using var context = CreateContext();
        var stockRoom = await StockRoomLocationAsync(context);
        var item = new InventoryItem { Id = 9014, BranchId = 1, Name = "Adjustment Roll", BaseUnit = "Piece", PurchaseUnitName = "Piece", DefaultConversionFactorToBase = 1m, ReorderLevel = 10 };
        context.InventoryItems.Add(item);
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = stockRoom.Id, QuantityBase = 5m, AverageUnitCostBase = 3m });
        await context.SaveChangesAsync();

        var service = InventoryAdjustmentService(context);
        var created = await service.CreateAdjustmentAsync(new CreateInventoryAdjustmentDto
        {
            InventoryItemId = item.Id,
            LocationType = InventoryLocationType.StockRoom,
            AdjustmentType = InventoryAdjustmentType.Missing,
            Quantity = 1m,
            UnitName = InventoryUnitCatalog.None,
            Reason = "Physical count short"
        }, StockManagerId, 1);

        Assert.Equal(1m, created.QuantityBaseUnit);
        Assert.Equal("Piece", created.DisplayUnitName);

        await service.ApproveAdjustmentAsync(new ApproveInventoryAdjustmentDto { AdjustmentId = created.Id }, StockManagerId, 1);

        Assert.Equal(4m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
    }

    [Fact]
    public async Task Approved_adjustment_cannot_reduce_stock_below_zero()
    {
        await using var context = CreateContext();
        var kitchen = await KitchenLocationAsync(context);
        var item = new InventoryItem { Id = 9013, BranchId = 1, Name = "Adjustment Cheese", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        context.InventoryItems.Add(item);
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = kitchen.Id, QuantityBase = 100m, AverageUnitCostBase = 1m });
        await context.SaveChangesAsync();

        var service = InventoryAdjustmentService(context);
        var created = await service.CreateAdjustmentAsync(new CreateInventoryAdjustmentDto
        {
            InventoryItemId = item.Id,
            LocationType = InventoryLocationType.Kitchen,
            AdjustmentType = InventoryAdjustmentType.Missing,
            Quantity = 200m,
            UnitName = "Gram",
            Reason = "Missing"
        }, StockManagerId, 1);

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.ApproveAdjustmentAsync(new ApproveInventoryAdjustmentDto { AdjustmentId = created.Id }, StockManagerId, 1));
        Assert.Equal(100m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
    }

    [Fact]
    public async Task Product_recipe_price_total_and_quantity_validation_are_service_enforced()
    {
        await using var context = CreateContext();
        var (productId, _) = await CreateProductWithKitchenStockAsync(context, "Cake", 1m, 10m, price: 25m);
        context.Products.Add(new Product { Id = 211, BranchId = 1, CategoryId = 1, Name = "No Recipe", Price = 999m });
        context.Products.Add(new Product { Id = 212, BranchId = 1, CategoryId = 1, Name = "Inactive", Price = 10m, IsActive = false });
        await context.SaveChangesAsync();

        var service = OrderService(context);
        var noRecipeResult = await service.FinalizeOrderAsync(OrderDto(211, customerPhone: "3039"));
        Assert.Equal(999m, noRecipeResult.Subtotal);
        Assert.Empty(await context.InventoryMovements.Where(x => x.ReferenceType == nameof(Order) && x.ReferenceId == noRecipeResult.OrderId).ToListAsync());
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(212)));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(999)));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId, quantity: 0)));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId, quantity: -1)));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId, quantity: 10001)));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(0)));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId, orderType: "Delivery", customerPhone: "3030", address: "")));

        var result = await service.FinalizeOrderAsync(OrderDto(productId, quantity: 2, discount: 5m, customerPhone: "3031"));
        Assert.Equal(50m, result.Subtotal);
        Assert.Equal(45m, result.TotalAmount);
    }

    [Fact]
    public async Task ManualKitchenIssue_and_PeriodicCount_recipe_items_do_not_auto_deduct_on_sale()
    {
        await using var context = CreateContext();
        var kitchen = await KitchenLocationAsync(context);
        var manual = new InventoryItem { Id = 9210, BranchId = 1, Name = "Manual Corn", BaseUnit = "Gram", PurchaseUnitName = "Tin", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        manual.ConsumptionMode = ConsumptionMode.ManualKitchenIssue;
        InventoryControlDefaults.ApplyDefaults(manual);
        var periodic = new InventoryItem { Id = 9211, BranchId = 1, Name = "Periodic Tissue", BaseUnit = "Piece", PurchaseUnitName = "Roll", DefaultConversionFactorToBase = 1m, ReorderLevel = 10 };
        periodic.ConsumptionMode = ConsumptionMode.PeriodicCount;
        InventoryControlDefaults.ApplyDefaults(periodic);
        context.InventoryItems.AddRange(manual, periodic);
        context.Products.Add(ProductWithRecipe(9210, "Corn Cup", manual, 100m, price: 50m));
        context.Products.Add(ProductWithRecipe(9211, "Wrapped Meal", periodic, 1m, price: 70m));
        context.InventoryStocks.AddRange(
            new InventoryStock { BranchId = 1, InventoryItem = manual, InventoryLocationId = kitchen.Id, QuantityBase = 1000m, AverageUnitCostBase = 0.1m },
            new InventoryStock { BranchId = 1, InventoryItem = periodic, InventoryLocationId = kitchen.Id, QuantityBase = 10m, AverageUnitCostBase = 1m });
        await context.SaveChangesAsync();

        var service = OrderService(context);
        await service.FinalizeOrderAsync(OrderDto(9210, customerPhone: "9210"));
        await service.FinalizeOrderAsync(OrderDto(9211, customerPhone: "9211"));

        Assert.Equal(1000m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == manual.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        Assert.Equal(10m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == periodic.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        Assert.Empty(await context.InventoryMovements.Where(x => x.InventoryItemId == manual.Id || x.InventoryItemId == periodic.Id).ToListAsync());
    }

    [Fact]
    public async Task DirectSale_product_deducts_direct_item_from_stock_room()
    {
        await using var context = CreateContext();
        var stockRoom = await StockRoomLocationAsync(context);
        var coke = new InventoryItem { Id = 9220, BranchId = 1, Name = "Direct Coke", BaseUnit = "Piece", PurchaseUnitName = "Crate", DefaultConversionFactorToBase = 24m, ReorderLevel = 10 };
        coke.ConsumptionMode = ConsumptionMode.DirectSale;
        InventoryControlDefaults.ApplyDefaults(coke);
        context.InventoryItems.Add(coke);
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = coke, InventoryLocationId = stockRoom.Id, QuantityBase = 3m, AverageUnitCostBase = 40m });
        context.Products.Add(new Product { Id = 9220, BranchId = 1, CategoryId = 1, Name = "Coke", Price = 100m, DirectInventoryItem = coke, DirectQuantityBase = 1m });
        await context.SaveChangesAsync();

        var result = await OrderService(context).FinalizeOrderAsync(OrderDto(9220, quantity: 2, customerPhone: "9220"));

        Assert.Equal(200m, result.Subtotal);
        Assert.Equal(1m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == coke.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        var movement = await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == coke.Id && x.ReferenceType == nameof(Order));
        Assert.Equal(2m, movement.QuantityBase);
        Assert.Equal(stockRoom.Id, movement.FromLocationId);
    }

    [Fact]
    public async Task ExpenseOnly_purchase_records_cost_without_creating_stock()
    {
        await using var context = CreateContext();
        var gas = new InventoryItem { Id = 9230, BranchId = 1, Name = "Expense Gas Refill", BaseUnit = "None", PurchaseUnitName = null, ReorderLevel = 0 };
        gas.ConsumptionMode = ConsumptionMode.ExpenseOnly;
        InventoryControlDefaults.ApplyDefaults(gas);
        context.InventoryItems.Add(gas);
        await context.SaveChangesAsync();

        var purchaseId = await PurchaseService(context).CreatePurchaseAsync(PurchaseDto(1,
            new PurchaseItemDto
            {
                InventoryItemId = gas.Id,
                PurchaseQuantity = 1m,
                UnitCostPerPurchaseUnit = 2500m
            }));

        var line = await context.PurchaseItems.SingleAsync(x => x.PurchaseId == purchaseId);
        Assert.True(line.IsExpenseOnly);
        Assert.Equal(2500m, line.TotalCost);
        Assert.False(await context.InventoryStocks.AnyAsync(x => x.InventoryItemId == gas.Id));
        Assert.Equal(2500m, await context.OperationalExpenses.Where(x => x.Description!.Contains($"purchase #{purchaseId}")).SumAsync(x => x.Amount));
    }

    [Fact]
    public async Task ManualKitchenIssue_item_can_be_deducted_through_manual_usage()
    {
        await using var context = CreateContext();
        var kitchen = await KitchenLocationAsync(context);
        var item = new InventoryItem { Id = 9240, BranchId = 1, Name = "Manual Olives", BaseUnit = "Gram", PurchaseUnitName = "Tin", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        item.ConsumptionMode = ConsumptionMode.ManualKitchenIssue;
        InventoryControlDefaults.ApplyDefaults(item);
        context.InventoryItems.Add(item);
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = kitchen.Id, QuantityBase = 10m, AverageUnitCostBase = 2m });
        await context.SaveChangesAsync();

        await new ManualKitchenUsageService(context, new InventoryTransactionService(context)).CreateAsync(new CreateManualKitchenUsageDto
        {
            UsageDate = new DateTime(2026, 5, 31),
            InventoryItemId = item.Id,
            OpeningKitchenQuantity = 10m,
            ReceivedFromStockRoomQuantity = 0m,
            ClosingKitchenQuantity = 7m,
            Notes = "Shift close"
        }, StockManagerId, 1);

        Assert.Equal(7m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        var usage = await context.ManualKitchenUsages.SingleAsync(x => x.InventoryItemId == item.Id);
        Assert.Equal(DateTimeKind.Utc, usage.UsageDate.Kind);
        Assert.Equal(3m, usage.ActualUsedQuantity);
        Assert.Equal(3m, await context.InventoryMovements.Where(x => x.InventoryItemId == item.Id && x.MovementType == InventoryMovementType.ManualConsumption).SumAsync(x => x.QuantityBase));
        Assert.False(await context.InventoryMovements.AnyAsync(x => x.InventoryItemId == item.Id && x.MovementType == InventoryMovementType.Wastage));
    }

    [Fact]
    public async Task PeriodicCount_item_changes_through_stock_count()
    {
        await using var context = CreateContext();
        var stockRoom = await StockRoomLocationAsync(context);
        var item = new InventoryItem { Id = 9250, BranchId = 1, Name = "Periodic Tape", BaseUnit = "Piece", PurchaseUnitName = "Roll", DefaultConversionFactorToBase = 1m, ReorderLevel = 10 };
        item.ConsumptionMode = ConsumptionMode.PeriodicCount;
        InventoryControlDefaults.ApplyDefaults(item);
        context.InventoryItems.Add(item);
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = stockRoom.Id, QuantityBase = 10m, AverageUnitCostBase = 3m });
        await context.SaveChangesAsync();

        await new StockCountService(context, new InventoryTransactionService(context)).CreateAsync(new CreateStockCountDto
        {
            CountDate = new DateTime(2026, 5, 31),
            LocationType = InventoryLocationType.StockRoom,
            InventoryItemId = item.Id,
            CountedQuantity = 7m,
            Reason = "Weekly count"
        }, StockManagerId, 1);

        Assert.Equal(7m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        var count = await context.StockCounts.SingleAsync(x => x.InventoryItemId == item.Id);
        Assert.Equal(DateTimeKind.Utc, count.CountDate.Kind);
        Assert.Equal(-3m, count.DifferenceQuantity);
    }

    [Fact]
    public async Task ExpenseOnly_items_cannot_be_used_in_recipes_or_kitchen_dispatch()
    {
        await using var context = CreateContext();
        var item = new InventoryItem { Id = 9260, BranchId = 1, Name = "Expense Cleaner", BaseUnit = "None", ReorderLevel = 0 };
        item.ConsumptionMode = ConsumptionMode.ExpenseOnly;
        InventoryControlDefaults.ApplyDefaults(item);
        context.InventoryItems.Add(item);
        context.Products.Add(new Product { Id = 9260, BranchId = 1, CategoryId = 1, Name = "Cleaner Sale", Price = 1m });
        await context.SaveChangesAsync();

        var recipeController = new RecipesController(context, new TestBranchContext(1), new PosMenuCacheInvalidator());
        var recipeResult = await recipeController.Edit(new Recipe
        {
            ProductId = 9260,
            Ingredients = { new RecipeIngredient { InventoryItemId = item.Id, QuantityRequiredBase = 1m } }
        });
        Assert.IsType<ViewResult>(recipeResult);

        var kitchen = await KitchenLocationAsync(context);
        var request = new KitchenRequest
        {
            BranchId = 1,
            RequestNumber = $"KR-EXP-{Guid.NewGuid():N}"[..24],
            Status = KitchenRequestStatus.Approved,
            KitchenLocationId = kitchen.Id,
            Details = { new KitchenRequestDetail { InventoryItem = item, InventoryItemId = item.Id, RequestedQuantity = 1m, ApprovedQuantity = 1m } }
        };
        context.KitchenRequests.Add(request);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessException>(() =>
            new RestaurantInventoryService(context, new TestBranchContext(1), new InventoryTransactionService(context), new TestIdempotencyService())
                .DispatchKitchenRequestAsync(request.Id, StockManagerId));
    }

    [Fact]
    public async Task Product_recipe_edit_uses_inventory_base_units_and_allows_prepared_items()
    {
        await using var context = CreateContext();
        var dough = new InventoryItem { Id = 801, BranchId = 1, Name = "Recipe Test Dough", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000, IsPreparedItem = true };
        var sauce = new InventoryItem { Id = 802, BranchId = 1, Name = "Recipe Test Sauce", BaseUnit = "ML", PurchaseUnitName = "Liter", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        context.InventoryItems.AddRange(dough, sauce);
        context.Products.Add(new Product { Id = 803, BranchId = 1, CategoryId = 1, Name = "Recipe Test Pizza", Price = 500m });
        await context.SaveChangesAsync();

        var controller = new RecipesController(context, new TestBranchContext(1), new PosMenuCacheInvalidator());
        var result = await controller.Edit(new Recipe
        {
            ProductId = 803,
            Ingredients =
            {
                new RecipeIngredient { InventoryItemId = dough.Id, QuantityRequiredBase = 250m, DisplayQuantity = 1m, DisplayUnit = "Kg" },
                new RecipeIngredient { InventoryItemId = sauce.Id, QuantityRequiredBase = 80m, DisplayQuantity = 1m, DisplayUnit = "Liter" }
            }
        });

        Assert.IsType<RedirectToActionResult>(result);
        var recipe = await context.Recipes.Include(x => x.Ingredients).SingleAsync(x => x.ProductId == 803);
        Assert.Contains(recipe.Ingredients, x => x.InventoryItemId == dough.Id && x.QuantityRequiredBase == 250m && x.DisplayQuantity == 250m && x.DisplayUnit == "Gram");
        Assert.Contains(recipe.Ingredients, x => x.InventoryItemId == sauce.Id && x.QuantityRequiredBase == 80m && x.DisplayQuantity == 80m && x.DisplayUnit == "ML");
    }

    [Fact]
    public async Task Product_create_uses_explicit_dynamic_recipe_rows_and_inventory_base_units()
    {
        await using var context = CreateContext();
        var dough = new InventoryItem { Id = 821, BranchId = 1, Name = "Product Test Dough", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000, IsPreparedItem = true };
        var sauce = new InventoryItem { Id = 822, BranchId = 1, Name = "Product Test Sauce", BaseUnit = "ML", PurchaseUnitName = "Liter", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        var box = new InventoryItem { Id = 823, BranchId = 1, Name = "Product Test Box", BaseUnit = "Piece", PurchaseUnitName = "Piece", DefaultConversionFactorToBase = 1m, ReorderLevel = 10 };
        context.InventoryItems.AddRange(dough, sauce, box);
        await context.SaveChangesAsync();

        var controller = ProductController(context);
        var create = Assert.IsType<ViewResult>(await controller.Create());
        var createModel = Assert.IsType<ProductEditViewModel>(create.Model);
        Assert.Single(createModel.RecipeItems);
        Assert.Equal(0, createModel.RecipeItems[0].InventoryItemId);

        var saved = await controller.Create(new ProductEditViewModel
        {
            Name = "Dynamic Recipe Pizza",
            Price = 500m,
            CategoryId = 1,
            RecipeItems =
            {
                new RecipeItemQuantityViewModel(),
                new RecipeItemQuantityViewModel { InventoryItemId = dough.Id, QuantityRequired = 250m, Unit = "Kg" },
                new RecipeItemQuantityViewModel { InventoryItemId = sauce.Id, QuantityRequired = 80m, Unit = "Liter" }
            }
        });

        Assert.IsType<RedirectToActionResult>(saved);
        var product = await context.Products
            .Include(x => x.Recipes)
            .ThenInclude(x => x.Ingredients)
            .SingleAsync(x => x.Name == "Dynamic Recipe Pizza");
        var recipe = Assert.Single(product.Recipes);
        Assert.Equal(2, recipe.Ingredients.Count);
        Assert.Contains(recipe.Ingredients, x => x.InventoryItemId == dough.Id && x.QuantityRequiredBase == 250m && x.DisplayQuantity == 250m && x.DisplayUnit == "Gram");
        Assert.Contains(recipe.Ingredients, x => x.InventoryItemId == sauce.Id && x.QuantityRequiredBase == 80m && x.DisplayQuantity == 80m && x.DisplayUnit == "ML");
        Assert.DoesNotContain(recipe.Ingredients, x => x.InventoryItemId == box.Id);
    }

    [Fact]
    public async Task Product_create_rejects_partial_zero_and_duplicate_recipe_rows()
    {
        await using var context = CreateContext();
        var dough = new InventoryItem { Id = 831, BranchId = 1, Name = "Product Validation Dough", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000, IsPreparedItem = true };
        context.InventoryItems.Add(dough);
        await context.SaveChangesAsync();

        var controller = ProductController(context);
        var result = await controller.Create(new ProductEditViewModel
        {
            Name = "Invalid Dynamic Recipe",
            Price = 500m,
            CategoryId = 1,
            RecipeItems =
            {
                new RecipeItemQuantityViewModel { InventoryItemId = dough.Id },
                new RecipeItemQuantityViewModel { InventoryItemId = dough.Id, QuantityRequired = 0m },
                new RecipeItemQuantityViewModel { QuantityRequired = 10m }
            }
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.False(await context.Products.AnyAsync(x => x.Name == "Invalid Dynamic Recipe"));
    }

    [Fact]
    public async Task Product_create_allows_direct_sale_stock_item_without_recipe_rows()
    {
        await using var context = CreateContext();
        var coke = new InventoryItem { Id = 834, BranchId = 1, Name = "Direct Product Coke", BaseUnit = "Piece", PurchaseUnitName = "Crate", DefaultConversionFactorToBase = 24m, ReorderLevel = 10 };
        coke.ConsumptionMode = ConsumptionMode.DirectSale;
        InventoryControlDefaults.ApplyDefaults(coke);
        context.InventoryItems.Add(coke);
        await context.SaveChangesAsync();

        var controller = ProductController(context);
        var result = await controller.Create(new ProductEditViewModel
        {
            Name = "Direct Product Coke 1.5L",
            Price = 100m,
            CategoryId = 1,
            DirectInventoryItemId = coke.Id,
            DirectQuantityBase = 1m
        });

        Assert.IsType<RedirectToActionResult>(result);
        var product = await context.Products.Include(x => x.Recipes).SingleAsync(x => x.Name == "Direct Product Coke 1.5L");
        Assert.Equal(coke.Id, product.DirectInventoryItemId);
        Assert.Equal(1m, product.DirectQuantityBase);
        Assert.Empty(product.Recipes);
    }

    [Fact]
    public async Task Piece_based_prepared_inventory_item_defaults_to_piece_purchase_unit()
    {
        await using var context = CreateContext();
        var controller = new InventoryItemsController(context, new TestBranchContext(1));

        var result = await controller.Create(new InventoryItem
        {
            Name = "Large Dough Ball",
            BaseUnit = "Piece",
            IsPreparedItem = true,
            ReorderLevel = 10
        });

        Assert.IsType<RedirectToActionResult>(result);
        var item = await context.InventoryItems.SingleAsync(x => x.Name == "Large Dough Ball");
        Assert.True(item.IsPreparedItem);
        Assert.Equal("Piece", item.BaseUnit);
        Assert.Equal("Piece", item.PurchaseUnitName);
        Assert.Equal(1m, item.DefaultConversionFactorToBase);
    }

    [Fact]
    public async Task Inventory_item_create_returns_validation_error_for_duplicate_name_and_base_unit()
    {
        await using var context = CreateContext();
        context.InventoryItems.Add(new InventoryItem
        {
            Id = 841,
            BranchId = 1,
            Name = "Duplicate Flour",
            BaseUnit = "Gram",
            PurchaseUnitName = "Kg",
            DefaultConversionFactorToBase = 1000m,
            ReorderLevel = 10m
        });
        await context.SaveChangesAsync();

        var controller = new InventoryItemsController(context, new TestBranchContext(1));
        var result = await controller.Create(new InventoryItem
        {
            Name = " Duplicate Flour ",
            BaseUnit = "Gram",
            PurchaseUnitName = "Kg",
            DefaultConversionFactorToBase = 1000m,
            ReorderLevel = 20m
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(1, await context.InventoryItems.CountAsync(x => x.Name == "Duplicate Flour" && x.BaseUnit == "Gram"));
    }

    [Fact]
    public async Task Inventory_item_edit_returns_validation_error_for_duplicate_name_and_base_unit()
    {
        await using var context = CreateContext();
        context.InventoryItems.AddRange(
            new InventoryItem
            {
                Id = 842,
                BranchId = 1,
                Name = "Edit Existing Flour",
                BaseUnit = "Gram",
                PurchaseUnitName = "Kg",
                DefaultConversionFactorToBase = 1000m,
                ReorderLevel = 10m
            },
            new InventoryItem
            {
                Id = 843,
                BranchId = 1,
                Name = "Edit Other Sugar",
                BaseUnit = "Gram",
                PurchaseUnitName = "Kg",
                DefaultConversionFactorToBase = 1000m,
                ReorderLevel = 10m
            });
        await context.SaveChangesAsync();

        var controller = new InventoryItemsController(context, new TestBranchContext(1));
        var result = await controller.Edit(843, new InventoryItem
        {
            Id = 843,
            Name = "Edit Existing Flour",
            BaseUnit = "Gram",
            PurchaseUnitName = "Kg",
            DefaultConversionFactorToBase = 1000m,
            ReorderLevel = 20m,
            IsActive = true
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var item = await context.InventoryItems.AsNoTracking().SingleAsync(x => x.Id == 843);
        Assert.Equal("Edit Other Sugar", item.Name);
    }

    [Fact]
    public async Task Preparation_recipe_edit_requires_prepared_output_and_overwrites_display_units()
    {
        await using var context = CreateContext();
        var flour = new InventoryItem { Id = 811, BranchId = 1, Name = "Prep Test Flour", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        var water = new InventoryItem { Id = 812, BranchId = 1, Name = "Prep Test Water", BaseUnit = "ML", PurchaseUnitName = "Liter", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        var dough = new InventoryItem { Id = 813, BranchId = 1, Name = "Prep Test Dough", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000, IsPreparedItem = true };
        context.InventoryItems.AddRange(flour, water, dough);
        await context.SaveChangesAsync();

        var controller = new PreparationRecipesController(context, new TestBranchContext(1));
        var blocked = await controller.Edit(new PreparationRecipe
        {
            Name = "Bad Flour Output",
            OutputInventoryItemId = flour.Id,
            OutputQuantityBase = 1000m,
            Ingredients = { new PreparationRecipeIngredient { InventoryItemId = water.Id, QuantityBase = 1000m } }
        });
        Assert.IsType<ViewResult>(blocked);
        Assert.False(controller.ModelState.IsValid);

        controller = new PreparationRecipesController(context, new TestBranchContext(1));
        var saved = await controller.Edit(new PreparationRecipe
        {
            Name = "Prep Test Dough Batch",
            OutputInventoryItemId = dough.Id,
            OutputQuantityBase = 16000m,
            Ingredients =
            {
                new PreparationRecipeIngredient { InventoryItemId = flour.Id, QuantityBase = 10000m, DisplayQuantity = 1m, DisplayUnit = "Kg" },
                new PreparationRecipeIngredient { InventoryItemId = water.Id, QuantityBase = 6000m, DisplayQuantity = 6m, DisplayUnit = "Liter" }
            }
        });

        Assert.IsType<RedirectToActionResult>(saved);
        var recipe = await context.PreparationRecipes.Include(x => x.Ingredients).SingleAsync(x => x.Name == "Prep Test Dough Batch");
        Assert.Contains(recipe.Ingredients, x => x.InventoryItemId == flour.Id && x.QuantityBase == 10000m && x.DisplayQuantity == 10000m && x.DisplayUnit == "Gram");
        Assert.Contains(recipe.Ingredients, x => x.InventoryItemId == water.Id && x.QuantityBase == 6000m && x.DisplayQuantity == 6000m && x.DisplayUnit == "ML");
    }

    [Fact]
    public async Task Preparation_recipe_edit_rejects_duplicate_input_ingredients()
    {
        await using var context = CreateContext();
        var flour = new InventoryItem { Id = 821, BranchId = 1, Name = "Duplicate Prep Flour", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        var dough = new InventoryItem { Id = 822, BranchId = 1, Name = "Duplicate Prep Dough", BaseUnit = "Piece", PurchaseUnitName = "Piece", DefaultConversionFactorToBase = 1m, ReorderLevel = 10, IsPreparedItem = true };
        context.InventoryItems.AddRange(flour, dough);
        await context.SaveChangesAsync();

        var controller = new PreparationRecipesController(context, new TestBranchContext(1));
        var result = await controller.Edit(new PreparationRecipe
        {
            Name = "Duplicate Prep Batch",
            OutputInventoryItemId = dough.Id,
            OutputQuantityBase = 10m,
            Ingredients =
            {
                new PreparationRecipeIngredient { InventoryItemId = flour.Id, QuantityBase = 100m },
                new PreparationRecipeIngredient { InventoryItemId = flour.Id, QuantityBase = 100m }
            }
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors, x => x.ErrorMessage == "Duplicate ingredient rows are not allowed.");
    }

    [Fact]
    public async Task Session_rules_prevent_missing_duplicate_and_wrong_role_workflows()
    {
        await using var context = CreateContext();
        var (productId, _) = await CreateProductWithKitchenStockAsync(context, "Juice", 1m, 5m);
        var sessionService = UserSessionService(context);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            OrderService(context).FinalizeOrderAsync(OrderDto(productId, cashierId: "no-session")));

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            OrderService(context).FinalizeOrderAsync(OrderDto(productId, cashierId: StockManagerId, userSessionId: 2, customerPhone: "3040")));

        var existingSession = await sessionService.StartSessionAsync(new StartSessionDto
        {
            UserId = CashierId,
            BranchId = 1,
            RoleName = "Cashier",
            TerminalId = 1,
            TerminalCode = "MAIN-01",
            TerminalName = "Duplicate"
        });
        Assert.Equal(1, existingSession.Id);

        var draft = await OrderService(context).CreateDraftOrderAsync(DraftDto(productId, customerPhone: "3041"));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => sessionService.CloseSessionAsync(new CloseSessionDto
        {
            SessionId = 1,
            UserId = CashierId,
            TerminalId = 1,
            TerminalCode = "MAIN-01",
            CountedClosingCash = 0,
            ConfirmationText = "END"
        }));

        await OrderService(context).CancelDraftOrderAsync(draft.OrderId, CashierId);
        await sessionService.CloseSessionAsync(new CloseSessionDto
        {
            SessionId = 1,
            UserId = CashierId,
            TerminalId = 1,
            TerminalCode = "MAIN-01",
            CountedClosingCash = 0,
            ConfirmationText = "END",
            IsManagerOrAdmin = true
        });

        var interrupted = context.UserSessions.Single(x => x.Id == 1);
        interrupted.Status = SessionStatus.Abandoned;
        interrupted.EndedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        var resumed = await sessionService.ContinueSessionAsync(1, CashierId);
        Assert.Equal(SessionStatus.Reopened, resumed.Status);
    }

    [Fact]
    public async Task Branch_scoping_blocks_cross_branch_products_orders_and_preserves_scoped_uniqueness()
    {
        await using var context = CreateContext();
        var (branchAProductId, _) = await CreateProductWithKitchenStockAsync(context, "Branch A Product", 1m, 3m);
        _ = branchAProductId;
        await EnsureBranchLocationsAsync(context, 2);
        var branchTwoItem = new InventoryItem { Id = 301, BranchId = 2, Name = "Branch B Flour", BaseUnit = "Gram", ReorderLevel = 1 };
        context.InventoryItems.Add(branchTwoItem);
        context.Products.Add(ProductWithRecipe(301, "Branch B Product", branchTwoItem, 1m, price: 20m, branchId: 2));
        context.InventoryStocks.Add(new InventoryStock { BranchId = 2, InventoryItem = branchTwoItem, InventoryLocationId = 4, Quantity = 3m, AverageUnitCost = 10m });
        context.Customers.AddRange(
            new Customer { BranchId = 1, Name = "A", PhoneNumber = "555" },
            new Customer { BranchId = 2, Name = "B", PhoneNumber = "555" });
        context.Orders.AddRange(
            ExistingOrder(branchId: 1, orderNumber: "POS-SHARED", cashierId: CashierId, sessionId: 1, terminalId: 1, terminalCode: "MAIN-01"),
            ExistingOrder(branchId: 2, orderNumber: "POS-SHARED", cashierId: BranchTwoCashierId, sessionId: 3, terminalId: 2, terminalCode: "B2-01"));
        await context.SaveChangesAsync();

        var branchAService = OrderService(context);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => branchAService.FinalizeOrderAsync(OrderDto(301, customerPhone: "3050")));

        var branchBDraft = await OrderService(context, branchId: 2).CreateDraftOrderAsync(DraftDto(301, cashierId: BranchTwoCashierId, userSessionId: 3, terminalId: 2, terminalCode: "B2-01", customerPhone: "3051"));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            branchAService.FinalizeOrderAsync(OrderDto(301, draftId: branchBDraft.OrderId, customerPhone: "3052")));

        Assert.Equal(2, await context.Customers.CountAsync(x => x.PhoneNumber == "555"));
        Assert.Equal(2, await context.Orders.CountAsync(x => x.OrderNumber == "POS-SHARED"));
    }

    [Fact]
    public async Task Purchase_dispatch_sale_flow_uses_stock_room_then_kitchen_then_consumption_costs()
    {
        await using var context = CreateContext();
        var item = new InventoryItem { Id = 401, BranchId = 1, Name = "Mozzarella", BaseUnit = "Gram", PurchaseUnitName = "Gram", DefaultConversionFactorToBase = 1m, ReorderLevel = 1m };
        var product = ProductWithRecipe(401, "Cheese Pizza", item, 0.5m, price: 100m);
        context.InventoryItems.Add(item);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        await PurchaseService(context).CreatePurchaseAsync(new CreatePurchaseDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            BranchId = 1,
            UserSessionId = 2,
            PerformedByUserId = StockManagerId,
            TerminalId = 3,
            TerminalCode = "STOCK-01",
            SupplierId = 1,
            Items = { new PurchaseItemDto { InventoryItemId = item.Id, PurchaseQuantity = 10m, PurchaseUnitName = "Gram", ConversionFactorToBase = 1m, UnitCostPerPurchaseUnit = 20m } }
        });

        var stockRoom = await StockRoomLocationAsync(context);
        var kitchen = await KitchenLocationAsync(context);
        Assert.Equal(10m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        Assert.Equal(200m, (await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == item.Id && x.MovementType == InventoryMovementType.Purchase)).TotalCost);

        var request = new KitchenRequest
        {
            BranchId = 1,
            RequestNumber = $"KR-TEST-{Guid.NewGuid():N}"[..24],
            Status = KitchenRequestStatus.Approved,
            RequestedByUserId = StockManagerId,
            ApprovedByUserId = StockManagerId,
            ApprovedAt = DateTime.UtcNow,
            Details = { new KitchenRequestDetail { InventoryItemId = item.Id, RequestedQuantity = 4m, ApprovedQuantity = 4m } }
        };
        context.KitchenRequests.Add(request);
        await context.SaveChangesAsync();

        await new RestaurantInventoryService(context, new TestBranchContext(1), new InventoryTransactionService(context), new TestIdempotencyService()).DispatchKitchenRequestAsync(request.Id, StockManagerId);
        Assert.Equal(6m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        Assert.Equal(4m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        Assert.Equal(80m, (await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == item.Id && x.MovementType == InventoryMovementType.StockRoomToKitchenDispatch)).TotalCost);

        await OrderService(context).FinalizeOrderAsync(OrderDto(product.Id, quantity: 2, customerPhone: "4010"));
        Assert.Equal(3m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        var consumption = await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == item.Id && x.MovementType == InventoryMovementType.Consumption);
        Assert.Equal(20m, consumption.UnitCostBase);
        Assert.Equal(20m, consumption.TotalCost);

        context.OperationalExpenses.Add(new OperationalExpense
        {
            BranchId = 1,
            ExpenseCategoryId = 1,
            Amount = 30m,
            ExpenseDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var profit = await new RestaurantInventoryService(context, new TestBranchContext(1), new InventoryTransactionService(context), new TestIdempotencyService()).BuildProfitReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.Equal(200m, profit.SalesRevenue);
        Assert.Equal(20m, profit.IngredientCost);
        Assert.Equal(30m, profit.OperationalExpenses);
        Assert.Equal(150m, profit.NetProfit);
    }

    [Fact]
    public async Task Stock_manager_can_dispatch_less_or_more_than_recommended_quantity()
    {
        await using var context = CreateContext();
        var stockRoom = await StockRoomLocationAsync(context);
        var kitchen = await KitchenLocationAsync(context);
        var item = new InventoryItem { Id = 441, BranchId = 1, Name = "Manager Choice Sauce", BaseUnit = "ML", ReorderLevel = 100m };
        context.InventoryItems.Add(item);
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = stockRoom.Id, QuantityBase = 20m, AverageUnitCostBase = 2m });
        var lessRequest = new KitchenRequest
        {
            BranchId = 1,
            RequestNumber = $"KR-LESS-{Guid.NewGuid():N}"[..24],
            Status = KitchenRequestStatus.PendingManagerReview,
            RequestSource = KitchenRequestSource.Auto,
            KitchenLocationId = kitchen.Id,
            Details =
            {
                new KitchenRequestDetail
                {
                    InventoryItem = item,
                    KitchenLocationId = kitchen.Id,
                    RequestSource = KitchenRequestSource.Auto,
                    RequestedQuantity = 5m,
                    RecommendedQuantity = 5m
                }
            }
        };
        var moreRequest = new KitchenRequest
        {
            BranchId = 1,
            RequestNumber = $"KR-MORE-{Guid.NewGuid():N}"[..24],
            Status = KitchenRequestStatus.PendingManagerReview,
            RequestSource = KitchenRequestSource.Manual,
            KitchenLocationId = kitchen.Id,
            Details =
            {
                new KitchenRequestDetail
                {
                    InventoryItem = item,
                    KitchenLocationId = kitchen.Id,
                    RequestSource = KitchenRequestSource.Manual,
                    RequestedQuantity = 5m,
                    RecommendedQuantity = 5m
                }
            }
        };
        context.KitchenRequests.AddRange(lessRequest, moreRequest);
        await context.SaveChangesAsync();

        var service = new RestaurantInventoryService(context, new TestBranchContext(1), new InventoryTransactionService(context), new TestIdempotencyService());
        await service.DispatchKitchenRequestAsync(lessRequest.Id, StockManagerId, new Dictionary<int, decimal> { [lessRequest.Details.Single().Id] = 2m });
        await service.DispatchKitchenRequestAsync(moreRequest.Id, StockManagerId, new Dictionary<int, decimal> { [moreRequest.Details.Single().Id] = 6m });

        Assert.Equal(12m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        Assert.Equal(8m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        Assert.Equal(KitchenRequestStatus.PartiallyDispatched, (await context.KitchenRequests.SingleAsync(x => x.Id == lessRequest.Id)).Status);
        Assert.Equal(KitchenRequestStatus.Dispatched, (await context.KitchenRequests.SingleAsync(x => x.Id == moreRequest.Id)).Status);
        Assert.Equal(2, await context.InventoryMovements.CountAsync(x => x.MovementType == InventoryMovementType.StockRoomToKitchenDispatch && x.InventoryItemId == item.Id));
    }

    [Fact]
    public async Task Purchase_conversion_stores_flour_stock_in_base_grams()
    {
        await using var context = CreateContext();
        var flour = new InventoryItem
        {
            Id = 601,
            BranchId = 1,
            Name = "Flour",
            BaseUnit = "Gram",
            PurchaseUnitName = "20kg Bag",
            DefaultConversionFactorToBase = 20000,
            ReorderLevel = 1000
        };
        context.InventoryItems.Add(flour);
        await context.SaveChangesAsync();

        await PurchaseService(context).CreatePurchaseAsync(new CreatePurchaseDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            BranchId = 1,
            UserSessionId = 2,
            PerformedByUserId = StockManagerId,
            TerminalId = 3,
            TerminalCode = "STOCK-01",
            SupplierId = 1,
            Items = { new PurchaseItemDto { InventoryItemId = flour.Id, PurchaseQuantity = 1m, PurchaseUnitName = "20kg Bag", ConversionFactorToBase = 20000m, UnitCostPerPurchaseUnit = 4000m } }
        });

        var stockRoom = await StockRoomLocationAsync(context);
        var stock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == flour.Id && x.InventoryLocationId == stockRoom.Id);
        Assert.Equal(20000m, stock.QuantityBase);
        Assert.Equal(0.2m, stock.AverageUnitCostBase);
    }

    [Fact]
    public async Task Pizza_recipe_consumes_100_grams_from_kitchen_base_stock()
    {
        await using var context = CreateContext();
        var flour = new InventoryItem { Id = 602, BranchId = 1, Name = "Pizza Flour", BaseUnit = "Gram", ReorderLevel = 1000 };
        context.InventoryItems.Add(flour);
        context.Products.Add(ProductWithRecipe(602, "Pizza 100g", flour, 100m, price: 500m));
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = flour, InventoryLocationId = 2, QuantityBase = 2000m, AverageUnitCostBase = 0.2m });
        await context.SaveChangesAsync();

        await OrderService(context).FinalizeOrderAsync(OrderDto(602, customerPhone: "6020"));

        var kitchen = await KitchenLocationAsync(context);
        Assert.Equal(1900m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == flour.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        var movement = await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == flour.Id && x.MovementType == InventoryMovementType.Consumption);
        Assert.Equal(100m, movement.QuantityBase);
        Assert.Equal(20m, movement.TotalCost);
    }

    [Fact]
    public async Task Coke_crate_purchase_becomes_12_base_pieces()
    {
        await using var context = CreateContext();
        var coke = new InventoryItem { Id = 603, BranchId = 1, Name = "Coke Bottle", BaseUnit = "Piece", PurchaseUnitName = "Crate", DefaultConversionFactorToBase = 12, ReorderLevel = 12 };
        context.InventoryItems.Add(coke);
        await context.SaveChangesAsync();

        await PurchaseService(context).CreatePurchaseAsync(new CreatePurchaseDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            BranchId = 1,
            UserSessionId = 2,
            PerformedByUserId = StockManagerId,
            TerminalId = 3,
            TerminalCode = "STOCK-01",
            SupplierId = 1,
            Items = { new PurchaseItemDto { InventoryItemId = coke.Id, PurchaseQuantity = 5m, ConversionFactorToBase = 12m, PurchaseUnitName = "Crate", UnitCostPerPurchaseUnit = 1200m } }
        });

        var stockRoom = await StockRoomLocationAsync(context);
        var stock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == coke.Id && x.InventoryLocationId == stockRoom.Id);
        Assert.Equal(60m, stock.QuantityBase);
        Assert.Equal(100m, stock.AverageUnitCostBase);
    }

    [Fact]
    public async Task Purchase_service_rejects_invalid_rows_duplicates_and_prepared_items()
    {
        await using var context = CreateContext();
        var flour = new InventoryItem { Id = 607, BranchId = 1, Name = "Purchase Validation Flour", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        var unconfigured = new InventoryItem { Id = 610, BranchId = 1, Name = "Unconfigured Purchase Item", BaseUnit = "Gram", ReorderLevel = 1000 };
        var dough = new InventoryItem { Id = 608, BranchId = 1, Name = "Purchase Validation Dough", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000, IsPreparedItem = true };
        context.InventoryItems.AddRange(flour, unconfigured, dough);
        await context.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => PurchaseService(context).CreatePurchaseAsync(PurchaseDto(0,
            new PurchaseItemDto { InventoryItemId = flour.Id, PurchaseQuantity = 1m, PurchaseUnitName = "Kg", ConversionFactorToBase = 1000m, UnitCostPerPurchaseUnit = 200m })));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => PurchaseService(context).CreatePurchaseAsync(PurchaseDto(1,
            new PurchaseItemDto { InventoryItemId = flour.Id, PurchaseQuantity = 0m, PurchaseUnitName = "Kg", ConversionFactorToBase = 1000m, UnitCostPerPurchaseUnit = 200m })));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => PurchaseService(context).CreatePurchaseAsync(PurchaseDto(1,
            new PurchaseItemDto { InventoryItemId = flour.Id, PurchaseQuantity = 1m, PurchaseUnitName = "Kg", ConversionFactorToBase = 1000m, UnitCostPerPurchaseUnit = 0m })));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => PurchaseService(context).CreatePurchaseAsync(PurchaseDto(1,
            new PurchaseItemDto { InventoryItemId = unconfigured.Id, PurchaseQuantity = 1m, UnitCostPerPurchaseUnit = 200m })));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => PurchaseService(context).CreatePurchaseAsync(PurchaseDto(1,
            new PurchaseItemDto { InventoryItemId = dough.Id, PurchaseQuantity = 1m, PurchaseUnitName = "Kg", ConversionFactorToBase = 1000m, UnitCostPerPurchaseUnit = 200m })));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => PurchaseService(context).CreatePurchaseAsync(PurchaseDto(1,
            new PurchaseItemDto { InventoryItemId = flour.Id, PurchaseQuantity = 1m, PurchaseUnitName = "Kg", ConversionFactorToBase = 1000m, UnitCostPerPurchaseUnit = 200m },
            new PurchaseItemDto { InventoryItemId = flour.Id, PurchaseQuantity = 1m, PurchaseUnitName = "Kg", ConversionFactorToBase = 1000m, UnitCostPerPurchaseUnit = 200m })));

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => PurchaseService(context).CreatePurchaseAsync(PurchaseDto(1,
            new PurchaseItemDto { InventoryItemId = flour.Id, PurchaseQuantity = 1m, PurchaseUnitName = "Crate", ConversionFactorToBase = 999m, UnitCostPerPurchaseUnit = 200m })));

        await PurchaseService(context).CreatePurchaseAsync(PurchaseDto(1,
            new PurchaseItemDto { InventoryItemId = flour.Id, PurchaseQuantity = 1m, PurchaseUnitName = "Kg", ConversionFactorToBase = 1000m, UnitCostPerPurchaseUnit = 200m }));

        var stockRoom = await StockRoomLocationAsync(context);
        var stock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == flour.Id && x.InventoryLocationId == stockRoom.Id);
        var movement = await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == flour.Id && x.MovementType == InventoryMovementType.Purchase);
        Assert.Equal(1000m, stock.QuantityBase);
        Assert.Equal(1000m, movement.QuantityBase);
        Assert.Equal(0.2m, movement.UnitCostBase);
        Assert.Empty(await context.InventoryMovements.Where(x => x.MovementType == InventoryMovementType.Purchase && x.InventoryItemId == dough.Id).ToListAsync());
    }

    [Fact]
    public async Task Twenty_kg_flour_purchase_uses_purchase_unit_cost_and_server_calculated_total()
    {
        await using var context = CreateContext();
        var flour = new InventoryItem { Id = 609, BranchId = 1, Name = "Purchase Calc Flour", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        context.InventoryItems.Add(flour);
        await context.SaveChangesAsync();

        await PurchaseService(context).CreatePurchaseAsync(PurchaseDto(1,
            new PurchaseItemDto
            {
                InventoryItemId = flour.Id,
                PurchaseQuantity = 20m,
                PurchaseUnitName = "Kg",
                ConversionFactorToBase = 1000m,
                UnitCostPerPurchaseUnit = 200m,
                TotalCost = 1m
            }));

        var stockRoom = await StockRoomLocationAsync(context);
        var stock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == flour.Id && x.InventoryLocationId == stockRoom.Id);
        var movement = await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == flour.Id && x.MovementType == InventoryMovementType.Purchase);
        Assert.Equal(20000m, stock.QuantityBase);
        Assert.Equal(0.2m, stock.AverageUnitCostBase);
        Assert.Equal(20000m, movement.QuantityBase);
        Assert.Equal(0.2m, movement.UnitCostBase);
        Assert.Equal(4000m, movement.TotalCost);
    }

    [Fact]
    public async Task Dough_batch_produces_stock_room_inventory_that_transfers_to_kitchen_and_sells()
    {
        await using var context = CreateContext();
        var stockRoom = await StockRoomLocationAsync(context);
        var kitchen = await KitchenLocationAsync(context);
        var flour = new InventoryItem { Id = 701, BranchId = 1, Name = "Batch Test Flour", BaseUnit = "Gram", PurchaseUnitName = "20kg Bag", DefaultConversionFactorToBase = 20000, ReorderLevel = 1000 };
        var yeast = new InventoryItem { Id = 702, BranchId = 1, Name = "Batch Test Yeast", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000, ReorderLevel = 100 };
        var salt = new InventoryItem { Id = 703, BranchId = 1, Name = "Batch Test Salt", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000, ReorderLevel = 100 };
        var water = new InventoryItem { Id = 704, BranchId = 1, Name = "Batch Test Water", BaseUnit = "ML", PurchaseUnitName = "Liter", DefaultConversionFactorToBase = 1000, ReorderLevel = 1000 };
        var dough = new InventoryItem { Id = 705, BranchId = 1, Name = "Batch Test Dough", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000, ReorderLevel = 1000, IsPreparedItem = true };
        context.InventoryItems.AddRange(flour, yeast, salt, water, dough);
        context.InventoryStocks.AddRange(
            new InventoryStock { BranchId = 1, InventoryItem = flour, InventoryLocationId = stockRoom.Id, QuantityBase = 10000m, AverageUnitCostBase = 0.20m },
            new InventoryStock { BranchId = 1, InventoryItem = yeast, InventoryLocationId = stockRoom.Id, QuantityBase = 100m, AverageUnitCostBase = 2.00m },
            new InventoryStock { BranchId = 1, InventoryItem = salt, InventoryLocationId = stockRoom.Id, QuantityBase = 200m, AverageUnitCostBase = 0.10m },
            new InventoryStock { BranchId = 1, InventoryItem = water, InventoryLocationId = stockRoom.Id, QuantityBase = 6000m, AverageUnitCostBase = 0.01m });
        context.PreparationRecipes.Add(new PreparationRecipe
        {
            BranchId = 1,
            Name = "Dough Batch",
            OutputInventoryItem = dough,
            OutputQuantityBase = 16000m,
            Ingredients =
            {
                new PreparationRecipeIngredient { InventoryItem = flour, QuantityBase = 10000m },
                new PreparationRecipeIngredient { InventoryItem = yeast, QuantityBase = 100m },
                new PreparationRecipeIngredient { InventoryItem = salt, QuantityBase = 200m },
                new PreparationRecipeIngredient { InventoryItem = water, QuantityBase = 6000m }
            }
        });
        context.Products.Add(ProductWithRecipe(706, "Pizza", dough, 250m, price: 500m));
        await context.SaveChangesAsync();

        var recipe = await context.PreparationRecipes.SingleAsync(x => x.Name == "Dough Batch");
        await PreparationService(context).CompletePreparationBatchAsync(PreparationDto(recipe.Id));

        var expectedCost = (10000m * 0.20m) + (100m * 2.00m) + (200m * 0.10m) + (6000m * 0.01m);
        var doughStock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == dough.Id && x.InventoryLocationId == stockRoom.Id);
        Assert.Equal(16000m, doughStock.QuantityBase);
        Assert.Equal(expectedCost / 16000m, doughStock.AverageUnitCostBase);
        Assert.Equal(0m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == flour.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        Assert.Equal(0m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == yeast.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        Assert.Equal(0m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == salt.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        Assert.Equal(0m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == water.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        Assert.Equal(1, await context.InventoryMovements.CountAsync(x => x.InventoryItemId == dough.Id && x.MovementType == InventoryMovementType.Production));
        Assert.Equal(4, await context.InventoryMovements.CountAsync(x => x.ReferenceType == nameof(PreparationBatch) && x.MovementType == InventoryMovementType.Consumption));

        var request = new KitchenRequest
        {
            BranchId = 1,
            RequestNumber = $"KR-DOUGH-{Guid.NewGuid():N}"[..24],
            Status = KitchenRequestStatus.Approved,
            RequestedByUserId = StockManagerId,
            ApprovedByUserId = StockManagerId,
            ApprovedAt = DateTime.UtcNow,
            Details = { new KitchenRequestDetail { InventoryItemId = dough.Id, RequestedQuantity = 1000m, ApprovedQuantity = 1000m } }
        };
        context.KitchenRequests.Add(request);
        await context.SaveChangesAsync();
        await new RestaurantInventoryService(context, new TestBranchContext(1), new InventoryTransactionService(context), new TestIdempotencyService()).DispatchKitchenRequestAsync(request.Id, StockManagerId);
        Assert.Equal(15000m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == dough.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        Assert.Equal(1000m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == dough.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);

        await OrderService(context).FinalizeOrderAsync(OrderDto(706, quantity: 2, customerPhone: "7060"));

        Assert.Equal(500m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == dough.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        var saleConsumption = await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == dough.Id && x.MovementType == InventoryMovementType.Consumption && x.ReferenceType == nameof(Order));
        Assert.Equal(500m, saleConsumption.QuantityBase);
        Assert.Equal((expectedCost / 16000m) * 500m, saleConsumption.TotalCost);
    }

    [Fact]
    public async Task Piece_based_dough_ball_batch_can_complete_actual_output_quantity_and_sells_by_piece()
    {
        await using var context = CreateContext();
        var stockRoom = await StockRoomLocationAsync(context);
        var kitchen = await KitchenLocationAsync(context);
        var flour = new InventoryItem { Id = 841, BranchId = 1, Name = "Ball Test Flour", BaseUnit = "Gram", PurchaseUnitName = "20kg Bag", DefaultConversionFactorToBase = 20000, ReorderLevel = 1000 };
        var yeast = new InventoryItem { Id = 842, BranchId = 1, Name = "Ball Test Yeast", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000, ReorderLevel = 100 };
        var salt = new InventoryItem { Id = 843, BranchId = 1, Name = "Ball Test Salt", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000, ReorderLevel = 100 };
        var water = new InventoryItem { Id = 844, BranchId = 1, Name = "Ball Test Water", BaseUnit = "ML", PurchaseUnitName = "Liter", DefaultConversionFactorToBase = 1000, ReorderLevel = 1000 };
        var doughBall = new InventoryItem { Id = 845, BranchId = 1, Name = "Large Dough Ball", BaseUnit = "Piece", PurchaseUnitName = "Piece", DefaultConversionFactorToBase = 1, ReorderLevel = 10, IsPreparedItem = true };
        var cheese = new InventoryItem { Id = 846, BranchId = 1, Name = "Ball Test Cheese", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000, ReorderLevel = 1000 };
        var sauce = new InventoryItem { Id = 847, BranchId = 1, Name = "Ball Test Sauce", BaseUnit = "ML", PurchaseUnitName = "Liter", DefaultConversionFactorToBase = 1000, ReorderLevel = 1000 };
        context.InventoryItems.AddRange(flour, yeast, salt, water, doughBall, cheese, sauce);
        context.InventoryStocks.AddRange(
            new InventoryStock { BranchId = 1, InventoryItem = flour, InventoryLocationId = stockRoom.Id, QuantityBase = 10000m, AverageUnitCostBase = 0.20m },
            new InventoryStock { BranchId = 1, InventoryItem = yeast, InventoryLocationId = stockRoom.Id, QuantityBase = 100m, AverageUnitCostBase = 2.00m },
            new InventoryStock { BranchId = 1, InventoryItem = salt, InventoryLocationId = stockRoom.Id, QuantityBase = 200m, AverageUnitCostBase = 0.10m },
            new InventoryStock { BranchId = 1, InventoryItem = water, InventoryLocationId = stockRoom.Id, QuantityBase = 6000m, AverageUnitCostBase = 0.01m },
            new InventoryStock { BranchId = 1, InventoryItem = cheese, InventoryLocationId = kitchen.Id, QuantityBase = 1000m, AverageUnitCostBase = 1.5m },
            new InventoryStock { BranchId = 1, InventoryItem = sauce, InventoryLocationId = kitchen.Id, QuantityBase = 1000m, AverageUnitCostBase = 0.5m });
        context.PreparationRecipes.Add(new PreparationRecipe
        {
            BranchId = 1,
            Name = "Large Dough Ball Batch",
            OutputInventoryItem = doughBall,
            OutputQuantityBase = 64m,
            Ingredients =
            {
                new PreparationRecipeIngredient { InventoryItem = flour, QuantityBase = 10000m },
                new PreparationRecipeIngredient { InventoryItem = yeast, QuantityBase = 100m },
                new PreparationRecipeIngredient { InventoryItem = salt, QuantityBase = 200m },
                new PreparationRecipeIngredient { InventoryItem = water, QuantityBase = 6000m }
            }
        });
        context.Products.Add(new Product
        {
            Id = 848,
            BranchId = 1,
            CategoryId = 1,
            Name = "Large Pizza With Dough Ball",
            Price = 900m,
            Recipes =
            {
                new Recipe
                {
                    BranchId = 1,
                    Ingredients =
                    {
                        new RecipeIngredient { InventoryItem = doughBall, QuantityRequiredBase = 1m, DisplayQuantity = 1m, DisplayUnit = "Piece" },
                        new RecipeIngredient { InventoryItem = cheese, QuantityRequiredBase = 120m, DisplayQuantity = 120m, DisplayUnit = "Gram" },
                        new RecipeIngredient { InventoryItem = sauce, QuantityRequiredBase = 80m, DisplayQuantity = 80m, DisplayUnit = "ML" }
                    }
                }
            }
        });
        await context.SaveChangesAsync();

        var recipe = await context.PreparationRecipes.SingleAsync(x => x.Name == "Large Dough Ball Batch");
        await PreparationService(context).CompletePreparationBatchAsync(PreparationDto(recipe.Id, 20m));

        var expectedCost = ((10000m * 0.20m) + (100m * 2.00m) + (200m * 0.10m) + (6000m * 0.01m)) * (20m / 64m);
        var outputStock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == doughBall.Id && x.InventoryLocationId == stockRoom.Id);
        Assert.Equal(20m, outputStock.QuantityBase);
        Assert.Equal(expectedCost / 20m, outputStock.AverageUnitCostBase);
        var production = await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == doughBall.Id && x.MovementType == InventoryMovementType.Production);
        Assert.Equal(20m, production.QuantityBase);
        Assert.Equal(expectedCost / 20m, production.UnitCostBase);
        Assert.Equal(10000m - (10000m * (20m / 64m)), (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == flour.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);

        var request = new KitchenRequest
        {
            BranchId = 1,
            RequestNumber = $"KR-BALL-{Guid.NewGuid():N}"[..24],
            Status = KitchenRequestStatus.Approved,
            RequestedByUserId = StockManagerId,
            ApprovedByUserId = StockManagerId,
            ApprovedAt = DateTime.UtcNow,
            Details = { new KitchenRequestDetail { InventoryItemId = doughBall.Id, RequestedQuantity = 10m, ApprovedQuantity = 10m } }
        };
        context.KitchenRequests.Add(request);
        await context.SaveChangesAsync();
        await new RestaurantInventoryService(context, new TestBranchContext(1), new InventoryTransactionService(context), new TestIdempotencyService()).DispatchKitchenRequestAsync(request.Id, StockManagerId);
        Assert.Equal(10m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == doughBall.Id && x.InventoryLocationId == stockRoom.Id)).QuantityBase);
        Assert.Equal(10m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == doughBall.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);

        await OrderService(context).FinalizeOrderAsync(OrderDto(848, quantity: 2, customerPhone: "8480"));

        Assert.Equal(8m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == doughBall.Id && x.InventoryLocationId == kitchen.Id)).QuantityBase);
        var saleConsumption = await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == doughBall.Id && x.MovementType == InventoryMovementType.Consumption && x.ReferenceType == nameof(Order));
        Assert.Equal(2m, saleConsumption.QuantityBase);
        Assert.Equal(expectedCost / 20m, saleConsumption.UnitCostBase);
        Assert.Equal((expectedCost / 20m) * 2m, saleConsumption.TotalCost);
    }

    [Fact]
    public async Task Preparation_batch_requires_valid_active_stock_session_and_real_terminal()
    {
        await using var context = CreateContext();
        var recipeId = await SeedSimplePreparationRecipeAsync(context, 901, outputBaseUnit: "Piece", outputQuantity: 10m);

        var missingSession = PreparationDto(recipeId);
        missingSession.UserSessionId = 0;
        await Assert.ThrowsAsync<BusinessException>(() =>
            PreparationService(context).CompletePreparationBatchAsync(missingSession));

        var cashierSession = PreparationDto(recipeId);
        cashierSession.UserSessionId = 1;
        cashierSession.CreatedByUserId = CashierId;
        cashierSession.TerminalId = 1;
        cashierSession.TerminalCode = "MAIN-01";
        await Assert.ThrowsAsync<BusinessException>(() =>
            PreparationService(context).CompletePreparationBatchAsync(cashierSession));

        var wrongTerminal = PreparationDto(recipeId);
        wrongTerminal.TerminalId = 1;
        wrongTerminal.TerminalCode = "MAIN-01";
        await Assert.ThrowsAsync<BusinessException>(() =>
            PreparationService(context).CompletePreparationBatchAsync(wrongTerminal));

        var stockSession = await context.UserSessions.SingleAsync(x => x.Id == 2);
        stockSession.TerminalId = 4;
        stockSession.TerminalCode = "INACTIVE-01";
        stockSession.TerminalName = "Inactive Terminal";
        await context.SaveChangesAsync();
        var inactiveTerminal = PreparationDto(recipeId);
        inactiveTerminal.TerminalId = 4;
        inactiveTerminal.TerminalCode = "INACTIVE-01";
        await Assert.ThrowsAsync<BusinessException>(() =>
            PreparationService(context).CompletePreparationBatchAsync(inactiveTerminal));
    }

    [Fact]
    public async Task Preparation_authorization_cannot_be_bypassed_without_valid_session()
    {
        await using var context = CreateContext();
        var recipeId = await SeedSimplePreparationRecipeAsync(context, 911, outputBaseUnit: "Piece", outputQuantity: 10m);

        var fakeAdmin = PreparationDto(recipeId);
        fakeAdmin.UserSessionId = 0;
        await Assert.ThrowsAsync<BusinessException>(() =>
            PreparationService(context).CompletePreparationBatchAsync(fakeAdmin));
    }

    [Fact]
    public async Task Preparation_batch_idempotency_prevents_duplicate_stock_and_rejects_payload_changes()
    {
        await using var context = CreateContext();
        var recipeId = await SeedSimplePreparationRecipeAsync(context, 921, outputBaseUnit: "Piece", outputQuantity: 10m);
        var idempotency = new TestIdempotencyService();
        var service = PreparationService(context, idempotencyService: idempotency);
        var key = Guid.NewGuid().ToString("N");

        var firstId = await service.CompletePreparationBatchAsync(PreparationDto(recipeId, 10m, key));
        var secondId = await service.CompletePreparationBatchAsync(PreparationDto(recipeId, 10m, key));

        Assert.Equal(firstId, secondId);
        Assert.Equal(1, await context.PreparationBatches.CountAsync(x => x.IdempotencyKey == key));
        Assert.Equal(2, await context.InventoryMovements.CountAsync(x => x.ReferenceType == nameof(PreparationBatch) && x.ReferenceId == firstId));
        Assert.Equal(10m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == 923 && x.InventoryLocationId == 1)).QuantityBase);

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.CompletePreparationBatchAsync(PreparationDto(recipeId, 5m, key)));
    }

    [Fact]
    public async Task Prepared_stock_controller_posts_actual_output_quantity_without_batch_multiplier()
    {
        await using var context = CreateContext();
        var recipeId = await SeedSimplePreparationRecipeAsync(context, 925, outputBaseUnit: "Piece", outputQuantity: 64m);
        var idempotency = new TestIdempotencyService();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, StockManagerId),
                new Claim(ClaimTypes.Role, "StockManager")
            }, "TestAuth"))
        };
        var controller = new PreparedStockController(
            context,
            new TestBranchContext(1),
            PreparationService(context, idempotencyService: idempotency),
            new StaticUserSessionService(context, 2),
            new StaticTerminalContextService(context, "STOCK-01"),
            idempotency)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };

        var result = await controller.Add(new PreparationBatchViewModel
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            PreparationRecipeId = recipeId,
            OutputQuantityBase = 20m
        });

        Assert.IsType<RedirectToActionResult>(result);
        var outputStock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == 927 && x.InventoryLocationId == 1);
        Assert.Equal(20m, outputStock.QuantityBase);
        Assert.NotEqual(1280m, outputStock.QuantityBase);
        var ingredientStock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == 925 && x.InventoryLocationId == 1);
        Assert.Equal(1000m - (128m * (20m / 64m)), ingredientStock.QuantityBase);
    }

    [Fact]
    public async Task Prepared_stock_controller_bulk_dough_count_deducts_matching_recipe_amount()
    {
        await using var context = CreateContext();
        var recipeId = await SeedSimplePreparationRecipeAsync(context, 928, outputBaseUnit: "Gram", outputQuantity: 200m);
        var ingredientStock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == 928 && x.InventoryLocationId == 1);
        ingredientStock.QuantityBase = 10000m;
        await context.SaveChangesAsync();

        var idempotency = new TestIdempotencyService();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, StockManagerId),
                new Claim(ClaimTypes.Role, "StockManager")
            }, "TestAuth"))
        };
        var controller = new PreparedStockController(
            context,
            new TestBranchContext(1),
            PreparationService(context, idempotencyService: idempotency),
            new StaticUserSessionService(context, 2),
            new StaticTerminalContextService(context, "STOCK-01"),
            idempotency)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };

        var result = await controller.Add(new PreparationBatchViewModel
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            PreparationRecipeId = recipeId,
            PreparedItemCount = 10m
        });

        Assert.IsType<RedirectToActionResult>(result);
        var outputStock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == 930 && x.InventoryLocationId == 1);
        Assert.Equal(2000m, outputStock.QuantityBase);
        Assert.Equal(6000m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == 928 && x.InventoryLocationId == 1)).QuantityBase);
    }

    [Fact]
    public async Task Piece_prepared_output_rejects_fractional_quantity_and_accepts_whole_quantity()
    {
        await using var context = CreateContext();
        var recipeId = await SeedSimplePreparationRecipeAsync(context, 931, outputBaseUnit: "Piece", outputQuantity: 10m);
        var service = PreparationService(context);

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.CompletePreparationBatchAsync(PreparationDto(recipeId, 10.5m)));

        await service.CompletePreparationBatchAsync(PreparationDto(recipeId, 10m));
        Assert.Equal(10m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == 933 && x.InventoryLocationId == 1)).QuantityBase);
    }

    [Fact]
    public async Task Preparation_recipe_rejects_inactive_items_and_non_prepared_output()
    {
        await using var context = CreateContext();
        var inactiveOutputRecipe = await SeedSimplePreparationRecipeAsync(context, 941, outputBaseUnit: "Piece", outputQuantity: 10m);
        var inactiveOutput = await context.InventoryItems.SingleAsync(x => x.Id == 943);
        inactiveOutput.IsActive = false;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessException>(() =>
            PreparationService(context).CompletePreparationBatchAsync(PreparationDto(inactiveOutputRecipe, 10m)));

        var inactiveInputRecipe = await SeedSimplePreparationRecipeAsync(context, 951, outputBaseUnit: "Piece", outputQuantity: 10m);
        var inactiveInput = await context.InventoryItems.SingleAsync(x => x.Id == 951);
        inactiveInput.IsActive = false;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessException>(() =>
            PreparationService(context).CompletePreparationBatchAsync(PreparationDto(inactiveInputRecipe, 10m)));

        var rawOutputRecipe = await SeedSimplePreparationRecipeAsync(context, 961, outputBaseUnit: "Piece", outputQuantity: 10m, preparedOutput: false);
        await Assert.ThrowsAsync<BusinessException>(() =>
            PreparationService(context).CompletePreparationBatchAsync(PreparationDto(rawOutputRecipe, 10m)));
    }

    [Fact]
    public async Task Failed_preparation_batch_rolls_back_all_mutations()
    {
        await using var context = CreateContext();
        var recipeId = await SeedSimplePreparationRecipeAsync(context, 971, outputBaseUnit: "Piece", outputQuantity: 10m);
        var ingredientStock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == 971 && x.InventoryLocationId == 1);
        ingredientStock.QuantityBase = 1m;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessException>(() =>
            PreparationService(context).CompletePreparationBatchAsync(PreparationDto(recipeId, 10m)));

        Assert.Equal(1m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == 971 && x.InventoryLocationId == 1)).QuantityBase);
        Assert.False(await context.InventoryStocks.AnyAsync(x => x.InventoryItemId == 973 && x.InventoryLocationId == 1));
        Assert.False(await context.PreparationBatches.AnyAsync(x => x.PreparationRecipeId == recipeId));
        Assert.False(await context.InventoryMovements.AnyAsync(x => x.ReferenceType == nameof(PreparationBatch)));
    }

    [Fact]
    public async Task Weighted_average_cost_uses_base_unit_costs()
    {
        await using var context = CreateContext();
        var flour = new InventoryItem { Id = 604, BranchId = 1, Name = "Weighted Flour", BaseUnit = "Gram", PurchaseUnitName = "Kg", DefaultConversionFactorToBase = 1000m, ReorderLevel = 1000 };
        context.InventoryItems.Add(flour);
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = flour, InventoryLocationId = 1, QuantityBase = 1000m, AverageUnitCostBase = 0.1m });
        await context.SaveChangesAsync();

        await PurchaseService(context).CreatePurchaseAsync(new CreatePurchaseDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            BranchId = 1,
            UserSessionId = 2,
            PerformedByUserId = StockManagerId,
            TerminalId = 3,
            TerminalCode = "STOCK-01",
            SupplierId = 1,
            Items = { new PurchaseItemDto { InventoryItemId = flour.Id, PurchaseQuantity = 1m, PurchaseUnitName = "Kg", ConversionFactorToBase = 1000m, UnitCostPerPurchaseUnit = 300m } }
        });

        var stockRoom = await StockRoomLocationAsync(context);
        var stock = await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == flour.Id && x.InventoryLocationId == stockRoom.Id);
        Assert.Equal(2000m, stock.QuantityBase);
        Assert.Equal(0.2m, stock.AverageUnitCostBase);
    }

    [Fact]
    public async Task Insufficient_kitchen_base_quantity_blocks_order_finalization()
    {
        await using var context = CreateContext();
        var flour = new InventoryItem { Id = 605, BranchId = 1, Name = "Short Flour", BaseUnit = "Gram", ReorderLevel = 1000 };
        context.InventoryItems.Add(flour);
        context.Products.Add(ProductWithRecipe(605, "Blocked Pizza", flour, 100m, price: 500m));
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = flour, InventoryLocationId = 2, QuantityBase = 99m, AverageUnitCostBase = 0.2m });
        await context.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            OrderService(context).FinalizeOrderAsync(OrderDto(605, customerPhone: "6050")));

        Assert.Empty(await context.InventoryMovements.Where(x => x.InventoryItemId == flour.Id && x.MovementType == InventoryMovementType.Consumption).ToListAsync());
    }

    [Fact]
    public async Task Dispatch_fails_when_stock_room_is_insufficient_and_does_not_move_stock()
    {
        await using var context = CreateContext();
        var item = new InventoryItem { Id = 501, BranchId = 1, Name = "Chicken", BaseUnit = "Gram", ReorderLevel = 1m };
        context.InventoryItems.Add(item);
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = 1, Quantity = 1m, AverageUnitCost = 30m });
        var request = new KitchenRequest
        {
            BranchId = 1,
            RequestNumber = $"KR-TEST-{Guid.NewGuid():N}"[..24],
            Status = KitchenRequestStatus.Approved,
            RequestedByUserId = StockManagerId,
            ApprovedByUserId = StockManagerId,
            ApprovedAt = DateTime.UtcNow,
            Details = { new KitchenRequestDetail { InventoryItem = item, RequestedQuantity = 2m, ApprovedQuantity = 2m } }
        };
        context.KitchenRequests.Add(request);
        await context.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            new RestaurantInventoryService(context, new TestBranchContext(1), new InventoryTransactionService(context), new TestIdempotencyService()).DispatchKitchenRequestAsync(request.Id, StockManagerId));

        Assert.Equal(KitchenRequestStatus.Approved, (await context.KitchenRequests.SingleAsync(x => x.Id == request.Id)).Status);
        Assert.Equal(1m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == 1)).QuantityBase);
        Assert.Empty(await context.InventoryMovements.Where(x => x.MovementType == InventoryMovementType.StockRoomToKitchenDispatch).ToListAsync());
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext(options);
    }

    private async Task SeedCoreAsync(AppDbContext context)
    {
        context.Branches.Add(new Branch { Id = 2, BranchCode = "B2", Name = "Branch Two" });
        context.Terminals.AddRange(
            new Terminal { Id = 2, BranchId = 2, TerminalCode = "B2-01", Name = "Branch Two Terminal" },
            new Terminal { Id = 3, BranchId = 1, TerminalCode = "STOCK-01", Name = "Stock Terminal" },
            new Terminal { Id = 4, BranchId = 1, TerminalCode = "INACTIVE-01", Name = "Inactive Terminal", IsActive = false });
        context.Users.AddRange(
            new ApplicationUser { Id = CashierId, UserName = "cashier", NormalizedUserName = "CASHIER", BranchId = 1, FullName = "Cashier One" },
            new ApplicationUser { Id = StockManagerId, UserName = "stock", NormalizedUserName = "STOCK", BranchId = 1, FullName = "Stock Manager" },
            new ApplicationUser { Id = BranchTwoCashierId, UserName = "cashier2", NormalizedUserName = "CASHIER2", BranchId = 2, FullName = "Cashier Two" });
        context.Suppliers.Add(new Supplier { Id = 1, Name = "Default Supplier" });
        context.UserSessions.AddRange(
            new UserSession { Id = 1, SessionCode = "SES-CASHIER", UserId = CashierId, BranchId = 1, RoleName = "Cashier", TerminalId = 1, TerminalCode = "MAIN-01", TerminalName = "Main Terminal" },
            new UserSession { Id = 2, SessionCode = "SES-STOCK", UserId = StockManagerId, BranchId = 1, RoleName = "StockManager", TerminalId = 3, TerminalCode = "STOCK-01", TerminalName = "Stock Terminal" },
            new UserSession { Id = 3, SessionCode = "SES-B2", UserId = BranchTwoCashierId, BranchId = 2, RoleName = "Cashier", TerminalId = 2, TerminalCode = "B2-01", TerminalName = "Branch Two Terminal" });
        await EnsureBranchLocationsAsync(context, 2);
        await context.SaveChangesAsync();
    }

    private async Task<(int ProductId, int InventoryItemId)> CreateProductWithKitchenStockAsync(
        AppDbContext context,
        string productName,
        decimal recipeQuantity,
        decimal stockQuantity,
        decimal price = 10m)
    {
        var id = Math.Abs(productName.GetHashCode() % 10000) + 1000;
        var item = new InventoryItem { Id = id, BranchId = 1, Name = $"{productName} Inventory Item", BaseUnit = "Piece", ReorderLevel = 1 };
        context.InventoryItems.Add(item);
        context.Products.Add(ProductWithRecipe(id, productName, item, recipeQuantity, price));
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = 2, Quantity = stockQuantity, AverageUnitCost = 10m });
        await context.SaveChangesAsync();
        return (id, item.Id);
    }

    private async Task<int> SeedSimplePreparationRecipeAsync(
        AppDbContext context,
        int baseId,
        string outputBaseUnit,
        decimal outputQuantity,
        bool preparedOutput = true)
    {
        var ingredient = new InventoryItem
        {
            Id = baseId,
            BranchId = 1,
            Name = $"Prep Ingredient {baseId}",
            BaseUnit = "Gram",
            PurchaseUnitName = "Kg",
            DefaultConversionFactorToBase = 1000m,
            ReorderLevel = 100m
        };
        var output = new InventoryItem
        {
            Id = baseId + 2,
            BranchId = 1,
            Name = $"Prep Output {baseId}",
            BaseUnit = outputBaseUnit,
            PurchaseUnitName = outputBaseUnit,
            DefaultConversionFactorToBase = 1m,
            ReorderLevel = 1m,
            IsPreparedItem = preparedOutput
        };
        var recipe = new PreparationRecipe
        {
            BranchId = 1,
            Name = $"Prep Recipe {baseId}",
            OutputInventoryItem = output,
            OutputQuantityBase = outputQuantity,
            Ingredients =
            {
                new PreparationRecipeIngredient { InventoryItem = ingredient, QuantityBase = outputQuantity * 2m }
            }
        };

        context.InventoryItems.AddRange(ingredient, output);
        context.InventoryStocks.Add(new InventoryStock
        {
            BranchId = 1,
            InventoryItem = ingredient,
            InventoryLocationId = 1,
            QuantityBase = 1000m,
            AverageUnitCostBase = 0.5m
        });
        context.PreparationRecipes.Add(recipe);
        await context.SaveChangesAsync();
        return recipe.Id;
    }

    private static Product ProductWithRecipe(int productId, string name, InventoryItem item, decimal quantityRequired, decimal price, int branchId = 1) =>
        new()
        {
            Id = productId,
            BranchId = branchId,
            CategoryId = 1,
            Name = name,
            Price = price,
            Recipes =
            {
                new Recipe
                {
                    BranchId = branchId,
                    Ingredients = { new RecipeIngredient { InventoryItem = item, QuantityRequiredBase = quantityRequired, DisplayQuantity = quantityRequired, DisplayUnit = item.BaseUnit } }
                }
            }
        };

    private static async Task EnsureBranchLocationsAsync(AppDbContext context, int branchId)
    {
        if (!await context.InventoryLocations.AnyAsync(x => x.BranchId == branchId && x.Name == "Stock Room"))
        {
            context.InventoryLocations.Add(new InventoryLocation { Id = branchId == 2 ? 3 : 0, BranchId = branchId, Name = "Stock Room" });
        }

        if (!await context.InventoryLocations.AnyAsync(x => x.BranchId == branchId && x.Name == "Kitchen"))
        {
            context.InventoryLocations.Add(new InventoryLocation { Id = branchId == 2 ? 4 : 0, BranchId = branchId, Name = "Kitchen" });
        }
    }

    private static Task<InventoryLocation> KitchenLocationAsync(AppDbContext context) =>
        context.InventoryLocations.SingleAsync(x => x.BranchId == 1 && x.Name == "Kitchen");

    private static Task<InventoryLocation> StockRoomLocationAsync(AppDbContext context) =>
        context.InventoryLocations.SingleAsync(x => x.BranchId == 1 && x.Name == "Stock Room");

    private async Task<OpResult> FinalizeInNewContextAsync(CreateOrderDto dto)
    {
        try
        {
            await using var context = CreateContext();
            await OrderService(context).FinalizeOrderAsync(dto);
            return OpResult.Success();
        }
        catch (Exception ex)
        {
            return OpResult.Failure(ex.Message);
        }
    }

    private OrderService OrderService(AppDbContext context, int branchId = 1) =>
        new(
            context,
            new CustomerService(context, new TestBranchContext(branchId)),
            UserSessionService(context),
            new TestBranchContext(branchId),
            new TestIdempotencyService(),
            new InventoryTransactionService(context));

    private UserSessionService UserSessionService(AppDbContext context) =>
        new(
            context,
            new AllowAllBranchService(context),
            new StaticTerminalContextService(context, "MAIN-01"),
            new TestSessionCodeGenerator(),
            new TestAuditLogService(),
            new TestIdempotencyService(),
            Options.Create(new PosOperationalOptions()),
            new MemoryCache(Options.Create(new MemoryCacheOptions())));

    private PurchaseService PurchaseService(AppDbContext context, int branchId = 1) =>
        new(context, new TestBranchContext(branchId), new TestIdempotencyService(), new InventoryTransactionService(context));

    private PreparationService PreparationService(AppDbContext context, int branchId = 1, IIdempotencyService? idempotencyService = null) =>
        new(context, new TestBranchContext(branchId), idempotencyService ?? new TestIdempotencyService(), new InventoryTransactionService(context));

    private InventoryAdjustmentService InventoryAdjustmentService(AppDbContext context) =>
        new(
            context,
            new InventoryTransactionService(context),
            Options.Create(new PosOperationalOptions
            {
                InventoryAdjustmentAutoApprovalCostThreshold = 10m,
                InventoryAdjustmentAutoApprovalQuantityThresholdBase = 10m
            }));

    private ProductsController ProductController(AppDbContext context, int branchId = 1) =>
        new(
            context,
            new ProductService(context, new TestBranchContext(branchId), new PosMenuCacheInvalidator()),
            new TestBranchContext(branchId),
            Options.Create(new SecurityRateLimitOptions()));

    private static CreateOrderDto OrderDto(
        int productId,
        int quantity = 1,
        int? draftId = null,
        string cashierId = CashierId,
        int userSessionId = 1,
        int terminalId = 1,
        string terminalCode = "MAIN-01",
        string orderType = "Takeaway",
        string customerPhone = "2000",
        string address = "Street 1",
        decimal discount = 0m) =>
        new()
        {
            DraftOrderId = draftId,
            CashierId = cashierId,
            BranchId = 1,
            UserSessionId = userSessionId,
            TerminalId = terminalId,
            TerminalCode = terminalCode,
            TerminalName = "Main Terminal",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            OrderType = orderType,
            DiscountAmount = discount,
            Customer = new CustomerDto { BranchId = 1, Name = "Walk In", PhoneNumber = TestPhone(customerPhone), Address = address },
            Items = { new OrderItemDto { ProductId = productId, Quantity = quantity } }
        };

    private static DraftOrderDto DraftDto(
        int productId,
        string cashierId = CashierId,
        int userSessionId = 1,
        int terminalId = 1,
        string terminalCode = "MAIN-01",
        string customerPhone = "2010") =>
        new()
        {
            CashierId = cashierId,
            BranchId = 1,
            UserSessionId = userSessionId,
            TerminalId = terminalId,
            TerminalCode = terminalCode,
            TerminalName = "Main Terminal",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Customer = new CustomerDto { BranchId = 1, Name = "Draft Customer", PhoneNumber = TestPhone(customerPhone), Address = "Street 1" },
            Items = { new OrderItemDto { ProductId = productId, Quantity = 1 } }
        };

    private static CreatePurchaseDto PurchaseDto(int supplierId, params PurchaseItemDto[] items) =>
        new()
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            BranchId = 1,
            UserSessionId = 2,
            PerformedByUserId = StockManagerId,
            TerminalId = 3,
            TerminalCode = "STOCK-01",
            SupplierId = supplierId,
            Items = items.ToList()
        };

    private static CompletePreparationBatchDto PreparationDto(int recipeId, decimal? outputQuantityBase = null, string? idempotencyKey = null) =>
        new()
        {
            IdempotencyKey = idempotencyKey ?? Guid.NewGuid().ToString("N"),
            UserSessionId = 2,
            CreatedByUserId = StockManagerId,
            TerminalId = 3,
            TerminalCode = "STOCK-01",
            PreparationRecipeId = recipeId,
            OutputQuantityBase = outputQuantityBase
        };

    private static string TestPhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 11 ? digits : $"03{digits.PadLeft(9, '0')[..9]}";
    }

    private static Order ExistingOrder(int branchId, string orderNumber, string cashierId, int sessionId, int terminalId, string terminalCode) =>
        new()
        {
            BranchId = branchId,
            OrderNumber = orderNumber,
            CashierId = cashierId,
            UserSessionId = sessionId,
            TerminalId = terminalId,
            TerminalCode = terminalCode,
            OrderStatus = OrderStatus.Completed,
            Subtotal = 1,
            TotalAmount = 1,
            CompletedAt = DateTime.UtcNow
        };

    private sealed record OpResult(bool Succeeded, string? Error)
    {
        public static OpResult Success() => new(true, null);

        public static OpResult Failure(string error) => new(false, error);
    }

    private sealed class TestBranchContext : IBranchContextService
    {
        private readonly int _branchId;

        public TestBranchContext(int branchId) => _branchId = branchId;

        public Task<int> GetCurrentBranchIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_branchId);

        public Task EnsureUserCanAccessBranchAsync(int branchId, CancellationToken cancellationToken = default) =>
            branchId == _branchId ? Task.CompletedTask : throw new UnauthorizedAccessException("Wrong branch.");
    }

    private sealed class AllowAllBranchService : IBranchService
    {
        private readonly AppDbContext _context;

        public AllowAllBranchService(AppDbContext context) => _context = context;

        public Task<List<Branch>> GetBranchesForUserAsync(string userId, CancellationToken cancellationToken = default) =>
            _context.Branches.Where(x => x.IsActive).ToListAsync(cancellationToken);

        public Task EnsureBranchAccessAsync(string userId, int branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StaticTerminalContextService : ITerminalContextService
    {
        private readonly AppDbContext _context;
        private readonly string _terminalCode;

        public StaticTerminalContextService(AppDbContext context, string terminalCode)
        {
            _context = context;
            _terminalCode = terminalCode;
        }

        public string? GetTerminalCodeFromRequest() => _terminalCode;

        public Task<Terminal?> GetCurrentTerminalAsync(CancellationToken cancellationToken = default) =>
            _context.Terminals.FirstOrDefaultAsync(x => x.TerminalCode == _terminalCode && x.IsActive, cancellationToken);

        public Task<Terminal?> GetCurrentTerminalFreshAsync(CancellationToken cancellationToken = default) =>
            GetCurrentTerminalAsync(cancellationToken);

        public async Task<Terminal> RequireCurrentTerminalAsync(CancellationToken cancellationToken = default) =>
            await GetCurrentTerminalAsync(cancellationToken) ?? throw new InvalidOperationException("Terminal is not registered or is inactive.");

        public async Task<Terminal> RequireCurrentTerminalFreshAsync(CancellationToken cancellationToken = default) =>
            await GetCurrentTerminalFreshAsync(cancellationToken) ?? throw new InvalidOperationException("Terminal is not registered or is inactive.");

        public async Task HeartbeatAsync(string? userId = null, int? sessionId = null, CancellationToken cancellationToken = default)
        {
            var terminal = await RequireCurrentTerminalAsync(cancellationToken);
            var heartbeat = await _context.TerminalHeartbeats.FirstOrDefaultAsync(x => x.TerminalId == terminal.Id, cancellationToken);
            if (heartbeat is null)
            {
                heartbeat = new TerminalHeartbeat { TerminalId = terminal.Id, TerminalCode = terminal.TerminalCode, BranchId = terminal.BranchId };
                _context.TerminalHeartbeats.Add(heartbeat);
            }

            heartbeat.LastSeenAt = DateTime.UtcNow;
            heartbeat.CurrentUserId = userId;
            heartbeat.CurrentSessionId = sessionId;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task IssueTerminalCookieAsync(Terminal terminal, string? rawToken = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StaticUserSessionService : IUserSessionService
    {
        private readonly AppDbContext _context;
        private readonly int _sessionId;

        public StaticUserSessionService(AppDbContext context, int sessionId)
        {
            _context = context;
            _sessionId = sessionId;
        }

        public Task<UserSession?> GetActiveSessionAsync(string userId, CancellationToken cancellationToken = default) =>
            _context.UserSessions.FirstOrDefaultAsync(x => x.Id == _sessionId && x.UserId == userId, cancellationToken);

        public Task<UserSession?> GetActiveSessionFreshAsync(string userId, CancellationToken cancellationToken = default) =>
            GetActiveSessionAsync(userId, cancellationToken);

        public Task<UserSession?> GetAbandonedSessionAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserSession?>(null);

        public Task<UserSession?> GetActiveSessionForTerminalAsync(int terminalId, CancellationToken cancellationToken = default) =>
            _context.UserSessions.FirstOrDefaultAsync(x => x.Id == _sessionId && x.TerminalId == terminalId, cancellationToken);

        public Task<UserSession> StartSessionAsync(StartSessionDto dto, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UserSession> ContinueSessionAsync(int sessionId, string userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SessionCloseViewModel> GetCloseSessionAsync(int sessionId, string userId, bool isManagerOrAdmin, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<List<PendingSessionCloseApprovalViewModel>> GetPendingCloseApprovalsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UserSession> ApprovePendingCloseAsync(int sessionId, string approvedByUserId, int terminalId, string terminalCode, string idempotencyKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UserSession> CloseSessionAsync(CloseSessionDto dto, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UserSession> ReopenSessionAsync(ReopenSessionDto dto, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task MarkAbandonedSessionsAsync(TimeSpan? staleAfter = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<SessionSummaryViewModel> GetSessionSummaryAsync(int sessionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task HeartbeatAsync(int sessionId, string terminalName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class TestSessionCodeGenerator : ISessionCodeGeneratorService
    {
        private int _value;

        public Task<string> GenerateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult($"SES-TEST-{Interlocked.Increment(ref _value):000000}-{Guid.NewGuid():N}");
    }

    private sealed class TestAuditLogService : IAuditLogService
    {
        public Task LogAsync(string action, string entityName, string? entityId, object? oldValues = null, object? newValues = null, int? branchId = null, int? terminalId = null, string? userId = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LogSecurityAsync(string eventType, string severity, string message, string? userId = null, string? attemptedUserName = null, int? branchId = null, int? terminalId = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestErrorLoggingService : IErrorLoggingService
    {
        public void LogException(HttpContext httpContext, Exception exception, string? userMessage = null)
        {
        }
    }

    private sealed class TestIdempotencyService : IIdempotencyService
    {
        private readonly Dictionary<string, IdempotencyRecord> _records = new();

        public string GetOrCreateKey() => Guid.NewGuid().ToString("N");

        public string HashPayload(object payload) => JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        public Task<IdempotencyStartResult> BeginAsync(string operationType, string idempotencyKey, string requestHash, string? userId, int? branchId, int? terminalId, CancellationToken cancellationToken = default)
        {
            idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey;
            if (_records.TryGetValue(idempotencyKey, out var existing))
            {
                if (existing.OperationType != operationType || existing.RequestHash != requestHash)
                {
                    return Task.FromResult(new IdempotencyStartResult(false, existing, "This request key was already used for a different operation. Please refresh and try again."));
                }

                return Task.FromResult(new IdempotencyStartResult(false, existing,
                    existing.Status == IdempotencyStatus.InProgress ? "This request is already being processed. Please wait." : null));
            }

            var record = new IdempotencyRecord
            {
                OperationType = operationType,
                IdempotencyKey = idempotencyKey,
                RequestHash = requestHash,
                UserId = userId,
                BranchId = branchId,
                TerminalId = terminalId,
                Status = IdempotencyStatus.InProgress,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            _records.Add(idempotencyKey, record);
            return Task.FromResult(new IdempotencyStartResult(true, record, null));
        }

        public Task CompleteAsync(IdempotencyRecord record, string resourceType, int resourceId, int responseCode, string responseBodySummary, CancellationToken cancellationToken = default)
        {
            record.Status = IdempotencyStatus.Completed;
            record.ResourceType = resourceType;
            record.ResourceId = resourceId;
            record.ResponseCode = responseCode;
            record.ResponseBodySummary = responseBodySummary;
            return Task.CompletedTask;
        }

        public Task FailAsync(IdempotencyRecord record, string message, CancellationToken cancellationToken = default)
        {
            record.Status = IdempotencyStatus.Failed;
            return Task.CompletedTask;
        }
    }
}
