using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.EntityFrameworkCore;

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
    public async Task Two_simultaneous_orders_for_one_available_ingredient_allow_exactly_one_sale()
    {
        await using var setup = CreateContext();
        var ingredientId = await CreateProductWithInventoryAsync(setup, "Burger", 1m, 1m);

        var results = await RunTwoConcurrentFinalizationsAsync(productId: 1);

        Assert.Single(results, x => x.Succeeded);
        Assert.Single(results, x => !x.Succeeded);

        await using var verify = CreateContext();
        var inventory = await verify.Inventories.SingleAsync(x => x.IngredientId == ingredientId);
        Assert.Equal(0m, inventory.CurrentQuantity);
        Assert.Equal(1, await verify.InventoryTransactions.CountAsync(x =>
            x.IngredientId == ingredientId &&
            x.TransactionType == InventoryTransactionType.Sale &&
            x.QuantityChanged == -1m));
        Assert.DoesNotContain(await verify.Inventories.ToListAsync(), x => x.CurrentQuantity < 0);
    }

    [Fact]
    public async Task Simultaneous_orders_for_products_sharing_ingredient_keep_stock_and_ledger_correct()
    {
        await using var setup = CreateContext();
        var ingredient = new Ingredient { BranchId = 1, Name = "Dough", UnitType = "kg" };
        setup.Ingredients.Add(ingredient);
        setup.Products.AddRange(
            new Product
            {
                Id = 1,
                BranchId = 1,
                CategoryId = 1,
                Name = "Pizza",
                Price = 100,
                ProductIngredients = { new ProductIngredient { Ingredient = ingredient, QuantityRequired = 1m } }
            },
            new Product
            {
                Id = 2,
                BranchId = 1,
                CategoryId = 1,
                Name = "Roll",
                Price = 50,
                ProductIngredients = { new ProductIngredient { Ingredient = ingredient, QuantityRequired = 1m } }
            });
        setup.Inventories.Add(new Inventory { BranchId = 1, Ingredient = ingredient, CurrentQuantity = 2m });
        await setup.SaveChangesAsync();

        var first = FinalizeInNewContextAsync(OrderDto(productId: 1, customerPhone: "3001"));
        var second = FinalizeInNewContextAsync(OrderDto(productId: 2, customerPhone: "3002"));
        var results = await Task.WhenAll(first, second);

        Assert.All(results, x => Assert.True(x.Succeeded, x.Error));
        await using var verify = CreateContext();
        var inventory = await verify.Inventories.SingleAsync(x => x.IngredientId == ingredient.Id);
        Assert.Equal(0m, inventory.CurrentQuantity);
        Assert.Equal(-2m, await verify.InventoryTransactions
            .Where(x => x.IngredientId == ingredient.Id && x.TransactionType == InventoryTransactionType.Sale)
            .SumAsync(x => x.QuantityChanged));
    }

    [Fact]
    public async Task Finalize_order_concurrent_with_stock_adjustment_never_corrupts_inventory_or_ledger()
    {
        await using var setup = CreateContext();
        var ingredientId = await CreateProductWithInventoryAsync(setup, "Tea", 1m, 1m);

        var sale = FinalizeInNewContextAsync(OrderDto(productId: 1, customerPhone: "3010"));
        var adjustment = Task.Run(async () =>
        {
            try
            {
                await using var context = CreateContext();
                await InventoryService(context).AdjustInventoryAsync(new InventoryAdjustmentDto
                {
                    BranchId = 1,
                    UserSessionId = 2,
                    PerformedByUserId = StockManagerId,
                    TerminalId = 1,
                    TerminalCode = "MAIN-01",
                    IngredientId = ingredientId,
                    QuantityChanged = -1m
                });
                return OpResult.Success();
            }
            catch (Exception ex)
            {
                return OpResult.Failure(ex.Message);
            }
        });

        var results = await Task.WhenAll(sale, adjustment);

        Assert.Single(results, x => x.Succeeded);
        Assert.Single(results, x => !x.Succeeded);
        await using var verify = CreateContext();
        var inventory = await verify.Inventories.SingleAsync(x => x.IngredientId == ingredientId);
        Assert.Equal(0m, inventory.CurrentQuantity);
        Assert.Equal(-1m, await verify.InventoryTransactions.Where(x => x.IngredientId == ingredientId).SumAsync(x => x.QuantityChanged));
    }

    [Fact]
    public async Task Failed_finalization_rolls_back_order_items_inventory_and_sale_transactions()
    {
        await using var setup = CreateContext();
        var ingredientId = await CreateProductWithInventoryAsync(setup, "Combo", 2m, 1m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrderService(setup).FinalizeOrderAsync(OrderDto(productId: 1, quantity: 1)));

        Assert.Empty(await setup.Orders.Where(x => x.OrderStatus == OrderStatus.Completed).ToListAsync());
        Assert.Empty(await setup.InventoryTransactions.Where(x => x.TransactionType == InventoryTransactionType.Sale).ToListAsync());
        Assert.Equal(1m, (await setup.Inventories.SingleAsync(x => x.IngredientId == ingredientId)).CurrentQuantity);
    }

    [Fact]
    public async Task Draft_completed_cancelled_and_receipt_rules_are_enforced()
    {
        await using var context = CreateContext();
        var ingredientId = await CreateProductWithInventoryAsync(context, "Sandwich", 1m, 2m);
        var service = OrderService(context);

        var draft = await service.CreateDraftOrderAsync(DraftDto(productId: 1));
        Assert.Equal(2m, (await context.Inventories.SingleAsync(x => x.IngredientId == ingredientId)).CurrentQuantity);
        Assert.Empty(await context.InventoryTransactions.ToListAsync());

        var completed = await service.FinalizeOrderAsync(OrderDto(productId: 1, draftId: draft.OrderId));
        Assert.Equal(1m, (await context.Inventories.SingleAsync(x => x.IngredientId == ingredientId)).CurrentQuantity);
        Assert.Equal(1, await context.InventoryTransactions.CountAsync(x => x.TransactionType == InventoryTransactionType.Sale));
        Assert.NotNull(await service.GetReceiptAsync(completed.OrderId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FinalizeOrderAsync(OrderDto(productId: 1, draftId: completed.OrderId, customerPhone: "3020")));

        var cancelledDraft = await service.CreateDraftOrderAsync(DraftDto(productId: 1, customerPhone: "3021"));
        await service.CancelDraftOrderAsync(cancelledDraft.OrderId, CashierId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FinalizeOrderAsync(OrderDto(productId: 1, draftId: cancelledDraft.OrderId, customerPhone: "3022")));

        Assert.Equal(1, await context.Orders.CountAsync(x => x.OrderStatus == OrderStatus.Completed));
        Assert.Equal(1, await context.InventoryTransactions.CountAsync(x => x.TransactionType == InventoryTransactionType.Sale));
    }

    [Fact]
    public async Task Product_recipe_price_total_and_quantity_validation_are_service_enforced()
    {
        await using var context = CreateContext();
        await CreateProductWithInventoryAsync(context, "Cake", 1m, 10m, price: 25m);
        context.Products.Add(new Product { Id = 2, BranchId = 1, CategoryId = 1, Name = "No Recipe", Price = 999m });
        context.Products.Add(new Product { Id = 3, BranchId = 1, CategoryId = 1, Name = "Inactive", Price = 10m, IsActive = false });
        await context.SaveChangesAsync();

        var service = OrderService(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId: 2)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId: 3)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId: 999)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId: 1, quantity: 0)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId: 1, quantity: -1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId: 1, quantity: 10001)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId: 0)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeOrderAsync(OrderDto(productId: 1, orderType: "Delivery", customerPhone: "3030", address: "")));

        var result = await service.FinalizeOrderAsync(OrderDto(productId: 1, quantity: 2, discount: 5m, customerPhone: "3031"));
        Assert.Equal(50m, result.Subtotal);
        Assert.Equal(45m, result.TotalAmount);
    }

    [Fact]
    public async Task Session_rules_prevent_missing_duplicate_and_wrong_role_workflows()
    {
        await using var context = CreateContext();
        await CreateProductWithInventoryAsync(context, "Juice", 1m, 5m);
        var sessionService = UserSessionService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrderService(context).FinalizeOrderAsync(OrderDto(productId: 1, cashierId: "no-session")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InventoryService(context).AdjustInventoryAsync(new InventoryAdjustmentDto
            {
                BranchId = 1,
                UserSessionId = 1,
                PerformedByUserId = CashierId,
                TerminalId = 1,
                TerminalCode = "MAIN-01",
                IngredientId = 1,
                QuantityChanged = 1m
            }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrderService(context).FinalizeOrderAsync(OrderDto(productId: 1, cashierId: StockManagerId, userSessionId: 2, customerPhone: "3040")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sessionService.StartSessionAsync(new StartSessionDto
        {
            UserId = CashierId,
            BranchId = 1,
            RoleName = "Cashier",
            TerminalId = 1,
            TerminalCode = "MAIN-01",
            TerminalName = "Duplicate"
        }));

        var draft = await OrderService(context).CreateDraftOrderAsync(DraftDto(productId: 1, customerPhone: "3041"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sessionService.EndSessionAsync(1, CashierId));
        await OrderService(context).CancelDraftOrderAsync(draft.OrderId, CashierId);
        await sessionService.EndSessionAsync(1, CashierId);

        var interrupted = context.UserSessions.Single(x => x.Id == 1);
        interrupted.Status = SessionStatus.Interrupted;
        interrupted.EndedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        var resumed = await sessionService.ContinueSessionAsync(1, CashierId);
        Assert.Equal(SessionStatus.Active, resumed.Status);
    }

    [Fact]
    public async Task Branch_scoping_blocks_cross_branch_products_orders_and_preserves_scoped_uniqueness()
    {
        await using var context = CreateContext();
        await CreateProductWithInventoryAsync(context, "Branch A Product", 1m, 3m);
        var branchTwoIngredient = new Ingredient { Id = 2, BranchId = 2, Name = "Branch B Flour", UnitType = "kg" };
        context.Ingredients.Add(branchTwoIngredient);
        context.Products.Add(new Product
        {
            Id = 2,
            BranchId = 2,
            CategoryId = 1,
            Name = "Branch B Product",
            Price = 20m,
            ProductIngredients = { new ProductIngredient { Ingredient = branchTwoIngredient, QuantityRequired = 1m } }
        });
        context.Inventories.Add(new Inventory { BranchId = 2, Ingredient = branchTwoIngredient, CurrentQuantity = 3m });
        context.Customers.AddRange(
            new Customer { BranchId = 1, Name = "A", PhoneNumber = "555" },
            new Customer { BranchId = 2, Name = "B", PhoneNumber = "555" });
        context.Orders.AddRange(
            ExistingOrder(branchId: 1, orderNumber: "POS-SHARED", cashierId: CashierId, sessionId: 1, terminalId: 1, terminalCode: "MAIN-01"),
            ExistingOrder(branchId: 2, orderNumber: "POS-SHARED", cashierId: BranchTwoCashierId, sessionId: 3, terminalId: 2, terminalCode: "B2-01"));
        await context.SaveChangesAsync();

        var branchAService = OrderService(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => branchAService.FinalizeOrderAsync(OrderDto(productId: 2, customerPhone: "3050")));

        var branchBDraft = await OrderService(context, branchId: 2).CreateDraftOrderAsync(DraftDto(productId: 2, cashierId: BranchTwoCashierId, userSessionId: 3, terminalId: 2, terminalCode: "B2-01", customerPhone: "3051"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            branchAService.FinalizeOrderAsync(OrderDto(productId: 2, draftId: branchBDraft.OrderId, customerPhone: "3052")));

        Assert.Equal(2, await context.Customers.CountAsync(x => x.PhoneNumber == "555"));
        Assert.Equal(2, await context.Orders.CountAsync(x => x.OrderNumber == "POS-SHARED"));
    }

    [Fact]
    public async Task Terminal_identity_and_heartbeat_rules_are_enforced()
    {
        await using var context = CreateContext();
        await CreateProductWithInventoryAsync(context, "Coffee", 1m, 3m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrderService(context).FinalizeOrderAsync(OrderDto(productId: 1, terminalId: 0, terminalCode: "")));

        context.UserSessions.Single(x => x.Id == 1).TerminalId = 3;
        context.UserSessions.Single(x => x.Id == 1).TerminalCode = "INACTIVE-01";
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrderService(context).FinalizeOrderAsync(OrderDto(productId: 1, terminalId: 3, terminalCode: "INACTIVE-01")));

        context.UserSessions.Single(x => x.Id == 1).TerminalId = 1;
        context.UserSessions.Single(x => x.Id == 1).TerminalCode = "MAIN-01";
        await context.SaveChangesAsync();
        var order = await OrderService(context).FinalizeOrderAsync(OrderDto(productId: 1, customerPhone: "3060"));
        var stored = await context.Orders.SingleAsync(x => x.Id == order.OrderId);
        Assert.Equal(1, stored.TerminalId);
        Assert.Equal("MAIN-01", stored.TerminalCode);

        var terminalService = new StaticTerminalContextService(context, "MAIN-01");
        await terminalService.HeartbeatAsync(CashierId, 1);
        var heartbeat = await context.TerminalHeartbeats.SingleAsync(x => x.TerminalId == 1);
        Assert.Equal(CashierId, heartbeat.CurrentUserId);
        Assert.Equal(1, heartbeat.CurrentSessionId);
        Assert.True(heartbeat.LastSeenAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Inventory_ledger_matches_current_quantity_and_failed_operations_do_not_log_transactions()
    {
        await using var context = CreateContext();
        var ingredientId = await CreateProductWithInventoryAsync(context, "Noodles", 1m, 5m);
        var purchaseService = PurchaseService(context);
        var orderService = OrderService(context);

        await purchaseService.CreatePurchaseAsync(new CreatePurchaseDto
        {
            BranchId = 1,
            UserSessionId = 2,
            PerformedByUserId = StockManagerId,
            TerminalId = 1,
            TerminalCode = "MAIN-01",
            SupplierId = 1,
            Items = { new PurchaseItemDto { IngredientId = ingredientId, Quantity = 2m, UnitCost = 10m } }
        });
        await orderService.FinalizeOrderAsync(OrderDto(productId: 1, quantity: 2));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            purchaseService.CreatePurchaseAsync(new CreatePurchaseDto
            {
                BranchId = 1,
                UserSessionId = 2,
                PerformedByUserId = StockManagerId,
                TerminalId = 1,
                TerminalCode = "MAIN-01",
                SupplierId = 1,
                Items = { new PurchaseItemDto { IngredientId = ingredientId, Quantity = 0m, UnitCost = 10m } }
            }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => orderService.FinalizeOrderAsync(OrderDto(productId: 1, quantity: 10001, customerPhone: "3070")));

        var inventory = await context.Inventories.SingleAsync(x => x.IngredientId == ingredientId);
        var ledgerSum = await context.InventoryTransactions.Where(x => x.IngredientId == ingredientId).SumAsync(x => x.QuantityChanged);
        Assert.Equal(5m + ledgerSum, inventory.CurrentQuantity);
        Assert.Equal(1, await context.InventoryTransactions.CountAsync(x => x.TransactionType == InventoryTransactionType.Purchase && x.QuantityChanged > 0));
        Assert.Equal(1, await context.InventoryTransactions.CountAsync(x => x.TransactionType == InventoryTransactionType.Sale && x.QuantityChanged < 0));
        Assert.Equal(2, await context.InventoryTransactions.CountAsync());
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
            new Terminal { Id = 3, BranchId = 1, TerminalCode = "INACTIVE-01", Name = "Inactive Terminal", IsActive = false });
        context.Users.AddRange(
            new ApplicationUser { Id = CashierId, UserName = "cashier", NormalizedUserName = "CASHIER", BranchId = 1, FullName = "Cashier One" },
            new ApplicationUser { Id = StockManagerId, UserName = "stock", NormalizedUserName = "STOCK", BranchId = 1, FullName = "Stock Manager" },
            new ApplicationUser { Id = BranchTwoCashierId, UserName = "cashier2", NormalizedUserName = "CASHIER2", BranchId = 2, FullName = "Cashier Two" });
        context.Suppliers.Add(new Supplier { Id = 1, Name = "Default Supplier" });
        context.UserSessions.AddRange(
            new UserSession { Id = 1, SessionCode = "SES-CASHIER", UserId = CashierId, BranchId = 1, RoleName = "Cashier", TerminalId = 1, TerminalCode = "MAIN-01", TerminalName = "Main Terminal" },
            new UserSession { Id = 2, SessionCode = "SES-STOCK", UserId = StockManagerId, BranchId = 1, RoleName = "StockManager", TerminalId = 1, TerminalCode = "MAIN-01", TerminalName = "Main Terminal" },
            new UserSession { Id = 3, SessionCode = "SES-B2", UserId = BranchTwoCashierId, BranchId = 2, RoleName = "Cashier", TerminalId = 2, TerminalCode = "B2-01", TerminalName = "Branch Two Terminal" });
        await context.SaveChangesAsync();
    }

    private async Task<int> CreateProductWithInventoryAsync(AppDbContext context, string productName, decimal recipeQuantity, decimal stockQuantity, decimal price = 10m)
    {
        var ingredient = new Ingredient { Id = 1, BranchId = 1, Name = $"{productName} Ingredient", UnitType = "unit" };
        context.Ingredients.Add(ingredient);
        context.Products.Add(new Product
        {
            Id = 1,
            BranchId = 1,
            CategoryId = 1,
            Name = productName,
            Price = price,
            ProductIngredients = { new ProductIngredient { Ingredient = ingredient, QuantityRequired = recipeQuantity } }
        });
        context.Inventories.Add(new Inventory { BranchId = 1, Ingredient = ingredient, CurrentQuantity = stockQuantity });
        await context.SaveChangesAsync();
        return ingredient.Id;
    }

    private async Task<OpResult[]> RunTwoConcurrentFinalizationsAsync(int productId)
    {
        var first = FinalizeInNewContextAsync(OrderDto(productId, customerPhone: "2001"));
        var second = FinalizeInNewContextAsync(OrderDto(productId, customerPhone: "2002"));
        return await Task.WhenAll(first, second);
    }

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
            new TestBranchContext(branchId));

    private UserSessionService UserSessionService(AppDbContext context) =>
        new(context, new AllowAllBranchService(context));

    private InventoryService InventoryService(AppDbContext context, int branchId = 1) =>
        new(context, new TestBranchContext(branchId));

    private PurchaseService PurchaseService(AppDbContext context, int branchId = 1) =>
        new(context, new TestBranchContext(branchId));

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
            OrderType = orderType,
            DiscountAmount = discount,
            Customer = new CustomerDto { BranchId = 1, Name = "Walk In", PhoneNumber = customerPhone, Address = address },
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
            Customer = new CustomerDto { BranchId = 1, Name = "Draft Customer", PhoneNumber = customerPhone, Address = "Street 1" },
            Items = { new OrderItemDto { ProductId = productId, Quantity = 1 } }
        };

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

        public async Task<Terminal> RequireCurrentTerminalAsync(CancellationToken cancellationToken = default) =>
            await GetCurrentTerminalAsync(cancellationToken) ?? throw new InvalidOperationException("Terminal is not registered or is inactive.");

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
    }
}
