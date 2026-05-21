using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

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
        Assert.Equal(0m, stock.Quantity);
        Assert.Equal(1, await verify.InventoryMovements.CountAsync(x =>
            x.InventoryItemId == itemId &&
            x.MovementType == InventoryMovementType.Consumption &&
            x.Quantity == 1m));
        Assert.DoesNotContain(await verify.InventoryStocks.ToListAsync(), x => x.Quantity < 0);
    }

    [Fact]
    public async Task Simultaneous_orders_for_products_sharing_inventory_item_keep_stock_and_movements_correct()
    {
        await using var setup = CreateContext();
        var item = new InventoryItem { Id = 201, BranchId = 1, Name = "Dough", Unit = "kg", ReorderLevel = 1 };
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
        Assert.Equal(0m, (await verify.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).Quantity);
        Assert.Equal(2m, await verify.InventoryMovements
            .Where(x => x.InventoryItemId == item.Id && x.MovementType == InventoryMovementType.Consumption)
            .SumAsync(x => x.Quantity));
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
        Assert.Equal(1m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == itemId && x.InventoryLocationId == kitchen.Id)).Quantity);
    }

    [Fact]
    public async Task Draft_completed_cancelled_and_receipt_rules_are_enforced_without_deducting_until_finalize()
    {
        await using var context = CreateContext();
        var (productId, itemId) = await CreateProductWithKitchenStockAsync(context, "Sandwich", 1m, 2m);
        var kitchen = await KitchenLocationAsync(context);
        var service = OrderService(context);

        var draft = await service.CreateDraftOrderAsync(DraftDto(productId));
        Assert.Equal(2m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == itemId && x.InventoryLocationId == kitchen.Id)).Quantity);
        Assert.Empty(await context.InventoryMovements.Where(x => x.MovementType == InventoryMovementType.Consumption).ToListAsync());

        var completed = await service.FinalizeOrderAsync(OrderDto(productId, draftId: draft.OrderId));
        Assert.Equal(1m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == itemId && x.InventoryLocationId == kitchen.Id)).Quantity);
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
    public async Task Product_recipe_price_total_and_quantity_validation_are_service_enforced()
    {
        await using var context = CreateContext();
        var (productId, _) = await CreateProductWithKitchenStockAsync(context, "Cake", 1m, 10m, price: 25m);
        context.Products.Add(new Product { Id = 211, BranchId = 1, CategoryId = 1, Name = "No Recipe", Price = 999m });
        context.Products.Add(new Product { Id = 212, BranchId = 1, CategoryId = 1, Name = "Inactive", Price = 10m, IsActive = false });
        await context.SaveChangesAsync();

        var service = OrderService(context);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(211)));
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
        var branchTwoItem = new InventoryItem { Id = 301, BranchId = 2, Name = "Branch B Flour", Unit = "kg", ReorderLevel = 1 };
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
        var item = new InventoryItem { Id = 401, BranchId = 1, Name = "Mozzarella", Unit = "kg", ReorderLevel = 1m };
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
            Items = { new PurchaseItemDto { InventoryItemId = item.Id, Quantity = 10m, UnitCost = 20m } }
        });

        var stockRoom = await StockRoomLocationAsync(context);
        var kitchen = await KitchenLocationAsync(context);
        Assert.Equal(10m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == stockRoom.Id)).Quantity);
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

        await new RestaurantInventoryService(context, new TestBranchContext(1)).DispatchKitchenRequestAsync(request.Id, StockManagerId);
        Assert.Equal(6m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == stockRoom.Id)).Quantity);
        Assert.Equal(4m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).Quantity);
        Assert.Equal(80m, (await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == item.Id && x.MovementType == InventoryMovementType.Transfer)).TotalCost);

        await OrderService(context).FinalizeOrderAsync(OrderDto(product.Id, quantity: 2, customerPhone: "4010"));
        Assert.Equal(3m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == kitchen.Id)).Quantity);
        var consumption = await context.InventoryMovements.SingleAsync(x => x.InventoryItemId == item.Id && x.MovementType == InventoryMovementType.Consumption);
        Assert.Equal(20m, consumption.UnitCost);
        Assert.Equal(20m, consumption.TotalCost);

        context.OperationalExpenses.Add(new OperationalExpense
        {
            BranchId = 1,
            ExpenseCategoryId = 1,
            Amount = 30m,
            ExpenseDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var profit = await new RestaurantInventoryService(context, new TestBranchContext(1)).BuildProfitReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.Equal(200m, profit.SalesRevenue);
        Assert.Equal(20m, profit.IngredientCost);
        Assert.Equal(30m, profit.OperationalExpenses);
        Assert.Equal(150m, profit.NetProfit);
    }

    [Fact]
    public async Task Dispatch_fails_when_stock_room_is_insufficient_and_does_not_move_stock()
    {
        await using var context = CreateContext();
        var item = new InventoryItem { Id = 501, BranchId = 1, Name = "Chicken", Unit = "kg", ReorderLevel = 1m };
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
            new RestaurantInventoryService(context, new TestBranchContext(1)).DispatchKitchenRequestAsync(request.Id, StockManagerId));

        Assert.Equal(KitchenRequestStatus.Approved, (await context.KitchenRequests.SingleAsync(x => x.Id == request.Id)).Status);
        Assert.Equal(1m, (await context.InventoryStocks.SingleAsync(x => x.InventoryItemId == item.Id && x.InventoryLocationId == 1)).Quantity);
        Assert.Empty(await context.InventoryMovements.Where(x => x.MovementType == InventoryMovementType.Transfer).ToListAsync());
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
        var item = new InventoryItem { Id = id, BranchId = 1, Name = $"{productName} Inventory Item", Unit = "unit", ReorderLevel = 1 };
        context.InventoryItems.Add(item);
        context.Products.Add(ProductWithRecipe(id, productName, item, recipeQuantity, price));
        context.InventoryStocks.Add(new InventoryStock { BranchId = 1, InventoryItem = item, InventoryLocationId = 2, Quantity = stockQuantity, AverageUnitCost = 10m });
        await context.SaveChangesAsync();
        return (id, item.Id);
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
                    Ingredients = { new RecipeIngredient { InventoryItem = item, QuantityRequired = quantityRequired } }
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
            new TestIdempotencyService());

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
        new(context, new TestBranchContext(branchId), new TestIdempotencyService());

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

    private sealed class TestIdempotencyService : IIdempotencyService
    {
        public string GetOrCreateKey() => Guid.NewGuid().ToString("N");

        public string HashPayload(object payload) => Guid.NewGuid().ToString("N");

        public Task<IdempotencyStartResult> BeginAsync(string operationType, string idempotencyKey, string requestHash, string? userId, int? branchId, int? terminalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IdempotencyStartResult(true, new IdempotencyRecord
            {
                OperationType = operationType,
                IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey,
                RequestHash = requestHash,
                UserId = userId,
                BranchId = branchId,
                TerminalId = terminalId,
                Status = IdempotencyStatus.InProgress,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }, null));

        public Task CompleteAsync(IdempotencyRecord record, string resourceType, int resourceId, int responseCode, string responseBodySummary, CancellationToken cancellationToken = default)
        {
            record.Status = IdempotencyStatus.Completed;
            record.ResourceType = resourceType;
            record.ResourceId = resourceId;
            return Task.CompletedTask;
        }

        public Task FailAsync(IdempotencyRecord record, string message, CancellationToken cancellationToken = default)
        {
            record.Status = IdempotencyStatus.Failed;
            return Task.CompletedTask;
        }
    }
}
