using Microsoft.EntityFrameworkCore;
using BranchPOS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace BranchPOS.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<InventoryLocation> InventoryLocations => Set<InventoryLocation>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<KitchenRequest> KitchenRequests => Set<KitchenRequest>();
    public DbSet<KitchenRequestDetail> KitchenRequestDetails => Set<KitchenRequestDetail>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<OperationalExpense> OperationalExpenses => Set<OperationalExpense>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserSessionHeartbeat> UserSessionHeartbeats => Set<UserSessionHeartbeat>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<TerminalHeartbeat> TerminalHeartbeats => Set<TerminalHeartbeat>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UX_Categories_Name");
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
        });

        builder.Entity<Branch>(entity =>
        {
            entity.HasIndex(x => x.BranchCode).IsUnique().HasDatabaseName("UX_Branches_BranchCode");
            entity.Property(x => x.BranchCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.Phone).HasMaxLength(40);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => new { x.BranchId, x.IsActive }).HasDatabaseName("IX_AspNetUsers_BranchId_IsActive");
            entity.Property(x => x.FullName).HasMaxLength(160);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Product>(entity =>
        {
            entity.HasIndex(x => new { x.BranchId, x.Name });
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Category)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InventoryLocation>(entity =>
        {
            entity.HasIndex(x => new { x.BranchId, x.Name }).IsUnique().HasDatabaseName("UX_InventoryLocations_BranchId_Name");
            entity.HasIndex(x => new { x.BranchId, x.IsActive }).HasDatabaseName("IX_InventoryLocations_BranchId_IsActive");
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InventoryItem>(entity =>
        {
            entity.HasIndex(x => new { x.BranchId, x.Name, x.Unit }).IsUnique().HasDatabaseName("UX_InventoryItems_BranchId_Name_Unit");
            entity.HasIndex(x => new { x.BranchId, x.IsActive }).HasDatabaseName("IX_InventoryItems_BranchId_IsActive");
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReorderLevel).HasPrecision(18, 3);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InventoryStock>(entity =>
        {
            entity.ToTable(t => t.HasCheckConstraint("CK_InventoryStocks_Quantity_NonNegative", "\"Quantity\" >= 0"));
            entity.Property<uint>("xmin").IsRowVersion();
            entity.HasIndex(x => new { x.InventoryItemId, x.InventoryLocationId }).IsUnique().HasDatabaseName("UX_InventoryStocks_Item_Location");
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.AverageUnitCost).HasPrecision(18, 4);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.InventoryItem)
                .WithMany(x => x.Stocks)
                .HasForeignKey(x => x.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.InventoryLocation)
                .WithMany()
                .HasForeignKey(x => x.InventoryLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InventoryMovement>(entity =>
        {
            entity.HasIndex(x => x.InventoryItemId).HasDatabaseName("IX_InventoryMovements_InventoryItemId");
            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_InventoryMovements_CreatedAt");
            entity.HasIndex(x => x.MovementType).HasDatabaseName("IX_InventoryMovements_MovementType");
            entity.HasIndex(x => new { x.ReferenceType, x.ReferenceId }).HasDatabaseName("IX_InventoryMovements_Reference");
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 4);
            entity.Property(x => x.TotalCost).HasPrecision(18, 2);
            entity.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.ReferenceType).HasMaxLength(80);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.InventoryItem)
                .WithMany(x => x.Movements)
                .HasForeignKey(x => x.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.FromLocation)
                .WithMany()
                .HasForeignKey(x => x.FromLocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ToLocation)
                .WithMany()
                .HasForeignKey(x => x.ToLocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<KitchenRequest>(entity =>
        {
            entity.HasIndex(x => x.RequestNumber).IsUnique().HasDatabaseName("UX_KitchenRequests_RequestNumber");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_KitchenRequests_Status");
            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_KitchenRequests_CreatedAt");
            entity.Property(x => x.RequestNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RequestedByUser)
                .WithMany()
                .HasForeignKey(x => x.RequestedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ApprovedByUser)
                .WithMany()
                .HasForeignKey(x => x.ApprovedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<KitchenRequestDetail>(entity =>
        {
            entity.Property(x => x.RequestedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ApprovedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.DispatchedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.Note).HasMaxLength(300);
            entity.HasOne(x => x.KitchenRequest)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.KitchenRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.InventoryItem)
                .WithMany()
                .HasForeignKey(x => x.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Recipe>(entity =>
        {
            entity.HasIndex(x => x.ProductId).HasDatabaseName("IX_Recipes_ProductId");
            entity.HasIndex(x => x.ProductId)
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE")
                .HasDatabaseName("UX_Recipes_ProductId_Active");
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.Recipes)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasIndex(x => new { x.RecipeId, x.InventoryItemId }).IsUnique().HasDatabaseName("UX_RecipeIngredients_RecipeId_Item");
            entity.Property(x => x.QuantityRequired).HasPrecision(18, 3);
            entity.HasOne(x => x.Recipe)
                .WithMany(x => x.Ingredients)
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.InventoryItem)
                .WithMany(x => x.RecipeIngredients)
                .HasForeignKey(x => x.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExpenseCategory>(entity =>
        {
            entity.HasIndex(x => new { x.BranchId, x.Name }).IsUnique().HasDatabaseName("UX_ExpenseCategories_BranchId_Name");
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OperationalExpense>(entity =>
        {
            entity.HasIndex(x => x.ExpenseDate).HasDatabaseName("IX_OperationalExpenses_ExpenseDate");
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ExpenseCategory)
                .WithMany(x => x.Expenses)
                .HasForeignKey(x => x.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Supplier>(entity =>
        {
            entity.HasIndex(x => x.Name);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40);
        });

        builder.Entity<Purchase>(entity =>
        {
            entity.HasIndex(x => x.PublicId).IsUnique().HasDatabaseName("UX_Purchases_PublicId");
            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("UX_Purchases_IdempotencyKey");
            entity.HasIndex(x => new { x.SupplierId, x.InvoiceNumber })
                .IsUnique()
                .HasFilter("\"InvoiceNumber\" IS NOT NULL")
                .HasDatabaseName("UX_Purchases_SupplierId_InvoiceNumber");
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => x.TerminalId);
            entity.HasIndex(x => x.UserSessionId);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120);
            entity.Property(x => x.InvoiceNumber).HasMaxLength(80);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UserSession)
                .WithMany()
                .HasForeignKey(x => x.UserSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PerformedByUser)
                .WithMany()
                .HasForeignKey(x => x.PerformedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.Purchases)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseItem>(entity =>
        {
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => new { x.PurchaseId, x.InventoryItemId });
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Purchase)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.InventoryItem)
                .WithMany()
                .HasForeignKey(x => x.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Customer>(entity =>
        {
            entity.HasIndex(x => new { x.BranchId, x.PhoneNumber }).IsUnique().HasDatabaseName("UX_Customers_BranchId_PhoneNumber");
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Order>(entity =>
        {
            entity.HasIndex(x => x.PublicId).IsUnique().HasDatabaseName("UX_Orders_PublicId");
            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("UX_Orders_IdempotencyKey");
            entity.HasIndex(x => new { x.BranchId, x.OrderNumber }).IsUnique().HasDatabaseName("UX_Orders_BranchId_OrderNumber");
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => x.TerminalId);
            entity.HasIndex(x => x.UserSessionId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.BranchId, x.CompletedAt, x.OrderStatus }).HasDatabaseName("IX_Orders_BranchId_CompletedAt_OrderStatus");
            entity.HasIndex(x => x.OrderStatus);
            entity.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120);
            entity.Property(x => x.OrderType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.OrderStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.TableNumber).HasMaxLength(40);
            entity.Property(x => x.TerminalName).HasMaxLength(120);
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UserSession)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.UserSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Cashier)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CashierId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrderItem>(entity =>
        {
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => new { x.OrderId, x.ProductId });
            entity.Property(x => x.ProductNameSnapshot).HasMaxLength(160).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserSession>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_UserSessions_OpeningCashAmount_NonNegative", "\"OpeningCashAmount\" >= 0");
                t.HasCheckConstraint("CK_UserSessions_CountedClosingCash_NonNegative", "\"CountedClosingCash\" IS NULL OR \"CountedClosingCash\" >= 0");
            });
            entity.HasIndex(x => x.PublicId).IsUnique().HasDatabaseName("UX_UserSessions_PublicId");
            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("UX_UserSessions_IdempotencyKey");
            entity.HasIndex(x => x.CloseIdempotencyKey)
                .IsUnique()
                .HasFilter("\"CloseIdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("UX_UserSessions_CloseIdempotencyKey");
            entity.HasIndex(x => x.SessionCode).IsUnique().HasDatabaseName("UX_UserSessions_SessionCode");
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.HasIndex(x => new { x.Status, x.StartedAt }).HasDatabaseName("IX_UserSessions_Status_StartedAt");
            entity.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("IX_UserSessions_BranchId_Status");
            entity.HasIndex(x => x.TerminalId);
            entity.HasIndex(x => x.UserId)
                .IsUnique()
                .HasFilter("\"Status\" IN ('Active', 'Reopened', 'ClosingPending')")
                .HasDatabaseName("UX_UserSessions_UserId_Active");
            entity.HasIndex(x => x.TerminalId)
                .IsUnique()
                .HasFilter("\"Status\" IN ('Active', 'Reopened', 'ClosingPending')")
                .HasDatabaseName("UX_UserSessions_TerminalId_Active");
            entity.HasIndex(x => new { x.UserId, x.BranchId })
                .IsUnique()
                .HasFilter("\"Status\" IN ('Active', 'Reopened', 'ClosingPending')")
                .HasDatabaseName("UX_UserSessions_UserId_BranchId_Active");
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.SessionCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120);
            entity.Property(x => x.CloseIdempotencyKey).HasMaxLength(120);
            entity.Property(x => x.RoleName).HasMaxLength(40).IsRequired();
            entity.Property(x => x.TerminalName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.OpeningCashAmount).HasPrecision(18, 2);
            entity.Property(x => x.CountedClosingCash).HasPrecision(18, 2);
            entity.Property(x => x.ExpectedClosingCash).HasPrecision(18, 2);
            entity.Property(x => x.CashDifference).HasPrecision(18, 2);
            entity.Property(x => x.ReopenReason).HasMaxLength(500);
            entity.HasOne(x => x.User)
                .WithMany(x => x.UserSessions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ClosedByUser)
                .WithMany()
                .HasForeignKey(x => x.ClosedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ReopenedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReopenedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Terminal>(entity =>
        {
            entity.HasIndex(x => x.TerminalCode).IsUnique().HasDatabaseName("UX_Terminals_TerminalCode");
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => new { x.BranchId, x.IsActive }).HasDatabaseName("IX_Terminals_BranchId_IsActive");
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(60);
            entity.Property(x => x.TerminalTokenHash).HasMaxLength(128);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TerminalHeartbeat>(entity =>
        {
            entity.HasIndex(x => x.TerminalId).IsUnique().HasDatabaseName("UX_TerminalHeartbeats_TerminalId");
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => new { x.BranchId, x.LastSeenAt }).HasDatabaseName("IX_TerminalHeartbeats_BranchId_LastSeenAt");
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CurrentUser)
                .WithMany()
                .HasForeignKey(x => x.CurrentUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CurrentSession)
                .WithMany()
                .HasForeignKey(x => x.CurrentSessionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<UserSessionHeartbeat>(entity =>
        {
            entity.HasIndex(x => x.UserSessionId).IsUnique().HasDatabaseName("UX_UserSessionHeartbeats_UserSessionId");
            entity.Property(x => x.TerminalName).HasMaxLength(120);
            entity.HasOne(x => x.UserSession)
                .WithMany()
                .HasForeignKey(x => x.UserSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.EntityName, x.EntityId });
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.EventType, x.CreatedAt }).HasDatabaseName("IX_AuditLogs_EventType_CreatedAt");
            entity.HasIndex(x => new { x.IpAddress, x.CreatedAt }).HasDatabaseName("IX_AuditLogs_IpAddress_CreatedAt");
            entity.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("IX_AuditLogs_UserId_CreatedAt");
            entity.HasIndex(x => new { x.Severity, x.CreatedAt }).HasDatabaseName("IX_AuditLogs_Severity_CreatedAt");
            entity.Property(x => x.Action).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(120);
            entity.Property(x => x.Severity).HasMaxLength(40);
            entity.Property(x => x.Message).HasMaxLength(500);
            entity.Property(x => x.AttemptedUserName).HasMaxLength(256);
            entity.Property(x => x.EntityName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.OldValues).HasColumnType("jsonb");
            entity.Property(x => x.NewValues).HasColumnType("jsonb");
            entity.Property(x => x.IpAddress).HasMaxLength(80);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_IdempotencyRecords_IdempotencyKey");
            entity.HasIndex(x => new { x.OperationType, x.IdempotencyKey }).IsUnique().HasDatabaseName("UX_IdempotencyRecords_OperationType_IdempotencyKey");
            entity.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("IX_IdempotencyRecords_Status_CreatedAt");
            entity.HasIndex(x => x.ExpiresAt).HasDatabaseName("IX_IdempotencyRecords_ExpiresAt");
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.OperationType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RequestHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ResourceType).HasMaxLength(80);
            entity.Property(x => x.ResponseBodySummary).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.HasSequence<long>("SessionCodeSequence");

        builder.Entity<Branch>().HasData(new Branch
        {
            Id = 1,
            BranchCode = "MAIN",
            Name = "Main Branch",
            Address = "Local Branch",
            Phone = "",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        builder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Food", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Category { Id = 2, Name = "Beverages", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Category { Id = 3, Name = "Desserts", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        builder.Entity<Terminal>().HasData(new Terminal
        {
            Id = 1,
            BranchId = 1,
            TerminalCode = "MAIN-01",
            Name = "Main Terminal",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.Entity<InventoryLocation>().HasData(
            new InventoryLocation { Id = 1, BranchId = 1, Name = "Stock Room", IsActive = true, CreatedAt = seedDate },
            new InventoryLocation { Id = 2, BranchId = 1, Name = "Kitchen", IsActive = true, CreatedAt = seedDate });

        builder.Entity<InventoryItem>().HasData(
            new InventoryItem { Id = 1, BranchId = 1, Name = "Coca-Cola", Unit = "0.5L Bottle", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 2, BranchId = 1, Name = "Coca-Cola", Unit = "1L Bottle", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 3, BranchId = 1, Name = "Coca-Cola", Unit = "1.5L Bottle", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 4, BranchId = 1, Name = "Coca-Cola", Unit = "300ML Bottle", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 5, BranchId = 1, Name = "Aluminium Foil", Unit = "Roll", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 6, BranchId = 1, Name = "BBQ Sauce", Unit = "Bottle", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 7, BranchId = 1, Name = "Black Olives", Unit = "Tin", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 8, BranchId = 1, Name = "Cheese", Unit = "Kg", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 9, BranchId = 1, Name = "Chicken Patty", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 10, BranchId = 1, Name = "Cling Film", Unit = "Roll", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 11, BranchId = 1, Name = "Cooking Oil", Unit = "Liter", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 12, BranchId = 1, Name = "Eka", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 13, BranchId = 1, Name = "F1 Packing", Unit = "Piece", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 14, BranchId = 1, Name = "F2 Packing", Unit = "Piece", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 15, BranchId = 1, Name = "Food Bag Large", Unit = "Piece", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 16, BranchId = 1, Name = "Forks", Unit = "Piece", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 17, BranchId = 1, Name = "Fries", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 18, BranchId = 1, Name = "Fry Oil", Unit = "Tin", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 19, BranchId = 1, Name = "Gas Cylinder", Unit = "Refill", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 20, BranchId = 1, Name = "Ice Sugar", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 21, BranchId = 1, Name = "Imli Sauce", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 22, BranchId = 1, Name = "Jalapeño", Unit = "Jar", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 23, BranchId = 1, Name = "Mayonnaise", Unit = "Liter", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 24, BranchId = 1, Name = "Medium Food Bags", Unit = "Piece", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 25, BranchId = 1, Name = "Mushrooms", Unit = "Tin", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 26, BranchId = 1, Name = "Mustard Sauce", Unit = "Liter", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 27, BranchId = 1, Name = "Nido", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 28, BranchId = 1, Name = "Nuggets", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 29, BranchId = 1, Name = "Paratha", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 30, BranchId = 1, Name = "Pepperoni", Unit = "Kg", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 31, BranchId = 1, Name = "Peri Peri Sauce", Unit = "Bottle", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 32, BranchId = 1, Name = "Pizza Sauce", Unit = "Liter", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 33, BranchId = 1, Name = "Pizza Table", Unit = "Piece", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 34, BranchId = 1, Name = "Printer Rolls", Unit = "Roll", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 35, BranchId = 1, Name = "Sandwich Packing", Unit = "Piece", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 36, BranchId = 1, Name = "Sausages", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 37, BranchId = 1, Name = "Sweet Corn", Unit = "Tin", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 38, BranchId = 1, Name = "Tomato Chilly Sachet", Unit = "Piece", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 39, BranchId = 1, Name = "Tape Roll", Unit = "Roll", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 40, BranchId = 1, Name = "Tikka Masala", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 41, BranchId = 1, Name = "Tissue Roll", Unit = "Roll", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 42, BranchId = 1, Name = "Yeast", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new InventoryItem { Id = 43, BranchId = 1, Name = "Zinger Recipe Masala", Unit = "Packet", ReorderLevel = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate });

        builder.Entity<ExpenseCategory>().HasData(
            new ExpenseCategory { Id = 1, BranchId = 1, Name = "Rent", IsActive = true, CreatedAt = seedDate },
            new ExpenseCategory { Id = 2, BranchId = 1, Name = "Electricity", IsActive = true, CreatedAt = seedDate },
            new ExpenseCategory { Id = 3, BranchId = 1, Name = "Gas", IsActive = true, CreatedAt = seedDate },
            new ExpenseCategory { Id = 4, BranchId = 1, Name = "Salaries", IsActive = true, CreatedAt = seedDate },
            new ExpenseCategory { Id = 5, BranchId = 1, Name = "Internet", IsActive = true, CreatedAt = seedDate },
            new ExpenseCategory { Id = 6, BranchId = 1, Name = "Repair", IsActive = true, CreatedAt = seedDate },
            new ExpenseCategory { Id = 7, BranchId = 1, Name = "Cleaning", IsActive = true, CreatedAt = seedDate },
            new ExpenseCategory { Id = 8, BranchId = 1, Name = "Transport", IsActive = true, CreatedAt = seedDate },
            new ExpenseCategory { Id = 9, BranchId = 1, Name = "Miscellaneous", IsActive = true, CreatedAt = seedDate });
    }

    public override int SaveChanges()
    {
        ApplyTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Properties.Any(x => x.Metadata.Name == "CreatedAt"))
            {
                entry.Property("CreatedAt").CurrentValue = now;
            }

            if ((entry.State == EntityState.Added || entry.State == EntityState.Modified) &&
                entry.Properties.Any(x => x.Metadata.Name == "UpdatedAt"))
            {
                entry.Property("UpdatedAt").CurrentValue = now;
            }
        }
    }
}
